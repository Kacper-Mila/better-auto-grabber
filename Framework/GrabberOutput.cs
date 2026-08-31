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

    /// <summary>How many items were collected this pass.</summary>
    public int Collected { get; private set; }

    /// <summary>Whether the chest ran out of room, which stops the grabber for this pass.</summary>
    public bool IsFull => !this.Chest.Items.HasEmptySlots();

    public GrabberOutput(Chest chest)
    {
        this.Chest = chest;
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
        this.Collected += item.Stack - (leftover?.Stack ?? 0);

        // A single harvest can yield several stacks (a meteorite gives ore, stone and geodes at once),
        // so the last slot can fill partway through. Anything that doesn't fit is dropped on the tile
        // it came from rather than deleted.
        if (leftover != null)
            Game1.createItemDebris(leftover, tile * 64f, -1, location);
    }
}
