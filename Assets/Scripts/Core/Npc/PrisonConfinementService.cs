using System;
using XianXia.Core.Construction;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Npc
{
    /// <summary>Room-scoped detainee movement; confinement is independent of Site ownership.</summary>
    public static class PrisonConfinementService
    {
        const string WalkKey = "prison:walk:";

        public static bool TryGetRoom(SimulationWorld world, EntityId id, out OutdoorConstructedAssetState room)
        {
            room = null;
            if (world == null || id.IsNone || !world.Entities.TryGet(id, out var entity) ||
                !entity.TryGet<LifecycleComponent>(out var life) || life.IsDead || life.IsRemoved) return false;
            // A formal carrier owns escorted captives; do not constrain that transport to a room.
            if (world.Civilians.TryGet(id, out var captive) && captive.Disposition == CivilianDisposition.CapturedEscorted)
                return false;
            var roomId = HuangcunSurvivalService.IsPrisoner(world, id) ? world.HuangcunSurvival.PrisonRoomId :
                captive?.Disposition == CivilianDisposition.Detained ? captive.DetainedResidenceWorkAreaId : null;
            return !string.IsNullOrEmpty(roomId) && world.OutdoorConstructedAssets.Assets.TryGetValue(roomId, out room) &&
                RoomBedGeometry.IsRoom(room) && room.RoomUsage == RoomUsage.Prison;
        }

        public static bool IsInside(SimulationWorld world, OutdoorConstructedAssetState room, string surface, WorldVec2 point)
        {
            if (room == null || room.SurfaceId != surface) return false;
            var cell = world.SurfaceSpatial.TryGet(room.SurfaceId, out var metric) ? metric.CellSize : 0f;
            // Raw Snapshot restore has no Content metric yet: validate its exact room rectangle.
            // Live navigation additionally includes the wall and the character's physical body.
            var margin = cell * 1.5f;
            return point.X >= room.WorldX + margin && point.Y >= room.WorldY + margin &&
                point.X <= room.WorldX + room.WorldWidth - margin && point.Y <= room.WorldY + room.WorldHeight - margin;
        }

        public static bool IsInside(SimulationWorld world, OutdoorConstructedAssetState room, EntityId id) =>
            CharacterWorldPresenceQuery.TryResolve(world, id, out var at) && at.HasWorldPosition &&
            IsInside(world, room, at.SurfaceId, at.WorldPosition);

        public static bool IsConfinedOutdoor(SimulationWorld world, EntityId id) =>
            world != null && !world.LocalMap.IsInInterior &&
            !CharacterEncounterService.OwnsParticipantSpatialState(world, id) && TryGetRoom(world, id, out _);

        public static bool CanWalk(SimulationWorld world, Entity entity) => entity != null &&
            entity.TryGet<LifecycleComponent>(out var life) && life.State == LifecycleState.Captured && !life.IsIncapacitated &&
            XianXia.Core.Combat.CombatInjuryService.CanWalk(entity) &&
            IsConfinedOutdoor(world, entity.Id);

        public static bool AllowsPosition(SimulationWorld world, EntityId id, string surface, WorldVec2 point) =>
            !IsConfinedOutdoor(world, id) || TryGetRoom(world, id, out var room) && IsInside(world, room, surface, point);

        /// <summary>Custody release never relocates or heals a real bed occupant.</summary>
        public static void ReleasePlacementInPlace(SimulationWorld world, EntityId id)
        {
            if (!world.Entities.TryGet(id, out var entity)) return;
            entity.TryGet<LifecycleComponent>(out var life);
            world.Civilians.TryGet(id, out var civilian);
            var preserveBed = RoomBedFacilityService.TryGetOccupiedBed(world, id, RoomUsage.Prison, out var bed) &&
                RoomBedFacilityService.IsAtBed(world, bed, id) &&
                (life?.IsIncapacitated == true || civilian != null && civilian.SleepPhase != CivilianSleepPhase.None);
            var placements = new System.Collections.Generic.List<string>();
            foreach (var pair in world.RecoverySpotDeliveries.Placements)
                if (pair.Value == id && (!preserveBed || pair.Key != bed.StableAssetId)) placements.Add(pair.Key);
            foreach (var key in placements) world.RecoverySpotDeliveries.RemoveSpot(key);
            if (preserveBed)
            {
                bed.ReservedCharacterId = id;
                if (life?.IsIncapacitated == true) world.RecoverySpotDeliveries.TryPlace(bed.StableAssetId, id);
            }
            else RoomBedFacilityService.Release(world, id);
            if (civilian != null && (!preserveBed || life?.IsIncapacitated == true))
            { civilian.SleepPhase = CivilianSleepPhase.None; civilian.SleepStartedTick = 0; }
            if (entity.TryGet<MovementIntentComponent>(out var intent)) intent.Clear();
        }

        static float CellSize(SimulationWorld world, OutdoorConstructedAssetState room) =>
            world.SurfaceSpatial.TryGet(room.SurfaceId, out var metric) ? metric.CellSize :
                Math.Min(room.WorldWidth / Math.Max(1, room.CellsW), room.WorldHeight / Math.Max(1, room.CellsH));

        public static void Drive(SimulationWorld world)
        {
            foreach (var entity in world.Entities.All)
            {
                if (!IsConfinedOutdoor(world, entity.Id) || !TryGetRoom(world, entity.Id, out var room)) continue;
                BackgroundCharacterTravelService.CancelTravelIfAny(world, entity.Id);
                if (world.Civilians.TryGet(entity.Id, out var civilian))
                {
                    // Retain one real capacity slot while awake, without claiming to lie on the bed.
                    OutdoorConstructedAssetState held = null;
                    foreach (var bed in RoomBedFacilityService.BedsInRoom(world, room.StableAssetId))
                        if (bed.OccupantCharacterId == entity.Id || bed.ReservedCharacterId == entity.Id) { held = bed; break; }
                    if (held == null) RoomBedFacilityService.TryReserveInRoom(world, room.StableAssetId, entity.Id, out held);
                    if (held != null)
                    {
                        held.ReservedCharacterId = entity.Id;
                        if (CanWalk(world, entity) && civilian.SleepPhase == CivilianSleepPhase.None)
                            held.OccupantCharacterId = EntityId.None;
                    }
                    if (civilian.SleepPhase != CivilianSleepPhase.None) continue;
                }
                if (!CanWalk(world, entity))
                {
                    if (entity.TryGet<MovementIntentComponent>(out var downedIntent) &&
                        downedIntent.DestinationKey.StartsWith(WalkKey, StringComparison.Ordinal)) downedIntent.Clear();
                    continue;
                }
                var keeperApproaching = false;
                foreach (var pair in world.Civilians.All)
                    if (pair.Value.Activity == MortalActivity.PrisonCare && pair.Value.WardenTargetId == entity.Id &&
                        pair.Value.Profession == MortalProfession.Warden)
                    { keeperApproaching = true; break; }
                if (keeperApproaching)
                {
                    if (entity.TryGet<MovementIntentComponent>(out var waiting) &&
                        waiting.DestinationKey.StartsWith(WalkKey, StringComparison.Ordinal)) waiting.Clear();
                    continue;
                }
                if (HuangcunSurvivalService.IsPrisoner(world, entity.Id) && IsReceivingCare(world)) continue;
                if (!entity.TryGet<MovementIntentComponent>(out var intent))
                { intent = new MovementIntentComponent(); entity.AddComponent(intent); }
                if (intent.Active && !intent.DestinationKey.StartsWith(WalkKey, StringComparison.Ordinal)) intent.Clear();
                if (intent.HostArrived) intent.Clear();
                if ((world.Tick.Value + entity.Id.Value) % 24 != 0) continue;
                var cell = CellSize(world, room);
                var columns = Math.Max(1, (int)(room.WorldWidth / cell) - 4);
                var rows = Math.Max(1, (int)(room.WorldHeight / cell) - 4);
                var phase = world.Tick.Value / 24 + entity.Id.Value;
                for (ulong n = 0; n < 8; n++)
                {
                    var x = room.WorldX + (2.5f + (int)((phase * 3 + n) % (ulong)columns)) * cell;
                    var y = room.WorldY + (2.5f + (int)((phase * 5 + n * 3) % (ulong)rows)) * cell;
                    var target = new WorldVec2(x, y);
                    if (!IsInside(world, room, room.SurfaceId, target) ||
                        !world.SurfaceGround.TryGet(room.SurfaceId, out var ground) || !ground.IsWalkable(x, y)) continue;
                    intent.BeginWorldPoint(room.SurfaceId, x, y, destinationKey: WalkKey + room.StableAssetId,
                        arrivalRadiusWorld: cell * .2f);
                    break;
                }
            }
        }

        static bool IsReceivingCare(SimulationWorld world)
        {
            foreach (var action in world.ActiveActions.Values)
                if (action is XianXia.Core.Actions.HuangcunSurvivalAction care &&
                    care.Status == XianXia.Core.Actions.ActionStatus.Running &&
                    (care.Task == "feed" || care.Task == "medicine" || care.Task == "force_release")) return true;
            return false;
        }
    }
}
