namespace ScreenAutoDim;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, @"Local\ScreenAutoDim", out var isFirstInstance);
        if (!isFirstInstance)
        {
            // Launching the exe again asks the running copy to show its settings, so it never looks like nothing happened.
            try
            {
                using var showSettings = EventWaitHandle.OpenExisting(TrayApp.ShowSettingsEventName);
                showSettings.Set();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                // The running copy is still starting up.
            }
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.ThreadException += (_, e) => Log.Error(e.Exception);
        Application.Run(new TrayApp());
    }
}
