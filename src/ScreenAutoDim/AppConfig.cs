using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScreenAutoDim;

public sealed class AppConfig
{
    /// <summary>Lowest allowed brightness, so the screen can never be dimmed to black.</summary>
    public const int MinBrightness = 10;

    public const int MinFadeMinutes = 10;
    public const int MaxFadeMinutes = 240;
    public const int MaxCloudDimmingPercent = 50;

    public string Place { get; set; } = "";
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    /// <summary>Percent of the monitor's own brightness used in clear daytime (100 = no dimming).</summary>
    public int DayBrightness { get; set; } = 100;

    /// <summary>Percent of the monitor's own brightness used at night.</summary>
    public int NightBrightness { get; set; } = 30;

    /// <summary>Minutes after sunrise until the daytime brightness is reached.</summary>
    public int SunriseFadeMinutes { get; set; } = CurveOptions.Default.SunriseFadeMinutes;

    /// <summary>Minutes before sunset at which dimming starts.</summary>
    public int DimBeforeSunsetMinutes { get; set; } = CurveOptions.Default.DimBeforeSunsetMinutes;

    /// <summary>Minutes after sunset at which the night brightness is reached.</summary>
    public int DarkAfterSunsetMinutes { get; set; } = CurveOptions.Default.DarkAfterSunsetMinutes;

    /// <summary>By how many percent a fully overcast sky lowers the daytime brightness (0 = ignore the weather).</summary>
    public int CloudDimmingPercent { get; set; } = CurveOptions.Default.CloudDimmingPercent;

    /// <summary>Whether the tray icon is shown. Launching the exe again always reopens the settings.</summary>
    public bool ShowTrayIcon { get; set; } = true;

    [JsonIgnore]
    public bool HasLocation => Latitude is not null && Longitude is not null;

    public CurveOptions ToCurveOptions() =>
        new(SunriseFadeMinutes, DimBeforeSunsetMinutes, DarkAfterSunsetMinutes, CloudDimmingPercent);

    public static string DataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScreenAutoDim");

    public static string DefaultPath => Path.Combine(DataDirectory, "config.json");

    /// <summary>Where the settings were stored before the app was renamed.</summary>
    public static string LegacyPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AutoDim", "config.json");

    /// <param name="path">The file to read; the default location when omitted.</param>
    /// <param name="legacyPath">Read instead when <paramref name="path"/> does not exist yet. Defaults to the pre-rename location when <paramref name="path"/> is omitted.</param>
    public static AppConfig Load(string? path = null, string? legacyPath = null)
    {
        if (path is null)
        {
            path = DefaultPath;
            legacyPath ??= LegacyPath;
        }

        try
        {
            if (!File.Exists(path) && legacyPath is not null && File.Exists(legacyPath))
                path = legacyPath;

            var json = File.ReadAllText(path);
            return (JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig()).Sanitized();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new AppConfig();
        }
    }

    public void Save(string? path = null)
    {
        path ??= DefaultPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    public AppConfig Sanitized()
    {
        DayBrightness = Math.Clamp(DayBrightness, MinBrightness, 100);
        NightBrightness = Math.Clamp(NightBrightness, MinBrightness, 100);
        SunriseFadeMinutes = Math.Clamp(SunriseFadeMinutes, MinFadeMinutes, MaxFadeMinutes);
        DimBeforeSunsetMinutes = Math.Clamp(DimBeforeSunsetMinutes, 0, MaxFadeMinutes);
        DarkAfterSunsetMinutes = Math.Clamp(DarkAfterSunsetMinutes, 0, MaxFadeMinutes);
        if (DimBeforeSunsetMinutes + DarkAfterSunsetMinutes < MinFadeMinutes)
            DarkAfterSunsetMinutes = MinFadeMinutes - DimBeforeSunsetMinutes;
        CloudDimmingPercent = Math.Clamp(CloudDimmingPercent, 0, MaxCloudDimmingPercent);
        if (Latitude is < -90 or > 90 || Longitude is < -180 or > 180)
        {
            Latitude = null;
            Longitude = null;
        }
        return this;
    }
}
