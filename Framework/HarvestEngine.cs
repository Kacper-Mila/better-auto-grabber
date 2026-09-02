using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.GameData.FarmAnimals;
using StardewValley.GameData.Machines;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using xTile.Layers;
using xTile.Tiles;
using Object = StardewValley.Object;

namespace BetterAutoGrabber.Framework;

/// <summary>Harvests the targets a grabber has been told to collect.</summary>
internal sealed class HarvestEngine
{
    private readonly ModConfig Config;
    private readonly IMonitor Monitor;
    private readonly GrabberHarvester Harvester = new();

    /// <summary>The garbage cans found on each location's map, keyed by location, cached for the day.</summary>
    private readonly Dictionary<string, List<(Vector2 Tile, string Id)>> TrashCansByLocation = new();

    public HarvestEngine(ModConfig config, IMonitor monitor)
    {
        this.Config = config;
        this.Monitor = monitor;
    }

    /// <summary>Run one harvest pass for a grabber.</summary>
    /// <param name="grabber">The placed grabber.</param>
    /// <param name="settings">The grabber's settings.</param>
    /// <param name="locations">The locations it reaches this pass.</param>
    /// <returns>A report of what the pass collected and what it passed over.</returns>
    public HarvestReport Run(Object grabber, GrabberSettings settings, IEnumerable<GameLocation> locations)
    {
        HarvestReport report = new();
        if (grabber.heldObject.Value is not Chest chest || !settings.HasExtraTargets)
            return report;

        GrabberOutput output = new(chest, report);
        foreach (GameLocation location in locations)
        {
            if (output.IsFull)
            {
                report.StoppedWhenFull = true;
                break;
            }

            report.Locations.Add(location.NameOrUniqueName);

            try
            {
                this.SweepForage(location, settings, output);
                this.SweepAnimals(location, settings, output);
                this.SweepCrops(location, settings, output, chest);
                this.SweepLargeTerrainFeatures(location, settings, output);
                this.SweepFruitTrees(location, settings, output);
                this.SweepResourceClumps(location, settings, output);
                this.SweepDigSpots(location, settings, output);
                this.SweepTrees(location, settings, output);
                this.SweepTrashCans(location, settings, output);
                this.SweepMachines(location, settings, output);
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Failed harvesting in '{location.NameOrUniqueName}': {ex}", LogLevel.Error);
            }
        }

        return report;
    }

    /// <summary>Drop anything cached for one day, before the first pass of the next one.</summary>
    /// <remarks>Maps are swapped out by season and rewritten by content packs between days.</remarks>
    public void ClearDailyCaches()
    {
        this.TrashCansByLocation.Clear();
    }

    /*********
    ** Forage
    *********/
    /// <summary>Pick up spawned forage lying on the ground.</summary>
    private void SweepForage(GameLocation location, GrabberSettings settings, GrabberOutput output)
    {
        foreach ((Vector2 tile, Object obj) in location.objects.Pairs.ToArray())
        {
            if (output.IsFull)
                return;

            if (!obj.isSpawnedObject.Value || obj.questItem.Value)
                continue;

            // dig spots are spawned objects too, but they're handled by the digging pass
            if (obj.QualifiedItemId is "(O)590" or "(O)SeedSpot")
                continue;

            // so is an egg on a coop floor, and a truffle a pig dug up; both belong to the Animals group
            if (TargetCatalog.IsAnimalProduct(obj.QualifiedItemId))
                continue;

            if (!this.WantsForage(settings, obj.QualifiedItemId))
                continue;

            bool isForage = obj.isForage();
            if (isForage)
                obj.Quality = location.GetHarvestSpawnedObjectQuality(Game1.player, true, tile);

            Item one = obj.getOne();
            location.objects.Remove(tile);
            output.Deposit(one, location, tile);
            Game1.stats.ItemsForaged++;

            if (this.Config.GrantExperience && isForage && !location.isFarmBuildingInterior())
                location.OnHarvestedForage(Game1.player, obj);
        }
    }

    /// <summary>Get whether a grabber wants a piece of forage, including through the catch-all row.</summary>
    private bool WantsForage(GrabberSettings settings, string qualifiedItemId)
    {
        string id = TargetCatalog.ForageId(qualifiedItemId);
        if (settings.TargetIds.Contains(id))
            return true;

        return settings.TargetIds.Contains(TargetCatalog.OtherForageId) && TargetCatalog.Get(id) == null;
    }

