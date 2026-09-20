using System;
using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Persistence;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.Settlement;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;
using XianXia.Data.Bootstrap;
using XianXia.Data.Content;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Snapshot Restore 后补全 Surface、Site、独立 LocalMap 与 PartyWorld；不 respawn Character。
    /// </summary>
    public static class HostSnapshotSessionRehydration
    {
        public static int LastLegacyAnchorMigrated { get; private set; }

        public static Result RehydrateAfterRestore(PlayableHostBootstrap bootstrap)
        {
            LastLegacyAnchorMigrated = 0;
            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return Result.Failure(ErrorCode.InvalidOperation, "Snapshot session is not initialized.");

            var session = bootstrap.Session;
            var world = session.World;
            var registry = session.Registry;
            if (world == null || registry == null)
                return Result.Failure(ErrorCode.InvalidOperation, "Snapshot world or content registry is missing.");

            // 先恢复 Item／功法／战技／境界／工作等静态定义壳；不重跑开局或重置存档状态。
            var contentShell = RuntimeContentShellBootstrap.Rehydrate(world, registry);
            if (contentShell.IsFailure)
            {
                return Result.Failure(ErrorCode.ContentLoadFailed,
                    "Static content shell rehydrate failed: " + contentShell.Error.Message,
                    contentShell.Error.ToString());
            }

            var scenarioParsed = XianXia.Core.Domain.Ids.DefinitionId.Parse(bootstrap.OpeningScenarioId ?? string.Empty);
            var scenarioId = scenarioParsed.IsSuccess
                ? scenarioParsed.Value
                : PlayableDayBootstrap.DefaultScenarioId;
            if (!registry.TryGetOpeningScenario(scenarioId, out var scenario) || scenario == null)
                return Result.Failure(ErrorCode.ContentLoadFailed,
                    "Snapshot opening scenario is missing.", scenarioId.ToString());

            var politicalSnapshot = session.PendingRestoredStrategicSnapshot;
            if (politicalSnapshot == null)
                return Result.Failure(ErrorCode.SnapshotInvalid, "Pending strategic snapshot is missing.");
            // Snapshot DTO restoration precedes content-shell registration of Surface navigation.
            // Rebuild the saved exact travel intent now that the authored route is available.
            if (politicalSnapshot.PlayerPartyTravel?.IsMoving == true &&
                politicalSnapshot.PlayerPartyTravel.HasContinuousPhysicalDestination)
            {
                PlayerPartyTransitionMembership.CaptureTravelingMembersForPartyTransition(
                    world, session.PlayerParty);
                StrategicSnapshotHelper.RestorePlayerPartyTravel(
                    world, politicalSnapshot.PlayerPartyTravel);
                PlayerPartyTransitionMembership.ReconcilePlayerPartyMemberWorldPresenceFromMotion(
                    world, session.PlayerParty, "SnapshotSurfaceTravelResume");
            }
            StrategicSnapshotHelper.RestoreBackgroundSurfaceTravels(
                world, politicalSnapshot.BackgroundCharacterTravels);

            // Old snapshots could contain an exact personal point without Surface provenance.
            // Repair once at restore, only when exactly one registered authored Surface owns it.
            if (politicalSnapshot.CharacterWorldPresences != null)
                foreach (var saved in politicalSnapshot.CharacterWorldPresences)
                {
                    if (saved == null || saved.CharacterId == 0 || !saved.HasWorldPosition ||
                        !string.IsNullOrEmpty(saved.PersonalSurfaceId) ||
                        (saved.Mode != (int)PartyWorldPresenceMode.AtWorldPosition &&
                         saved.Mode != (int)PartyWorldPresenceMode.AtSite) ||
                        !world.WorldPresence.TryGet(new EntityId(saved.CharacterId), out var personal) ||
                        personal == null || !personal.HasContinuousWorldPosition ||
                        !string.IsNullOrEmpty(personal.PersonalSurfaceId))
                        continue;
                    string resolvedSurface = null;
                    var ambiguous = false;
                    foreach (var candidate in world.SurfaceGround.Registered)
                        if (candidate.Value.Contains(personal.WorldPosX, personal.WorldPosY))
                        {
                            if (resolvedSurface != null) { ambiguous = true; break; }
                            resolvedSurface = candidate.Key;
                        }
                    if (!ambiguous && resolvedSurface != null)
                        personal.PersonalSurfaceId = resolvedSurface;
                }

            var encounter = world.Strategic.CharacterEncounter;
            if (encounter != null)
            {
                OutdoorWorldSurfaceDefinition encounterSource = null;
                foreach (var pair in registry.OutdoorSurfaces)
                    if (pair.Value?.SurfaceId == encounter.SourceSurfaceId) encounterSource = pair.Value;
                if (encounterSource == null || encounterSource.AcceptanceOnly)
                    return Result.Failure(ErrorCode.SnapshotInvalid, "Encounter source is unavailable.");
                var members = new System.Collections.Generic.List<EncounterCharacter>(encounter.Participants);
                foreach (var candidate in encounter.Candidates) members.AddRange(candidate.Members);
                foreach (var member in members)
                    if (!OutdoorSurfaceCoverageResolver.ContainsWorldPosition(encounterSource, member.OriginX, member.OriginY) ||
                        !OutdoorSurfaceCoverageResolver.ContainsWorldPosition(encounterSource, member.TacticalX, member.TacticalY))
                        return Result.Failure(ErrorCode.SnapshotInvalid, "Encounter personal position outside source: " + member.CharacterId);
            }

            foreach (var pair in world.WorldPresence.All)
            {
                var personal = pair.Value;
                if (personal == null || string.IsNullOrEmpty(personal.PersonalSurfaceId) ||
                    world.Strategic.CharacterEncounter?.Find(personal.EntityId.Value) != null) continue;
                OutdoorWorldSurfaceDefinition source = null;
                foreach (var candidate in registry.OutdoorSurfaces)
                    if (string.Equals(candidate.Value?.SurfaceId, personal.PersonalSurfaceId,
                            System.StringComparison.Ordinal))
                        source = candidate.Value;
                if (source == null || source.AcceptanceOnly ||
                    !OutdoorSurfaceCoverageResolver.ContainsWorldPosition(source, personal.WorldPosX, personal.WorldPosY))
                    return Result.Failure(ErrorCode.SnapshotInvalid,
                        "Personal spatial authority references an unavailable surface/position: CharacterId=" + personal.EntityId.Value);
            }

            {
                var surfaceSites = StrategicContentBootstrap.ApplySurfaceSites(world, registry, scenario);
                if (surfaceSites.IsFailure)
                    return Result.Failure(ErrorCode.ContentLoadFailed,
                        "Surface Site snapshot shell rehydrate failed.", surfaceSites.Error.ToString());

                var openingMigration = RestoreMissingLegacyOpeningPresences(
                    world, registry, scenario, politicalSnapshot);
                if (openingMigration.IsFailure)
                    return openingMigration;

                var fixedCores = ContentRuntimeBootstrap.RebindPresetWorldSiteCoreMetadata(world, registry);
                if (fixedCores.IsFailure)
                    return Result.Failure(ErrorCode.ContentLoadFailed,
                        "Preset SiteCore snapshot shell rehydrate failed.", fixedCores.Error.ToString());

                var political = StrategicSnapshotHelper.RestoreHexPoliticalState(world, politicalSnapshot);
                if (political.IsFailure)
                    return political;
                // StorageRoom registry is derived presentation/economy wiring, but it needs the
                // complete authored/runtime WorldSite shell restored above. It must not run in
                // RuntimeContentShellBootstrap, where a snapshot world has no Site identities yet.
                var storageRooms = WorldSiteStorageRoomBootstrap.Rehydrate(world, registry);
                if (storageRooms.IsFailure)
                    return Result.Failure(ErrorCode.ContentLoadFailed,
                        "Storage room registry rehydrate failed: " + storageRooms.Error.Message,
                        storageRooms.Error.ToString());
                var economy = WorldSiteEconomyBootstrap.ApplyLegacySaveFallback(world, registry);
                if (economy.IsFailure)
                    return economy;
                // Political overlay does not replace the canonical static placement binding.
                SettlementAuthoritySync.Rebuild(world);

                var motions = StrategicSnapshotHelper.RestoreFormalArmyMotions(world, politicalSnapshot);
                if (motions.IsFailure)
                    return motions;
            }

            ResolvePartyWorldFromActiveControlledCharacter(world, session.PlayerParty);

            // SPACE-01：Separate Space 优先于 Outdoor ActiveControlled 解析。
            // Surface 已就绪后，再尝试旧档 migration。
            if (!world.LocalMap.IsActive)
            {
                var migrated = SeparateSpaceSessionSnapshotRestore.TryMigrateLegacySeparateSpace(
                    world, politicalSnapshot);
                if (migrated.IsFailure)
                    return migrated;
            }
            else
            {
                // Active Separate Space：覆盖 Outdoor resolver 可能写入的 return authority。
                if (world.LocalMap.HasOutdoorReturn)
                {
                    if (string.IsNullOrEmpty(world.LocalMap.ReturnSurfaceId) ||
                        !world.SurfaceGround.TryGet(world.LocalMap.ReturnSurfaceId, out var returnNav) ||
                        !returnNav.Contains(world.LocalMap.ReturnWorldX, world.LocalMap.ReturnWorldY))
                        return Result.Failure(ErrorCode.SnapshotInvalid,
                            "SeparateSpace return Surface unavailable after content shell.",
                            world.LocalMap.ReturnSurfaceId);
                }

                world.PartyWorld.LocalMapId = world.LocalMap.ActiveMapLayoutId;
                world.PartyWorld.SiteId = string.Empty;
                world.PartyWorld.Mode = PartyWorldPresenceMode.InSeparateSpace;
            }

            var mapId = world.LocalMap.IsActive
                ? (world.LocalMap.ActiveMapLayoutId?.Trim() ?? string.Empty)
                : (world.PartyWorld?.LocalMapId?.Trim() ?? string.Empty);
            if (mapId == "base:map_world_node_stub")
            {
                mapId = StrategicEncounterCatalog.DefaultEncounterLocalMapId;
                world.PartyWorld.LocalMapId = mapId;
            }
            if (!world.LocalMap.IsActive && IsRetiredOutdoorMapId(mapId))
            {
                if (!world.PlayerPartyTravel.HasPosition ||
                    !world.SurfaceGround.TryResolveContaining(world.PlayerPartyTravel.WorldPosition, out _))
                    return Result.Failure(ErrorCode.SnapshotInvalid,
                        "Legacy Outdoor LocalMap cannot be migrated to a valid Surface position.", mapId);
                world.PartyWorld.ClearSiteFocus();
                world.PartyWorld.LocalMapId = string.Empty;
                // Retired outdoor LocalMap snapshots are migrated onto the restored exact Surface
                // position above. Keep PartyWorld as a non-authoritative presentation summary.
                world.PartyWorld.Mode = PartyWorldPresenceMode.AtWorldPosition;
                mapId = string.Empty;
            }
            if (!string.IsNullOrEmpty(mapId))
            {
                session.PreferredMapLayoutId = mapId;
                bootstrap.ConfigurePreferredMapLayout(mapId);
                var places = InteriorLocalPlaceBootstrap.ActivatePlacesForMapLayout(world, registry, mapId);
                if (places.IsFailure)
                    return Result.Failure(ErrorCode.ContentLoadFailed,
                        "Interior LocalPlace snapshot shell rehydrate failed.", places.Error.ToString());
                if (world.LocalMap.IsActive)
                    world.LocalMap.ActiveLocalPlaceSetId = world.LocalPlaces.RegionId ?? string.Empty;
                RestoreLegacyAuthoredEntityLocations(world, session.PlayerParty, scenario);
                if (world.LocalMap.IsActive)
                    LoadedLocalMapPlacementSnapshotRestore.ApplySavedPlacementsToDomain(world, mapId);
                if (world.LocalMap.IsActive)
                    SeparateSpaceTransitionService.ReconcileActiveSeparateSpaceWorldPresence(world);
            }

            // 全部 Content shell 与 motion overlay 均成功后，才同步成员／presentation／pursuit，
            // 最后才允许进入 LocalMap materialization。
            StrategicSnapshotHelper.FinalizeRuntimeLinks(world);
            CharacterEncounterService.BindRuntime(world);
            // Separate Space 已有完整 session；禁止 ApplyLocalMapSessionFromFocus 清掉 occupants／return。
            if (!string.IsNullOrEmpty(mapId) && !world.LocalMap.IsActive)
                WorldTravelService.ApplyLocalMapSessionFromFocus(world);
            var presenceInvariant = StrategicSnapshotHelper.ValidateRestoredCharacterWorldPresences(
                world, politicalSnapshot);
            if (presenceInvariant.IsFailure)
                return presenceInvariant;
            session.ConsumePendingRestoredStrategicSnapshot();
            session.RefreshViewableEntityIds();
            return Result.Success();
        }

        static bool IsRetiredOutdoorMapId(string mapId) =>
            !string.IsNullOrEmpty(mapId) &&
            (mapId == "base:map_ch01_reference" ||
             mapId == "base:map_player_camp" ||
             mapId == "base:map_huangcun_01" ||
             mapId.StartsWith("base:map_site_", System.StringComparison.Ordinal) ||
             mapId.StartsWith("base:map_wilderness_", System.StringComparison.Ordinal));

        /// <summary>
        /// Best-effort Opening anchor inference is only for a genuinely legacy Character:
        /// modern EntityLocation snapshot authority and active Separate Space ownership both win.
        /// </summary>
        static Result RestoreMissingLegacyOpeningPresences(
            SimulationWorld world, DefinitionRegistry registry, OpeningScenarioDefinition scenario,
            StrategicSnapshotDto saved)
        {
            if (scenario?.Spawns == null || string.IsNullOrWhiteSpace(scenario.OpeningSurfaceId) ||
                !DefinitionId.TryParse(scenario.OpeningSurfaceId, out var surfaceId) ||
                !registry.TryGetOutdoorSurface(surfaceId, out var surface) || surface == null)
                return Result.Success();
            var savedIds = new HashSet<ulong>();
            if (saved?.CharacterWorldPresences != null)
                foreach (var presence in saved.CharacterWorldPresences)
                    if (presence != null) savedIds.Add(presence.CharacterId);
            var ordinals = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var spawn in scenario.Spawns)
            {
                if (spawn == null || string.IsNullOrWhiteSpace(spawn.DefinitionId)) continue;
                var definitionId = spawn.DefinitionId.Trim();
                ordinals.TryGetValue(definitionId, out var ordinal);
                ordinals[definitionId] = ordinal + 1;
                var matching = EntityId.None;
                Entity matchedEntity = null;
                var matches = 0;
                foreach (var entity in world.Entities.All)
                    if (entity != null &&
                        string.Equals(entity.DefinitionId.ToString(), definitionId, StringComparison.Ordinal))
                    {
                        matching = entity.Id;
                        matchedEntity = entity;
                        matches++;
                    }
                if (matches == 0) continue; // The old save may have removed this entity.
                // A modern snapshot explicitly owns both EntityLocation and the intentional absence
                // of Outdoor WorldPresence. Do not even enter legacy identity inference for it.
                var hasSnapshotLocationAuthority =
                    matchedEntity.TryGet<EntityLocationSnapshotAuthorityComponent>(out var snapshotAuthority) &&
                    snapshotAuthority.SnapshotFieldPresent;
                // Active Separate Space has stronger spatial authority even for legacy snapshots.
                var ownedByActiveSeparateSpace =
                    SeparateSpaceTransitionService.IsOwnedByActiveSeparateSpace(world, matching);
                if (matches != 1)
                {
                    if (hasSnapshotLocationAuthority || ownedByActiveSeparateSpace)
                        continue;
                    if (!savedIds.Contains(matching.Value))
                        return Result.Failure(ErrorCode.SnapshotInvalid,
                            "Old-save opening spawn identity is ambiguous.", definitionId);
                    continue;
                }
                world.OpeningSpawnIdentities.Register(
                    matching, OpeningSpawnIdentityBoard.BuildStableKey(definitionId, ordinal));
                if (hasSnapshotLocationAuthority || ownedByActiveSeparateSpace)
                    continue;
                if (savedIds.Contains(matching.Value) ||
                    world.WorldPresence.TryGet(matching, out _) ||
                    ArmyService.TryGetArmyForCharacter(world, matching, out _))
                    continue;
                if (!ContinuousOpeningSpawnPresenceResolver.TryApply(
                        world, surface, matching, definitionId, spawn.WorldSiteId, out var failure))
                    return Result.Failure(ErrorCode.SnapshotInvalid,
                        "Old-save opening anchor migration failed.", failure);
                Debug.Log("[SnapshotOpeningAnchorMigrated] Entity=" + matching.Value +
                          " SpawnKey=" + definitionId);
                LastLegacyAnchorMigrated++;
            }
            return Result.Success();
        }

        /// <summary>
        /// 旧 v6 没有 EntityLocation 字段，只能对唯一 Opening spawn 恢复 authored Location。
        /// 新格式 Snapshot 一律以自身 Location authority 为准；绝不重生实体或用 DefinitionId 猜重复实例。
        /// </summary>
        static void RestoreLegacyAuthoredEntityLocations(
            SimulationWorld world,
            PlayerPartyRuntime party,
            OpeningScenarioDefinition scenario)
        {
            if (world == null || scenario?.Spawns == null)
                return;
            foreach (var entity in world.Entities.All)
            {
                if (entity == null || (entity.Tags & EntityTag.Character) == 0 ||
                    IsPartyMember(party, entity.Id) ||
                    ArmyService.TryGetArmyForCharacter(world, entity.Id, out _) ||
                    world.WorldPresence.TryGet(entity.Id, out _))
                    continue;
                if (entity.TryGet<EntityLocationSnapshotAuthorityComponent>(out var snapshotAuthority) &&
                    snapshotAuthority.SnapshotFieldPresent)
                    continue;
                if (entity.TryGet<EntityLocationComponent>(out var existing) && existing.HasLocation)
                    continue;

                var matches = 0;
                OpeningSpawnEntry unique = null;
                var definitionId = entity.DefinitionId.ToString();
                for (var i = 0; i < scenario.Spawns.Count; i++)
                {
                    var candidate = scenario.Spawns[i];
                    if (candidate == null || !string.Equals(candidate.DefinitionId, definitionId, System.StringComparison.Ordinal))
                        continue;
                    matches++;
                    unique = candidate;
                }
                if (matches != 1 || unique == null || string.IsNullOrEmpty(unique.LocalLocationId))
                {
                    if (matches > 1)
                        Debug.LogWarning("[SnapshotRestore] 跳过旧档地点回填：Opening spawn 非唯一 " + definitionId);
                    continue;
                }

                var location = existing ?? new EntityLocationComponent();
                location.LocationId = unique.LocalLocationId;
                if (existing == null)
                    entity.AddComponent(location);
            }
        }

        static bool IsPartyMember(PlayerPartyRuntime party, XianXia.Core.Domain.Ids.EntityId entityId)
        {
            if (party?.Members == null)
                return false;
            for (var i = 0; i < party.Members.Count; i++)
                if (party.Members[i] == entityId)
                    return true;
            return false;
        }

        /// <summary>
        /// ActiveControlledCharacter WorldLocation → Required LocalMap（覆盖 stale PartyWorld cache）。
        /// </summary>
        public static void ResolvePartyWorldFromActiveControlledCharacter(
            SimulationWorld world,
            PlayerPartyRuntime party)
        {
            if (world?.PartyWorld == null)
                return;

            if (!SnapshotActiveControlledLocalMapResolver.TryResolveRequiredLocalMap(
                    world,
                    party,
                    out var resolved) ||
                !resolved.HasValue)
            {
                SyncPartyWorldPresentationCacheFallback(world, party);
                return;
            }

            SnapshotActiveControlledLocalMapResolver.ApplyResolvedPartyWorldFocus(world, in resolved);
        }

        static void SyncPartyWorldPresentationCacheFallback(
            SimulationWorld world,
            PlayerPartyRuntime party)
        {
            if (world?.PartyWorld == null)
                return;

            if (!PlayerPartyWorldLocationQuery.TryResolve(world, party, out var resolved) ||
                !resolved.HasValue)
                return;

            world.PartyWorld.EncounterId = string.Empty;
            world.PartyWorld.FocusFormalArmyId = string.Empty;

            if (resolved.LocationKind == PlayerPartyLocationKind.AtWorldSite &&
                !string.IsNullOrEmpty(resolved.SiteId))
            {
                world.PartyWorld.SiteId = resolved.SiteId;
                world.PartyWorld.LocalMapId = resolved.ResolvedLocalMapId ?? string.Empty;
                world.PartyWorld.Mode = PartyWorldPresenceMode.AtSite;
                return;
            }

            world.PartyWorld.ClearSiteFocus();
            world.PartyWorld.LocalMapId = resolved.ResolvedLocalMapId ?? string.Empty;
            // TryResolve returns AtWorldPosition for normal Continuous runtime. The AtHex fallback
            // below is restricted to legacy Outdoor LocalMap / old Hex snapshot compatibility.
            world.PartyWorld.Mode = resolved.LocationKind == PlayerPartyLocationKind.AtWorldPosition
                ? PartyWorldPresenceMode.AtWorldPosition
                : PartyWorldPresenceMode.AtHex;
        }

        public static void LogDomainTrace(PlayableHostSession session, string phase)
        {
            if (session == null || !session.IsInitialized)
                return;

            var world = session.World;
            var characterCount = 0;
            foreach (var entity in world.Entities.All)
            {
                if (entity != null && (entity.Tags & XianXia.Core.Entities.EntityTag.Character) != 0)
                    characterCount++;
            }

            var partyMembers = session.PlayerParty?.Count ?? 0;
            var active = session.PlayerParty?.ActiveCharacterId.Value ?? 0UL;
            var armyCount = world.Strategic?.FormalArmies?.Armies?.Count ?? 0;

            SnapshotActiveControlledLocalMapResolver.TryResolveRequiredLocalMap(
                world,
                session.PlayerParty,
                out var required);

            var sb = new System.Text.StringBuilder();
            sb.Append("[SnapshotRestore.Domain] phase=").Append(phase);
            sb.Append(" RuntimeCharacterCount=").Append(characterCount);
            sb.Append(" ActiveCharacterId=").Append(active);
            sb.Append(" PlayerPartyMembers=").Append(partyMembers);
            sb.Append(" RequiredLocalMap=").Append(required.LocalMapId ?? string.Empty);
            sb.Append(" RequiredWorldLocation=").Append(required.WorldLocationLabel ?? string.Empty);
            sb.Append(" PartyWorld.Site=").Append(world.PartyWorld?.SiteId ?? string.Empty);
            sb.Append(" PartyWorld.Map=").Append(world.PartyWorld?.LocalMapId ?? string.Empty);
            sb.Append(" ActiveLocalMap=").Append(world.LocalMap?.ActiveMapLayoutId ?? string.Empty);

            foreach (var entity in world.Entities.All)
            {
                if (entity == null || (entity.Tags & EntityTag.Character) == 0) continue;
                world.Strategic.Squads.TryGetForCharacter(entity.Id, out var squad);
                world.WorldPresence.TryGet(entity.Id, out var personal);
                entity.TryGet<EntityLocationComponent>(out var location);
                var participant = world.Strategic.Participants.FindByEntity(entity.Id);
                sb.Append("\nCharacterId=").Append(entity.Id.Value)
                    .Append(" SquadId=").Append(squad?.SquadId ?? "")
                    .Append(" LegacyArmy=").Append(squad?.LegacyArmyId ?? "")
                    .Append(" Source=").Append(string.IsNullOrEmpty(personal?.PersonalSurfaceId)
                        ? "LegacyUnqualified" : "PersonalWorldPresence")
                    .Append(" Space/SurfaceId=").Append(personal?.PersonalSurfaceId ?? "")
                    .Append(" HasPosition=").Append(personal?.HasContinuousWorldPosition ?? false)
                    .Append(" World=(").Append(personal?.WorldPosX).Append(',').Append(personal?.WorldPosY).Append(')')
                    .Append(" Materialized=").Append(world.ContinuousOutdoorMaterialization.IsMaterialized(entity.Id))
                    .Append(" Override=").Append(location?.HasPresentationOverride ?? false)
                    .Append(" IncludedReason=").Append(participant?.IncludedReason ?? "NotIncluded");
            }

            Debug.Log(sb.ToString());
        }
    }
}
