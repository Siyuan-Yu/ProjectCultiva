using System;
using System.Collections.Generic;
using System.IO;
using XianXia.Core.Content;
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

            var refs = new ContentReferenceValidator().Validate(registry);
            if (!refs.IsValid)
                report.AddRange(refs.Errors);

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
                    case "opportunitySite":
                        LoadOpportunitySite(item, parsed.Value, registry, report);
                        break;
                    case "openingScenario":
                        LoadOpeningScenario(item, parsed.Value, registry, report);
                        break;
                    case "resource":
                        LoadResource(item, parsed.Value, registry, report);
                        break;
                    case "facility":
                        LoadFacility(item, parsed.Value, registry, report);
                        break;
                    case "settlement":
                        LoadSettlement(item, parsed.Value, registry, report);
                        break;
                    case "worldRegion":
                        LoadWorldRegion(item, parsed.Value, registry, report);
                        break;
                    case "quest":
                        LoadQuest(item, parsed.Value, registry, report);
                        break;
                    case "contentEvent":
                        LoadContentEvent(item, parsed.Value, registry, report);
                        break;
                    case "chapter":
                        LoadChapter(item, parsed.Value, registry, report);
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
                NameKey = item.GetString("nameKey", string.Empty),
                SpiritRootPlaceholder = item.GetString("spiritRootPlaceholder", string.Empty),
                InitialRealmPlaceholder = item.GetString("initialRealmPlaceholder", string.Empty)
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
            ReadNamedTagArray(item, "personalityTags", character.PersonalityTags, report, id.ToString());
            ReadNamedTagArray(item, "backgroundTags", character.BackgroundTags, report, id.ToString());
            ReadNamedTagArray(item, "talentTags", character.TalentTags, report, id.ToString());
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

            if (item.TryGetProperty("cultivationSpeed", out var speedNode))
            {
                if (speedNode.Kind != JsonValueKind.Number)
                {
                    report.Add(ErrorCode.ContentLoadFailed, "cultivationSpeed must be number.", id.ToString());
                    return;
                }

                cultivation.CultivationSpeed = (int)speedNode.Number;
                if (cultivation.CultivationSpeed < 0)
                {
                    report.Add(ErrorCode.InvalidArgument, "cultivationSpeed must be >= 0.", id.ToString());
                    return;
                }
            }

            if (item.TryGetProperty("breakthroughProgress", out var breakNode))
            {
                if (breakNode.Kind != JsonValueKind.Number)
                {
                    report.Add(ErrorCode.ContentLoadFailed, "breakthroughProgress must be number.", id.ToString());
                    return;
                }

                cultivation.BreakthroughProgress = (int)breakNode.Number;
                if (cultivation.BreakthroughProgress < 0)
                {
                    report.Add(ErrorCode.InvalidArgument, "breakthroughProgress must be >= 0.", id.ToString());
                    return;
                }
            }

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

        static void LoadOpportunitySite(
            JsonValue item,
            DefinitionId id,
            DefinitionRegistry registry,
            ValidationReport report)
        {
            var errorsBefore = report.Errors.Count;
            DefinitionSchema.RejectUnknownFields(item, DefinitionSchema.OpportunitySiteFields, report, id.ToString());
            if (report.Errors.Count > errorsBefore)
                return;

            var allows = false;
            if (item.TryGetProperty("allowsCultivation", out var allowsNode))
            {
                if (allowsNode.Kind != JsonValueKind.Boolean)
                {
                    report.Add(ErrorCode.ContentLoadFailed, "allowsCultivation must be boolean.", id.ToString());
                    return;
                }

                allows = allowsNode.Bool;
            }

            var site = new OpportunitySiteDefinition
            {
                Id = id,
                Name = item.GetString("name", string.Empty),
                NameKey = item.GetString("nameKey", string.Empty),
                Description = item.GetString("description", string.Empty),
                AllowsCultivation = allows,
                OfferedManualId = item.GetString("offeredManualId", string.Empty)
            };

            var tags = new List<string>();
            ReadTags(item, tags, report, id.ToString());
            if (report.Errors.Count > errorsBefore)
                return;

            var reg = registry.RegisterOpportunitySite(site);
            if (reg.IsFailure)
                report.Add(reg.Error);
        }

        static void LoadOpeningScenario(
            JsonValue item,
            DefinitionId id,
            DefinitionRegistry registry,
            ValidationReport report)
        {
            var errorsBefore = report.Errors.Count;
            DefinitionSchema.RejectUnknownFields(item, DefinitionSchema.OpeningScenarioFields, report, id.ToString());
            if (report.Errors.Count > errorsBefore)
                return;

            var scenario = new OpeningScenarioDefinition
            {
                Id = id,
                Name = item.GetString("name", string.Empty),
                ScheduleId = item.GetString("scheduleId", string.Empty),
                OpeningFactionId = item.GetString("openingFactionId", string.Empty),
                OpeningSettlementId = item.GetString("openingSettlementId", string.Empty),
                OpeningWorldRegionId = item.GetString("openingWorldRegionId", string.Empty),
                OpeningChapterId = item.GetString("openingChapterId", string.Empty)
            };

            if (item.TryGetProperty("spawns", out var spawnsNode))
            {
                if (spawnsNode.Kind != JsonValueKind.Array)
                {
                    report.Add(ErrorCode.ContentLoadFailed, "spawns must be array.", id.ToString());
                    return;
                }

                foreach (var spawnNode in spawnsNode.Array)
                {
                    if (spawnNode.Kind != JsonValueKind.Object)
                    {
                        report.Add(ErrorCode.ContentLoadFailed, "spawn entries must be objects.", id.ToString());
                        continue;
                    }

                    DefinitionSchema.RejectUnknownFields(
                        spawnNode,
                        DefinitionSchema.OpeningSpawnFields,
                        report,
                        id + ".spawn");
                    if (report.Errors.Count > errorsBefore)
                        return;

                    var entry = new OpeningSpawnEntry
                    {
                        DefinitionId = spawnNode.GetString("definitionId", string.Empty),
                        EntityKind = spawnNode.GetString("entityKind", "character"),
                        DisplayName = spawnNode.GetString("displayName", string.Empty),
                        AssignOpeningFaction = spawnNode.GetBool("assignOpeningFaction", false),
                        FactionRole = spawnNode.GetString("factionRole", string.Empty),
                        BindSchedule = spawnNode.GetBool("bindSchedule", true),
                        BindDailyTask = spawnNode.GetBool("bindDailyTask", true),
                        Recruitable = spawnNode.GetBool("recruitable", false),
                        WorkRole = spawnNode.GetString("workRole", string.Empty),
                        ScheduleId = spawnNode.GetString("scheduleId", string.Empty),
                        AiRole = spawnNode.GetString("aiRole", string.Empty)
                    };
                    if (string.IsNullOrWhiteSpace(entry.DefinitionId))
                    {
                        report.Add(ErrorCode.MissingRequiredField, "spawn.definitionId required.", id.ToString());
                        return;
                    }

                    scenario.Spawns.Add(entry);
                }
            }

            if (item.TryGetProperty("openingRelations", out var relNode))
            {
                if (relNode.Kind != JsonValueKind.Array)
                {
                    report.Add(ErrorCode.ContentLoadFailed, "openingRelations must be array.", id.ToString());
                    return;
                }

                foreach (var edge in relNode.Array)
                {
                    if (edge.Kind != JsonValueKind.Object)
                    {
                        report.Add(ErrorCode.ContentLoadFailed, "openingRelations entries must be objects.", id.ToString());
                        continue;
                    }

                    DefinitionSchema.RejectUnknownFields(
                        edge,
                        DefinitionSchema.OpeningRelationFields,
                        report,
                        id + ".relation");
                    if (report.Errors.Count > errorsBefore)
                        return;

                    var rel = new OpeningRelationEntry
                    {
                        FromDefinitionId = edge.GetString("fromDefinitionId", string.Empty),
                        ToDefinitionId = edge.GetString("toDefinitionId", string.Empty),
                        Delta = edge.TryGetProperty("delta", out var d) && d.Kind == JsonValueKind.Number
                            ? (int)d.Number
                            : 0,
                        ReasonTag = edge.GetString("reasonTag", "opening_companion"),
                        Mutual = edge.GetBool("mutual", true)
                    };
                    scenario.OpeningRelations.Add(rel);
                }
            }

            if (scenario.Spawns.Count == 0)
            {
                report.Add(ErrorCode.MissingRequiredField, "openingScenario.spawns required.", id.ToString());
                return;
            }

            var reg = registry.RegisterOpeningScenario(scenario);
            if (reg.IsFailure)
                report.Add(reg.Error);
        }

        static void LoadResource(
            JsonValue item,
            DefinitionId id,
            DefinitionRegistry registry,
            ValidationReport report)
        {
            var errorsBefore = report.Errors.Count;
            DefinitionSchema.RejectUnknownFields(item, DefinitionSchema.ResourceFields, report, id.ToString());
            if (report.Errors.Count > errorsBefore)
                return;

            var resource = new ResourceDefinition
            {
                Id = id,
                Name = item.GetString("name", string.Empty),
                NameKey = item.GetString("nameKey", string.Empty)
            };
            var reg = registry.RegisterResource(resource);
            if (reg.IsFailure)
                report.Add(reg.Error);
        }

        static void LoadFacility(
            JsonValue item,
            DefinitionId id,
            DefinitionRegistry registry,
            ValidationReport report)
        {
            var errorsBefore = report.Errors.Count;
            DefinitionSchema.RejectUnknownFields(item, DefinitionSchema.FacilityFields, report, id.ToString());
            if (report.Errors.Count > errorsBefore)
                return;

            var facility = new FacilityDefinition
            {
                Id = id,
                Name = item.GetString("name", string.Empty),
                LaborResourceId = item.GetString("laborResourceId", string.Empty),
                LaborAmountPerWorker = ReadInt(item, "laborAmountPerWorker", 0),
                GatherResourceId = item.GetString("gatherResourceId", string.Empty),
                GatherAmountPerWorker = ReadInt(item, "gatherAmountPerWorker", 0),
                CultivateProgressBonusPerWorker = ReadInt(item, "cultivateProgressBonusPerWorker", 0)
            };
            var reg = registry.RegisterFacility(facility);
            if (reg.IsFailure)
                report.Add(reg.Error);
        }

        static void LoadSettlement(
            JsonValue item,
            DefinitionId id,
            DefinitionRegistry registry,
            ValidationReport report)
        {
            var errorsBefore = report.Errors.Count;
            DefinitionSchema.RejectUnknownFields(item, DefinitionSchema.SettlementFields, report, id.ToString());
            if (report.Errors.Count > errorsBefore)
                return;

            var settlement = new SettlementDefinition
            {
                Id = id,
                Name = item.GetString("name", string.Empty)
            };

            if (item.TryGetProperty("initialStock", out var stockNode))
            {
                if (stockNode.Kind != JsonValueKind.Array)
                {
                    report.Add(ErrorCode.ContentLoadFailed, "initialStock must be array.", id.ToString());
                    return;
                }

                foreach (var entry in stockNode.Array)
                {
                    if (entry.Kind != JsonValueKind.Object)
                    {
                        report.Add(ErrorCode.ContentLoadFailed, "initialStock entries must be objects.", id.ToString());
                        continue;
                    }

                    DefinitionSchema.RejectUnknownFields(
                        entry, DefinitionSchema.SettlementStockFields, report, id + ".stock");
                    if (report.Errors.Count > errorsBefore)
                        return;

                    settlement.InitialStock.Add(new SettlementStockEntry
                    {
                        ResourceId = entry.GetString("resourceId", string.Empty),
                        Amount = ReadInt(entry, "amount", 0)
                    });
                }
            }

            if (item.TryGetProperty("facilities", out var facNode))
            {
                if (facNode.Kind != JsonValueKind.Array)
                {
                    report.Add(ErrorCode.ContentLoadFailed, "facilities must be array.", id.ToString());
                    return;
                }

                foreach (var f in facNode.Array)
                {
                    if (f.Kind != JsonValueKind.String || string.IsNullOrWhiteSpace(f.String))
                    {
                        report.Add(ErrorCode.ContentLoadFailed, "facilities entries must be strings.", id.ToString());
                        continue;
                    }

                    settlement.FacilityIds.Add(f.String);
                }
            }

            var reg = registry.RegisterSettlement(settlement);
            if (reg.IsFailure)
                report.Add(reg.Error);
        }

        static int ReadInt(JsonValue obj, string name, int fallback)
        {
            if (!obj.TryGetProperty(name, out var n) || n.Kind != JsonValueKind.Number)
                return fallback;
            return (int)n.Number;
        }

        static float ReadFloat(JsonValue obj, string name, float fallback)
        {
            if (!obj.TryGetProperty(name, out var n) || n.Kind != JsonValueKind.Number)
                return fallback;
            return (float)n.Number;
        }

        static void LoadWorldRegion(
            JsonValue item,
            DefinitionId id,
            DefinitionRegistry registry,
            ValidationReport report)
        {
            var errorsBefore = report.Errors.Count;
            DefinitionSchema.RejectUnknownFields(item, DefinitionSchema.WorldRegionFields, report, id.ToString());
            if (report.Errors.Count > errorsBefore)
                return;

            var region = new WorldRegionDefinition
            {
                Id = id,
                Name = item.GetString("name", string.Empty),
                StartLocationId = item.GetString("startLocationId", string.Empty)
            };

            if (!item.TryGetProperty("locations", out var locs) || locs.Kind != JsonValueKind.Array)
            {
                report.Add(ErrorCode.MissingRequiredField, "worldRegion.locations required.", id.ToString());
                return;
            }

            foreach (var locNode in locs.Array)
            {
                if (locNode.Kind != JsonValueKind.Object)
                {
                    report.Add(ErrorCode.ContentLoadFailed, "location entries must be objects.", id.ToString());
                    continue;
                }

                DefinitionSchema.RejectUnknownFields(
                    locNode, DefinitionSchema.WorldLocationFields, report, id + ".location");
                if (report.Errors.Count > errorsBefore)
                    return;

                var entry = new WorldLocationEntry
                {
                    Id = locNode.GetString("id", string.Empty),
                    Name = locNode.GetString("name", string.Empty),
                    Kind = locNode.GetString("kind", "Wild"),
                    ResourceOnExploreId = locNode.GetString("resourceOnExploreId", string.Empty),
                    ResourceOnExploreAmount = ReadInt(locNode, "resourceOnExploreAmount", 0),
                    OpportunitySiteId = locNode.GetString("opportunitySiteId", string.Empty),
                    ResidentNpcDefinitionId = locNode.GetString("residentNpcDefinitionId", string.Empty),
                    PresentationX = ReadFloat(locNode, "presentationX", 0f),
                    PresentationZ = ReadFloat(locNode, "presentationZ", 0f)
                };

                if (string.IsNullOrWhiteSpace(entry.Id))
                {
                    report.Add(ErrorCode.MissingRequiredField, "location.id required.", id.ToString());
                    return;
                }

                if (locNode.TryGetProperty("adjacentIds", out var adj) && adj.Kind == JsonValueKind.Array)
                {
                    foreach (var a in adj.Array)
                    {
                        if (a.Kind == JsonValueKind.String && !string.IsNullOrWhiteSpace(a.String))
                            entry.AdjacentIds.Add(a.String);
                    }
                }

                ReadConditions(
                    locNode, "enterConditions", entry.EnterConditions, report, id + "." + entry.Id);
                ReadStringList(locNode, "questOfferIds", entry.QuestOfferIds, report, id + "." + entry.Id);

                region.Locations.Add(entry);
            }

            if (region.Locations.Count == 0)
            {
                report.Add(ErrorCode.MissingRequiredField, "worldRegion.locations empty.", id.ToString());
                return;
            }

            var reg = registry.RegisterWorldRegion(region);
            if (reg.IsFailure)
                report.Add(reg.Error);
        }

        static void LoadChapter(
            JsonValue item,
            DefinitionId id,
            DefinitionRegistry registry,
            ValidationReport report)
        {
            var errorsBefore = report.Errors.Count;
            DefinitionSchema.RejectUnknownFields(item, DefinitionSchema.ChapterFields, report, id.ToString());
            if (report.Errors.Count > errorsBefore)
                return;

            var chapter = new ChapterDefinition
            {
                Id = id,
                Name = item.GetString("name", string.Empty),
                Description = item.GetString("description", string.Empty),
                OpeningScenarioId = item.GetString("openingScenarioId", string.Empty),
                PlannedDays = ReadInt(item, "plannedDays", 0)
            };
            ReadStringList(item, "questChainIds", chapter.QuestChainIds, report, id.ToString());
            ReadStringList(item, "eventChainIds", chapter.EventChainIds, report, id.ToString());

            if (item.TryGetProperty("dayBeats", out var beats) && beats.Kind == JsonValueKind.Array)
            {
                foreach (var beatNode in beats.Array)
                {
                    if (beatNode.Kind != JsonValueKind.Object)
                    {
                        report.Add(ErrorCode.ContentLoadFailed, "dayBeats entries must be objects.", id.ToString());
                        continue;
                    }

                    DefinitionSchema.RejectUnknownFields(
                        beatNode, DefinitionSchema.ChapterDayBeatFields, report, id + ".dayBeat");
                    var beat = new ChapterDayBeatDefinition
                    {
                        DayIndex = ReadInt(beatNode, "dayIndex", 0)
                    };
                    ReadConditions(beatNode, "conditions", beat.Conditions, report, id + ".dayBeat");
                    ReadStringList(beatNode, "questOfferIds", beat.QuestOfferIds, report, id + ".dayBeat");
                    ReadStringList(beatNode, "contentEventIds", beat.ContentEventIds, report, id + ".dayBeat");
                    ReadStringList(beatNode, "setFlags", beat.SetFlags, report, id + ".dayBeat");
                    chapter.DayBeats.Add(beat);
                }
            }

            if (report.Errors.Count > errorsBefore)
                return;

            var reg = registry.RegisterChapter(chapter);
            if (reg.IsFailure)
                report.Add(reg.Error);
        }

        static void LoadQuest(
            JsonValue item,
            DefinitionId id,
            DefinitionRegistry registry,
            ValidationReport report)
        {
            var errorsBefore = report.Errors.Count;
            DefinitionSchema.RejectUnknownFields(item, DefinitionSchema.QuestFields, report, id.ToString());
            if (report.Errors.Count > errorsBefore)
                return;

            var quest = new QuestDefinition
            {
                Id = id,
                Name = item.GetString("name", string.Empty),
                Description = item.GetString("description", string.Empty),
                AutoOffer = item.GetBool("autoOffer", false)
            };
            ReadConditions(item, "offerConditions", quest.OfferConditions, report, id.ToString());
            ReadConditions(item, "completeConditions", quest.CompleteConditions, report, id.ToString());
            ReadConditions(item, "failConditions", quest.FailConditions, report, id.ToString());
            ReadOutcomes(item, "rewards", quest.Rewards, report, id.ToString());
            ReadOutcomes(item, "failResults", quest.FailResults, report, id.ToString());
            if (report.Errors.Count > errorsBefore)
                return;

            var reg = registry.RegisterQuest(quest);
            if (reg.IsFailure)
                report.Add(reg.Error);
        }

        static void LoadContentEvent(
            JsonValue item,
            DefinitionId id,
            DefinitionRegistry registry,
            ValidationReport report)
        {
            var errorsBefore = report.Errors.Count;
            DefinitionSchema.RejectUnknownFields(item, DefinitionSchema.ContentEventFields, report, id.ToString());
            if (report.Errors.Count > errorsBefore)
                return;

            var evt = new ContentEventDefinition
            {
                Id = id,
                Name = item.GetString("name", string.Empty),
                Body = item.GetString("body", string.Empty),
                Trigger = item.GetString("trigger", string.Empty),
                LocationId = item.GetString("locationId", string.Empty),
                QuestId = item.GetString("questId", string.Empty),
                Once = item.GetBool("once", true)
            };
            ReadConditions(item, "conditions", evt.Conditions, report, id.ToString());

            if (item.TryGetProperty("choices", out var choices) && choices.Kind == JsonValueKind.Array)
            {
                foreach (var choiceNode in choices.Array)
                {
                    if (choiceNode.Kind != JsonValueKind.Object)
                    {
                        report.Add(ErrorCode.ContentLoadFailed, "choice entries must be objects.", id.ToString());
                        continue;
                    }

                    DefinitionSchema.RejectUnknownFields(
                        choiceNode, DefinitionSchema.ContentEventChoiceFields, report, id + ".choice");
                    var choice = new ContentEventChoiceDefinition
                    {
                        Id = choiceNode.GetString("id", string.Empty),
                        Text = choiceNode.GetString("text", string.Empty)
                    };
                    if (string.IsNullOrWhiteSpace(choice.Id))
                    {
                        report.Add(ErrorCode.MissingRequiredField, "choice.id required.", id.ToString());
                        return;
                    }

                    ReadConditions(choiceNode, "conditions", choice.Conditions, report, id + "." + choice.Id);
                    ReadOutcomes(choiceNode, "outcomes", choice.Outcomes, report, id + "." + choice.Id);
                    evt.Choices.Add(choice);
                }
            }

            if (report.Errors.Count > errorsBefore)
                return;

            var reg = registry.RegisterContentEvent(evt);
            if (reg.IsFailure)
                report.Add(reg.Error);
        }

        static void ReadConditions(
            JsonValue item,
            string field,
            List<ContentCondition> list,
            ValidationReport report,
            string context)
        {
            if (!item.TryGetProperty(field, out var arr) || arr.Kind != JsonValueKind.Array)
                return;
            foreach (var node in arr.Array)
            {
                if (node.Kind != JsonValueKind.Object)
                {
                    report.Add(ErrorCode.ContentLoadFailed, field + " entries must be objects.", context);
                    continue;
                }

                DefinitionSchema.RejectUnknownFields(
                    node, DefinitionSchema.ContentConditionFields, report, context + "." + field);
                var c = new ContentCondition
                {
                    Kind = node.GetString("kind", string.Empty),
                    Id = node.GetString("id", string.Empty),
                    Amount = ReadInt(node, "amount", 0),
                    Realm = node.GetString("realm", string.Empty)
                };
                if (string.IsNullOrWhiteSpace(c.Kind))
                {
                    report.Add(ErrorCode.MissingRequiredField, "condition.kind required.", context);
                    continue;
                }

                list.Add(c);
            }
        }

        static void ReadOutcomes(
            JsonValue item,
            string field,
            List<ContentOutcome> list,
            ValidationReport report,
            string context)
        {
            if (!item.TryGetProperty(field, out var arr) || arr.Kind != JsonValueKind.Array)
                return;
            foreach (var node in arr.Array)
            {
                if (node.Kind != JsonValueKind.Object)
                {
                    report.Add(ErrorCode.ContentLoadFailed, field + " entries must be objects.", context);
                    continue;
                }

                DefinitionSchema.RejectUnknownFields(
                    node, DefinitionSchema.ContentOutcomeFields, report, context + "." + field);
                var o = new ContentOutcome
                {
                    Kind = node.GetString("kind", string.Empty),
                    Id = node.GetString("id", string.Empty),
                    Amount = ReadInt(node, "amount", 0),
                    FromDefinitionId = node.GetString("fromDefinitionId", string.Empty),
                    ToDefinitionId = node.GetString("toDefinitionId", string.Empty)
                };
                if (string.IsNullOrWhiteSpace(o.Kind))
                {
                    report.Add(ErrorCode.MissingRequiredField, "outcome.kind required.", context);
                    continue;
                }

                list.Add(o);
            }
        }

        static void ReadStringList(
            JsonValue item,
            string field,
            List<string> list,
            ValidationReport report,
            string context)
        {
            if (!item.TryGetProperty(field, out var arr) || arr.Kind != JsonValueKind.Array)
                return;
            foreach (var node in arr.Array)
            {
                if (node.Kind != JsonValueKind.String || string.IsNullOrWhiteSpace(node.String))
                {
                    report.Add(ErrorCode.ContentLoadFailed, field + " entries must be strings.", context);
                    continue;
                }

                list.Add(node.String);
            }
        }

        static void ReadTags(JsonValue item, List<string> tags, ValidationReport report, string context) =>
            ReadNamedTagArray(item, "tags", tags, report, context);

        static void ReadNamedTagArray(
            JsonValue item,
            string field,
            List<string> tags,
            ValidationReport report,
            string context)
        {
            if (!item.TryGetProperty(field, out var tagsNode))
                return;
            if (tagsNode.Kind != JsonValueKind.Array)
            {
                report.Add(ErrorCode.ContentLoadFailed, field + " must be array.", context);
                return;
            }

            foreach (var t in tagsNode.Array)
            {
                if (t.Kind != JsonValueKind.String)
                {
                    report.Add(ErrorCode.ContentLoadFailed, field + " entries must be strings.", context);
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