    /*********
    ** Animals
    *********/
    /// <summary>Collect animal products, both what's waiting on the floor and what's still on the animal.</summary>
    /// <remarks>
    ///   Vanilla splits these three ways by <see cref="FarmAnimalHarvestType" />. Eggs and the rest of
    ///   the DropOvernight produce, and the truffles a pig digs up, are ordinary spawned objects lying on
    ///   the ground by the time a pass sees them; only milk and wool are still held by the animal. Slime
    ///   balls aren't animal produce at all, but collecting them is the same chore, so they share the pass.
    /// </remarks>
    private void SweepAnimals(GameLocation location, GrabberSettings settings, GrabberOutput output)
    {
        this.SweepProduceOnGround(location, settings, output);
        this.SweepSlimeBalls(location, settings, output);
        this.SweepProduceOnAnimals(location, settings, output);
    }

    /// <summary>Pick up animal produce lying on the ground, like eggs on a coop floor or a dug-up truffle.</summary>
    private void SweepProduceOnGround(GameLocation location, GrabberSettings settings, GrabberOutput output)
    {
        foreach ((Vector2 tile, Object obj) in location.objects.Pairs.ToArray())
        {
            if (output.IsFull)
                return;

            if (!obj.isSpawnedObject.Value || obj.questItem.Value)
                continue;

            if (!settings.TargetIds.Contains(TargetCatalog.AnimalId(obj.QualifiedItemId)))
                continue;

            // A truffle counts as forage to the game -- Object.isForage special-cases it by ID -- so it
            // gets the foraging quality roll and the experience that an egg on a coop floor doesn't.
            bool isForage = obj.isForage();
            if (isForage)
                obj.Quality = location.GetHarvestSpawnedObjectQuality(Game1.player, true, tile);

            Item one = obj.getOne();
            location.objects.Remove(tile);
            output.Deposit(one, location, tile);

            if (!isForage)
                continue;

            Game1.stats.ItemsForaged++;
            if (this.Config.GrantExperience && !location.isFarmBuildingInterior())
                location.OnHarvestedForage(Game1.player, obj);
        }
    }

    /// <summary>Pop the slime balls on a slime hutch floor.</summary>
    private void SweepSlimeBalls(GameLocation location, GrabberSettings settings, GrabberOutput output)
    {
        if (!settings.TargetIds.Contains(TargetCatalog.SlimeBallId))
            return;

        foreach ((Vector2 tile, Object obj) in location.objects.Pairs.ToArray())
        {
            if (output.IsFull)
                return;

            if (obj.QualifiedItemId != TargetCatalog.SlimeBallItemId)
                continue;

            // The seed matches Object.CheckForActionOnSlimeBall, so the grabber gets exactly the slime
            // that popping this ball by hand would have given today rather than its own roll.
            Random random = Utility.CreateRandom(Game1.stats.DaysPlayed, Game1.uniqueIDForThisGame, tile.X * 77.0, tile.Y * 777.0, 2.0);

            location.objects.Remove(tile);
            output.Deposit(ItemRegistry.Create("(O)766", random.Next(10, 21)), location, tile);
        }
    }

    /// <summary>Take the milk and wool still held by the animals here.</summary>
    /// <remarks>
    ///   This mirrors what a stock grabber already does for the animal house it stands in
    ///   (<c>Object.DayUpdate</c>, case <c>(BC)165</c>), which needs no milk pail or shears either. The
    ///   only thing added is reach: this runs wherever the grabber's scope says, so one grabber outside
    ///   can serve a barn, and it catches animals that have since wandered out to graze.
    /// </remarks>
    private void SweepProduceOnAnimals(GameLocation location, GrabberSettings settings, GrabberOutput output)
    {
        foreach (FarmAnimal animal in location.animals.Values.ToArray())
        {
            if (output.IsFull)
                return;

            // Only tool-harvested produce is taken off the animal. A pig carries its truffle in the same
            // field until it digs one up, and taking that directly would hand out a guaranteed truffle a
            // day regardless of season, weather, or whether the pig ever left the barn.
            if (animal.GetHarvestType() != FarmAnimalHarvestType.HarvestWithTool)
                continue;

            string? produceId = animal.currentProduce.Value;
            if (string.IsNullOrWhiteSpace(produceId) || !animal.isAdult())
                continue;

            string? qualified = ItemRegistry.QualifyItemId(produceId);
            if (qualified == null || !settings.TargetIds.Contains(TargetCatalog.AnimalId(qualified)))
                continue;

            Object produce = ItemRegistry.Create<Object>(qualified);
            produce.CanBeSetDown = false;
            produce.Quality = animal.produceQuality.Value;
            if (animal.hasEatenAnimalCracker.Value)
                produce.Stack = 2;

            // counted before depositing, because depositing may split the stack across the last slot
            animal.HandleStatsOnProduceCollected(produce, (uint)produce.Stack);
            animal.currentProduce.Value = null;

            // the sheep keeps its woolly sprite until this is called
            animal.ReloadTextureIfNeeded();

            output.Deposit(produce, location, animal.Tile);

            // No friendship. The tool grants +5 for the walk over; a grabber didn't make the walk, and
            // a stock grabber emptying its own barn grants none either.
            if (this.Config.GrantExperience)
                Game1.player.gainExperience(0, 5);
        }
    }

