using Avalonia.Controls;

namespace StreamWall.App.Services;

/// <summary>
/// Quitting cleanly. When the control panel starts to close: close the walls as part of shutdown
/// (not as the user closing them, so they reopen next launch), then save any pending change, then
/// let the window close. Done in the window's Closing event, on the UI thread and without blocking
/// it: the Exit event is too late (the lifetime has already closed the walls) and blocking there
/// on the save deadlocks against the dispatcher the save needs (both found in the first real run).
/// </summary>
public static class Shutdown
{
    public static void Wire(Window controlPanel, WallManager walls, AppHost host)
    {
        bool saved = false;
        controlPanel.Closing += async (_, e) =>
        {
            if (saved)
                return;
            e.Cancel = true;
            walls.CloseAll();
            try
            {
                await host.FlushAsync();
                // A downloaded update installs once this process has exited.
                host.Updates.OnQuit();
            }
            finally
            {
                saved = true;
                controlPanel.Close();
            }
        };
    }
}
