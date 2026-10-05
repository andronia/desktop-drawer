using System.Text;
using DesktopInk.Infrastructure;
using FluentAssertions;
using Xunit;

namespace DesktopInk.Tests.Infrastructure;

public sealed class AppLogTests : IDisposable
{
    private readonly string _tempDir;

    public AppLogTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "DesktopInkTests", Guid.NewGuid().ToString("N"));
        AppLog.LogPath = Path.Combine(_tempDir, "desktopink.log");
    }

    public void Dispose()
    {
        AppLog.LogPath = AppLog.DefaultLogPath;
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void DefaultLogPath_ShouldBeUnderLocalAppData()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        AppLog.DefaultLogPath.Should().StartWith(localAppData);
        AppLog.DefaultLogPath.Should().EndWith(Path.Combine("DesktopInk", "desktopink.log"));
    }

    [Fact]
    public void Error_ShouldWriteErrorMessageToLogFile()
    {
        var testMessage = $"Error message at {DateTime.Now.Ticks}";

        AppLog.Error(testMessage);

        var logContent = File.ReadAllText(AppLog.LogPath, Encoding.UTF8);
        logContent.Should().Contain("ERROR");
        logContent.Should().Contain(testMessage);
    }

    [Fact]
    public void Error_WithException_ShouldWriteExceptionDetails()
    {
        var testMessage = $"Error with exception at {DateTime.Now.Ticks}";
        var exception = new InvalidOperationException("Test exception");

        AppLog.Error(testMessage, exception);

        var logContent = File.ReadAllText(AppLog.LogPath, Encoding.UTF8);
        logContent.Should().Contain(testMessage);
        logContent.Should().Contain("InvalidOperationException");
        logContent.Should().Contain("Test exception");
    }

    [Fact]
    public void Error_ShouldRotateLogWhenOverSizeCap()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(AppLog.LogPath, new string('x', 1024 * 1024 + 1));

        AppLog.Error("after rotation");

        new FileInfo(AppLog.LogPath).Length.Should().BeLessThan(1024);
        File.Exists(Path.Combine(_tempDir, "desktopink.old.log")).Should().BeTrue();
    }

#if DEBUG
    [Fact]
    public void Info_ShouldWriteToLogFile()
    {
        var testMessage = $"Test message at {DateTime.Now.Ticks}";

        AppLog.Info(testMessage);

        var logContent = File.ReadAllText(AppLog.LogPath, Encoding.UTF8);
        logContent.Should().Contain("INFO");
        logContent.Should().Contain(testMessage);
    }
#endif

#if RELEASE
    [Fact]
    public void Info_InReleaseMode_ShouldNotWriteToLogFile()
    {
        var testMessage = $"Release test at {DateTime.Now.Ticks}";

        AppLog.Info(testMessage);

        // Other test classes may log errors concurrently, so check for this message only.
        var logContent = File.Exists(AppLog.LogPath) ? File.ReadAllText(AppLog.LogPath, Encoding.UTF8) : string.Empty;
        logContent.Should().NotContain(testMessage);
    }
#endif
}
