using System;
using System.Collections.Generic;
using System.Linq;
using StardewValley;
using StardewValley.GameData.Buildings;
using StardewValley.TokenizableStrings;
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

/// <summary>Which seeds a grabber may put back in the soil after harvesting a crop.</summary>
internal enum ReplantMode
{
    /// <summary>Leave the soil empty.</summary>
    Never,

    /// <summary>Replant the crop that was just harvested, when its seed is in the grabber.</summary>
    MatchingSeed,

    /// <summary>Replant the harvested crop's seed, or any other seed in the grabber when that one has run out.</summary>
    AnySeed
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
    private const string DeniedKey = GrabberSettings.KeyPrefix + "denied";
    private const string SchemaKey = GrabberSettings.KeyPrefix + "schema";
    private const string ScopeKey = GrabberSettings.KeyPrefix + "scope";
    private const string LocationsKey = GrabberSettings.KeyPrefix + "locations";
    private const string FrequencyKey = GrabberSettings.KeyPrefix + "frequency";
    private const string ReplantKey = GrabberSettings.KeyPrefix + "replant";

    /// <summary>The schema the settings on a grabber are written in, so older ones can be read as they were meant.</summary>
    private const int CurrentSchema = 2;

    /// <summary>The IDs of the targets this grabber collects. Empty means it behaves exactly like a vanilla grabber.</summary>
    /// <remarks>A group's wildcard row is stored here like any other ID.</remarks>
    public HashSet<string> TargetIds { get; } = new();

    /// <summary>The IDs of the targets this grabber has been told to leave alone.</summary>
    /// <remarks>
    ///   Only meaningful under a wildcard: without one, a row that isn't in <see cref="TargetIds" /> is
    ///   left alone anyway. This is what lets "everything in this group except that one" be said at all.
    /// </remarks>
    public HashSet<string> DeniedIds { get; } = new();

    /// <summary>Which locations this grabber reaches.</summary>
    public ScopeMode Scope { get; set; } = ScopeMode.Local;

    /// <summary>The locations picked for <see cref="ScopeMode.Selected" />, by <see cref="GrabberSettings.SelectionKey" />.</summary>
    public HashSet<string> SelectedLocations { get; } = new();

    /// <summary>How often this grabber runs, or <see cref="GrabFrequency.Default" /> to follow the mod-wide setting.</summary>
    public GrabFrequency Frequency { get; set; } = GrabFrequency.Default;

    /// <summary>Which seeds this grabber replants after harvesting a crop.</summary>
    public ReplantMode Replant { get; set; } = ReplantMode.Never;

    /// <summary>Whether this grabber has been given anything to collect beyond what vanilla already gives it.</summary>
    public bool HasExtraTargets => this.TargetIds.Count > 0;

    /// <summary>Read the settings stored on a grabber.</summary>
    public static GrabberSettings Load(Object grabber)
    {
        GrabberSettings settings = new();

        if (grabber.modData.TryGetValue(GrabberSettings.TargetsKey, out string? targets))
            settings.TargetIds.UnionWith(GrabberSettings.Split(targets));

        if (grabber.modData.TryGetValue(GrabberSettings.DeniedKey, out string? denied))
            settings.DeniedIds.UnionWith(GrabberSettings.Split(denied));

        if (!grabber.modData.TryGetValue(GrabberSettings.SchemaKey, out string? schema) || !int.TryParse(schema, out int version) || version < 2)
            settings.UpgradeToWildcards();

        if (grabber.modData.TryGetValue(GrabberSettings.ScopeKey, out string? scope) && Enum.TryParse(scope, out ScopeMode parsedScope))
            settings.Scope = parsedScope;

        if (grabber.modData.TryGetValue(GrabberSettings.LocationsKey, out string? locations))
        {
            // Settings saved before buildings were grouped hold a tier ("Deluxe Coop"). Folding those
            // onto the family key keeps the tick working, and widens it to the small coops as well --
            // which is what the grouped row now means anyway.
            settings.SelectedLocations.UnionWith(GrabberSettings.Split(locations).Select(GrabberSettings.RootBuildingType));
        }

        if (grabber.modData.TryGetValue(GrabberSettings.FrequencyKey, out string? frequency) && Enum.TryParse(frequency, out GrabFrequency parsedFrequency))
            settings.Frequency = parsedFrequency;

        if (grabber.modData.TryGetValue(GrabberSettings.ReplantKey, out string? replant) && Enum.TryParse(replant, out ReplantMode parsedReplant))
            settings.Replant = parsedReplant;

        return settings;
    }

