# DesktopInk

Lightweight, always-on-top screen-annotation overlay for Windows. Draw, highlight, shape, and arrow on top of anything — browsers, slides, videos, live screenshares. Built for presenters, course creators, and anyone who wants a quick pointing/annotation layer over the desktop.

This is a fork of [atman-33/desktop-ink](https://github.com/atman-33/desktop-ink) with added tools, a redesigned palette, and a proper Windows installer.

## Install

Grab the latest installer from the [Releases page](https://github.com/andronia/desktop-drawer/releases/latest) and double-click it.

- Per-user install — no admin prompt, no UAC
- ~52 MB installer, no .NET install required (runtime is bundled)
- Shows up normally in **Settings → Apps → Installed apps** with a clean uninstaller

> **SmartScreen:** on first launch Windows shows "unknown publisher" because the installer isn't code-signed. Click **More info → Run anyway**. The app makes zero network requests by default.

## Features

**Drawing tools**
- **Pen** — freehand, hold `Shift` for a straight line at any angle
- **Highlighter** — translucent, thicker stroke for highlighting text or regions
- **Rectangle** — drag to draw outline; hold `Shift` to constrain to a square
- **Arrow** — click-drag to draw a straight arrow pointing where you release

**Presentation helpers**
- **Cursor spotlight** — a translucent yellow circle follows your cursor so viewers can track where you're pointing
- **Auto-fade** — strokes fade out ~3 seconds after you lift the pen (Epic Pen style)

**Styling**
- **9-color palette** in a 3×3 grid: red, blue, green, yellow, white, magenta, orange, cyan, black. Direct-select, no cycling.
- **Brush thickness slider** — 1 to 10, adjustable by drag or mouse wheel

**App UX**
- System-tray icon with show/hide, toggle draw, clear, quit
- Single-instance guard (second launch activates the running instance)
- Taskbar entry + Alt-Tab presence
- Palette position persists across launches
- Full multi-monitor support with per-monitor DPI

## Hotkeys

| Key | Action |
|---|---|
| `Win+Shift+D` | Toggle draw mode |
| `Win+Shift+C` | Clear all strokes |
| `Win+Shift+Q` | Quit the app |
| `Alt` (double-tap + hold) | Temporary draw mode (auto-clears on release) |
| `Alt+S` | Cycle pen color (in temporary draw mode) |
| `Shift` while drawing | Straight line (pen) or square (rectangle) |

## Palette overview

From top to bottom:

1. Draw mode toggle (blue when active)
2. Highlighter toggle (amber when active)
3. Rectangle toggle (amber when active)
4. Arrow toggle (amber when active)
5. Auto-fade toggle (teal when active)
6. Cursor spotlight toggle (purple when active)
7. Thickness slider (1–10, mouse-wheel adjustable)
8. 3×3 color grid (active color shows a white ring)
9. Clear all
10. Quit

The palette is draggable — move it to any monitor; drawing will engage on the monitor the palette is currently on.

## Privacy

Fully offline. No telemetry, no analytics, no crash reporting, no external dependencies that call home. The upstream GitHub update-check is **disabled by default** in this fork. If you want to re-enable it, edit `%APPDATA%\DesktopInk\settings.json` and set `versionCheck.enabled` to `true` (though it points at the original upstream repo, not this fork).

The app writes exactly one file to disk: `%APPDATA%\DesktopInk\settings.json` (stores palette position and update-check preference). That's it.

## Building from source

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bat
scripts\build.cmd       REM Debug + Release build
scripts\run.cmd         REM Debug run
scripts\test.cmd        REM xUnit tests
```

To produce an installer:

```bat
scripts\make-installer.cmd
```

Output: `publish\installer\DesktopInkSetup-{version}.exe`. Requires [Inno Setup 6](https://jrsoftware.org/isdl.php) (installable via `winget install JRSoftware.InnoSetup`).

## Architecture

- **Overlay windows** — one transparent, click-through, always-on-top window per monitor. Drawing happens on a WPF `Canvas` child.
- **Control palette** — floating draggable window (`ControlWindow`) with the toolbar UI.
- **Tray manager** — `System.Windows.Forms.NotifyIcon` for the system tray + context menu.
- **Keyboard hook** — low-level `WH_KEYBOARD_LL` hook for the Alt double-tap temporary-mode gesture.
- **Overlay manager** — fan-out controller that routes palette commands to the per-monitor overlays.

The stack is **C# / WPF / .NET 10**, Windows-only. No third-party runtime dependencies beyond `System.Text.Json`.

## Credits

- Upstream: [atman-33/desktop-ink](https://github.com/atman-33/desktop-ink) (MIT) — the original transparent-overlay foundation, multi-monitor DPI handling, and keyboard-hook temporary-mode gesture.
- This fork adds highlighter/rectangle/arrow tools, cursor spotlight, auto-fade, 9-color palette, thickness slider, tray icon, single-instance guard, persistent palette position, and Inno Setup packaging.

## License

MIT — see [LICENSE](LICENSE). Upstream copyright is preserved in that file per the MIT terms.
