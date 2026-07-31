using System;
using System.Collections.Generic;
using System.IO;
using XianXia.Core.Domain;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Data.Serialization;

namespace XianXia.Data.Content
{
    /// <summary>
    /// Loads explicitly listed ContentPackage directories. Never scans Mods/.
    /// Data Pipeline M1-A: character / cultivation / item definitions with strict field checks.
    /// </summary>
    public sealed class ContentPackageLoader
    {
        public Result<LoadedContent> Load(IReadOnlyList<string> packageDirectories)
        {
            if (packageDirectories == null || packageDirectories.Count == 0)
                return Result.Fail<LoadedContent>(ErrorCode.ContentLoadFailed, "No package directories specified.");

            var report = new ValidationReport();
            var registry = new DefinitionRegistry();
            var manifests = new List<ContentManifest>();

            foreach (var dir in packageDirectories)
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                {
                    report.Add(ErrorCode.ContentLoadFailed, "Package directory missing.", dir);
                    continue;
                }

                var manifestPath = Path.Combine(dir, "manifest.json");
                if (!File.Exists(manifestPath))
                {
                    report.Add(ErrorCode.MissingRequiredField, "manifest.json missing.", dir);
                    continue;
                }

                ContentManifest manifest;
                try
                {
                    manifest = ReadManifest(File.ReadAllText(manifestPath), dir, report);
                }
                catch (Exception ex)
                {
                    report.Add(ErrorCode.ContentLoadFailed, "Failed to parse manifest.json.", ex.Message);
                    continue;
                }

                if (manifest == null)
                    continue;

                manifests.Add(manifest);

                var dataRoot = Path.Combine(dir, "Data");
                if (!Directory.Exists(dataRoot))
                    continue;

                foreach (var file in Directory.GetFiles(dataRoot, "*.json", SearchOption.AllDirectories))
                {
                    try
                    {
                        LoadDefinitionsFile(File.ReadAllText(file), file, manifest, registry, report);
                    }
                    catch (Exception ex)
                    {
                        report.Add(ErrorCode.ContentLoadFailed, "Failed to parse definitions.", file + ": " + ex.Message);
                    }
                }
            }

            if (!report.IsValid)
                return report.ToResult<LoadedContent>(null);

            return Result.Ok(new LoadedContent(manifests, registry));
        }

        static ContentManifest ReadManifest(string json, string dir, ValidationReport report)
        {
            var root = SimpleJson.Parse(json);
            var modId = root.GetString("modId");
            var ns = root.GetString("namespace");
            var versionText = root.GetString("version");
            var compatible = root.GetString("compatibleGameVersion");

            if (string.IsNullOrEmpty(modId))
                report.Add(ErrorCode.MissingRequiredField, "modId required.", dir);
            if (string.IsNullOrEmpty(ns))
                report.Add(ErrorCode.MissingRequiredField, "namespace required.", dir);
            if (string.IsNullOrEmpty(versionText))
                report.Add(ErrorCode.MissingRequiredField, "version required.", dir);

            if (!report.IsValid && (string.IsNullOrEmpty(modId) || string.IsNullOrEmpty(ns) || string.IsNullOrEmpty(versionText)))
                return null;

            var manifest = new ContentManifest
            {
                ModId = modId,
                Namespace = ns,
                Version = new DataVersion(versionText),
                CompatibleGameVersion = compatible ?? string.Empty,
                PackageDirectory = dir
            };

            if (root.TryGetProperty("contentFolders", out var folders) && folders.Kind == JsonValueKind.Array)
            {
                foreach (var f in folders.Array)
                {
                    if (f.Kind == JsonValueKind.String)
                        manifest.ContentFolders.Add(f.String);
                }
            }

            return manifest;
        }

