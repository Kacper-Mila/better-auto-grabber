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

    /// <summary>Get the name this mod gives a location, or <c>null</c> if it doesn't name that one.</summary>
    /// <param name="internalName">The location's internal name, like <c>BugLand</c>.</param>
    /// <remarks>
    ///   Unlike every other string here this one is looked up rather than named, because the set of
    ///   locations worth naming is a list rather than a handful: a typed accessor each would be thirty
    ///   near-identical lines, and a translator adding a name would then have to edit code as well.
    ///   A missing key is an answer, not a failure, which is why this reads <see cref="Translation.HasValue" />
    ///   instead of the string -- SMAPI hands back a "(missing translation)" placeholder otherwise.
    /// </remarks>
    public static string? Location(string internalName)
    {
        Translation? translation = I18n.Translations?.Get("location." + internalName);
        return translation?.HasValue() == true
            ? translation.ToString()
            : null;
    }

    public static string Target_EverythingElse() => I18n.Get("target.everything-else");
    public static string Target_EverythingElseTooltip() => I18n.Get("target.everything-else.tooltip");
    public static string Target_LargeStump() => I18n.Get("target.large-stump");
    public static string Target_LargeLog() => I18n.Get("target.large-log");
    public static string Target_Boulder() => I18n.Get("target.boulder");
    public static string Target_Meteorite() => I18n.Get("target.meteorite");
    public static string Target_MineBoulder() => I18n.Get("target.mine-boulder");
    public static string Target_ArtifactSpot() => I18n.Get("target.artifact-spot");
    public static string Target_SeedSpot() => I18n.Get("target.seed-spot");
    public static string Target_PanningSpot() => I18n.Get("target.panning-spot");
    public static string Target_ShakeTrees() => I18n.Get("target.shake-trees");
    public static string Target_HarvestMoss() => I18n.Get("target.harvest-moss");
    public static string Target_SlimeBall() => I18n.Get("target.slime-ball");
    public static string Target_TrashCan() => I18n.Get("target.trash-can");

    public static string Group_Forage() => I18n.Get("group.forage");
    public static string Group_Crops() => I18n.Get("group.crops");
    public static string Group_FruitTrees() => I18n.Get("group.fruit-trees");
    public static string Group_Bushes() => I18n.Get("group.bushes");
    public static string Group_Clumps() => I18n.Get("group.clumps");
    public static string Group_Digging() => I18n.Get("group.digging");
    public static string Group_Trees() => I18n.Get("group.trees");
    public static string Group_TrashCans() => I18n.Get("group.trash-cans");
    public static string Group_Animals() => I18n.Get("group.animals");
    public static string Group_Machines() => I18n.Get("group.machines");

    public static string Menu_Title() => I18n.Get("menu.title");
    public static string Menu_TabTargets() => I18n.Get("menu.tab.targets");
    public static string Menu_TabScope() => I18n.Get("menu.tab.scope");
    public static string Menu_TabBehaviour() => I18n.Get("menu.tab.behaviour");
    public static string Menu_SearchHint() => I18n.Get("menu.search-hint");
    public static string Menu_CheckAll() => I18n.Get("menu.check-all");
    public static string Menu_UncheckAll() => I18n.Get("menu.uncheck-all");
    public static string Menu_NoResults() => I18n.Get("menu.no-results");
    public static string Menu_SettingsTooltip() => I18n.Get("menu.settings-tooltip");
    public static string Menu_NothingSelected() => I18n.Get("menu.nothing-selected");
    public static string Menu_SelectedCount(int count) => I18n.Get("menu.selected-count", new { count });
    public static string Menu_SelectedCountWithGroups(int count, int groups) => I18n.Get("menu.selected-count-groups", new { count, groups });
    public static string Menu_SelectedGroups(int groups) => I18n.Get("menu.selected-groups", new { groups });
    public static string Menu_Everything() => I18n.Get("menu.everything");
    public static string Menu_AllBut(int count) => I18n.Get("menu.all-but", new { count });

    public static string Scope_Local() => I18n.Get("scope.local");
    public static string Scope_LocalDesc(string location) => I18n.Get("scope.local.desc", new { location });
    public static string Scope_Global() => I18n.Get("scope.global");
    public static string Scope_GlobalDesc() => I18n.Get("scope.global.desc");
    public static string Scope_Selected() => I18n.Get("scope.selected");
    public static string Scope_SelectedDesc() => I18n.Get("scope.selected.desc");
    public static string Scope_UnvisitedNote() => I18n.Get("scope.unvisited-note");
    public static string Scope_BuildingCount(int count) => I18n.Get("scope.building-count", new { count });

    public static string Behaviour_Frequency() => I18n.Get("behaviour.frequency");
    public static string Behaviour_Replant() => I18n.Get("behaviour.replant");

    public static string Replant_Never() => I18n.Get("replant.never");
    public static string Replant_MatchingSeed() => I18n.Get("replant.matching-seed");
    public static string Replant_AnySeed() => I18n.Get("replant.any-seed");

    public static string Frequency_Default() => I18n.Get("frequency.default");
    public static string Frequency_TenMinutes() => I18n.Get("frequency.ten-minutes");
    public static string Frequency_Hourly() => I18n.Get("frequency.hourly");
    public static string Frequency_FourHours() => I18n.Get("frequency.four-hours");
    public static string Frequency_Daily() => I18n.Get("frequency.daily");

    public static string Summary_Header() => I18n.Get("summary.header");
    public static string Summary_Line(string location, int count) => I18n.Get("summary.line", new { location, count });
    public static string Summary_Full(string location) => I18n.Get("summary.full", new { location });
}
