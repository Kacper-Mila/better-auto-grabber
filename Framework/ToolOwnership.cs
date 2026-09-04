using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Tools;

namespace BetterAutoGrabber.Framework;

/// <summary>A kind of tool whose upgrade level gates part of the harvest.</summary>
internal enum ToolKind
{
    /// <summary>An axe, needed for large stumps and hollow logs.</summary>
    Axe,

    /// <summary>A pickaxe, needed for boulders, meteorites and mine rocks.</summary>
    Pickaxe,

    /// <summary>A hoe, needed for artifact and seed spots.</summary>
    Hoe
}

/// <summary>Tracks the best tool of each kind the player owns, wherever the tool happens to be.</summary>
/// <remarks>
/// A grabber isn't a farmer, so gating it on what's in the backpack right now meant hauling a tool
/// around purely to keep a grabber working. What actually matters is whether the upgrade was bought,
/// so the whole world is swept once a day for the best of each kind. That covers the tool left in a
/// chest, and the one sitting at Clint's forge mid-upgrade, which is in no inventory at all for two days.
/// Nothing in the game records the level a tool was ever upgraded to, so the sweep's result is
/// latched into save data: once an upgrade has been seen it stays seen, even if the tool is later
/// trashed, and an offline farmhand's tools are still remembered from when they were online.
/// </remarks>
internal sealed class ToolOwnership
{
    /*********
    ** Fields
    *********/
    /// <summary>The save data key the latched levels are stored under.</summary>
    private const string SaveKey = "tool-ownership";

    /// <summary>The upgrade level of an iridium tool, past which a sweep can't learn anything new.</summary>
    private const int MaxUpgradeLevel = 4;

    private readonly IDataHelper Data;
    private readonly IMonitor Monitor;

    /// <summary>The best upgrade level seen so far for each kind of tool.</summary>
    private Dictionary<ToolKind, int> LatchedLevels = new();

    /*********
    ** Public methods
    *********/
    public ToolOwnership(IDataHelper data, IMonitor monitor)
    {
        this.Data = data;
        this.Monitor = monitor;
    }

    /// <summary>Read the levels latched by earlier days of this save.</summary>
    public void Load()
    {
        this.LatchedLevels = new();
        if (!Context.IsMainPlayer)
            return; // save data belongs to the host, who is the only one harvesting anyway

        try
        {
            this.LatchedLevels = this.Data.ReadSaveData<Dictionary<ToolKind, int>>(ToolOwnership.SaveKey) ?? new();
        }
        catch (Exception ex)
        {
            this.Monitor.Log($"Couldn't read the remembered tool levels, so today's sweep starts from scratch: {ex}", LogLevel.Warn);
        }
    }

    /// <summary>Sweep the world for tools and latch anything better than what's already remembered.</summary>
    public void Refresh()
    {
        if (!Context.IsMainPlayer || this.IsFullyUpgraded())
            return;

        Dictionary<ToolKind, int> found = new();
        Utility.ForEachItem(item =>
        {
            ToolOwnership.Note(found, item);
            return true;
        });

        this.Latch(found);
    }

    /// <summary>Get the best upgrade level the player owns for a kind of tool, or -1 if they've never had one.</summary>
    /// <param name="kind">The kind of tool to look up.</param>
    public int GetLevel(ToolKind kind)
    {
        // The world sweep is the expensive half and only runs once a day, so a tool acquired since
        // this morning -- collected from Clint, or bought back from the lost-and-found -- is caught
        // by scanning the players themselves, which is cheap enough to do on every query.
        Dictionary<ToolKind, int> carried = ToolOwnership.ScanFarmers();

        int latched = this.LatchedLevels.TryGetValue(kind, out int saved) ? saved : -1;
        return carried.TryGetValue(kind, out int live) && live > latched
            ? live
            : latched;
    }

    /*********
    ** Private methods
    *********/
    /// <summary>Get whether every kind is already at iridium, so another sweep couldn't change anything.</summary>
    private bool IsFullyUpgraded()
    {
        return Enum.GetValues<ToolKind>()
            .All(kind => this.LatchedLevels.TryGetValue(kind, out int level) && level >= ToolOwnership.MaxUpgradeLevel);
    }

    /// <summary>Merge freshly seen levels into the latched ones, and save them if any improved.</summary>
    /// <param name="found">The best level seen for each kind by the caller.</param>
    private void Latch(Dictionary<ToolKind, int> found)
    {
        bool changed = false;
        foreach ((ToolKind kind, int level) in found)
        {
            if (this.LatchedLevels.TryGetValue(kind, out int known) && known >= level)
                continue;

            this.LatchedLevels[kind] = level;
            changed = true;
        }

        if (!changed)
            return;

        try
        {
            this.Data.WriteSaveData(ToolOwnership.SaveKey, this.LatchedLevels);
        }
        catch (Exception ex)
        {
            this.Monitor.Log($"Couldn't save the remembered tool levels; tomorrow's sweep will find them again: {ex}", LogLevel.Warn);
        }
    }

    /// <summary>Get the best level of each kind held by a player right now, including one being upgraded.</summary>
    /// <remarks>A tool at Clint's forge belongs to no inventory for two days: buying the upgrade takes
    /// the old tool away and parks the new one in <see cref="Farmer.toolBeingUpgraded"/>.</remarks>
    private static Dictionary<ToolKind, int> ScanFarmers()
    {
        Dictionary<ToolKind, int> found = new();
        foreach (Farmer farmer in Game1.getAllFarmers())
        {
            foreach (Item? item in farmer.Items)
                ToolOwnership.Note(found, item);

            ToolOwnership.Note(found, farmer.toolBeingUpgraded.Value);
        }

        return found;
    }

    /// <summary>Record an item's upgrade level, if it's a tracked tool and better than what's been seen.</summary>
    /// <param name="found">The levels seen so far.</param>
    /// <param name="item">The item to look at, which may be null or anything else entirely.</param>
    private static void Note(Dictionary<ToolKind, int> found, Item? item)
    {
        ToolKind? kind = item switch
        {
            Axe => ToolKind.Axe,
            Pickaxe => ToolKind.Pickaxe,
            Hoe => ToolKind.Hoe,
            _ => null
        };

        if (kind == null)
            return;

        int level = ((Tool)item!).UpgradeLevel;
        if (!found.TryGetValue(kind.Value, out int best) || level > best)
            found[kind.Value] = level;
    }
}
