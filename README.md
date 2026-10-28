# ScreenAutoDim

A small Windows tray app that automatically dims your monitors in the evening and eases them back up in the morning, based on **sunrise, sunset and the weather** at your location.

- Sunny day: no dimming.
- Overcast day: slightly dimmer (up to 15% by default, adjustable or off).
- Around sunset: starts dimming 30 minutes before sunset and reaches your night brightness 30 minutes after it.
- At sunrise: smoothly returns to daytime brightness over the next hour.

It works on **every monitor, including ones connected through USB docks and DisplayLink adapters**, which usually can't be controlled with DDC/CI (the usual way monitor brightness is changed from software).

## How it works

ScreenAutoDim does not talk to the monitor. For each display it draws a black, click-through, always-on-top overlay and changes the overlay's opacity. That's why it works through any hub or adapter, but it also means:

- It can only make the screen **darker** than the monitor's own brightness setting. Set your monitor's brightness to the level you like in daylight and let ScreenAutoDim handle the evenings.
- The backlight itself isn't reduced, so black levels in a dark room still glow a little and no power is saved.
- Exclusive-fullscreen games and some video players may draw above the overlay, and screenshots can capture the dimming.
- It isn't compatible with Windows HDR mode.

## Install

Download `ScreenAutoDim.exe` from the [Releases](../../releases) page and run it. It is self-contained and needs no .NET install. The first run opens the settings window.

Tick **Start ScreenAutoDim with Windows** in the settings to launch it at every logon. Move the `.exe` somewhere permanent first, since the autostart entry points to its current location.

## Settings

Right-click the tray icon (or double-click it) and choose **Settings**:

| Setting | Meaning |
| --- | --- |
| Location | Search for your city, or click **Map...** and click your spot on an OpenStreetMap map. Used for sunrise, sunset and cloud cover. |
| Daytime brightness | Percent of the monitor's brightness in clear daytime. `100` = no dimming. |
| Night brightness | Percent of the monitor's brightness at night. Minimum `10` so the screen can't go black. |
| Sunrise: brighten up over | Brightness starts rising at sunrise and reaches the daytime level after this many minutes (10-240, default 60). |
| Sunset: start dimming before | Dimming starts this many minutes before sunset, because it is already getting dark by then (0-240, default 30). |
| Sunset: fully dark after | The night brightness is reached this many minutes after sunset (0-240, default 30). |
| Cloudy weather: dim by up to | A fully overcast sky lowers the daytime brightness by this many percent, less for partial cloud (0-50, default 15). Set `0` to ignore the weather. |
| Start ScreenAutoDim with Windows | Adds or removes a per-user autostart entry (no admin rights needed). |
| Show icon in the system tray | Untick to run without a tray icon. |

The settings window also has a **Quit ScreenAutoDim** button, so you can stop the app even when the tray icon is hidden.

Starting `ScreenAutoDim.exe` while it is already running just opens the settings window of the running copy. Windows 11 hides new tray icons behind the `^` arrow; the first time it runs, ScreenAutoDim pins its icon next to the clock. If you later hide it yourself in *Settings > Personalization > Taskbar > Other system tray icons*, ScreenAutoDim respects that.

The tray menu also has **Pause dimming** (handy for colour-critical work or watching a film) and **Quit**.

Settings are stored in `%APPDATA%\ScreenAutoDim\config.json`. Errors are logged to `%APPDATA%\ScreenAutoDim\autodim.log`. Without a location, a fixed 07:00-19:00 schedule is used.

## Privacy

The only network requests are to the free [Open-Meteo](https://open-meteo.com/) forecast and geocoding APIs, sent with the coordinates of the place you picked. The map picker additionally loads the [Leaflet](https://leafletjs.com/) library from unpkg.com and map tiles from OpenStreetMap, only while the map window is open. The map needs the Microsoft Edge WebView2 Runtime, which is included in Windows 11 and recent Windows 10 updates; the city search works without it. Nothing else leaves your machine. Open-Meteo data is licensed [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) and the free API is for non-commercial use.

## Build from source

You need the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
dotnet test
dotnet run --project src/ScreenAutoDim

# single self-contained exe, written to ./publish
dotnet publish src/ScreenAutoDim -c Release -r win-x64 --self-contained `
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o publish
```

The brightness curve lives in [`BrightnessCurve.cs`](src/ScreenAutoDim/BrightnessCurve.cs); its shape is controlled by the sunrise, sunset and weather settings above.

## License

[MIT](LICENSE)
