namespace ScreenAutoDim;

/// <summary>Tunable shape of the brightness curve.</summary>
/// <param name="SunriseFadeMinutes">Brightness starts rising at sunrise and is back to the daytime level after this many minutes.</param>
/// <param name="DimBeforeSunsetMinutes">Dimming starts this many minutes before sunset, because it is already getting dark then.</param>
/// <param name="DarkAfterSunsetMinutes">The night level is reached this many minutes after sunset.</param>
/// <param name="CloudDimmingPercent">Fully overcast skies lower the daytime level by up to this many percent.</param>
public sealed record CurveOptions(
    int SunriseFadeMinutes = 60,
    int DimBeforeSunsetMinutes = 30,
    int DarkAfterSunsetMinutes = 30,
    int CloudDimmingPercent = 15)
{
    public static readonly CurveOptions Default = new();
}

/// <summary>
/// Maps time of day and cloud cover to a brightness percentage, where 100 means "no dimming".
/// </summary>
public static class BrightnessCurve
{
    private const double MinTransitionSeconds = 60;

    /// <summary>0 = night, 1 = full day, with smooth transitions around sunrise and sunset.</summary>
    public static double DaylightWeight(DateTime now, DateTime sunrise, DateTime sunset, CurveOptions? options = null)
    {
        options ??= CurveOptions.Default;
        var sunriseFade = Math.Max(TimeSpan.FromMinutes(options.SunriseFadeMinutes).TotalSeconds, MinTransitionSeconds);
        var dimStart = sunset - TimeSpan.FromMinutes(options.DimBeforeSunsetMinutes);
        var dimLength = Math.Max(
            TimeSpan.FromMinutes(options.DimBeforeSunsetMinutes + options.DarkAfterSunsetMinutes).TotalSeconds,
            MinTransitionSeconds);

        var rising = SmoothStep((now - sunrise).TotalSeconds / sunriseFade);
        var setting = 1 - SmoothStep((now - dimStart).TotalSeconds / dimLength);
        return Math.Min(rising, setting);
    }

    /// <param name="cloudCover">Cloud cover in percent (0-100).</param>
    /// <param name="day">Brightness percent to use in clear daytime.</param>
    /// <param name="night">Brightness percent to use at night.</param>
    public static double Percent(DateTime now, DateTime sunrise, DateTime sunset, double cloudCover, int day, int night, CurveOptions? options = null)
    {
        options ??= CurveOptions.Default;
        var cloudFactor = 1 - options.CloudDimmingPercent / 100.0 * Math.Clamp(cloudCover, 0, 100) / 100;
        var weight = DaylightWeight(now, sunrise, sunset, options);
        return night + (day * cloudFactor - night) * weight;
    }

    private static double SmoothStep(double x)
    {
        x = Math.Clamp(x, 0, 1);
        return x * x * (3 - 2 * x);
    }
}