        static void LoadDefinitionsFile(
            string json,
            string filePath,
            ContentManifest manifest,
            DefinitionRegistry registry,
            ValidationReport report)
        {
            var root = SimpleJson.Parse(json);
            if (!root.TryGetProperty("definitions", out var defs) || defs.Kind != JsonValueKind.Array)
            {
                report.Add(ErrorCode.MissingRequiredField, "definitions array required.", filePath);
                return;
            }

            // File root may only contain "definitions" in M1-A strict samples.
            if (root.Kind == JsonValueKind.Object && root.Object != null)
            {
                foreach (var key in root.Object.Keys)
                {
                    if (!string.Equals(key, "definitions", StringComparison.Ordinal) &&
                        !string.Equals(key, "schemaVersion", StringComparison.Ordinal))
                    {
                        report.Add(ErrorCode.InvalidArgument, "Unknown field in definitions file.", filePath + "." + key);
                    }
                }
            }

            foreach (var item in defs.Array)
            {
                if (item.Kind != JsonValueKind.Object)
                {
                    report.Add(ErrorCode.ContentLoadFailed, "Definition entry must be object.", filePath);
                    continue;
                }

                var idText = item.GetString("id");
                var type = item.GetString("type");
                if (string.IsNullOrEmpty(idText))
                {
                    report.Add(ErrorCode.MissingRequiredField, "definition.id required.", filePath);
                    continue;
                }

                if (string.IsNullOrEmpty(type))
                {
                    report.Add(ErrorCode.MissingRequiredField, "definition.type required.", idText);
                    continue;
                }

                var parsed = DefinitionId.Parse(idText);
                if (parsed.IsFailure)
                {
                    report.Add(parsed.Error);
                    continue;
                }

                if (!string.Equals(parsed.Value.Namespace, manifest.Namespace, StringComparison.Ordinal))
                {
                    report.Add(
                        ErrorCode.InvalidDefinitionId,
                        "DefinitionId namespace must match package namespace.",
                        idText + " vs " + manifest.Namespace);
                    continue;
                }

                switch (type)
                {
                    case "character":
                        LoadCharacter(item, parsed.Value, registry, report);
                        break;
                    case "cultivation":
                        LoadCultivation(item, parsed.Value, registry, report);
                        break;
                    case "item":
                        LoadItem(item, parsed.Value, registry, report);
                        break;
                    default:
                        report.Add(ErrorCode.InvalidArgument, "Unknown definition type.", type);
                        break;
                }
            }
        }

        static void LoadCharacter(
            JsonValue item,
            DefinitionId id,
            DefinitionRegistry registry,
            ValidationReport report)
        {
            var errorsBefore = report.Errors.Count;
            DefinitionSchema.RejectUnknownFields(item, DefinitionSchema.CharacterFields, report, id.ToString());
            if (report.Errors.Count > errorsBefore)
                return;

            var character = new CharacterDefinition
            {
                Id = id,
                Name = item.GetString("name", string.Empty),
                DisplayNameKey = item.GetString("displayNameKey", string.Empty),
                NameKey = item.GetString("nameKey", string.Empty)
            };

            if (item.TryGetProperty("baseAttributes", out var attrs))
            {
                if (attrs.Kind != JsonValueKind.Object)
                {
                    report.Add(ErrorCode.ContentLoadFailed, "baseAttributes must be object.", id.ToString());
                    return;
                }

                foreach (var kv in attrs.Object)
                {
                    if (!DefinitionSchema.TryParseAttributeId(kv.Key, out _))
                    {
                        report.Add(ErrorCode.InvalidArgument, "Unknown AttributeId in baseAttributes.", id + "." + kv.Key);
                        continue;
                    }

                    if (kv.Value.Kind != JsonValueKind.Number)
                    {
                        report.Add(ErrorCode.ContentLoadFailed, "Attribute value must be number.", id + "." + kv.Key);
                        continue;
                    }

                    character.BaseAttributes[kv.Key] = (int)kv.Value.Number;
                }

                if (report.Errors.Count > errorsBefore)
                    return;
            }

            ReadTags(item, character.Tags, report, id.ToString());
            if (report.Errors.Count > errorsBefore)
                return;

            var reg = registry.RegisterCharacter(character);
            if (reg.IsFailure)
                report.Add(reg.Error);
        }

