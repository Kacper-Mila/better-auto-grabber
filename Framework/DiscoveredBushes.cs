using System;
using System.Collections.Generic;
using System.Linq;
using StardewValley;
using StardewModdingAPI;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using Object = StardewValley.Object;

namespace BetterAutoGrabber.Framework;

/// <summary>Remembers what the bushes in a save turned out to hold, so each of them can have a row.</summary>
/// <remarks>
/// Every other group is built from a data asset, so a content pack's additions are on the list before
/// the player ever meets one. Bushes have no such asset: what a bush gives is decided by
/// <see cref="Bush.GetShakeOffItem" />, which vanilla hardcodes by bush size and framework mods patch.
/// The only way to know that a bush grows vanilla pods is to look at one, so the world is swept each
/// morning and whatever the bushes are holding is written down.
///
/// The findings are latched into save data rather than recomputed, because a bush only names its yield
/// while it's carrying one. Without that, a row would appear the morning a bush came into bloom and
/// vanish the moment it was picked, which is no use to someone trying to tick it. Until a bush has been
/// seen at least once, its group's wildcard row is what collects it.
/// </remarks>
internal sealed class DiscoveredBushes
{
    /*********
    ** Fields
    *********/
    /// <summary>The save data key the learned yields are stored under.</summary>
    private const string SaveKey = "discovered-bushes";

    private readonly IDataHelper Data;
    private readonly IMonitor Monitor;

    /// <summary>The qualified item IDs bushes in this save have been seen holding.</summary>
    private HashSet<string> Drops = new();

    /*********
    ** Accessors
    *********/
    /// <summary>The yields learned so far, for the catalog to build rows from.</summary>
    public IReadOnlyCollection<string> Yields => this.Drops;

    /*********
    ** Public methods
    *********/
    public DiscoveredBushes(IDataHelper data, IMonitor monitor)
    {
        this.Data = data;
        this.Monitor = monitor;
    }

    /// <summary>Read what earlier days of this save learned.</summary>
    public void Load()
    {
        this.Drops = new();
        if (!Context.IsMainPlayer)
            return; // save data belongs to the host, who is the only one harvesting anyway

        try
        {
            this.Drops = this.Data.ReadSaveData<HashSet<string>>(DiscoveredBushes.SaveKey) ?? new();
        }
        catch (Exception ex)
        {
            this.Monitor.Log($"Couldn't read the remembered bush yields, so today's sweep starts from scratch: {ex}", LogLevel.Warn);
        }
    }

    /// <summary>Sweep every loaded location for bushes and note what they're holding.</summary>
    public void Refresh()
    {
        if (!Context.IsMainPlayer)
            return;

        Utility.ForEachLocation(location =>
        {
            foreach (Bush bush in DiscoveredBushes.GetBushes(location))
                this.Note(bush);

            return true;
        });
    }

    /// <summary>Note what a bush is holding, and give it a row if it doesn't have one.</summary>
    /// <param name="bush">The bush to look at.</param>
    public void Note(Bush bush)
    {
        // A bush only answers while it's carrying something: the patched GetShakeOffItem returns null
        // for a modded bush with nothing cached, and vanilla's walnut bushes are never listed at all.
        if (bush.townBush.Value || bush.size.Value == Bush.walnutBush)
            return;

        string? shakeOff = bush.GetShakeOffItem();
        if (shakeOff == null)
            return;

        string? qualified = ItemRegistry.QualifyItemId(shakeOff);
        if (qualified != null)
            this.Note(qualified);
    }

    /// <summary>Note a yield a bush was seen holding, and give it a row if it doesn't have one.</summary>
    /// <param name="qualifiedItemId">The item the bush gives when shaken.</param>
    public void Note(string qualifiedItemId)
    {
        TargetCatalog.AddDiscoveredBush(qualifiedItemId);

        if (!Context.IsMainPlayer || !this.Drops.Add(qualifiedItemId))
            return;

        try
        {
            this.Data.WriteSaveData(DiscoveredBushes.SaveKey, this.Drops);
        }
        catch (Exception ex)
        {
            this.Monitor.Log($"Couldn't save the remembered bush yields; tomorrow's sweep will find them again: {ex}", LogLevel.Warn);
        }
    }

    /// <summary>Get every bush in a location, including the ones growing in garden pots.</summary>
    /// <param name="location">The location to look in.</param>
    /// <remarks>
    ///   A potted bush isn't a terrain feature at all: it hangs off the pot as <see cref="IndoorPot.bush" />,
    ///   which is where a tea sapling and every framework bush planted indoors ends up.
    /// </remarks>
    public static IEnumerable<Bush> GetBushes(GameLocation location)
    {
        // copies, because a caller may well be harvesting as it walks these
        foreach (LargeTerrainFeature feature in location.largeTerrainFeatures.ToArray())
        {
            if (feature is Bush bush)
                yield return bush;
        }

        foreach (Object obj in location.objects.Values.ToArray())
        {
            if (obj is IndoorPot pot && pot.bush.Value != null)
                yield return pot.bush.Value;
        }
    }
}