    /*********
    ** Crops
    *********/
    /// <summary>Harvest ready crops in the ground and in indoor pots.</summary>
    private void SweepCrops(GameLocation location, GrabberSettings settings, GrabberOutput output, Chest chest)
    {
        foreach ((Vector2 tile, TerrainFeature feature) in location.terrainFeatures.Pairs.ToArray())
        {
            if (feature is HoeDirt dirt)
                this.TryHarvestCrop(location, tile, dirt, settings, output, chest);
        }

        foreach ((Vector2 tile, Object obj) in location.objects.Pairs.ToArray())
        {
            if (obj is IndoorPot pot && pot.hoeDirt.Value != null)
                this.TryHarvestCrop(location, tile, pot.hoeDirt.Value, settings, output, chest);
        }
    }

    /// <summary>Harvest one tile of soil if its crop is ready and wanted.</summary>
    private void TryHarvestCrop(GameLocation location, Vector2 tile, HoeDirt dirt, GrabberSettings settings, GrabberOutput output, Chest chest)
    {
        if (output.IsFull || dirt.crop == null || !dirt.readyForHarvest())
            return;

        string? harvestId = HarvestEngine.GetCropHarvestId(dirt.crop);
        if (harvestId == null || !settings.TargetIds.Contains(TargetCatalog.CropId(harvestId)))
            return;

        string? seedId = dirt.crop.netSeedIndex.Value;
        this.Harvester.Retarget(output, location, tile);

        if (dirt.crop.harvest((int)tile.X, (int)tile.Y, dirt, this.Harvester))
        {
            dirt.destroyCrop(showAnimation: false);
            HarvestEngine.TryReplant(dirt, seedId, chest, settings.Replant);
        }

        if (this.Config.GrantExperience)
            this.GrantCropExperience(harvestId);
    }

    /// <summary>Get the qualified item ID a crop yields, or <c>null</c> if it can't be worked out.</summary>
    private static string? GetCropHarvestId(Crop crop)
    {
        // forage crops (spring onions) don't use indexOfHarvest
        if (crop.forageCrop.Value)
            return crop.whichForageCrop.Value == "1" ? "(O)399" : null;

        return string.IsNullOrWhiteSpace(crop.indexOfHarvest.Value)
            ? null
            : ItemRegistry.QualifyItemId(crop.indexOfHarvest.Value);
    }

    /// <summary>Replant the soil a crop was just harvested from, using a seed the grabber is holding.</summary>
    /// <param name="dirt">The soil the crop came out of.</param>
    /// <param name="seedId">The seed the harvested crop grew from.</param>
    /// <param name="chest">The grabber's contents, which is the only place a seed may come from.</param>
    /// <param name="mode">Which seeds this grabber is allowed to plant.</param>
    private static void TryReplant(HoeDirt dirt, string? seedId, Chest chest, ReplantMode mode)
    {
        if (mode == ReplantMode.Never || dirt.crop != null)
            return;

        foreach (Item seed in HarvestEngine.GetReplantSeeds(chest, seedId, mode))
        {
            // plant() is what decides whether a seed may go in: it checks the crop data, the soil's own
            // season and the location's planting rules, and leaves the dirt untouched when it refuses,
            // so a rejected seed just means trying the next one. canPlantThisSeedHere() looks like the
            // fitting check, but it tests Game1.currentLocation and the player's own footprint, neither
            // of which says anything about soil a remote grabber is reaching.
            if (!dirt.plant(seed.ItemId, Game1.player, isFertilizer: false))
                continue;

            seed.Stack--;
            if (seed.Stack <= 0)
                chest.Items.Remove(seed);
            return;
        }
    }

