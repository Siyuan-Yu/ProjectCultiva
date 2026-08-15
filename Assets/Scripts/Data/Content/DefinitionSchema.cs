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
            "homeWorkAreaId"
        };

        public static readonly HashSet<string> OpeningScenarioFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "scheduleId", "openingFactionId", "openingSettlementId",
            "openingWorldRegionId", "openingChapterId", "spawns", "openingRelations"
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
            "originX", "originY", "cellSize", "width", "height", "placements"
        };

        public static readonly HashSet<string> MapPlacementFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "kind", "x", "y", "w", "h", "blocksMovement", "boundLocationId", "label", "lootItemId"
        };

        public static readonly HashSet<string> QuestFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "description", "autoOffer", "abandonable", "deadlineDays",
            "offerConditions", "completeConditions", "failConditions", "rewards", "failResults"
        };

        public static readonly HashSet<string> ContentEventFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "body", "trigger", "locationId", "questId", "npcDefinitionId", "once",
            "conditions", "choices"
        };

        public static readonly HashSet<string> ContentEventChoiceFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "text", "conditions", "outcomes"
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
            "definitionId", "entityKind", "displayName", "assignOpeningFaction", "factionRole",
            "bindSchedule", "bindDailyTask", "recruitable", "workRole", "scheduleId", "aiRole", "jobId"
        };

        public static readonly HashSet<string> ResourceFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "nameKey", "tags"
        };

        public static readonly HashSet<string> FacilityFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "laborResourceId", "laborAmountPerWorker",
            "gatherResourceId", "gatherAmountPerWorker", "cultivateProgressBonusPerWorker", "tags"
        };

        public static readonly HashSet<string> SettlementFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "initialStock", "facilities"
        };

        public static readonly HashSet<string> SettlementStockFields = new HashSet<string>(StringComparer.Ordinal)
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
            "cultivationSpeed", "breakthroughProgress", "grantedModifiers", "tags"
        };

        public static readonly HashSet<string> ItemFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "displayNameKey", "nameKey", "maxStack", "teachesManualId", "tags"
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
