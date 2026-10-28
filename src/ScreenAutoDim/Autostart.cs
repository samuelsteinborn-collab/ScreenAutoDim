using Microsoft.Win32;

namespace ScreenAutoDim;

/// <summary>Starts ScreenAutoDim at logon through the per-user Run key; no administrator rights needed.</summary>
internal static class Autostart
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ScreenAutoDim";

    /// <summary>The value name used before the app was renamed.</summary>
    private const string LegacyValueName = "AutoDim";

    public static bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
                return key?.GetValue(ValueName) is string || key?.GetValue(LegacyValueName) is string;
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException)
            {
                return false;
            }
        }
    }

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        key.DeleteValue(LegacyValueName, throwOnMissingValue: false);
        if (enabled && Environment.ProcessPath is { } path)
            key.SetValue(ValueName, $"\"{path}\"");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    /// <summary>If autostart was enabled under the old name, re-enables it for this executable under the new name.</summary>
    public static void MigrateLegacyEntry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            if (key?.GetValue(LegacyValueName) is string)
                Set(true);
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException)
        {
            Log.Error(ex);
        }
    }
}
