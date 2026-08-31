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
            inventory: chest.Items,
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
        this.LayOutContents();
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
        this.LayOutContents();
        this.PositionSettingsButton();
    }

    /// <summary>Get the menu's geometry, for diagnosing layout problems.</summary>
    public string DescribeLayout()
    {
        return $"menu ({this.xPositionOnScreen},{this.yPositionOnScreen}) {this.width}x{this.height}"
            + $" | contents ({this.ItemsToGrabMenu.xPositionOnScreen},{this.ItemsToGrabMenu.yPositionOnScreen}) {this.ItemsToGrabMenu.width}x{this.ItemsToGrabMenu.height} capacity {this.ItemsToGrabMenu.capacity} rows {this.ItemsToGrabMenu.rows}"
            + $" | backpack ({this.inventory.xPositionOnScreen},{this.inventory.yPositionOnScreen}) {this.inventory.width}x{this.inventory.height} capacity {this.inventory.capacity} rows {this.inventory.rows}";
    }

    /// <summary>Build the grabber's item grid and put it clear of the player's inventory.</summary>
    /// <remarks>
    ///   The inherited layout puts the two grids on top of each other here, so rather than trusting the
    ///   positions that come out of the base menu, the grid is rebuilt at a stated size and anchored to
    ///   the player's inventory, which is the one part known to land in the right place.
    /// </remarks>
    private void LayOutContents()
    {
        InventoryMenu grid = new(
            this.xPositionOnScreen + 32,
            this.yPositionOnScreen,
            playerInventory: false,
            this.GrabberChest.Items,
            InventoryMenu.highlightAllItems,
            this.GrabberChest.GetActualCapacity(),
            3);

        grid.SetPosition(grid.xPositionOnScreen, this.inventory.yPositionOnScreen - grid.height - 100);
        grid.populateClickableComponentList();

        // keep the ID offsets the base menu applies, so controller navigation still works
        foreach (ClickableComponent slot in grid.inventory)
        {
            if (slot == null)
                continue;

            slot.myID += 53910;
            slot.upNeighborID += 53910;
            slot.rightNeighborID += 53910;
            slot.downNeighborID = -7777;
            slot.leftNeighborID += 53910;
            slot.fullyImmutable = true;
        }

        this.ItemsToGrabMenu = grid;
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
