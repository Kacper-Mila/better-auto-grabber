using System;
using System.Collections.Generic;
using System.Linq;
using StardewValley;
using StardewValley.GameData.Crops;
using StardewValley.GameData.FarmAnimals;
using StardewValley.GameData.FruitTrees;
using StardewValley.GameData.Locations;
using StardewValley.GameData.Machines;
using StardewValley.GameData.Objects;
using StardewValley.ItemTypeDefinitions;
using StardewValley.TerrainFeatures;

namespace BetterAutoGrabber.Framework;

/// <summary>The list of things a grabber can be told to collect, built from the loaded game data.</summary>
/// <remarks>
///   This is rebuilt on save load rather than hardcoded, so items added by content packs show up on the
///   list alongside vanilla ones.
///
///   Enumeration only ever names things; it never decides what a grabber may take. Anything a data
///   asset can't be read for -- a bush a framework mod grows, an item query that only resolves as it's
///   spawned -- is covered by its group's wildcard row instead, so content this list has never heard of
///   is still collectable the day it's installed. See <see cref="GrabberSettings.Wants" />.
/// </remarks>
internal static class TargetCatalog
{
    /// <summary>The wildcard row for the forage group.</summary>
    /// <remarks>Named for what it was before groups had wildcards, because the ID is saved on grabbers.</remarks>
    public const string OtherForageId = "forage:*";

    /// <summary>The wildcard row for the crops group.</summary>
    public const string OtherCropsId = "crop:*";

    /// <summary>The wildcard row for the fruit tree group.</summary>
    public const string OtherFruitId = "fruit:*";

    /// <summary>The wildcard row for the bush group.</summary>
    public const string OtherBushesId = "bush:*";

    /// <summary>The wildcard row for the litter group.</summary>
    public const string OtherLitterId = "litter:*";

    /// <summary>The wildcard row for the animal group.</summary>
    public const string OtherAnimalsId = "animal:*";

    /// <summary>The wildcard row for the machine group.</summary>
    public const string OtherMachinesId = "machine:*";

    /// <summary>Every wildcard row's ID, which is what makes one recognisable as a wildcard.</summary>
    private static readonly HashSet<string> Wildcards = new()
    {
        TargetCatalog.OtherForageId,
        TargetCatalog.OtherCropsId,
        TargetCatalog.OtherFruitId,
        TargetCatalog.OtherBushesId,
        TargetCatalog.OtherLitterId,
        TargetCatalog.OtherAnimalsId,
        TargetCatalog.OtherMachinesId
    };

    private static readonly List<HarvestTarget> Targets = new();
    private static readonly Dictionary<string, HarvestTarget> ByIdLookup = new();
    private static readonly HashSet<string> AnimalProductIds = new();
    private static readonly Dictionary<string, string> LitterRowByItem = new();

    /// <summary>Every row, in display order.</summary>
    public static IReadOnlyList<HarvestTarget> All => TargetCatalog.Targets;

    /// <summary>Get a row by its saved ID, or <c>null</c> if the data it came from is no longer loaded.</summary>
    public static HarvestTarget? Get(string id)
    {
        return TargetCatalog.ByIdLookup.TryGetValue(id, out HarvestTarget? target) ? target : null;
    }

    /// <summary>Get the wildcard row that answers for a target when the grabber hasn't been asked about it, if its group has one.</summary>
    /// <param name="targetId">The row's saved ID.</param>
    /// <remarks>
    ///   Groups whose rows are fixed and few -- stumps, dig spots, trees, trash cans -- have no wildcard:
    ///   there's nothing a mod can add to them that the list wouldn't already show, so "everything else"
    ///   would mean nothing.
    /// </remarks>
    public static string? WildcardFor(string targetId)
    {
        int separator = targetId.IndexOf(':');
        if (separator < 0)
            return null;

        string wildcard = targetId[..separator] + ":*";
        return wildcard == targetId || !TargetCatalog.Wildcards.Contains(wildcard)
            ? null
            : wildcard;
    }

