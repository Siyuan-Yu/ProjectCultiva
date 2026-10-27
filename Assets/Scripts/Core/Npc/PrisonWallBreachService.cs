using System;
using System.Globalization;
using XianXia.Core.Combat;
using XianXia.Core.Construction;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Npc
{
    /// <summary>Political wall damage qualification and in-place release; Host supplies real navigation.</summary>
    public static class PrisonWallBreachService
    {
        public static bool TryResolveWall(SimulationWorld w, string wallId,
            out OutdoorConstructedAssetState room, out int gx, out int gy)
        {
            room = null; gx = gy = -1;
            if (w == null || string.IsNullOrEmpty(wallId)) return false;
            foreach (var candidate in w.OutdoorConstructedAssets.Assets.Values)
            {
                if (!RoomBedGeometry.IsRoom(candidate) || candidate.RoomUsage != RoomUsage.Prison) continue;
                var prefix = candidate.StableAssetId + ":wall:";
                if (!wallId.StartsWith(prefix, StringComparison.Ordinal)) continue;
                var parts = wallId.Substring(prefix.Length).Split(':');
                if (parts.Length != 2 || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out gx) ||
                    !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out gy) ||
                    !RoomBedGeometry.IsRoomWallCell(candidate, gx, gy) ||
                    RoomBedGeometry.RoomWallCellId(candidate, gx, gy) != wallId) return false;
                room = candidate; return true;
            }
            return false;
        }

        public static bool TryOwner(SimulationWorld w, OutdoorConstructedAssetState room, out WorldSite site)
        {
            site = null;
            return w != null && room != null && WorldSiteAdministrativeControlResolver.TryResolve(w, room.SurfaceId,
                room.WorldX + room.WorldWidth * .5f, room.WorldY + room.WorldHeight * .5f, out site, out _) &&
                site != null && !string.IsNullOrEmpty(site.OwnerFactionId);
        }

        public static bool IsWallInField(SimulationWorld w, CharacterEncounterState field,
            OutdoorConstructedAssetState room, int gx, int gy)
        {
            if (field == null || room.SurfaceId != field.SourceSurfaceId ||
                !w.SurfaceSpatial.TryGet(room.SurfaceId, out var metric)) return false;
            for (var side = 0; side < 2; side++)
            {
                if (!RoomBedGeometry.TryRoomWallRectangle(room, gx, gy, metric.CellSize, side == 1,
                        out var x, out var y, out var width, out var height)) continue;
                if (!field.Contains(x, y) || !field.Contains(x + width, y + height)) return false;
            }
            return true;
        }

        public static bool CanDamage(SimulationWorld w, EntityId actor, string wallId)
        {
            if (!TryResolveWall(w, wallId, out var room, out var gx, out var gy)) return true; // Other destructibles unchanged.
            var field = w.Strategic.CharacterEncounter;
            var participant = field?.Find(actor.Value);
            return field != null && (field.Phase == CharacterEncounterPhase.Active || field.Phase == CharacterEncounterPhase.ReadyToEnd) &&
                participant != null && !participant.Enemy && !participant.Withdrawn &&
                w.Entities.TryGet(actor, out var entity) && CombatLifeStateService.CanFight(entity) &&
                entity.TryGet<FactionMembershipComponent>(out var faction) && faction.IsAffiliated &&
                TryOwner(w, room, out var site) && WarGateService.CanAttack(w, faction.FactionId, site.OwnerFactionId) &&
                IsWallInField(w, field, room, gx, gy);
        }

        /// <summary>The segment must cross a destroyed wall, not detour through the original doorway.</summary>
        public static bool TryFindPassableBreach(SimulationWorld w, OutdoorConstructedAssetState room,
            Func<WorldVec2, WorldVec2, bool> canCross, out WorldVec2 outside)
        {
            outside = default;
            if (!w.SurfaceSpatial.TryGet(room.SurfaceId, out var metric)) return false;
            var c = metric.CellSize;
            for (var gy = 0; gy < room.CellsH; gy++) for (var gx = 0; gx < room.CellsW; gx++)
            {
                if (!RoomBedGeometry.IsRoomWallCell(room, gx, gy) ||
                    !w.OutdoorStatefulObjects.IsDestructibleDestroyed(RoomBedGeometry.RoomWallCellId(room, gx, gy)) ||
                    (w.Strategic.CharacterEncounter != null &&
                    !IsWallInField(w, w.Strategic.CharacterEncounter, room, gx, gy))) continue;
                for (var side = 0; side < 2; side++)
                {
                    if (!RoomBedGeometry.TryRoomWallRectangle(room, gx, gy, c, side == 1,
                            out var x, out var y, out var width, out var height)) continue;
                    var horizontal = side == 0 && (gy == 0 || gy == room.CellsH - 1);
                    var length = horizontal ? width : height;
                    for (var along = c * .5f; along <= length - c * .5f; along += c * .5f)
                    {
                        var center = new WorldVec2(horizontal ? x + along : x + width * .5f,
                            horizontal ? y + height * .5f : y + along);
                        var nx = horizontal ? 0 : gx == 0 ? -1f : 1f;
                        var ny = horizontal ? gy == 0 ? -1f : 1f : 0;
                        var inside = new WorldVec2(center.X - nx*c*1.5f, center.Y - ny*c*1.5f);
                        var exit = new WorldVec2(center.X + nx*c*1.5f, center.Y + ny*c*1.5f);
                        var field = w.Strategic.CharacterEncounter;
                        if (!PrisonConfinementService.IsInside(w, room, room.SurfaceId, inside) ||
                            (field != null && !field.Contains(exit.X, exit.Y)) || !canCross(inside, exit)) continue;
                        outside = exit; return true;
                    }
                }
            }
            return false;
        }

        public static bool IsEncounterBystander(SimulationWorld w, EntityId id)
        {
            var field = w?.Strategic?.CharacterEncounter;
            if (field == null || field.Find(id.Value) != null ||
                (field.Phase != CharacterEncounterPhase.Active && field.Phase != CharacterEncounterPhase.ReadyToEnd) ||
                !CharacterWorldPresenceQuery.TryResolve(w, id, out var at) || !at.HasWorldPosition ||
                at.SurfaceId != field.SourceSurfaceId || !field.Contains(at.WorldPosition.X, at.WorldPosition.Y)) return false;
            return w.HuangcunSurvival.Enabled && id.Value == w.HuangcunSurvival.PrisonerId ||
                PrisonConfinementService.TryGetRoom(w, id, out _) ||
                w.Civilians.TryGet(id, out var civilian) && civilian.Disposition == CivilianDisposition.Fleeing;
        }

        public static int ReleaseRoom(SimulationWorld w, OutdoorConstructedAssetState room, EntityId rescuer, WorldVec2 exit)
        {
            var count = 0;
            foreach (var entity in w.Entities.All)
            {
                if (!PrisonConfinementService.TryGetRoom(w, entity.Id, out var held) || held.StableAssetId != room.StableAssetId ||
                    !PrisonConfinementService.IsInside(w, room, entity.Id)) continue;
                if (HuangcunSurvivalService.IsPrisoner(w, entity.Id)) HuangcunSurvivalService.ReleaseInPlace(w, rescuer);
                else if (MortalCivilianService.Release(w, entity.Id).IsFailure) continue;
                if (entity.TryGet<MovementIntentComponent>(out var intent)) intent.Clear();
                if (CombatLifeStateService.CanFight(entity))
                {
                    if (intent == null) { intent = new MovementIntentComponent(); entity.AddComponent(intent); }
                    intent.BeginWorldPoint(room.SurfaceId, exit.X, exit.Y,
                        destinationKey: "prison:exit:" + room.StableAssetId,
                        arrivalRadiusWorld: w.SurfaceSpatial.TryGet(room.SurfaceId, out var metric) ? metric.CellSize * .2f : .01f);
                }
                count++;
            }
            return count;
        }

        /// <summary>Finish the real exit before ordinary schedules replace its destination.</summary>
        public static bool DriveExitIntent(SimulationWorld w, Entity entity)
        {
            if (!entity.TryGet<MovementIntentComponent>(out var intent) || !intent.Active || !intent.HasWorldTarget ||
                !intent.DestinationKey.StartsWith("prison:exit:", StringComparison.Ordinal)) return false;
            if (intent.HostArrived || !CombatLifeStateService.CanFight(entity) ||
                intent.HostPathState == MovementHostPathState.PermanentFailure ||
                intent.HostPathState == MovementHostPathState.RetryablePathUnavailable)
            { intent.Clear(); return false; }
            if (!w.ContinuousOutdoorMaterialization.IsMaterialized(entity.Id))
            {
                if (!NpcColdMovement.Advance(w, entity, intent.TargetSurfaceId,
                        new WorldVec2(intent.TargetWorldX, intent.TargetWorldY), intent.ArrivalRadiusWorld, out var arrived))
                { intent.Clear(); return false; }
                if (arrived) { intent.Clear(); return false; }
            }
            return true;
        }
    }
}
