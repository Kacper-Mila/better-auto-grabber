using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using BetterAutoGrabber.Framework;
using BetterAutoGrabber.Patches;
using BetterAutoGrabber.UI;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;
using Object = StardewValley.Object;

namespace BetterAutoGrabber;

/// <summary>The mod entry point.</summary>
internal sealed class ModEntry : Mod
{
    /*********
    ** Fields
    *********/
    /// <summary>The Deluxe Grabber Fix mod, which harvests the same things and would double up with this one.</summary>
    private const string ConflictingModId = "Rafia.DeluxeGrabberFix";

    private ModConfig Config = null!;
    private HarvestEngine Engine = null!;

    /// <summary>How much each grabber collected today, keyed by the location it stands in.</summary>
    private readonly Dictionary<string, int> DailyTally = new();

    /// <summary>The grabbers that filled up today, keyed the same way.</summary>
    private readonly HashSet<string> FullGrabbers = new();

    /// <summary>The settings button drawn on the open grabber menu, if one is open.</summary>
    private ClickableTextureComponent? SettingsButton;

    /*********
    ** Public methods
    *********/
    /// <inheritdoc />
    public override void Entry(IModHelper helper)
    {
        this.Config = helper.ReadConfig<ModConfig>();
        I18n.Init(helper.Translation);
        this.Engine = new HarvestEngine(this.Config, this.Monitor);

        AutoGrabberPatches.Apply(new Harmony(this.ModManifest.UniqueID), this.Monitor);

        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
        helper.Events.GameLoop.DayStarted += this.OnDayStarted;
        helper.Events.GameLoop.TimeChanged += this.OnTimeChanged;
        helper.Events.GameLoop.DayEnding += this.OnDayEnding;
        helper.Events.Display.RenderedActiveMenu += this.OnRenderedActiveMenu;
        helper.Events.Input.ButtonPressed += this.OnButtonPressed;
    }

    /*********
    ** Event handlers
    *********/
    /// <summary>Warn about overlapping mods and hook up the config UI.</summary>
    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        if (this.Helper.ModRegistry.IsLoaded(ModEntry.ConflictingModId))
            this.Monitor.Log("Deluxe Grabber Fix is installed. Both mods harvest the same things, so grabbers will collect twice or race each other. Disable one of them.", LogLevel.Warn);

