using System;
using System.Collections.Generic;
using System.Linq;

namespace BetterAutoGrabber.Framework;

/// <summary>What one grabber's harvest pass did, and what it declined to do.</summary>
/// <remarks>
///   The declined half matters as much as the collected half: "why didn't my tapper get emptied" is
///   otherwise unanswerable without attaching a debugger.
/// </remarks>
internal sealed class HarvestReport
{
    /// <summary>How many of each item was collected, keyed by display name.</summary>
    public Dictionary<string, int> Collected { get; } = new();

    /// <summary>How many times each reason stopped something being collected.</summary>
    public Dictionary<string, int> Skipped { get; } = new();

    /// <summary>The locations this pass actually swept.</summary>
    public List<string> Locations { get; } = new();

    /// <summary>The total number of items collected.</summary>
    public int Total { get; private set; }

    /// <summary>Whether the pass ended early because the grabber was full.</summary>
    public bool StoppedWhenFull { get; set; }

    /// <summary>Whether anything at all is worth reporting.</summary>
    public bool HasAnythingToSay => this.Total > 0 || this.Skipped.Count > 0;

    /// <summary>Record items landing in the grabber.</summary>
    public void Add(string itemName, int count)
    {
        if (count <= 0)
            return;

        this.Collected[itemName] = this.Collected.GetValueOrDefault(itemName) + count;
        this.Total += count;
    }

    /// <summary>Record something the grabber wanted but didn't take.</summary>
    public void Skip(string reason)
    {
        this.Skipped[reason] = this.Skipped.GetValueOrDefault(reason) + 1;
    }

    /// <summary>Summarise what was collected, most plentiful first.</summary>
    public string DescribeCollected()
    {
        return string.Join(", ", this.Collected.OrderByDescending(pair => pair.Value).Select(pair => $"{pair.Key} x{pair.Value}"));
    }

    /// <summary>Summarise what was passed over.</summary>
    public string DescribeSkipped()
    {
        return string.Join("; ", this.Skipped.OrderByDescending(pair => pair.Value).Select(pair => $"{pair.Key} (x{pair.Value})"));
    }
}
