using ScreenAutoDim;
using Xunit;

namespace ScreenAutoDim.Tests;

public class BrightnessCurveTests
{
    private static readonly DateTime Sunrise = new(2026, 10, 28, 6, 30, 0);
    private static readonly DateTime Sunset = new(2026, 10, 28, 18, 30, 0);

    private static double At(int hour, int minute = 0, double clouds = 0, int day = 100, int night = 30) =>
        BrightnessCurve.Percent(new DateTime(2026, 10, 28, hour, minute, 0), Sunrise, Sunset, clouds, day, night);

    [Fact]
    public void ClearMidday_UsesDayBrightness() => Assert.Equal(100, At(12), 6);

    [Fact]
    public void Midnight_UsesNightBrightness() => Assert.Equal(30, At(0), 6);

    [Fact]
    public void LateEvening_UsesNightBrightness() => Assert.Equal(30, At(21, 0), 6);

    [Fact]
    public void BeforeDawnRamp_StaysAtNight() => Assert.Equal(30, At(5, 0), 6);

    [Fact]
    public void AtSunrise_StillNight() => Assert.Equal(30, At(6, 30), 6);

    [Fact]
    public void AnHourAfterSunrise_IsFullDay() => Assert.Equal(100, At(7, 30), 6);

    [Fact]
    public void ThirtyMinutesBeforeSunset_StillFullDay() => Assert.Equal(100, At(18, 0), 6);

    [Fact]
    public void AtSunset_IsHalfwayToNight() => Assert.Equal(65, At(18, 30), 6);

    [Fact]
    public void ThirtyMinutesAfterSunset_ReachesNight() => Assert.Equal(30, At(19, 0), 6);

    [Fact]
    public void DimmingStartsBeforeSunset()
    {
        Assert.True(At(18, 15) < 100, "should already be dimming 15 minutes before sunset");
        Assert.True(At(18, 15) > At(18, 30));
    }

    [Fact]
    public void Overcast_LowersDaytimeByDefaultCloudDimming() => Assert.Equal(85, At(12, clouds: 100), 6);

    [Fact]
    public void CustomCloudDimming_IsApplied()
    {
        var now = new DateTime(2026, 10, 28, 12, 0, 0);

        var value = BrightnessCurve.Percent(now, Sunrise, Sunset, 100, 100, 30, new CurveOptions(CloudDimmingPercent: 40));

        Assert.Equal(60, value, 6);
    }

    [Fact]
    public void CustomSunsetWindow_IsApplied()
    {
        var options = new CurveOptions(DimBeforeSunsetMinutes: 90, DarkAfterSunsetMinutes: 0);

        Assert.Equal(100, BrightnessCurve.Percent(Sunset.AddMinutes(-90), Sunrise, Sunset, 0, 100, 30, options), 6);
        Assert.Equal(30, BrightnessCurve.Percent(Sunset, Sunrise, Sunset, 0, 100, 30, options), 6);
    }

    [Fact]
    public void CustomSunriseFade_StartsAtSunriseAndEndsAfterTheConfiguredTime()
    {
        var options = new CurveOptions(SunriseFadeMinutes: 120);

        Assert.Equal(30, BrightnessCurve.Percent(Sunrise, Sunrise, Sunset, 0, 100, 30, options), 6);
        Assert.Equal(100, BrightnessCurve.Percent(Sunrise.AddMinutes(120), Sunrise, Sunset, 0, 100, 30, options), 6);
    }

    [Fact]
    public void ZeroLengthSunsetWindow_DoesNotDivideByZero()
    {
        var options = new CurveOptions(DimBeforeSunsetMinutes: 0, DarkAfterSunsetMinutes: 0);

        var value = BrightnessCurve.Percent(Sunset, Sunrise, Sunset, 0, 100, 30, options);

        Assert.InRange(value, 30, 100);
    }

    [Fact]
    public void HalfCloudy_IsBetweenClearAndOvercast()
    {
        var half = At(12, clouds: 50);
        Assert.InRange(half, At(12, clouds: 100), At(12));
    }

    [Fact]
    public void CloudCover_DoesNotAffectNight() => Assert.Equal(At(0), At(0, clouds: 100), 6);

    [Fact]
    public void AroundSunset_FallsGraduallyAndMonotonically()
    {
        var previous = BrightnessCurve.Percent(Sunset.AddMinutes(-30), Sunrise, Sunset, 0, 100, 30);
        Assert.Equal(100, previous, 6);

        for (var minutes = -25; minutes <= 30; minutes += 5)
        {
            var value = BrightnessCurve.Percent(Sunset.AddMinutes(minutes), Sunrise, Sunset, 0, 100, 30);
            Assert.True(value <= previous, $"brightness rose at {minutes} min from sunset");
            previous = value;
        }

        Assert.Equal(30, previous, 6);
    }

    [Fact]
    public void AfterSunrise_RisesGraduallyAndMonotonically()
    {
        var previous = BrightnessCurve.Percent(Sunrise, Sunrise, Sunset, 0, 100, 30);
        for (var minutes = 5; minutes <= 60; minutes += 5)
        {
            var value = BrightnessCurve.Percent(Sunrise.AddMinutes(minutes), Sunrise, Sunset, 0, 100, 30);
            Assert.True(value >= previous, $"brightness fell at +{minutes} min from sunrise");
            previous = value;
        }

        Assert.Equal(100, previous, 6);
    }

    [Fact]
    public void LowDaytimeSetting_IsRespected() => Assert.Equal(40, At(12, day: 40, night: 10), 6);
}

