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
}