        this.RegisterConfigMenu();
    }

    /// <summary>Build the target list from the loaded game data.</summary>
    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        TargetCatalog.Rebuild();
        this.Monitor.Log($"Loaded {TargetCatalog.All.Count} harvest targets.", LogLevel.Trace);
    }

    /// <summary>Run the grabbers set to collect once a day, and reset the day's tally.</summary>
    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        this.DailyTally.Clear();
        this.FullGrabbers.Clear();
        this.RunGrabbers(GrabFrequency.Daily);
    }

    /// <summary>Run the grabbers that are due this in-game clock tick.</summary>
    private void OnTimeChanged(object? sender, TimeChangedEventArgs e)
    {
        this.RunGrabbers(null);
    }

    /// <summary>Report what the grabbers collected today.</summary>
    private void OnDayEnding(object? sender, DayEndingEventArgs e)
    {
        if (!this.Config.DailySummary || this.DailyTally.Count == 0)
            return;

        this.Monitor.Log(I18n.Summary_Header(), LogLevel.Info);
        foreach ((string location, int count) in this.DailyTally.OrderByDescending(pair => pair.Value))
            this.Monitor.Log("  " + I18n.Summary_Line(location, count), LogLevel.Info);

        foreach (string location in this.FullGrabbers)
            Game1.addHUDMessage(new HUDMessage(I18n.Summary_Full(location), HUDMessage.error_type));
    }

    /// <summary>Draw the settings button on an open grabber menu.</summary>
    private void OnRenderedActiveMenu(object? sender, RenderedActiveMenuEventArgs e)
    {
        if (!this.TryGetOpenGrabber(out _, out ItemGrabMenu? menu))
        {
            this.SettingsButton = null;
            return;
        }

        this.SettingsButton = new ClickableTextureComponent(
            new Rectangle(
                menu.xPositionOnScreen + menu.width + this.Config.SettingsButtonOffsetX,
                menu.yPositionOnScreen + menu.height / 3 - 224 + this.Config.SettingsButtonOffsetY,
                64,
                64),
            Game1.mouseCursors,
            new Rectangle(383, 493, 11, 14),
            4f);

        this.SettingsButton.draw(e.SpriteBatch);

        if (this.SettingsButton.containsPoint(Game1.getOldMouseX(), Game1.getOldMouseY()))
            IClickableMenu.drawHoverText(e.SpriteBatch, I18n.Menu_SettingsTooltip(), Game1.smallFont);

        // the button is drawn after the menu, so the cursor has to be drawn again on top of it
        menu.drawMouse(e.SpriteBatch);
    }

    /// <summary>Open the settings page when the button is clicked.</summary>
    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (this.SettingsButton == null || (e.Button != SButton.MouseLeft && e.Button != SButton.ControllerA))
            return;

        if (!this.TryGetOpenGrabber(out Object? grabber, out _))
            return;

        Vector2 cursor = e.Cursor.GetScaledScreenPixels();
        if (!this.SettingsButton.containsPoint((int)cursor.X, (int)cursor.Y))
            return;

        this.Helper.Input.Suppress(e.Button);
        Game1.playSound("smallSelect");
        Game1.activeClickableMenu = new GrabberSettingsMenu(grabber, GrabberSettings.Load(grabber), this.Config);
    }

    /*********
    ** Harvesting
    *********/
    /// <summary>Run every grabber that's due.</summary>
    /// <param name="trigger">The frequency being triggered, or <c>null</c> for the ten-minute clock tick.</param>
    private void RunGrabbers(GrabFrequency? trigger)
    {
        // harvesting changes the world, so only the host does it; farmhands would each collect a copy
        if (!Context.IsWorldReady || !Context.IsMainPlayer)
            return;

        HashSet<string> claimed = new();

        foreach ((GameLocation home, Object grabber) in ModEntry.FindGrabbers())
        {
            GrabberSettings settings = GrabberSettings.Load(grabber);
            if (!settings.HasExtraTargets || !this.IsDue(settings, trigger))
                continue;

            // a location is swept once per pass, so two global grabbers don't both strip it
            List<GameLocation> locations = settings
                .ResolveLocations(grabber, this.Config)
                .Where(location => claimed.Add(location.NameOrUniqueName))
                .ToList();

            if (locations.Count == 0)
                continue;

            int collected = this.Engine.Run(grabber, settings, locations);
            if (collected <= 0)
                continue;

            string label = home.DisplayName ?? home.Name;
            this.DailyTally[label] = this.DailyTally.GetValueOrDefault(label) + collected;

            if (grabber.heldObject.Value is Chest chest)
            {
                grabber.showNextIndex.Value = !chest.isEmpty();
                if (!chest.Items.HasEmptySlots())
                    this.FullGrabbers.Add(label);
            }

            if (this.Config.VerboseLogging)
                this.Monitor.Log($"Grabber in '{home.NameOrUniqueName}' collected {collected} item(s) from {locations.Count} location(s).", LogLevel.Debug);
        }
    }

    /// <summary>Get every placed auto-grabber, ordered so narrower grabbers claim their location first.</summary>
    private static IEnumerable<(GameLocation Location, Object Grabber)> FindGrabbers()
    {
        List<(GameLocation Location, Object Grabber)> found = new();

        Utility.ForEachLocation(location =>
        {
            foreach (Object obj in location.objects.Values)
            {
                if (obj.QualifiedItemId == AutoGrabberPatches.AutoGrabberId)
                    found.Add((location, obj));
            }

            return true;
        });

        // local grabbers get first claim on their own location, then narrow selections, then global ones
        return found.OrderBy(entry => (int)GrabberSettings.Load(entry.Grabber).Scope)
            .ThenBy(entry => entry.Location.NameOrUniqueName, StringComparer.Ordinal)
            .ThenBy(entry => entry.Grabber.TileLocation.X)
            .ThenBy(entry => entry.Grabber.TileLocation.Y);
    }

    /// <summary>Get whether a grabber should run now.</summary>
    private bool IsDue(GrabberSettings settings, GrabFrequency? trigger)
    {
        GrabFrequency frequency = settings.Frequency == GrabFrequency.Default
            ? this.Config.DefaultFrequency
            : settings.Frequency;

        if (frequency == GrabFrequency.Default)
            frequency = GrabFrequency.Hourly;

        if (trigger == GrabFrequency.Daily)
            return frequency == GrabFrequency.Daily;

        return frequency switch
        {
            GrabFrequency.TenMinutes => true,
            GrabFrequency.Hourly => Game1.timeOfDay % 100 == 0,
            GrabFrequency.FourHours => Game1.timeOfDay % 100 == 0 && Game1.timeOfDay / 100 % 4 == 0,
            _ => false
        };
    }

    /*********
    ** Helpers
    *********/
    /// <summary>Get the grabber whose menu is open, if any.</summary>
    private bool TryGetOpenGrabber([NotNullWhen(true)] out Object? grabber, [NotNullWhen(true)] out ItemGrabMenu? menu)
    {
        grabber = null;
        menu = null;

        if (Game1.activeClickableMenu is not ItemGrabMenu found || found.context is not Object obj || obj.QualifiedItemId != AutoGrabberPatches.AutoGrabberId)
            return false;

        grabber = obj;
        menu = found;
        return true;
    }

    /// <summary>Add the mod's settings to Generic Mod Config Menu, if it's installed.</summary>
    private void RegisterConfigMenu()
    {
        IGenericModConfigMenuApi? api = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (api == null)
            return;

        api.Register(this.ModManifest, () => this.Config = new ModConfig(), () => this.Helper.WriteConfig(this.Config));

        api.AddTextOption(
            this.ModManifest,
            () => this.Config.DefaultFrequency.ToString(),
            value => this.Config.DefaultFrequency = Enum.TryParse(value, out GrabFrequency parsed) ? parsed : GrabFrequency.Hourly,
            () => I18n.Scope_Frequency(),
            allowedValues: new[] { nameof(GrabFrequency.TenMinutes), nameof(GrabFrequency.Hourly), nameof(GrabFrequency.FourHours), nameof(GrabFrequency.Daily) }
        );

        api.AddBoolOption(this.ModManifest, () => this.Config.RespectToolRequirements, value => this.Config.RespectToolRequirements = value, () => this.Helper.Translation.Get("config.respect-tools"));
        api.AddBoolOption(this.ModManifest, () => this.Config.GrantExperience, value => this.Config.GrantExperience = value, () => this.Helper.Translation.Get("config.grant-xp"));
        api.AddBoolOption(this.ModManifest, () => this.Config.ReplantCrops, value => this.Config.ReplantCrops = value, () => this.Helper.Translation.Get("config.replant"));
        api.AddBoolOption(this.ModManifest, () => this.Config.SkipFestivalLocations, value => this.Config.SkipFestivalLocations = value, () => this.Helper.Translation.Get("config.skip-festivals"));
        api.AddBoolOption(this.ModManifest, () => this.Config.DailySummary, value => this.Config.DailySummary = value, () => this.Helper.Translation.Get("config.daily-summary"));
        api.AddBoolOption(this.ModManifest, () => this.Config.VerboseLogging, value => this.Config.VerboseLogging = value, () => this.Helper.Translation.Get("config.verbose-logging"));
        api.AddNumberOption(this.ModManifest, () => this.Config.SettingsButtonOffsetX, value => this.Config.SettingsButtonOffsetX = value, () => this.Helper.Translation.Get("config.button-offset-x"), min: -500, max: 500);
        api.AddNumberOption(this.ModManifest, () => this.Config.SettingsButtonOffsetY, value => this.Config.SettingsButtonOffsetY = value, () => this.Helper.Translation.Get("config.button-offset-y"), min: -500, max: 500);
    }
}
