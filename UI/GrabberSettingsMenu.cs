using System;
using System.Collections.Generic;
using System.Linq;
using BetterAutoGrabber.Framework;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Menus;
using Object = StardewValley.Object;

namespace BetterAutoGrabber.UI;

/// <summary>The settings page for one placed auto-grabber.</summary>
internal sealed class GrabberSettingsMenu : IClickableMenu
{
    /*********
    ** Fields
    *********/
    private const int RowHeight = 56;
    private const int MenuWidth = 900;
    private const int MenuHeight = 700;

    private readonly Object Grabber;
    private readonly GrabberSettings Settings;
    private readonly ModConfig Config;

    private readonly ClickableComponent TargetsTab;
    private readonly ClickableComponent ScopeTab;
    private readonly ClickableTextureComponent ScrollUp;
    private readonly ClickableTextureComponent ScrollDown;
    private readonly TextBox SearchBox;

    private readonly List<ListRow> Rows = new();
    private bool ShowingScope;
    private int ScrollIndex;
    private string LastSearch = "";

    /// <summary>How many rows fit in the list area.</summary>
    private int VisibleRows => (this.height - this.ListTop() + this.yPositionOnScreen - 80) / GrabberSettingsMenu.RowHeight;

    /*********
    ** Public methods
    *********/
    public GrabberSettingsMenu(Object grabber, GrabberSettings settings, ModConfig config)
        : base(
            (Game1.uiViewport.Width - GrabberSettingsMenu.MenuWidth) / 2,
            (Game1.uiViewport.Height - GrabberSettingsMenu.MenuHeight) / 2,
            GrabberSettingsMenu.MenuWidth,
            GrabberSettingsMenu.MenuHeight,
            showUpperRightCloseButton: true)
    {
        this.Grabber = grabber;
        this.Settings = settings;
        this.Config = config;

        int tabWidth = 200;
        this.TargetsTab = new ClickableComponent(new Rectangle(this.xPositionOnScreen + 32, this.yPositionOnScreen + 72, tabWidth, 48), I18n.Menu_TabTargets());
        this.ScopeTab = new ClickableComponent(new Rectangle(this.xPositionOnScreen + 32 + tabWidth + 16, this.yPositionOnScreen + 72, tabWidth, 48), I18n.Menu_TabScope());

        this.SearchBox = new TextBox(Game1.content.Load<Texture2D>("LooseSprites\\textBox"), null, Game1.smallFont, Game1.textColor)
        {
            X = this.xPositionOnScreen + 32,
            Y = this.yPositionOnScreen + 136,
            Width = this.width - 160
        };

        this.ScrollUp = new ClickableTextureComponent(new Rectangle(this.xPositionOnScreen + this.width - 60, this.yPositionOnScreen + 200, 44, 48), Game1.mouseCursors, new Rectangle(421, 459, 11, 12), 4f);
        this.ScrollDown = new ClickableTextureComponent(new Rectangle(this.xPositionOnScreen + this.width - 60, this.yPositionOnScreen + this.height - 120, 44, 48), Game1.mouseCursors, new Rectangle(421, 472, 11, 12), 4f);

        this.BuildRows();
    }

    /// <inheritdoc />
    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        base.receiveLeftClick(x, y, playSound);

        if (this.TargetsTab.containsPoint(x, y) && this.ShowingScope)
        {
            this.SwitchTab(showScope: false);
            return;
        }

        if (this.ScopeTab.containsPoint(x, y) && !this.ShowingScope)
        {
            this.SwitchTab(showScope: true);
            return;
        }

        if (this.SearchBox.Y <= y && y <= this.SearchBox.Y + 48 && this.SearchBox.X <= x && x <= this.SearchBox.X + this.SearchBox.Width)
        {
            this.SearchBox.SelectMe();
            return;
        }

        this.SearchBox.Selected = false;

        if (this.ScrollUp.containsPoint(x, y))
        {
            this.Scroll(-1);
            return;
        }

        if (this.ScrollDown.containsPoint(x, y))
        {
            this.Scroll(1);
            return;
        }