    /// <summary>Get whether a row is a group's wildcard rather than a thing in its own right.</summary>
    /// <param name="targetId">The row's saved ID.</param>
    public static bool IsWildcard(string targetId) => TargetCatalog.Wildcards.Contains(targetId);

    /// <summary>Get the wildcard row for a group, or <c>null</c> if it doesn't have one.</summary>
    /// <param name="group">The group.</param>
    public static string? WildcardForGroup(TargetGroup group)
    {
        return group switch
        {
            TargetGroup.Forage => TargetCatalog.OtherForageId,
            TargetGroup.Crops => TargetCatalog.OtherCropsId,
            TargetGroup.FruitTrees => TargetCatalog.OtherFruitId,
            TargetGroup.Bushes => TargetCatalog.OtherBushesId,
            TargetGroup.Litter => TargetCatalog.OtherLitterId,
            TargetGroup.Animals => TargetCatalog.OtherAnimalsId,
            TargetGroup.Machines => TargetCatalog.OtherMachinesId,
            _ => null
        };
    }

    /// <summary>Add a row for a bush yield the world turned out to hold, if it isn't listed already.</summary>
    /// <param name="qualifiedItemId">The item the bush gives when shaken.</param>
    /// <remarks>
    ///   Bushes are the one group with no data asset behind them: what a bush drops is decided by
    ///   <see cref="StardewValley.TerrainFeatures.Bush.GetShakeOffItem" />, which vanilla hardcodes and
    ///   framework mods patch. So the rows for modded bushes are learned from the bushes themselves, and
    ///   remembered in save data so a row doesn't disappear the moment its bush is out of season.
    /// </remarks>
    public static void AddDiscoveredBush(string qualifiedItemId)
    {
        HarvestTarget? added = TargetCatalog.Add(TargetCatalog.BushId(qualifiedItemId), TargetCatalog.NameOf(qualifiedItemId), TargetGroup.Bushes, qualifiedItemId);
        if (added != null)
            TargetCatalog.ByIdLookup[added.Id] = added;
    }

    /// <summary>Build the target ID for a forage item.</summary>
    public static string ForageId(string qualifiedItemId) => "forage:" + qualifiedItemId;

    /// <summary>Build the target ID for a harvested crop.</summary>
    public static string CropId(string qualifiedItemId) => "crop:" + qualifiedItemId;

    /// <summary>The spring onion's qualified item ID.</summary>
    public const string SpringOnionItemId = "(O)399";

    /// <summary>Ginger's qualified item ID.</summary>
    public const string GingerItemId = "(O)829";

    /// <summary>Build the target ID for a tree fruit.</summary>
    public static string FruitId(string qualifiedItemId) => "fruit:" + qualifiedItemId;

    /// <summary>Build the target ID for a bush yield.</summary>
    public static string BushId(string qualifiedItemId) => "bush:" + qualifiedItemId;

    /// <summary>Build the target ID for a resource clump.</summary>
    public static string ClumpId(int parentSheetIndex) => "clump:" + parentSheetIndex;

    /// <summary>Build the target ID for a piece of breakable litter.</summary>
    public static string LitterId(string qualifiedItemId) => "litter:" + qualifiedItemId;

    /// <summary>Get the row a piece of litter is ticked under, or <c>null</c> if it isn't a listed one.</summary>
    /// <param name="qualifiedItemId">The rock, twig or weed standing on the tile.</param>
    /// <remarks>
    ///   Litter is the one group where a row doesn't answer for a single item ID. The game has eighteen
    ///   separate stones all called "Stone" and six called "Snowy Stone", so rows are merged by the name
    ///   they'd be listed under and this is what maps an item back to the row it ended up in.
    /// </remarks>
    public static string? LitterRowFor(string qualifiedItemId)
    {
        return TargetCatalog.LitterRowByItem.GetValueOrDefault(qualifiedItemId);
    }

    /// <summary>The target ID for artifact spots.</summary>
    public const string ArtifactSpotId = "dig:artifact";

    /// <summary>The target ID for seed spots.</summary>
    public const string SeedSpotId = "dig:seed";