        static void LoadCultivation(
            JsonValue item,
            DefinitionId id,
            DefinitionRegistry registry,
            ValidationReport report)
        {
            var errorsBefore = report.Errors.Count;
            DefinitionSchema.RejectUnknownFields(item, DefinitionSchema.CultivationFields, report, id.ToString());
            if (report.Errors.Count > errorsBefore)
                return;

            var cultivation = new CultivationDefinition
            {
                Id = id,
                Name = item.GetString("name", string.Empty),
                DisplayNameKey = item.GetString("displayNameKey", string.Empty),
                NameKey = item.GetString("nameKey", string.Empty),
                RequiredRealm = item.GetString("requiredRealm", string.Empty)
            };

            if (item.TryGetProperty("grantedModifiers", out var grants))
            {
                if (grants.Kind != JsonValueKind.Array)
                {
                    report.Add(ErrorCode.ContentLoadFailed, "grantedModifiers must be array.", id.ToString());
                    return;
                }

                foreach (var grant in grants.Array)
                {
                    if (grant.Kind != JsonValueKind.Object)
                    {
                        report.Add(ErrorCode.ContentLoadFailed, "grantedModifiers entry must be object.", id.ToString());
                        continue;
                    }

                    var grantErrorsBefore = report.Errors.Count;
                    DefinitionSchema.RejectUnknownFields(
                        grant,
                        DefinitionSchema.ModifierGrantFields,
                        report,
                        id + ".grantedModifiers");
                    if (report.Errors.Count > grantErrorsBefore)
                        continue;

                    var target = grant.GetString("targetAttribute");
                    var operation = grant.GetString("operation");
                    if (string.IsNullOrEmpty(target) ||
                        !DefinitionSchema.TryParseAttributeId(target, out _))
                    {
                        report.Add(ErrorCode.InvalidArgument, "Illegal targetAttribute.", id + "." + target);
                        continue;
                    }

                    if (string.IsNullOrEmpty(operation) ||
                        !DefinitionSchema.AllowedOperations.Contains(operation))
                    {
                        report.Add(ErrorCode.InvalidArgument, "Illegal modifier operation.", id + "." + operation);
                        continue;
                    }

                    if (!grant.TryGetProperty("value", out var valueNode) || valueNode.Kind != JsonValueKind.Number)
                    {
                        report.Add(ErrorCode.MissingRequiredField, "grantedModifiers.value required.", id.ToString());
                        continue;
                    }

                    cultivation.GrantedModifiers.Add(new ModifierGrantDefinition
                    {
                        TargetAttribute = target,
                        Operation = operation,
                        Value = (int)valueNode.Number,
                        StackingKey = grant.GetString("stackingKey", string.Empty)
                    });
                }

                if (report.Errors.Count > errorsBefore)
                    return;
            }

            ReadTags(item, cultivation.Tags, report, id.ToString());
            if (report.Errors.Count > errorsBefore)
                return;

            var reg = registry.RegisterCultivation(cultivation);
            if (reg.IsFailure)
                report.Add(reg.Error);
        }

        static void LoadItem(
            JsonValue item,
            DefinitionId id,
            DefinitionRegistry registry,
            ValidationReport report)
        {
            var errorsBefore = report.Errors.Count;
            DefinitionSchema.RejectUnknownFields(item, DefinitionSchema.ItemFields, report, id.ToString());
            if (report.Errors.Count > errorsBefore)
                return;

            var maxStack = 1;
            if (item.TryGetProperty("maxStack", out var stackNode))
            {
                if (stackNode.Kind != JsonValueKind.Number)
                {
                    report.Add(ErrorCode.ContentLoadFailed, "maxStack must be number.", id.ToString());
                    return;
                }

                maxStack = (int)stackNode.Number;
                if (maxStack < 1)
                {
                    report.Add(ErrorCode.InvalidArgument, "maxStack must be >= 1.", id.ToString());
                    return;
                }
            }

            var itemDef = new ItemDefinition
            {
                Id = id,
                Name = item.GetString("name", string.Empty),
                DisplayNameKey = item.GetString("displayNameKey", string.Empty),
                NameKey = item.GetString("nameKey", string.Empty),
                MaxStack = maxStack
            };

            ReadTags(item, itemDef.Tags, report, id.ToString());
            if (report.Errors.Count > errorsBefore)
                return;

            var reg = registry.RegisterItem(itemDef);
            if (reg.IsFailure)
                report.Add(reg.Error);
        }

        static void ReadTags(JsonValue item, List<string> tags, ValidationReport report, string context)
        {
            if (!item.TryGetProperty("tags", out var tagsNode))
                return;
            if (tagsNode.Kind != JsonValueKind.Array)
            {
                report.Add(ErrorCode.ContentLoadFailed, "tags must be array.", context);
                return;
            }

            foreach (var t in tagsNode.Array)
            {
                if (t.Kind != JsonValueKind.String)
                {
                    report.Add(ErrorCode.ContentLoadFailed, "tags entries must be strings.", context);
                    continue;
                }

                tags.Add(t.String);
            }
        }
    }

    public sealed class LoadedContent
    {
        public LoadedContent(IReadOnlyList<ContentManifest> manifests, DefinitionRegistry registry)
        {
            Manifests = manifests;
            Registry = registry;
        }

        public IReadOnlyList<ContentManifest> Manifests { get; }

        public DefinitionRegistry Registry { get; }
    }
}
