using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace BetterAutoGrabber;

/// <summary>The mod entry point.</summary>
internal sealed class ModEntry : Mod
{
    /// <summary>The mod entry point, called after the mod is first loaded.</summary>
    /// <param name="helper">Provides simplified APIs for writing mods.</param>
    public override void Entry(IModHelper helper)
    {
        helper.Events.GameLoop.DayStarted += this.OnDayStarted;
    }

    /// <summary>Raised after the game begins a new day, including when the player loads a save.</summary>
    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        this.Monitor.Log("Better Auto-Grabber: day started.", LogLevel.Debug);
    }
}