    /// <summary>Get the seeds in a grabber that may be replanted, in the order they should be tried.</summary>
    /// <remarks>
    ///   The harvested crop's own seed comes first even under <see cref="ReplantMode.AnySeed" />. That
    ///   mode is there so soil keeps being used once the right seed runs out, not so a parsnip field
    ///   quietly turns into whatever else was in the box.
    /// </remarks>
    private static IEnumerable<Item> GetReplantSeeds(Chest chest, string? seedId, ReplantMode mode)
    {
        string? qualifiedSeed = string.IsNullOrWhiteSpace(seedId)
            ? null
            : ItemRegistry.QualifyItemId(seedId);

        Item? matching = qualifiedSeed == null
            ? null
            : chest.Items.FirstOrDefault(item => item?.QualifiedItemId == qualifiedSeed);

        if (matching != null)
            yield return matching;

        if (mode != ReplantMode.AnySeed)
            yield break;

        // Category rather than a crop-data lookup, because that's what tells a seed apart from the
        // fertiliser and saplings sharing the box. Mixed Seeds pass, and resolve to something in season
        // when they're planted, which is the whole point of them.
        foreach (Item item in chest.Items.ToArray())
        {
            if (item != null && item != matching && item.Category == Object.SeedsCategory)
                yield return item;
        }
    }

    /// <summary>Grant the farming experience the player would have got for harvesting a crop by hand.</summary>
    /// <remarks>The game only awards this on the player's own harvest path, so it's recomputed here using its formula.</remarks>
    private void GrantCropExperience(string qualifiedItemId)
    {
        int price = (ItemRegistry.Create(qualifiedItemId) as Object)?.Price ?? 0;
        int experience = (int)Math.Round(16.0 * Math.Log(0.018 * Math.Max(0, price) + 1.0, Math.E));
        if (experience > 0)
            Game1.player.gainExperience(0, experience);
    }

    /*********
    ** Bushes
    *********/
    /// <summary>Shake berry and tea bushes that are ready.</summary>
    private void SweepLargeTerrainFeatures(GameLocation location, GrabberSettings settings, GrabberOutput output)
    {
        foreach (LargeTerrainFeature feature in location.largeTerrainFeatures.ToArray())
        {
            if (output.IsFull)
                return;

            if (feature is not Bush bush || bush.townBush.Value || !bush.readyForHarvest() || !bush.inBloom())
                continue;

            string? shakeOff = bush.GetShakeOffItem();
            if (shakeOff == null)
                continue;

            string? qualified = ItemRegistry.QualifyItemId(shakeOff);
            if (qualified == null || !settings.TargetIds.Contains(TargetCatalog.BushId(qualified)))
                continue;

            bush.tileSheetOffset.Value = 0;
            bush.setUpSourceRect();

            if (bush.size.Value == Bush.greenTeaBush)
            {
                output.Deposit(ItemRegistry.Create(qualified), location, bush.Tile);
                continue;
            }

            // berry bushes give more the higher your foraging level, and perfect quality with Botanist
            int count = 1 + Game1.player.ForagingLevel / 4;
            for (int i = 0; i < count; i++)
            {
                Item berry = ItemRegistry.Create(qualified);
                if (Game1.player.professions.Contains(16))
                    berry.Quality = 4;
                output.Deposit(berry, location, bush.Tile);
            }

            if (this.Config.GrantExperience)
                Game1.player.gainExperience(2, count);
        }
    }

    /*********
    ** Fruit trees
    *********/
    /// <summary>Take the fruit waiting on fruit trees.</summary>
    private void SweepFruitTrees(GameLocation location, GrabberSettings settings, GrabberOutput output)
    {
        foreach ((Vector2 tile, TerrainFeature feature) in location.terrainFeatures.Pairs.ToArray())
        {
            if (feature is not FruitTree tree || tree.fruit.Count == 0)
                continue;

            for (int i = tree.fruit.Count - 1; i >= 0; i--)
            {
                if (output.IsFull)
                    return;

                Item fruit = tree.fruit[i];
                if (fruit == null || !settings.TargetIds.Contains(TargetCatalog.FruitId(fruit.QualifiedItemId)))
                    continue;

                tree.fruit.RemoveAt(i);
                output.Deposit(fruit, location, tile);
            }
        }
    }