    /// <summary>Write the settings back onto a grabber.</summary>
    public void Save(Object grabber)
    {
        GrabberSettings.Write(grabber, GrabberSettings.TargetsKey, string.Join(",", this.TargetIds));
        GrabberSettings.Write(grabber, GrabberSettings.DeniedKey, string.Join(",", this.DeniedIds));
        GrabberSettings.Write(grabber, GrabberSettings.SchemaKey, GrabberSettings.CurrentSchema.ToString());
        GrabberSettings.Write(grabber, GrabberSettings.ScopeKey, this.Scope == ScopeMode.Local ? null : this.Scope.ToString());
        GrabberSettings.Write(grabber, GrabberSettings.LocationsKey, string.Join(",", this.SelectedLocations));
        GrabberSettings.Write(grabber, GrabberSettings.FrequencyKey, this.Frequency == GrabFrequency.Default ? null : this.Frequency.ToString());
        GrabberSettings.Write(grabber, GrabberSettings.ReplantKey, this.Replant == ReplantMode.Never ? null : this.Replant.ToString());
    }

    /// <summary>Get whether this grabber collects a target.</summary>
    /// <param name="targetId">The row's saved ID.</param>
    /// <remarks>
    ///   A row has three answers, not two. Ticked means yes and crossed means no, both of them the
    ///   player's own words; a row they've never touched has no answer at all, and the group's wildcard
    ///   row speaks for it. That third state is what keeps a grabber's behaviour steady as mods come and
    ///   go: the day something the list has never seen gets a row of its own, the row starts out unasked,
    ///   so it keeps being collected under the wildcard instead of quietly stopping.
    /// </remarks>
    public bool Wants(string targetId)
    {
        if (this.TargetIds.Contains(targetId))
            return true;

        if (this.DeniedIds.Contains(targetId))
            return false;

        string? wildcard = TargetCatalog.WildcardFor(targetId);
        return wildcard != null && this.TargetIds.Contains(wildcard);
    }

    /// <summary>Get whether a target was ticked by hand, rather than answered for by a wildcard.</summary>
    /// <param name="targetId">The row's saved ID.</param>
    public bool IsExplicit(string targetId) => this.TargetIds.Contains(targetId);

