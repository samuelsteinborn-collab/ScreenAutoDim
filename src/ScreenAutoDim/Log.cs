namespace ScreenAutoDim;

internal static class Log
{
    public static void Error(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(AppConfig.DataDirectory);
            File.AppendAllText(Path.Combine(AppConfig.DataDirectory, "autodim.log"), $"{DateTime.Now:s} {ex}{Environment.NewLine}");
        }
        catch
        {
            // Logging must never take the app down.
        }
    }
}
