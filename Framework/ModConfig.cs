namespace BetterAutoGrabber.Framework;

/// <summary>How often a grabber runs its extra (non-animal) harvest pass.</summary>
internal enum GrabFrequency
{
    /// <summary>Use the mod-wide default. Only valid as a per-grabber override.</summary>
    Default,

    /// <summary>Every 10 in-game minutes.</summary>
    TenMinutes,

    /// <summary>Every in-game hour.</summary>
    Hourly,

    /// <summary>Every four in-game hours.</summary>
    FourHours,

    /// <summary>Once per day, when the day starts.</summary>
    Daily
}

/// <summary>The mod-wide settings, editable through <c>config.json</c> or Generic Mod Config Menu.</summary>
internal sealed class ModConfig
{
    /// <summary>How often grabbers harvest, unless a grabber overrides it on its own settings page.</summary>
    public GrabFrequency DefaultFrequency { get; set; } = GrabFrequency.Hourly;

    /// <summary>Whether harvesting respects the tool upgrades vanilla would require (a Copper Axe for large stumps, and so on).</summary>
    public bool RespectToolRequirements { get; set; } = true;

    /// <summary>Whether grabbed items grant the skill experience that harvesting them by hand would.</summary>
    public bool GrantExperience { get; set; } = true;

    /// <summary>Whether to replant a harvested crop when its seeds are in the grabber.</summary>
    public bool ReplantCrops { get; set; } = true;

    /// <summary>Whether to leave festival and other temporary event locations alone.</summary>
    public bool SkipFestivalLocations { get; set; } = true;

    /// <summary>Whether to report what each grabber collected at the end of the day.</summary>
    public bool DailySummary { get; set; } = true;

    /// <summary>Whether to log per-item harvest detail to the SMAPI console.</summary>
    public bool VerboseLogging { get; set; }

    /// <summary>Horizontal nudge for the settings button drawn on the grabber menu, for when another mod puts a button in the same spot.</summary>
    public int SettingsButtonOffsetX { get; set; }

    /// <summary>Vertical nudge for the settings button drawn on the grabber menu.</summary>
    public int SettingsButtonOffsetY { get; set; }
}