    /// <summary>The target ID for the glittering panning spot in a location's water.</summary>
    public const string PanningSpotId = "dig:panning";

    /// <summary>The target ID for shaking wild trees.</summary>
    public const string ShakeTreesId = "tree:shake";

    /// <summary>The target ID for cutting the moss off wild trees.</summary>
    public const string MossId = "tree:moss";

    /// <summary>The target ID for rummaging in trash cans.</summary>
    public const string TrashCanId = "trash:can";

    /// <summary>Build the target ID for an animal product.</summary>
    public static string AnimalId(string qualifiedItemId) => "animal:" + qualifiedItemId;

    /// <summary>The target ID for slime balls on a slime hutch floor.</summary>
    public const string SlimeBallId = "animal:slime-ball";

    /// <summary>The slime ball's qualified item ID.</summary>
    public const string SlimeBallItemId = "(BC)56";

    /// <summary>Get whether an item is something a farm animal produces.</summary>
    /// <remarks>
    ///   These have their own group and their own pass, so the forage pass has to leave them alone:
    ///   eggs and truffles are spawned objects lying on the floor like any other forage, and its
    ///   catch-all row would otherwise collect them whatever the Animals group says.
    /// </remarks>
    public static bool IsAnimalProduct(string qualifiedItemId)
    {
        return TargetCatalog.AnimalProductIds.Contains(qualifiedItemId);
    }

    /// <summary>Build the target ID for a machine.</summary>
    public static string MachineId(string qualifiedItemId) => "machine:" + qualifiedItemId;

    /// <summary>The crab pot's qualified item ID, which is a machine the game handles specially.</summary>
    public const string CrabPotItemId = "(O)710";

    /// <summary>The auto-grabber's own qualified item ID.</summary>
    public const string AutoGrabberItemId = "(BC)165";

    /// <summary>Rebuild the catalog from the currently loaded game data.</summary>
    /// <param name="discoveredBushDrops">The bush yields earlier days of this save have seen, which no data asset lists.</param>
    public static void Rebuild(IEnumerable<string>? discoveredBushDrops = null)
    {
        TargetCatalog.Targets.Clear();
        TargetCatalog.ByIdLookup.Clear();
        TargetCatalog.AnimalProductIds.Clear();
        TargetCatalog.LitterRowByItem.Clear();

        TargetCatalog.AddForage();
        TargetCatalog.AddCrops();
        TargetCatalog.AddFruitTrees();
        TargetCatalog.AddBushes(discoveredBushDrops ?? Enumerable.Empty<string>());
        TargetCatalog.AddClumps();
        TargetCatalog.AddLitter();
        TargetCatalog.AddDigging();
        TargetCatalog.AddTrees();
        TargetCatalog.AddTrashCans();
        TargetCatalog.AddAnimals();
        TargetCatalog.AddMachines();

        foreach (HarvestTarget target in TargetCatalog.Targets)
            TargetCatalog.ByIdLookup[target.Id] = target;
    }

    /// <summary>Forage the game drops on the ground from its own code, which no data asset lists.</summary>
    /// <remarks>
    ///   <c>Data/Locations</c> covers ten maps and misses the rest. Coral, sea urchins and seaweed are
    ///   placed by <c>Beach.DayUpdate</c>, <c>IslandWest.DayUpdate</c> and the dangerous mines; magma caps
    ///   and dragon teeth are placed as each volcano floor is generated. All of them are ordinary spawned
    ///   objects once they land, so the forage pass has always collected them under the group's wildcard
    ///   -- they just had no row to be ticked or refused on. Hardcoded because the game hardcodes them,
    ///   the same reason the vanilla bush rows are.
    /// </remarks>
    private static readonly string[] CodeSpawnedForage =
    {
        "(O)393", // Coral
        "(O)397", // Sea Urchin
        "(O)152", // Seaweed
        "(O)851", // Magma Cap
        "(O)852"  // Dragon Tooth
    };

    /// <summary>Add a row for every item that can be found lying on the ground as forage.</summary>
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

