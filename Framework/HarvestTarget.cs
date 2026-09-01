namespace BetterAutoGrabber.Framework;

/// <summary>The section of the grabber's target list that a target is listed under.</summary>
internal enum TargetGroup
{
    Forage,
    Crops,
    FruitTrees,
    Bushes,
    Clumps,
    Digging,
    Trees,
    Animals,
    Machines
}

/// <summary>One selectable row on a grabber's target list.</summary>
/// <remarks>
///   A row is whatever the grabber physically interacts with, which isn't always the item you end up
///   holding. Forage, crops and fruit are listed as the item itself; a large log is listed as the log,
///   because you can't ask for hardwood without saying where it should come from.
/// </remarks>
internal sealed class HarvestTarget
{
    /// <summary>A stable identifier saved in the grabber's mod data, like <c>forage:(O)16</c>.</summary>
    public string Id { get; }

    /// <summary>The name shown on the row.</summary>
    public string DisplayName { get; }

    /// <summary>The section this row is listed under.</summary>
    public TargetGroup Group { get; }

    /// <summary>The qualified item ID whose sprite is drawn beside the row.</summary>
    public string IconItemId { get; }

    public HarvestTarget(string id, string displayName, TargetGroup group, string iconItemId)
    {
        this.Id = id;
        this.DisplayName = displayName;
        this.Group = group;
        this.IconItemId = iconItemId;
    }
}
