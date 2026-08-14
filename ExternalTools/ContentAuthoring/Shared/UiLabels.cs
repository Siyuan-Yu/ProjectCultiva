namespace ContentAuthoring.Shared;

/// <summary>
/// Editor UI labels for fixed enum-like options. Disk JSON always stores <see cref="Key"/>.
/// Showing Chinese in UI is safe as long as save paths call <see cref="ToKey"/>.
/// </summary>
public static class UiLabels
{
    public readonly record struct Option(string Key, string Label);

    public static readonly Option[] EventTriggers =
    [
        new("manual", "手动"),
        new("onArrive", "抵达地点时"),
        new("onExplore", "探索时"),
        new("onQuestCompleted", "任务完成时"),
        new("onQuestFailed", "任务失败时")
    ];

    public static readonly Option[] JobBindingModes =
    [
        new("single", "定点"),
        new("route", "轮巡")
    ];

    public static readonly Option[] LocationKinds =
    [
        new("Settlement", "据点"),
        new("Village", "村落"),
        new("Wild", "野外"),
        new("Opportunity", "机缘"),
        new("None", "无")
    ];

    public static readonly Option[] ScheduleActivities =
    [
        new("Labor", "劳作"),
        new("Rest", "休息"),
        new("Eat", "吃饭"),
        new("Cultivate", "修炼"),
        new("Explore", "探索"),
        new("Patrol", "巡逻"),
        new("Inspect", "巡查")
    ];

    /// <summary>2B spirit-root axes; values 0–30 on character.spiritRoots.</summary>
    public static readonly Option[] SpiritRoots =
    [
        new("Fire", "火"),
        new("Metal", "金"),
        new("Earth", "土"),
        new("Wood", "木"),
        new("Thunder", "雷"),
        new("Wind", "风"),
        new("Ice", "冰"),
        new("Poison", "毒")
    ];

    public static readonly Option[] FactionRoles =
    [
        new("LaborDisciple", "劳役弟子"),
        new("Member", "成员"),
        new("Supervisor", "主管")
    ];

    public static readonly Option[] AiRoles =
    [
        new("Mortal", "凡人倾向"),
        new("Cultivator", "修士倾向"),
        new("Supervisor", "主管倾向")
    ];

    public static string ToLabel(IReadOnlyList<Option> options, string? key, string fallbackLabel)
    {
        if (string.IsNullOrWhiteSpace(key)) return fallbackLabel;
        foreach (var o in options)
        {
            if (string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase))
                return o.Label;
            if (string.Equals(o.Label, key, StringComparison.Ordinal))
                return o.Label;
        }

        return key;
    }

    public static string ToKey(IReadOnlyList<Option> options, string? labelOrKey, string fallbackKey)
    {
        if (string.IsNullOrWhiteSpace(labelOrKey)) return fallbackKey;
        foreach (var o in options)
        {
            if (string.Equals(o.Label, labelOrKey, StringComparison.Ordinal))
                return o.Key;
            if (string.Equals(o.Key, labelOrKey, StringComparison.OrdinalIgnoreCase))
                return o.Key;
        }

        return labelOrKey.Trim();
    }

    public static string[] Labels(IReadOnlyList<Option> options) =>
        options.Select(o => o.Label).ToArray();

    /// <summary>Comma-separated activity keys ↔ Chinese labels for Region allowedActivities.</summary>
    public static string ActivitiesCsvToDisplay(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return "";
        return string.Join(", ",
            csv.Split([',', '，', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(p => ToLabel(ScheduleActivities, p, p)));
    }

    public static string ActivitiesCsvToKeys(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return "";
        return string.Join(", ",
            csv.Split([',', '，', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(p => ToKey(ScheduleActivities, p, p)));
    }
}
