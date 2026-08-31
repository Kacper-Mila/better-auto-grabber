using StardewModdingAPI;

namespace BetterAutoGrabber.Framework;

/// <summary>Typed access to the mod's translations.</summary>
internal static class I18n
{
    private static ITranslationHelper? Translations;

    /// <summary>Point the helper at the mod's translation files.</summary>
    public static void Init(ITranslationHelper translations) => I18n.Translations = translations;

    private static string Get(string key, object? tokens = null)
    {
        return I18n.Translations?.Get(key, tokens) ?? key;
    }

    public static string Target_OtherForage() => I18n.Get("target.other-forage");
    public static string Target_LargeStump() => I18n.Get("target.large-stump");
    public static string Target_LargeLog() => I18n.Get("target.large-log");
    public static string Target_Boulder() => I18n.Get("target.boulder");
    public static string Target_Meteorite() => I18n.Get("target.meteorite");
    public static string Target_MineBoulder() => I18n.Get("target.mine-boulder");
    public static string Target_ArtifactSpot() => I18n.Get("target.artifact-spot");
    public static string Target_SeedSpot() => I18n.Get("target.seed-spot");
    public static string Target_ShakeTrees() => I18n.Get("target.shake-trees");

    public static string Group_Forage() => I18n.Get("group.forage");
    public static string Group_Crops() => I18n.Get("group.crops");
    public static string Group_FruitTrees() => I18n.Get("group.fruit-trees");
    public static string Group_Bushes() => I18n.Get("group.bushes");
    public static string Group_Clumps() => I18n.Get("group.clumps");
    public static string Group_Digging() => I18n.Get("group.digging");
    public static string Group_Trees() => I18n.Get("group.trees");
    public static string Group_Machines() => I18n.Get("group.machines");

    public static string Menu_Title() => I18n.Get("menu.title");
    public static string Menu_TabTargets() => I18n.Get("menu.tab.targets");
    public static string Menu_TabScope() => I18n.Get("menu.tab.scope");
    public static string Menu_SearchHint() => I18n.Get("menu.search-hint");
    public static string Menu_CheckAll() => I18n.Get("menu.check-all");
    public static string Menu_UncheckAll() => I18n.Get("menu.uncheck-all");
    public static string Menu_NoResults() => I18n.Get("menu.no-results");
    public static string Menu_SettingsTooltip() => I18n.Get("menu.settings-tooltip");
    public static string Menu_NothingSelected() => I18n.Get("menu.nothing-selected");
    public static string Menu_SelectedCount(int count) => I18n.Get("menu.selected-count", new { count });

    public static string Scope_Local() => I18n.Get("scope.local");
    public static string Scope_LocalDesc(string location) => I18n.Get("scope.local.desc", new { location });
    public static string Scope_Global() => I18n.Get("scope.global");
    public static string Scope_GlobalDesc() => I18n.Get("scope.global.desc");
    public static string Scope_Selected() => I18n.Get("scope.selected");
    public static string Scope_SelectedDesc() => I18n.Get("scope.selected.desc");
    public static string Scope_Frequency() => I18n.Get("scope.frequency");
    public static string Scope_UnvisitedNote() => I18n.Get("scope.unvisited-note");

    public static string Frequency_Default() => I18n.Get("frequency.default");
    public static string Frequency_TenMinutes() => I18n.Get("frequency.ten-minutes");
    public static string Frequency_Hourly() => I18n.Get("frequency.hourly");
    public static string Frequency_FourHours() => I18n.Get("frequency.four-hours");
    public static string Frequency_Daily() => I18n.Get("frequency.daily");

    public static string Summary_Header() => I18n.Get("summary.header");
    public static string Summary_Line(string location, int count) => I18n.Get("summary.line", new { location, count });
    public static string Summary_Full(string location) => I18n.Get("summary.full", new { location });
}
