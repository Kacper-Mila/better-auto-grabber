using System;
using System.Collections.Generic;
using System.Linq;
using StardewValley;
using Object = StardewValley.Object;

namespace BetterAutoGrabber.Framework;

/// <summary>Which locations a grabber pulls from.</summary>
internal enum ScopeMode
{
    /// <summary>Only the location the grabber is standing in.</summary>
    Local,

    /// <summary>Every location the player has visited at least once.</summary>
    Global,

    /// <summary>Only the locations picked on the grabber's scope tab.</summary>
    Selected
}

/// <summary>The settings for one placed auto-grabber, stored on the grabber itself.</summary>
/// <remarks>
///   These live in the grabber's <see cref="Object.modData" />, so they're saved with the game, survive
///   being picked up and put down, and stay attached to the right grabber when you own several.
/// </remarks>
internal sealed class GrabberSettings
{
    private const string KeyPrefix = "Kacper.BetterAutoGrabber/";
    private const string TargetsKey = GrabberSettings.KeyPrefix + "targets";
    private const string ScopeKey = GrabberSettings.KeyPrefix + "scope";
    private const string LocationsKey = GrabberSettings.KeyPrefix + "locations";
    private const string FrequencyKey = GrabberSettings.KeyPrefix + "frequency";

    /// <summary>The IDs of the targets this grabber collects. Empty means it behaves exactly like a vanilla grabber.</summary>
    public HashSet<string> TargetIds { get; } = new();

    /// <summary>Which locations this grabber reaches.</summary>
    public ScopeMode Scope { get; set; } = ScopeMode.Local;

    /// <summary>The locations picked for <see cref="ScopeMode.Selected" />, by internal name.</summary>
    public HashSet<string> SelectedLocations { get; } = new();

    /// <summary>How often this grabber runs, or <see cref="GrabFrequency.Default" /> to follow the mod-wide setting.</summary>
    public GrabFrequency Frequency { get; set; } = GrabFrequency.Default;

    /// <summary>Whether this grabber has been given anything to collect beyond animal products.</summary>
    public bool HasExtraTargets => this.TargetIds.Count > 0;

    /// <summary>Read the settings stored on a grabber.</summary>
    public static GrabberSettings Load(Object grabber)
    {
        GrabberSettings settings = new();

        if (grabber.modData.TryGetValue(GrabberSettings.TargetsKey, out string? targets))
            settings.TargetIds.UnionWith(GrabberSettings.Split(targets));

        if (grabber.modData.TryGetValue(GrabberSettings.ScopeKey, out string? scope) && Enum.TryParse(scope, out ScopeMode parsedScope))
            settings.Scope = parsedScope;

        if (grabber.modData.TryGetValue(GrabberSettings.LocationsKey, out string? locations))
            settings.SelectedLocations.UnionWith(GrabberSettings.Split(locations));

        if (grabber.modData.TryGetValue(GrabberSettings.FrequencyKey, out string? frequency) && Enum.TryParse(frequency, out GrabFrequency parsedFrequency))
            settings.Frequency = parsedFrequency;

        return settings;
    }

    /// <summary>Write the settings back onto a grabber.</summary>
    public void Save(Object grabber)
    {
        GrabberSettings.Write(grabber, GrabberSettings.TargetsKey, string.Join(",", this.TargetIds));
        GrabberSettings.Write(grabber, GrabberSettings.ScopeKey, this.Scope == ScopeMode.Local ? null : this.Scope.ToString());
        GrabberSettings.Write(grabber, GrabberSettings.LocationsKey, string.Join(",", this.SelectedLocations));
        GrabberSettings.Write(grabber, GrabberSettings.FrequencyKey, this.Frequency == GrabFrequency.Default ? null : this.Frequency.ToString());
    }

    /// <summary>Get the locations this grabber should sweep this pass.</summary>
    /// <param name="grabber">The placed grabber.</param>
    /// <param name="config">The mod-wide settings.</param>
    public IEnumerable<GameLocation> ResolveLocations(Object grabber, ModConfig config)
    {
        GameLocation? home = grabber.Location;
        if (home == null)
            yield break;

        switch (this.Scope)
        {
            case ScopeMode.Local:
                if (GrabberSettings.IsHarvestable(home, config))
                    yield return home;
                break;

            case ScopeMode.Global:
                foreach (GameLocation location in GrabberSettings.AllLocations())
                {
                    if (GrabberSettings.IsHarvestable(location, config) && GrabberSettings.HasVisited(location))
                        yield return location;
                }
                break;

            case ScopeMode.Selected:
                foreach (GameLocation location in GrabberSettings.AllLocations())
                {
                    if (this.SelectedLocations.Contains(location.Name) && GrabberSettings.IsHarvestable(location, config))
                        yield return location;
                }
                break;
        }
    }

    /// <summary>Get every loaded location, including building interiors.</summary>
    public static List<GameLocation> AllLocations()
    {
        List<GameLocation> locations = new();
        Utility.ForEachLocation(location =>
        {
            locations.Add(location);
            return true;
        });
        return locations;
    }

    /// <summary>Get whether the player has set foot in a location.</summary>
    /// <remarks>
    ///   The game tracks this itself and backfilled it for saves upgraded to 1.6, so this works on an
    ///   existing save. It records <see cref="GameLocation.Name" /> rather than the unique name, so
    ///   every barn shares one entry.
    /// </remarks>
    public static bool HasVisited(GameLocation location)
    {
        return Game1.player.locationsVisited.Contains(location.Name);
    }

    /// <summary>Get whether a location should ever be harvested from.</summary>
    private static bool IsHarvestable(GameLocation location, ModConfig config)
    {
        if (location == null)
            return false;

        if (config.SkipFestivalLocations && (location.IsTemporary || Game1.isFestival()))
            return false;

        return true;
    }

    private static void Write(Object grabber, string key, string? value)
    {
        if (string.IsNullOrEmpty(value))
            grabber.modData.Remove(key);
        else
            grabber.modData[key] = value;
    }

    private static IEnumerable<string> Split(string value)
    {
        return value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(part => part.Trim()).Where(part => part.Length > 0);
    }
}
