using System;
using System.Collections.Generic;
using System.Linq;
using StardewValley;
using StardewValley.GameData.Crops;
using StardewValley.GameData.FruitTrees;
using StardewValley.GameData.Locations;
using StardewValley.ItemTypeDefinitions;
using StardewValley.TerrainFeatures;

namespace BetterAutoGrabber.Framework;

/// <summary>The list of things a grabber can be told to collect, built from the loaded game data.</summary>
/// <remarks>
///   This is rebuilt on save load rather than hardcoded, so items added by content packs show up on the
///   list alongside vanilla ones.
/// </remarks>
internal static class TargetCatalog
{
    /// <summary>The catch-all row matching any forage the catalog didn't know about.</summary>
    public const string OtherForageId = "forage:*";

    private static readonly List<HarvestTarget> Targets = new();
    private static readonly Dictionary<string, HarvestTarget> ByIdLookup = new();

    /// <summary>Every row, in display order.</summary>
    public static IReadOnlyList<HarvestTarget> All => TargetCatalog.Targets;

    /// <summary>Get a row by its saved ID, or <c>null</c> if the data it came from is no longer loaded.</summary>
    public static HarvestTarget? Get(string id)
    {
        return TargetCatalog.ByIdLookup.TryGetValue(id, out HarvestTarget? target) ? target : null;
    }

    /// <summary>Build the target ID for a forage item.</summary>
    public static string ForageId(string qualifiedItemId) => "forage:" + qualifiedItemId;

    /// <summary>Build the target ID for a harvested crop.</summary>
    public static string CropId(string qualifiedItemId) => "crop:" + qualifiedItemId;

    /// <summary>Build the target ID for a tree fruit.</summary>
    public static string FruitId(string qualifiedItemId) => "fruit:" + qualifiedItemId;

    /// <summary>Build the target ID for a bush yield.</summary>
    public static string BushId(string qualifiedItemId) => "bush:" + qualifiedItemId;

    /// <summary>Build the target ID for a resource clump.</summary>
    public static string ClumpId(int parentSheetIndex) => "clump:" + parentSheetIndex;

    /// <summary>The target ID for artifact spots.</summary>
    public const string ArtifactSpotId = "dig:artifact";

    /// <summary>The target ID for seed spots.</summary>
    public const string SeedSpotId = "dig:seed";

    /// <summary>Rebuild the catalog from the currently loaded game data.</summary>
    public static void Rebuild()
    {
        TargetCatalog.Targets.Clear();
        TargetCatalog.ByIdLookup.Clear();

        TargetCatalog.AddForage();
        TargetCatalog.AddCrops();
        TargetCatalog.AddFruitTrees();
        TargetCatalog.AddBushes();
        TargetCatalog.AddClumps();
        TargetCatalog.AddDigging();

        foreach (HarvestTarget target in TargetCatalog.Targets)
            TargetCatalog.ByIdLookup[target.Id] = target;
    }

    /// <summary>Add a row for every item that <c>Data/Locations</c> can spawn as forage.</summary>
    private static void AddForage()
    {
        HashSet<string> itemIds = new();
        foreach (LocationData data in Game1.locationData.Values)
        {
            if (data.Forage == null)
                continue;

            foreach (SpawnForageData forage in data.Forage)
            {
                TargetCatalog.CollectItemIds(forage.ItemId, forage.RandomItemId, itemIds);
            }
        }

        foreach (string id in TargetCatalog.SortByName(itemIds))
            TargetCatalog.Add(TargetCatalog.ForageId(id), TargetCatalog.NameOf(id), TargetGroup.Forage, id);

        // Forage can also be placed by event scripts, content packs and weather, none of which is
        // listed in Data/Locations. This row covers whatever the loop above couldn't enumerate.
        TargetCatalog.Add(TargetCatalog.OtherForageId, I18n.Target_OtherForage(), TargetGroup.Forage, "(O)16");
    }

    /// <summary>Add a row for every crop's harvested item.</summary>
    private static void AddCrops()
    {
        HashSet<string> itemIds = new();
        foreach (CropData data in Game1.cropData.Values)
        {
            if (data?.HarvestItemId != null)
                TargetCatalog.CollectItemIds(data.HarvestItemId, null, itemIds);
        }

        foreach (string id in TargetCatalog.SortByName(itemIds))
            TargetCatalog.Add(TargetCatalog.CropId(id), TargetCatalog.NameOf(id), TargetGroup.Crops, id);
    }

