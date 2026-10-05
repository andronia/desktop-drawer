# Release Process

Every release is a git tag `vX.Y.Z` plus a GitHub Release carrying three files. The installer is the one the README points users to, so it must be attached to every release.

| File | Size | Built by | For |
|---|---|---|---|
| `DesktopInkSetup-X.Y.Z.exe` | ~51 MB | `scripts\make-installer.cmd` (locally) | End users: per-user install, no admin, no .NET needed |
| `DesktopInk-vX.Y.Z-win-x64.exe` | ~182 MB | release workflow | Portable single file, .NET runtime bundled |
| `DesktopInk-vX.Y.Z-win-x64-framework.exe` | < 1 MB | release workflow | Portable; needs the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) |

## Prerequisites

- .NET 10 SDK (`dotnet --version` → 10.0.x)
- Inno Setup 6: `winget install JRSoftware.InnoSetup -e`
- GitHub CLI signed in with push access: `gh auth status`

## Versioning

[Semantic Versioning](https://semver.org/): MAJOR for breaking changes, MINOR for new features, PATCH for fixes.

The version lives only in `<Version>` and `<FileVersion>` in `src/DesktopInk/DesktopInk.csproj`. The installer script receives it from `make-installer.cmd`; nothing else needs editing.

## Steps

1. **Tests green** in both configurations, on an up-to-date `main`:

   ```cmd
   dotnet test desktop-ink.slnx -c Debug
   dotnet test desktop-ink.slnx -c Release
   ```

2. **Bump the version.** This updates the csproj, commits `chore: bump version to X.Y.Z` and creates the tag locally. Don't pass `-Push` yet: the installer is tested first.

   ```cmd
   scripts\bump-version.cmd X.Y.Z
   ```

3. **Build and smoke-test the installer.**

   ```cmd
   scripts\make-installer.cmd
   publish\installer\DesktopInkSetup-X.Y.Z.exe /VERYSILENT /CURRENTUSER
   ```

   Quit any running DesktopInk first; the installer upgrades in place and keeps settings. Launch the installed app and check the palette, drawing and hotkeys. On a multi-monitor setup, check every monitor.

4. **Push the commit and the tag.**

   ```cmd
   git push
   git push origin vX.Y.Z
   ```

   The tag starts `.github/workflows/release.yml`, which runs the tests, builds both portable executables and creates the GitHub Release with auto-generated notes. `ci.yml` runs on the `main` push.

5. **Attach the installer and write the notes** once the workflow has finished (`gh run watch`):

   ```cmd
   gh release upload vX.Y.Z publish\installer\DesktopInkSetup-X.Y.Z.exe
   gh release edit vX.Y.Z --title "vX.Y.Z - <summary>" --notes-file <notes.md>
   ```

   Notes are user-facing: an **Install** section (download the installer, SmartScreen "More info → Run anyway") followed by what was fixed or added. The Releases page is how users learn about new versions; the app does not check for updates.

6. **Verify** with `gh release view vX.Y.Z`: three assets, correct title, marked Latest.

## Troubleshooting

- **Tag already exists**: `bump-version.cmd` offers to recreate it. Manually: `git tag -d vX.Y.Z`, `git push origin :refs/tags/vX.Y.Z`, then tag again.
- **Release workflow failed**: `gh run list`, then `gh run view <id> --log-failed`. Fix, then delete and re-push the tag, or `gh run rerun <id>` for a transient failure.
- **`ISCC.exe not found`**: install Inno Setup 6 (see Prerequisites). `make-installer.cmd` looks in the per-user and Program Files locations.
- **Dev build exits immediately while testing**: the installed DesktopInk is running. Only one instance runs at a time.