        int index = this.RowIndexAt(y);
        if (index >= 0 && index < this.Rows.Count)
        {
            ListRow row = this.Rows[index];
            if (row.Greyed)
                return;

            row.Toggle();
            this.Settings.Save(this.Grabber);
            Game1.playSound("drumkit6");

            // headers show a live count, and the scope tab's list appears and disappears with the mode
            this.BuildRows();
        }
    }

    /// <inheritdoc />
    public override void receiveScrollWheelAction(int direction)
    {
        this.Scroll(direction > 0 ? -1 : 1);
    }

    /// <inheritdoc />
    public override void receiveKeyPress(Keys key)
    {
        // let the search box swallow keystrokes so typing "e" doesn't close the menu
        if (this.SearchBox.Selected && key != Keys.Escape)
            return;

        if (this.SearchBox.Selected && key == Keys.Escape)
        {
            this.SearchBox.Selected = false;
            return;
        }

        base.receiveKeyPress(key);
    }

    /// <inheritdoc />
    public override void update(GameTime time)
    {
        base.update(time);

        if (this.SearchBox.Text != this.LastSearch)
        {
            this.LastSearch = this.SearchBox.Text;
            this.ScrollIndex = 0;
            this.BuildRows();
        }
    }

    /// <inheritdoc />
    public override void draw(SpriteBatch b)
    {
        b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * 0.5f);
        IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60), this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, Color.White);

        SpriteText.drawString(b, I18n.Menu_Title(), this.xPositionOnScreen + 32, this.yPositionOnScreen + 24);

        this.DrawTab(b, this.TargetsTab, !this.ShowingScope);
        this.DrawTab(b, this.ScopeTab, this.ShowingScope);

        this.SearchBox.Draw(b);
        if (string.IsNullOrEmpty(this.SearchBox.Text) && !this.SearchBox.Selected)
            Utility.drawTextWithShadow(b, I18n.Menu_SearchHint(), Game1.smallFont, new Vector2(this.SearchBox.X + 16, this.SearchBox.Y + 12), Game1.textColor * 0.5f);

        this.DrawRows(b);

        Utility.drawTextWithShadow(b, this.FooterText(), Game1.smallFont, new Vector2(this.xPositionOnScreen + 32, this.yPositionOnScreen + this.height - 68), Game1.textColor);

        if (this.ScrollIndex > 0)
            this.ScrollUp.draw(b);
        if (this.ScrollIndex + this.VisibleRows < this.Rows.Count)
            this.ScrollDown.draw(b);

        base.draw(b);
        this.drawMouse(b);
    }

    /*********
    ** Private methods
    *********/
    /// <summary>Get the top of the scrolling list area.</summary>
    private int ListTop() => this.yPositionOnScreen + 200;

    /// <summary>Get the row index under a screen position, or -1.</summary>
    private int RowIndexAt(int y)
    {
        int offset = y - this.ListTop();
        if (offset < 0)
            return -1;

        int row = offset / GrabberSettingsMenu.RowHeight;
        return row >= this.VisibleRows ? -1 : this.ScrollIndex + row;
    }

    private void SwitchTab(bool showScope)
    {
        this.ShowingScope = showScope;
        this.ScrollIndex = 0;
        this.SearchBox.Text = "";
        this.LastSearch = "";
        this.BuildRows();
        Game1.playSound("smallSelect");
    }

    private void Scroll(int direction)
    {
        int max = Math.Max(0, this.Rows.Count - this.VisibleRows);
        int next = Math.Clamp(this.ScrollIndex + direction * 3, 0, max);
        if (next != this.ScrollIndex)
        {
            this.ScrollIndex = next;
            Game1.playSound("shiny4");
        }
    }

    private string FooterText()
    {
        return this.Settings.TargetIds.Count == 0
            ? I18n.Menu_NothingSelected()
            : I18n.Menu_SelectedCount(this.Settings.TargetIds.Count);
    }

    /// <summary>Rebuild the visible rows for the current tab and search text.</summary>
    private void BuildRows()
    {
        this.Rows.Clear();

        if (this.ShowingScope)
            this.BuildScopeRows();
        else
            this.BuildTargetRows();

        this.ScrollIndex = Math.Clamp(this.ScrollIndex, 0, Math.Max(0, this.Rows.Count - this.VisibleRows));
    }

    /// <summary>Build the target list, grouped into sections with a check-all row each.</summary>
    private void BuildTargetRows()
    {
        string search = this.SearchBox.Text?.Trim() ?? "";

        foreach (TargetGroup group in Enum.GetValues<TargetGroup>())
        {
            List<HarvestTarget> matches = TargetCatalog.All
                .Where(target => target.Group == group)
                .Where(target => search.Length == 0 || target.DisplayName.Contains(search, StringComparison.CurrentCultureIgnoreCase))
                .ToList();

            if (matches.Count == 0)
                continue;

            int selected = matches.Count(target => this.Settings.TargetIds.Contains(target.Id));
            this.Rows.Add(new ListRow
            {
                Label = GrabberSettingsMenu.GroupName(group),
                IsHeader = true,
                Suffix = selected == matches.Count ? I18n.Menu_UncheckAll() : I18n.Menu_CheckAll(),
                IsChecked = () => false,
                Toggle = () =>
                {
                    if (selected == matches.Count)
                        this.Settings.TargetIds.ExceptWith(matches.Select(target => target.Id));
                    else
                        this.Settings.TargetIds.UnionWith(matches.Select(target => target.Id));
                }
            });

            foreach (HarvestTarget target in matches)
            {
                this.Rows.Add(new ListRow
                {
                    Label = target.DisplayName,
                    IconItemId = target.IconItemId,
                    IsChecked = () => this.Settings.TargetIds.Contains(target.Id),
                    Toggle = () =>
                    {
                        if (!this.Settings.TargetIds.Add(target.Id))
                            this.Settings.TargetIds.Remove(target.Id);
                    }
                });
            }
        }

        if (this.Rows.Count == 0)
            this.Rows.Add(new ListRow { Label = I18n.Menu_NoResults(), IsHeader = true });
    }

    /// <summary>Build the scope tab: reach, frequency, and the location picker when it applies.</summary>
    private void BuildScopeRows()
    {
        string here = this.Grabber.Location?.DisplayName ?? this.Grabber.Location?.Name ?? "?";

        this.Rows.Add(new ListRow { Label = I18n.Menu_TabScope(), IsHeader = true });
        this.AddScopeOption(ScopeMode.Local, I18n.Scope_Local(), I18n.Scope_LocalDesc(here));
        this.AddScopeOption(ScopeMode.Global, I18n.Scope_Global(), I18n.Scope_GlobalDesc());
        this.AddScopeOption(ScopeMode.Selected, I18n.Scope_Selected(), I18n.Scope_SelectedDesc());

        this.Rows.Add(new ListRow
        {
            Label = I18n.Scope_Frequency(),
            IsHeader = true,
            Suffix = GrabberSettingsMenu.FrequencyName(this.Settings.Frequency),
            Toggle = () => this.Settings.Frequency = GrabberSettingsMenu.NextFrequency(this.Settings.Frequency)
        });

        if (this.Settings.Scope != ScopeMode.Selected)
            return;

        string search = this.SearchBox.Text?.Trim() ?? "";
        List<GameLocation> locations = GrabberSettings.AllLocations()
            .Where(location => !string.IsNullOrWhiteSpace(location.Name))
            .GroupBy(location => location.Name)
            .Select(group => group.First())
            .OrderBy(location => location.DisplayName ?? location.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        foreach (GameLocation location in locations)
        {
            string key = location.Name;
            string name = string.IsNullOrWhiteSpace(location.DisplayName) ? key : location.DisplayName;
            bool visited = GrabberSettings.HasVisited(location);

            if (search.Length > 0
                && !name.Contains(search, StringComparison.CurrentCultureIgnoreCase)
                && !key.Contains(search, StringComparison.CurrentCultureIgnoreCase))
            {
                continue;
            }

            this.Rows.Add(new ListRow
            {
                Label = name,

                // several places share a display name -- the farm, the farmhouse and the cellar are all
                // "<name> Farm" -- so the internal name is shown alongside to tell them apart
                Suffix = visited ? key : $"{key} - {I18n.Scope_UnvisitedNote()}",
                Greyed = !visited,
                IsChecked = () => this.Settings.SelectedLocations.Contains(key),
                Toggle = () =>
                {
                    if (!this.Settings.SelectedLocations.Add(key))
                        this.Settings.SelectedLocations.Remove(key);
                }
            });
        }
    }

    private void AddScopeOption(ScopeMode mode, string label, string description)
    {
        this.Rows.Add(new ListRow
        {
            Label = label,
            Suffix = description,
            IsChecked = () => this.Settings.Scope == mode,
            Toggle = () => this.Settings.Scope = mode
        });
    }

    /// <summary>Draw the visible slice of the row list.</summary>
    private void DrawRows(SpriteBatch b)
    {
        int y = this.ListTop();

        for (int i = this.ScrollIndex; i < this.Rows.Count && i < this.ScrollIndex + this.VisibleRows; i++)
        {
            ListRow row = this.Rows[i];
            int x = this.xPositionOnScreen + 40;
            float alpha = row.Greyed ? 0.4f : 1f;

            if (row.IsHeader)
            {
                Utility.drawTextWithShadow(b, row.Label, Game1.dialogueFont, new Vector2(x, y + 8), Game1.textColor * alpha, 0.75f);
            }
            else
            {
                b.Draw(Game1.mouseCursors, new Vector2(x, y + 12), row.IsChecked() ? OptionsCheckbox.sourceRectChecked : OptionsCheckbox.sourceRectUnchecked, Color.White * alpha, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.4f);

                int textX = x + 56;
                if (row.IconItemId != null)
                {
                    ParsedItemData data = ItemRegistry.GetDataOrErrorItem(row.IconItemId);
                    b.Draw(data.GetTexture(), new Vector2(textX, y + 4), data.GetSourceRect(), Color.White * alpha, 0f, Vector2.Zero, 2.5f, SpriteEffects.None, 0.9f);
                    textX += 52;
                }

                Utility.drawTextWithShadow(b, row.Label, Game1.smallFont, new Vector2(textX, y + 14), Game1.textColor * alpha);
            }

            if (row.Suffix != null)
            {
                Vector2 size = Game1.smallFont.MeasureString(row.Suffix);
                Utility.drawTextWithShadow(b, row.Suffix, Game1.smallFont, new Vector2(this.xPositionOnScreen + this.width - 80 - size.X, y + 14), Game1.textColor * 0.7f);
            }

            y += GrabberSettingsMenu.RowHeight;
        }
    }

    private void DrawTab(SpriteBatch b, ClickableComponent tab, bool active)
    {
        IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(active ? 0 : 64, 256, 60, 60), tab.bounds.X, tab.bounds.Y, tab.bounds.Width, tab.bounds.Height, Color.White);
        Utility.drawTextWithShadow(b, tab.name, Game1.smallFont, new Vector2(tab.bounds.X + 20, tab.bounds.Y + 12), Game1.textColor * (active ? 1f : 0.6f));
    }

    private static string GroupName(TargetGroup group)
    {
        return group switch
        {
            TargetGroup.Forage => I18n.Group_Forage(),
            TargetGroup.Crops => I18n.Group_Crops(),
            TargetGroup.FruitTrees => I18n.Group_FruitTrees(),
            TargetGroup.Bushes => I18n.Group_Bushes(),
            TargetGroup.Clumps => I18n.Group_Clumps(),
            _ => I18n.Group_Digging()
        };
    }

    private static string FrequencyName(GrabFrequency frequency)
    {
        return frequency switch
        {
            GrabFrequency.TenMinutes => I18n.Frequency_TenMinutes(),
            GrabFrequency.Hourly => I18n.Frequency_Hourly(),
            GrabFrequency.FourHours => I18n.Frequency_FourHours(),
            GrabFrequency.Daily => I18n.Frequency_Daily(),
            _ => I18n.Frequency_Default()
        };
    }

    private static GrabFrequency NextFrequency(GrabFrequency frequency)
    {
        GrabFrequency[] all = Enum.GetValues<GrabFrequency>();
        return all[(Array.IndexOf(all, frequency) + 1) % all.Length];
    }
}
