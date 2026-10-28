using System.Globalization;
using System.Text.Json;

namespace ScreenAutoDim;

/// <param name="Sunrise">Today's sunrise, in the location's local time.</param>
/// <param name="Sunset">Today's sunset, in the location's local time.</param>
/// <param name="CloudCover">Current cloud cover in percent.</param>
/// <param name="UtcOffset">Offset of the location's time zone from UTC.</param>
public sealed record Forecast(DateTime Sunrise, DateTime Sunset, double CloudCover, TimeSpan UtcOffset)
{
    public DateTime LocalNow => DateTime.UtcNow + UtcOffset;

    /// <summary>Moves the sunrise/sunset times to <paramref name="day"/> when the data is a day or more old.</summary>
    public Forecast ShiftedTo(DateTime day)
    {
        var delta = TimeSpan.FromDays((day.Date - Sunrise.Date).Days);
        return this with { Sunrise = Sunrise + delta, Sunset = Sunset + delta };
    }
}

public sealed record Place(string Label, double Latitude, double Longitude)
{
    public override string ToString() => Label;
}

/// <summary>Talks to the free Open-Meteo forecast and geocoding APIs (no API key needed).</summary>
public static class WeatherService
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ScreenAutoDim/0.1");
        return client;
    }

    public static async Task<Forecast> GetForecastAsync(double latitude, double longitude)
    {
        var url = "https://api.open-meteo.com/v1/forecast" +
                  $"?latitude={Num(latitude)}&longitude={Num(longitude)}" +
                  "&daily=sunrise,sunset&current=cloud_cover&timezone=auto&forecast_days=1";
        using var doc = JsonDocument.Parse(await Http.GetStringAsync(url));
        return ParseForecast(doc.RootElement);
    }

    public static Forecast ParseForecast(JsonElement root)
    {
        var daily = root.GetProperty("daily");
        return new Forecast(
            ParseLocalTime(daily.GetProperty("sunrise")[0]),
            ParseLocalTime(daily.GetProperty("sunset")[0]),
            root.GetProperty("current").GetProperty("cloud_cover").GetDouble(),
            TimeSpan.FromSeconds(root.GetProperty("utc_offset_seconds").GetInt32()));
    }

    public static async Task<IReadOnlyList<Place>> SearchPlacesAsync(string query)
    {
        var url = "https://geocoding-api.open-meteo.com/v1/search" +
                  $"?name={Uri.EscapeDataString(query)}&count=6&language=en&format=json";
        using var doc = JsonDocument.Parse(await Http.GetStringAsync(url));
        if (!doc.RootElement.TryGetProperty("results", out var results))
            return Array.Empty<Place>();

        return results.EnumerateArray().Select(r =>
        {
            var parts = new[] { Text(r, "name"), Text(r, "admin1"), Text(r, "country") }
                .Where(p => !string.IsNullOrEmpty(p)).Distinct();
            return new Place(string.Join(", ", parts), r.GetProperty("latitude").GetDouble(), r.GetProperty("longitude").GetDouble());
        }).ToList();
    }

    private static string Num(double value) => value.ToString(CultureInfo.InvariantCulture);

    private static string? Text(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    // Open-Meteo returns local times without an offset, e.g. "2026-10-28T06:41".
    private static DateTime ParseLocalTime(JsonElement e) =>
        DateTime.Parse(e.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.None);
}
