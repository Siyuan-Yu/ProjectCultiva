using System.Collections.Generic;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Events;
using XianXia.Core.Opportunity;
using XianXia.Core.Results;
using XianXia.Core.Settlement;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Exploration
{
    /// <summary>
    /// Continuous Outdoor ↔ Separate Space 的 Domain transition 真源（SPACE-01）。
    /// Host 只负责 presentation rebuild／input／registry place activation。
    /// </summary>
    public static class SeparateSpaceTransitionService
    {
        const float FormationSpacing = 1.1f;

        /// <summary>
        /// Enter：Continuous Outdoor → Separate Space。
        /// Proximity 由 Host 门禁；EntityLocation.LocationId 不得冒充 transition authority。
        /// </summary>
        public static Result Enter(
            SimulationWorld world,
            EntityId subject,
            string entranceLocationId,
            string explicitSpaceKind = null,
            string entryReason = "enter")
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld is null.");
            if (!world.Entities.TryGet(subject, out var entity))
                return Result.Failure(ErrorCode.EntityNotFound, "Subject missing.", subject.ToString());

            var entranceId = string.IsNullOrWhiteSpace(entranceLocationId)
                ? null
                : entranceLocationId.Trim();
            if (string.IsNullOrEmpty(entranceId))
            {
                if (!entity.TryGet<EntityLocationComponent>(out var loc) || !loc.HasLocation)
                    return Result.Failure(ErrorCode.InvalidOperation, "Entrance location required.");
                entranceId = loc.LocationId;
            }

            if (!WorldLocationQuery.TryGet(world, entranceId, out var entrance))
                return Result.Failure(ErrorCode.NotFound, "Entrance location missing.", entranceId);
            if (string.IsNullOrEmpty(entrance.EnterLocalMapId) ||
                string.IsNullOrEmpty(entrance.EnterSpawnLocationId))
                return Result.Failure(ErrorCode.InvalidOperation, "Location is not a Separate Space entrance.",
                    entranceId);

            // 已在其它 Separate Space：拒绝。同一空间再进（救人）允许。
            if (world.LocalMap != null && world.LocalMap.IsActive &&
                !string.Equals(
                    world.LocalMap.ActiveMapLayoutId,
                    entrance.EnterLocalMapId,
                    System.StringComparison.Ordinal))
                return Result.Failure(ErrorCode.InvalidOperation, "Already inside a different Separate Space.");

            var fromContinuousOutdoor =
                !world.LocalMap.IsActive &&
                world.ContinuousOutdoorMaterialization.TryGetAnyPlace(entranceId, out _);

            if (!ContentConditionEvaluator.AllPass(world, subject, entrance.EnterConditions))
                return Result.Failure(ErrorCode.InvalidOperation, "Entrance conditions not met.", entranceId);

            if (!string.IsNullOrEmpty(entrance.OpportunitySiteId))
            {
                if (!entity.TryGet<KnownSitesComponent>(out var known) ||
                    !DefinitionId.TryParse(entrance.OpportunitySiteId, out var siteId) ||
                    !known.Knows(siteId))
                {
                    return Result.Failure(
                        ErrorCode.InvalidOperation,
                        "Discover the cave first (explore).",
                        entrance.OpportunitySiteId);
                }
            }

            if (!world.LocalPlaces.TryGet(entrance.EnterSpawnLocationId, out var spawn) ||
                !string.Equals(spawn.LocalMapId, entrance.EnterLocalMapId, System.StringComparison.Ordinal))
                return Result.Failure(ErrorCode.NotFound, "Interior spawn location is not active.",
                    entrance.EnterSpawnLocationId);

            var session = world.LocalMap;
            var kind = SeparateSpaceResolver.InferFromEntrance(entrance, explicitSpaceKind);

            // Capture Outdoor exact return BEFORE mutating session.
            if (fromContinuousOutdoor)
            {
                var capture = TryCaptureOutdoorReturn(world, out var surfaceId, out var rx, out var ry);
                if (capture.IsFailure)
                    return capture;
                session.HasOutdoorReturn = true;
                session.ReturnSurfaceId = surfaceId;
                session.ReturnWorldX = rx;
                session.ReturnWorldY = ry;
            }

            if (string.IsNullOrEmpty(session.OverworldMapLayoutId))
                session.OverworldMapLayoutId = session.ActiveMapLayoutId;

            session.EstablishSeparateSpace(
                entrance.EnterLocalMapId,
                world.LocalPlaces.RegionId ?? string.Empty,
                kind,
                entranceId,
                entranceId,
                entryReason ?? "enter");

            MovePlayerPartyIntoSeparateSpace(world, session, entrance.EnterSpawnLocationId, spawn);

            world.PartyWorld.LocalMapId = session.ActiveMapLayoutId;
            world.PartyWorld.SiteId = string.Empty;
            world.PartyWorld.Mode = PartyWorldPresenceMode.AtHex;

            world.Events.Publish(
                EventType.LocalMapChanged,
                world.Tick,
                target: subject,
                payload: session.ActiveMapLayoutId + ";" + entrance.EnterSpawnLocationId);

            return Result.Success();
        }

        public static Result Leave(SimulationWorld world, EntityId subject)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld is null.");
            if (!world.Entities.TryGet(subject, out _))
                return Result.Failure(ErrorCode.EntityNotFound, "Subject missing.", subject.ToString());

            var session = world.LocalMap;
            if (session == null || !session.IsActive)
                return Result.Failure(ErrorCode.InvalidOperation, "Not inside a Separate Space.");
            if (string.IsNullOrEmpty(session.OverworldMapLayoutId) && !session.HasOutdoorReturn)
                return Result.Failure(ErrorCode.InvalidOperation, "Outdoor return authority missing.");
            if (string.IsNullOrEmpty(session.ReturnLocationId) ||
                (!session.HasOutdoorReturn &&
                 !world.LocalPlaces.TryGet(session.ReturnLocationId, out _)))
                return Result.Failure(ErrorCode.NotFound, "Return location missing.", session.ReturnLocationId);
            if (session.HasOutdoorReturn &&
                (string.IsNullOrEmpty(session.ReturnSurfaceId) ||
                 !world.SurfaceGround.TryGet(session.ReturnSurfaceId, out var returnNavigation) ||
                 !returnNavigation.Contains(session.ReturnWorldX, session.ReturnWorldY)))
                return Result.Failure(ErrorCode.InvalidOperation,
                    "Continuous return Surface is unavailable.", session.ReturnSurfaceId);

            var returnId = session.ReturnLocationId;
            var continuousReturn = session.HasOutdoorReturn;
            var interiorMap = session.ActiveMapLayoutId;
            EvacuateSeparateSpaceParty(world, session, interiorMap, returnId);

            session.ActiveMapLayoutId = continuousReturn ? string.Empty : session.OverworldMapLayoutId;
            session.ClearOccupants();
            session.ClearSeparateSpaceIdentity();

            if (continuousReturn && world.PlayerPartyTravel != null)
            {
                var position = new WorldVec2(session.ReturnWorldX, session.ReturnWorldY);
                world.PlayerPartyTravel.SetAtSurfacePosition(position);
                world.PlayerPartyTravel.SetCurrentOutdoorWorldSiteContext(
                    WorldSiteAdministrativeControlResolver.TryResolveOnRegisteredSurface(
                        world, position.X, position.Y, out _, out var returnSite, out _)
                        ? returnSite.SiteId
                        : string.Empty);
                var party = world.Strategic.PlayerPartyContext;
                if (party != null)
                {
                    foreach (var id in party.Members)
                        if (PlayerPartyTransitionMembership.ShouldMemberTransitionWithParty(world, party, id))
                            world.WorldPresence.SetAtWorldPosition(id, position, default, session.ReturnSurfaceId);
                }
                else
                {
                    foreach (var traveler in world.PlayerPartyTravel.TravelingMembers)
                        world.WorldPresence.SetAtWorldPosition(traveler, position, default, session.ReturnSurfaceId);
                }

                world.PartyWorld.LocalMapId = string.Empty;
                world.PartyWorld.SiteId = string.Empty;
                world.PartyWorld.Mode = PartyWorldPresenceMode.AtWorldPosition;
                session.OverworldMapLayoutId = string.Empty;
                session.HasOutdoorReturn = false;
                session.ReturnSurfaceId = string.Empty;
            }

            world.Events.Publish(
                EventType.LocalMapChanged,
                world.Tick,
                target: subject,
                payload: session.ActiveMapLayoutId + ";" + returnId);

            return continuousReturn ? Result.Success() : Result.Success();
        }

        public static Result TryCaptureOutdoorReturn(
            SimulationWorld world,
            out string surfaceId,
            out float worldX,
            out float worldY)
        {
            surfaceId = string.Empty;
            worldX = 0f;
            worldY = 0f;
            var party = world?.Strategic?.PlayerPartyContext;
            var returnSurface = world?.SurfaceGround?.Active;
            if (!PlayerPartyWorldLocationQuery.TryResolve(world, party, out var resolved) ||
                !resolved.HasValue ||
                resolved.IsLegacyFallback)
                return Result.Failure(ErrorCode.InvalidOperation,
                    "Continuous entrance has no exact Surface return position.");
            if (returnSurface == null ||
                !returnSurface.Contains(resolved.WorldPosition.X, resolved.WorldPosition.Y))
                world.SurfaceGround.TryResolveContaining(resolved.WorldPosition, out returnSurface);
            if (returnSurface == null)
                return Result.Failure(ErrorCode.InvalidOperation,
                    "Continuous entrance has no registered return Surface.");
            surfaceId = returnSurface.SurfaceId;
            worldX = resolved.WorldPosition.X;
            worldY = resolved.WorldPosition.Y;
            return Result.Success();
        }

        /// <summary>
        /// 只带走 PlayerParty 真实随行成员（Active + living companions）；不用 FormalArmy／旁观 NPC。
        /// </summary>
        static void MovePlayerPartyIntoSeparateSpace(
            SimulationWorld world,
            LocalMapSession session,
            string spawnLocationId,
            WorldLocationState spawn)
        {
            session.ClearOccupants();
            var party = world.Strategic?.PlayerPartyContext;
            var members = CollectTransitionMembers(world, party);
            var spawnX = spawn?.PresentationX ?? 0f;
            var spawnZ = spawn?.PresentationZ ?? 0f;
            var targetMap = spawn?.LocalMapId ?? session.ActiveMapLayoutId;
            var formationSlot = 0;

            // 已在目标 Separate Space 内的成员（救人／再进）保留站位。
            for (var i = 0; i < members.Count; i++)
            {
                var id = members[i];
                if (!world.Entities.TryGet(id, out var e) || e == null)
                    continue;
                if (e.TryGet<EntityLocationComponent>(out var existing) &&
                    existing.HasLocation &&
                    IsInteriorLocation(world, existing.LocationId, targetMap))
                {
                    session.AddOccupant(id);
                    continue;
                }

                SetEntityLocation(world, e, spawnLocationId);
                var isActive = party != null && party.IsActive(id);
                ApplyDeterministicFormation(e, spawnX, spawnZ, formationSlot, isActive || formationSlot == 0);
                formationSlot++;
                session.AddOccupant(id);
            }

            // 登记仍挂在内室地点、但未进 party 列表的己方（兼容旧再进路径）。
            foreach (var e in world.Entities.All)
            {
                if (!IsPlayerPartyCharacter(e) || session.ContainsOccupant(e.Id))
                    continue;
                if (!e.TryGet<EntityLocationComponent>(out var lc) || !lc.HasLocation)
                    continue;
                if (!IsInteriorLocation(world, lc.LocationId, targetMap))
                    continue;
                session.AddOccupant(e.Id);
            }
        }

        static List<EntityId> CollectTransitionMembers(SimulationWorld world, PlayerPartyRuntime party)
        {
            var list = new List<EntityId>(8);
            if (party != null)
            {
                foreach (var id in party.Members)
                {
                    if (!PlayerPartyTransitionMembership.ShouldMemberTransitionWithParty(world, party, id))
                        continue;
                    if (!list.Contains(id))
                        list.Add(id);
                }

                if (list.Count > 0)
                    return list;
            }

            foreach (var e in world.Entities.All)
            {
                if (!IsPlayerPartyCharacter(e))
                    continue;
                list.Add(e.Id);
            }

            return list;
        }

        static void ApplyDeterministicFormation(
            Entity entity,
            float spawnX,
            float spawnZ,
            int index,
            bool isActive)
        {
            if (entity == null)
                return;
            if (!entity.TryGet<EntityLocationComponent>(out var lc))
            {
                lc = new EntityLocationComponent();
                entity.AddComponent(lc);
            }

            if (isActive || index == 0)
            {
                lc.SetPresentationOverride(spawnX, spawnZ);
                return;
            }

            // Small diamond / ring around spawn; deterministic by companion slot.
            var slot = index;
            float dx, dz;
            switch ((slot - 1) % 4)
            {
                case 0:
                    dx = FormationSpacing;
                    dz = 0f;
                    break;
                case 1:
                    dx = -FormationSpacing;
                    dz = 0f;
                    break;
                case 2:
                    dx = 0f;
                    dz = FormationSpacing;
                    break;
                default:
                    dx = 0f;
                    dz = -FormationSpacing;
                    break;
            }

            var ring = 1 + ((slot - 1) / 4);
            lc.SetPresentationOverride(spawnX + dx * ring, spawnZ + dz * ring);
        }

        static void EvacuateSeparateSpaceParty(
            SimulationWorld world,
            LocalMapSession session,
            string interiorMapLayoutId,
            string returnLocationId)
        {
            if (world == null || string.IsNullOrEmpty(returnLocationId))
                return;

            if (session != null)
            {
                for (var i = 0; i < session.OccupantIds.Count; i++)
                {
                    var id = session.OccupantIds[i];
                    if (id.IsNone || !world.Entities.TryGet(id, out var occupant) ||
                        !IsPlayerPartyCharacter(occupant))
                        continue;
                    if (!occupant.TryGet<EntityLocationComponent>(out _))
                        continue;
                    SetEntityLocation(world, occupant, returnLocationId);
                }
            }

            if (string.IsNullOrEmpty(interiorMapLayoutId))
                return;
            foreach (var e in world.Entities.All)
            {
                if (!IsPlayerPartyCharacter(e))
                    continue;
                if (!e.TryGet<EntityLocationComponent>(out var lc) || !lc.HasLocation)
                    continue;
                if (!IsInteriorLocation(world, lc.LocationId, interiorMapLayoutId))
                    continue;
                SetEntityLocation(world, e, returnLocationId);
            }
        }

        static bool IsInteriorLocation(SimulationWorld world, string locationId, string interiorMapLayoutId)
        {
            if (world == null || string.IsNullOrEmpty(locationId) || string.IsNullOrEmpty(interiorMapLayoutId))
                return false;
            return world.LocalPlaces.TryGet(locationId, out var place) &&
                   !string.IsNullOrEmpty(place.LocalMapId) &&
                   string.Equals(place.LocalMapId, interiorMapLayoutId, System.StringComparison.Ordinal);
        }

        static bool IsPlayerPartyCharacter(Entity e) =>
            e != null &&
            (e.Tags & EntityTag.Character) != 0 &&
            (e.Tags & EntityTag.Npc) == 0;

        static void SetEntityLocation(SimulationWorld world, Entity e, string locationId)
        {
            if (world == null || e == null || string.IsNullOrEmpty(locationId))
                return;
            if (!e.TryGet<EntityLocationComponent>(out var lc))
            {
                lc = new EntityLocationComponent();
                e.AddComponent(lc);
            }

            lc.LocationId = locationId;
            lc.HasPresentationOverride = false;
            lc.PresentationOverrideX = 0f;
            lc.PresentationOverrideZ = 0f;
            world.Events.Publish(
                EventType.LocationChanged,
                world.Tick,
                target: e.Id,
                payload: locationId);
        }
    }
}