    /// <summary>Read settings written before wildcards answered for untouched rows.</summary>
    /// <remarks>
    ///   Forage is the only group that had a catch-all row before this, and it used to cover just the
    ///   items the catalog couldn't name. Under the new rule it would also cover every forage row left
    ///   unticked, which would quietly widen what an existing grabber collects, so those rows are written
    ///   down as refusals here. No other group needs this: with no wildcard to inherit from, an untouched
    ///   row and a refused one behave identically, and ticking a group's new wildcard later is a player
    ///   asking for the rest of the group on purpose.
    /// </remarks>
    private void UpgradeToWildcards()
    {
        if (!this.TargetIds.Contains(TargetCatalog.OtherForageId))
            return;

        foreach (HarvestTarget target in TargetCatalog.All)
        {
            if (target.Group == TargetGroup.Forage && !TargetCatalog.IsWildcard(target.Id) && !this.TargetIds.Contains(target.Id))
                this.DeniedIds.Add(target.Id);
        }
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
                    if (this.SelectedLocations.Contains(GrabberSettings.SelectionKey(location)) && GrabberSettings.IsHarvestable(location, config))
                        yield return location;
                }
                break;
        }
    }

    /// <summary>Get the key a location is ticked under on the scope tab.</summary>
    /// <remarks>
    ///   Building interiors are keyed by the family they belong to rather than by the building, so one
    ///   "Coop" row covers every coop you own at any tier. The game gives each interior a unique ID, but
    ///   nothing a player would recognise to label it with, so a row per building would have to read as
    ///   <c>Coop4cb0a4d1-3f8b-49c9-a375-eb8251426524</c>.
    /// </remarks>
    public static string SelectionKey(GameLocation location)
    {
        string? buildingType = location.ParentBuilding?.buildingType.Value;
        return buildingType != null
            ? GrabberSettings.RootBuildingType(buildingType)
            : location.Name;
    }

    /// <summary>Get the name shown for a location's row on the scope tab.</summary>
    /// <remarks>
    ///   Three sources, in the order they're trusted. A building interior is named by its building data.
    ///   Everything else is named by <c>Data/Locations</c> where that says anything, because the game's
    ///   own name is the canonical one and is already translated. Where it says nothing -- which is most
    ///   of the map, including the swamp, the bug lair, the casino and every part of Ginger Island -- the
    ///   mod supplies the name the wiki and the community use, so the row reads "Mutant Bug Lair" rather
    ///   than "BugLand".
    ///
    ///   Note this deliberately doesn't use <see cref="GameLocation.DisplayName" />, which falls back to
    ///   the containing location: that's what made the cellar read as "&lt;your farm&gt; Farm".
    /// </remarks>
    public static string SelectionName(GameLocation location)
    {
        // A building interior has no display name of its own, so GameLocation.DisplayName falls through
        // to the farm's -- every coop and barn would read as "<your farm> Farm". The building's own data
        // is where the readable name is.
        if (location.ParentBuilding != null && Game1.buildingData.TryGetValue(GrabberSettings.SelectionKey(location), out BuildingData? data))
        {
            string? name = TokenParser.ParseText(data.Name);
            if (!string.IsNullOrWhiteSpace(name))
                return name;
        }

        // GetDisplayName is the raw answer from Data/Locations, and is null for a location the asset
        // doesn't name. That null is the gap the mod's own names fill.
        string? fromGame = location.GetDisplayName();
        if (!string.IsNullOrWhiteSpace(fromGame))
            return fromGame;

        return GrabberSettings.CommunityName(location.Name) ?? location.Name;
    }

    /// <summary>Get the name this mod gives a map the game leaves unnamed, or <c>null</c> if it doesn't name that one.</summary>
    /// <param name="name">The location's internal name.</param>
    /// <remarks>
    ///   Maps that come in numbered copies -- <c>Cellar2</c> for the second cabin's cellar,
    ///   <c>VolcanoDungeon4</c> for the fourth floor down -- share the one entry their family is listed
    ///   under, so the trailing digits are dropped when the full name isn't listed itself.
    /// </remarks>
    private static string? CommunityName(string name)
    {
        string? exact = I18n.Location(name);
        if (exact != null)
            return exact;

        string family = name.TrimEnd(GrabberSettings.Digits);
        return family.Length > 0 && family != name
            ? I18n.Location(family)
            : null;
    }

    /// <summary>The digits stripped off a numbered map's name to find the family it belongs to.</summary>
    private static readonly char[] Digits = "0123456789".ToCharArray();

    /// <summary>Walk a building type back to the one it was first built as, so every tier shares a key.</summary>
    /// <remarks>
    ///   <c>BuildingData.BuildingToUpgrade</c> is what links the tiers: a Deluxe Coop upgrades from a Big
    ///   Coop, which upgrades from a Coop. Following it is data-driven, so a content pack that adds a
    ///   fourth coop tier joins the same row without being listed anywhere here.
    /// </remarks>
    public static string RootBuildingType(string buildingType)
    {
        HashSet<string> seen = new() { buildingType };

        while (Game1.buildingData != null && Game1.buildingData.TryGetValue(buildingType, out BuildingData? data))
        {
            string? parent = data.BuildingToUpgrade;

            // seen guards against a content pack declaring a cycle, which would otherwise hang the game
            if (string.IsNullOrWhiteSpace(parent) || !seen.Add(parent))
                break;

            buildingType = parent;
        }

        return buildingType;
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