public class ForecastTests
{
    [Fact]
    public void ShiftedTo_MovesTimesByWholeDays()
    {
        var forecast = new Forecast(new DateTime(2026, 10, 27, 6, 41, 0), new DateTime(2026, 10, 27, 16, 20, 0), 20, TimeSpan.FromHours(1));

        var shifted = forecast.ShiftedTo(new DateTime(2026, 10, 28, 0, 5, 0));

        Assert.Equal(new DateTime(2026, 10, 28, 6, 41, 0), shifted.Sunrise);
        Assert.Equal(new DateTime(2026, 10, 28, 16, 20, 0), shifted.Sunset);
    }

    [Fact]
    public void ParseForecast_ReadsOpenMeteoResponse()
    {
        const string json = """
            {"utc_offset_seconds":3600,
             "current":{"cloud_cover":42},
             "daily":{"sunrise":["2026-10-28T06:41"],"sunset":["2026-10-28T16:20"]}}
            """;
        using var doc = System.Text.Json.JsonDocument.Parse(json);

        var forecast = WeatherService.ParseForecast(doc.RootElement);

        Assert.Equal(new DateTime(2026, 10, 28, 6, 41, 0), forecast.Sunrise);
        Assert.Equal(new DateTime(2026, 10, 28, 16, 20, 0), forecast.Sunset);
        Assert.Equal(42, forecast.CloudCover);
        Assert.Equal(TimeSpan.FromHours(1), forecast.UtcOffset);
    }
}

public class AppConfigTests
{
    [Fact]
    public void Sanitized_ClampsBrightnessToAllowedRange()
    {
        var config = new AppConfig { DayBrightness = 500, NightBrightness = 0 }.Sanitized();

        Assert.Equal(100, config.DayBrightness);
        Assert.Equal(AppConfig.MinBrightness, config.NightBrightness);
    }

    [Fact]
    public void Sanitized_ClampsCurveSettings()
    {
        var config = new AppConfig { SunriseFadeMinutes = 1, DimBeforeSunsetMinutes = 9999, DarkAfterSunsetMinutes = -5, CloudDimmingPercent = 90 }.Sanitized();

        Assert.Equal(AppConfig.MinFadeMinutes, config.SunriseFadeMinutes);
        Assert.Equal(AppConfig.MaxFadeMinutes, config.DimBeforeSunsetMinutes);
        Assert.Equal(0, config.DarkAfterSunsetMinutes);
        Assert.Equal(AppConfig.MaxCloudDimmingPercent, config.CloudDimmingPercent);
    }

    [Fact]
    public void Sanitized_KeepsTheSunsetWindowFromCollapsing()
    {
        var config = new AppConfig { DimBeforeSunsetMinutes = 0, DarkAfterSunsetMinutes = 0 }.Sanitized();

        Assert.True(config.DimBeforeSunsetMinutes + config.DarkAfterSunsetMinutes >= AppConfig.MinFadeMinutes);
    }

    [Fact]
    public void ToCurveOptions_CopiesCurveSettings()
    {
        var options = new AppConfig { SunriseFadeMinutes = 45, DimBeforeSunsetMinutes = 20, DarkAfterSunsetMinutes = 40, CloudDimmingPercent = 25 }.ToCurveOptions();

        Assert.Equal(new CurveOptions(45, 20, 40, 25), options);
    }

    [Fact]
    public void Load_IgnoresSettingsFromOlderVersions()
    {
        var path = Path.Combine(Path.GetTempPath(), $"autodim-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """{"DayBrightness":80,"DawnMinutes":60,"DuskMinutes":60}""");
        try
        {
            var loaded = AppConfig.Load(path);

            Assert.Equal(80, loaded.DayBrightness);
            Assert.Equal(CurveOptions.Default.DimBeforeSunsetMinutes, loaded.DimBeforeSunsetMinutes);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Sanitized_DropsImpossibleCoordinates()
    {
        var config = new AppConfig { Latitude = 123, Longitude = 10 }.Sanitized();

        Assert.False(config.HasLocation);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var path = Path.Combine(Path.GetTempPath(), $"autodim-{Guid.NewGuid():N}", "config.json");
        try
        {
            new AppConfig { Place = "Warsaw, Poland", Latitude = 52.23, Longitude = 21.01, DayBrightness = 80, NightBrightness = 20 }.Save(path);

            var loaded = AppConfig.Load(path);

            Assert.Equal("Warsaw, Poland", loaded.Place);
            Assert.Equal(52.23, loaded.Latitude);
            Assert.Equal(80, loaded.DayBrightness);
            Assert.Equal(20, loaded.NightBrightness);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, true);
        }
    }

    [Fact]
    public void Load_MissingOrCorruptFile_FallsBackToDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), $"autodim-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "{ not json");
        try
        {
            Assert.Equal(30, AppConfig.Load(path).NightBrightness);
            Assert.Equal(30, AppConfig.Load(path + ".missing").NightBrightness);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_FallsBackToSettingsFromBeforeTheRename()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"autodim-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        var legacy = Path.Combine(folder, "legacy.json");
        File.WriteAllText(legacy, """{"Place":"Gdynia","NightBrightness":25}""");
        try
        {
            var loaded = AppConfig.Load(Path.Combine(folder, "missing.json"), legacy);

            Assert.Equal("Gdynia", loaded.Place);
            Assert.Equal(25, loaded.NightBrightness);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void Load_PrefersNewSettingsOverLegacyOnes()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"autodim-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        var current = Path.Combine(folder, "current.json");
        var legacy = Path.Combine(folder, "legacy.json");
        File.WriteAllText(current, """{"Place":"New"}""");
        File.WriteAllText(legacy, """{"Place":"Old"}""");
        try
        {
            Assert.Equal("New", AppConfig.Load(current, legacy).Place);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }
}