    /*********
    ** Resource clumps
    *********/
    /// <summary>Break large stumps, logs, boulders and giant crops.</summary>
    private void SweepResourceClumps(GameLocation location, GrabberSettings settings, GrabberOutput output)
    {
        foreach (ResourceClump clump in location.resourceClumps.ToArray())
        {
            if (output.IsFull)
                return;

            // giant crops are left alone: plenty of players grow them as decoration, and a grabber
            // that quietly chopped one down would be destroying something deliberate
            if (clump is GiantCrop)
                continue;

            string targetId = TargetCatalog.ClumpId(HarvestEngine.NormalizeClumpIndex(clump.parentSheetIndex.Value));
            if (!settings.TargetIds.Contains(targetId))
                continue;

            if (!this.HasToolFor(targetId))
            {
                output.Report.Skip($"{TargetCatalog.Get(targetId)?.DisplayName ?? targetId}: {this.DescribeToolRequirement(targetId)}");
                continue;
            }

            Vector2 tile = clump.Tile;
            location.resourceClumps.Remove(clump);

            foreach (Item drop in this.GetClumpDrops(clump.parentSheetIndex.Value))
                output.Deposit(drop, location, tile);
        }
    }

    /// <summary>Map the several mine-rock variants onto the single row they're listed under.</summary>
    private static int NormalizeClumpIndex(int parentSheetIndex)
    {
        return parentSheetIndex switch
        {
            ResourceClump.mineRock2Index or ResourceClump.mineRock3Index or ResourceClump.mineRock4Index or ResourceClump.quarryBoulderIndex => ResourceClump.mineRock1Index,
            _ => parentSheetIndex
        };
    }

    /// <summary>Get what a clump drops when broken.</summary>
    /// <remarks>
    ///   These mirror <see cref="ResourceClump.destroy" /> rather than calling it: that method sends its
    ///   drops to <see cref="Game1.currentLocation" /> instead of the clump's own location, which would
    ///   scatter loot around the player whenever a grabber worked somewhere else.
    /// </remarks>
    private IEnumerable<Item> GetClumpDrops(int parentSheetIndex)
    {
        bool lumberjack = Game1.player.professions.Contains(12);

        switch (parentSheetIndex)
        {
            case ResourceClump.stumpIndex:
            case ResourceClump.hollowLogIndex:
            {
                bool isLog = parentSheetIndex == ResourceClump.hollowLogIndex;
                int hardwood = isLog ? 8 : 2;
                if (lumberjack)
                    hardwood = isLog ? 10 : hardwood + (Game1.random.NextBool() ? 1 : 0);

                yield return ItemRegistry.Create("(O)709", hardwood);

                if (Game1.random.NextDouble() < 0.1)
                    yield return ItemRegistry.Create("(O)292");

                Game1.stats.StumpsChopped++;
                if (this.Config.GrantExperience)
                    Game1.player.gainExperience(2, 25);
                break;
            }

            case ResourceClump.boulderIndex:
                yield return ItemRegistry.Create("(O)390", 15);
                break;

            case ResourceClump.meteoriteIndex:
                yield return ItemRegistry.Create("(O)386", 10);
                yield return ItemRegistry.Create("(O)390", 8);
                yield return ItemRegistry.Create("(O)749", 2);
                if (Game1.random.NextDouble() < 0.25)
                    yield return ItemRegistry.Create("(O)74");
                break;

            default:
                yield return ItemRegistry.Create("(O)390", 10);
                break;
        }
    }

    /*********
    ** Dig spots
    *********/
    /// <summary>Dig up artifact and seed spots.</summary>
    private void SweepDigSpots(GameLocation location, GrabberSettings settings, GrabberOutput output)
    {
        foreach ((Vector2 tile, Object obj) in location.objects.Pairs.ToArray())
        {
            if (output.IsFull)
                return;

            bool isSeedSpot = obj.QualifiedItemId == "(O)SeedSpot";
            if (!isSeedSpot && obj.QualifiedItemId != "(O)590")
                continue;

            string targetId = isSeedSpot ? TargetCatalog.SeedSpotId : TargetCatalog.ArtifactSpotId;
            if (!settings.TargetIds.Contains(targetId))
                continue;

            if (!this.HasToolFor(targetId))
            {
                output.Report.Skip($"{TargetCatalog.Get(targetId)?.DisplayName ?? targetId}: {this.DescribeToolRequirement(targetId)}");
                continue;
            }

            location.objects.Remove(tile);
            Game1.player.stats.Increment("ArtifactSpotsDug", 1);

            if (isSeedSpot)
            {
                Random random = Utility.CreateDaySaveRandom(-tile.X * 7f, tile.Y * 777f, Game1.netWorldState.Value.TreasureTotemsUsed * 777);
                output.Deposit(Utility.getRaccoonSeedForCurrentTimeOfYear(Game1.player, random), location, tile);
            }
            else
            {
                this.CaptureDebris(location, output, tile, () => location.digUpArtifactSpot((int)tile.X, (int)tile.Y, Game1.player));
            }

            location.makeHoeDirt(tile, ignoreChecks: true);

            if (this.Config.GrantExperience)
                Game1.player.gainExperience(2, 15);
        }
    }

