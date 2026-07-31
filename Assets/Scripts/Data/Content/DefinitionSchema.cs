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
            "spiritRootPlaceholder", "initialRealmPlaceholder"
        };

        public static readonly HashSet<string> CultivationFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "displayNameKey", "nameKey", "requiredRealm", "grantedModifiers", "tags"
        };

        public static readonly HashSet<string> ItemFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "type", "name", "displayNameKey", "nameKey", "maxStack", "tags"
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
