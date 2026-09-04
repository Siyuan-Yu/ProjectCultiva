using System;
using System.Collections.Generic;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Core.Social;

namespace XianXia.Data.Content
{
    /// <summary>
    /// Cross-reference check for chapter production packages (quest／event／flag／npc／location…).
    /// </summary>
    public sealed class ContentReferenceValidator
    {
        public ValidationReport Validate(DefinitionRegistry registry)
        {
            var report = new ValidationReport();
            if (registry == null)
            {
                report.Add(ErrorCode.InvalidArgument, "DefinitionRegistry is null.");
                return report;
            }

            var locations = CollectLocationIds(registry);
            var producedFlags = new HashSet<string>(StringComparer.Ordinal);
            var consumedFlags = new HashSet<string>(StringComparer.Ordinal);

            ValidateScenarios(registry, locations, report);
            ValidateFormalArmies(registry, report);
            ValidateStrategicFactions(registry, report);
            ValidateWorldRegions(registry, locations, report);
            ValidateLocalPlaceSets(registry, locations, report);
            ValidateItems(registry, report);
            ValidateSpawnTables(registry, report);
            ValidateMapSpawnZones(registry, locations, report);
            ValidateQuests(registry, locations, producedFlags, consumedFlags, report);
            ValidateContentEvents(registry, locations, producedFlags, consumedFlags, report);
            ValidateChapters(registry, locations, producedFlags, consumedFlags, report);
            ValidateFlagConsumers(producedFlags, consumedFlags, report);

            return report;
        }

