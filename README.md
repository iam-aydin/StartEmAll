# Launcher (C# / WPF, .NET 8)

Build once, get `dist\Launcher.exe`, delete the old `.ps1` files.

## Build
- `build.cmd` - small single-file exe (needs the .NET 8 **Desktop Runtime** installed)
- `build-standalone.cmd` - ~150 MB exe that runs anywhere, nothing to install
- `run.cmd` - run from source without building (`dotnet run`)

- `build-installer.cmd [version]` - builds the standalone exe and wraps it in `installer\Output\Launcher-Setup.exe` (needs Inno Setup 6: `winget install JRSoftware.InnoSetup`)

## Publish on GitHub
Push the project, then tag a version: `git tag v1.0.0 && git push origin v1.0.0`.
`.github/workflows/release.yml` builds the installer and attaches `Launcher-Setup.exe` (and the plain `Launcher.exe`) to a Release automatically.
The installer is per-user (no admin prompt) and keeps `settings.json` when you update or uninstall.

Requires the .NET 8 SDK to build: https://dotnet.microsoft.com/download/dotnet/8.0

## Layout
```
Launcher.csproj
App.xaml / App.xaml.cs      startup, single-instance, 144 fps hint
src/
  Core.cs                   windows, tray icon, Rebuild / Commit
  Config.cs                 settings.json (same format as the old script)
  Theme.cs                  palette, Solid / Mica Alt backdrop, brushes
  Native.cs                 DWM / user32 interop
  AppLauncher.cs            wildcard path resolve, icon extract, launch
  Tile.cs                   3D card
  Renderer.cs               smooth scroll + card animation loop
  StripController.cs        edge strip (cursor polled on its own thread)
  Reorder.cs                drag a card onto another to swap places
  SettingsPages.cs          settings pages (Appearance, Side strip, General)
ui/
  Styles.xaml               control styles
  MainWindow / StripWindow / SettingsWindow (.xaml + .xaml.cs)
```
`settings.json` is created next to the exe. Your old one is compatible, just copy it next to `Launcher.exe`.
