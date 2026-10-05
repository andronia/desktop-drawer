using System.Threading;
using System.Windows;
using DesktopInk.Core;
using DesktopInk.Infrastructure;
using DesktopInk.Windows;

namespace DesktopInk;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
	// Unique names for single-instance coordination (derived from a fixed GUID).
	private const string SingleInstanceMutexName = "DesktopInk-SingleInstance-7c8d3f12";
	private const string ShowPaletteSignalName = "DesktopInk-ShowPalette-7c8d3f12";

	private OverlayManager? _overlayManager;
	private ControlWindow? _controlWindow;
	private TrayIconManager? _trayIcon;
	private AppSettings? _appSettings;
	private Mutex? _singleInstanceMutex;
	private bool _ownsSingleInstanceMutex;
	private EventWaitHandle? _showPaletteSignal;
	private RegisteredWaitHandle? _showPaletteWait;
	private readonly object _updateDialogGate = new();
	private bool _updateDialogShown;

	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

		ShutdownMode = ShutdownMode.OnExplicitShutdown;
		DispatcherUnhandledException += OnDispatcherUnhandledException;

		AppLog.Info($"Startup cwd='{Environment.CurrentDirectory}' args='{string.Join(' ', e.Args)}'");

		_singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var isFirstInstance);
		_ownsSingleInstanceMutex = isFirstInstance;
		if (!isFirstInstance)
		{
			// Another instance is already running — ask it to show its palette and exit.
			AppLog.Info("Another instance detected; signalling and exiting.");
			try
			{
				using var signal = EventWaitHandle.OpenExisting(ShowPaletteSignalName);
				signal.Set();
			}
			catch (Exception ex)
			{
				AppLog.Error("Failed to signal existing instance.", ex);
			}

			Shutdown();
			return;
		}

		_showPaletteSignal = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, ShowPaletteSignalName);
		_showPaletteWait = ThreadPool.RegisterWaitForSingleObject(
			_showPaletteSignal,
			callBack: (_, _) => Dispatcher.BeginInvoke(() => _trayIcon?.ShowPalette()),
			state: null,
			millisecondsTimeOutInterval: Timeout.Infinite,
			executeOnlyOnce: false);

		_appSettings = AppSettings.Load();

		_overlayManager = new OverlayManager();
		_overlayManager.ShowOverlays();

		_controlWindow = new ControlWindow(_overlayManager, _appSettings);
		_controlWindow.Show();

		_trayIcon = new TrayIconManager(_overlayManager, _controlWindow);

		if (_controlWindow.UnboundHotkeys.Count > 0)
		{
			_trayIcon.ShowWarning(DescribeUnboundHotkeys(_controlWindow.UnboundHotkeys));
		}

		_ = Task.Run(async () =>
		{
			try
			{
				await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
				await CheckForUpdatesAsync().ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				AppLog.Error("Update check task failed.", ex);
			}
		});
	}

	protected override void OnExit(ExitEventArgs e)
	{
		AppLog.Info("Exit");

		_trayIcon?.Dispose();
		_trayIcon = null;

		_controlWindow?.Close();
		_controlWindow = null;

		_overlayManager?.Dispose();
		_overlayManager = null;

		_showPaletteWait?.Unregister(waitObject: null);
		_showPaletteWait = null;

		_showPaletteSignal?.Dispose();
		_showPaletteSignal = null;

		// Only the first instance owns the mutex; releasing an unowned mutex throws.
		if (_ownsSingleInstanceMutex)
		{
			_singleInstanceMutex?.ReleaseMutex();
			_ownsSingleInstanceMutex = false;
		}

		_singleInstanceMutex?.Dispose();
		_singleInstanceMutex = null;

		base.OnExit(e);
	}

	private static void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
	{
		// Keep the overlay alive (and the user's annotations on screen) on a non-fatal UI error.
		AppLog.Error("Unhandled UI exception.", e.Exception);
		e.Handled = true;
	}

	private static string DescribeUnboundHotkeys(IReadOnlyList<HotkeyAction> actions)
	{
		var names = actions.Select(action => action switch
		{
			HotkeyAction.ToggleDraw => "Toggle draw",
			HotkeyAction.ClearAll => "Clear",
			HotkeyAction.Quit => "Quit",
			_ => action.ToString(),
		});

		return $"Shortcut unavailable, already used by another app: {string.Join(", ", names)}.";
	}

	private async Task CheckForUpdatesAsync()
	{
		if (_appSettings is null)
		{
			return;
		}

		var versionSettings = _appSettings.VersionCheck;
		if (!versionSettings.Enabled)
		{
			AppLog.Info("Version check disabled.");
			return;
		}

		var currentVersion = GetCurrentVersion();
		using var checker = new VersionChecker();
		var result = await checker.CheckForUpdatesAsync(currentVersion, versionSettings, CancellationToken.None)
			.ConfigureAwait(false);

		_appSettings.Save();

		if (!result.IsNewVersionAvailable || string.IsNullOrWhiteSpace(result.LatestVersion))
		{
			return;
		}

		if (string.Equals(versionSettings.SkippedVersion, result.LatestVersion, StringComparison.OrdinalIgnoreCase))
		{
			AppLog.Info($"Update skipped for version '{result.LatestVersion}'.");
			return;
		}

		if (!string.IsNullOrWhiteSpace(versionSettings.SkippedVersion) &&
			!string.Equals(versionSettings.SkippedVersion, result.LatestVersion, StringComparison.OrdinalIgnoreCase))
		{
			versionSettings.SkippedVersion = null;
			_appSettings.Save();
		}

		lock (_updateDialogGate)
		{
			if (_updateDialogShown)
			{
				return;
			}

			_updateDialogShown = true;
		}

		await Dispatcher.InvokeAsync(() =>
		{
			var dialog = new UpdateNotificationDialog(currentVersion, result)
			{
				Owner = _controlWindow
			};

			dialog.ShowDialog();

			if (dialog.UserChoice is null)
			{
				return;
			}

			AppLog.Info($"Update dialog action: {dialog.UserChoice}.");

			if (dialog.UserChoice == UserAction.SkipVersion)
			{
				versionSettings.SkippedVersion = result.LatestVersion;
				versionSettings.LastChecked = DateTime.UtcNow;
				_appSettings.Save();
			}
		});
	}

	private static string GetCurrentVersion()
	{
		// Environment variable override for debugging/testing
		var overrideVersion = Environment.GetEnvironmentVariable("DESKTOPINK_DEBUG_VERSION");
		if (!string.IsNullOrWhiteSpace(overrideVersion))
		{
			AppLog.Info($"Using debug version from environment: {overrideVersion}");
			return overrideVersion;
		}

		// Read the attribute rather than the file: Assembly.Location is empty in single-file publishes.
		var fileVersion = typeof(App).Assembly
			.GetCustomAttributes(typeof(System.Reflection.AssemblyFileVersionAttribute), inherit: false)
			.OfType<System.Reflection.AssemblyFileVersionAttribute>()
			.FirstOrDefault()?.Version;

		if (string.IsNullOrWhiteSpace(fileVersion))
		{
			AppLog.Error("Failed to read current version.");
			return "0.0.0";
		}

		return fileVersion;
	}
}