    /*********
    ** Trees
    *********/
    /// <summary>Shake wild trees for whatever they're holding.</summary>
    /// <remarks>
    ///   This calls the game's own shake, so the yield is whatever shaking would normally give: seeds,
    ///   the occasional mystery box, coconuts, anything a content pack adds. The game guards repeat
    ///   shakes itself, so running every hour costs nothing after the first one each day.
    /// </remarks>
    private void SweepTrees(GameLocation location, GrabberSettings settings, GrabberOutput output)
    {
        if (!settings.TargetIds.Contains(TargetCatalog.ShakeTreesId))
            return;

        foreach ((Vector2 tile, TerrainFeature feature) in location.terrainFeatures.Pairs.ToArray())
        {
            if (output.IsFull)
                return;

            if (feature is not Tree tree || tree.tapped.Value || tree.stump.Value || tree.growthStage.Value < 5)
                continue;

            // nothing to shake loose once the seed is gone and today's shake has happened
            if (!tree.hasSeed.Value && tree.wasShakenToday.Value)
                continue;

            this.CaptureDebris(location, output, tile, () => tree.shake(tile, doEvenIfStillShaking: false));
        }
    }

    /*********
    ** Trash cans
    *********/
    /// <summary>Rummage in the location's trash cans for whatever the day's roll gives.</summary>
    /// <remarks>
    ///   This doesn't call <c>GameLocation.CheckGarbage</c>. Even with its animations and NPC reactions
    ///   turned off, its last step hands the item to <c>Farmer.addItemByMenuIfNecessary</c> whenever the
    ///   data entry sets <c>AddToInventoryDirectly</c> -- which would drop it in the host's pockets, or
    ///   pop an item grab menu mid-pass, instead of putting it in the grabber. So the pass does what
    ///   CheckGarbage does around that step: mark the can checked, roll it, count it.
    ///
    ///   No NPC reactions either. Vanilla costs the player 25 friendship with whoever is standing nearby
    ///   and announces it in chat, on the same reasoning as the milk pail's +5: it's payment for walking
    ///   over and being seen doing it, and a grabber did neither.
    /// </remarks>
    private void SweepTrashCans(GameLocation location, GrabberSettings settings, GrabberOutput output)
    {
        if (!settings.TargetIds.Contains(TargetCatalog.TrashCanId))
            return;

        ISet<string> checkedToday = Game1.netWorldState.Value.CheckedGarbage;
        foreach ((Vector2 tile, string id) in this.GetTrashCans(location))
        {
            // Bail before rolling rather than after. TryGetGarbageItem doesn't touch the world, so a
            // grabber with no room leaves the can exactly as it found it and the player can still check it.
            if (output.IsFull)
                return;

            if (checkedToday.Contains(id))
                continue;

            location.TryGetGarbageItem(
                id,
                Game1.player.DailyLuck,
                out Item? item,
                out _,
                out _,
                error => this.Monitor.Log($"Ignored invalid garbage can '{id}' in '{location.NameOrUniqueName}': {error}.", LogLevel.Warn)
            );

            // The can is used up either way, exactly as vanilla does it. The roll is seeded by the day
            // and the can's ID, so a can that came up empty here would have come up empty for the player.
            checkedToday.Add(id);
            Game1.stats.Increment("trashCansChecked");

            if (item != null)
                output.Deposit(item, location, tile);
        }
    }

