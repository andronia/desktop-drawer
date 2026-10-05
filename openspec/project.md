# Project Context

## Purpose
DesktopInk is an always-on-top screen-annotation overlay for Windows. Presenters and course creators draw, highlight, frame and point at anything on screen (slides, browsers, video, screen shares) without switching apps, then clear it in one action.

## Tech Stack
- C# / WPF on .NET 10, Windows only (`net10.0-windows`)
- WinForms only for the system-tray `NotifyIcon`
- Win32 interop for click-through layered windows, global hotkeys, a low-level keyboard hook and per-monitor DPI
- xUnit + FluentAssertions for tests; Inno Setup 6 for the installer; GitHub Actions for CI and releases

## Project Conventions
Code layout, architecture, the mixed-DPI rules, hotkey handling, commit style and test commands are documented in `CLAUDE.md` at the repository root. The release procedure is in `docs/RELEASE.md`.

Specs in `openspec/specs/` describe current behaviour. Bug fixes that restore or clarify behaviour update the specs directly; new capabilities go through a change proposal.

## Domain Context
- **Draw mode** makes the overlay on the palette's monitor capture the mouse; **pass-through mode** lets every click reach the apps underneath.
- **Temporary draw mode** is held with a double-tap of Alt and clears its strokes on release, for quick pointing during a presentation.
- One overlay window exists per monitor. The palette's position decides which monitor accepts drawing.

## Important Constraints
- No network access: no telemetry, no update check.
- Per-user install without admin rights; settings in `%APPDATA%\DesktopInk`, error log in `%LOCALAPPDATA%\DesktopInk`.
- Must behave correctly on monitors with different scale factors (PerMonitorV2).
- Minimal on-screen text: no explanatory or state-narrating labels beyond the draw-mode indicator.

## External Dependencies
None at runtime beyond Windows and the .NET runtime (bundled in the installer and the self-contained build).