    /// <summary>Add a row for every fruit a fruit tree can bear.</summary>
    private static void AddFruitTrees()
    {
        HashSet<string> itemIds = new();
        foreach (FruitTreeData data in Game1.fruitTreeData.Values)
        {
            if (data?.Fruit == null)
                continue;

            foreach (FruitTreeFruitData fruit in data.Fruit)
                TargetCatalog.CollectItemIds(fruit.ItemId, fruit.RandomItemId, itemIds);
        }

        foreach (string id in TargetCatalog.SortByName(itemIds))
            TargetCatalog.Add(TargetCatalog.FruitId(id), TargetCatalog.NameOf(id), TargetGroup.FruitTrees, id);
    }

    /// <summary>Add a row for each thing a bush can be shaken for.</summary>
    private static void AddBushes()
    {
        foreach (string id in new[] { "(O)296", "(O)410", "(O)815" })
            TargetCatalog.Add(TargetCatalog.BushId(id), TargetCatalog.NameOf(id), TargetGroup.Bushes, id);
    }

    /// <summary>Add a row for each resource clump, named after the clump rather than what it drops.</summary>
    private static void AddClumps()
    {
        TargetCatalog.Add(TargetCatalog.ClumpId(ResourceClump.stumpIndex), I18n.Target_LargeStump(), TargetGroup.Clumps, "(O)709");
        TargetCatalog.Add(TargetCatalog.ClumpId(ResourceClump.hollowLogIndex), I18n.Target_LargeLog(), TargetGroup.Clumps, "(O)709");
        TargetCatalog.Add(TargetCatalog.ClumpId(ResourceClump.boulderIndex), I18n.Target_Boulder(), TargetGroup.Clumps, "(O)390");
        TargetCatalog.Add(TargetCatalog.ClumpId(ResourceClump.meteoriteIndex), I18n.Target_Meteorite(), TargetGroup.Clumps, "(O)386");
        TargetCatalog.Add(TargetCatalog.ClumpId(ResourceClump.mineRock1Index), I18n.Target_MineBoulder(), TargetGroup.Clumps, "(O)390");
    }

    /// <summary>Add a row for each spot that has to be dug up.</summary>
    private static void AddDigging()
    {
        TargetCatalog.Add(TargetCatalog.ArtifactSpotId, I18n.Target_ArtifactSpot(), TargetGroup.Digging, "(O)590");
        TargetCatalog.Add(TargetCatalog.SeedSpotId, I18n.Target_SeedSpot(), TargetGroup.Digging, "(O)SeedSpot");
    }

    /// <summary>Add the qualified form of any of the given IDs which resolve to a real item.</summary>
    private static void CollectItemIds(string? itemId, List<string>? randomItemIds, HashSet<string> found)
    {
        foreach (string? id in new[] { itemId }.Concat(randomItemIds ?? Enumerable.Empty<string>()))
        {
            if (string.IsNullOrWhiteSpace(id))
                continue;

            // Data fields may hold item queries (like FLAVORED_ITEM or RANDOM_ITEMS) rather than a
            // plain ID. Those can't be resolved without a spawn context, so they're skipped; the
            // catch-all forage row is what covers anything they'd produce.
            string? qualified = ItemRegistry.QualifyItemId(id);
            if (qualified != null && ItemRegistry.GetData(qualified) != null)
                found.Add(qualified);
        }
    }

    /// <summary>Get an item's display name, falling back to its ID if the item is missing.</summary>
    private static string NameOf(string qualifiedItemId)
    {
        ParsedItemData? data = ItemRegistry.GetData(qualifiedItemId);
        return data?.DisplayName ?? qualifiedItemId;
    }

    /// <summary>Sort item IDs by the name they'll be listed under.</summary>
    private static IEnumerable<string> SortByName(IEnumerable<string> itemIds)
    {
        return itemIds.OrderBy(TargetCatalog.NameOf, StringComparer.CurrentCultureIgnoreCase);
    }

    private static void Add(string id, string displayName, TargetGroup group, string iconItemId)
    {
        if (!TargetCatalog.Targets.Any(target => target.Id == id))
            TargetCatalog.Targets.Add(new HarvestTarget(id, displayName, group, iconItemId));
    }
}
