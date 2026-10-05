using DesktopInk.Infrastructure;
using FluentAssertions;
using Xunit;

namespace DesktopInk.Tests.Infrastructure;

public class AppSettingsTests
{
    [Fact]
    public void Load_ShouldCreateDefaults_WhenFileMissing()
    {
        var tempDir = CreateTempDirectory();
        var path = Path.Combine(tempDir, "settings.json");

        var settings = AppSettings.Load(path);

        settings.Palette.Left.Should().BeNull();
        settings.Palette.Top.Should().BeNull();
        settings.Hotkeys.ClearAll.Should().BeNull();
        File.Exists(path).Should().BeTrue();
    }

    [Fact]
    public void Save_ShouldPersistValues()
    {
        var tempDir = CreateTempDirectory();
        var path = Path.Combine(tempDir, "settings.json");

        var settings = new AppSettings
        {
            Palette = new PaletteSettings { Left = 331, Top = 762 },
            Hotkeys = new HotkeySettings { ClearAll = "Win+Shift+X" },
        };

        settings.Save(path);

        var loaded = AppSettings.Load(path);
        loaded.Palette.Left.Should().Be(331);
        loaded.Palette.Top.Should().Be(762);
        loaded.Hotkeys.ClearAll.Should().Be("Win+Shift+X");
    }

    [Fact]
    public void Load_ShouldIgnoreRemovedVersionCheckSection()
    {
        var tempDir = CreateTempDirectory();
        var path = Path.Combine(tempDir, "settings.json");
        File.WriteAllText(path, """
            {
              "versionCheck": { "enabled": false, "skippedVersion": "1.4.2" },
              "palette": { "left": 331, "top": 762 }
            }
            """);

        var settings = AppSettings.Load(path);

        settings.Palette.Left.Should().Be(331);
        settings.Palette.Top.Should().Be(762);
    }

    private static string CreateTempDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "DesktopInkTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