        static HashSet<string> CollectLocationIds(DefinitionRegistry registry)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kv in registry.WorldRegions)
                AddLocationIds(set, kv.Value?.Locations);
            foreach (var kv in registry.LocalPlaceSets)
                AddLocationIds(set, kv.Value?.Locations);
            return set;
        }

        static void AddLocationIds(HashSet<string> set, System.Collections.Generic.List<WorldLocationEntry> locs)
        {
            if (locs == null)
                return;
            for (var i = 0; i < locs.Count; i++)
            {
                if (!string.IsNullOrEmpty(locs[i].Id))
                    set.Add(locs[i].Id);
            }
        }

        static void ValidateItems(DefinitionRegistry registry, ValidationReport report)
        {
            foreach (var kv in registry.Items)
            {
                var item = kv.Value;
                if (item == null)
                    continue;
                if (!string.IsNullOrWhiteSpace(item.TeachesManualId))
                {
                    RequireDef(
                        registry,
                        item.TeachesManualId,
                        "cultivation",
                        item.Id + ".teachesManualId",
                        report);
                }

                if (!string.IsNullOrWhiteSpace(item.TeachesArtId))
                {
                    RequireDef(
                        registry,
                        item.TeachesArtId,
                        "combatArt",
                        item.Id + ".teachesArtId",
                        report);
                }
            }
        }

        static void ValidateSpawnTables(DefinitionRegistry registry, ValidationReport report)
        {
            foreach (var kv in registry.SpawnTables)
            {
                var table = kv.Value;
                if (table?.Entries == null)
                    continue;
                for (var i = 0; i < table.Entries.Count; i++)
                {
                    var e = table.Entries[i];
                    if (e == null)
                        continue;
                    RequireDef(
                        registry,
                        e.DefinitionId,
                        "character",
                        table.Id + ".entries[" + i + "].definitionId",
                        report);
                }
            }
        }

        static void ValidateMapSpawnZones(
            DefinitionRegistry registry,
            HashSet<string> locations,
            ValidationReport report)
        {
            foreach (var kv in registry.MapLayouts)
            {
                var layout = kv.Value;
                if (layout?.Placements == null)
                    continue;
                for (var i = 0; i < layout.Placements.Count; i++)
                {
                    var p = layout.Placements[i];
                    if (p == null ||
                        !string.Equals(p.Kind, "spawnZone", StringComparison.OrdinalIgnoreCase))
                        continue;
                    var ctx = layout.Id + ".placements[" + p.Id + "]";
                    if (string.IsNullOrWhiteSpace(p.SpawnTableId))
                    {
                        report.Add(
                            ErrorCode.MissingRequiredField,
                            "spawnZone.spawnTableId required.",
                            ctx);
                    }
                    else
                        RequireDef(registry, p.SpawnTableId, "spawnTable", ctx + ".spawnTableId", report);
                    if (string.IsNullOrWhiteSpace(p.BoundLocationId))
                    {
                        report.Add(
                            ErrorCode.MissingRequiredField,
                            "spawnZone.boundLocationId required.",
                            ctx);
                    }
                    else if (!locations.Contains(p.BoundLocationId))
                    {
                        report.Add(
                            ErrorCode.NotFound,
                            "spawnZone.boundLocationId missing in worldRegion.",
                            ctx + ":" + p.BoundLocationId);
                    }
                }
            }
        }

        /// <summary>
        /// characterRoster.entries 与 openingScenario.spawns 同形，支持 optional authored placement。
        /// definitionId → character；jobId → job；localLocationId → 已存在地点表。
        /// worldSiteId 依赖配对 scenario 的 HexWorld context，此处不猜测。
        /// </summary>
        void ValidateCharacterRosters(
            DefinitionRegistry registry,
            HashSet<string> locations,
            ValidationReport report)
        {
            foreach (var kv in registry.CharacterRosters)
            {
                var roster = kv.Value;
                var ctx = kv.Key.ToString();
                if (roster.Entries == null)
                    continue;
                for (var i = 0; i < roster.Entries.Count; i++)
                {
                    var entry = roster.Entries[i];
                    RequireDef(registry, entry.DefinitionId, "character", ctx + ".entry[" + i + "]", report);
                    if (!string.IsNullOrWhiteSpace(entry.JobId))
                        RequireDef(registry, entry.JobId, "job", ctx + ".entry[" + i + "].jobId", report);
                    if (!string.IsNullOrWhiteSpace(entry.LocalLocationId) &&
                        !locations.Contains(entry.LocalLocationId))
                    {
                        report.Add(
                            ErrorCode.NotFound,
                            "roster.entry.localLocationId missing in any localPlaceSet/worldRegion.",
                            ctx + ".entry[" + i + "].localLocationId:" + entry.LocalLocationId);
                    }
                }
            }
        }

        void ValidateScenarios(
            DefinitionRegistry registry,
            HashSet<string> locations,
            ValidationReport report)
        {
            foreach (var kv in registry.OpeningScenarios)
            {
                var s = kv.Value;
                var ctx = s.Id.ToString();
                RequireDef(registry, s.OpeningSettlementId, "settlement", ctx + ".openingSettlementId", report);
                RequireDef(registry, s.OpeningWorldRegionId, "worldRegion", ctx + ".openingWorldRegionId", report);
                RequireDef(registry, s.OpeningLocalPlaceSetId, "localPlaceSet", ctx + ".openingLocalPlaceSetId", report);
                RequireDef(registry, s.OpeningHexWorldId, "hexWorld", ctx + ".openingHexWorldId", report);
                RequireDef(registry, s.OpeningChapterId, "chapter", ctx + ".openingChapterId", report);

                var hexWorld = ResolveScenarioHexWorld(registry, s, ctx, report);
                var worldSites = new HashSet<string>(StringComparer.Ordinal);
                if (hexWorld?.Sites != null)
                {
                    for (var si = 0; si < hexWorld.Sites.Count; si++)
                    {
                        if (!string.IsNullOrEmpty(hexWorld.Sites[si].SiteId))
                            worldSites.Add(hexWorld.Sites[si].SiteId);
                    }
                }

                if (s.Spawns == null)
                    continue;
                for (var i = 0; i < s.Spawns.Count; i++)
                {
                    var spawn = s.Spawns[i];
                    RequireDef(registry, spawn.DefinitionId, "character", ctx + ".spawn[" + i + "]", report);
                    if (!string.IsNullOrWhiteSpace(spawn.JobId))
                        RequireDef(registry, spawn.JobId, "job", ctx + ".spawn[" + i + "].jobId", report);

                    // Optional authored placement 校验：
                    // localLocationId 必须存在于某个 LocalPlaceSet／WorldRegion 地点表。
                    // worldSiteId 必须存在于该 scenario 的 OpeningHexWorldId 对应 HexWorld 站点。
                    if (!string.IsNullOrWhiteSpace(spawn.LocalLocationId))
                    {
                        if (!locations.Contains(spawn.LocalLocationId))
                        {
                            report.Add(
                                ErrorCode.NotFound,
                                "spawn.localLocationId missing in any localPlaceSet/worldRegion.",
                                ctx + ".spawn[" + i + "].localLocationId:" + spawn.LocalLocationId);
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(spawn.WorldSiteId))
                    {
                        if (hexWorld == null)
                        {
                            report.Add(
                                ErrorCode.InvalidArgument,
                                "spawn.worldSiteId requires scenario.openingHexWorldId.",
                                ctx + ".spawn[" + i + "].worldSiteId:" + spawn.WorldSiteId);
                        }
                        else if (!worldSites.Contains(spawn.WorldSiteId))
                        {
                            report.Add(
                                ErrorCode.NotFound,
                                "spawn.worldSiteId missing in opening hex world sites.",
                                ctx + ".spawn[" + i + "].worldSiteId:" + spawn.WorldSiteId);
                        }
                    }
                }

                if (s.OpeningRelations == null)
                    continue;
                for (var i = 0; i < s.OpeningRelations.Count; i++)
                {
                    var e = s.OpeningRelations[i];
                    RequireDef(registry, e.FromDefinitionId, "character", ctx + ".relation.from", report);
                    RequireDef(registry, e.ToDefinitionId, "character", ctx + ".relation.to", report);
                }

                if (s.InitialFormalArmyIds == null)
                    continue;
                for (var i = 0; i < s.InitialFormalArmyIds.Count; i++)
                {
                    var armyId = s.InitialFormalArmyIds[i];
                    RequireDef(registry, armyId, "formalArmy", ctx + ".initialFormalArmyIds[" + i + "]", report);
                    ValidateInitialFormalArmyHex(registry, armyId, hexWorld, ctx + ".initialFormalArmyIds[" + i + "]", report);
                }
            }
        }

        /// <summary>
        /// FormalArmy.initialHex 是 scenario-aware：坐标是否合法取决于该 scenario
        /// 选的 OpeningHexWorld。在 bounds 内且 passable、且不属于任何 WorldSite footprint
        /// 才合法（footprint 内应改用 assemblySiteId 的 AtWorldSite 部署）。
        /// </summary>
        static void ValidateInitialFormalArmyHex(
            DefinitionRegistry registry,
            string armyIdText,
            HexWorldContentDefinition hexWorld,
            string ctx,
            ValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(armyIdText) ||
                !DefinitionId.TryParse(armyIdText, out var armyId))
                return;
            if (!registry.FormalArmies.TryGetValue(armyId, out var def) || def == null)
                return;
            if (def.InitialHex == null)
                return;

            if (hexWorld == null)
            {
                report.Add(
                    ErrorCode.InvalidArgument,
                    "formalArmy.initialHex requires scenario.openingHexWorldId.",
                    ctx + ":" + def.Id);
                return;
            }

            var q = def.InitialHex.Q;
            var r = def.InitialHex.R;
            if (q < 0 || r < 0 || q >= hexWorld.Width || r >= hexWorld.Height)
            {
                report.Add(
                    ErrorCode.InvalidArgument,
                    "formalArmy.initialHex out of hex world bounds.",
                    ctx + ":" + def.Id + " (q=" + q + ", r=" + r + ")");
                return;
            }

            if (!IsCellPassable(hexWorld, q, r))
            {
                report.Add(
                    ErrorCode.InvalidArgument,
                    "formalArmy.initialHex not passable in opening hex world.",
                    ctx + ":" + def.Id + " (q=" + q + ", r=" + r + ")");
            }

            if (hexWorld.Sites != null)
            {
                for (var i = 0; i < hexWorld.Sites.Count; i++)
                {
                    var site = hexWorld.Sites[i];
                    if (site?.Footprint == null)
                        continue;
                    for (var f = 0; f < site.Footprint.Count; f++)
                    {
                        if (site.Footprint[f] == null)
                            continue;
                        if (site.Footprint[f].Q == q && site.Footprint[f].R == r)
                        {
                            report.Add(
                                ErrorCode.InvalidArgument,
                                "formalArmy.initialHex inside WorldSite footprint; use assemblySiteId for AtWorldSite deployment.",
                                ctx + ":" + def.Id + " (q=" + q + ", r=" + r + " in " + site.SiteId + ")");
                            return;
                        }
                    }
                }
            }
        }

        static bool IsCellPassable(HexWorldContentDefinition hexWorld, int q, int r)
        {
            if (hexWorld?.Cells != null)
            {
                for (var i = 0; i < hexWorld.Cells.Count; i++)
                {
                    var cell = hexWorld.Cells[i];
                    if (cell == null || cell.Q != q || cell.R != r)
                        continue;
                    return cell.Passable ?? hexWorld.DefaultPassable;
                }
            }

            return hexWorld != null && hexWorld.DefaultPassable;
        }

        /// <summary>解析 scenario 的 OpeningHexWorld；缺省时返回 null（含错误已记录）。</summary>
        static HexWorldContentDefinition ResolveScenarioHexWorld(
            DefinitionRegistry registry,
            OpeningScenarioDefinition scenario,
            string ctx,
            ValidationReport report)
        {
            var hexWorldId = scenario?.OpeningHexWorldId;
            if (string.IsNullOrWhiteSpace(hexWorldId))
                return null;
            if (!DefinitionId.TryParse(hexWorldId, out var id))
            {
                report.Add(ErrorCode.InvalidDefinitionId, "Invalid openingHexWorldId.", ctx + ":" + hexWorldId);
                return null;
            }

            if (!registry.HexWorldContents.TryGetValue(id, out var def))
            {
                report.Add(ErrorCode.NotFound, "openingHexWorldId missing.", ctx + ":" + hexWorldId);
                return null;
            }

            return def;
        }


        /// <summary>
        /// 每个 member.characterDefinitionId 必须存在；runtimeArmyId / runtimeStackId 全局唯一；
        /// 恰好一个 leader 已在 Load 层验证，这里再补成员数 / 引用完整性。
        /// </summary>
        /// <summary>
        /// Strategic Faction cross-reference：formalArmy.factionId / legacy scenario.openingFactionId /
        /// spawns factionId / roster entries factionId / hexWorld site.ownerFactionId /
        /// territoryRegion.controlFactionId 引用的 faction 必须存在于 StrategicFactions。
        /// 未知引用 = Content Validation ERROR（不得静默随机颜色）。空引用不校验。
        /// </summary>
        static void ValidateStrategicFactions(DefinitionRegistry registry, ValidationReport report)
        {
            // CharacterDefinition.defaultFaction* 一致性 + 存在性（Case A/B）。
            foreach (var kv in registry.Characters)
            {
                var character = kv.Value;
                if (character == null)
                    continue;
                var hasDefaultFaction = !string.IsNullOrWhiteSpace(character.DefaultFactionId);
                var hasDefaultRole = !string.IsNullOrWhiteSpace(character.DefaultFactionRole);
                if (hasDefaultFaction)
                {
                    RequireFaction(registry, character.DefaultFactionId, character.Id + ".defaultFactionId", report, allowEmpty: false);
                    if (!hasDefaultRole ||
                        !Enum.TryParse(character.DefaultFactionRole.Trim(), true, out FactionRoleKind role) ||
                        role == FactionRoleKind.None)
                    {
                        report.Add(
                            ErrorCode.InvalidArgument,
                            "Character defaultFactionId requires a non-None defaultFactionRole.",
                            character.Id + ".defaultFactionRole");
                    }
                }
                else if (hasDefaultRole)
                {
                    report.Add(
                        ErrorCode.InvalidArgument,
                        "Character defaultFactionRole requires defaultFactionId.",
                        character.Id + ".defaultFactionRole");
                }
            }

            foreach (var kv in registry.FormalArmies)
            {
                var def = kv.Value;
                if (def == null)
                    continue;
                RequireFaction(registry, def.FactionId, def.Id + ".factionId", report, allowEmpty: false);
            }

            foreach (var kv in registry.OpeningScenarios)
            {
                var scenario = kv.Value;
                if (scenario == null)
                    continue;
                RequireFaction(registry, scenario.OpeningFactionId, scenario.Id + ".openingFactionId", report, allowEmpty: true);
                if (scenario.Spawns == null)
                    continue;
                for (var i = 0; i < scenario.Spawns.Count; i++)
                {
                    var spawn = scenario.Spawns[i];
                    if (spawn == null)
                        continue;
                    ValidateSpawnMembership(registry, spawn, scenario.Id + ".spawns[" + i + "]", report);
                }
            }

            foreach (var kv in registry.CharacterRosters)
            {
                var roster = kv.Value;
                if (roster?.Entries == null)
                    continue;
                for (var i = 0; i < roster.Entries.Count; i++)
                {
                    var entry = roster.Entries[i];
                    if (entry == null)
                        continue;
                    ValidateSpawnMembership(registry, entry, roster.Id + ".entries[" + i + "]", report);
                }
            }

            foreach (var kv in registry.HexWorldContents)
            {
                var world = kv.Value;
                if (world == null)
                    continue;
                var worldCtx = world.Id.ToString();
                if (world.Sites != null)
                {
                    for (var i = 0; i < world.Sites.Count; i++)
                    {
                        var site = world.Sites[i];
                        if (site == null)
                            continue;
                        RequireFaction(
                            registry,
                            site.OwnerFactionId,
                            worldCtx + ".sites[" + i + "]:" + site.SiteId + ".ownerFactionId",
                            report);
                    }
                }

                if (world.TerritoryRegions != null)
                {
                    for (var i = 0; i < world.TerritoryRegions.Count; i++)
                    {
                        var region = world.TerritoryRegions[i];
                        if (region == null)
                            continue;
                        RequireFaction(
                            registry,
                            region.ControlFactionId,
                            worldCtx + ".territoryRegions[" + i + "]:" + region.RegionId + ".controlFactionId",
                            report);
                    }
                }

                if (world.StandaloneTerritoryHexes != null)
                {
                    for (var i = 0; i < world.StandaloneTerritoryHexes.Count; i++)
                    {
                        var control = world.StandaloneTerritoryHexes[i];
                        if (control == null)
                            continue;
                        RequireFaction(
                            registry,
                            control.ControlFactionId,
                            worldCtx + ".standaloneTerritoryHexes[" + i + "]:(" +
                            control.Q + "," + control.R + ").controlFactionId",
                            report);
                    }
                }
            }
        }

        static void ValidateSpawnMembership(
            DefinitionRegistry registry,
            OpeningSpawnEntry entry,
            string context,
            ValidationReport report)
        {
            var hasFaction = !string.IsNullOrWhiteSpace(entry.FactionId);
            var hasRole = !string.IsNullOrWhiteSpace(entry.FactionRole) &&
                          Enum.TryParse(entry.FactionRole.Trim(), true, out FactionRoleKind role) &&
                          role != FactionRoleKind.None;
            var modeExplicit = entry.FactionModeExplicit;

            switch (entry.FactionMode)
            {
                case OpeningFactionMode.Override:
                    // Override：factionId 非空 + role 有效 + faction 存在。
                    if (!hasFaction)
                    {
                        report.Add(ErrorCode.InvalidArgument,
                            "Spawn factionMode=Override requires factionId.", context + ".factionId");
                        return;
                    }
                    RequireFaction(registry, entry.FactionId, context + ".factionId", report, allowEmpty: false);
                    if (!hasRole)
                        report.Add(ErrorCode.InvalidArgument,
                            "Spawn factionMode=Override requires a non-None factionRole.", context + ".factionRole");
                    return;

                case OpeningFactionMode.Unaffiliated:
                    // Unaffiliated：禁止 factionId/factionRole。
                    if (hasFaction || !string.IsNullOrWhiteSpace(entry.FactionRole))
                    {
                        report.Add(ErrorCode.InvalidArgument,
                            "Spawn factionMode=Unaffiliated must not carry factionId/factionRole.", context + ".factionId");
                    }
                    return;

                case OpeningFactionMode.CharacterDefault:
                default:
                    if (modeExplicit)
                    {
                        // 显式 CharacterDefault：新格式 spawn 自己不得带 factionId/factionRole。
                        if (hasFaction || !string.IsNullOrWhiteSpace(entry.FactionRole))
                        {
                            report.Add(ErrorCode.InvalidArgument,
                                "Spawn factionMode=CharacterDefault must not carry factionId/factionRole (inherit CharacterDefinition).",
                                context + ".factionId");
                        }
                        return;
                    }

                    // mode 缺省：区分三态。
                    if (hasFaction)
                    {
                        // Legacy Explicit Override：无 mode 但显式 factionId → 按 Override 校验（deprecated）。
                        RequireFaction(registry, entry.FactionId, context + ".factionId", report, allowEmpty: false);
                        if (!hasRole)
                            report.Add(ErrorCode.InvalidArgument, "Spawn factionId requires a non-None factionRole.", context + ".factionRole");
                        return;
                    }

                    if (entry.AssignOpeningFaction)
                    {
                        // Legacy assignOpeningFaction：role 与 scenario.openingFactionId 的隐式继承仍被接受。
                        if (!hasRole)
                            report.Add(ErrorCode.InvalidArgument, "Legacy assignOpeningFaction requires a non-None factionRole.", context + ".factionRole");
                        return;
                    }

                    if (!string.IsNullOrWhiteSpace(entry.FactionRole))
                        report.Add(ErrorCode.InvalidArgument, "Spawn factionRole requires factionId.", context + ".factionRole");
                    return;
            }
        }

        /// <summary>factionId 必须能解析为 DefinitionId 且存在于 StrategicFactions；成员资格不检查 territorySelectable。</summary>
        static void RequireFaction(
            DefinitionRegistry registry,
            string factionId,
            string ctx,
            ValidationReport report,
            bool allowEmpty = true)
        {
            if (string.IsNullOrEmpty(factionId))
            {
                if (!allowEmpty)
                    report.Add(ErrorCode.MissingRequiredField, "strategicFaction reference required.", ctx);
                return;
            }
            if (!DefinitionId.TryParse(factionId, out var id) ||
                !registry.TryGetStrategicFaction(id, out _))
            {
                report.Add(
                    ErrorCode.NotFound,
                    "strategicFaction reference missing: " + factionId,
                    ctx);
            }
        }

        static void ValidateFormalArmies(DefinitionRegistry registry, ValidationReport report)
        {
            var seenArmyIds = new HashSet<string>(StringComparer.Ordinal);
            var seenStackIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var kv in registry.FormalArmies)
            {
                var def = kv.Value;
                var ctx = def.Id.ToString();
                if (def.Members == null || def.Members.Count == 0)
                {
                    report.Add(ErrorCode.MissingRequiredField, "formalArmy.members empty.", ctx);
                    continue;
                }

                if (!string.IsNullOrEmpty(def.RuntimeArmyId) && !seenArmyIds.Add(def.RuntimeArmyId))
                {
                    report.Add(
                        ErrorCode.DuplicateDefinitionId,
                        "Duplicate formalArmy.runtimeArmyId.",
                        ctx + ":" + def.RuntimeArmyId);
                }

                if (!string.IsNullOrEmpty(def.RuntimeStackId) && !seenStackIds.Add(def.RuntimeStackId))
                {
                    report.Add(
                        ErrorCode.DuplicateDefinitionId,
                        "Duplicate formalArmy.runtimeStackId.",
                        ctx + ":" + def.RuntimeStackId);
                }

                for (var i = 0; i < def.Members.Count; i++)
                {
                    var member = def.Members[i];
                    if (member == null)
                        continue;
                    RequireDef(
                        registry,
                        member.CharacterDefinitionId,
                        "character",
                        ctx + ".members[" + i + "].characterDefinitionId",
                        report);
                }
            }
        }

        void ValidateWorldRegions(
            DefinitionRegistry registry,
            HashSet<string> locations,
            ValidationReport report)
        {
            foreach (var kv in registry.WorldRegions)
            {
                var region = kv.Value;
                var ctx = region.Id.ToString();
                if (!string.IsNullOrEmpty(region.StartLocationId) && !locations.Contains(region.StartLocationId))
                    report.Add(ErrorCode.NotFound, "startLocationId missing in locations.", ctx + ":" + region.StartLocationId);

                if (region.Locations == null)
                    continue;
                for (var i = 0; i < region.Locations.Count; i++)
                {
                    var loc = region.Locations[i];
                    var lctx = ctx + "." + loc.Id;
                    if (loc.AdjacentIds != null)
                    {
                        for (var a = 0; a < loc.AdjacentIds.Count; a++)
                        {
                            if (!locations.Contains(loc.AdjacentIds[a]))
                                report.Add(ErrorCode.NotFound, "adjacent location missing.", lctx + "->" + loc.AdjacentIds[a]);
                        }
                    }

                    RequireDef(registry, loc.OpportunitySiteId, "opportunitySite", lctx + ".opportunitySiteId", report);
                    RequireDef(registry, loc.ResidentNpcDefinitionId, "character", lctx + ".residentNpc", report);
                    RequireDef(registry, loc.ResourceOnExploreId, "resource", lctx + ".resourceOnExploreId", report);
                    ScanConditions(loc.EnterConditions, registry, locations, null, null, lctx + ".enter", report);
                    if (loc.QuestOfferIds != null)
                    {
                        for (var q = 0; q < loc.QuestOfferIds.Count; q++)
                            RequireDef(registry, loc.QuestOfferIds[q], "quest", lctx + ".questOffer", report);
                    }
                }
            }
        }

        void ValidateLocalPlaceSets(
            DefinitionRegistry registry,
            HashSet<string> locations,
            ValidationReport report)
        {
            foreach (var kv in registry.LocalPlaceSets)
            {
                var set = kv.Value;
                var ctx = set.Id.ToString();
                if (!string.IsNullOrEmpty(set.MapLayoutId))
                {
                    var mapParsed = DefinitionId.Parse(set.MapLayoutId);
                    if (mapParsed.IsFailure || !registry.MapLayouts.ContainsKey(mapParsed.Value))
                    {
                        report.Add(
                            ErrorCode.NotFound,
                            "localPlaceSet.mapLayoutId missing.",
                            ctx + ":" + set.MapLayoutId);
                    }
                }

                if (!string.IsNullOrEmpty(set.StartLocationId) && !locations.Contains(set.StartLocationId))
                    report.Add(ErrorCode.NotFound, "startLocationId missing in locations.", ctx + ":" + set.StartLocationId);

                if (set.Locations == null)
                    continue;
                for (var i = 0; i < set.Locations.Count; i++)
                {
                    var loc = set.Locations[i];
                    var lctx = ctx + "." + loc.Id;
                    if (loc.AdjacentIds != null)
                    {
                        for (var a = 0; a < loc.AdjacentIds.Count; a++)
                        {
                            if (!locations.Contains(loc.AdjacentIds[a]))
                                report.Add(ErrorCode.NotFound, "adjacent location missing.", lctx + "->" + loc.AdjacentIds[a]);
                        }
                    }

                    RequireDef(registry, loc.OpportunitySiteId, "opportunitySite", lctx + ".opportunitySiteId", report);
                    RequireDef(registry, loc.ResidentNpcDefinitionId, "character", lctx + ".residentNpc", report);
                    RequireDef(registry, loc.ResourceOnExploreId, "resource", lctx + ".resourceOnExploreId", report);
                    ScanConditions(loc.EnterConditions, registry, locations, null, null, lctx + ".enter", report);
                    if (loc.QuestOfferIds != null)
                    {
                        for (var q = 0; q < loc.QuestOfferIds.Count; q++)
                            RequireDef(registry, loc.QuestOfferIds[q], "quest", lctx + ".questOffer", report);
                    }
                }
            }
        }

        void ValidateQuests(
            DefinitionRegistry registry,
            HashSet<string> locations,
            HashSet<string> producedFlags,
            HashSet<string> consumedFlags,
            ValidationReport report)
        {
            foreach (var kv in registry.Quests)
            {
                var q = kv.Value;
                var ctx = q.Id.ToString();
                ScanConditions(q.OfferConditions, registry, locations, producedFlags, consumedFlags, ctx + ".offer", report);
                ScanConditions(q.CompleteConditions, registry, locations, producedFlags, consumedFlags, ctx + ".complete", report);
                ScanConditions(q.FailConditions, registry, locations, producedFlags, consumedFlags, ctx + ".fail", report);
                ScanOutcomes(q.Rewards, registry, locations, producedFlags, consumedFlags, ctx + ".reward", report);
                ScanOutcomes(q.FailResults, registry, locations, producedFlags, consumedFlags, ctx + ".failResult", report);
            }
        }

        void ValidateContentEvents(
            DefinitionRegistry registry,
            HashSet<string> locations,
            HashSet<string> producedFlags,
            HashSet<string> consumedFlags,
            ValidationReport report)
        {
            foreach (var kv in registry.ContentEvents)
            {
                var e = kv.Value;
                var ctx = e.Id.ToString();
                if (!string.IsNullOrEmpty(e.LocationId) && !locations.Contains(e.LocationId))
                    report.Add(ErrorCode.NotFound, "contentEvent.locationId missing.", ctx + ":" + e.LocationId);
                RequireDef(registry, e.QuestId, "quest", ctx + ".questId", report);
                ScanConditions(e.Conditions, registry, locations, producedFlags, consumedFlags, ctx + ".cond", report);
                if (e.Choices == null)
                    continue;
                for (var i = 0; i < e.Choices.Count; i++)
                {
                    var c = e.Choices[i];
                    var cctx = ctx + ".choice." + c.Id;
                    ScanConditions(c.Conditions, registry, locations, producedFlags, consumedFlags, cctx, report);
                    ScanOutcomes(c.Outcomes, registry, locations, producedFlags, consumedFlags, cctx, report);
                }
            }
        }

        void ValidateChapters(
            DefinitionRegistry registry,
            HashSet<string> locations,
            HashSet<string> producedFlags,
            HashSet<string> consumedFlags,
            ValidationReport report)
        {
            foreach (var kv in registry.Chapters)
            {
                var ch = kv.Value;
                var ctx = ch.Id.ToString();
                // openingScenarioId is documentary; warn only if set and missing
                if (!string.IsNullOrEmpty(ch.OpeningScenarioId))
                    RequireDef(registry, ch.OpeningScenarioId, "openingScenario", ctx + ".openingScenarioId", report);

                for (var i = 0; i < ch.QuestChainIds.Count; i++)
                    RequireDef(registry, ch.QuestChainIds[i], "quest", ctx + ".questChain", report);
                for (var i = 0; i < ch.EventChainIds.Count; i++)
                    RequireDef(registry, ch.EventChainIds[i], "contentEvent", ctx + ".eventChain", report);

                for (var b = 0; b < ch.DayBeats.Count; b++)
                {
                    var beat = ch.DayBeats[b];
                    var bctx = ctx + ".dayBeat[" + beat.DayIndex + "]";
                    ScanConditions(beat.Conditions, registry, locations, producedFlags, consumedFlags, bctx, report);
                    for (var i = 0; i < beat.QuestOfferIds.Count; i++)
                        RequireDef(registry, beat.QuestOfferIds[i], "quest", bctx + ".questOffer", report);
                    for (var i = 0; i < beat.ContentEventIds.Count; i++)
                        RequireDef(registry, beat.ContentEventIds[i], "contentEvent", bctx + ".event", report);
                    for (var i = 0; i < beat.SetFlags.Count; i++)
                    {
                        if (!string.IsNullOrEmpty(beat.SetFlags[i]))
                            producedFlags.Add(beat.SetFlags[i]);
                    }
                }
            }
        }

        void ScanConditions(
            IList<ContentCondition> conditions,
            DefinitionRegistry registry,
            HashSet<string> locations,
            HashSet<string> producedFlags,
            HashSet<string> consumedFlags,
            string ctx,
            ValidationReport report)
        {
            if (conditions == null)
                return;
            for (var i = 0; i < conditions.Count; i++)
            {
                var c = conditions[i];
                if (c == null || string.IsNullOrEmpty(c.Kind))
                    continue;
                var kind = c.Kind.Trim().ToLowerInvariant();
                switch (kind)
                {
                    case "atlocation":
                    case "exploredlocation":
                        if (!string.IsNullOrEmpty(c.Id) && !locations.Contains(c.Id))
                            report.Add(ErrorCode.NotFound, "condition location missing.", ctx + ":" + c.Id);
                        break;
                    case "laboratlocation":
                    case "uniquelaboratlocation":
                    case "uniqueharvestatlocation":
                        if (!string.IsNullOrEmpty(c.Id) && !locations.Contains(c.Id))
                            report.Add(ErrorCode.NotFound, "condition location missing.", ctx + ":" + c.Id);
                        if (kind == "laboratlocation" && !string.IsNullOrEmpty(c.CharacterId))
                            RequireDef(registry, c.CharacterId, "character", ctx + "." + c.Kind, report);
                        break;
                    case "characteratlocation":
                        if (!string.IsNullOrEmpty(c.Id) && !locations.Contains(c.Id))
                            report.Add(ErrorCode.NotFound, "condition location missing.", ctx + ":" + c.Id);
                        if (!string.IsNullOrEmpty(c.CharacterId))
                            RequireDef(registry, c.CharacterId, "character", ctx + "." + c.Kind, report);
                        break;
                    case "knowssite":
                        RequireDef(registry, c.Id, "opportunitySite", ctx + ".knowsSite", report);
                        break;
                    case "stockatleast":
                        RequireDef(registry, c.Id, "resource", ctx + ".stock", report);
                        break;
                    case "questactive":
                    case "questcompleted":
                        RequireDef(registry, c.Id, "quest", ctx + ".quest", report);
                        break;
                    case "hasmanual":
                        RequireDef(registry, c.Id, "cultivation", ctx + ".manual", report);
                        break;
                    case "counteratleast":
                    case "missingdailyflag":
                    case "hasdailyflag":
                        if (string.IsNullOrEmpty(c.Id))
                        {
                            report.Add(
                                ErrorCode.MissingRequiredField,
                                c.Kind + " requires id.",
                                ctx);
                        }

                        break;
                    case "encountercleared":
                        if (string.IsNullOrEmpty(c.Id))
                        {
                            report.Add(
                                ErrorCode.MissingRequiredField,
                                "encounterCleared requires id.",
                                ctx);
                        }
                        else if (consumedFlags != null)
                        {
                            consumedFlags.Add(ContentConditionEvaluator.EncounterFlag(c.Id));
                        }

                        break;
                    case "hasflag":
                    case "storyflag":
                    case "missingflag":
                    case "missingstoryflag":
                        if (!string.IsNullOrEmpty(c.Id) && consumedFlags != null)
                            consumedFlags.Add(c.Id);
                        break;
                }
            }
        }

        void ScanOutcomes(
            IList<ContentOutcome> outcomes,
            DefinitionRegistry registry,
            HashSet<string> locations,
            HashSet<string> producedFlags,
            HashSet<string> consumedFlags,
            string ctx,
            ValidationReport report)
        {
            if (outcomes == null)
                return;
            for (var i = 0; i < outcomes.Count; i++)
            {
                var o = outcomes[i];
                if (o == null || string.IsNullOrEmpty(o.Kind))
                    continue;
                var kind = o.Kind.Trim().ToLowerInvariant();
                switch (kind)
                {
                    case "setflag":
                    case "setstoryflag":
                        if (!string.IsNullOrEmpty(o.Id) && producedFlags != null)
                            producedFlags.Add(o.Id);
                        break;
                    case "clearflag":
                    case "clearstoryflag":
                        if (!string.IsNullOrEmpty(o.Id) && consumedFlags != null)
                            consumedFlags.Add(o.Id);
                        break;
                    case "addstock":
                    {
                        if (string.IsNullOrEmpty(o.Id) || !DefinitionId.TryParse(o.Id, out var stockId))
                        {
                            RequireDef(registry, o.Id, "resource", ctx + ".addStock", report);
                            break;
                        }

                        if (registry.Resources.ContainsKey(stockId) || registry.Items.ContainsKey(stockId))
                            break;
                        report.Add(
                            ErrorCode.NotFound,
                            "resource/item reference missing.",
                            ctx + ".addStock:" + o.Id);
                        break;
                    }
                    case "startquest":
                        RequireDef(registry, o.Id, "quest", ctx + ".startQuest", report);
                        break;
                    case "discoversite":
                        RequireDef(registry, o.Id, "opportunitySite", ctx + ".discoverSite", report);
                        break;
                    case "learnmanual":
                        RequireDef(registry, o.Id, "cultivation", ctx + ".learnManual", report);
                        break;
                    case "addcounter":
                    case "setcounter":
                    case "setdailyflag":
                    case "cleardailyflag":
                        if (string.IsNullOrEmpty(o.Id))
                        {
                            report.Add(
                                ErrorCode.MissingRequiredField,
                                o.Kind + " requires id.",
                                ctx);
                        }

                        break;
                    case "setencountercleared":
                        if (string.IsNullOrEmpty(o.Id))
                        {
                            report.Add(
                                ErrorCode.MissingRequiredField,
                                o.Kind + " requires id.",
                                ctx);
                        }
                        else if (producedFlags != null)
                        {
                            producedFlags.Add(ContentConditionEvaluator.EncounterFlag(o.Id));
                        }

                        break;
                    case "relationdelta":
                        RequireDef(registry, o.FromDefinitionId, "character", ctx + ".relation.from", report);
                        if (o.ToDefinitionIds.Count > 0)
                        {
                            for (var ti = 0; ti < o.ToDefinitionIds.Count; ti++)
                            {
                                var targetId = o.ToDefinitionIds[ti];
                                if (string.Equals(targetId, "@party", StringComparison.OrdinalIgnoreCase))
                                    continue;
                                RequireDef(registry, targetId, "character", ctx + ".relation.to", report);
                            }
                        }
                        else if (!string.IsNullOrEmpty(o.ToDefinitionId))
                        {
                            if (!string.Equals(o.ToDefinitionId, "@party", StringComparison.OrdinalIgnoreCase))
                                RequireDef(registry, o.ToDefinitionId, "character", ctx + ".relation.to", report);
                        }
                        else
                        {
                            report.Add(
                                ErrorCode.MissingRequiredField,
                                "relationDelta requires toDefinitionId or toDefinitionIds.",
                                ctx);
                        }

                        break;
                }
            }
        }

        static void ValidateFlagConsumers(
            HashSet<string> producedFlags,
            HashSet<string> consumedFlags,
            ValidationReport report)
        {
            foreach (var flag in consumedFlags)
            {
                if (IsRuntimeProducedFlag(flag))
                    continue;
                if (!producedFlags.Contains(flag))
                {
                    report.Add(
                        ErrorCode.NotFound,
                        "Flag consumed but never produced by content outcomes／dayBeats.",
                        flag);
                }
            }
        }

        static bool IsRuntimeProducedFlag(string flag)
        {
            if (string.IsNullOrEmpty(flag))
                return true;
            // Core exploration writes explored:<locationId>
            if (flag.StartsWith("explored:", StringComparison.Ordinal))
                return true;
            // setEncounterCleared writes encounter:<id>
            if (flag.StartsWith("encounter:", StringComparison.Ordinal))
                return true;
            // SupervisorPressureHandler writes this at day end.
            if (string.Equals(flag, "story:supervisor_pressure", StringComparison.Ordinal))
                return true;
            return false;
        }

        static void RequireDef(
            DefinitionRegistry registry,
            string idText,
            string expectedKind,
            string context,
            ValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(idText))
                return;
            if (!DefinitionId.TryParse(idText, out var id))
            {
                report.Add(ErrorCode.InvalidDefinitionId, "Invalid DefinitionId.", context + ":" + idText);
                return;
            }

            var ok = false;
            switch (expectedKind)
            {
                case "character":
                    ok = registry.Characters.ContainsKey(id);
                    break;
                case "quest":
                    ok = registry.Quests.ContainsKey(id);
                    break;
                case "contentEvent":
                    ok = registry.ContentEvents.ContainsKey(id);
                    break;
                case "chapter":
                    ok = registry.Chapters.ContainsKey(id);
                    break;
                case "openingScenario":
                    ok = registry.OpeningScenarios.ContainsKey(id);
                    break;
                case "worldRegion":
                    ok = registry.WorldRegions.ContainsKey(id);
                    break;
                case "hexWorld":
                    ok = registry.HexWorldContents.ContainsKey(id);
                    break;
                case "localPlaceSet":
                    ok = registry.LocalPlaceSets.ContainsKey(id);
                    break;
                case "settlement":
                    ok = registry.Settlements.ContainsKey(id);
                    break;
                case "job":
                    ok = registry.Jobs.ContainsKey(id);
                    break;
                case "workArea":
                    ok = registry.WorkAreas.ContainsKey(id);
                    break;
                case "schedule":
                    ok = registry.Schedules.ContainsKey(id);
                    break;
                case "resource":
                    ok = registry.Resources.ContainsKey(id);
                    break;
                case "opportunitySite":
                    ok = registry.OpportunitySites.ContainsKey(id);
                    break;
                case "cultivation":
                    ok = registry.Cultivations.ContainsKey(id);
                    break;
                case "combatArt":
                    ok = registry.CombatArts.ContainsKey(id);
                    break;
                case "facility":
                    ok = registry.Facilities.ContainsKey(id);
                    break;
                case "item":
                    ok = registry.Items.ContainsKey(id);
                    break;
                case "spawnTable":
                    ok = registry.SpawnTables.ContainsKey(id);
                    break;
                case "formalArmy":
                    ok = registry.FormalArmies.ContainsKey(id);
                    break;
            }

            if (!ok)
                report.Add(ErrorCode.NotFound, expectedKind + " reference missing.", context + ":" + idText);
        }
    }
}
