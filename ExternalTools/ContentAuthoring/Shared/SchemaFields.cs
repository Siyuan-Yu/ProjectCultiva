namespace ContentAuthoring.Shared;

public static class SchemaFields
{
    public static readonly Dictionary<string, HashSet<string>> TypeFields = new(StringComparer.Ordinal)
    {
        ["character"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "displayNameKey", "nameKey", "baseAttributes", "tags",
            "personalityTags", "backgroundTags", "talentTags",
            "spiritRootPlaceholder", "initialRealmPlaceholder",
            "activityCapabilities", "activityPriorities", "preferredWorkAreaIds", "homeWorkAreaId",
            "spiritRoots", "hometown", "reputation", "goals", "desires", "playerControllable"
        },
        ["cultivation"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "displayNameKey", "nameKey", "requiredRealm",
            "grade", "effectSummary",
            "cultivationSpeed", "breakthroughProgress", "grantedModifiers", "mastery", "tags"
        },
        ["combatArt"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "grade", "effectSummary",
            "attackBonusPercent", "damageFlat", "damageAttackMult", "hitCount", "cooldownSeconds",
            "mastery", "tags"
        },
        ["item"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "displayNameKey", "nameKey", "maxStack",
            "teachesManualId", "teachesArtId", "tags"
        },
        ["opportunitySite"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "nameKey", "description", "allowsCultivation", "offeredManualId", "tags"
        },
        ["openingScenario"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "scheduleId", "openingFactionId",
            "openingWorldRegionId", "openingChapterId", "spawns", "openingRelations"
        },
        ["characterRoster"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "entries"
        },
        ["resource"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "nameKey", "tags"
        },
        ["worldSiteEconomy"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "siteId", "initialPublicStock"
        },
        ["worldRegion"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "startLocationId", "locations"
        },
        ["localPlaceSet"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "mapLayoutId", "startLocationId", "locations"
        },
        ["quest"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "description", "autoOffer", "abandonable", "deadlineDays",
            "runtimeMode", "acceptanceMode", "deliveryRequirements", "questKind",
            "offerConditions", "completeConditions", "failConditions", "rewards", "failResults"
        },
        ["contentEvent"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "body", "trigger", "locationId", "questId", "once",
            "conditions", "choices", "npcDefinitionId", "npcTags", "worldOpportunityId", "worldObjectKind", "worldObjectId",
            "priority", "topicText", "onceScope", "entryStepId", "steps"
        },
        ["chapter"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "description", "openingScenarioId", "plannedDays",
            "questChainIds", "eventChainIds", "dayBeats"
        },
        ["workArea"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "locationId", "tags", "allowedActivities", "offsetX", "offsetZ", "capacity",
            "residentTags", "isControlCore", "maxDurability", "defense", "occupyHoldSeconds", "grantsPrivileges"
        },
        ["job"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "primaryWorkAreaId", "activityBindings"
        },
        ["schedule"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "blocks"
        },
        ["mapLayout"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "worldRegionId",
            "originX", "originY", "cellSize", "width", "height", "placements"
        },
        ["spawnTable"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "entries"
        },
        ["worldOpportunityDirector"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "surfaceId", "targetActiveMin", "targetActiveMax"
        },
        ["worldOpportunity"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "surfaceId", "weight", "maxActive", "spawnKind", "spawnTableId", "durationDays",
            "minPlayerDistanceWorld", "maxPlayerDistanceWorld", "allowInsideWorldSite", "discoveryMode",
            "worldObjectKind", "worldObjectLabel", "worldObjectWorldWidth", "worldObjectWorldHeight",
            "publicNoticeTitle", "publicNoticeText", "publicNoticeRevealExactLocation",
            "discoveryRadiusWorld", "discoveryNoticeTitle", "discoveryNoticeText", "conditions", "expireOutcomes"
        }
    };

    public static readonly HashSet<string> MapPlacementFields = new(StringComparer.Ordinal)
    {
        "id", "kind", "x", "y", "w", "h", "blocksMovement", "boundLocationId", "label", "lootItemId"
    };

    public static readonly HashSet<string> LocationFields = new(StringComparer.Ordinal)
    {
        "id", "name", "kind", "adjacentIds", "resourceOnExploreId", "resourceOnExploreAmount",
        "opportunitySiteId", "residentNpcDefinitionId", "presentationX", "presentationZ",
        "enterConditions", "questOfferIds", "tags", "allowedActivities",
        "localMapId", "enterLocalMapId", "enterSpawnLocationId", "surveySenseRequired"
    };

    public static readonly string[] ConditionKinds =
    [
        "storyFlag", "hasFlag", "missingFlag", "exploredLocation", "atLocation",
        "stockAtLeast", "realmAtLeast", "hasManual", "knowsSite", "questActive", "questCompleted",
        "laborAtLocation", "characterAtLocation", "uniqueLaborAtLocation", "uniqueHarvestAtLocation",
        "counterAtLeast", "missingDailyFlag", "hasDailyFlag", "encounterCleared"
        , "questOfferableFromTarget", "questActiveFromTarget", "questHandedInFromTarget",
        "questDeliveryAvailableFromTarget", "questReadyToClaimFromTarget",
        "questCompletedFromTarget", "questFailedFromTarget", "affectionAtLeast"
    ];

    public static readonly string[] OutcomeKinds =
    [
        "setFlag", "clearFlag", "addStock", "removeStock", "grantProgress", "discoverSite",
        "relationDelta", "startQuest", "acceptQuestFromTarget", "deliverQuestToTarget",
        "addCounter", "setCounter", "setDailyFlag", "clearDailyFlag", "learnManual", "setEncounterCleared", "startMinigame",
        "resolveCurrentOpportunity"
    ];

    public static readonly string[] EventTriggers =
    [
        "manual", "onArrive", "onExplore", "onQuestCompleted", "onQuestFailed"
    ];
}