    /// <summary>Find the garbage cans on a location's map, caching the answer for the rest of the day.</summary>
    /// <remarks>
    ///   A trash can isn't an object or a terrain feature -- it's an <c>Action Garbage &lt;id&gt;</c> tile
    ///   property on the Buildings layer, so the only way to find one is to sweep the layer.
    ///
    ///   Which of the two map accessors this uses matters. <see cref="GameLocation.Map" /> calls
    ///   <c>updateMap</c>, loading the map if it isn't in memory yet; the <c>map</c> field doesn't. Somewhere
    ///   the player has already been is loaded during an ordinary day anyway, so forcing it there costs
    ///   nothing and is the only way a grabber can find cans before the player next walks past them.
    ///   Somewhere they haven't been is left alone rather than paged in for a grabber's sake.
    /// </remarks>
    private List<(Vector2 Tile, string Id)> GetTrashCans(GameLocation location)
    {
        if (this.TrashCansByLocation.TryGetValue(location.NameOrUniqueName, out List<(Vector2, string)>? cached))
            return cached;

        Layer? layer = (GrabberSettings.HasVisited(location) ? location.Map : location.map)?.GetLayer("Buildings");
        if (layer == null)
            return new List<(Vector2, string)>();  // not cached: the map may well be loaded by the next pass

        List<(Vector2 Tile, string Id)> cans = new();
        for (int x = 0; x < layer.LayerWidth; x++)
        {
            for (int y = 0; y < layer.LayerHeight; y++)
            {
                Tile? tile = layer.Tiles[x, y];
                if (tile == null)
                    continue;

                // tile properties win over tilesheet ones, the same order the game reads them in
                if (!tile.Properties.TryGetValue("Action", out string action) && !tile.TileIndexProperties.TryGetValue("Action", out action))
                    continue;

                string[] parts = ArgUtility.SplitBySpace(action);
                if (ArgUtility.Get(parts, 0) != "Garbage" || !ArgUtility.TryGet(parts, 1, out string id, out _, allowBlank: false))
                    continue;

                cans.Add((new Vector2(x, y), HarvestEngine.NormalizeTrashCanId(id)));
            }
        }

        this.TrashCansByLocation[location.NameOrUniqueName] = cans;
        return cans;
    }

    /// <summary>Get the ID a garbage can is tracked under in <c>NetWorldState.CheckedGarbage</c>.</summary>
    /// <remarks>
    ///   Maps written before 1.6 number their cans, and <c>CheckGarbage</c> renames those before touching
    ///   the checked set. Skipping this would file a can under "0" while the player's own check files the
    ///   same can under "JodiAndKent", so both would pay out.
    /// </remarks>
    private static string NormalizeTrashCanId(string id)
    {
        return id switch
        {
            "0" => "JodiAndKent",
            "1" => "EmilyAndHaley",
            "2" => "Mayor",
            "3" => "Museum",
            "4" => "Blacksmith",
            "5" => "Saloon",
            "6" => "Evelyn",
            "7" => "JojaMart",
            _ => id
        };
    }

    /*********
    ** Machines
    *********/
    /// <summary>Empty machines that have finished.</summary>
    private void SweepMachines(GameLocation location, GrabberSettings settings, GrabberOutput output)
    {
        foreach ((Vector2 tile, Object machine) in location.objects.Pairs.ToArray())
        {
            if (output.IsFull)
                return;

            if (machine is Chest || !settings.TargetIds.Contains(TargetCatalog.MachineId(machine.QualifiedItemId)))
                continue;

            if (!machine.readyForHarvest.Value || machine.heldObject.Value == null)
            {
                output.Report.Skip($"{machine.DisplayName}: nothing ready to collect");
                continue;
            }

            if (machine is CrabPot pot)
                this.HarvestCrabPot(location, tile, pot, output);
            else
                this.HarvestMachine(location, tile, machine, output);
        }
    }

    /// <summary>Take a machine's output, mirroring what collecting it by hand does.</summary>
    private void HarvestMachine(GameLocation location, Vector2 tile, Object machine, GrabberOutput output)
    {
        Farmer who = Game1.player;
        MachineData? data = machine.GetMachineData();
        Object collected = machine.heldObject.Value;

        // some machines decide their output at the moment you collect it
        if (machine.lastOutputRuleId.Value != null && data?.OutputRules != null)
        {
            MachineOutputRule? rule = data.OutputRules.FirstOrDefault(candidate => candidate.Id == machine.lastOutputRuleId.Value);
            if (rule?.RecalculateOnCollect == true)
            {
                machine.heldObject.Value = null;
                machine.OutputMachine(data, rule, machine.lastInputItem.Value, who, location, probe: false, heldObjectOnly: true);
                collected = machine.heldObject.Value ?? collected;
            }
        }

        machine.heldObject.Value = null;
        machine.readyForHarvest.Value = false;
        machine.showNextIndex.Value = false;
        machine.ResetParentSheetIndex();

        MachineDataUtility.UpdateStats(data?.StatsToIncrementWhenHarvested, collected, collected.Stack);
        output.Deposit(collected, location, tile);

        // machines like the crystalarium start their next batch the moment the last one is taken
        if (MachineDataUtility.TryGetMachineOutputRule(machine, data, MachineOutputTrigger.OutputCollected, collected.getOne(), who, location, out MachineOutputRule outputRule, out _, out _, out _))
            machine.OutputMachine(data, outputRule, machine.lastInputItem.Value, who, location, probe: false);

        if (machine.IsTapper() && location.terrainFeatures.TryGetValue(tile, out TerrainFeature? feature) && feature is Tree tree)
            tree.UpdateTapperProduct(machine, collected);

        if (this.Config.GrantExperience && data?.ExperienceGainOnHarvest != null)
            HarvestEngine.GrantMachineExperience(data.ExperienceGainOnHarvest);
    }

