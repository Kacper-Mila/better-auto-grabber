using System;
using BetterAutoGrabber.Framework;
using BetterAutoGrabber.UI;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
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
    private static ModConfig Config = null!;

    /// <summary>Apply the patches.</summary>
    public static void Apply(Harmony harmony, IMonitor monitor, ModConfig config)
    {
        AutoGrabberPatches.Monitor = monitor;
        AutoGrabberPatches.Config = config;

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

            Game1.activeClickableMenu = new GrabberMenu(__instance, chest, AutoGrabberPatches.Config);
            __result = true;
            return false;
        }
        catch (Exception ex)
        {
            AutoGrabberPatches.Monitor.Log($"Failed opening the auto-grabber menu, falling back to the vanilla one: {ex}", LogLevel.Error);
            return true;
        }
    }
}
