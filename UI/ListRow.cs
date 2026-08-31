using System;

namespace BetterAutoGrabber.UI;

/// <summary>One line in a scrolling checkbox list.</summary>
internal sealed class ListRow
{
    /// <summary>The text drawn on the row.</summary>
    public string Label { get; init; } = "";

    /// <summary>The qualified item ID whose sprite is drawn beside the label, if any.</summary>
    public string? IconItemId { get; init; }

    /// <summary>Whether this row is a section heading rather than a checkbox.</summary>
    public bool IsHeader { get; init; }

    /// <summary>Whether the row's checkbox is ticked.</summary>
    public Func<bool> IsChecked { get; init; } = () => false;

    /// <summary>Toggle the row.</summary>
    public Action Toggle { get; init; } = () => { };

    /// <summary>Whether the row is shown as unavailable.</summary>
    public bool Greyed { get; init; }

    /// <summary>Extra text drawn right-aligned on the row, such as a section's selected count.</summary>
    public string? Suffix { get; init; }

    /// <summary>Whether the suffix is an action, and should be drawn as a button.</summary>
    public bool SuffixIsButton { get; init; }

    /// <summary>Whether the row's value is chosen from a dropdown rather than toggled.</summary>
    public bool IsDropdown { get; init; }
}
