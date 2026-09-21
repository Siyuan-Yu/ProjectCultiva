using System;
using System.Collections.Generic;
using System.IO;
using XianXia.Core.Content;
using XianXia.Core.Domain;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Core.Social;
using XianXia.Core.World.Surface;
using XianXia.Data.Content.Compatibility;
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

            // Strategic Faction Content → Core presentation 单点安装。registry 无 strategicFaction
            // 定义（如仅含 character 的临时包）→ ResetInstall 回 fallback，不残留上次状态。
            StrategicFactionContentInstaller.Install(registry);
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
                    case "building":
                        LoadBuilding(item, parsed.Value, registry, report);
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
                    case "worldSiteEconomy":
                        LoadWorldSiteEconomy(item, parsed.Value, registry, report);
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
                    case "outdoorSurface":
                        LoadOutdoorSurface(item, parsed.Value, registry, report);
                        break;
                    case "worldSpatialRules":
                        LoadWorldSpatialRules(item, parsed.Value, registry, report);
                        break;
                    case "outdoorSurfaceGeography":
                        LoadOutdoorSurfaceGeography(item, parsed.Value, registry, report);
                        break;
                    case "continuousSurfaceWorldMap":
                        LoadContinuousSurfaceWorldMap(item, parsed.Value, registry, report);
                        break;
                    case "spawnTable":
                        LoadSpawnTable(item, parsed.Value, registry, report);
                        break;
                    case "hexWorld":
                        LoadHexWorldContent(item, parsed.Value, registry, report);
                        break;
                    case "realmLadder":
                        LoadRealmLadder(item, parsed.Value, registry, report);
                        break;
                    case "formalArmy":
                        LoadLegacyFormalArmy(item, parsed.Value, registry, report);
                        break;
                    case "npcSquad":
                        LoadNpcSquad(item, parsed.Value, registry, report);
                        break;
                    case "strategicFaction":
                        LoadStrategicFaction(item, parsed.Value, registry, report);
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
                PlayerControllable = item.GetBool("playerControllable", false),
                DefaultFactionId = item.GetString("defaultFactionId", string.Empty),
                DefaultFactionRole = item.GetString("defaultFactionRole", string.Empty)
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

        static void LoadBuilding(
            JsonValue item,
            DefinitionId id,
            DefinitionRegistry registry,
            ValidationReport report)
        {
            var errorsBefore = report.Errors.Count;
            DefinitionSchema.RejectUnknownFields(item, DefinitionSchema.BuildingFields, report, id.ToString());
            if (report.Errors.Count > errorsBefore)
                return;

            var placementKind = item.GetString("placementKind", string.Empty);
            if (string.IsNullOrWhiteSpace(placementKind))
                report.Add(ErrorCode.MissingRequiredField, "placementKind required.", id.ToString());

            var unlocked = false;
            if (item.TryGetProperty("unlockedByDefault", out var unlockedNode))
            {
                if (unlockedNode.Kind != JsonValueKind.Boolean)
                    report.Add(ErrorCode.ContentLoadFailed, "unlockedByDefault must be boolean.", id.ToString());
                else
                    unlocked = unlockedNode.Bool;
            }

            float refundRate = 0f;
            if (!item.TryGetProperty("dismantleRefundRate", out var refundNode) ||
                refundNode.Kind != JsonValueKind.Number)
                report.Add(ErrorCode.MissingRequiredField, "dismantleRefundRate number required.", id.ToString());
            else
            {
                refundRate = (float)refundNode.Number;
                if (refundRate < 0f || refundRate > 1f)
                    report.Add(ErrorCode.InvalidArgument, "dismantleRefundRate must be in [0,1].", id.ToString());
            }

            var definition = new BuildingDefinition
            {
                Id = id,
                Name = item.GetString("name", id.ToString()),
                Description = item.GetString("description", string.Empty),
                UnlockedByDefault = unlocked,
                PlacementKind = placementKind,
                OutdoorKind = item.GetString("outdoorKind", string.Empty),
                FootprintCellsW = ReadPositiveInt(item, "footprintCellsW", report, id.ToString(),
                    placementKind == "farmField" || placementKind == "recoverySpot" || placementKind == "storageRoom"),
                FootprintCellsH = ReadPositiveInt(item, "footprintCellsH", report, id.ToString(),
                    placementKind == "farmField" || placementKind == "recoverySpot" || placementKind == "storageRoom"),
                CreatesWorldSite = item.GetBool("createsWorldSite", false),
                CreatedSiteName = item.GetString("createdSiteName", string.Empty),
                CreatedSiteType = item.GetString("createdSiteType", string.Empty),
                InitialSiteLevel = (int)item.GetNumber("initialSiteLevel", 0),
                DismantleRefundRate = refundRate
            };
            if (string.IsNullOrWhiteSpace(definition.Name))
                definition.Name = id.ToString();
            if (definition.CreatesWorldSite &&
                (definition.InitialSiteLevel < 1 || string.IsNullOrWhiteSpace(definition.CreatedSiteType)))
            {
                report.Add(ErrorCode.InvalidArgument,
                    "WorldSite core building requires positive level/range and createdSiteType.", id.ToString());
            }

            if (!item.TryGetProperty("costs", out var costsNode) || costsNode.Kind != JsonValueKind.Array)
                report.Add(ErrorCode.MissingRequiredField, "costs array required.", id.ToString());
            else
            {
                for (var i = 0; i < costsNode.Array.Count; i++)
                {
                    var costNode = costsNode.Array[i];
                    var context = id + ".costs[" + i + "]";
                    if (costNode.Kind != JsonValueKind.Object)
                    {
                        report.Add(ErrorCode.ContentLoadFailed, "building cost must be object.", context);
                        continue;
                    }
                    DefinitionSchema.RejectUnknownFields(
                        costNode, DefinitionSchema.BuildingCostFields, report, context);
                    var itemId = costNode.GetString("itemId", string.Empty);
                    if (string.IsNullOrWhiteSpace(itemId))
                    {
                        report.Add(ErrorCode.MissingRequiredField, "building cost itemId required.", context);
                        continue;
                    }
                    if (!costNode.TryGetProperty("count", out var countNode) || countNode.Kind != JsonValueKind.Number)
                    {
                        report.Add(ErrorCode.MissingRequiredField, "building cost count number required.", context);
                        continue;
                    }
                    var count = (int)countNode.Number;
                    if (count <= 0 || Math.Abs(countNode.Number - count) > 0.00001d)
                    {
                        report.Add(ErrorCode.InvalidArgument, "building cost count must be a positive integer.", context);
                        continue;
                    }
                    definition.Costs.Add(new BuildingMaterialCostDefinition { ItemId = itemId, Count = count });
                }
            }

            if (report.Errors.Count > errorsBefore)
                return;
            var registered = registry.RegisterBuilding(definition);
            if (registered.IsFailure)
                report.Add(registered.Error);
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
                OpeningWorldRegionId = item.GetString("openingWorldRegionId", string.Empty),
                OpeningLocalPlaceSetId = item.GetString("openingLocalPlaceSetId", string.Empty),
                OpeningHexWorldId = item.GetString("openingHexWorldId", string.Empty),
                OpeningSurfaceId = item.GetString("openingSurfaceId", string.Empty),
                OpeningChapterId = item.GetString("openingChapterId", string.Empty)
            };
            if (item.TryGetProperty("startingInventory", out var inventoryNode))
            {
                if (inventoryNode.Kind != JsonValueKind.Array)
                    report.Add(ErrorCode.InvalidArgument, "startingInventory must be array.", id.ToString());
                else foreach (var entry in inventoryNode.Array)
                {
                    if (entry.Kind != JsonValueKind.Object)
                    { report.Add(ErrorCode.InvalidArgument, "startingInventory entry must be object.", id.ToString()); continue; }
                    DefinitionSchema.RejectUnknownFields(entry, DefinitionSchema.BuildingCostFields, report, id.ToString());
                    scenario.StartingInventory.Add(new OpeningStartingInventoryEntry {
                        ItemId = entry.GetString("itemId", string.Empty),
                        Count = ReadPositiveInt(entry, "count", report, id.ToString(), true)
                    });
                }
            }
            if (item.TryGetProperty("strategicOpening", out var strategicNode))
            {
                if (strategicNode.Kind != JsonValueKind.Object)
                {
                    report.Add(ErrorCode.ContentLoadFailed, "strategicOpening must be object.", id.ToString());
                    return;
                }
                DefinitionSchema.RejectUnknownFields(strategicNode, DefinitionSchema.OpeningStrategicFields, report, id + ".strategicOpening");
                if (report.Errors.Count > errorsBefore) return;
                var opening = new OpeningStrategicStateDefinition { PlayerFactionId = strategicNode.GetString("playerFactionId", string.Empty) };
                if (string.IsNullOrWhiteSpace(opening.PlayerFactionId)) { report.Add(ErrorCode.MissingRequiredField, "strategicOpening.playerFactionId required.", id.ToString()); return; }
                ReadStrategicList(strategicNode, "vassalages", DefinitionSchema.OpeningVassalageFields, id, report, (n) => opening.Vassalages.Add(new OpeningVassalageDefinition { VassalFactionId = n.GetString("vassalFactionId", ""), OverlordFactionId = n.GetString("overlordFactionId", "") }));
                ReadStrategicList(strategicNode, "alliances", DefinitionSchema.OpeningAllianceFields, id, report, (n) => opening.Alliances.Add(new OpeningAllianceDefinition { FactionAId = n.GetString("factionAId", ""), FactionBId = n.GetString("factionBId", "") }));
                ReadStrategicList(strategicNode, "initialWars", DefinitionSchema.OpeningWarFields, id, report, (n) => opening.InitialWars.Add(new OpeningWarDefinition { DeclarerFactionId = n.GetString("declarerFactionId", ""), TargetFactionId = n.GetString("targetFactionId", "") }));
                if (report.Errors.Count > errorsBefore) return;
                scenario.StrategicOpening = opening;
            }

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
                        FactionMode = ReadFactionMode(spawnNode, id + ".spawn", report, errorsBefore, out var modeExplicit),
                        FactionModeExplicit = modeExplicit,
                        FactionId = spawnNode.GetString("factionId", string.Empty),
                        FactionRole = spawnNode.GetString("factionRole", string.Empty),
                        BindSchedule = spawnNode.GetBool("bindSchedule", true),
                        BindDailyTask = spawnNode.GetBool("bindDailyTask", true),
                        Recruitable = spawnNode.GetBool("recruitable", false),
                        ScheduleId = spawnNode.GetString("scheduleId", string.Empty),
                        AiRole = spawnNode.GetString("aiRole", string.Empty),
                        JobId = spawnNode.GetString("jobId", string.Empty),
                        WorldSiteId = spawnNode.GetString("worldSiteId", string.Empty),
                        LocalLocationId = spawnNode.GetString("localLocationId", string.Empty)
                    };
                    if (report.Errors.Count > errorsBefore)
                        return;
                    if (!TryReadOpeningLocalPosition(spawnNode, id + ".spawn", report, out var localPosition))
                        return;
                    entry.LocalPosition = localPosition;
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

            if (item.TryGetProperty("openingBonds", out var bondNode))
            {
                if (bondNode.Kind != JsonValueKind.Array)
                {
                    report.Add(ErrorCode.ContentLoadFailed, "openingBonds must be array.", id.ToString());
                    return;
                }

                foreach (var edge in bondNode.Array)
                {
                    if (edge.Kind != JsonValueKind.Object)
                    {
                        report.Add(ErrorCode.ContentLoadFailed, "openingBonds entries must be objects.", id.ToString());
                        continue;
                    }
                    DefinitionSchema.RejectUnknownFields(
                        edge, DefinitionSchema.OpeningBondFields, report, id + ".bond");
                    if (report.Errors.Count > errorsBefore)
                        return;
                    var kindText = edge.GetString("kind", string.Empty);
                    if (!System.Enum.TryParse(kindText, false, out SocialBondKind kind) ||
                        !System.Enum.IsDefined(typeof(SocialBondKind), kind) ||
                        !string.Equals(kind.ToString(), kindText, StringComparison.Ordinal))
                    {
                        report.Add(ErrorCode.ContentLoadFailed, "openingBond.kind invalid.", id + ":" + kindText);
                        return;
                    }
                    scenario.OpeningBonds.Add(new OpeningBondEntry
                    {
                        Kind = kind,
                        FromDefinitionId = edge.GetString("fromDefinitionId", string.Empty),
                        ToDefinitionId = edge.GetString("toDefinitionId", string.Empty)
                    });
                }
            }

            if (scenario.Spawns.Count == 0)
            {
                report.Add(ErrorCode.MissingRequiredField, "openingScenario.spawns required.", id.ToString());
                return;
            }

            if (item.TryGetProperty("initialFormalArmyIds", out var armyIdsNode))
            {
                if (armyIdsNode.Kind != JsonValueKind.Array)
                {
                    report.Add(ErrorCode.ContentLoadFailed, "initialFormalArmyIds must be array.", id.ToString());
                    return;
                }

                foreach (var armyIdNode in armyIdsNode.Array)
                {
                    if (armyIdNode.Kind != JsonValueKind.String || string.IsNullOrWhiteSpace(armyIdNode.String))
                    {
                        report.Add(ErrorCode.ContentLoadFailed, "initialFormalArmyIds entries must be strings.", id.ToString());
                        continue;
                    }

                    scenario.InitialLegacyFormalArmyIds.Add(armyIdNode.String);
                }
            }

            if (item.TryGetProperty("initialNpcSquadIds", out var squadIdsNode))
            {
                if (squadIdsNode.Kind != JsonValueKind.Array)
                {
                    report.Add(ErrorCode.ContentLoadFailed, "initialNpcSquadIds must be array.", id.ToString());
                    return;
                }
                foreach (var squadIdNode in squadIdsNode.Array)
                {
                    if (squadIdNode.Kind != JsonValueKind.String || string.IsNullOrWhiteSpace(squadIdNode.String))
                    {
                        report.Add(ErrorCode.ContentLoadFailed, "initialNpcSquadIds entries must be strings.", id.ToString());
                        continue;
                    }
                    scenario.InitialNpcSquadIds.Add(squadIdNode.String);
                }
            }

            var reg = registry.RegisterOpeningScenario(scenario);
            if (reg.IsFailure)
                report.Add(reg.Error);
        }

        static void ReadStrategicList(
            JsonValue root, string field, System.Collections.Generic.HashSet<string> fields,
            DefinitionId scenarioId, ValidationReport report, System.Action<JsonValue> add)
        {
            if (!root.TryGetProperty(field, out var list)) return;
            if (list.Kind != JsonValueKind.Array) { report.Add(ErrorCode.ContentLoadFailed, field + " must be array.", scenarioId.ToString()); return; }
            foreach (var node in list.Array)
            {
                if (node.Kind != JsonValueKind.Object) { report.Add(ErrorCode.ContentLoadFailed, field + " entries must be objects.", scenarioId.ToString()); continue; }
                DefinitionSchema.RejectUnknownFields(node, fields, report, scenarioId + ".strategicOpening." + field);
                add(node);
            }

        }

        static void LoadStrategicFaction(
            JsonValue item,
            DefinitionId id,
            DefinitionRegistry registry,
            ValidationReport report)
        {
            var errorsBefore = report.Errors.Count;
            DefinitionSchema.RejectUnknownFields(item, DefinitionSchema.StrategicFactionFields, report, id.ToString());
            if (report.Errors.Count > errorsBefore)
                return;

            var name = item.GetString("name", string.Empty);
            var mapColor = item.GetString("mapColor", string.Empty);
            if (string.IsNullOrWhiteSpace(name))
            {
                report.Add(ErrorCode.MissingRequiredField, "strategicFaction.name required.", id.ToString());
                return;
            }

            if (string.IsNullOrWhiteSpace(mapColor))
            {
                report.Add(ErrorCode.MissingRequiredField, "strategicFaction.mapColor required.", id.ToString());
                return;
            }

            if (!XianXia.Core.World.Strategic.StrategicFactionCatalog.TryParseMapColor(
                    mapColor, out _, out _, out _))
            {
                report.Add(
                    ErrorCode.ContentLoadFailed,
                    "strategicFaction.mapColor must be #RRGGBB.",
                    id + ":" + mapColor);
                return;
            }

            var def = new StrategicFactionDefinition
            {
                Id = id,
                Name = name,
                MapColor = mapColor,
                TerritorySelectable = item.GetBool("territorySelectable", true),
                SortOrder = (int)item.GetNumber("sortOrder", 0)
            };

            var registered = registry.RegisterStrategicFaction(def);
            if (registered.IsFailure)
                report.Add(registered.Error);
        }

        static void LoadNpcSquad(
            JsonValue item, DefinitionId id, DefinitionRegistry registry, ValidationReport report)
        {
            var errorsBefore = report.Errors.Count;
            DefinitionSchema.RejectUnknownFields(item, DefinitionSchema.NpcSquadFields, report, id.ToString());
            if (report.Errors.Count > errorsBefore) return;
            var def = new NpcSquadDefinition
            {
                Id = id,
                SquadId = item.GetString("squadId", string.Empty),
                Name = item.GetString("name", string.Empty),
                FactionId = item.GetString("factionId", string.Empty),
                AssemblySiteId = item.GetString("assemblySiteId", string.Empty)
            };
            if (item.TryGetProperty("initialSurfacePosition", out var surfaceNode))
            {
                if (surfaceNode.Kind != JsonValueKind.Object) { report.Add(ErrorCode.ContentLoadFailed, "npcSquad.initialSurfacePosition must be object.", id.ToString()); return; }
                DefinitionSchema.RejectUnknownFields(surfaceNode, DefinitionSchema.NpcSquadInitialSurfacePositionFields, report, id + ".initialSurfacePosition");
                def.InitialSurfacePosition = new NpcSquadInitialSurfacePositionDefinition
                {
                    SurfaceId = surfaceNode.GetString("surfaceId", string.Empty),
                    WorldX = ReadFloat(surfaceNode, "worldX", 0f), WorldY = ReadFloat(surfaceNode, "worldY", 0f)
                };
            }
            if (item.TryGetProperty("initialSurfaceDeployment", out var deploymentNode))
            {
                if (deploymentNode.Kind != JsonValueKind.Object || def.InitialSurfacePosition != null) { report.Add(ErrorCode.ContentLoadFailed, "npcSquad must choose one initial Surface deployment form.", id.ToString()); return; }
                DefinitionSchema.RejectUnknownFields(deploymentNode, DefinitionSchema.NpcSquadInitialSurfaceDeploymentFields, report, id + ".initialSurfaceDeployment");
                if (!deploymentNode.TryGetProperty("offsetCellsX", out var ox) || !deploymentNode.TryGetProperty("offsetCellsY", out var oy) ||
                    ox.Kind != JsonValueKind.Number || oy.Kind != JsonValueKind.Number || ox.Number != Math.Truncate(ox.Number) || oy.Number != Math.Truncate(oy.Number))
                { report.Add(ErrorCode.ContentLoadFailed, "npcSquad.initialSurfaceDeployment requires integer cell offsets.", id.ToString()); return; }
                def.InitialSurfaceDeployment = new NpcSquadInitialSurfaceDeploymentDefinition
                {
                    SurfaceId = deploymentNode.GetString("surfaceId", string.Empty), AnchorSiteId = deploymentNode.GetString("anchorSiteId", string.Empty),
                    OffsetCellsX = ReadInt(deploymentNode, "offsetCellsX", 0), OffsetCellsY = ReadInt(deploymentNode, "offsetCellsY", 0)
                };
            }
            if (string.IsNullOrWhiteSpace(def.SquadId) || string.IsNullOrWhiteSpace(def.FactionId) || string.IsNullOrWhiteSpace(def.AssemblySiteId))
            { report.Add(ErrorCode.MissingRequiredField, "npcSquad.squadId/factionId/assemblySiteId required.", id.ToString()); return; }
            if (!item.TryGetProperty("members", out var membersNode) || membersNode.Kind != JsonValueKind.Array)
            { report.Add(ErrorCode.MissingRequiredField, "npcSquad.members required array.", id.ToString()); return; }
            var leaders = 0;
            foreach (var memberNode in membersNode.Array)
            {
                if (memberNode.Kind != JsonValueKind.Object) { report.Add(ErrorCode.ContentLoadFailed, "npcSquad.members entries must be objects.", id.ToString()); continue; }
                DefinitionSchema.RejectUnknownFields(memberNode, DefinitionSchema.NpcSquadMemberFields, report, id + ".member");
                var member = new NpcSquadMemberDefinition
                {
                    CharacterDefinitionId = memberNode.GetString("characterDefinitionId", string.Empty), DisplayName = memberNode.GetString("displayName", string.Empty),
                    Leader = memberNode.GetBool("leader", false), ReuseOpeningSpawn = memberNode.GetBool("reuseOpeningSpawn", false)
                };
                if (string.IsNullOrWhiteSpace(member.CharacterDefinitionId)) { report.Add(ErrorCode.MissingRequiredField, "npcSquad.member.characterDefinitionId required.", id.ToString()); continue; }
                if (member.Leader) leaders++;
                def.Members.Add(member);
            }
            if (def.Members.Count == 0 || leaders != 1) { report.Add(ErrorCode.InvalidArgument, "npcSquad.members requires a non-empty roster and exactly one leader.", id.ToString()); return; }
            var registered = registry.RegisterNpcSquad(def);
            if (registered.IsFailure) report.Add(registered.Error);
        }

        static void LoadLegacyFormalArmy(
            JsonValue item,
            DefinitionId id,
            DefinitionRegistry registry,
            ValidationReport report)
        {
            var errorsBefore = report.Errors.Count;
            DefinitionSchema.RejectUnknownFields(item, DefinitionSchema.LegacyFormalArmyFields, report, id.ToString());
            if (report.Errors.Count > errorsBefore)
                return;

            var def = new LegacyFormalArmyDefinition
            {
                Id = id,
                Name = item.GetString("name", string.Empty),
                RuntimeArmyId = item.GetString("runtimeArmyId", string.Empty),
                RuntimeStackId = item.GetString("runtimeStackId", string.Empty),
                FactionId = item.GetString("factionId", string.Empty),
                AssemblySiteId = item.GetString("assemblySiteId", string.Empty)
            };

            if (item.TryGetProperty("initialSurfacePosition", out var surfaceNode))
            {
                if (surfaceNode.Kind != JsonValueKind.Object)
                {
                    report.Add(ErrorCode.ContentLoadFailed, "formalArmy.initialSurfacePosition must be object.", id.ToString());
                    return;
                }
                DefinitionSchema.RejectUnknownFields(surfaceNode,
                    DefinitionSchema.LegacyFormalArmyInitialSurfacePositionFields, report, id + ".initialSurfacePosition");
                def.InitialSurfacePosition = new LegacyFormalArmyInitialSurfacePositionDefinition
                {
                    SurfaceId = surfaceNode.GetString("surfaceId", string.Empty),
                    WorldX = ReadFloat(surfaceNode, "worldX", 0f),
                    WorldY = ReadFloat(surfaceNode, "worldY", 0f)
                };
            }

            if (item.TryGetProperty("initialSurfaceDeployment", out var deploymentNode))
            {
                if (deploymentNode.Kind != JsonValueKind.Object || def.InitialSurfacePosition != null)
                {
                    report.Add(ErrorCode.ContentLoadFailed,
                        "formalArmy must choose one initial Surface deployment form.", id.ToString());
                    return;
                }
                DefinitionSchema.RejectUnknownFields(deploymentNode,
                    DefinitionSchema.LegacyFormalArmyInitialSurfaceDeploymentFields,
                    report, id + ".initialSurfaceDeployment");
                if (!deploymentNode.TryGetProperty("offsetCellsX", out var offsetX) ||
                    !deploymentNode.TryGetProperty("offsetCellsY", out var offsetY) ||
                    offsetX.Kind != JsonValueKind.Number ||
                    offsetY.Kind != JsonValueKind.Number ||
                    offsetX.Number != Math.Truncate(offsetX.Number) ||
                    offsetY.Number != Math.Truncate(offsetY.Number) ||
                    offsetX.Number < int.MinValue || offsetX.Number > int.MaxValue ||
                    offsetY.Number < int.MinValue || offsetY.Number > int.MaxValue)
                {
                    report.Add(ErrorCode.ContentLoadFailed,
                        "formalArmy.initialSurfaceDeployment requires integer cell offsets.",
                        id.ToString());
                    return;
                }
                def.InitialSurfaceDeployment = new LegacyFormalArmyInitialSurfaceDeploymentDefinition
                {
                    SurfaceId = deploymentNode.GetString("surfaceId", string.Empty),
                    AnchorSiteId = deploymentNode.GetString("anchorSiteId", string.Empty),
                    OffsetCellsX = ReadInt(deploymentNode, "offsetCellsX", 0),
                    OffsetCellsY = ReadInt(deploymentNode, "offsetCellsY", 0)
                };
            }

            if (item.TryGetProperty("initialHex", out var hexNode))
            {
                if (hexNode.Kind != JsonValueKind.Object)
                {
                    report.Add(ErrorCode.ContentLoadFailed, "formalArmy.initialHex must be object.", id.ToString());
                    return;
                }

                var hexErrorsBefore = report.Errors.Count;
                DefinitionSchema.RejectUnknownFields(
                    hexNode, DefinitionSchema.LegacyFormalArmyInitialHexFields, report, id + ".initialHex");
                if (report.Errors.Count > hexErrorsBefore)
                    return;

                // (0,0) 是合法 Hex：以 InitialHex != null 为 presence authority，
                // 不能靠 Q/R 是否为 0 判断有没有 initialHex。
                def.InitialHex = new LegacyFormalArmyInitialHexDefinition
                {
                    Q = hexNode.TryGetProperty("q", out var qNode) && qNode.Kind == JsonValueKind.Number
                        ? (int)qNode.Number
                        : 0,
                    R = hexNode.TryGetProperty("r", out var rNode) && rNode.Kind == JsonValueKind.Number
                        ? (int)rNode.Number
                        : 0
                };
            }

            if (string.IsNullOrWhiteSpace(def.RuntimeArmyId))
            {
                report.Add(ErrorCode.MissingRequiredField, "formalArmy.runtimeArmyId required.", id.ToString());
                return;
            }

            if (string.IsNullOrWhiteSpace(def.RuntimeStackId))
            {
                report.Add(ErrorCode.MissingRequiredField, "formalArmy.runtimeStackId required.", id.ToString());
                return;
            }

            if (string.IsNullOrWhiteSpace(def.FactionId))
            {
                report.Add(ErrorCode.MissingRequiredField, "formalArmy.factionId required.", id.ToString());
                return;
            }

            if (string.IsNullOrWhiteSpace(def.AssemblySiteId))
            {
                report.Add(ErrorCode.MissingRequiredField, "formalArmy.assemblySiteId required.", id.ToString());
                return;
            }

            if (!item.TryGetProperty("members", out var membersNode) || membersNode.Kind != JsonValueKind.Array)
            {
                report.Add(ErrorCode.MissingRequiredField, "formalArmy.members required array.", id.ToString());
                return;
            }

            var leaderCount = 0;
            foreach (var memberNode in membersNode.Array)
            {
                if (memberNode.Kind != JsonValueKind.Object)
                {
                    report.Add(ErrorCode.ContentLoadFailed, "formalArmy.members entries must be objects.", id.ToString());
                    continue;
                }

                var memberErrorsBefore = report.Errors.Count;
                DefinitionSchema.RejectUnknownFields(
                    memberNode, DefinitionSchema.LegacyFormalArmyMemberFields, report, id + ".member");
                if (report.Errors.Count > memberErrorsBefore)
                    continue;

                var member = new LegacyFormalArmyMemberDefinition
                {
                    CharacterDefinitionId = memberNode.GetString("characterDefinitionId", string.Empty),
                    DisplayName = memberNode.GetString("displayName", string.Empty),
                    Leader = memberNode.GetBool("leader", false),
                    ReuseOpeningSpawn = memberNode.GetBool("reuseOpeningSpawn", false)
                };
                if (string.IsNullOrWhiteSpace(member.CharacterDefinitionId))
                {
                    report.Add(ErrorCode.MissingRequiredField, "formalArmy.member.characterDefinitionId required.", id.ToString());
                    continue;
                }

                if (member.Leader)
                    leaderCount++;
                def.Members.Add(member);
            }

            if (def.Members.Count == 0)
            {
                report.Add(ErrorCode.MissingRequiredField, "formalArmy.members must be non-empty.", id.ToString());
                return;
            }

            if (leaderCount != 1)
            {
                report.Add(
                    ErrorCode.InvalidArgument,
                    "formalArmy.members requires exactly one leader.",
                    id.ToString());
                return;
            }

            var reg = registry.RegisterLegacyFormalArmyDefinition(def);
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
                    FactionMode = ReadFactionMode(spawnNode, id + ".entry", report, errorsBefore, out var rosterModeExplicit),
                    FactionModeExplicit = rosterModeExplicit,
                    FactionId = spawnNode.GetString("factionId", string.Empty),
                    FactionRole = spawnNode.GetString("factionRole", string.Empty),
                    BindSchedule = spawnNode.GetBool("bindSchedule", true),
                    BindDailyTask = spawnNode.GetBool("bindDailyTask", true),
                    Recruitable = spawnNode.GetBool("recruitable", false),
                    ScheduleId = spawnNode.GetString("scheduleId", string.Empty),
                    AiRole = spawnNode.GetString("aiRole", string.Empty),
                    JobId = spawnNode.GetString("jobId", string.Empty),
                    WorldSiteId = spawnNode.GetString("worldSiteId", string.Empty),
                    LocalLocationId = spawnNode.GetString("localLocationId", string.Empty)
                };
                if (report.Errors.Count > errorsBefore)
                    return;
                if (!TryReadOpeningLocalPosition(spawnNode, id + ".entry", report, out var localPosition))
                    return;
                entry.LocalPosition = localPosition;
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

        static bool TryReadOpeningLocalPosition(
            JsonValue spawnNode,
            string context,
            ValidationReport report,
            out OpeningLocalPositionDefinition position)
        {
            position = null;
            if (!spawnNode.TryGetProperty("localPosition", out var node))
                return true;
            if (node == null || node.Kind != JsonValueKind.Object)
            {
                report.Add(ErrorCode.ContentLoadFailed, "localPosition must be an object.", context + ".localPosition");
                return false;
            }
            var errorsBefore = report.Errors.Count;
            DefinitionSchema.RejectUnknownFields(node, DefinitionSchema.OpeningLocalPositionFields, report, context + ".localPosition");
            if (report.Errors.Count > errorsBefore)
                return false;
            if (!node.TryGetProperty("x", out var x) || x.Kind != JsonValueKind.Number ||
                double.IsNaN(x.Number) || double.IsInfinity(x.Number) ||
                !node.TryGetProperty("z", out var z) || z.Kind != JsonValueKind.Number ||
                double.IsNaN(z.Number) || double.IsInfinity(z.Number))
            {
                report.Add(ErrorCode.ContentLoadFailed, "localPosition.x and localPosition.z must be finite numbers.", context + ".localPosition");
                return false;
            }
            position = new OpeningLocalPositionDefinition { X = (float)x.Number, Z = (float)z.Number };
            return true;
        }

        /// <summary>
        /// 读 spawn factionMode（缺省 = CharacterDefault）。非法值 = Content error。
        /// </summary>
        static OpeningFactionMode ReadFactionMode(
            JsonValue node,
            string context,
            ValidationReport report,
            int errorsBefore,
            out bool modeExplicit)
        {
            modeExplicit = node.TryGetProperty("factionMode", out var modeNode);
            if (!modeExplicit)
                return OpeningFactionMode.CharacterDefault;
            var text = modeNode.Kind == JsonValueKind.String
                ? (string)modeNode.String
                : string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                modeExplicit = false;
                return OpeningFactionMode.CharacterDefault;
            }
            if (Enum.TryParse(text.Trim(), ignoreCase: true, out OpeningFactionMode mode))
                return mode;
            report.Add(ErrorCode.InvalidArgument,
                "Unknown factionMode '" + text + "' (expected CharacterDefault|Override|Unaffiliated).",
                context + ".factionMode");
            return OpeningFactionMode.CharacterDefault;
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

        static void LoadWorldSiteEconomy(
            JsonValue item,
            DefinitionId id,
            DefinitionRegistry registry,
            ValidationReport report)
        {
            var errorsBefore = report.Errors.Count;
            DefinitionSchema.RejectUnknownFields(item, DefinitionSchema.WorldSiteEconomyFields, report, id.ToString());
            if (report.Errors.Count > errorsBefore)
                return;

            var economy = new WorldSiteEconomyDefinition
            {
                Id = id,
                SiteId = item.GetString("siteId", string.Empty)
            };

            if (!item.TryGetProperty("initialPublicStock", out var stockNode) || stockNode.Kind != JsonValueKind.Array)
            {
                report.Add(ErrorCode.ContentLoadFailed, "initialPublicStock must be array.", id.ToString());
                return;
            }
            foreach (var entry in stockNode.Array)
            {
                if (entry.Kind != JsonValueKind.Object)
                {
                    report.Add(ErrorCode.ContentLoadFailed, "initialPublicStock entries must be objects.", id.ToString());
                    continue;
                }
                DefinitionSchema.RejectUnknownFields(
                    entry, DefinitionSchema.WorldSiteEconomyStockFields, report, id + ".initialPublicStock");
                if (report.Errors.Count > errorsBefore) return;
                economy.InitialPublicStock.Add(new WorldSiteEconomyStockEntry
                {
                    ResourceId = entry.GetString("resourceId", string.Empty),
                    Amount = ReadInt(entry, "amount", -1)
                });
            }
            var reg = registry.RegisterWorldSiteEconomy(economy);
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
                Height = ReadInt(item, "height", 0),
                ExitTriggerDepth = ReadFloat(item, "exitTriggerDepth", 0f),
                SpaceKind = item.GetString("spaceKind", string.Empty)
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

        static void LoadContinuousSurfaceWorldMap(JsonValue item, DefinitionId id, DefinitionRegistry registry, ValidationReport report)
        {
            var compositionId = item.GetString("compositionId", string.Empty);
            var surfaceId = item.GetString("surfaceId", string.Empty);
            var hash = item.GetString("sourceHash", string.Empty);
            var originX = ReadFloat(item, "originWorldX", 0f); var originY = ReadFloat(item, "originWorldY", 0f);
            var cell = ReadFloat(item, "cellSize", 0f); var width = ReadInt(item, "widthCells", 0); var height = ReadInt(item, "heightCells", 0);
            if (string.IsNullOrWhiteSpace(compositionId) || string.IsNullOrWhiteSpace(surfaceId) || string.IsNullOrWhiteSpace(hash) || cell <= 0f || width <= 0 || height <= 0 ||
                !item.TryGetProperty("baseTerrainRows", out var baseRows) || baseRows.Kind != JsonValueKind.Array || baseRows.Array.Count != height ||
                !item.TryGetProperty("forestRows", out var forestRows) || forestRows.Kind != JsonValueKind.Array || forestRows.Array.Count != height)
            { report.Add(ErrorCode.InvalidArgument, "Invalid continuousSurfaceWorldMap metadata.", id.ToString()); return; }
            var definition = new ContinuousSurfaceWorldMapDefinition { Id=id, CompositionId=compositionId, SurfaceId=surfaceId, SourceHash=hash, OriginWorldX=originX, OriginWorldY=originY, CellSize=cell, WidthCells=width, HeightCells=height };
            for (var row=0; row<height; row++)
            {
                var terrain=baseRows.Array[row]; var forest=forestRows.Array[row];
                if (terrain.Kind != JsonValueKind.String || forest.Kind != JsonValueKind.String || terrain.String.Length != width || forest.String.Length != width || HasInvalidWorldMapTerrain(terrain.String) || HasInvalidWorldMapForest(forest.String))
                { report.Add(ErrorCode.InvalidArgument, "Invalid continuousSurfaceWorldMap raster row.", id + ".rows[" + row + "]"); return; }
                definition.BaseTerrainRows.Add(terrain.String); definition.ForestRows.Add(forest.String);
            }
            var registered=registry.RegisterContinuousSurfaceWorldMap(definition); if (registered.IsFailure) report.Add(registered.Error);
        }

        static bool HasInvalidWorldMapTerrain(string row)
        {
            for (var i = 0; i < row.Length; i++) if (row[i] != 'P' && row[i] != 'M' && row[i] != 'W') return true;
            return false;
        }

        static bool HasInvalidWorldMapForest(string row)
        {
            for (var i = 0; i < row.Length; i++) if (row[i] < '0' || row[i] > '9') return true;
            return false;
        }

        static void LoadOutdoorSurfaceGeography(
            JsonValue item, DefinitionId id, DefinitionRegistry registry, ValidationReport report)
        {
            var errorsBefore = report.Errors.Count;
            DefinitionSchema.RejectUnknownFields(item, DefinitionSchema.OutdoorSurfaceGeographyFields, report, id.ToString());
            if (report.Errors.Count > errorsBefore) return;
            var surfaceId = item.GetString("surfaceId", string.Empty);
            var schema = ReadInt(item, "sourceSchemaVersion", 0);
            var revision = item.GetString("sourceRevision", string.Empty);
            var hash = item.GetString("sourceHash", string.Empty);
            var originX = ReadFloat(item, "originWorldX", 0f);
            var originY = ReadFloat(item, "originWorldY", 0f);
            var cellSize = ReadFloat(item, "cellSize", 0f);
            var width = ReadInt(item, "width", 0);
            var height = ReadInt(item, "height", 0);
            if (string.IsNullOrWhiteSpace(surfaceId) || schema <= 0 || string.IsNullOrWhiteSpace(revision) ||
                string.IsNullOrWhiteSpace(hash) || cellSize <= 0f || width <= 0 || height <= 0)
            {
                report.Add(ErrorCode.InvalidArgument, "Invalid outdoorSurfaceGeography metadata.", id.ToString());
                return;
            }
            if (!item.TryGetProperty("rows", out var rows) || rows.Kind != JsonValueKind.Array || rows.Array.Count != height)
            {
                report.Add(ErrorCode.InvalidArgument, "outdoorSurfaceGeography rows/height mismatch.", id.ToString());
                return;
            }
            var cells = new List<SurfaceGroundCellKind>(width * height);
            for (var y = 0; y < rows.Array.Count; y++)
            {
                var row = rows.Array[y];
                if (row.Kind != JsonValueKind.String || row.String.Length != width)
                {
                    report.Add(ErrorCode.InvalidArgument, "outdoorSurfaceGeography row width mismatch.", id + ".rows[" + y + "]");
                    return;
                }
                for (var x = 0; x < row.String.Length; x++)
                {
                    switch (row.String[x])
                    {
                        case '.': cells.Add(SurfaceGroundCellKind.Ground); break;
                        case '~': cells.Add(SurfaceGroundCellKind.Water); break;
                        case '=': cells.Add(SurfaceGroundCellKind.Road); break;
                        case 'B': cells.Add(SurfaceGroundCellKind.Water | SurfaceGroundCellKind.Bridge | SurfaceGroundCellKind.Road); break;
                        case '#': cells.Add(SurfaceGroundCellKind.Solid); break;
                        default:
                            report.Add(ErrorCode.InvalidArgument, "Unknown outdoor geography cell code.", id + ".rows[" + y + "][" + x + "]");
                            return;
                    }
                }
            }
            var definition = new OutdoorSurfaceGeographyDefinition
            {
                Id = id, SurfaceId = surfaceId, SourceSchemaVersion = schema,
                SourceRevision = revision, SourceHash = hash,
                Navigation = new SurfaceGroundNavigation(surfaceId, revision, hash, originX, originY, cellSize, width, height, cells)
            };
            if (!item.TryGetProperty("coverageChunks", out var coverage) || coverage.Kind != JsonValueKind.Array)
            {
                report.Add(ErrorCode.MissingRequiredField, "outdoorSurfaceGeography.coverageChunks required.", id.ToString());
                return;
            }
            var uniqueChunks = new HashSet<SurfaceChunkCoord>();
            foreach (var node in coverage.Array)
            {
                DefinitionSchema.RejectUnknownFields(node, DefinitionSchema.OutdoorGeographyChunkFields, report, id + ".coverageChunk");
                var coord = new SurfaceChunkCoord(ReadInt(node, "x", 0), ReadInt(node, "y", 0));
                if (!uniqueChunks.Add(coord))
                    report.Add(ErrorCode.InvalidArgument, "Duplicate geography coverage chunk.", id + ".coverageChunks");
                else definition.CoverageChunks.Add(coord);
            }
            if (!item.TryGetProperty("chunkRows", out var chunkRows) || chunkRows.Kind != JsonValueKind.Array ||
                chunkRows.Array.Count != definition.CoverageChunks.Count)
            {
                report.Add(ErrorCode.InvalidArgument, "outdoorSurfaceGeography chunkRows/coverage mismatch.", id.ToString());
                return;
            }
            var minChunkX = int.MaxValue; var minChunkY = int.MaxValue;
            var maxChunkX = int.MinValue; var maxChunkY = int.MinValue;
            foreach (var coord in definition.CoverageChunks)
            {
                minChunkX = Math.Min(minChunkX, coord.X); minChunkY = Math.Min(minChunkY, coord.Y);
                maxChunkX = Math.Max(maxChunkX, coord.X); maxChunkY = Math.Max(maxChunkY, coord.Y);
            }
            var chunkCountX = maxChunkX - minChunkX + 1; var chunkCountY = maxChunkY - minChunkY + 1;
            if (chunkCountX <= 0 || chunkCountY <= 0 || width % chunkCountX != 0 || height % chunkCountY != 0)
            {
                report.Add(ErrorCode.InvalidArgument, "outdoorSurfaceGeography grid cannot be split by coverage.", id.ToString());
                return;
            }
            var chunkWidthCells = width / chunkCountX; var chunkHeightCells = height / chunkCountY;
            var seenChunkRows = new HashSet<SurfaceChunkCoord>();
            foreach (var node in chunkRows.Array)
            {
                DefinitionSchema.RejectUnknownFields(node, DefinitionSchema.OutdoorGeographyChunkRowsFields, report, id + ".chunkRows");
                var coord = new SurfaceChunkCoord(ReadInt(node, "x", 0), ReadInt(node, "y", 0));
                if (!uniqueChunks.Contains(coord) || !seenChunkRows.Add(coord) ||
                    !node.TryGetProperty("rows", out var localRows) || localRows.Kind != JsonValueKind.Array ||
                    localRows.Array.Count != chunkHeightCells)
                {
                    report.Add(ErrorCode.InvalidArgument, "Invalid outdoorSurfaceGeography chunkRows entry.", id + ".chunkRows");
                    return;
                }
                for (var localY = 0; localY < chunkHeightCells; localY++)
                {
                    var text = localRows.Array[localY];
                    if (text.Kind != JsonValueKind.String || text.String.Length != chunkWidthCells)
                    {
                        report.Add(ErrorCode.InvalidArgument, "Invalid outdoorSurfaceGeography chunk row width.", id + ".chunkRows");
                        return;
                    }
                    var globalY = (coord.Y - minChunkY) * chunkHeightCells + localY;
                    var globalX = (coord.X - minChunkX) * chunkWidthCells;
                    for (var localX = 0; localX < chunkWidthCells; localX++)
                        if (text.String[localX] != rows.Array[globalY].String[globalX + localX])
                        {
                            report.Add(ErrorCode.InvalidArgument, "Chunk/global geography bake mismatch.", id + ".chunkRows");
                            return;
                        }
                }
            }
            if (item.TryGetProperty("mapPrimitives", out var primitives) && primitives.Kind == JsonValueKind.Array)
                foreach (var node in primitives.Array)
                {
                    DefinitionSchema.RejectUnknownFields(node, DefinitionSchema.OutdoorGeographyPrimitiveFields, report, id + ".mapPrimitive");
                    var primitive = new OutdoorGeographyPrimitive
                    {
                        StableId = node.GetString("stableId", string.Empty), Kind = node.GetString("kind", string.Empty),
                        WorldX = ReadFloat(node, "worldX", 0f), WorldY = ReadFloat(node, "worldY", 0f),
                        WorldWidth = ReadFloat(node, "worldWidth", 0f), WorldHeight = ReadFloat(node, "worldHeight", 0f),
                        StrokeWidth = ReadFloat(node, "strokeWidth", 0f)
                    };
                    if (node.TryGetProperty("points", out var points) && points.Kind == JsonValueKind.Array)
                        foreach (var value in points.Array)
                            if (value.Kind == JsonValueKind.Number) primitive.Points.Add((float)value.Number);
                    definition.MapPrimitives.Add(primitive);
                }
            if (item.TryGetProperty("landmarks", out var landmarks) && landmarks.Kind == JsonValueKind.Array)
                foreach (var node in landmarks.Array)
                {
                    DefinitionSchema.RejectUnknownFields(node, DefinitionSchema.OutdoorGeographyLandmarkFields, report, id + ".landmark");
                    definition.Landmarks.Add(new OutdoorGeographyLandmark
                    {
                        StableId = node.GetString("stableId", string.Empty), Label = node.GetString("label", string.Empty),
                        WorldX = ReadFloat(node, "worldX", 0f), WorldY = ReadFloat(node, "worldY", 0f)
                    });
                }
            if (item.TryGetProperty("hexSummary", out var summaries) && summaries.Kind == JsonValueKind.Array)
                foreach (var node in summaries.Array)
                {
                    DefinitionSchema.RejectUnknownFields(node, DefinitionSchema.OutdoorGeographyHexSummaryFields, report, id + ".hexSummary");
                    definition.HexSummary.Add(new OutdoorGeographyHexSummary
                    {
                        Q = ReadInt(node, "q", 0), R = ReadInt(node, "r", 0),
                        CoveredFraction = ReadFloat(node, "coveredFraction", 0f), WaterFraction = ReadFloat(node, "waterFraction", 0f),
                        RoadFraction = ReadFloat(node, "roadFraction", 0f), HasRiver = node.GetBool("hasRiver", false),
                        HasBridge = node.GetBool("hasBridge", false)
                    });
                }
            if (report.Errors.Count > errorsBefore) return;
            var registration = registry.RegisterOutdoorSurfaceGeography(definition);
            if (registration.IsFailure) report.Add(registration.Error);
        }

        static void LoadWorldSpatialRules(JsonValue item, DefinitionId id, DefinitionRegistry registry, ValidationReport report)
        {
            var errorsBefore = report.Errors.Count;
            DefinitionSchema.RejectUnknownFields(
                item, DefinitionSchema.WorldSpatialRulesFields, report, id.ToString());
            if (report.Errors.Count > errorsBefore) return;
            var hasEncounterCells = item.TryGetProperty("wildernessEncounterWidthCells", out _) ||
                                    item.TryGetProperty("wildernessEncounterHeightCells", out _);
            var hasEncounterLegacy = item.TryGetProperty("wildernessEncounterWidthWorld", out _) ||
                                     item.TryGetProperty("wildernessEncounterHeightWorld", out _);
            if (hasEncounterCells == hasEncounterLegacy)
            {
                report.Add(ErrorCode.InvalidArgument,
                    "Specify exactly one complete wilderness encounter size: *Cells or legacy *World.", id.ToString());
                return;
            }
            var rules = new XianXia.Core.World.Strategic.WorldSpatialRules
            {
                Id = id.ToString(),
                // Legacy *World names carried cell counts. Accept them only as an old Content alias.
                WildernessEncounterWidthCells = ReadFloat(item,
                    hasEncounterCells ? "wildernessEncounterWidthCells" : "wildernessEncounterWidthWorld", 0f),
                WildernessEncounterHeightCells = ReadFloat(item,
                    hasEncounterCells ? "wildernessEncounterHeightCells" : "wildernessEncounterHeightWorld", 0f),
                InterventionDecisionSeconds = ReadFloat(item, "interventionDecisionSeconds", -1f),
                InterventionArrivalSeconds = ReadFloat(item, "interventionArrivalSeconds", -1f),
                InterventionRelationThreshold = (int)item.GetNumber("interventionRelationThreshold", 0),
                InterventionChanceBasisPoints = (int)item.GetNumber("interventionChanceBasisPoints", -1)
            };
            var levels = new HashSet<int>();
            if (item.TryGetProperty("coreLevels", out var rows) && rows.Kind == JsonValueKind.Array)
                foreach (var row in rows.Array)
                {
                    errorsBefore = report.Errors.Count;
                    DefinitionSchema.RejectUnknownFields(
                        row, DefinitionSchema.CoreLevelControlRangeFields, report, id + ".coreLevels");
                    if (report.Errors.Count > errorsBefore) return;
                    var level = (int)row.GetNumber("level", 0);
                    var hasCells = row.TryGetProperty("controlWidthCells", out _) ||
                                   row.TryGetProperty("controlHeightCells", out _);
                    var hasLegacy = row.TryGetProperty("controlWidthWorld", out _) ||
                                    row.TryGetProperty("controlHeightWorld", out _);
                    if (hasCells == hasLegacy)
                    {
                        report.Add(ErrorCode.InvalidArgument,
                            "Specify exactly one complete core control size: *Cells or legacy *World.", id.ToString());
                        return;
                    }
                    var width = ReadFloat(row, hasCells ? "controlWidthCells" : "controlWidthWorld", 0f);
                    var height = ReadFloat(row, hasCells ? "controlHeightCells" : "controlHeightWorld", 0f);
                    if (level < 1 || !levels.Add(level) || !(width > 0f) || !(height > 0f) ||
                        float.IsInfinity(width) || float.IsInfinity(height))
                    { report.Add(ErrorCode.InvalidArgument, "Invalid/duplicate core level control range.", id.ToString()); return; }
                    rules.CoreLevels.Add(new XianXia.Core.World.Strategic.CoreLevelControlRange
                        { Level = level, WidthCells = width, HeightCells = height });
                }
            if (!levels.Contains(1) || !(rules.WildernessEncounterWidthCells > 0f) ||
                !(rules.WildernessEncounterHeightCells > 0f) || float.IsInfinity(rules.WildernessEncounterWidthCells) ||
                float.IsInfinity(rules.WildernessEncounterHeightCells) || !(rules.InterventionDecisionSeconds >= 0f) ||
                !(rules.InterventionArrivalSeconds >= 0f) || rules.InterventionChanceBasisPoints < 0 ||
                rules.InterventionChanceBasisPoints > 10000 || rules.InterventionRelationThreshold < 1 ||
                rules.InterventionRelationThreshold > 100 || float.IsInfinity(rules.InterventionDecisionSeconds) ||
                float.IsInfinity(rules.InterventionArrivalSeconds))
            { report.Add(ErrorCode.InvalidArgument, "Invalid world spatial/encounter configuration.", id.ToString()); return; }
            var result = registry.RegisterSpatialRules(rules);
            if (result.IsFailure) report.Add(result.Error);
        }

        static void LoadOutdoorSurface(
            JsonValue item, DefinitionId id, DefinitionRegistry registry, ValidationReport report)
        {
            var errorsBefore = report.Errors.Count;
            DefinitionSchema.RejectUnknownFields(item, DefinitionSchema.OutdoorSurfaceFields, report, id.ToString());
            if (report.Errors.Count > errorsBefore) return;
            var surface = new OutdoorWorldSurfaceDefinition
            {
                SurfaceId = id.ToString(),
                OriginWorldX = ReadFloat(item, "originWorldX", 0f),
                OriginWorldY = ReadFloat(item, "originWorldY", 0f),
                CellSize = ReadFloat(item, "cellSize", 1f),
                ChunkWidth = ReadFloat(item, "chunkWidth", 50f),
                ChunkHeight = ReadFloat(item, "chunkHeight", 50f),
                AcceptanceOnly = item.GetBool("acceptanceOnly", false)
            };
            if (surface.CellSize <= 0f || surface.ChunkWidth <= 0f || surface.ChunkHeight <= 0f)
            { report.Add(ErrorCode.InvalidArgument, "outdoorSurface metric must be positive.", id.ToString()); return; }
            if (!item.TryGetProperty("chunks", out var chunks) || chunks.Kind != JsonValueKind.Array)
            { report.Add(ErrorCode.MissingRequiredField, "outdoorSurface.chunks required.", id.ToString()); return; }
            var usedCoords = new HashSet<XianXia.Core.World.Surface.SurfaceChunkCoord>();
            var usedChunkIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in chunks.Array)
            {
                if (node.Kind != JsonValueKind.Object) continue;
                DefinitionSchema.RejectUnknownFields(node, DefinitionSchema.OutdoorSurfaceChunkFields, report, id + ".chunk");
                var stableChunkId = node.GetString("id", string.Empty);
                var coord = new XianXia.Core.World.Surface.SurfaceChunkCoord(ReadInt(node, "x", 0), ReadInt(node, "y", 0));
                if (string.IsNullOrWhiteSpace(stableChunkId))
                {
                    report.Add(ErrorCode.MissingRequiredField, "outdoorSurface chunk id required.", id + ".chunk");
                    continue;
                }
                if (!usedChunkIds.Add(stableChunkId) || !usedCoords.Add(coord))
                {
                    report.Add(ErrorCode.InvalidArgument, "outdoorSurface chunk ids and physical rectangle coordinates must be unique.", id + ".chunk");
                    continue;
                }
                surface.Chunks.Add(new OutdoorSurfaceChunkDefinition
                {
                    StableChunkId = stableChunkId,
                    Coord = coord,
                    Width = surface.ChunkWidth, Height = surface.ChunkHeight
                });
            }
            if (surface.Chunks.Count == 0) { report.Add(ErrorCode.MissingRequiredField, "outdoorSurface requires chunks.", id.ToString()); return; }
            if (item.TryGetProperty("siteRegions", out var regions) && regions.Kind == JsonValueKind.Array)
                foreach (var node in regions.Array)
                {
                    DefinitionSchema.RejectUnknownFields(node, DefinitionSchema.WorldSitePhysicalRegionFields, report, id + ".siteRegion");
                    surface.SiteRegions.Add(new WorldSitePhysicalRegionDefinition
                    {
                        SiteId = node.GetString("siteId", string.Empty), SurfaceId = node.GetString("surfaceId", id.ToString()),
                        DisplayName = node.GetString("displayName", string.Empty),
                        SiteType = node.GetString("siteType", string.Empty),
                        OwnerFactionId = node.GetString("ownerFactionId", string.Empty),
                        TerritoryRegionId = node.GetString("territoryRegionId", string.Empty),
                        ArrivalWorldX = ReadFloat(node, "arrivalWorldX", 0f), ArrivalWorldY = ReadFloat(node, "arrivalWorldY", 0f)
                    });
                }
            if (item.TryGetProperty("factionFlags", out var flags) && flags.Kind == JsonValueKind.Array)
                foreach (var node in flags.Array)
                {
                    DefinitionSchema.RejectUnknownFields(node, DefinitionSchema.SurfaceFactionFlagFields, report, id + ".factionFlag");
                    surface.FactionFlags.Add(new SurfaceFactionFlagDefinition
                    {
                        FlagId = node.GetString("flagId", string.Empty),
                        FactionId = node.GetString("factionId", string.Empty),
                        WorldX = ReadFloat(node, "worldX", 0f),
                        WorldY = ReadFloat(node, "worldY", 0f),
                        EstablishedOrder = (long)node.GetNumber("establishedOrder", 0),
                        CreatesWorldSite = node.GetBool("createsWorldSite", false),
                        SiteDisplayName = node.GetString("siteDisplayName", string.Empty),
                        SiteType = node.GetString("siteType", string.Empty),
                        CoreLevel = (int)node.GetNumber("coreLevel", 1)
                    });
                }
            if (item.TryGetProperty("sitePlacements", out var placements) && placements.Kind == JsonValueKind.Array)
                foreach (var node in placements.Array)
                {
                    DefinitionSchema.RejectUnknownFields(node, DefinitionSchema.OutdoorSurfacePlacementFields, report, id + ".sitePlacement");
                    surface.SitePlacements.Add(new OutdoorSurfacePlacementDefinition
                    {
                        StableId = node.GetString("stableId", string.Empty), SiteId = node.GetString("siteId", string.Empty),
                        ChunkX = ReadInt(node, "chunkX", 0), ChunkY = ReadInt(node, "chunkY", 0),
                        WorldX = ReadFloat(node, "worldX", 0f), WorldY = ReadFloat(node, "worldY", 0f),
                        WorldWidth = ReadFloat(node, "worldWidth", 0f), WorldHeight = ReadFloat(node, "worldHeight", 0f),
                        SourceGridX = ReadInt(node, "sourceGridX", 0), SourceGridY = ReadInt(node, "sourceGridY", 0),
                        SourceCellsW = ReadInt(node, "sourceCellsW", 0), SourceCellsH = ReadInt(node, "sourceCellsH", 0),
                        Kind = node.GetString("kind", string.Empty), BlocksMovement = node.GetBool("blocksMovement", false),
                        BoundLocationId = node.GetString("boundLocationId", string.Empty), Label = node.GetString("label", string.Empty),
                        LootItemId = node.GetString("lootItemId", string.Empty), SpawnTableId = node.GetString("spawnTableId", string.Empty),
                        SpawnCount = ReadInt(node, "spawnCount", 0)
                    });
                }
            if (item.TryGetProperty("sitePlaces", out var sitePlaces) && sitePlaces.Kind == JsonValueKind.Array)
                foreach (var node in sitePlaces.Array)
                {
                    DefinitionSchema.RejectUnknownFields(node, DefinitionSchema.WorldSitePlaceFields, report, id + ".sitePlace");
                    surface.SitePlaces.Add(new WorldSitePlaceDefinition
                    {
                        SiteId = node.GetString("siteId", string.Empty), LocationId = node.GetString("locationId", string.Empty),
                        Name = node.GetString("name", string.Empty), WorldX = ReadFloat(node, "worldX", 0f), WorldY = ReadFloat(node, "worldY", 0f),
                        Kind = node.GetString("kind", string.Empty),
                        ResourceOnExploreId = node.GetString("resourceOnExploreId", string.Empty),
                        ResourceOnExploreAmount = ReadInt(node, "resourceOnExploreAmount", 0),
                        OpportunitySiteId = node.GetString("opportunitySiteId", string.Empty),
                        ResidentNpcDefinitionId = node.GetString("residentNpcDefinitionId", string.Empty),
                        LocalMapId = node.GetString("localMapId", string.Empty), EnterLocalMapId = node.GetString("enterLocalMapId", string.Empty),
                        EnterSpawnLocationId = node.GetString("enterSpawnLocationId", string.Empty),
                        SurveySenseRequired = ReadInt(node, "surveySenseRequired", 0)
                    });
                    var loadedPlace = surface.SitePlaces[surface.SitePlaces.Count - 1];
                    ReadStringList(node, "adjacentIds", loadedPlace.AdjacentIds, report, id + "." + loadedPlace.LocationId);
                    ReadConditions(node, "enterConditions", loadedPlace.EnterConditions, report, id + "." + loadedPlace.LocationId);
                    ReadStringList(node, "questOfferIds", loadedPlace.QuestOfferIds, report, id + "." + loadedPlace.LocationId);
                    ReadTags(node, loadedPlace.Tags, report, id + "." + loadedPlace.LocationId);
                    ReadStringList(node, "allowedActivities", loadedPlace.AllowedActivities, report, id + "." + loadedPlace.LocationId);
                }
            if (item.TryGetProperty("openingEntityAnchors", out var openingAnchors) &&
                openingAnchors.Kind == JsonValueKind.Array)
                foreach (var node in openingAnchors.Array)
                {
                    DefinitionSchema.RejectUnknownFields(
                        node, DefinitionSchema.OpeningEntityAnchorFields, report, id + ".openingEntityAnchor");
                    surface.OpeningEntityAnchors.Add(new WorldSiteOpeningEntityAnchorDefinition
                    {
                        SiteId = node.GetString("siteId", string.Empty),
                        SpawnKey = node.GetString("spawnKey", string.Empty),
                        DefinitionId = node.GetString("definitionId", string.Empty),
                        SourceLocationId = node.GetString("sourceLocationId", string.Empty),
                        WorldX = ReadFloat(node, "worldX", 0f),
                        WorldY = ReadFloat(node, "worldY", 0f)
                    });
                }
            var regionIds = new HashSet<string>(StringComparer.Ordinal);
            var placementIds = new HashSet<string>(StringComparer.Ordinal);
            var placeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var region in surface.SiteRegions)
                if (string.IsNullOrWhiteSpace(region.SiteId) ||
                    !string.Equals(region.SurfaceId, surface.SurfaceId, StringComparison.Ordinal) || !regionIds.Add(region.SiteId))
                    report.Add(ErrorCode.InvalidArgument, "Invalid or duplicate Outdoor WorldSite physical region.", id + ".siteRegions");
            foreach (var placement in surface.SitePlacements)
            {
                if (string.IsNullOrWhiteSpace(placement.StableId) || string.IsNullOrWhiteSpace(placement.SiteId) ||
                    !placementIds.Add(placement.StableId) || placement.WorldWidth <= 0f || placement.WorldHeight <= 0f ||
                    placement.SourceCellsW <= 0 || placement.SourceCellsH <= 0 ||
                    !usedCoords.Contains(new XianXia.Core.World.Surface.SurfaceChunkCoord(placement.ChunkX, placement.ChunkY)))
                    report.Add(ErrorCode.InvalidArgument, "Invalid/duplicate Outdoor Site placement or missing target chunk.", id + ".sitePlacements");
                if (!regionIds.Contains(placement.SiteId))
                    report.Add(ErrorCode.InvalidArgument, "Outdoor Site placement has no physical region.", placement.StableId);
            }
            foreach (var place in surface.SitePlaces)
                if (string.IsNullOrWhiteSpace(place.SiteId) || string.IsNullOrWhiteSpace(place.LocationId) ||
                    string.IsNullOrWhiteSpace(place.Kind) || !placeIds.Add(place.LocationId) ||
                    !regionIds.Contains(place.SiteId))
                    report.Add(ErrorCode.InvalidArgument,
                        "Outdoor SitePlace requires unique locationId, valid siteId and baked kind metadata.",
                        id + ".sitePlaces");
            var anchorKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var anchor in surface.OpeningEntityAnchors)
            {
                if (string.IsNullOrWhiteSpace(anchor.SiteId) || string.IsNullOrWhiteSpace(anchor.SpawnKey) ||
                    string.IsNullOrWhiteSpace(anchor.DefinitionId) || !regionIds.Contains(anchor.SiteId))
                    report.Add(ErrorCode.InvalidArgument,
                        "Outdoor opening entity anchor requires siteId/spawnKey/definitionId and a valid siteId.",
                        id + ".openingEntityAnchors");
                else if (!anchorKeys.Add(anchor.SiteId + "\n" + anchor.SpawnKey))
                    report.Add(ErrorCode.DuplicateDefinitionId,
                        "Duplicate Outdoor opening entity anchor spawnKey within the same Site.",
                        id + ".openingEntityAnchors." + anchor.SpawnKey);
            }
            var result = registry.RegisterOutdoorSurface(surface);
            if (result.IsFailure) report.Add(result.Error);
        }

        static void LoadHexWorldContent(
            JsonValue item,
            DefinitionId id,
            DefinitionRegistry registry,
            ValidationReport report)
        {
            var errorsBefore = report.Errors.Count;
            DefinitionSchema.RejectUnknownFields(item, DefinitionSchema.HexWorldFields, report, id.ToString());
            if (report.Errors.Count > errorsBefore)
                return;

            var width = ReadInt(item, "width", 0);
            var height = ReadInt(item, "height", 0);
            if (width < 1 || height < 1)
            {
                report.Add(ErrorCode.MissingRequiredField, "hexWorld.width/height required.", id.ToString());
                return;
            }

            var world = new HexWorldContentDefinition
            {
                Id = id,
                Name = item.GetString("name", string.Empty),
                Width = width,
                Height = height,
                HexSize = ReadFloat(item, "hexSize", 1f),
                DefaultTerrain = item.GetString("defaultTerrain", "Mountain"),
                DefaultPassable = item.GetBool("defaultPassable", false),
            };

            if (item.TryGetProperty("cells", out var cellsNode) && cellsNode.Kind == JsonValueKind.Array)
            {
                foreach (var cNode in cellsNode.Array)
                {
                    if (cNode.Kind != JsonValueKind.Object)
                        continue;
                    world.Cells.Add(new HexWorldCellDefinition
                    {
                        Q = ReadInt(cNode, "q", 0),
                        R = ReadInt(cNode, "r", 0),
                        Terrain = cNode.GetString("terrain", world.DefaultTerrain),
                        IsRoad = cNode.GetBool("isRoad", false),
                        Passable = cNode.TryGetProperty("passable", out var passNode) && passNode.Kind == JsonValueKind.Boolean
                            ? passNode.Bool
                            : (bool?)null,
                    });
                }
            }

            if (item.TryGetProperty("sites", out var sitesNode) && sitesNode.Kind == JsonValueKind.Array)
            {
                foreach (var sNode in sitesNode.Array)
                {
                    if (sNode.Kind != JsonValueKind.Object)
                        continue;
                    var site = new HexWorldSiteDefinition
                    {
                        SiteId = sNode.GetString("siteId", string.Empty),
                        DisplayName = sNode.GetString("displayName", string.Empty),
                        SiteType = sNode.GetString("siteType", string.Empty),
                        AnchorQ = ReadInt(sNode, "anchorQ", 0),
                        AnchorR = ReadInt(sNode, "anchorR", 0),
                        LocalMapId = sNode.GetString("localMapId", string.Empty),
                        UsesContinuousOutdoorSurface = sNode.GetBool("continuousOutdoor", false),
                        OwnerFactionId = sNode.GetString("ownerFactionId", string.Empty),
                        ControlEstablishedOrder = (long)sNode.GetNumber("controlEstablishedOrder", 0),
                        TerritoryRegionId = sNode.GetString("territoryRegionId", string.Empty),
                    };
                    if (sNode.TryGetProperty("presenceQ", out var pqNode) &&
                        sNode.TryGetProperty("presenceR", out var prNode) &&
                        pqNode.Kind == JsonValueKind.Number &&
                        prNode.Kind == JsonValueKind.Number)
                    {
                        site.PresenceQ = (int)pqNode.Number;
                        site.PresenceR = (int)prNode.Number;
                    }
                    else
                    {
                        site.PresenceQ = site.AnchorQ;
                        site.PresenceR = site.AnchorR;
                    }
                    if (sNode.TryGetProperty("footprint", out var fpNode) && fpNode.Kind == JsonValueKind.Array)
                    {
                        foreach (var hNode in fpNode.Array)
                        {
                            if (hNode.Kind != JsonValueKind.Object)
                                continue;
                            site.Footprint.Add(new HexWorldCoordDefinition
                            {
                                Q = ReadInt(hNode, "q", 0),
                                R = ReadInt(hNode, "r", 0),
                            });
                        }
                    }

                    world.Sites.Add(site);
                }
            }

            if (item.TryGetProperty("factionFlags", out var flagsNode) && flagsNode.Kind == JsonValueKind.Array)
            {
                foreach (var fNode in flagsNode.Array)
                {
                    if (fNode.Kind != JsonValueKind.Object) continue;
                    var flagErrorsBefore = report.Errors.Count;
                    DefinitionSchema.RejectUnknownFields(
                        fNode,
                        DefinitionSchema.HexWorldFactionFlagFields,
                        report,
                        id.ToString() + ".factionFlag");
                    if (report.Errors.Count > flagErrorsBefore)
                        continue;
                    var hasSurface = fNode.TryGetProperty("surfaceId", out var surfaceNode) &&
                                     surfaceNode.Kind == JsonValueKind.String;
                    var hasWorldX = fNode.TryGetProperty("worldX", out var worldXNode) &&
                                    worldXNode.Kind == JsonValueKind.Number;
                    var hasWorldY = fNode.TryGetProperty("worldY", out var worldYNode) &&
                                    worldYNode.Kind == JsonValueKind.Number;
                    var hasAnyPreciseField = fNode.TryGetProperty("surfaceId", out _) ||
                                             fNode.TryGetProperty("worldX", out _) ||
                                             fNode.TryGetProperty("worldY", out _);
                    if (hasAnyPreciseField && !(hasSurface && hasWorldX && hasWorldY))
                        report.Add(ErrorCode.MissingRequiredField,
                            "FactionFlag precise position requires surfaceId + worldX + worldY.",
                            id + ".factionFlag");
                    world.FactionFlags.Add(new FactionFlagContentDefinition
                    {
                        FlagId=fNode.GetString("flagId",string.Empty), FactionId=fNode.GetString("factionId",string.Empty),
                        AnchorQ=ReadInt(fNode,"anchorQ",0), AnchorR=ReadInt(fNode,"anchorR",0),
                        EstablishedOrder=(long)fNode.GetNumber("establishedOrder",0),
                        HasLocalPosition=fNode.GetBool("hasLocalPosition",false), LocalX=(float)fNode.GetNumber("localX",0), LocalZ=(float)fNode.GetNumber("localZ",0),
                        HasWorldPosition=hasSurface && hasWorldX && hasWorldY,
                        SurfaceId=hasSurface ? surfaceNode.String : string.Empty,
                        WorldX=hasWorldX ? (float)worldXNode.Number : 0f,
                        WorldY=hasWorldY ? (float)worldYNode.Number : 0f,
                        CreatesWorldSite=fNode.GetBool("createsWorldSite",false),
                        SiteDisplayName=fNode.GetString("siteDisplayName",string.Empty),
                        SiteType=fNode.GetString("siteType",string.Empty),
                        CoreLevel=ReadInt(fNode,"coreLevel",1),
                        LegacyDebugOnly=fNode.GetBool("legacyDebugOnly",false)
                    });
                }
            }

            if (item.TryGetProperty("territoryRegions", out var regionsNode) && regionsNode.Kind == JsonValueKind.Array)
            {
                foreach (var rNode in regionsNode.Array)
                {
                    if (rNode.Kind != JsonValueKind.Object)
                        continue;
                    var regionErrorsBefore = report.Errors.Count;
                    DefinitionSchema.RejectUnknownFields(
                        rNode,
                        DefinitionSchema.HexWorldTerritoryRegionFields,
                        report,
                        id.ToString() + ".region");
                    if (report.Errors.Count > regionErrorsBefore)
                        continue;
                    var region = new TerritoryRegionContentDefinition
                    {
                        RegionId = rNode.GetString("regionId", string.Empty),
                        PrimaryWorldSiteId = rNode.GetString("primaryWorldSiteId", string.Empty),
                        ControlFactionId = rNode.GetString("controlFactionId", string.Empty),
                    };
                    if (rNode.TryGetProperty("hexes", out var hexesNode) && hexesNode.Kind == JsonValueKind.Array)
                    {
                        foreach (var hNode in hexesNode.Array)
                        {
                            if (hNode.Kind != JsonValueKind.Object)
                                continue;
                            region.Hexes.Add(new HexWorldCoordDefinition
                            {
                                Q = ReadInt(hNode, "q", 0),
                                R = ReadInt(hNode, "r", 0),
                            });
                        }
                    }

                    world.TerritoryRegions.Add(region);
                }
            }

            if (item.TryGetProperty("standaloneTerritoryHexes", out var standaloneNode) &&
                standaloneNode.Kind == JsonValueKind.Array)
            {
                foreach (var hNode in standaloneNode.Array)
                {
                    if (hNode.Kind != JsonValueKind.Object)
                        continue;
                    var hexErrorsBefore = report.Errors.Count;
                    DefinitionSchema.RejectUnknownFields(
                        hNode,
                        DefinitionSchema.HexWorldStandaloneHexFields,
                        report,
                        id.ToString() + ".standalone");
                    if (report.Errors.Count > hexErrorsBefore)
                        continue;
                    world.StandaloneTerritoryHexes.Add(new HexWorldStandaloneHexControlDefinition
                    {
                        Q = ReadInt(hNode, "q", 0),
                        R = ReadInt(hNode, "r", 0),
                        ControlFactionId = hNode.GetString("controlFactionId", string.Empty),
                    });
                }
            }

            var reg = registry.RegisterHexWorldContent(world);
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

        static int ReadPositiveInt(JsonValue node, string key, ValidationReport report, string context, bool required)
        {
            if (!node.TryGetProperty(key, out var value))
            {
                if (required) report.Add(ErrorCode.MissingRequiredField, key + " required.", context);
                return 0;
            }
            if (value.Kind != JsonValueKind.Number || value.Number <= 0 || value.Number > int.MaxValue ||
                double.IsNaN(value.Number) || double.IsInfinity(value.Number) || value.Number != System.Math.Floor(value.Number))
            { report.Add(ErrorCode.InvalidArgument, key + " must be a positive integer.", context); return 0; }
            return (int)value.Number;
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
