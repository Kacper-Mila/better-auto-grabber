using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
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
    Hoe,

    /// <summary>A pan, needed for the glittering spots in water.</summary>
    Pan
}

/// <summary>Tracks the best tool of each kind the player owns, wherever the tool happens to be.</summary>
/// <remarks>
/// A grabber isn't a farmer, so gating it on what's in the backpack right now meant hauling a tool
/// around purely to keep a grabber working. What actually matters is whether the upgrade was bought,
/// so the whole world is swept once a day for the best of each kind. That covers the tool left in a
/// chest, and the one sitting at Clint's forge mid-upgrade, which is in no inventory at all for two days.
/// Nothing in the game records the level a tool was ever upgraded to, so the sweep's result is
/// latched into save data: once an upgrade has been seen it stays seen, even if the tool is later
/// trashed, and an offline farmhand's tools are still remembered from when they were online. The
/// sweep also keeps the tool it found, because panning reads the pan's enchantments and not just its
/// level.
/// </remarks>
internal sealed class ToolOwnership
{
    /*********
    ** Fields
    *********/
    /// <summary>The save data key the latched levels are stored under.</summary>
    private const string SaveKey = "tool-ownership";

    private readonly IDataHelper Data;
    private readonly IMonitor Monitor;

    /// <summary>The best upgrade level seen so far for each kind of tool.</summary>
    private Dictionary<ToolKind, int> LatchedLevels = new();

    /// <summary>The best tool of each kind the last sweep actually found, for the passes that need the
    /// tool itself rather than its level. Not saved: an instance is only meaningful while it exists.</summary>
    private Dictionary<ToolKind, Tool> SweptTools = new();

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
        this.SweptTools = new(); // tools from whichever save was open before this one
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
        if (!Context.IsMainPlayer)
            return;

        Dictionary<ToolKind, Tool> found = new();
        Utility.ForEachItem(item =>
        {
            ToolOwnership.Note(found, item);
            return true;
        });

        this.SweptTools = found;
        this.Latch(found);
    }

    /// <summary>Get the best upgrade level the player owns for a kind of tool, or -1 if they've never had one.</summary>
    /// <param name="kind">The kind of tool to look up.</param>
    public int GetLevel(ToolKind kind)
    {
        int latched = this.LatchedLevels.TryGetValue(kind, out int saved) ? saved : -1;
        Tool? tool = this.GetTool(kind);

        return tool != null && tool.UpgradeLevel > latched
            ? tool.UpgradeLevel
            : latched;
    }

    /// <summary>Get the best tool of a kind the player owns, or <c>null</c> if none can be laid hands on.</summary>
    /// <param name="kind">The kind of tool to look up.</param>
    /// <remarks>The tool itself, not just its level, because its enchantments change what panning yields.
    /// This can come back null even when <see cref="GetLevel"/> reports a level: the level is
    /// remembered across days, the instance only lives as long as the tool does.</remarks>
    public Tool? GetTool(ToolKind kind)
    {
        // The world sweep is the expensive half and only runs once a day, so a tool acquired since
        // this morning -- collected from Clint, or bought back from the lost-and-found -- is caught
        // by scanning the players themselves, which is cheap enough to do on every query.
        Dictionary<ToolKind, Tool> carried = ToolOwnership.ScanFarmers();

        carried.TryGetValue(kind, out Tool? live);
        this.SweptTools.TryGetValue(kind, out Tool? swept);

        if (live == null)
            return swept;

        return swept == null || live.UpgradeLevel >= swept.UpgradeLevel
            ? live
            : swept;
    }

    /*********
    ** Private methods
    *********/
    /// <summary>Merge freshly seen levels into the latched ones, and save them if any improved.</summary>
    /// <param name="found">The best level seen for each kind by the caller.</param>
    private void Latch(Dictionary<ToolKind, Tool> found)
    {
        bool changed = false;
        foreach ((ToolKind kind, Tool tool) in found)
        {
            if (this.LatchedLevels.TryGetValue(kind, out int known) && known >= tool.UpgradeLevel)
                continue;

            this.LatchedLevels[kind] = tool.UpgradeLevel;
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
    private static Dictionary<ToolKind, Tool> ScanFarmers()
    {
        Dictionary<ToolKind, Tool> found = new();
        foreach (Farmer farmer in Game1.getAllFarmers())
        {
            foreach (Item? item in farmer.Items)
                ToolOwnership.Note(found, item);

            ToolOwnership.Note(found, farmer.toolBeingUpgraded.Value);
            ToolOwnership.Note(found, farmer.hat.Value); // a pan is worn as a hat, not carried as a tool
        }

        return found;
    }

    /// <summary>Record an item as the best of its kind, if it's a tracked tool and beats what's been seen.</summary>
    /// <param name="found">The best tool of each kind seen so far.</param>
    /// <param name="item">The item to look at, which may be null or anything else entirely.</param>
    private static void Note(Dictionary<ToolKind, Tool> found, Item? item)
    {
        // A pan is worn on the head, so a player who owns one usually has a hat rather than a tool.
        // The game's own conversion turns it back, enchantments and all, and returns anything that
        // isn't a pan hat untouched.
        if (item is Hat)
            item = Utility.PerformSpecialItemGrabReplacement(item);

        ToolKind? kind = item switch
        {
            Axe => ToolKind.Axe,
            Pickaxe => ToolKind.Pickaxe,
            Hoe => ToolKind.Hoe,
            Pan => ToolKind.Pan,
            _ => null
        };

        if (kind == null)
            return;

        Tool tool = (Tool)item!;
        if (!found.TryGetValue(kind.Value, out Tool? best) || tool.UpgradeLevel > best.UpgradeLevel)
            found[kind.Value] = tool;
    }
}
