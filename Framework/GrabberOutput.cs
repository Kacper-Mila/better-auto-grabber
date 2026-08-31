using System.Linq;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Objects;

namespace BetterAutoGrabber.Framework;

/// <summary>Collects harvested items into one grabber's chest.</summary>
/// <remarks>
///   The chest stays a plain <see cref="Chest" /> so that mods like Automate can keep pulling items out
///   of the grabber the way they always have.
/// </remarks>
internal sealed class GrabberOutput
{
    private readonly Chest Chest;

    /// <summary>What this pass collected and passed over.</summary>
    public HarvestReport Report { get; }

    /// <summary>Whether the chest ran out of room, which stops the grabber for this pass.</summary>
    public bool IsFull => GrabberOutput.IsChestFull(this.Chest);

    /// <summary>Get whether a chest has no room left.</summary>
    /// <remarks>
    ///   This can't use <c>Inventory.HasEmptySlots</c>: that compares the number of slots which exist
    ///   against the number in use, and an untouched grabber has an empty list rather than 36 empty
    ///   slots — so it reports a brand new grabber as full. Comparing against the chest's capacity is
    ///   what actually answers the question.
    /// </remarks>
    public static bool IsChestFull(Chest chest)
    {
        return chest.Items.Count(item => item != null) >= chest.GetActualCapacity();
    }

    public GrabberOutput(Chest chest, HarvestReport report)
    {
        this.Chest = chest;
        this.Report = report;
    }

    /// <summary>Put a harvested item in the chest, or drop it where it came from if the chest filled up mid-harvest.</summary>
    /// <param name="item">The harvested item.</param>
    /// <param name="location">The location it was harvested from.</param>
    /// <param name="tile">The tile it was harvested from.</param>
    public void Deposit(Item? item, GameLocation location, Vector2 tile)
    {
        if (item == null)
            return;

        Item? leftover = this.Chest.addItem(item);
        this.Report.Add(item.DisplayName, item.Stack - (leftover?.Stack ?? 0));

        // A single harvest can yield several stacks (a meteorite gives ore, stone and geodes at once),
        // so the last slot can fill partway through. Anything that doesn't fit is dropped on the tile
        // it came from rather than deleted.
        if (leftover != null)
        {
            this.Report.Skip($"{item.DisplayName} dropped on the ground (grabber full)");
            Game1.createItemDebris(leftover, tile * 64f, -1, location);
        }
    }
}
