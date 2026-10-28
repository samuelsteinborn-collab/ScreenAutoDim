A small Windows tray app that automatically dims your monitors in the evening and brings them back up in the morning, based on **sunrise, sunset and the weather** at your location. It works on every monitor, including ones connected through USB docks and DisplayLink adapters, because it dims with an on-screen overlay instead of DDC/CI.

## Install

1. Download `ScreenAutoDim.exe` below and run it. There is nothing to install and no .NET needed.
2. On first run the settings window opens. Pick your location (search or map), choose your day and night brightness, and click **Save**.
3. Optionally tick **Start ScreenAutoDim with Windows**. Move the `.exe` to a permanent folder first.

## Windows SmartScreen warning

When you first run the file, Windows may show **"Windows protected your PC"**. This is Microsoft Defender SmartScreen, which warns about any program it has not seen many times before. ScreenAutoDim is not code-signed, so it triggers the warning.

To continue, click **More info**, then **Run anyway**. You only have to do this once.

If you would rather not trust a download, the full source is in this repository and the `.exe` is built from it by GitHub Actions, so you can read the code or build it yourself (see the README).
