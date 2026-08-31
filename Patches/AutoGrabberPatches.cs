using System;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;
using Object = StardewValley.Object;

namespace BetterAutoGrabber.Patches;

/// <summary>Patches the auto-grabber so its menu can be opened at any time.</summary>
/// <remarks>
///   Vanilla only opens the grabber menu when the chest has something in it, so an empty grabber can't be
///   configured — or even opened. Since a grabber with no targets selected stays empty forever, this
///   patch is what makes the settings page reachable at all.
/// </remarks>
internal static class AutoGrabberPatches
{
    /// <summary>The auto-grabber's qualified item ID.</summary>
    public const string AutoGrabberId = "(BC)165";

    private static IMonitor Monitor = null!;

    /// <summary>Apply the patches.</summary>
    public static void Apply(Harmony harmony, IMonitor monitor)
    {
        AutoGrabberPatches.Monitor = monitor;

        harmony.Patch(
            original: AccessTools.Method(typeof(Object), "CheckForActionOnAutoGrabber"),
            prefix: new HarmonyMethod(typeof(AutoGrabberPatches), nameof(AutoGrabberPatches.Before_CheckForActionOnAutoGrabber))
        );
    }

    /// <summary>Open the grabber menu even when the grabber is empty.</summary>
    private static bool Before_CheckForActionOnAutoGrabber(Object __instance, Farmer who, bool justCheckingForActivity, ref bool __result)
    {
        try
        {
            if (justCheckingForActivity)
            {
                __result = true;
                return false;
            }

            if (__instance.heldObject.Value is not Chest chest)
                return true;

            AutoGrabberPatches.OpenMenu(__instance, chest);
            __result = true;
            return false;
        }
        catch (Exception ex)
        {
            AutoGrabberPatches.Monitor.Log($"Failed opening the auto-grabber menu, falling back to the vanilla one: {ex}", LogLevel.Error);
            return true;
        }
    }

    /// <summary>Open the grabber's inventory menu.</summary>
    public static void OpenMenu(Object grabber, Chest chest)
    {
        Game1.activeClickableMenu = new ItemGrabMenu(
            inventory: chest.Items,
            reverseGrab: false,
            showReceivingMenu: true,
            highlightFunction: InventoryMenu.highlightAllItems,
            behaviorOnItemSelectFunction: chest.grabItemFromInventory,
            message: null,
            behaviorOnItemGrab: (item, farmer) => AutoGrabberPatches.GrabItem(grabber, chest, item, farmer),
            snapToBottom: false,
            canBeExitedWithKey: true,
            playRightClickSound: true,
            allowRightClick: true,
            showOrganizeButton: true,
            source: 1,
            sourceItem: null,
            whichSpecialButton: -1,
            context: grabber
        );
    }

    /// <summary>Take an item out of the grabber, mirroring the game's own handler.</summary>
    /// <remarks>The vanilla equivalent is protected, so it's reimplemented here to keep the reopened menu ours.</remarks>
    private static void GrabItem(Object grabber, Chest chest, Item item, Farmer who)
    {
        if (who.couldInventoryAcceptThisItem(item))
        {
            chest.Items.Remove(item);
            chest.clearNulls();
            AutoGrabberPatches.OpenMenu(grabber, chest);
        }

        if (chest.isEmpty())
            grabber.showNextIndex.Value = false;
    }
}