        foreach (string id in TargetCatalog.CodeSpawnedForage)
            TargetCatalog.CollectItemIds(id, null, itemIds);

        // Forage can also be placed by event scripts, content packs and weather, none of which is listed
        // in Data/Locations. This row answers for anything the loop below didn't name.
        TargetCatalog.Add(TargetCatalog.OtherForageId, I18n.Target_EverythingElse(), TargetGroup.Forage, null);

        foreach (string id in TargetCatalog.SortByName(itemIds))
            TargetCatalog.Add(TargetCatalog.ForageId(id), TargetCatalog.NameOf(id), TargetGroup.Forage, id);
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

        // Spring onions and ginger are crops with no entry in Data/Crops: the game grows them as forage
        // crops and hardcodes what they give in Crop.harvest and Crop.hitWithHoe. They're listed here
        // rather than under Forage because that's what they are in the world -- something growing in
        // soil that the grabber harvests, not something lying on the ground.
        TargetCatalog.CollectItemIds(TargetCatalog.SpringOnionItemId, null, itemIds);
        TargetCatalog.CollectItemIds(TargetCatalog.GingerItemId, null, itemIds);

        TargetCatalog.Add(TargetCatalog.OtherCropsId, I18n.Target_EverythingElse(), TargetGroup.Crops, null);

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

        TargetCatalog.Add(TargetCatalog.OtherFruitId, I18n.Target_EverythingElse(), TargetGroup.FruitTrees, null);