    /// <summary>Take a crab pot's catch and clear its bait, the way emptying one by hand does.</summary>
    private void HarvestCrabPot(GameLocation location, Vector2 tile, CrabPot pot, GrabberOutput output)
    {
        Object catchItem = pot.heldObject.Value;

        pot.heldObject.Value = null;
        pot.readyForHarvest.Value = false;
        pot.tileIndexToShow = 710;
        pot.bait.Value = null;

        output.Deposit(catchItem, location, tile);

        if (this.Config.GrantExperience)
            Game1.player.gainExperience(1, 5);
    }

    /// <summary>Grant the skill experience a machine gives when its output is collected.</summary>
    private static void GrantMachineExperience(string experienceGain)
    {
        string[] parts = experienceGain.Split(' ');
        for (int i = 0; i < parts.Length; i += 2)
        {
            int skill = Farmer.getSkillNumberFromName(parts[i]);
            if (skill != -1 && ArgUtility.TryGetInt(parts, i + 1, out int amount, out _, "int amount"))
                Game1.player.gainExperience(skill, amount);
        }
    }

    /*********
    ** Shared
    *********/
    /// <summary>Run an action that drops items into the world, and collect whatever it dropped.</summary>
    private void CaptureDebris(GameLocation location, GrabberOutput output, Vector2 tile, Action action)
    {
        HashSet<Debris> before = new(location.debris);
        action();

        foreach (Debris debris in location.debris.ToArray())
        {
            if (before.Contains(debris))
                continue;

            Item? item = debris.item;
            if (item == null && !string.IsNullOrWhiteSpace(debris.itemId.Value))
                item = ItemRegistry.Create(debris.itemId.Value, debris.Chunks.Count, debris.itemQuality);

            location.debris.Remove(debris);
            output.Deposit(item, location, tile);
        }
    }

    /// <summary>Get whether the player owns the tool vanilla would require for a target.</summary>
    private bool HasToolFor(string targetId)
    {
        if (!this.Config.RespectToolRequirements)
            return true;

        return targetId switch
        {
            TargetCatalog.ArtifactSpotId or TargetCatalog.SeedSpotId => HarvestEngine.ToolLevel("Hoe") >= 0,
            _ when targetId == TargetCatalog.ClumpId(ResourceClump.stumpIndex) => HarvestEngine.ToolLevel("Axe") >= 1,
            _ when targetId == TargetCatalog.ClumpId(ResourceClump.hollowLogIndex) => HarvestEngine.ToolLevel("Axe") >= 2,
            _ when targetId == TargetCatalog.ClumpId(ResourceClump.boulderIndex) => HarvestEngine.ToolLevel("Pickaxe") >= 2,
            _ when targetId == TargetCatalog.ClumpId(ResourceClump.meteoriteIndex) => HarvestEngine.ToolLevel("Pickaxe") >= 3,
            _ when targetId == TargetCatalog.ClumpId(ResourceClump.mineRock1Index) => HarvestEngine.ToolLevel("Pickaxe") >= 0,
            _ => true
        };
    }

    /// <summary>Describe why a tool requirement wasn't met, for the log.</summary>
    private string DescribeToolRequirement(string targetId)
    {
        if (targetId == TargetCatalog.ArtifactSpotId || targetId == TargetCatalog.SeedSpotId)
            return "needs a hoe in your inventory";

        if (targetId == TargetCatalog.ClumpId(ResourceClump.stumpIndex))
            return "needs a copper axe or better in your inventory";

        if (targetId == TargetCatalog.ClumpId(ResourceClump.hollowLogIndex))
            return "needs a steel axe or better in your inventory";

        if (targetId == TargetCatalog.ClumpId(ResourceClump.boulderIndex))
            return "needs a steel pickaxe or better in your inventory";

        if (targetId == TargetCatalog.ClumpId(ResourceClump.meteoriteIndex))
            return "needs a gold pickaxe or better in your inventory";

        return "needs a better tool in your inventory";
    }

    /// <summary>Get the upgrade level of a tool the player is carrying, or -1 if they aren't carrying one.</summary>
    private static int ToolLevel(string name)
    {
        Tool? tool = Game1.player.getToolFromName(name);
        return tool?.UpgradeLevel ?? -1;
    }
}
