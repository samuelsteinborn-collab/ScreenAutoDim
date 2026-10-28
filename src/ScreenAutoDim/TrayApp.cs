using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ScreenAutoDim;

/// <summary>The tray icon plus the timers that keep the overlays and the forecast up to date.</summary>
internal sealed class TrayApp : ApplicationContext
{
    /// <summary>Signalled by a second launch of the exe; the running copy then opens its settings.</summary>
    public const string ShowSettingsEventName = @"Local\ScreenAutoDim.ShowSettings";

    private static readonly TimeSpan ApplyInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan WeatherInterval = TimeSpan.FromMinutes(15);

    private readonly Control _ui = new();
    private readonly EventWaitHandle _showSettingsSignal = new(false, EventResetMode.AutoReset, ShowSettingsEventName);
    private readonly RegisteredWaitHandle _showSettingsWait;
    private readonly OverlayManager _overlays = new();
    private readonly NotifyIcon _tray;
    private readonly ToolStripMenuItem _pauseItem;
    private readonly System.Windows.Forms.Timer _applyTimer = new() { Interval = (int)ApplyInterval.TotalMilliseconds };
    private readonly System.Windows.Forms.Timer _weatherTimer = new() { Interval = (int)WeatherInterval.TotalMilliseconds };

    private AppConfig _config = AppConfig.Load();
    private Forecast? _forecast;
    private SettingsForm? _settings;
    private bool _paused;

    public TrayApp()
    {
        _ = _ui.Handle; // create the handle now so BeginInvoke works from any thread
        Autostart.MigrateLegacyEntry();

        _pauseItem = new ToolStripMenuItem("Pause dimming", null, (_, _) => TogglePause());
        var menu = new ContextMenuStrip();
        menu.Items.Add("Settings...", null, (_, _) => OpenSettings());
        menu.Items.Add(_pauseItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => Quit());

        _tray = new NotifyIcon { Icon = CreateIcon(), Text = "ScreenAutoDim", ContextMenuStrip = menu, Visible = _config.ShowTrayIcon };
        _tray.DoubleClick += (_, _) => OpenSettings();
        if (_config.ShowTrayIcon)
            PinTrayIconOnce();
        _showSettingsWait = ThreadPool.RegisterWaitForSingleObject(
            _showSettingsSignal, (_, _) => _ui.BeginInvoke(OpenSettings), null, Timeout.Infinite, executeOnlyOnce: false);

        _applyTimer.Tick += (_, _) => Apply();
        _weatherTimer.Tick += async (_, _) => await RefreshWeatherAsync();
        _applyTimer.Start();
        _weatherTimer.Start();

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        _ui.BeginInvoke(async () =>
        {
            Apply();
            if (!_config.HasLocation)
                OpenSettings();
            await RefreshWeatherAsync();
        });
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => _ui.BeginInvoke(Apply);

    private void Apply()
    {
        try
        {
            var percent = _paused ? 100 : CurrentPercent();
            _overlays.Apply(1 - percent / 100.0);
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
    }

    private double CurrentPercent()
    {
        DateTime now, sunrise, sunset;
        double clouds;

        if (_config.HasLocation && _forecast is { } forecast)
        {
            now = forecast.LocalNow;
            var today = forecast.ShiftedTo(now);
            (sunrise, sunset, clouds) = (today.Sunrise, today.Sunset, today.CloudCover);
        }
        else
        {
            // No location or no forecast yet: fall back to a fixed schedule in local time.
            now = DateTime.Now;
            (sunrise, sunset, clouds) = (now.Date.AddHours(7), now.Date.AddHours(19), 0);
        }

        return BrightnessCurve.Percent(now, sunrise, sunset, clouds,
            _config.DayBrightness, _config.NightBrightness, _config.ToCurveOptions());
    }

    private async Task RefreshWeatherAsync()
    {
        if (_config.Latitude is not { } lat || _config.Longitude is not { } lon)
            return;

        var requestedFor = _config;
        try
        {
            var forecast = await WeatherService.GetForecastAsync(lat, lon);
            if (!ReferenceEquals(requestedFor, _config))
                return; // the location changed while the request was in flight
            _forecast = forecast;
        }
        catch (Exception ex)
        {
            Log.Error(ex); // keep the previous forecast, or the fixed schedule
            return;
        }
        Apply();
    }

    private void TogglePause()
    {
        _paused = !_paused;
        _pauseItem.Checked = _paused;
        Apply();
    }

    private void OpenSettings()
    {
        if (_settings is { IsDisposed: false })
        {
            _settings.Activate();
            return;
        }

        var form = new SettingsForm(_config);
        _settings = form;
        form.QuitRequested += Quit;
        form.FormClosed += async (_, _) =>
        {
            _settings = null;
            if (form.DialogResult != DialogResult.OK)
                return;

            try
            {
                Autostart.Set(form.StartWithWindows);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
            {
                Log.Error(ex);
            }
            await SaveAsync(form.Result);
        };
        form.Show();
    }

    private async Task SaveAsync(AppConfig config)
    {
        _config = config.Sanitized();
        _forecast = null;
        _tray.Visible = _config.ShowTrayIcon;
        if (_config.ShowTrayIcon)
            PinTrayIconOnce();
        try
        {
            _config.Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Error(ex);
        }
        Apply();
        await RefreshWeatherAsync();
    }

    /// <summary>
    /// Windows 11 hides new tray icons behind the ^ arrow. The first time Windows has seen our icon, pin it next to the clock.
    /// Once the user has chosen (the value exists), their choice is left alone.
    /// </summary>
    private void PinTrayIconOnce()
    {
        var attempts = 0;
        var timer = new System.Windows.Forms.Timer { Interval = 2000 };
        timer.Tick += (_, _) =>
        {
            // Windows creates the icon's settings entry shortly after the icon first appears.
            if (TryPinTrayIcon() || ++attempts >= 3)
            {
                timer.Stop();
                timer.Dispose();
            }
        };
        timer.Start();
    }

    private static bool TryPinTrayIcon()
    {
        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(@"Control Panel\NotifyIconSettings");
            foreach (var name in root?.GetSubKeyNames() ?? Array.Empty<string>())
            {
                using var key = root!.OpenSubKey(name, writable: true);
                if (key?.GetValue("ExecutablePath") is not string path ||
                    !string.Equals(path, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (key.GetValue("IsPromoted") is null)
                    key.SetValue("IsPromoted", 1, RegistryValueKind.DWord);
                return true;
            }
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            Log.Error(ex);
            return true; // not allowed here, so retrying will not help
        }
        return false;
    }

    private void Quit()
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _applyTimer.Stop();
        _weatherTimer.Stop();
        _showSettingsWait.Unregister(null);
        _showSettingsSignal.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _overlays.Dispose();
        ExitThread();
    }

    private static Icon CreateIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var amber = new SolidBrush(Color.FromArgb(255, 196, 64));
            g.FillEllipse(amber, 4, 4, 24, 24);
            g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
            using var cutout = new SolidBrush(Color.Transparent);
            g.FillEllipse(cutout, 12, 1, 24, 24); // carve a crescent moon out of the disc
        }

        var handle = bitmap.GetHicon();
        try
        {
            return (Icon)Icon.FromHandle(handle).Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);
}
