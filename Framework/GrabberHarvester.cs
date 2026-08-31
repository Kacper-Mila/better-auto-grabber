using StardewValley;
using StardewValley.Characters;
using StardewValley.TerrainFeatures;
using Microsoft.Xna.Framework;

namespace BetterAutoGrabber.Framework;

/// <summary>A stand-in Junimo that routes crop yields into a grabber instead of a Junimo hut.</summary>
/// <remarks>
///   <see cref="Crop.harvest" /> takes a <see cref="JunimoHarvester" /> as its "harvested by something
///   other than the player" path: it skips the player's animations, inventory and sounds, and hands each
///   item to <see cref="JunimoHarvester.tryToAddItemToHut" />. Overriding that one virtual method lets us
///   reuse the game's own harvest logic — quality rolls, professions, multi-yield crops and giant-crop
///   checks all included — instead of reimplementing it and drifting out of sync with the game.
/// </remarks>
internal sealed class GrabberHarvester : JunimoHarvester
{
    private GrabberOutput Output = null!;
    private GameLocation TargetLocation = null!;
    private Vector2 TargetTile;

    /// <summary>Point the harvester at a tile before harvesting it.</summary>
    public void Retarget(GrabberOutput output, GameLocation location, Vector2 tile)
    {
        this.Output = output;
        this.TargetLocation = location;
        this.TargetTile = tile;
        this.currentLocation = location;
        this.Position = tile * 64f;
    }

    /// <inheritdoc />
    public override void tryToAddItemToHut(Item i)
    {
        this.Output.Deposit(i, this.TargetLocation, this.TargetTile);
    }
}
