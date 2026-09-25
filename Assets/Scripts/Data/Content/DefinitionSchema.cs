using System;
using System.Collections.Generic;
using XianXia.Core.Attributes;
using XianXia.Core.Results;
using XianXia.Data.Serialization;

namespace XianXia.Data.Content
{
    /// <summary>
    /// Strict JSON field allow-lists for Data Pipeline M1-A.
    /// </summary>
    public static class DefinitionSchema
    {
        public static readonly HashSet<string> CharacterFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "displayNameKey", "nameKey", "baseAttributes", "tags",
            "personalityTags", "backgroundTags", "talentTags",
            "spiritRootPlaceholder", "spiritRoots", "initialRealmPlaceholder",
            "hometown", "reputation", "goals", "desires",
            "playerControllable", "activityCapabilities", "activityPriorities", "preferredWorkAreaIds",
            "tradeProviderShopId", "homeWorkAreaId", "defeatEncounterId", "defaultFactionId", "defaultFactionRole"
        };

        public static readonly HashSet<string> OpeningScenarioFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "scheduleId", "openingFactionId",
            "openingWorldRegionId", "openingLocalPlaceSetId", "openingSurfaceId", "openingChapterId", "spawns", "openingRelations", "openingBonds",
            "initialNpcSquadIds", "strategicOpening", "startingInventory", "startingWallet"
        };

        public static readonly HashSet<string> NpcSquadFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "squadId", "name", "factionId", "assemblySiteId",
            "initialSurfacePosition", "initialSurfaceDeployment", "members"
        };
        public static readonly HashSet<string> NpcSquadInitialSurfacePositionFields = new HashSet<string>(StringComparer.Ordinal)
        { "surfaceId", "worldX", "worldY" };
        public static readonly HashSet<string> NpcSquadInitialSurfaceDeploymentFields = new HashSet<string>(StringComparer.Ordinal)
        { "surfaceId", "anchorSiteId", "offsetCellsX", "offsetCellsY" };
        public static readonly HashSet<string> NpcSquadMemberFields = new HashSet<string>(StringComparer.Ordinal)
        { "characterDefinitionId", "displayName", "leader", "reuseOpeningSpawn" };

        public static readonly HashSet<string> StrategicFactionFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "mapColor", "territorySelectable", "sortOrder"
        };

        public static readonly HashSet<string> CharacterRosterFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "entries"
        };

        public static readonly HashSet<string> ChapterFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "description", "openingScenarioId", "plannedDays",
            "questChainIds", "eventChainIds", "dayBeats"
        };

        public static readonly HashSet<string> ChapterDayBeatFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "dayIndex", "conditions", "questOfferIds", "contentEventIds", "setFlags"
        };

        public static readonly HashSet<string> WorldRegionFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "startLocationId", "locations"
        };

        public static readonly HashSet<string> LocalPlaceSetFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "mapLayoutId", "startLocationId", "locations"
        };

        public static readonly HashSet<string> WorldLocationFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "name", "kind", "adjacentIds", "resourceOnExploreId", "resourceOnExploreAmount",
            "opportunitySiteId", "residentNpcDefinitionId", "presentationX", "presentationZ",
            "enterConditions", "questOfferIds", "tags", "allowedActivities",
            "localMapId", "enterLocalMapId", "enterSpawnLocationId", "surveySenseRequired"
        };

        public static readonly HashSet<string> WorkAreaFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "locationId", "tags", "allowedActivities", "offsetX", "offsetZ", "capacity",
            "residentTags", "isControlCore", "maxDurability", "defense", "occupyHoldSeconds", "grantsPrivileges"
        };

        public static readonly HashSet<string> JobFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "primaryWorkAreaId", "activityBindings"
        };

        public static readonly HashSet<string> JobActivityBindingFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "activity", "workAreaIds", "mode"
        };

        public static readonly HashSet<string> ScheduleFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "blocks"
        };

        public static readonly HashSet<string> ScheduleBlockFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "startTick", "endTick", "activity", "orderDurationTicks"
        };

        public static readonly HashSet<string> MapLayoutFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "worldRegionId",
            "originX", "originY", "cellSize", "width", "height", "exitTriggerDepth", "spaceKind", "placements"
        };

        public static readonly HashSet<string> OutdoorSurfaceFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "originWorldX", "originWorldY", "movementScale", "cellSize", "chunkWidth", "chunkHeight", "acceptanceOnly", "chunks",
            "siteRegions", "factionFlags", "sitePlacements", "sitePlaces", "openingEntityAnchors"
        };
        public static readonly HashSet<string> WorldSpatialRulesFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "coreLevels",
            "wildernessEncounterWidthCells", "wildernessEncounterHeightCells",
            "wildernessEncounterWidthWorld", "wildernessEncounterHeightWorld",
            "interventionDecisionSeconds", "interventionArrivalSeconds",
            "interventionRelationThreshold", "interventionChanceBasisPoints"
        };
        public static readonly HashSet<string> CoreLevelControlRangeFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "level", "controlWidthCells", "controlHeightCells", "controlWidthWorld", "controlHeightWorld"
        };
        public static readonly HashSet<string> OpeningEntityAnchorFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "siteId", "spawnKey", "definitionId", "sourceLocationId", "worldX", "worldY"
        };
        public static readonly HashSet<string> OutdoorSurfaceChunkFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "x", "y", "sourceMapLayoutId" // accepted from older packages, ignored by runtime
        };

        public static readonly HashSet<string> OutdoorSurfaceGeographyFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "surfaceId", "sourceSchemaVersion", "sourceRevision", "sourceHash",
            "originWorldX", "originWorldY", "cellSize", "width", "height", "coverageChunks", "chunkRows", "rows",
            "mapPrimitives", "landmarks", "hexSummary"
        };
        public static readonly HashSet<string> OutdoorGeographyChunkFields = new HashSet<string>(StringComparer.Ordinal)
        { "x", "y" };
        public static readonly HashSet<string> OutdoorGeographyChunkRowsFields = new HashSet<string>(StringComparer.Ordinal)
        { "x", "y", "rows" };
        public static readonly HashSet<string> OutdoorGeographyPrimitiveFields = new HashSet<string>(StringComparer.Ordinal)
        { "stableId", "kind", "worldX", "worldY", "worldWidth", "worldHeight", "strokeWidth", "points" };
        public static readonly HashSet<string> OutdoorGeographyLandmarkFields = new HashSet<string>(StringComparer.Ordinal)
        { "stableId", "label", "worldX", "worldY" };
        public static readonly HashSet<string> OutdoorGeographyHexSummaryFields = new HashSet<string>(StringComparer.Ordinal)
        { "q", "r", "coveredFraction", "waterFraction", "roadFraction", "hasRiver", "hasBridge" };

        public static readonly HashSet<string> MapPlacementFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "kind", "x", "y", "w", "h", "blocksMovement", "boundLocationId", "label", "lootItemId",
            "spawnTableId", "spawnCount"
        };

        public static readonly HashSet<string> SpawnTableFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "entries"
        };

        public static readonly HashSet<string> SpawnTableEntryFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "definitionId", "weight", "countMin", "countMax"
        };

        public static readonly HashSet<string> WorldOpportunityDirectorFields = new HashSet<string>(StringComparer.Ordinal)
        { "id", "type", "name", "surfaceId", "targetActiveMin", "targetActiveMax" };

        public static readonly HashSet<string> WorldOpportunityFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "surfaceId", "weight", "maxActive", "spawnKind", "spawnTableId", "durationDays",
            "minPlayerDistanceWorld", "maxPlayerDistanceWorld", "allowInsideWorldSite", "discoveryMode",
            "worldObjectKind", "worldObjectLabel", "worldObjectWorldWidth", "worldObjectWorldHeight",
            "publicNoticeTitle", "publicNoticeText", "publicNoticeRevealExactLocation",
            "discoveryRadiusWorld", "discoveryNoticeTitle", "discoveryNoticeText", "conditions", "expireOutcomes"
        };

        public static readonly HashSet<string> WorldSitePhysicalRegionFields = new HashSet<string>(StringComparer.Ordinal)
        { "siteId", "surfaceId", "displayName", "siteType", "ownerFactionId", "sourceLocalMapId", "arrivalWorldX", "arrivalWorldY" };
        public static readonly HashSet<string> SurfaceFactionFlagFields = new HashSet<string>(StringComparer.Ordinal)
        { "flagId", "factionId", "worldX", "worldY", "establishedOrder", "createsWorldSite", "siteDisplayName", "siteType", "coreLevel" };
        public static readonly HashSet<string> OutdoorSurfacePlacementFields = new HashSet<string>(StringComparer.Ordinal)
        { "stableId", "siteId", "chunkX", "chunkY", "worldX", "worldY", "worldWidth", "worldHeight", "sourceGridX", "sourceGridY", "sourceCellsW", "sourceCellsH", "kind", "blocksMovement", "boundLocationId", "label", "lootItemId", "spawnTableId", "spawnCount" };
        public static readonly HashSet<string> WorldSitePlaceFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "siteId", "locationId", "name", "worldX", "worldY", "kind", "adjacentIds",
            "resourceOnExploreId", "resourceOnExploreAmount", "opportunitySiteId",
            "residentNpcDefinitionId", "enterConditions", "questOfferIds", "tags",
            "allowedActivities", "localMapId", "enterLocalMapId", "enterSpawnLocationId",
            "surveySenseRequired"
        };

        public static readonly HashSet<string> QuestFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "description", "autoOffer", "abandonable", "deadlineDays",
            "runtimeMode", "acceptanceMode", "deliveryRequirements", "questKind",
            "offerConditions", "completeConditions", "failConditions", "rewards", "failResults"
        };
        public static readonly HashSet<string> QuestDeliveryRequirementFields = new HashSet<string>(StringComparer.Ordinal)
        { "itemId", "amount" };

        public static readonly HashSet<string> ContentEventFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "body", "trigger", "locationId", "questId", "npcDefinitionId", "npcTags", "worldOpportunityId",
            "worldObjectKind", "worldObjectId", "once",
            "conditions", "choices", "priority", "topicText", "onceScope", "entryStepId", "steps"
        };
        public static readonly HashSet<string> ContentEventStepFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "speakerRef", "text", "nextStepId", "outcomes", "choices"
        };
        public static readonly HashSet<string> ContentEventChoiceFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "text", "conditions", "outcomes", "nextStepId", "unavailableMode", "requirementText"
        };

        public static readonly HashSet<string> ContentConditionFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "kind", "id", "amount", "realm", "characterId"
        };

        public static readonly HashSet<string> ContentOutcomeFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "kind", "id", "amount", "fromDefinitionId", "toDefinitionId", "toDefinitionIds"
        };

        public static readonly HashSet<string> OpeningSpawnFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "definitionId", "entityKind", "displayName", "assignOpeningFaction", "factionMode", "factionId", "factionRole",
            "bindSchedule", "bindDailyTask", "recruitable", "scheduleId", "aiRole", "jobId",
            "worldSiteId", "localLocationId", "localPosition"
        };

        public static readonly HashSet<string> OpeningStrategicFields = new HashSet<string>(StringComparer.Ordinal) { "playerFactionId", "vassalages", "alliances", "initialWars" };
        public static readonly HashSet<string> OpeningVassalageFields = new HashSet<string>(StringComparer.Ordinal) { "vassalFactionId", "overlordFactionId" };
        public static readonly HashSet<string> OpeningAllianceFields = new HashSet<string>(StringComparer.Ordinal) { "factionAId", "factionBId" };
        public static readonly HashSet<string> OpeningWarFields = new HashSet<string>(StringComparer.Ordinal) { "declarerFactionId", "targetFactionId" };

        public static readonly HashSet<string> OpeningLocalPositionFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "x", "z"
        };

        public static readonly HashSet<string> ResourceFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "nameKey", "tags"
        };

        public static readonly HashSet<string> WorldSiteEconomyFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "siteId", "initialPublicStock"
        };

        public static readonly HashSet<string> WorldSiteEconomyStockFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "resourceId", "amount"
        };

        public static readonly HashSet<string> OpeningRelationFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "fromDefinitionId", "toDefinitionId", "delta", "reasonTag", "mutual"
        };

        public static readonly HashSet<string> CultivationFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "displayNameKey", "nameKey", "requiredRealm",
            "grade", "effectSummary",
            "cultivationSpeed", "breakthroughProgress", "grantedModifiers", "mastery", "tags"
        };

        public static readonly HashSet<string> CombatArtFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "grade", "effectSummary",
            "attackBonusPercent", "damageFlat", "damageAttackMult", "hitCount", "cooldownSeconds",
            "mastery", "tags"
        };

        public static readonly HashSet<string> ItemFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "displayNameKey", "nameKey", "maxStack", "teachesManualId", "teachesArtId", "tags", "baseTradePrice", "tradeCategory", "notTradable"
        };

        public static readonly HashSet<string> OpeningBondFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "kind", "fromDefinitionId", "toDefinitionId"
        };

        public static readonly HashSet<string> BuildingFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "description", "unlockedByDefault", "placementKind",
            "createsWorldSite", "createdSiteName", "createdSiteType", "initialSiteLevel",
            "dismantleRefundRate", "costs", "outdoorKind", "footprintCellsW", "footprintCellsH"
        };

        public static readonly HashSet<string> BuildingCostFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "itemId", "count"
        };

        public static readonly HashSet<string> OpportunitySiteFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "nameKey", "description", "allowsCultivation", "offeredManualId", "tags"
        };

        public static readonly HashSet<string> ModifierGrantFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "targetAttribute", "operation", "value", "stackingKey"
        };

        public static readonly HashSet<string> AllowedOperations = new HashSet<string>(StringComparer.Ordinal)
        {
            "Fixed", "Percentage"
        };

        public static bool TryParseAttributeId(string name, out AttributeId id)
        {
            return Enum.TryParse(name, ignoreCase: false, out id);
        }

        public static void RejectUnknownFields(
            JsonValue obj,
            HashSet<string> allowed,
            ValidationReport report,
            string context)
        {
            if (obj == null || obj.Kind != JsonValueKind.Object || obj.Object == null)
            {
                report.Add(ErrorCode.ContentLoadFailed, "Expected JSON object.", context);
                return;
            }

            foreach (var key in obj.Object.Keys)
            {
                if (!allowed.Contains(key))
                    report.Add(ErrorCode.InvalidArgument, "Unknown field in definition.", context + "." + key);
            }
        }
    }
}
