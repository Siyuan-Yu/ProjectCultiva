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
            "spiritRootPlaceholder", "initialRealmPlaceholder"
        };

        public static readonly HashSet<string> OpeningScenarioFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "scheduleId", "openingFactionId", "openingSettlementId",
            "openingWorldRegionId", "spawns", "openingRelations"
        };

        public static readonly HashSet<string> WorldRegionFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "startLocationId", "locations"
        };

        public static readonly HashSet<string> WorldLocationFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "name", "kind", "adjacentIds", "resourceOnExploreId", "resourceOnExploreAmount",
            "opportunitySiteId", "residentNpcDefinitionId", "presentationX", "presentationZ"
        };

        public static readonly HashSet<string> OpeningSpawnFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "definitionId", "entityKind", "displayName", "assignOpeningFaction", "factionRole",
            "bindSchedule", "bindDailyTask", "recruitable", "workRole"
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
            "cultivationSpeed", "breakthroughProgress", "grantedModifiers", "tags"
        };

        public static readonly HashSet<string> ItemFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "displayNameKey", "nameKey", "maxStack", "tags"
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
