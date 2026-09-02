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

/// <summary>Which page of the grabber's settings is showing.</summary>
internal enum MenuTab
{
    /// <summary>What the grabber collects.</summary>
    Targets,

    /// <summary>Which locations it reaches.</summary>
    Scope,

    /// <summary>How it runs: its interval, and what it replants.</summary>
    Behaviour
}

/// <summary>The settings page for one placed auto-grabber.</summary>
internal sealed class GrabberSettingsMenu : IClickableMenu
{
    /*********
    ** Accessors
    *********/
    /// <summary>The grabber this page is configuring, so it can be harvested as soon as the page closes.</summary>
    public Object ConfiguredGrabber => this.Grabber;

    /*********
    ** Fields
    *********/
    private const int RowHeight = 56;
    private const int MenuWidth = 900;
    private const int MenuHeight = 700;

    /// <summary>The texture box drawn behind action buttons.</summary>
    private static readonly Rectangle ButtonSource = new(432, 439, 9, 9);

    private readonly Object Grabber;
    private readonly GrabberSettings Settings;
    private readonly ModConfig Config;

    private readonly ClickableComponent TargetsTab;
    private readonly ClickableComponent ScopeTab;
    private readonly ClickableComponent BehaviourTab;
    private readonly ClickableTextureComponent ScrollUp;
    private readonly ClickableTextureComponent ScrollDown;
    private readonly TextBox SearchBox;

    private readonly List<ListRow> Rows = new();
    private MenuTab Tab = MenuTab.Targets;
    private int ScrollIndex;
    private string LastSearch = "";

    /// <summary>Where the mouse is, for drawing hover states.</summary>
    private Point Hover;

    /// <summary>Where each visible dropdown row's closed box sits, by row index. Rebuilt as the list is drawn.</summary>
    private readonly Dictionary<int, Rectangle> DropdownBounds = new();

    /// <summary>The row index of the dropdown showing its options, or -1 when none is open.</summary>
    private int OpenDropdown = -1;

    /// <summary>The frequencies a grabber can be set to, in the order they're listed.</summary>
    private static readonly GrabFrequency[] Frequencies =
    {
        GrabFrequency.Default,
        GrabFrequency.TenMinutes,
        GrabFrequency.Hourly,
        GrabFrequency.FourHours,
        GrabFrequency.Daily
    };

    /// <summary>The replanting modes a grabber can be set to, in the order they're listed.</summary>
    private static readonly ReplantMode[] ReplantModes =
    {
        ReplantMode.Never,
        ReplantMode.MatchingSeed,
        ReplantMode.AnySeed
    };

    /// <summary>Whether the current tab has a list long enough to be worth searching.</summary>
    private bool HasSearchBox => this.Tab != MenuTab.Behaviour;

    /// <summary>How many rows fit in the list area.</summary>
    private int VisibleRows => (this.height - this.ListTop() + this.yPositionOnScreen - 80) / GrabberSettingsMenu.RowHeight;

    /// <summary>The tabs across the top of the menu, in the order they're drawn.</summary>
    private IEnumerable<(ClickableComponent Component, MenuTab Tab)> Tabs()
    {
        yield return (this.TargetsTab, MenuTab.Targets);
        yield return (this.ScopeTab, MenuTab.Scope);
        yield return (this.BehaviourTab, MenuTab.Behaviour);
    }

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
        this.ScopeTab = new ClickableComponent(new Rectangle(this.xPositionOnScreen + 32 + (tabWidth + 16), this.yPositionOnScreen + 72, tabWidth, 48), I18n.Menu_TabScope());
        this.BehaviourTab = new ClickableComponent(new Rectangle(this.xPositionOnScreen + 32 + 2 * (tabWidth + 16), this.yPositionOnScreen + 72, tabWidth, 48), I18n.Menu_TabBehaviour());

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
        // the open dropdown covers the rows beneath it, so it gets first refusal on the click
        if (this.OpenDropdown >= 0)
        {
            ListRow open = this.Rows[this.OpenDropdown];
            int row = this.OpenDropdown;
            this.OpenDropdown = -1;

            for (int i = 0; i < open.DropdownOptions!.Length; i++)
            {
                if (!this.DropdownOptionBounds(row, i).Contains(x, y))
                    continue;

                open.DropdownSelect(i);
                this.Settings.Save(this.Grabber);
                Game1.playSound("drumkit6");
                this.BuildRows();
                return;
            }

            Game1.playSound("smallSelect");
            return;
        }

