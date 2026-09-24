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

            var party = world.Strategic?.PlayerPartyContext;
            PlayerPartyLifeStateMembershipService.ReconcilePlayerPartyAfterLifeStateChange(world);
            var members = CollectTransitionMembers(world, party);
            if (members.Count == 0)
            {
                return Result.Failure(
                    ErrorCode.InvalidOperation,
                    "No eligible PlayerParty members to enter Separate Space.");
            }

            if (!PlayerPartyTransitionMembership.ShouldMemberTransitionWithParty(world, party, subject) ||
                !members.Contains(subject))
            {
                return Result.Failure(
                    ErrorCode.InvalidOperation,
                    "Active character is not an eligible Separate Space transition member.",
                    subject.ToString());
            }

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

            MovePlayerPartyIntoSeparateSpace(world, session, entrance.EnterSpawnLocationId, spawn, members);
            ReconcileActiveSeparateSpaceWorldPresence(world);

            world.PartyWorld.LocalMapId = session.ActiveMapLayoutId;
            world.PartyWorld.SiteId = string.Empty;
            world.PartyWorld.Mode = PartyWorldPresenceMode.InSeparateSpace;

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
            PlayerPartyLifeStateMembershipService.ReconcilePlayerPartyAfterLifeStateChange(world);
            EvacuateSeparateSpaceParty(world, session, interiorMap, returnId);

            session.ActiveMapLayoutId = continuousReturn ? string.Empty : session.OverworldMapLayoutId;
            session.ClearOccupants();
            session.ClearSeparateSpaceIdentity();

            if (continuousReturn && world.PlayerPartyTravel != null)
            {
                var position = new WorldVec2(session.ReturnWorldX, session.ReturnWorldY);
                world.PlayerPartyTravel.SetAtSurfacePosition(session.ReturnSurfaceId, position);
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
                            world.WorldPresence.SetAtWorldPosition(id, position, session.ReturnSurfaceId);
                }
                else
                {
                    foreach (var traveler in world.PlayerPartyTravel.TravelingMembers)
                        world.WorldPresence.SetAtWorldPosition(traveler, position, session.ReturnSurfaceId);
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

        /// <summary>
        /// Ends only the active PlayerParty presentation claim after a genuine party wipe.
        /// Unlike <see cref="Leave"/>, this never evacuates occupants and never changes corpse
        /// EntityLocation. Persistent Separate Space state therefore remains available on re-entry.
        /// </summary>
        public static void ReleasePlayerControlAfterPartyWipe(SimulationWorld world)
        {
            var session = world?.LocalMap;
            if (session == null || !session.IsActive)
                return;

            var nextMapLayoutId = session.HasOutdoorReturn
                ? string.Empty
                : session.OverworldMapLayoutId ?? string.Empty;
            session.ActiveMapLayoutId = nextMapLayoutId;
            session.ClearOccupants();
            session.ClearSeparateSpaceIdentity();
            session.OverworldMapLayoutId = string.Empty;
            session.ReturnLocationId = string.Empty;
            session.HasOutdoorReturn = false;
            session.ReturnSurfaceId = string.Empty;
            session.ReturnWorldX = 0f;
            session.ReturnWorldY = 0f;
        }

        /// <summary>
        /// Active Separate Space local placement owns its Characters. Removes stale Outdoor
        /// personal presence from current occupants and from persistent Characters whose authored
        /// EntityLocation belongs to the active map. It never changes placement or membership.
        /// </summary>
        public static void ReconcileActiveSeparateSpaceWorldPresence(SimulationWorld world)
        {
            var session = world?.LocalMap;
            if (world?.WorldPresence == null || session == null || !session.IsActive)
                return;
            foreach (var entity in world.Entities.All)
            {
                if (entity == null)
                    continue;
                if (IsOwnedByActiveSeparateSpace(world, entity.Id))
                    world.WorldPresence.Remove(entity.Id);
            }
        }

        public static bool IsOwnedByActiveSeparateSpace(SimulationWorld world, EntityId id)
        {
            var session = world?.LocalMap;
            if (world == null || session == null || !session.IsActive || id.IsNone)
                return false;
            if (session.ContainsOccupant(id))
                return true;
            return world.Entities.TryGet(id, out var entity) && entity != null &&
                   entity.TryGet<EntityLocationComponent>(out var location) && location != null &&
                   location.HasLocation && IsInteriorLocation(
                       world, location.LocationId, session.ActiveMapLayoutId);
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
                !resolved.HasValue)
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
        /// 只带走当前 PlayerParty + ShouldMemberTransitionWithParty 的合法成员。
        /// </summary>
        static void MovePlayerPartyIntoSeparateSpace(
            SimulationWorld world,
            LocalMapSession session,
            string spawnLocationId,
            WorldLocationState spawn,
            List<EntityId> members)
        {
            session.ClearOccupants();
            var party = world.Strategic?.PlayerPartyContext;
            var spawnX = spawn?.PresentationX ?? 0f;
            var spawnZ = spawn?.PresentationZ ?? 0f;
            var targetMap = spawn?.LocalMapId ?? session.ActiveMapLayoutId;
            var formationSlot = 0;

            if (members == null)
                return;

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
        }

        /// <summary>
        /// 唯一正式来源：当前 PlayerParty membership + ShouldMemberTransitionWithParty。
        /// 空列表 = 明确无成员；禁止 fallback 抓取全部 Player Character。
        /// </summary>
        static List<EntityId> CollectTransitionMembers(SimulationWorld world, PlayerPartyRuntime party)
        {
            var list = new List<EntityId>(8);
            if (party == null)
                return list;

            foreach (var id in party.Members)
            {
                if (!PlayerPartyTransitionMembership.ShouldMemberTransitionWithParty(world, party, id))
                    continue;
                if (!list.Contains(id))
                    list.Add(id);
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

        /// <summary>
        /// 只撤离当前合法 transition members。stranded／downed／corpse／detached 保留 Interior ownership。
        /// </summary>
        static void EvacuateSeparateSpaceParty(
            SimulationWorld world,
            LocalMapSession session,
            string interiorMapLayoutId,
            string returnLocationId)
        {
            if (world == null || string.IsNullOrEmpty(returnLocationId))
                return;

            var party = world.Strategic?.PlayerPartyContext;
            if (party == null)
                return;

            foreach (var id in party.Members)
            {
                if (!PlayerPartyTransitionMembership.ShouldMemberTransitionWithParty(world, party, id))
                    continue;
                if (id.IsNone || !world.Entities.TryGet(id, out var member) || member == null)
                    continue;
                if (!member.TryGet<EntityLocationComponent>(out var lc) || lc == null)
                    continue;

                var inInterior = !string.IsNullOrEmpty(interiorMapLayoutId) &&
                                 lc.HasLocation &&
                                 IsInteriorLocation(world, lc.LocationId, interiorMapLayoutId);
                var isOccupant = session != null && session.ContainsOccupant(id);
                if (!inInterior && !isOccupant)
                    continue;

                SetEntityLocation(world, member, returnLocationId);
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
