using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.GameData.Machines;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using Object = StardewValley.Object;

namespace BetterAutoGrabber.Framework;

/// <summary>Harvests the targets a grabber has been told to collect.</summary>
internal sealed class HarvestEngine
{
    private readonly ModConfig Config;
    private readonly IMonitor Monitor;
    private readonly GrabberHarvester Harvester = new();

    public HarvestEngine(ModConfig config, IMonitor monitor)
    {
        this.Config = config;
        this.Monitor = monitor;
    }

    /// <summary>Run one harvest pass for a grabber.</summary>
    /// <param name="grabber">The placed grabber.</param>
    /// <param name="settings">The grabber's settings.</param>
    /// <param name="locations">The locations it reaches this pass.</param>
    /// <returns>The number of items collected.</returns>
    public int Run(Object grabber, GrabberSettings settings, IEnumerable<GameLocation> locations)
    {
        if (grabber.heldObject.Value is not Chest chest || !settings.HasExtraTargets)
            return 0;

        GrabberOutput output = new(chest);
        foreach (GameLocation location in locations)
        {
            if (output.IsFull)
                break;

            try
            {
                this.SweepForage(location, settings, output);
                this.SweepCrops(location, settings, output, chest);
                this.SweepLargeTerrainFeatures(location, settings, output);
                this.SweepFruitTrees(location, settings, output);
                this.SweepResourceClumps(location, settings, output);
                this.SweepDigSpots(location, settings, output);
                this.SweepTrees(location, settings, output);
                this.SweepMachines(location, settings, output);
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Failed harvesting in '{location.NameOrUniqueName}': {ex}", LogLevel.Error);
            }
        }

        return output.Collected;
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
            if (this.Config.ReplantCrops)
                HarvestEngine.TryReplant(dirt, seedId, chest);
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

    /// <summary>Replant the seed for a harvested crop, if the grabber is holding one.</summary>
    private static void TryReplant(HoeDirt dirt, string? seedId, Chest chest)
    {
        if (string.IsNullOrWhiteSpace(seedId) || dirt.crop != null)
            return;

        string? qualifiedSeed = ItemRegistry.QualifyItemId(seedId);
        if (qualifiedSeed == null)
            return;

        Item? seed = chest.Items.FirstOrDefault(item => item?.QualifiedItemId == qualifiedSeed);
        if (seed == null)
            return;

        if (!dirt.plant(seed.ItemId, Game1.player, isFertilizer: false))
            return;

        seed.Stack--;
        if (seed.Stack <= 0)
            chest.Items.Remove(seed);
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
            if (!settings.TargetIds.Contains(targetId) || !this.HasToolFor(targetId))
                continue;

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
            if (!settings.TargetIds.Contains(targetId) || !this.HasToolFor(targetId))
                continue;

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
    ** Machines
    *********/
    /// <summary>Empty machines that have finished.</summary>
    private void SweepMachines(GameLocation location, GrabberSettings settings, GrabberOutput output)
    {
        foreach ((Vector2 tile, Object machine) in location.objects.Pairs.ToArray())
        {
            if (output.IsFull)
                return;

            if (!machine.readyForHarvest.Value || machine.heldObject.Value == null || machine is Chest)
                continue;

            if (!settings.TargetIds.Contains(TargetCatalog.MachineId(machine.QualifiedItemId)))
                continue;

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

    /// <summary>Get the upgrade level of a tool the player is carrying, or -1 if they aren't carrying one.</summary>
    private static int ToolLevel(string name)
    {
        Tool? tool = Game1.player.getToolFromName(name);
        return tool?.UpgradeLevel ?? -1;
    }
}
