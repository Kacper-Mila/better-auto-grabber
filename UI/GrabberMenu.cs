using System.Collections.Generic;
using BetterAutoGrabber.Framework;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;
using Object = StardewValley.Object;

namespace BetterAutoGrabber.UI;

/// <summary>The auto-grabber's inventory menu, with a button that opens its settings page.</summary>
/// <remarks>
///   This subclasses the vanilla menu rather than drawing an overlay on top of it. An overlay has to do
///   its own hit-testing, and the cursor position a mod gets outside of rendering is in world space
///   rather than the menu's UI space — so on any zoom level other than 100% the button draws in one
///   place and responds in another. Letting the game dispatch the click removes the problem entirely.
/// </remarks>
internal sealed class GrabberMenu : ItemGrabMenu
{
    private readonly Object Grabber;
    private readonly Chest GrabberChest;
    private readonly ModConfig Config;
    private ClickableTextureComponent SettingsButton = null!;

    public GrabberMenu(Object grabber, Chest chest, ModConfig config)
        : base(
            inventory: GrabberMenu.PrepareInventory(chest),
            reverseGrab: false,
            showReceivingMenu: true,
            highlightFunction: InventoryMenu.highlightAllItems,
            behaviorOnItemSelectFunction: chest.grabItemFromInventory,
            message: null,
            behaviorOnItemGrab: null,
            snapToBottom: false,
            canBeExitedWithKey: true,
            playRightClickSound: true,
            allowRightClick: true,
            showOrganizeButton: true,
            source: 1,
            sourceItem: null,
            whichSpecialButton: -1,
            context: grabber)
    {
        this.Grabber = grabber;
        this.GrabberChest = chest;
        this.Config = config;

        this.behaviorOnItemGrab = this.OnItemGrabbed;
        this.PositionSettingsButton();
    }

    /// <inheritdoc />
    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (this.SettingsButton.containsPoint(x, y))
        {
            Game1.playSound("smallSelect");
            GrabberSettingsMenu settings = new(this.Grabber, GrabberSettings.Load(this.Grabber), this.Config);

            // come back to the grabber's inventory when the settings page is closed
            settings.exitFunction = () => Game1.activeClickableMenu = new GrabberMenu(this.Grabber, this.GrabberChest, this.Config);
            Game1.activeClickableMenu = settings;
            return;
        }

        base.receiveLeftClick(x, y, playSound);
    }

    /// <inheritdoc />
    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
    {
        base.gameWindowSizeChanged(oldBounds, newBounds);
        this.PositionSettingsButton();
    }

    /// <inheritdoc />
    public override void draw(SpriteBatch b)
    {
        base.draw(b);

        this.SettingsButton.draw(b);
        if (this.SettingsButton.containsPoint(Game1.getOldMouseX(), Game1.getOldMouseY()))
            IClickableMenu.drawHoverText(b, I18n.Menu_SettingsTooltip(), Game1.smallFont);

        // the base menu draws the cursor, so it has to be drawn again above the button
        this.drawMouse(b);
    }

    /// <summary>Give the chest its full set of slots before the menu lays itself out.</summary>
    /// <remarks>
    ///   An untouched grabber holds an empty list rather than 36 empty slots, because vanilla only ever
    ///   opens a grabber that already has something in it. The menu lays out its grid from that list, so
    ///   without this the receiving area comes out malformed. Emptying a chest by hand leaves it in this
    ///   same padded state, so it's nothing the game doesn't already do.
    /// </remarks>
    private static IList<Item> PrepareInventory(Chest chest)
    {
        while (chest.Items.Count < chest.GetActualCapacity())
            chest.Items.Add(null);

        return chest.Items;
    }

    /// <inheritdoc />
    protected override void cleanupBeforeExit()
    {
        base.cleanupBeforeExit();

        // don't leave the padding behind in the save file
        this.GrabberChest.clearNulls();
    }

    /// <summary>Put the settings button in the column of buttons down the menu's right edge.</summary>
    private void PositionSettingsButton()
    {
        this.SettingsButton = new ClickableTextureComponent(
            new Rectangle(
                this.xPositionOnScreen + this.width + this.Config.SettingsButtonOffsetX,
                this.yPositionOnScreen + this.height / 3 - 64 - 64 - 16 - 80 + this.Config.SettingsButtonOffsetY,
                64,
                64),
            Game1.mouseCursors,
            new Rectangle(383, 493, 11, 14),
            4f);
    }

    /// <summary>Take an item out of the grabber, mirroring the game's own handler.</summary>
    private void OnItemGrabbed(Item item, Farmer who)
    {
        if (who.couldInventoryAcceptThisItem(item))
        {
            this.GrabberChest.Items.Remove(item);
            this.GrabberChest.clearNulls();
            Game1.activeClickableMenu = new GrabberMenu(this.Grabber, this.GrabberChest, this.Config);
        }

        if (this.GrabberChest.isEmpty())
            this.Grabber.showNextIndex.Value = false;
    }
}
