using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Data.Content;
using XianXia.Data.Serialization;

namespace XianXia.Data.Import
{
    /// <summary>
    /// CSV → JSON authoring helper for Data Pipeline M1-B.
    /// Does not read Excel. Blocks output when ValidationReport is invalid.
    /// </summary>
    public sealed class CsvDefinitionImporter
    {
        public const string CharactersFileName = "characters.csv";
        public const string CultivationFileName = "cultivation.csv";
        public const string ItemsFileName = "items.csv";

        /// <summary>
        /// Convert CSV files under <paramref name="csvDirectory"/> into runtime JSON under
        /// <paramref name="outputDataDirectory"/>. Writes nothing when validation fails.
        /// </summary>
        public Result<CsvImportResult> ConvertDirectory(string csvDirectory, string outputDataDirectory)
        {
            var report = new ValidationReport();
            if (string.IsNullOrEmpty(csvDirectory) || !Directory.Exists(csvDirectory))
            {
                report.Add(ErrorCode.ContentLoadFailed, "CSV directory missing.", csvDirectory);
                return report.ToResult<CsvImportResult>(null);
            }

            if (string.IsNullOrEmpty(outputDataDirectory))
            {
                report.Add(ErrorCode.InvalidArgument, "Output data directory required.");
                return report.ToResult<CsvImportResult>(null);
            }

            var characters = new Dictionary<string, Dictionary<string, JsonValue>>(StringComparer.Ordinal);
            var cultivations = new Dictionary<string, Dictionary<string, JsonValue>>(StringComparer.Ordinal);
            var items = new Dictionary<string, Dictionary<string, JsonValue>>(StringComparer.Ordinal);
            var allIds = new HashSet<string>(StringComparer.Ordinal);
            var pendingRealmRefs = new List<(string ownerId, string requiredRealm)>();

            ParseCharacters(Path.Combine(csvDirectory, CharactersFileName), characters, allIds, report);
            ParseCultivation(Path.Combine(csvDirectory, CultivationFileName), cultivations, allIds, pendingRealmRefs, report);
            ParseItems(Path.Combine(csvDirectory, ItemsFileName), items, allIds, report);

            foreach (var pending in pendingRealmRefs)
            {
                if (!allIds.Contains(pending.requiredRealm))
                {
                    report.Add(
                        ErrorCode.NotFound,
                        "Required realm DefinitionId does not exist.",
                        pending.ownerId + " -> " + pending.requiredRealm);
                }
            }

            if (!report.IsValid)
                return report.ToResult<CsvImportResult>(null);

            Directory.CreateDirectory(outputDataDirectory);
            var written = new List<string>();
            written.Add(WriteDefinitionsFile(
                Path.Combine(outputDataDirectory, "characters.json"),
                characters.Values));
            written.Add(WriteDefinitionsFile(
                Path.Combine(outputDataDirectory, "cultivation.json"),
                cultivations.Values));
            written.Add(WriteDefinitionsFile(
                Path.Combine(outputDataDirectory, "items.json"),
                items.Values));

            return Result.Ok(new CsvImportResult(report, written));
        }

