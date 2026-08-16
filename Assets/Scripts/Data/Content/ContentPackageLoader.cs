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
                    case "combatArt":
                        LoadCombatArt(item, parsed.Value, registry, report);
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
                    case "characterRoster":
                        LoadCharacterRoster(item, parsed.Value, registry, report);
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
                    case "localPlaceSet":
                        LoadLocalPlaceSet(item, parsed.Value, registry, report);
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
                    case "workArea":
                        LoadWorkArea(item, parsed.Value, registry, report);
                        break;
                    case "job":
                        LoadJob(item, parsed.Value, registry, report);
                        break;
                    case "schedule":
                        LoadSchedule(item, parsed.Value, registry, report);
                        break;
                    case "mapLayout":
                        LoadMapLayout(item, parsed.Value, registry, report);
                        break;
                    case "spawnTable":
                        LoadSpawnTable(item, parsed.Value, registry, report);
                        break;
                    case "worldGraph":
                        LoadWorldGraph(item, parsed.Value, registry, report);
                        break;
                    case "realmLadder":
                        LoadRealmLadder(item, parsed.Value, registry, report);
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
                InitialRealmPlaceholder = item.GetString("initialRealmPlaceholder", string.Empty),
                PlayerControllable = item.GetBool("playerControllable", false)
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
            ReadNamedTagArray(item, "preferredWorkAreaIds", character.PreferredWorkAreaIds, report, id.ToString());
            character.HomeWorkAreaId = item.GetString("homeWorkAreaId", string.Empty) ?? string.Empty;
            ReadNamedTagArray(item, "goals", character.Goals, report, id.ToString());
            ReadNamedTagArray(item, "desires", character.Desires, report, id.ToString());
            ReadBoolMap(item, "activityCapabilities", character.ActivityCapabilities, report, id.ToString());
            ReadIntMap(item, "activityPriorities", character.ActivityPriorities, report, id.ToString());
            ReadIntMap(item, "spiritRoots", character.SpiritRoots, report, id.ToString());
            character.Hometown = item.GetString("hometown", string.Empty);
            if (item.TryGetProperty("reputation", out var repNode) && repNode.Kind == JsonValueKind.Number)
                character.Reputation = (int)repNode.Number;
            character.DefeatEncounterId = item.GetString("defeatEncounterId", string.Empty) ?? string.Empty;
            if (report.Errors.Count > errorsBefore)
                return;

            var reg = registry.RegisterCharacter(character);
            if (reg.IsFailure)
                report.Add(reg.Error);
        }

        static void LoadRealmLadder(
            JsonValue item,
            DefinitionId id,
            DefinitionRegistry registry,
            ValidationReport report)
        {
            var def = new RealmLadderDefinition
            {
                Id = id,
                Name = item.GetString("name", string.Empty)
            };

            if (!item.TryGetProperty("steps", out var stepsNode) || stepsNode.Kind != JsonValueKind.Array)
            {
                report.Add(ErrorCode.MissingRequiredField, "realmLadder.steps required.", id.ToString());
                return;
            }

            for (var i = 0; i < stepsNode.Array.Count; i++)
            {
                var row = stepsNode.Array[i];
                if (row.Kind != JsonValueKind.Object)
                    continue;
                var step = new RealmLadderStepDefinition
                {
                    FromRealm = row.GetString("fromRealm", string.Empty),
                    FromMinor = (int)row.GetNumber("fromMinor", 0),
                    ToRealm = row.GetString("toRealm", string.Empty),
                    ToMinor = (int)row.GetNumber("toMinor", 0),
                    ProgressRequired = (int)row.GetNumber("progressRequired", 0),
                    SuccessPercent = (int)row.GetNumber("successPercent", 95),
                    MajorRealmJump = row.GetBool("majorRealmJump", false),
                    GrantSpiritPower = (int)row.GetNumber("grantSpiritPower", 0)
                };
                if (row.TryGetProperty("bonuses", out var bonuses) && bonuses.Kind == JsonValueKind.Object)
                {
                    foreach (var kv in bonuses.Object)
                    {
                        if (kv.Value.Kind == JsonValueKind.Number)
                            step.Bonuses[kv.Key] = (int)kv.Value.Number;
                    }
                }

                def.Steps.Add(step);
            }

            var reg = registry.RegisterRealmLadder(def);
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
                RequiredRealm = item.GetString("requiredRealm", string.Empty),
                Grade = item.GetString("grade", string.Empty),
                EffectSummary = item.GetString("effectSummary", string.Empty)
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
                        Value = valueNode.Number,
                        StackingKey = grant.GetString("stackingKey", string.Empty)
                    });
                }

                if (report.Errors.Count > errorsBefore)
                    return;
            }

            if (item.TryGetProperty("mastery", out var masteryNode))
            {
                if (!SkillMasteryProfileParser.TryParse(
                        masteryNode, id.ToString(), report, out var masteryDef))
                    return;
                cultivation.Mastery = masteryDef;
            }

            ReadTags(item, cultivation.Tags, report, id.ToString());
            if (report.Errors.Count > errorsBefore)
                return;

            var reg = registry.RegisterCultivation(cultivation);
            if (reg.IsFailure)
                report.Add(reg.Error);
        }

        static void LoadCombatArt(
            JsonValue item,
            DefinitionId id,
            DefinitionRegistry registry,
            ValidationReport report)
        {
            var errorsBefore = report.Errors.Count;
            DefinitionSchema.RejectUnknownFields(item, DefinitionSchema.CombatArtFields, report, id.ToString());
            if (report.Errors.Count > errorsBefore)
                return;

            var art = new CombatArtDefinition
            {
                Id = id,
                Name = item.GetString("name", string.Empty),
                Grade = item.GetString("grade", string.Empty),
                EffectSummary = item.GetString("effectSummary", string.Empty),
                HitCount = 1,
                CooldownSeconds = 2f
            };

            if (item.TryGetProperty("attackBonusPercent", out var ab) && ab.Kind == JsonValueKind.Number)
                art.AttackBonusPercent = ab.Number;
            if (item.TryGetProperty("damageFlat", out var df) && df.Kind == JsonValueKind.Number)
                art.DamageFlat = (int)df.Number;
            if (item.TryGetProperty("damageAttackMult", out var dm) && dm.Kind == JsonValueKind.Number)
                art.DamageAttackMult = dm.Number;
            if (item.TryGetProperty("hitCount", out var hc) && hc.Kind == JsonValueKind.Number)
                art.HitCount = (int)hc.Number < 1 ? 1 : (int)hc.Number;
            if (item.TryGetProperty("cooldownSeconds", out var cd) && cd.Kind == JsonValueKind.Number)
                art.CooldownSeconds = (float)cd.Number;

            if (item.TryGetProperty("mastery", out var masteryNode))
            {
                if (!SkillMasteryProfileParser.TryParse(
                        masteryNode, id.ToString(), report, out var masteryDef))
                    return;
                art.Mastery = masteryDef;
            }

            ReadTags(item, art.Tags, report, id.ToString());
            if (report.Errors.Count > errorsBefore)
                return;

            var regArt = registry.RegisterCombatArt(art);
            if (regArt.IsFailure)
                report.Add(regArt.Error);
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
                MaxStack = maxStack,
                TeachesManualId = item.GetString("teachesManualId", string.Empty),
                TeachesArtId = item.GetString("teachesArtId", string.Empty)
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
                OpeningLocalPlaceSetId = item.GetString("openingLocalPlaceSetId", string.Empty),
                OpeningWorldGraphId = item.GetString("openingWorldGraphId", string.Empty),
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
                        AiRole = spawnNode.GetString("aiRole", string.Empty),
                        JobId = spawnNode.GetString("jobId", string.Empty)
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

        static void LoadCharacterRoster(
            JsonValue item,
            DefinitionId id,
            DefinitionRegistry registry,
            ValidationReport report)
        {
            var errorsBefore = report.Errors.Count;
            DefinitionSchema.RejectUnknownFields(item, DefinitionSchema.CharacterRosterFields, report, id.ToString());
            if (report.Errors.Count > errorsBefore)
                return;

            var roster = new CharacterRosterDefinition
            {
                Id = id,
                Name = item.GetString("name", string.Empty)
            };

            if (!item.TryGetProperty("entries", out var entriesNode) || entriesNode.Kind != JsonValueKind.Array)
            {
                report.Add(ErrorCode.MissingRequiredField, "characterRoster.entries required array.", id.ToString());
                return;
            }

            foreach (var spawnNode in entriesNode.Array)
            {
                if (spawnNode.Kind != JsonValueKind.Object)
                {
                    report.Add(ErrorCode.ContentLoadFailed, "roster entries must be objects.", id.ToString());
                    continue;
                }

                DefinitionSchema.RejectUnknownFields(
                    spawnNode,
                    DefinitionSchema.OpeningSpawnFields,
                    report,
                    id + ".entry");
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
                    AiRole = spawnNode.GetString("aiRole", string.Empty),
                    JobId = spawnNode.GetString("jobId", string.Empty)
                };
                if (string.IsNullOrWhiteSpace(entry.DefinitionId))
                {
                    report.Add(ErrorCode.MissingRequiredField, "roster.entry.definitionId required.", id.ToString());
                    return;
                }

                roster.Entries.Add(entry);
            }

            if (roster.Entries.Count == 0)
            {
                report.Add(ErrorCode.MissingRequiredField, "characterRoster.entries must be non-empty.", id.ToString());
                return;
            }

            var reg = registry.RegisterCharacterRoster(roster);
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

        static bool ReadBool(JsonValue obj, string name, bool fallback)
        {
            if (!obj.TryGetProperty(name, out var n) || n.Kind != JsonValueKind.Boolean)
                return fallback;
            return n.Bool;
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
                    PresentationZ = ReadFloat(locNode, "presentationZ", 0f),
                    LocalMapId = locNode.GetString("localMapId", string.Empty),
                    EnterLocalMapId = locNode.GetString("enterLocalMapId", string.Empty),
                    EnterSpawnLocationId = locNode.GetString("enterSpawnLocationId", string.Empty),
                    SurveySenseRequired = ReadInt(locNode, "surveySenseRequired", 0)
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
                ReadTags(locNode, entry.Tags, report, id + "." + entry.Id);
                ReadStringList(
                    locNode, "allowedActivities", entry.AllowedActivities, report, id + "." + entry.Id);

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

        static void LoadLocalPlaceSet(
            JsonValue item,
            DefinitionId id,
            DefinitionRegistry registry,
            ValidationReport report)
        {
            var errorsBefore = report.Errors.Count;
            DefinitionSchema.RejectUnknownFields(item, DefinitionSchema.LocalPlaceSetFields, report, id.ToString());
            if (report.Errors.Count > errorsBefore)
                return;

            var set = new LocalPlaceSetDefinition
            {
                Id = id,
                Name = item.GetString("name", string.Empty),
                MapLayoutId = item.GetString("mapLayoutId", string.Empty),
                StartLocationId = item.GetString("startLocationId", string.Empty)
            };

            if (string.IsNullOrWhiteSpace(set.MapLayoutId))
            {
                report.Add(ErrorCode.MissingRequiredField, "localPlaceSet.mapLayoutId required.", id.ToString());
                return;
            }

            if (!item.TryGetProperty("locations", out var locs) || locs.Kind != JsonValueKind.Array)
            {
                report.Add(ErrorCode.MissingRequiredField, "localPlaceSet.locations required.", id.ToString());
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
                    PresentationZ = ReadFloat(locNode, "presentationZ", 0f),
                    LocalMapId = locNode.GetString("localMapId", string.Empty),
                    EnterLocalMapId = locNode.GetString("enterLocalMapId", string.Empty),
                    EnterSpawnLocationId = locNode.GetString("enterSpawnLocationId", string.Empty),
                    SurveySenseRequired = ReadInt(locNode, "surveySenseRequired", 0)
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
                ReadTags(locNode, entry.Tags, report, id + "." + entry.Id);
                ReadStringList(
                    locNode, "allowedActivities", entry.AllowedActivities, report, id + "." + entry.Id);

                set.Locations.Add(entry);
            }

            if (set.Locations.Count == 0)
            {
                report.Add(ErrorCode.MissingRequiredField, "localPlaceSet.locations empty.", id.ToString());
                return;
            }

            var reg = registry.RegisterLocalPlaceSet(set);
            if (reg.IsFailure)
                report.Add(reg.Error);
        }

        static void LoadWorkArea(
            JsonValue item,
            DefinitionId id,
            DefinitionRegistry registry,
            ValidationReport report)
        {
            var errorsBefore = report.Errors.Count;
            DefinitionSchema.RejectUnknownFields(item, DefinitionSchema.WorkAreaFields, report, id.ToString());
            if (report.Errors.Count > errorsBefore)
                return;

            var area = new WorkAreaContentDefinition
            {
                Id = id,
                Name = item.GetString("name", string.Empty),
                LocationId = item.GetString("locationId", string.Empty),
                OffsetX = ReadFloat(item, "offsetX", 0f),
                OffsetZ = ReadFloat(item, "offsetZ", 0f),
                Capacity = Math.Max(1, ReadInt(item, "capacity", 4)),
                IsControlCore = item.GetBool("isControlCore", false),
                MaxDurability = Math.Max(0, ReadInt(item, "maxDurability", 0)),
                Defense = Math.Max(0, ReadInt(item, "defense", 0)),
                OccupyHoldSeconds = Math.Max(0.1f, ReadFloat(item, "occupyHoldSeconds", 10f))
            };
            if (string.IsNullOrWhiteSpace(area.LocationId))
            {
                report.Add(ErrorCode.MissingRequiredField, "workArea.locationId required.", id.ToString());
                return;
            }

            ReadTags(item, area.Tags, report, id.ToString());
            ReadStringList(item, "allowedActivities", area.AllowedActivities, report, id.ToString());
            ReadNamedTagArray(item, "residentTags", area.ResidentTags, report, id.ToString());
            ReadNamedTagArray(item, "grantsPrivileges", area.GrantsPrivileges, report, id.ToString());

            var reg = registry.RegisterWorkArea(area);
            if (reg.IsFailure)
                report.Add(reg.Error);
        }

        static void LoadJob(
            JsonValue item,
            DefinitionId id,
            DefinitionRegistry registry,
            ValidationReport report)
        {
            var errorsBefore = report.Errors.Count;
            DefinitionSchema.RejectUnknownFields(item, DefinitionSchema.JobFields, report, id.ToString());
            if (report.Errors.Count > errorsBefore)
                return;

            var job = new JobContentDefinition
            {
                Id = id,
                Name = item.GetString("name", string.Empty),
                PrimaryWorkAreaId = item.GetString("primaryWorkAreaId", string.Empty)
            };

            if (item.TryGetProperty("activityBindings", out var bindingsNode))
            {
                if (bindingsNode.Kind != JsonValueKind.Array)
                {
                    report.Add(ErrorCode.ContentLoadFailed, "job.activityBindings must be array.", id.ToString());
                    return;
                }

                foreach (var bindNode in bindingsNode.Array)
                {
                    if (bindNode.Kind != JsonValueKind.Object)
                    {
                        report.Add(ErrorCode.ContentLoadFailed, "activityBindings entries must be objects.", id.ToString());
                        continue;
                    }

                    DefinitionSchema.RejectUnknownFields(
                        bindNode, DefinitionSchema.JobActivityBindingFields, report, id + ".binding");
                    if (report.Errors.Count > errorsBefore)
                        return;

                    var binding = new JobActivityBindingEntry
                    {
                        Activity = bindNode.GetString("activity", string.Empty),
                        Mode = bindNode.GetString("mode", "single")
                    };
                    ReadStringList(bindNode, "workAreaIds", binding.WorkAreaIds, report, id + ".binding");
                    if (string.IsNullOrWhiteSpace(binding.Activity) || binding.WorkAreaIds.Count == 0)
                    {
                        report.Add(
                            ErrorCode.MissingRequiredField,
                            "activityBinding.activity and workAreaIds required.",
                            id.ToString());
                        return;
                    }

                    job.ActivityBindings.Add(binding);
                }
            }

            if (job.ActivityBindings.Count == 0)
            {
                report.Add(ErrorCode.MissingRequiredField, "job.activityBindings required.", id.ToString());
                return;
            }

            var reg = registry.RegisterJob(job);
            if (reg.IsFailure)
                report.Add(reg.Error);
        }

        static void LoadSchedule(
            JsonValue item,
            DefinitionId id,
            DefinitionRegistry registry,
            ValidationReport report)
        {
            var errorsBefore = report.Errors.Count;
            DefinitionSchema.RejectUnknownFields(item, DefinitionSchema.ScheduleFields, report, id.ToString());
            if (report.Errors.Count > errorsBefore)
                return;

            var schedule = new ScheduleContentDefinition
            {
                Id = id,
                Name = item.GetString("name", string.Empty)
            };

            if (!item.TryGetProperty("blocks", out var blocksNode) || blocksNode.Kind != JsonValueKind.Array)
            {
                report.Add(ErrorCode.MissingRequiredField, "schedule.blocks required.", id.ToString());
                return;
            }

            foreach (var blockNode in blocksNode.Array)
            {
                if (blockNode.Kind != JsonValueKind.Object)
                {
                    report.Add(ErrorCode.ContentLoadFailed, "schedule.blocks entries must be objects.", id.ToString());
                    continue;
                }

                DefinitionSchema.RejectUnknownFields(
                    blockNode, DefinitionSchema.ScheduleBlockFields, report, id + ".block");
                if (report.Errors.Count > errorsBefore)
                    return;

                var entry = new ScheduleBlockEntry
                {
                    StartTick = ReadInt(blockNode, "startTick", 0),
                    EndTick = ReadInt(blockNode, "endTick", 0),
                    Activity = blockNode.GetString("activity", string.Empty),
                    OrderDurationTicks = (ulong)System.Math.Max(0, ReadInt(blockNode, "orderDurationTicks", 6))
                };
                if (string.IsNullOrWhiteSpace(entry.Activity))
                {
                    report.Add(ErrorCode.MissingRequiredField, "schedule.block.activity required.", id.ToString());
                    return;
                }

                schedule.Blocks.Add(entry);
            }

            if (schedule.Blocks.Count == 0)
            {
                report.Add(ErrorCode.MissingRequiredField, "schedule.blocks empty.", id.ToString());
                return;
            }

            var reg = registry.RegisterSchedule(schedule);
            if (reg.IsFailure)
                report.Add(reg.Error);
        }

        static void LoadMapLayout(
            JsonValue item,
            DefinitionId id,
            DefinitionRegistry registry,
            ValidationReport report)
        {
            var errorsBefore = report.Errors.Count;
            DefinitionSchema.RejectUnknownFields(item, DefinitionSchema.MapLayoutFields, report, id.ToString());
            if (report.Errors.Count > errorsBefore)
                return;

            var layout = new MapLayoutDefinition
            {
                Id = id,
                Name = item.GetString("name", string.Empty),
                WorldRegionId = item.GetString("worldRegionId", string.Empty),
                OriginX = ReadFloat(item, "originX", 0f),
                OriginY = ReadFloat(item, "originY", 0f),
                CellSize = ReadFloat(item, "cellSize", 1f),
                Width = ReadInt(item, "width", 0),
                Height = ReadInt(item, "height", 0)
            };

            if (layout.Width <= 0 || layout.Height <= 0 || layout.CellSize <= 0f)
            {
                report.Add(
                    ErrorCode.MissingRequiredField,
                    "mapLayout.width/height/cellSize must be positive.",
                    id.ToString());
                return;
            }

            if (item.TryGetProperty("placements", out var placementsNode))
            {
                if (placementsNode.Kind != JsonValueKind.Array)
                {
                    report.Add(ErrorCode.ContentLoadFailed, "mapLayout.placements must be array.", id.ToString());
                    return;
                }

                foreach (var pNode in placementsNode.Array)
                {
                    if (pNode.Kind != JsonValueKind.Object)
                    {
                        report.Add(ErrorCode.ContentLoadFailed, "placement entries must be objects.", id.ToString());
                        continue;
                    }

                    DefinitionSchema.RejectUnknownFields(
                        pNode, DefinitionSchema.MapPlacementFields, report, id + ".placement");
                    if (report.Errors.Count > errorsBefore)
                        return;

                    var placement = new MapPlacement
                    {
                        Id = pNode.GetString("id", string.Empty),
                        Kind = pNode.GetString("kind", "wall"),
                        X = ReadInt(pNode, "x", 0),
                        Y = ReadInt(pNode, "y", 0),
                        W = ReadInt(pNode, "w", 1),
                        H = ReadInt(pNode, "h", 1),
                        BlocksMovement = ReadBool(pNode, "blocksMovement", false),
                        BoundLocationId = pNode.GetString("boundLocationId", string.Empty),
                        Label = pNode.GetString("label", string.Empty),
                        LootItemId = pNode.GetString("lootItemId", string.Empty),
                        SpawnTableId = pNode.GetString("spawnTableId", string.Empty),
                        SpawnCount = ReadInt(pNode, "spawnCount", 0)
                    };

                    if (string.IsNullOrWhiteSpace(placement.Id))
                    {
                        report.Add(ErrorCode.MissingRequiredField, "placement.id required.", id.ToString());
                        return;
                    }

                    layout.Placements.Add(placement);
                }
            }

            var reg = registry.RegisterMapLayout(layout);
            if (reg.IsFailure)
                report.Add(reg.Error);
        }

        static void LoadSpawnTable(
            JsonValue item,
            DefinitionId id,
            DefinitionRegistry registry,
            ValidationReport report)
        {
            var errorsBefore = report.Errors.Count;
            DefinitionSchema.RejectUnknownFields(item, DefinitionSchema.SpawnTableFields, report, id.ToString());
            if (report.Errors.Count > errorsBefore)
                return;

            var table = new SpawnTableDefinition
            {
                Id = id,
                Name = item.GetString("name", string.Empty)
            };

            if (item.TryGetProperty("entries", out var entriesNode))
            {
                if (entriesNode.Kind != JsonValueKind.Array)
                {
                    report.Add(ErrorCode.ContentLoadFailed, "spawnTable.entries must be array.", id.ToString());
                    return;
                }

                foreach (var eNode in entriesNode.Array)
                {
                    if (eNode.Kind != JsonValueKind.Object)
                        continue;
                    DefinitionSchema.RejectUnknownFields(
                        eNode, DefinitionSchema.SpawnTableEntryFields, report, id + ".entry");
                    if (report.Errors.Count > errorsBefore)
                        return;

                    var entry = new SpawnTableEntry
                    {
                        DefinitionId = eNode.GetString("definitionId", string.Empty),
                        Weight = ReadInt(eNode, "weight", 1),
                        CountMin = ReadInt(eNode, "countMin", 1),
                        CountMax = ReadInt(eNode, "countMax", 1)
                    };
                    if (string.IsNullOrWhiteSpace(entry.DefinitionId))
                    {
                        report.Add(ErrorCode.MissingRequiredField, "spawnTable.entry.definitionId required.", id.ToString());
                        return;
                    }

                    if (entry.Weight < 1)
                        entry.Weight = 1;
                    if (entry.CountMin < 0)
                        entry.CountMin = 0;
                    if (entry.CountMax < entry.CountMin)
                        entry.CountMax = entry.CountMin;
                    table.Entries.Add(entry);
                }
            }

            if (table.Entries.Count == 0)
            {
                report.Add(ErrorCode.MissingRequiredField, "spawnTable.entries required.", id.ToString());
                return;
            }

            var reg = registry.RegisterSpawnTable(table);
            if (reg.IsFailure)
                report.Add(reg.Error);
        }

        static void LoadWorldGraph(
            JsonValue item,
            DefinitionId id,
            DefinitionRegistry registry,
            ValidationReport report)
        {
            var errorsBefore = report.Errors.Count;
            DefinitionSchema.RejectUnknownFields(item, DefinitionSchema.WorldGraphFields, report, id.ToString());
            if (report.Errors.Count > errorsBefore)
                return;

            var graph = new WorldGraphDefinition
            {
                Id = id,
                Name = item.GetString("name", string.Empty),
                StartNodeId = item.GetString("startNodeId", string.Empty)
            };

            if (!item.TryGetProperty("nodes", out var nodesNode) || nodesNode.Kind != JsonValueKind.Array)
            {
                report.Add(ErrorCode.MissingRequiredField, "worldGraph.nodes required.", id.ToString());
                return;
            }

            foreach (var nNode in nodesNode.Array)
            {
                if (nNode.Kind != JsonValueKind.Object)
                    continue;
                DefinitionSchema.RejectUnknownFields(
                    nNode, DefinitionSchema.WorldNodeFields, report, id + ".node");
                if (report.Errors.Count > errorsBefore)
                    return;

                var node = new WorldNodeEntry
                {
                    Id = nNode.GetString("id", string.Empty),
                    Name = nNode.GetString("name", string.Empty),
                    Kind = nNode.GetString("kind", string.Empty),
                    LocalMapId = nNode.GetString("localMapId", string.Empty),
                    WorldX = ReadFloat(nNode, "worldX", 0f),
                    WorldY = ReadFloat(nNode, "worldY", 0f),
                    OwnerId = nNode.GetString("ownerId", string.Empty),
                    State = nNode.GetString("state", string.Empty)
                };
                if (string.IsNullOrWhiteSpace(node.Id))
                {
                    report.Add(ErrorCode.MissingRequiredField, "worldGraph.node.id required.", id.ToString());
                    return;
                }

                if (nNode.TryGetProperty("tags", out var tagsNode) && tagsNode.Kind == JsonValueKind.Array)
                {
                    foreach (var t in tagsNode.Array)
                    {
                        if (t.Kind == JsonValueKind.String && !string.IsNullOrWhiteSpace(t.String))
                            node.Tags.Add(t.String);
                    }
                }

                graph.Nodes.Add(node);
            }

            if (graph.Nodes.Count == 0)
            {
                report.Add(ErrorCode.MissingRequiredField, "worldGraph.nodes empty.", id.ToString());
                return;
            }

            if (string.IsNullOrWhiteSpace(graph.StartNodeId))
            {
                report.Add(ErrorCode.MissingRequiredField, "worldGraph.startNodeId required.", id.ToString());
                return;
            }

            if (item.TryGetProperty("routes", out var routesNode))
            {
                if (routesNode.Kind != JsonValueKind.Array)
                {
                    report.Add(ErrorCode.ContentLoadFailed, "worldGraph.routes must be array.", id.ToString());
                    return;
                }

                foreach (var rNode in routesNode.Array)
                {
                    if (rNode.Kind != JsonValueKind.Object)
                        continue;
                    DefinitionSchema.RejectUnknownFields(
                        rNode, DefinitionSchema.WorldRouteFields, report, id + ".route");
                    if (report.Errors.Count > errorsBefore)
                        return;

                    var route = new WorldRouteEntry
                    {
                        Id = rNode.GetString("id", string.Empty),
                        FromNodeId = rNode.GetString("fromNodeId", string.Empty),
                        ToNodeId = rNode.GetString("toNodeId", string.Empty),
                        Kind = rNode.GetString("kind", string.Empty),
                        TravelCost = ReadInt(rNode, "travelCost", 0),
                        Danger = ReadFloat(rNode, "danger", 0f),
                        OwnerId = rNode.GetString("ownerId", string.Empty),
                        State = rNode.GetString("state", string.Empty),
                        Directed = ReadBool(rNode, "directed", false),
                        EncounterPoolId = rNode.GetString("encounterPoolId", string.Empty)
                    };
                    if (string.IsNullOrWhiteSpace(route.Id) ||
                        string.IsNullOrWhiteSpace(route.FromNodeId) ||
                        string.IsNullOrWhiteSpace(route.ToNodeId))
                    {
                        report.Add(
                            ErrorCode.MissingRequiredField,
                            "worldGraph.route id／fromNodeId／toNodeId required.",
                            id.ToString());
                        return;
                    }

                    ReadConditions(
                        rNode,
                        "traversalRequirements",
                        route.TraversalRequirements,
                        report,
                        id + "." + route.Id);
                    graph.Routes.Add(route);
                }
            }

            var reg = registry.RegisterWorldGraph(graph);
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
                AutoOffer = item.GetBool("autoOffer", false),
                Abandonable = item.GetBool("abandonable", false),
                DeadlineDays = (int)item.GetNumber("deadlineDays", 0)
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
                NpcDefinitionId = item.GetString("npcDefinitionId", string.Empty),
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
                    Realm = node.GetString("realm", string.Empty),
                    CharacterId = node.GetString("characterId", string.Empty)
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
                var toDefinitionIds = new List<string>();
                ReadStringList(node, "toDefinitionIds", toDefinitionIds, report, context + "." + field);
                var o = new ContentOutcome
                {
                    Kind = node.GetString("kind", string.Empty),
                    Id = node.GetString("id", string.Empty),
                    Amount = ReadInt(node, "amount", 0),
                    FromDefinitionId = node.GetString("fromDefinitionId", string.Empty),
                    ToDefinitionId = node.GetString("toDefinitionId", string.Empty)
                };
                o.ToDefinitionIds.AddRange(toDefinitionIds);
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

        static void ReadBoolMap(
            JsonValue item,
            string field,
            Dictionary<string, bool> map,
            ValidationReport report,
            string context)
        {
            if (!item.TryGetProperty(field, out var node))
                return;
            if (node.Kind != JsonValueKind.Object)
            {
                report.Add(ErrorCode.ContentLoadFailed, field + " must be object.", context);
                return;
            }

            foreach (var kv in node.Object)
            {
                if (kv.Value.Kind != JsonValueKind.Boolean)
                {
                    report.Add(ErrorCode.ContentLoadFailed, field + " values must be bool.", context + "." + kv.Key);
                    continue;
                }

                map[kv.Key] = kv.Value.Bool;
            }
        }

        static void ReadIntMap(
            JsonValue item,
            string field,
            Dictionary<string, int> map,
            ValidationReport report,
            string context)
        {
            if (!item.TryGetProperty(field, out var node))
                return;
            if (node.Kind != JsonValueKind.Object)
            {
                report.Add(ErrorCode.ContentLoadFailed, field + " must be object.", context);
                return;
            }

            foreach (var kv in node.Object)
            {
                if (kv.Value.Kind != JsonValueKind.Number)
                {
                    report.Add(ErrorCode.ContentLoadFailed, field + " values must be number.", context + "." + kv.Key);
                    continue;
                }

                map[kv.Key] = (int)kv.Value.Number;
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