        foreach (string id in TargetCatalog.SortByName(itemIds))
            TargetCatalog.Add(TargetCatalog.FruitId(id), TargetCatalog.NameOf(id), TargetGroup.FruitTrees, id);
    }

    /// <summary>Add a row for each thing a bush can be shaken for.</summary>
    /// <param name="discovered">The yields earlier days of this save saw bushes holding.</param>
    /// <remarks>
    ///   The three vanilla rows are hardcoded because the game hardcodes them: <c>Bush.GetShakeOffItem</c>
    ///   is a switch on the bush's size with no data asset behind it. Everything else in this group is
    ///   learned from the world by <see cref="DiscoveredBushes" />, which is how bushes grown by a
    ///   framework mod get a row of their own without this mod knowing that framework exists.
    /// </remarks>
    private static void AddBushes(IEnumerable<string> discovered)
    {
        TargetCatalog.Add(TargetCatalog.OtherBushesId, I18n.Target_EverythingElse(), TargetGroup.Bushes, null);

        HashSet<string> itemIds = new() { "(O)296", "(O)410", "(O)815" };
        foreach (string id in discovered)
        {
            if (ItemRegistry.GetData(id) != null)
                itemIds.Add(id);
        }

        foreach (string id in TargetCatalog.SortByName(itemIds))
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

    /// <summary>The coal nodes, which the game gives no name of their own.</summary>
    /// <remarks>
    ///   Every other node is named for what's in it -- Copper Stone, Omni Geode Stone -- but the four
    ///   coal nodes are called "Stone" in <c>Data/Objects</c> like the plain grey rocks, so merging rows
    ///   by name would bury them in the Stone row with no way to ask for coal on its own. They're the
    ///   one litter row this mod names itself.
    /// </remarks>
    private static readonly HashSet<string> CoalNodeIds = new()
    {
        "BasicCoalNode0",
        "BasicCoalNode1",
        "VolcanoCoalNode0",
        "VolcanoCoalNode1"
    };

    /// <summary>Add a row for every rock, twig and weed that can be broken where it stands.</summary>
    /// <remarks>
    ///   Rows come from <c>Data/Objects</c>: litter is category -999, and the game tells the three kinds
    ///   apart by name (<c>Object.IsBreakableStone</c>, <c>IsTwig</c>, <c>IsWeeds</c>), so a stone added by
    ///   a content pack is listed the day it's installed.
    ///
    ///   Unlike every other group, several item IDs share one row. The game has eighteen stones named
    ///   "Stone" and six named "Snowy Stone"; listing them separately would be eighteen identical rows
    ///   that nobody could tell apart, so they're merged under the name they'd all carry anyway.
    /// </remarks>
    private static void AddLitter()
    {
        TargetCatalog.Add(TargetCatalog.OtherLitterId, I18n.Target_EverythingElse(), TargetGroup.Litter, null);

        Dictionary<string, List<string>> byRowName = new();
        foreach ((string id, ObjectData? data) in Game1.objectData)
        {
            if (data == null || data.Category != -999)
                continue;

            string name = data.Name ?? "";
            bool isLitter = name == "Stone" || name == "Twig" || name.Contains("weeds", StringComparison.OrdinalIgnoreCase);
            if (!isLitter)
                continue;

            string qualified = "(O)" + id;
            string rowName = TargetCatalog.CoalNodeIds.Contains(id)
                ? I18n.Target_CoalNode()
                : TargetCatalog.NameOf(qualified);

            if (!byRowName.TryGetValue(rowName, out List<string>? members))
                byRowName[rowName] = members = new List<string>();
            members.Add(qualified);
        }

        foreach ((string rowName, List<string> members) in byRowName.OrderBy(pair => pair.Key, StringComparer.CurrentCultureIgnoreCase))
        {
            // The row is filed under one of its members, and which one has to stay put: if a content
            // pack's stone could take the ID, everyone's tick would quietly move to a different row the
            // day they installed it. Vanilla's numeric IDs sort ahead of the string IDs mods use.
            string owner = members.OrderBy(id => TargetCatalog.LitterSortKey(id)).ThenBy(id => id, StringComparer.Ordinal).First();

            TargetCatalog.Add(TargetCatalog.LitterId(owner), rowName, TargetGroup.Litter, owner);

            foreach (string member in members)
                TargetCatalog.LitterRowByItem[member] = TargetCatalog.LitterId(owner);
        }
    }

    /// <summary>Sort a litter item ID so that the game's own numeric IDs come before the string IDs content packs use.</summary>
    private static (int Rank, int Number) LitterSortKey(string qualifiedItemId)
    {
        string id = qualifiedItemId.StartsWith("(O)", StringComparison.Ordinal) ? qualifiedItemId[3..] : qualifiedItemId;
        return int.TryParse(id, out int number)
            ? (0, number)
            : (1, 0);
    }

    /// <summary>Add a row for each spot that has to be worked with a tool for whatever it's hiding.</summary>
    /// <remarks>
    ///   A panning spot is panned rather than dug, but it belongs here rather than in a group of its
    ///   own: it's the same bargain as the other two rows, one spot worked with one tool for a random
    ///   handful, and a group with a single row in it would only be harder to find.
    /// </remarks>
    private static void AddDigging()
    {
        TargetCatalog.Add(TargetCatalog.ArtifactSpotId, I18n.Target_ArtifactSpot(), TargetGroup.Digging, "(O)590");
        TargetCatalog.Add(TargetCatalog.SeedSpotId, I18n.Target_SeedSpot(), TargetGroup.Digging, "(O)SeedSpot");
        TargetCatalog.Add(TargetCatalog.PanningSpotId, I18n.Target_PanningSpot(), TargetGroup.Digging, "(T)Pan");
    }

    /// <summary>Add the rows for the things a wild tree can be worked for without felling it.</summary>
    /// <remarks>
    ///   One shake row rather than one per species: shaking is a single action with an unpredictable
    ///   yield — seeds, the occasional mystery box, whatever a content pack has added — so there's
    ///   nothing meaningful to pick between. Moss is its own row because it's a separate action with a
    ///   price attached: cutting it sets the tree back several growth stages, which the player should
    ///   get to decide about apart from shaking.
    /// </remarks>
    private static void AddTrees()
    {
        TargetCatalog.Add(TargetCatalog.ShakeTreesId, I18n.Target_ShakeTrees(), TargetGroup.Trees, "(O)309");
        TargetCatalog.Add(TargetCatalog.MossId, I18n.Target_HarvestMoss(), TargetGroup.Trees, "(O)Moss");
    }

    /// <summary>Add the row for rummaging in trash cans.</summary>
    /// <remarks>
    ///   One row rather than one per can, for the same reasons the scope tab lists building families
    ///   rather than buildings: <c>Data/GarbageCans</c> keys are internal handles (<c>JodiAndKent</c>,
    ///   <c>Mayor</c>) with nothing to label a row with, and the yield is a random roll either way.
    ///   Which cans a grabber reaches is the scope tab's job, not this list's.
    /// </remarks>
    private static void AddTrashCans()
    {
        TargetCatalog.Add(TargetCatalog.TrashCanId, I18n.Target_TrashCan(), TargetGroup.TrashCans, "(O)168");
    }

    /// <summary>Add a row for every item a farm animal produces.</summary>
    /// <remarks>
    ///   One row per item rather than one per animal, so that ticking Egg doesn't quietly hand you
    ///   Large Eggs as well. Deluxe produce is listed alongside the ordinary kind for the same reason.
    /// </remarks>
    private static void AddAnimals()
    {
        HashSet<string> itemIds = new();
        foreach (FarmAnimalData data in Game1.farmAnimalData.Values)
        {
            if (data == null)
                continue;

            foreach (FarmAnimalProduce produce in (data.ProduceItemIds ?? new()).Concat(data.DeluxeProduceItemIds ?? new()))
                TargetCatalog.CollectItemIds(produce?.ItemId, null, itemIds);
        }

        TargetCatalog.AnimalProductIds.UnionWith(itemIds);

        TargetCatalog.Add(TargetCatalog.OtherAnimalsId, I18n.Target_EverythingElse(), TargetGroup.Animals, null);

        foreach (string id in TargetCatalog.SortByName(itemIds))
            TargetCatalog.Add(TargetCatalog.AnimalId(id), TargetCatalog.NameOf(id), TargetGroup.Animals, id);

        // Slime balls aren't farm animal produce -- a slime is a monster, not a FarmAnimal -- but a
        // hutch full of them is the same chore, so the row lives in the same group. It's listed as the
        // ball rather than as Slime, because slime also drops from anything you kill.
        TargetCatalog.Add(TargetCatalog.SlimeBallId, I18n.Target_SlimeBall(), TargetGroup.Animals, TargetCatalog.SlimeBallItemId);
    }

    /// <summary>Add a row for every machine that can hold an output.</summary>
    private static void AddMachines()
    {
        Dictionary<string, MachineData> machines = DataLoader.Machines(Game1.content);
        HashSet<string> machineIds = new(machines.Keys);

        // crab pots have their own class and their own collection rules, so they're listed explicitly
        machineIds.Add(TargetCatalog.CrabPotItemId);

        TargetCatalog.Add(TargetCatalog.OtherMachinesId, I18n.Target_EverythingElse(), TargetGroup.Machines, null);

        foreach (string id in TargetCatalog.SortByName(machineIds))
        {
            // the auto-grabber is a machine too; letting one empty another is asking for trouble
            if (id == TargetCatalog.AutoGrabberItemId || ItemRegistry.GetData(id) == null)
                continue;

            // An incubator's output is the egg that was put into it, so there's nothing to collect that
            // a player would want: taking it hands the egg back and cancels the hatch. Reading the flag
            // rather than matching IDs covers the ostrich incubator and any a content pack declares.
            if (machines.TryGetValue(id, out MachineData? data) && data.IsIncubator)
                continue;

            TargetCatalog.Add(TargetCatalog.MachineId(id), TargetCatalog.NameOf(id), TargetGroup.Machines, id);
        }
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

    /// <summary>Add a row, unless one with the same ID is already listed.</summary>
    /// <returns>The row that was added, or <c>null</c> if it was already there.</returns>
    private static HarvestTarget? Add(string id, string displayName, TargetGroup group, string? iconItemId)
    {
        if (TargetCatalog.Targets.Any(target => target.Id == id))
            return null;

        HarvestTarget target = new(id, displayName, group, iconItemId);
        TargetCatalog.Targets.Add(target);
        return target;
    }
}
