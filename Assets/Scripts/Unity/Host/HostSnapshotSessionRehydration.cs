using UnityEngine;
using XianXia.Core.Persistence;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;
using XianXia.Data.Bootstrap;
using XianXia.Data.Content;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Snapshot Restore 后补全 Content Shell（HexWorld／WorldRegion／PartyWorld），不 respawn Character。
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
                    "Static content shell rehydrate failed.", contentShell.Error.ToString());
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

            {
                var hex = HexStrategicMapContentBootstrap.TryApplyToSession(world, registry, scenario);
                if (hex.IsFailure)
                    return Result.Failure(ErrorCode.ContentLoadFailed,
                        "HexWorld snapshot shell rehydrate failed.", hex.Error.ToString());

                var political = StrategicSnapshotHelper.RestoreHexPoliticalState(world, politicalSnapshot);
                if (political.IsFailure)
                    return political;
                // Political overlay 后才让 ControlCore／权限读取最终 Owner。
                CaptureObjectiveService.RebindControlCoreSites(world);

                var motions = StrategicSnapshotHelper.RestoreFormalArmyMotions(world, politicalSnapshot);
                if (motions.IsFailure)
                    return motions;
            }

            ResolvePartyWorldFromActiveControlledCharacter(world, session.PlayerParty);

            var mapId = world.PartyWorld?.LocalMapId?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(mapId))
            {
                session.PreferredMapLayoutId = mapId;
                bootstrap.ConfigurePreferredMapLayout(mapId);
                var places = WorldRegionBootstrap.ActivatePlacesForMapLayout(world, registry, mapId);
                if (places.IsFailure)
                    return Result.Failure(ErrorCode.ContentLoadFailed,
                        "WorldRegion snapshot shell rehydrate failed.", places.Error.ToString());
                RestoreLegacyAuthoredEntityLocations(world, session.PlayerParty, scenario);
            }

            // 全部 Content shell 与 motion overlay 均成功后，才同步成员／presentation／pursuit，
            // 最后才允许进入 LocalMap materialization。
            StrategicSnapshotHelper.FinalizeRuntimeLinks(world);
            if (!string.IsNullOrEmpty(mapId))
                WorldTravelService.ApplyLocalMapSessionFromFocus(world);
            session.ConsumePendingRestoredStrategicSnapshot();
            session.RefreshViewableEntityIds();
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
            world.PartyWorld.Mode = PartyWorldPresenceMode.AtHex;
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

            Debug.Log(sb.ToString());
        }
    }
}