        foreach ((int row, Rectangle bounds) in this.DropdownBounds)
        {
            if (bounds.Contains(x, y))
            {
                this.OpenDropdown = row;
                Game1.playSound("shwip");
                return;
            }
        }

        base.receiveLeftClick(x, y, playSound);

        foreach ((ClickableComponent component, MenuTab tab) in this.Tabs())
        {
            if (component.containsPoint(x, y) && this.Tab != tab)
            {
                this.SwitchTab(tab);
                return;
            }
        }

        if (this.HasSearchBox && this.SearchBox.Y <= y && y <= this.SearchBox.Y + 48 && this.SearchBox.X <= x && x <= this.SearchBox.X + this.SearchBox.Width)
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

            // clicking a dropdown's label opens it, not just the box on the right
            if (row.IsDropdown)
            {
                this.OpenDropdown = index;
                Game1.playSound("shwip");
                return;
            }

            row.Toggle();
            this.Settings.Save(this.Grabber);
            Game1.playSound("drumkit6");

            // headers show a live count, and the scope tab's list appears and disappears with the mode
            this.BuildRows();
        }
    }

    /// <inheritdoc />
    public override void performHoverAction(int x, int y)
    {
        base.performHoverAction(x, y);
        this.Hover = new Point(x, y);
    }

    /// <inheritdoc />
    public override void receiveScrollWheelAction(int direction)
    {
        this.OpenDropdown = -1;
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

        if (this.HasSearchBox && this.SearchBox.Text != this.LastSearch)
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

        foreach ((ClickableComponent component, MenuTab tab) in this.Tabs())
            this.DrawTab(b, component, this.Tab == tab);

        // the behaviour tab is two dropdowns, with nothing a search could narrow down
        if (this.HasSearchBox)
        {
            this.SearchBox.Draw(b);
            if (string.IsNullOrEmpty(this.SearchBox.Text) && !this.SearchBox.Selected)
                Utility.drawTextWithShadow(b, I18n.Menu_SearchHint(), Game1.smallFont, new Vector2(this.SearchBox.X + 16, this.SearchBox.Y + 12), Game1.textColor * 0.5f);
        }

        this.DrawRows(b);

        Utility.drawTextWithShadow(b, this.FooterText(), Game1.smallFont, new Vector2(this.xPositionOnScreen + 32, this.yPositionOnScreen + this.height - 68), Game1.textColor);

        if (this.OpenDropdown >= 0 && this.DropdownBounds.ContainsKey(this.OpenDropdown))
            this.DrawDropdownOptions(b);

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
    /// <summary>Get the top of the scrolling list area, which rises into the space a hidden search box leaves.</summary>
    private int ListTop() => this.yPositionOnScreen + (this.HasSearchBox ? 200 : 144);

    /// <summary>Get the row index under a screen position, or -1.</summary>
    private int RowIndexAt(int y)
    {
        int offset = y - this.ListTop();
        if (offset < 0)
            return -1;

        int row = offset / GrabberSettingsMenu.RowHeight;
        return row >= this.VisibleRows ? -1 : this.ScrollIndex + row;
    }

    private void SwitchTab(MenuTab tab)
    {
        this.OpenDropdown = -1;
        this.Tab = tab;
        this.ScrollIndex = 0;
        this.SearchBox.Text = "";
        this.LastSearch = "";
        this.SearchBox.Selected = false;
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
        this.OpenDropdown = -1;

        switch (this.Tab)
        {
            case MenuTab.Scope:
                this.BuildScopeRows();
                break;

            case MenuTab.Behaviour:
                this.BuildBehaviourRows();
                break;

            default:
                this.BuildTargetRows();
                break;
        }

        this.ScrollIndex = Math.Clamp(this.ScrollIndex, 0, Math.Max(0, this.Rows.Count - this.VisibleRows));
    }

    /// <summary>Build the target list, grouped into sections with a check-all row each.</summary>
    private void BuildTargetRows()
    {
        string search = this.SearchBox.Text?.Trim() ?? "";

        foreach (TargetGroup group in Enum.GetValues<TargetGroup>())
        {
            // Typing a group's name lists the whole group. Nothing in Animals is called "animal", so
            // matching only row names would leave the most obvious search anyone tries returning
            // nothing at all.
            bool groupMatches = search.Length > 0
                && GrabberSettingsMenu.GroupName(group).Contains(search, StringComparison.CurrentCultureIgnoreCase);

            List<HarvestTarget> matches = TargetCatalog.All
                .Where(target => target.Group == group)
                .Where(target => search.Length == 0 || groupMatches || target.DisplayName.Contains(search, StringComparison.CurrentCultureIgnoreCase))
                .ToList();

            if (matches.Count == 0)
                continue;

            int selected = matches.Count(target => this.Settings.TargetIds.Contains(target.Id));
            this.Rows.Add(new ListRow
            {
                Label = GrabberSettingsMenu.GroupName(group),
                IsHeader = true,
                Suffix = selected == matches.Count ? I18n.Menu_UncheckAll() : I18n.Menu_CheckAll(),
                SuffixIsButton = true,
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

        if (this.Settings.Scope != ScopeMode.Selected)
            return;

        string search = this.SearchBox.Text?.Trim() ?? "";

        // One row per key, not per location: every coop the player owns shares the "Coop" key, at any
        // tier, so the list stays as short as the farm looks rather than growing a row per building.
        List<IGrouping<string, GameLocation>> groups = GrabberSettings.AllLocations()
            .Where(location => !string.IsNullOrWhiteSpace(location.Name))
            .GroupBy(GrabberSettings.SelectionKey)
            .OrderBy(group => GrabberSettings.SelectionName(group.First()), StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        foreach (IGrouping<string, GameLocation> group in groups)
        {
            string key = group.Key;
            string name = GrabberSettings.SelectionName(group.First());
            bool visited = group.Any(GrabberSettings.HasVisited);
            int count = group.Count();

            if (search.Length > 0
                && !name.Contains(search, StringComparison.CurrentCultureIgnoreCase)
                && !key.Contains(search, StringComparison.CurrentCultureIgnoreCase))
            {
                continue;
            }

            // Several places share a display name -- the farm, the farmhouse and the cellar are all
            // "<name> Farm" -- so the key is shown alongside to tell them apart. Where the row stands for
            // more than one building, how many is the more useful thing to say.
            string suffix = count > 1 ? I18n.Scope_BuildingCount(count) : key;

            this.Rows.Add(new ListRow
            {
                Label = name,
                Suffix = visited ? suffix : $"{suffix} - {I18n.Scope_UnvisitedNote()}",
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

    /// <summary>Build the behaviour tab: how often the grabber runs, and what it replants.</summary>
    private void BuildBehaviourRows()
    {
        this.Rows.Add(new ListRow { Label = I18n.Menu_TabBehaviour(), IsHeader = true });

        this.Rows.Add(new ListRow
        {
            Label = I18n.Behaviour_Frequency(),
            DropdownOptions = GrabberSettingsMenu.Frequencies.Select(GrabberSettingsMenu.FrequencyName).ToArray(),
            DropdownSelected = () => Array.IndexOf(GrabberSettingsMenu.Frequencies, this.Settings.Frequency),
            DropdownSelect = index => this.Settings.Frequency = GrabberSettingsMenu.Frequencies[index]
        });

        this.Rows.Add(new ListRow
        {
            Label = I18n.Behaviour_Replant(),
            DropdownOptions = GrabberSettingsMenu.ReplantModes.Select(GrabberSettingsMenu.ReplantName).ToArray(),
            DropdownSelected = () => Array.IndexOf(GrabberSettingsMenu.ReplantModes, this.Settings.Replant),
            DropdownSelect = index => this.Settings.Replant = GrabberSettingsMenu.ReplantModes[index]
        });
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
        this.DropdownBounds.Clear();
        int y = this.ListTop();

        for (int i = this.ScrollIndex; i < this.Rows.Count && i < this.ScrollIndex + this.VisibleRows; i++)
        {
            ListRow row = this.Rows[i];
            int x = this.xPositionOnScreen + 40;
            float alpha = row.Greyed ? 0.4f : 1f;
            Rectangle rowBounds = new(x - 8, y, this.width - 96, GrabberSettingsMenu.RowHeight - 4);

            // anything that responds to a click lights up under the cursor
            bool interactive = !row.Greyed && (!row.IsHeader || row.SuffixIsButton || row.IsDropdown);
            if (interactive && rowBounds.Contains(this.Hover))
                b.Draw(Game1.staminaRect, rowBounds, Color.Wheat * 0.35f);

            if (row.IsHeader || row.IsDropdown)
            {
                Utility.drawTextWithShadow(b, row.Label, Game1.dialogueFont, new Vector2(x, y + 8), Game1.textColor * alpha, 0.75f);
            }
            else
            {
                b.Draw(Game1.mouseCursors, new Vector2(x, y + 12), row.IsChecked() ? OptionsCheckbox.sourceRectChecked : OptionsCheckbox.sourceRectUnchecked, Color.White * alpha, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.4f);

                int textX = x + 56;
                if (row.IconItemId != null)
                {
                    const float box = 40f;
                    ParsedItemData data = ItemRegistry.GetDataOrErrorItem(row.IconItemId);
                    Rectangle source = data.GetSourceRect();

                    // a keg's sprite is 16x32 where a parsnip's is 16x16, so each is scaled to fit the
                    // same square and centred in it rather than spilling into the rows above and below
                    float scale = box / Math.Max(source.Width, source.Height);
                    Vector2 position = new(
                        textX + (box - source.Width * scale) / 2f,
                        y + (GrabberSettingsMenu.RowHeight - 8 - source.Height * scale) / 2f);

                    b.Draw(data.GetTexture(), position, source, Color.White * alpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0.9f);
                    textX += 52;
                }

                Utility.drawTextWithShadow(b, row.Label, Game1.smallFont, new Vector2(textX, y + 14), Game1.textColor * alpha);
            }

            if (row.IsDropdown)
                this.DrawDropdown(b, row, i, y);
            else if (row.Suffix != null)
                this.DrawSuffix(b, row, y);

            y += GrabberSettingsMenu.RowHeight;
        }
    }

    /// <summary>Draw a row's trailing text, as a button when it's an action.</summary>
    private void DrawSuffix(SpriteBatch b, ListRow row, int y)
    {
        Vector2 size = Game1.smallFont.MeasureString(row.Suffix);
        float right = this.xPositionOnScreen + this.width - 80;

        if (!row.SuffixIsButton)
        {
            Utility.drawTextWithShadow(b, row.Suffix, Game1.smallFont, new Vector2(right - size.X, y + 14), Game1.textColor * 0.7f);
            return;
        }

        Rectangle bounds = new((int)(right - size.X - 24), y + 4, (int)size.X + 32, GrabberSettingsMenu.RowHeight - 16);
        IClickableMenu.drawTextureBox(b, Game1.mouseCursors, GrabberSettingsMenu.ButtonSource, bounds.X, bounds.Y, bounds.Width, bounds.Height, Color.White, 4f, drawShadow: false);
        Utility.drawTextWithShadow(b, row.Suffix, Game1.smallFont, new Vector2(bounds.X + 16, bounds.Y + 6), Game1.textColor);
    }

    /// <summary>Draw a closed dropdown, and remember where it is so clicks can find it.</summary>
    /// <param name="b">The sprite batch being drawn to.</param>
    /// <param name="row">The row being drawn.</param>
    /// <param name="index">The row's index in the list, which is what its bounds are filed under.</param>
    /// <param name="y">The top of the row on screen.</param>
    private void DrawDropdown(SpriteBatch b, ListRow row, int index, int y)
    {
        // wide enough for the longest option any dropdown offers, which is a replanting mode
        int width = 360;
        Rectangle bounds = new(this.xPositionOnScreen + this.width - width - 72, y + 4, width, GrabberSettingsMenu.RowHeight - 16);
        this.DropdownBounds[index] = bounds;

        IClickableMenu.drawTextureBox(b, Game1.mouseCursors, OptionsDropDown.dropDownBGSource, bounds.X, bounds.Y, bounds.Width - 48, bounds.Height, Color.White, 4f, drawShadow: false);
        b.DrawString(Game1.smallFont, row.DropdownOptions![row.DropdownSelected()], new Vector2(bounds.X + 8, bounds.Y + 6), Game1.textColor);
        b.Draw(Game1.mouseCursors, new Vector2(bounds.Right - 48, bounds.Y), OptionsDropDown.dropDownButtonSource, Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.88f);
    }

    /// <summary>Draw the open dropdown's options over everything else.</summary>
    private void DrawDropdownOptions(SpriteBatch b)
    {
        ListRow row = this.Rows[this.OpenDropdown];
        string[] options = row.DropdownOptions!;
        Rectangle bounds = this.DropdownBounds[this.OpenDropdown];

        Rectangle panel = new(bounds.X, bounds.Y, bounds.Width - 48, bounds.Height * options.Length);
        IClickableMenu.drawTextureBox(b, Game1.mouseCursors, OptionsDropDown.dropDownBGSource, panel.X, panel.Y, panel.Width, panel.Height, Color.White, 4f, drawShadow: false, 0.97f);

        for (int i = 0; i < options.Length; i++)
        {
            Rectangle option = this.DropdownOptionBounds(this.OpenDropdown, i);
            bool highlight = i == row.DropdownSelected() || option.Contains(this.Hover);
            if (highlight)
                b.Draw(Game1.staminaRect, new Rectangle(option.X, option.Y, panel.Width, option.Height), new Rectangle(0, 0, 1, 1), Color.Wheat, 0f, Vector2.Zero, SpriteEffects.None, 0.975f);

            b.DrawString(Game1.smallFont, options[i], new Vector2(option.X + 8, option.Y + 6), Game1.textColor, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0.98f);
        }

        b.Draw(Game1.mouseCursors, new Vector2(bounds.Right - 48, bounds.Y), OptionsDropDown.dropDownButtonSource, Color.Wheat, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.981f);
    }

    /// <summary>Get where one of an open dropdown's options sits.</summary>
    /// <param name="row">The dropdown row's index in the list.</param>
    /// <param name="index">The option's index within the dropdown.</param>
    private Rectangle DropdownOptionBounds(int row, int index)
    {
        Rectangle bounds = this.DropdownBounds[row];
        return new Rectangle(bounds.X, bounds.Y + index * bounds.Height, bounds.Width - 48, bounds.Height);
    }

    private void DrawTab(SpriteBatch b, ClickableComponent tab, bool active)
    {
        IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(active ? 0 : 64, 256, 60, 60), tab.bounds.X, tab.bounds.Y, tab.bounds.Width, tab.bounds.Height, Color.White);
        if (!active && tab.containsPoint(this.Hover.X, this.Hover.Y))
            b.Draw(Game1.staminaRect, tab.bounds, Color.Wheat * 0.3f);
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
            TargetGroup.Digging => I18n.Group_Digging(),
            TargetGroup.Trees => I18n.Group_Trees(),
            TargetGroup.TrashCans => I18n.Group_TrashCans(),
            TargetGroup.Animals => I18n.Group_Animals(),
            _ => I18n.Group_Machines()
        };
    }

    private static string ReplantName(ReplantMode mode)
    {
        return mode switch
        {
            ReplantMode.MatchingSeed => I18n.Replant_MatchingSeed(),
            ReplantMode.AnySeed => I18n.Replant_AnySeed(),
            _ => I18n.Replant_Never()
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

}
