using System;
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
        public static Result RehydrateAfterRestore(PlayableHostBootstrap bootstrap)
        {
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
            var mastery = new XianXia.Core.Cultivation.SkillMasteryService()
                .NormalizeLoadedMasteryState(world);
            if (mastery.IsFailure)
                return Result.Failure(
                    ErrorCode.SnapshotInvalid,
                    "Skill mastery restore normalization failed: " + mastery.Error.Message,
                    mastery.Error.ToString());

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

            // Content-dependent spatial restore is deliberately phase two: Surface navigation
            // first, authored Site identity second, then PlayerParty/background route authority.
            var surfaceSites = StrategicContentBootstrap.ApplySurfaceSites(world, registry, scenario);
            if (surfaceSites.IsFailure)
                return Result.Failure(ErrorCode.ContentLoadFailed,
                    "Surface Site snapshot shell rehydrate failed.", surfaceSites.Error.ToString());

            var partyTravel = StrategicSnapshotHelper.FinalizePlayerPartyTravelAfterContentShell(
                world, politicalSnapshot.PlayerPartyTravel);
            if (partyTravel.IsFailure)
                return partyTravel;

            var backgroundTravel = StrategicSnapshotHelper.RestoreBackgroundSurfaceTravels(
                world, politicalSnapshot.BackgroundCharacterTravels);
            if (backgroundTravel.IsFailure)
                return backgroundTravel;

            PlayerPartyTransitionMembership.CaptureTravelingMembersForPartyTransition(
                world, session.PlayerParty);
            var separateSpaceOwnsPresence = world.LocalMap != null && world.LocalMap.IsActive;
            var encounterOwnsPresence = world.Strategic.CharacterEncounter != null;
            if (!separateSpaceOwnsPresence && !encounterOwnsPresence)
                PlayerPartyTransitionMembership.ReconcilePlayerPartyMemberWorldPresenceFromMotion(
                    world, session.PlayerParty);

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
                var fixedCores = ContentRuntimeBootstrap.RebindPresetWorldSiteCoreMetadata(world, registry);
                if (fixedCores.IsFailure)
                    return Result.Failure(ErrorCode.ContentLoadFailed,
                        "Preset SiteCore snapshot shell rehydrate failed.", fixedCores.Error.ToString());

                var political = StrategicSnapshotHelper.RestorePoliticalState(world, politicalSnapshot);
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

                var motions = StrategicSnapshotHelper.RestoreSquadWorldMotions(world, politicalSnapshot);
                if (motions.IsFailure)
                    return motions;
            }

            ResolvePartyWorldFromActiveControlledCharacter(world, session.PlayerParty);

            // SPACE-01：current Separate Space authority 优先于 Outdoor ActiveControlled 解析。
            if (world.LocalMap.IsActive)
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
                if (world.LocalMap.IsActive)
                    LoadedLocalMapPlacementSnapshotRestore.ApplySavedPlacementsToDomain(world, mapId);
                if (world.LocalMap.IsActive)
                    SeparateSpaceTransitionService.ReconcileActiveSeparateSpaceWorldPresence(world);
            }

            // 全部 Content shell 与 motion overlay 均成功后，才同步成员／presentation／pursuit，
            // 最后才允许进入 LocalMap materialization。
            StrategicSnapshotHelper.FinalizeRuntimeLinks(world);
            CharacterEncounterService.BindRuntime(world);
            var presenceInvariant = StrategicSnapshotHelper.ValidateRestoredCharacterWorldPresences(
                world, politicalSnapshot);
            if (presenceInvariant.IsFailure)
                return presenceInvariant;
            var partySurfaceInvariant =
                StrategicSnapshotHelper.ValidatePlayerPartyContinuousAuthorityAfterContentShell(world);
            if (partySurfaceInvariant.IsFailure)
                return partySurfaceInvariant;
            session.ConsumePendingRestoredStrategicSnapshot();
            session.RefreshViewableEntityIds();
            return Result.Success();
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
            world.PartyWorld.ClearSiteFocus();
            world.PartyWorld.LocalMapId = string.Empty;
            world.PartyWorld.Mode = PartyWorldPresenceMode.AtWorldPosition;
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
