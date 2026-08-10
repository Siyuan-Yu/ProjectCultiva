namespace ContentAuthoring.Shared;

public static class SchemaFields
{
    public static readonly Dictionary<string, HashSet<string>> TypeFields = new(StringComparer.Ordinal)
    {
        ["character"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "displayNameKey", "nameKey", "baseAttributes", "tags",
            "personalityTags", "backgroundTags", "talentTags",
            "spiritRootPlaceholder", "initialRealmPlaceholder"
        },
        ["cultivation"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "displayNameKey", "nameKey", "requiredRealm",
            "cultivationSpeed", "breakthroughProgress", "grantedModifiers", "tags"
        },
        ["item"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "displayNameKey", "nameKey", "maxStack", "tags"
        },
        ["opportunitySite"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "nameKey", "description", "allowsCultivation", "offeredManualId", "tags"
        },
        ["openingScenario"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "scheduleId", "openingFactionId", "openingSettlementId",
            "openingWorldRegionId", "openingChapterId", "spawns", "openingRelations"
        },
        ["resource"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "nameKey", "tags"
        },
        ["facility"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "laborResourceId", "laborAmountPerWorker",
            "gatherResourceId", "gatherAmountPerWorker", "cultivateProgressBonusPerWorker", "tags"
        },
        ["settlement"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "initialStock", "facilities"
        },
        ["worldRegion"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "startLocationId", "locations"
        },
        ["quest"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "description", "autoOffer",
            "offerConditions", "completeConditions", "failConditions", "rewards", "failResults"
        },
        ["contentEvent"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "body", "trigger", "locationId", "questId", "once",
            "conditions", "choices"
        },
        ["chapter"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "description", "openingScenarioId", "plannedDays",
            "questChainIds", "eventChainIds", "dayBeats"
        },
        ["workArea"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "locationId", "tags", "allowedActivities", "offsetX", "offsetZ"
        },
        ["job"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "primaryWorkAreaId", "activityBindings"
        }
    };

    public static readonly HashSet<string> LocationFields = new(StringComparer.Ordinal)
    {
        "id", "name", "kind", "adjacentIds", "resourceOnExploreId", "resourceOnExploreAmount",
        "opportunitySiteId", "residentNpcDefinitionId", "presentationX", "presentationZ",
        "enterConditions", "questOfferIds", "tags", "allowedActivities"
    };

    public static readonly string[] ConditionKinds =
    [
        "storyFlag", "hasFlag", "missingFlag", "exploredLocation", "atLocation",
        "stockAtLeast", "realmAtLeast", "hasManual", "knowsSite", "questActive", "questCompleted"
    ];

    public static readonly string[] OutcomeKinds =
    [
        "setFlag", "clearFlag", "addStock", "grantProgress", "discoverSite",
        "relationDelta", "startQuest"
    ];

    public static readonly string[] EventTriggers =
    [
        "manual", "onArrive", "onExplore", "onQuestCompleted", "onQuestFailed"
    ];
}
