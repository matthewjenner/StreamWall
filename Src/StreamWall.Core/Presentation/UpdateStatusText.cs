using StreamWall.Core.Updates;

namespace StreamWall.Core.Presentation;

/// <summary>What the Settings tab says about updates, and whether its buttons apply.</summary>
public sealed record UpdateStatusText(string Text, bool CanCheck, bool CanInstall)
{
    public static UpdateStatusText From(UpdateState state)
    {
        string version = state.Version is { } v ? " v" + v : "";
        return state.Status switch
        {
            UpdateStatus.Disabled => new("Updates are off in dev and demo copies (only the installed app updates).", false, false),
            UpdateStatus.Idle => new("Checks shortly after launch and every hour.", true, false),
            UpdateStatus.Checking => new("Checking for updates...", false, false),
            UpdateStatus.NotAvailable => new("You're up to date.", true, false),
            UpdateStatus.Available => new($"Update{version} found - downloading...", false, false),
            UpdateStatus.Downloading => new($"Downloading update{version}... {state.Percent ?? 0}%", false, false),
            UpdateStatus.Downloaded => new($"Update{version} is ready. It installs when you quit, or restart now.", false, true),
            UpdateStatus.Error => new("The update check failed. Try again, or it retries within the hour.", true, false),
            _ => new("", true, false),
        };
    }
}
