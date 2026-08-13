namespace ContentAuthoring.Shared;

public enum JsonArrayEditorMode
{
    Condition,
    Outcome
}

public sealed class FieldSpec
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public FieldEditorKind Editor { get; init; } = FieldEditorKind.Text;
    public string[]? Options { get; init; }
}

public enum FieldEditorKind
{
    Text,
    Number,
    Location,
    Quest,
    Character,
    Resource,
    Site,
    Manual,
    Realm
}

public static class ContentFieldCatalog
{
    public static readonly (string Kind, string Label)[] ConditionKinds =
    [
        ("storyFlag", "已有 Flag"),
        ("missingFlag", "缺少 Flag"),
        ("exploredLocation", "已探索地点"),
        ("atLocation", "当前在地点"),
        ("stockAtLeast", "库存 ≥"),
        ("realmAtLeast", "境界 ≥"),
        ("questCompleted", "任务已完成"),
        ("questActive", "任务进行中"),
        ("knowsSite", "已知机缘点"),
        ("hasManual", "已学功法")
    ];

    public static readonly (string Kind, string Label)[] OutcomeKinds =
    [
        ("setFlag", "设置 Flag"),
        ("clearFlag", "清除 Flag"),
        ("addStock", "增加库存"),
        ("startQuest", "开始任务"),
        ("discoverSite", "发现机缘点"),
        ("grantProgress", "修炼进度"),
        ("relationDelta", "关系变化")
    ];

    public static readonly string[] RealmOptions = ["凡人", "炼气"];

    public static IReadOnlyList<FieldSpec> FieldsForCondition(string kind) => kind switch
    {
        "atLocation" or "exploredLocation" =>
        [
            new FieldSpec { Key = "id", Label = "地点", Editor = FieldEditorKind.Location }
        ],
        "stockAtLeast" =>
        [
            new FieldSpec { Key = "id", Label = "资源", Editor = FieldEditorKind.Resource },
            new FieldSpec { Key = "amount", Label = "数量", Editor = FieldEditorKind.Number }
        ],
        "realmAtLeast" =>
        [
            new FieldSpec { Key = "realm", Label = "境界", Editor = FieldEditorKind.Realm, Options = RealmOptions }
        ],
        "questCompleted" or "questActive" =>
        [
            new FieldSpec { Key = "id", Label = "任务", Editor = FieldEditorKind.Quest }
        ],
        "knowsSite" =>
        [
            new FieldSpec { Key = "id", Label = "机缘点", Editor = FieldEditorKind.Site }
        ],
        "hasManual" =>
        [
            new FieldSpec { Key = "id", Label = "功法", Editor = FieldEditorKind.Manual }
        ],
        _ => [new FieldSpec { Key = "id", Label = "Flag / Id", Editor = FieldEditorKind.Text }]
    };

    public static IReadOnlyList<FieldSpec> FieldsForOutcome(string kind) => kind switch
    {
        "addStock" =>
        [
            new FieldSpec { Key = "id", Label = "资源", Editor = FieldEditorKind.Resource },
            new FieldSpec { Key = "amount", Label = "数量", Editor = FieldEditorKind.Number }
        ],
        "startQuest" =>
        [
            new FieldSpec { Key = "id", Label = "任务", Editor = FieldEditorKind.Quest }
        ],
        "discoverSite" =>
        [
            new FieldSpec { Key = "id", Label = "机缘点", Editor = FieldEditorKind.Site }
        ],
        "grantProgress" =>
        [
            new FieldSpec { Key = "amount", Label = "进度值", Editor = FieldEditorKind.Number }
        ],
        "relationDelta" =>
        [
            new FieldSpec { Key = "fromDefinitionId", Label = "来自角色", Editor = FieldEditorKind.Character },
            new FieldSpec { Key = "toDefinitionId", Label = "目标角色", Editor = FieldEditorKind.Character },
            new FieldSpec { Key = "amount", Label = "变化值", Editor = FieldEditorKind.Number }
        ],
        _ => [new FieldSpec { Key = "id", Label = "Flag / Id", Editor = FieldEditorKind.Text }]
    };
}
