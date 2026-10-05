using System;
using System.IO;
using System.Text;

namespace DesktopInk.Infrastructure;

/// <summary>
/// File log under %LOCALAPPDATA%\DesktopInk. Errors are always written; Info is Debug-only
/// because it traces keyboard-hook and window-geometry events at high volume.
/// </summary>
internal static class AppLog
{
    private const long MaxLogBytes = 1024 * 1024;

    private static readonly object LockObj = new();

    internal static string DefaultLogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DesktopInk",
        "desktopink.log");

    /// <summary>Settable so tests can redirect output away from the real log.</summary>
    internal static string LogPath { get; set; } = DefaultLogPath;

    internal static void Info(string message)
    {
#if DEBUG
        Write("INFO", message);
#endif
    }

    internal static void Error(string message, Exception? ex = null)
    {
        var full = ex is null ? message : message + "\n" + ex;
        Write("ERROR", full);
    }

    private static void Write(string level, string message)
    {
        try
        {
            var line = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] {level} {message}";

            lock (LockObj)
            {
                var path = LogPath;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                RotateIfTooLarge(path);
                File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch
        {
            // Never crash due to logging.
        }
    }

    private static void RotateIfTooLarge(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length < MaxLogBytes)
        {
            return;
        }

        File.Move(path, Path.ChangeExtension(path, ".old.log"), overwrite: true);
    }
}
