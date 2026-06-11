# Sunglasses

A lightweight Windows application that places a full-screen, click-through black
overlay on top of everything to dim your display. Transparency is controlled
entirely with global hotkeys, and the current level is shown briefly in the
center of the screen.

## Features

- Full-screen borderless overlay spanning all monitors (virtual desktop).
- **Click-through**: mouse clicks pass straight to the apps underneath.
- Adjust dimming with the mouse wheel while holding **Right Alt**.
- On-screen display shows the current percentage in large white text, then
  auto-hides after 2 seconds.
- Remembers the last dimming level between runs.
- System tray icon with **Start with Windows** (toggle), **Adjust Transparency...**, and **Exit**.
- Optional auto-start at login (per-user, no admin required).
- Single-instance (launching a second copy does nothing).

## Hotkeys

| Action | Hotkey |
| --- | --- |
| Coarse adjust (5% steps) | **Right Alt** + Mouse Wheel |
| Fine adjust (1% steps) | **Right Alt** + **Right Ctrl** + Mouse Wheel |
| Exit | **Right Alt** + **Right Ctrl** + **Q** |

Wheel up brightens (less dimming); wheel down dims (more dimming).

## Install

Once published to the winget community repo:

```powershell
winget install mkronvold.Sunglasses
```

Or grab the latest self-contained `Sunglasses.exe` from the
[Releases](https://github.com/mkronvold/sunglasses/releases) page (no .NET
install required) and run it. See [`winget/`](winget/README.md) for packaging
details.

## Requirements

- **Windows 10 or 11** (the app uses Windows-only APIs).
- **.NET 8 SDK** — needed to build. Install it one of these ways:
  - winget: `winget install Microsoft.DotNet.SDK.8`
  - or download from <https://dotnet.microsoft.com/download/dotnet/8.0>

  Verify a build SDK is available (you should see an `8.0.x` entry):

  ```powershell
  dotnet --list-sdks
  ```

  > A self-contained published build (see below) runs on machines with **no**
  > .NET installed — the runtime is bundled into the `.exe`.

## Get the source

```powershell
git clone https://github.com/mkronvold/sunglasses.git
cd sunglasses
```

## Build & run

```powershell
dotnet build -c Release      # compile
dotnet run -c Release        # build and launch
```

The compiled executable is at `bin\Release\net8.0-windows\Sunglasses.exe`.

## Publish a single executable

Produces one self-contained `Sunglasses.exe` you can copy anywhere:

```powershell
dotnet publish -c Release -r win-x64 --self-contained `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

The executable is written to
`bin\Release\net8.0-windows\win-x64\publish\Sunglasses.exe`.

### Run at startup (optional)

Use the tray icon's **Start with Windows** menu item to toggle auto-start. It
adds/removes a per-user entry under
`HKCU\Software\Microsoft\Windows\CurrentVersion\Run` pointing at the current
executable (no admin rights required). The checkmark reflects the current state.

Alternatively, copy the published `Sunglasses.exe` into your Startup folder:

```
%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup
```

## Configuration

The dimming level is saved to:

```
%LocalAppData%\Sunglasses\config.json
```

The file is created/updated automatically. Delete it to reset to the default
20% dimming. If the file is missing or corrupt, the app falls back to 20%.

## Project layout

```
Sunglasses.csproj        WPF (.NET 8) project, app.manifest (Per-Monitor V2 DPI)
App.xaml(.cs)            Startup, single-instance, service wiring, teardown
Models/AppConfig.cs       Persisted settings (Opacity)
Services/
  TransparencyService.cs  Current dimming level + change notifications
  ConfigService.cs        JSON load / debounced save
  OsdService.cs           Auto-hide timer for the on-screen display
  TrayIconService.cs      System tray icon + context menu
  AutoStartService.cs     Enable/disable launch at login (HKCU Run key)
Platform/                 Windows-specific code (isolated for portability)
  Win32Interop.cs         P/Invoke declarations
  GlobalHookService.cs    Low-level mouse/keyboard hooks for global hotkeys
Views/
  MainWindow.xaml(.cs)    The click-through dimming overlay + OSD
  AdjustTransparencyWindow.xaml(.cs)  Tray slider dialog
```

## Notes

- The app automatically recovers after Windows lock/unlock, monitor sleep, and
  resume from sleep: it re-asserts the overlay's size/top-most/click-through
  state and reinstalls the global hotkey hooks (which Windows can silently drop
  across these events). A periodic safety-net check also runs every few minutes.
- Dimming is applied via the alpha of the overlay's black background brush (the
  window stays fully opaque), so the white OSD text remains readable even at
  very low dimming levels.
- Global hotkeys use low-level Windows hooks installed on the UI thread. The
  `Platform` folder isolates all Windows-specific code to ease a potential
  future cross-platform (e.g. Linux/Avalonia) port.
- On mixed-DPI multi-monitor setups, spanning a single window across monitors
  with different scale factors may not be pixel-perfect.

## License

[MIT](LICENSE) © mkronvold