        void ParseCharacters(
            string path,
            Dictionary<string, Dictionary<string, JsonValue>> sink,
            HashSet<string> allIds,
            ValidationReport report)
        {
            if (!File.Exists(path))
            {
                report.Add(ErrorCode.MissingRequiredField, "characters.csv required.", path);
                return;
            }

            List<Dictionary<string, string>> rows;
            try
            {
                rows = SimpleCsv.Parse(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                report.Add(ErrorCode.ContentLoadFailed, "Failed to parse characters.csv.", ex.Message);
                return;
            }

            foreach (var row in rows)
            {
                var id = GetRequired(row, "id", "characters.csv", report);
                if (id == null)
                    continue;

                var parsed = DefinitionId.Parse(id);
                if (parsed.IsFailure)
                {
                    report.Add(parsed.Error);
                    continue;
                }

                if (sink.ContainsKey(id) || allIds.Contains(id))
                {
                    report.Add(ErrorCode.DuplicateDefinitionId, "Duplicate DefinitionId.", id);
                    continue;
                }

                var name = GetRequired(row, "name", id, report);
                if (name == null)
                    continue;

                var def = new Dictionary<string, JsonValue>(StringComparer.Ordinal)
                {
                    ["id"] = JsonValue.FromString(id),
                    ["type"] = JsonValue.FromString("character"),
                    ["name"] = JsonValue.FromString(name),
                    ["displayNameKey"] = JsonValue.FromString(GetOptional(row, "displayNameKey")),
                    ["nameKey"] = JsonValue.FromString(GetOptional(row, "nameKey", GetOptional(row, "displayNameKey")))
                };

                var attrs = new Dictionary<string, JsonValue>(StringComparer.Ordinal);
                TryAddAttr(row, "MaxHp", attrs, report, id);
                TryAddAttr(row, "Attack", attrs, report, id);
                TryAddAttr(row, "Defense", attrs, report, id);
                TryAddAttr(row, "Speed", attrs, report, id);
                if (attrs.Count > 0)
                    def["baseAttributes"] = JsonValue.FromObject(attrs);

                var tags = ParseTags(GetOptional(row, "tags"));
                if (tags.Count > 0)
                    def["tags"] = JsonValue.FromArray(tags);

                sink[id] = def;
                allIds.Add(id);
            }
        }

        void ParseCultivation(
            string path,
            Dictionary<string, Dictionary<string, JsonValue>> sink,
            HashSet<string> allIds,
            List<(string ownerId, string requiredRealm)> pendingRealmRefs,
            ValidationReport report)
        {
            if (!File.Exists(path))
            {
                report.Add(ErrorCode.MissingRequiredField, "cultivation.csv required.", path);
                return;
            }

            List<Dictionary<string, string>> rows;
            try
            {
                rows = SimpleCsv.Parse(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                report.Add(ErrorCode.ContentLoadFailed, "Failed to parse cultivation.csv.", ex.Message);
                return;
            }

            foreach (var row in rows)
            {
                var id = GetRequired(row, "id", "cultivation.csv", report);
                if (id == null)
                    continue;

                if (!sink.TryGetValue(id, out var def))
                {
                    var parsed = DefinitionId.Parse(id);
                    if (parsed.IsFailure)
                    {
                        report.Add(parsed.Error);
                        continue;
                    }

                    if (allIds.Contains(id))
                    {
                        report.Add(ErrorCode.DuplicateDefinitionId, "Duplicate DefinitionId.", id);
                        continue;
                    }

                    var name = GetRequired(row, "name", id, report);
                    if (name == null)
                        continue;

                    def = new Dictionary<string, JsonValue>(StringComparer.Ordinal)
                    {
                        ["id"] = JsonValue.FromString(id),
                        ["type"] = JsonValue.FromString("cultivation"),
                        ["name"] = JsonValue.FromString(name),
                        ["displayNameKey"] = JsonValue.FromString(GetOptional(row, "displayNameKey")),
                        ["nameKey"] = JsonValue.FromString(GetOptional(row, "nameKey", GetOptional(row, "displayNameKey"))),
                        ["requiredRealm"] = JsonValue.FromString(GetOptional(row, "requiredRealm")),
                        ["grantedModifiers"] = JsonValue.FromArray(new List<JsonValue>())
                    };

                    var tags = ParseTags(GetOptional(row, "tags"));
                    if (tags.Count > 0)
                        def["tags"] = JsonValue.FromArray(tags);

                    var requiredRealm = GetOptional(row, "requiredRealm");
                    if (!string.IsNullOrEmpty(requiredRealm) && requiredRealm.IndexOf(':') >= 0)
                        pendingRealmRefs.Add((id, requiredRealm));

                    sink[id] = def;
                    allIds.Add(id);
                }

                var target = GetOptional(row, "targetAttribute");
                var operation = GetOptional(row, "operation");
                var valueText = GetOptional(row, "value");
                if (string.IsNullOrEmpty(target) && string.IsNullOrEmpty(operation) && string.IsNullOrEmpty(valueText))
                    continue;

                if (string.IsNullOrEmpty(target) || string.IsNullOrEmpty(operation) || string.IsNullOrEmpty(valueText))
                {
                    report.Add(ErrorCode.MissingRequiredField, "Modifier grant requires targetAttribute/operation/value.", id);
                    continue;
                }

                if (!DefinitionSchema.TryParseAttributeId(target, out _))
                {
                    report.Add(ErrorCode.InvalidArgument, "Illegal targetAttribute.", id + "." + target);
                    continue;
                }

                if (!DefinitionSchema.AllowedOperations.Contains(operation))
                {
                    report.Add(ErrorCode.InvalidArgument, "Illegal modifier operation.", id + "." + operation);
                    continue;
                }

                if (!int.TryParse(valueText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                {
                    report.Add(ErrorCode.InvalidArgument, "modifier value must be int.", id + "." + valueText);
                    continue;
                }

                var grant = new Dictionary<string, JsonValue>(StringComparer.Ordinal)
                {
                    ["targetAttribute"] = JsonValue.FromString(target),
                    ["operation"] = JsonValue.FromString(operation),
                    ["value"] = JsonValue.FromNumber(value),
                    ["stackingKey"] = JsonValue.FromString(GetOptional(row, "stackingKey"))
                };
                def["grantedModifiers"].Array.Add(JsonValue.FromObject(grant));
            }
        }

        void ParseItems(
            string path,
            Dictionary<string, Dictionary<string, JsonValue>> sink,
            HashSet<string> allIds,
            ValidationReport report)
        {
            if (!File.Exists(path))
            {
                report.Add(ErrorCode.MissingRequiredField, "items.csv required.", path);
                return;
            }

            List<Dictionary<string, string>> rows;
            try
            {
                rows = SimpleCsv.Parse(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                report.Add(ErrorCode.ContentLoadFailed, "Failed to parse items.csv.", ex.Message);
                return;
            }

            foreach (var row in rows)
            {
                var id = GetRequired(row, "id", "items.csv", report);
                if (id == null)
                    continue;

                var parsed = DefinitionId.Parse(id);
                if (parsed.IsFailure)
                {
                    report.Add(parsed.Error);
                    continue;
                }

                if (sink.ContainsKey(id) || allIds.Contains(id))
                {
                    report.Add(ErrorCode.DuplicateDefinitionId, "Duplicate DefinitionId.", id);
                    continue;
                }

                var name = GetRequired(row, "name", id, report);
                if (name == null)
                    continue;

                var maxStack = 1;
                var maxStackText = GetOptional(row, "maxStack");
                if (!string.IsNullOrEmpty(maxStackText))
                {
                    if (!int.TryParse(maxStackText, NumberStyles.Integer, CultureInfo.InvariantCulture, out maxStack) ||
                        maxStack < 1)
                    {
                        report.Add(ErrorCode.InvalidArgument, "maxStack must be >= 1.", id);
                        continue;
                    }
                }

                var def = new Dictionary<string, JsonValue>(StringComparer.Ordinal)
                {
                    ["id"] = JsonValue.FromString(id),
                    ["type"] = JsonValue.FromString("item"),
                    ["name"] = JsonValue.FromString(name),
                    ["displayNameKey"] = JsonValue.FromString(GetOptional(row, "displayNameKey")),
                    ["nameKey"] = JsonValue.FromString(GetOptional(row, "nameKey", GetOptional(row, "displayNameKey"))),
                    ["maxStack"] = JsonValue.FromNumber(maxStack)
                };

                var tags = ParseTags(GetOptional(row, "tags"));
                if (tags.Count > 0)
                    def["tags"] = JsonValue.FromArray(tags);

                sink[id] = def;
                allIds.Add(id);
            }
        }

        static void TryAddAttr(
            Dictionary<string, string> row,
            string key,
            Dictionary<string, JsonValue> attrs,
            ValidationReport report,
            string id)
        {
            var text = GetOptional(row, key);
            if (string.IsNullOrEmpty(text))
                return;
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                report.Add(ErrorCode.InvalidArgument, "Attribute value must be int.", id + "." + key);
                return;
            }

            attrs[key] = JsonValue.FromNumber(value);
        }

        static string GetRequired(
            Dictionary<string, string> row,
            string key,
            string context,
            ValidationReport report)
        {
            if (!row.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            {
                report.Add(ErrorCode.MissingRequiredField, key + " required.", context);
                return null;
            }

            return value.Trim();
        }

        static string GetOptional(Dictionary<string, string> row, string key, string fallback = "")
        {
            if (!row.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
                return fallback ?? string.Empty;
            return value.Trim();
        }

        static List<JsonValue> ParseTags(string raw)
        {
            var list = new List<JsonValue>();
            if (string.IsNullOrWhiteSpace(raw))
                return list;
            foreach (var part in raw.Split(new[] { ';', '|' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var tag = part.Trim();
                if (tag.Length > 0)
                    list.Add(JsonValue.FromString(tag));
            }

            return list;
        }

        static string WriteDefinitionsFile(string path, IEnumerable<Dictionary<string, JsonValue>> defs)
        {
            var array = new List<JsonValue>();
            foreach (var def in defs)
                array.Add(JsonValue.FromObject(def));

            var root = JsonValue.FromObject(new Dictionary<string, JsonValue>(StringComparer.Ordinal)
            {
                ["schemaVersion"] = JsonValue.FromNumber(1),
                ["definitions"] = JsonValue.FromArray(array)
            });
            File.WriteAllText(path, SimpleJson.Stringify(root));
            return path;
        }
    }

    public sealed class CsvImportResult
    {
        public CsvImportResult(ValidationReport report, IReadOnlyList<string> writtenFiles)
        {
            Report = report;
            WrittenFiles = writtenFiles;
        }

        public ValidationReport Report { get; }

        public IReadOnlyList<string> WrittenFiles { get; }
    }
}
