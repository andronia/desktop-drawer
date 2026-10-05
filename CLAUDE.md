# DesktopInk

Always-on-top screen-annotation overlay for Windows: draw, highlight, rectangles, arrows, cursor spotlight. Fork of `atman-33/desktop-ink`; this fork ships its own GitHub releases with an installer. User-facing docs: `README.md`.

OpenSpec instructions live in @AGENTS.md.

## Stack

C# / WPF / .NET 10, Windows only. WinForms is referenced only for the tray icon (`NotifyIcon`). No runtime dependencies beyond the framework. Tests: xUnit + FluentAssertions.

## Commands

| Task | Command |
|---|---|
| Build (Debug + Release) | `scripts\build.cmd` |
| Test | `scripts\test.cmd` or `dotnet test desktop-ink.slnx -c Release` |
| Run (Debug) | `scripts\run.cmd` |
| Build installer | `scripts\make-installer.cmd` → `publish\installer\DesktopInkSetup-<version>.exe` (needs Inno Setup 6, installed per-user) |

Run tests in both `-c Debug` and `-c Release`: `AppLog` and its tests behave differently per configuration.

## Layout

```
src/DesktopInk/
  Core/            UI-free logic: OverlayManager, KeyboardHookManager, HotkeyGesture, enums
  Infrastructure/  Win32 interop, settings, logging, tray icon, monitor enumeration, hotkey registration
  Windows/         WPF windows: ControlWindow (palette), OverlayWindow (one per monitor)
src/DesktopInk.Tests/   mirrors the Core/ and Infrastructure/ folders
installer/       Inno Setup script
scripts/         build/test/run/publish/installer .cmd wrappers
openspec/specs/  living behaviour specs, one folder per capability
docs/            RELEASE.md (release procedure)
notes/           gitignored: open questions, decisions in progress, anything naming people
```

New files follow this split: pure logic in `Core/`, anything touching Win32 or disk in `Infrastructure/`, windows in `Windows/`. Tests go in the matching test folder.

## Architecture

- `OverlayManager` owns one `OverlayWindow` per monitor (via `IOverlayWindow`, so tests use fakes). Draw mode is enabled only on the overlay whose monitor contains the palette; all other overlays stay click-through (`WS_EX_TRANSPARENT`).
- On display changes, overlays are reconciled by monitor bounds, debounced by 500 ms. Overlays of unchanged monitors (and their strokes) survive. `OverlaysRefreshed` tells the palette to re-check its position, z-order and monitor.
- `ControlWindow` is the palette. It owns the global hotkeys and the low-level keyboard hook (double-tap-and-hold Alt = temporary draw mode, Alt+S = cycle colour).
- Settings: `%APPDATA%\DesktopInk\settings.json` (palette position in physical pixels, optional `hotkeys` overrides). Unknown keys are ignored, so removed settings stay harmless.
- Log: `%LOCALAPPDATA%\DesktopInk\desktopink.log`. Release builds log errors only; Debug builds also log verbose Info. 1 MB cap with one `.old.log`.
- The app makes no network requests. Keep it that way unless asked.

## Mixed-DPI rules (do not "simplify" these away)

The app is PerMonitorV2 DPI-aware (`app.manifest`). Monitors can have different scale factors (e.g. 5K @ 200% next to 1440p @ 100%). Each rule below fixes a real bug:

- **Never set `handled = true` for `WM_DPICHANGED`.** WPF must see it to rescale its content. Swallowing it caused a clipped or tiny palette after crossing monitors.
- **Overlays are pinned to their monitor's physical pixel bounds.** `OverlayWindow` rewrites every `WM_WINDOWPOSCHANGING` and the `WM_DPICHANGED` suggested rect to `_boundsPx`, which never changes for the window's lifetime. Never adopt the DPI-scaled suggested rect: it breaks the bounds matching `OverlayManager` relies on.
- **Overlays must not re-assert `HWND_TOPMOST`** (always pass `SWP_NOZORDER`). Otherwise they rise above the palette and swallow its clicks in draw mode.
- **Position the palette in physical pixels with `SWP_NOSIZE`** and let WPF size it for the destination DPI (`ControlWindow.PlaceOnMonitor`). Do not write WPF `Left/Top/Width/Height` from pixel math.
- Map screen pixels to WPF coordinates with `Visual.PointFromScreen`, not by dividing by a cached DPI.

Unit tests cannot catch DPI regressions. Verify window-geometry changes live on a mixed-DPI setup: check each window's `GetWindowRect`/`GetDpiForWindow` from an external DPI-aware process and screenshot the palette on each monitor.

## Hotkeys

Each action tries a list of gestures and uses the first one Windows accepts (`HotkeySettings` defaults: Clear = `Win+Shift+C`, then `Win+Shift+X`, …). A settings override replaces the list. Hotkey ids are the `HotkeyAction` enum values. When no candidate is free, the app shows one tray notification, never a modal dialog. Palette tooltips are set in code from the bound gestures, so don't hardcode shortcuts in XAML.

## Gotchas

- **Single instance:** a second launch signals the running one and exits. Quit the installed app (`%LOCALAPPDATA%\Programs\DesktopInk`) before running a dev build, or the dev build exits immediately.
- `AppLog.LogPath` is process-wide static state. Tests that redirect it belong to the non-parallel `AppLogTests` collection.
- `Assembly.Location` is empty in single-file publishes. Read version info from assembly attributes instead.
- The keyboard hook callback must return fast: Windows silently unhooks slow low-level hooks. Raise events via `Dispatcher.BeginInvoke`.

## Release

Follow `docs/RELEASE.md` (the single source for the procedure). Essentials: the version lives only in the csproj; build and smoke-test the installer before pushing the tag; every GitHub Release carries the installer, because the README points users to it.

## Conventions

- Specs: bug fixes update `openspec/specs/*` directly; new capabilities go through an OpenSpec change proposal (see `openspec/AGENTS.md`).
- Docs describe the present. No placeholder or stub files, no "changed from" notes: git history is the archive. `openspec/changes/archive/` is the append-only record and is never edited.
- One canonical doc per topic: the release procedure lives only in `docs/RELEASE.md`; architecture and conventions live here.
- Commits: conventional prefixes (`feat:`, `fix:`, `chore:`), with a summary body for releases.
- No personal or company names in tracked files beyond deliberate product content (installer publisher, LICENSE, release URLs). Process notes go in `notes/`.
- Product UI text stays minimal: no explanatory or state-narrating labels the user didn't ask for.
