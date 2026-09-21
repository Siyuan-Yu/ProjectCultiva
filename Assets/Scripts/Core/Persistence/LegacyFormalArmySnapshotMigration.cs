using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Persistence
{
    /// <summary>
    /// One-way compatibility boundary for pre-Squad snapshots. It consumes legacy DTOs and
    /// publishes only Squad/SquadWorldMotion runtime state.
    /// </summary>
    public static class LegacyFormalArmySnapshotMigration
    {
        public static Result RestoreSquads(SimulationWorld world, StrategicSnapshotDto snapshot)
        {
            if (world?.Strategic == null || snapshot == null)
                return Result.Failure(ErrorCode.InvalidArgument, "Legacy army migration requires world and snapshot.");
            if (snapshot.FormalArmies == null) return Result.Success();

            for (var i = 0; i < snapshot.FormalArmies.Count; i++)
            {
                var legacy = snapshot.FormalArmies[i];
                if (legacy == null || string.IsNullOrWhiteSpace(legacy.ArmyId))
                    return Invalid(legacy, "missing ArmyId");
                if (TryResolveSquad(world, snapshot, legacy.ArmyId, EntityId.None, out var existing))
                {
                    existing.LegacyArmyId = string.Empty;
                    continue;
                }

                var members = CollectMembers(world, snapshot, legacy);
                if (members.Count == 0)
                    return Invalid(legacy, "roster contains no restored Character");
                var leader = new EntityId(legacy.LeaderCharacterId);
                if (leader.IsNone || !members.Contains(leader)) leader = members[0];
                var squadId = SquadMembershipService.ArmySquadId(legacy.ArmyId);
                var created = SquadMembershipService.Create(world, squadId, members, leader,
                    string.Empty, SquadCommandKind.SquadWorldMotion, importingSnapshot: true,
                    displayName: string.Empty, factionId: legacy.FactionId ?? string.Empty);
                if (created.IsFailure)
                    return Result.Failure(ErrorCode.SnapshotInvalid,
                        "Legacy FormalArmy roster cannot migrate directly to Squad.",
                        legacy.ArmyId + ":" + created.Error);
            }
            return Result.Success();
        }

        public static Result RestoreMotions(SimulationWorld world, StrategicSnapshotDto snapshot)
        {
            if (world?.Strategic == null || snapshot == null)
                return Result.Failure(ErrorCode.InvalidArgument, "Legacy army motion migration requires world and snapshot.");
            if (snapshot.FormalArmies == null) return Result.Success();

            for (var i = 0; i < snapshot.FormalArmies.Count; i++)
            {
                var legacy = snapshot.FormalArmies[i];
                if (legacy == null || !TryResolveSquad(world, snapshot, legacy.ArmyId,
                        new EntityId(legacy.LeaderCharacterId), out var squad))
                    return Invalid(legacy, "Squad mapping is missing");

                squad.LegacyArmyId = string.Empty;
                if (string.Equals(squad.SquadId, snapshot.ControlledSquadId, StringComparison.Ordinal) ||
                    SquadWorldMotionService.IsPlayerPartySquad(world, squad))
                {
                    world.Strategic.SquadWorldMotions.Remove(squad.SquadId);
                    SquadCommandService.SetExecution(world, squad.SquadId, SquadCommandKind.FollowLeader,
                        new EntityId(snapshot.PlayerParty?.ActiveCharacterId ?? squad.LeaderCharacterId.Value));
                    continue;
                }

                if (legacy.LocationKind == 1 && !string.IsNullOrWhiteSpace(legacy.SiteId) &&
                    !IsLegacyMoving(legacy))
                {
                    var atSite = SquadWorldMotionService.InitializeAtSite(world, squad.SquadId, legacy.SiteId);
                    if (atSite.IsFailure)
                        return Invalid(legacy, "canonical SiteArrival is unavailable: " + atSite.Error);
                    continue;
                }

                var position = new WorldVec2(legacy.WorldX, legacy.WorldY);
                var surfaceId = legacy.SurfaceId ?? string.Empty;
                if (legacy.LocationKind == 1 && !string.IsNullOrWhiteSpace(legacy.SiteId) &&
                    world.SurfaceGround.TryResolveSiteArrival(legacy.SiteId, out var arrivalSurface, out var arrival))
                {
                    surfaceId = arrivalSurface;
                    position = arrival;
                }
                else if (legacy.LocationKind == 0 ||
                         !world.SurfaceGround.TryResolveContaining(position, out var containing))
                {
                    var scale = world.HexWorld != null && world.HexWorld.HexSize > 0f
                        ? world.HexWorld.HexSize : 1f;
                    HexMath.ToWorldPosition(new HexCoord(legacy.CurrentHexQ, legacy.CurrentHexR),
                        scale, out var x, out var y);
                    position = new WorldVec2(x, y);
                    if (world.SurfaceGround.TryResolveContaining(position, out containing))
                        surfaceId = containing.SurfaceId;
                }
                else if (string.IsNullOrWhiteSpace(surfaceId)) surfaceId = containing.SurfaceId;

                var initialized = SquadWorldMotionService.Initialize(world, squad.SquadId,
                    surfaceId, position, legacy.SiteId);
                if (initialized.IsFailure) return Invalid(legacy, initialized.Error.ToString());

                if (legacy.SurfacePath != null && legacy.SurfacePath.Count > 0 && IsLegacyMoving(legacy))
                {
                    var route = new List<WorldVec2>(legacy.SurfacePath.Count);
                    for (var p = 0; p < legacy.SurfacePath.Count; p++)
                    {
                        var point = legacy.SurfacePath[p];
                        if (point == null || !Finite(point.X) || !Finite(point.Y))
                            return Invalid(legacy, "surface route contains a non-finite point");
                        route.Add(new WorldVec2(point.X, point.Y));
                    }
                    var destination = new WorldVec2(legacy.PhysicalDestinationX, legacy.PhysicalDestinationY);
                    initialized.Value.Restore(surfaceId, string.Empty, position, true, destination, route,
                        legacy.SurfaceWaypointIndex, legacy.SegmentProgress,
                        legacy.SurfaceSourceRevision, legacy.SurfaceSourceHash);
                }
                else if (IsLegacyMoving(legacy))
                {
                    WorldVec2 destination;
                    if (!string.IsNullOrWhiteSpace(legacy.DestinationSiteId) &&
                        world.SurfaceGround.TryResolveSiteArrival(legacy.DestinationSiteId, out _, out var siteArrival))
                        destination = siteArrival;
                    else
                    {
                        var scale = world.HexWorld != null && world.HexWorld.HexSize > 0f
                            ? world.HexWorld.HexSize : 1f;
                        HexMath.ToWorldPosition(new HexCoord(legacy.DestinationHexQ, legacy.DestinationHexR),
                            scale, out var x, out var y);
                        destination = new WorldVec2(x, y);
                    }
                    var routed = SquadWorldMotionService.MoveToWorldPosition(world, squad.SquadId, destination);
                    if (routed.IsFailure) return Invalid(legacy, "route cannot migrate: " + routed.Error);
                }
            }
            return Result.Success();
        }

        public static bool TryResolveSquad(SimulationWorld world, StrategicSnapshotDto snapshot,
            string legacyArmyId, EntityId characterHint, out SquadState squad)
        {
            squad = null;
            if (world?.Strategic?.Squads == null) return false;
            var generatedId = SquadMembershipService.ArmySquadId(legacyArmyId);
            if (!string.IsNullOrEmpty(legacyArmyId) && world.Strategic.Squads.TryGet(generatedId, out squad))
                return true;
            foreach (var candidate in world.Strategic.Squads.Squads.Values)
                if (!string.IsNullOrEmpty(legacyArmyId) &&
                    string.Equals(candidate.LegacyArmyId, legacyArmyId, StringComparison.Ordinal))
                { squad = candidate; return true; }
            if (!characterHint.IsNone && world.Strategic.Squads.TryGetForCharacter(characterHint, out squad))
                return true;
            if (snapshot?.ArmyMemberships != null && !string.IsNullOrEmpty(legacyArmyId))
                for (var i = 0; i < snapshot.ArmyMemberships.Count; i++)
                {
                    var membership = snapshot.ArmyMemberships[i];
                    if (membership != null && string.Equals(membership.ArmyId, legacyArmyId, StringComparison.Ordinal) &&
                        world.Strategic.Squads.TryGetForCharacter(new EntityId(membership.CharacterId), out squad))
                        return true;
                }
            return false;
        }

        static List<EntityId> CollectMembers(SimulationWorld world, StrategicSnapshotDto snapshot,
            FormalArmySnapshotDto legacy)
        {
            var result = new List<EntityId>();
            if (legacy.MemberCharacterIds != null)
                for (var i = 0; i < legacy.MemberCharacterIds.Count; i++)
                    AddCharacter(world, result, legacy.MemberCharacterIds[i]);
            if (snapshot.ArmyMemberships != null)
                for (var i = 0; i < snapshot.ArmyMemberships.Count; i++)
                {
                    var membership = snapshot.ArmyMemberships[i];
                    if (membership != null && string.Equals(membership.ArmyId, legacy.ArmyId, StringComparison.Ordinal))
                        AddCharacter(world, result, membership.CharacterId);
                }
            return result;
        }

        static void AddCharacter(SimulationWorld world, List<EntityId> result, ulong raw)
        {
            var id = new EntityId(raw);
            if (id.IsNone || result.Contains(id) || !world.Entities.TryGet(id, out var entity) ||
                (entity.Tags & (Entities.EntityTag.Character | Entities.EntityTag.Npc)) == 0) return;
            result.Add(id);
        }

        static bool IsLegacyMoving(FormalArmySnapshotDto legacy) =>
            legacy != null && ((legacy.SurfacePath?.Count ?? 0) > 0 ||
                               (legacy.HexPath?.Count ?? 0) > 1 || legacy.State == 1);

        static Result Invalid(FormalArmySnapshotDto legacy, string reason) =>
            Result.Failure(ErrorCode.SnapshotInvalid, "Invalid legacy FormalArmy snapshot.",
                "ArmyId='" + (legacy?.ArmyId ?? string.Empty) + "' Reason=" + reason);

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
