using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DesktopInk.Core;

namespace DesktopInk.Infrastructure;

public sealed class AppSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    [JsonPropertyName("versionCheck")]
    public VersionCheckSettings VersionCheck { get; set; } = new();

    [JsonPropertyName("palette")]
    public PaletteSettings Palette { get; set; } = new();

    [JsonPropertyName("hotkeys")]
    public HotkeySettings Hotkeys { get; set; } = new();

    public static AppSettings Load(string? pathOverride = null)
    {
        var path = pathOverride ?? ResolveSettingsPath();

        try
        {
            if (!File.Exists(path))
            {
                var settings = new AppSettings();
                settings.Save(path);
                return settings;
            }

            var json = File.ReadAllText(path, Encoding.UTF8);
            var settingsFromFile = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            settingsFromFile.VersionCheck ??= new VersionCheckSettings();
            settingsFromFile.Palette ??= new PaletteSettings();
            settingsFromFile.Hotkeys ??= new HotkeySettings();
            return settingsFromFile;
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to load settings. Falling back to defaults.", ex);
            var settings = new AppSettings();
            settings.Save(path);
            return settings;
        }
    }

    public void Save(string? pathOverride = null)
    {
        var path = pathOverride ?? ResolveSettingsPath();

        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(path, json, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to save settings.", ex);
        }
    }

    internal static string ResolveSettingsPath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(root, "DesktopInk", "settings.json");
    }
}

public sealed class VersionCheckSettings
{
    public bool Enabled { get; set; } = false;

    public string? SkippedVersion { get; set; }

    public DateTime? LastChecked { get; set; }
}

public sealed class PaletteSettings
{
    public double? Left { get; set; }

    public double? Top { get; set; }
}

/// <summary>
/// Optional hotkey overrides, e.g. "Win+Shift+D". When an action is left unset, a
/// built-in list is tried in order and the first combination Windows accepts is used,
/// so one shortcut taken by another app does not leave the action unbound.
/// </summary>
public sealed class HotkeySettings
{
    private static readonly IReadOnlyDictionary<HotkeyAction, string[]> Defaults = new Dictionary<HotkeyAction, string[]>
    {
        [HotkeyAction.ToggleDraw] = ["Win+Shift+D", "Ctrl+Alt+Shift+D"],
        [HotkeyAction.ClearAll] = ["Win+Shift+C", "Win+Shift+X", "Ctrl+Alt+Shift+C"],
        [HotkeyAction.Quit] = ["Win+Shift+Q", "Ctrl+Alt+Shift+Q"],
    };

    public string? ToggleDraw { get; set; }

    public string? ClearAll { get; set; }

    public string? Quit { get; set; }

    public IReadOnlyList<string> GetCandidates(HotkeyAction action)
    {
        var configured = action switch
        {
            HotkeyAction.ToggleDraw => ToggleDraw,
            HotkeyAction.ClearAll => ClearAll,
            HotkeyAction.Quit => Quit,
            _ => null,
        };

        return string.IsNullOrWhiteSpace(configured) ? Defaults[action] : [configured];
    }
}
