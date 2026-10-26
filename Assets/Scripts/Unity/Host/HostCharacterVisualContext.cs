using System;
using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Actions;
using XianXia.Core.Construction;
using XianXia.Core.Entities;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Exploration;
using XianXia.Core.Npc;
using XianXia.Core.Schedule;
using XianXia.Core.Simulation;

namespace XianXia.Unity.Host
{
    public enum HumanWorkMotion { None, Generic, Soil, Sow, Tend, Harvest, Chop, Mine, Build, Meditate, Eat, BoardThink, BoardPlace }
    public enum HumanAttackStyle { Unarmed, Swing, Thrust, Cast }

    public struct HostCharacterWorkVisual
    {
        public HumanWorkMotion Motion;
        public HumanCommonRigView.Equipment Tool;
        public bool HasTarget;
        public Vector3 Target;
        public bool Active => Motion != HumanWorkMotion.None;

        public static HostCharacterWorkVisual For(HumanWorkMotion motion,
            HumanCommonRigView.Equipment tool = HumanCommonRigView.Equipment.None) =>
            new HostCharacterWorkVisual { Motion = motion, Tool = tool };
    }

    /// <summary>Read-only semantics. Job titles and ID substrings are not animation authority.</summary>
    public static class HostCharacterVisualContext
    {
        static SimulationWorld _carryWorld;
        static int _carryFrame = -1;
        static readonly HashSet<EntityId> CargoCarriers = new HashSet<EntityId>();
        static readonly HashSet<EntityId> PersonCarriers = new HashSet<EntityId>();
        static readonly HashSet<EntityId> CarriedPeople = new HashSet<EntityId>();

        static SimulationWorld _bedWorld;
        static int _bedFrame = -1;
        static readonly Dictionary<(EntityId, RoomUsage), OutdoorConstructedAssetState> OccupiedBeds =
            new Dictionary<(EntityId, RoomUsage), OutdoorConstructedAssetState>();

        // Presentation-only index, rebuilt each frame: never caches permission or
        // navigation results across ticks, demolition, room usage changes or loading.
        public static bool TryGetValidOccupiedRestBed(SimulationWorld world, EntityId id,
            out OutdoorConstructedAssetState bed)
        {
            bed = null;
            if (world == null || id.IsNone) return false;
            if (!ReferenceEquals(_bedWorld, world) || _bedFrame != Time.frameCount)
            {
                _bedWorld = world; _bedFrame = Time.frameCount;
                OccupiedBeds.Clear();
                var beds = world.OutdoorConstructedAssets.Beds;
                for (var i = 0; i < beds.Count; i++)
                {
                    var candidate = beds[i];
                    if (candidate.OccupantCharacterId.IsNone) continue;
                    var usage = RoomUsage.Residential;
                    if (!string.IsNullOrEmpty(candidate.ParentRoomId))
                    {
                        if (!world.OutdoorConstructedAssets.Assets.TryGetValue(candidate.ParentRoomId, out var room) ||
                            !RoomBedGeometry.IsRoom(room)) continue;
                        usage = room.RoomUsage;
                    }
                    var key = (candidate.OccupantCharacterId, usage);
                    if (!OccupiedBeds.ContainsKey(key)) OccupiedBeds.Add(key, candidate);
                }
            }
            return Valid(RoomUsage.Residential, world, id, out bed) ||
                   Valid(RoomUsage.Prison, world, id, out bed) ||
                   Valid(RoomUsage.Medical, world, id, out bed);
        }

        static bool Valid(RoomUsage usage, SimulationWorld world, EntityId id,
            out OutdoorConstructedAssetState bed)
        {
            return OccupiedBeds.TryGetValue((id, usage), out bed) && bed.OccupantCharacterId == id &&
                world.OutdoorConstructedAssets.Assets.TryGetValue(bed.StableAssetId, out var current) &&
                ReferenceEquals(current, bed) && RoomBedFacilityService.BedHasUsage(world, bed, usage) &&
                RoomBedFacilityService.CanUseBed(world, bed, id, out _) &&
                RoomBedFacilityService.IsAtBed(world, bed, id);
        }

        public static void ResolveCarry(SimulationWorld world, EntityId id,
            out bool cargo, out bool person, out bool carried)
        {
            if (!ReferenceEquals(_carryWorld, world) || _carryFrame != Time.frameCount)
            {
                _carryWorld = world;
                _carryFrame = Time.frameCount;
                CargoCarriers.Clear(); PersonCarriers.Clear(); CarriedPeople.Clear();
                foreach (var job in world.CivilianConstructionJobs.Jobs.Values)
                    if (!job.CarrierId.IsNone && job.PayloadCount > 0) CargoCarriers.Add(job.CarrierId);
                foreach (var pair in world.Civilians.All)
                {
                    var state = pair.Value;
                    if (state == null) continue;
                    if (state.RescueCarrying && !state.RescueTargetId.IsNone)
                    { PersonCarriers.Add(pair.Key); CarriedPeople.Add(state.RescueTargetId); }
                    if (state.Disposition == CivilianDisposition.CapturedEscorted && !state.CarrierId.IsNone)
                    { PersonCarriers.Add(state.CarrierId); CarriedPeople.Add(pair.Key); }
                }
            }
            cargo = CargoCarriers.Contains(id); person = PersonCarriers.Contains(id); carried = CarriedPeople.Contains(id);
        }

        public static HostCharacterWorkVisual ResolveWork(SimulationWorld world, Entity entity,
            HostFarmFieldLabor farm, HostDestructibleAssault assault)
        {
            if (world == null || entity == null) return default;
            if (farm != null && farm.TryGetWorkVisual(entity.Id, out var farming)) return farming;
            if (assault != null && assault.TryGetWorkVisual(entity.Id, out var logging)) return logging;
            // An executor in its approach/wait phase must not inherit a stale scheduled action.
            if (farm != null && farm.IsFarming(entity.Id) || assault != null && assault.IsAttacker(entity.Id))
                return default;

            var traveling = entity.TryGet<MovementIntentComponent>(out var intent) &&
                intent.Active && !intent.HostArrived;
            if (traveling) return default;
            if (ChessRecreationService.IsPlaying(world, entity.Id, out var placing, out _))
                return HostCharacterWorkVisual.For(placing ? HumanWorkMotion.BoardPlace : HumanWorkMotion.BoardThink);
            IAction active = null;
            if (entity.TryGet<ActionStateComponent>(out var actionState) && actionState.HasActiveAction &&
                world.ActiveActions.TryGetValue(actionState.ActiveActionId, out var running) &&
                running.Status == ActionStatus.Running) active = running;
            // A real running action is shared by player and NPC work. Resolve it before
            // civilian fallback so a stale/default civilian activity cannot hide it.
            // Non-work actions also remain authoritative instead of inheriting old labor.
            if (active != null) return FromRunningAction(world, entity, active);

            if (MortalCivilianQuery.IsManagedCivilian(world, entity) &&
                world.Civilians.TryGet(entity.Id, out var civilian))
            {
                if (civilian.FoodFetchPhase == FoodFetchPhase.Eating)
                    return HostCharacterWorkVisual.For(HumanWorkMotion.Eat);
                switch (civilian.Activity)
                {
                    case MortalActivity.MedicCare:
                        return civilian.MedicTreatmentSeconds > 0 && civilian.MedicLastTreatmentTick == world.Tick.Value
                            ? HostCharacterWorkVisual.For(HumanWorkMotion.Generic) : default;
                    case MortalActivity.FarmerWork:
                    case MortalActivity.HerbFarmerWork:
                        // Managed farm movement has no WorkAction. Only an arrived farm intent is evidence.
                        if (intent != null && intent.Active && intent.HostArrived &&
                            world.TryGetWorkArea(intent.TargetWorkAreaId, out var farmArea))
                            return FromTags(farmArea.Tags);
                        return default;
                    case MortalActivity.Construction:
                        if (intent == null || !intent.Active || !intent.HostArrived || !intent.HasWorldTarget)
                            return default;
                        foreach (var job in world.CivilianConstructionJobs.Jobs.Values)
                            if (job.MaterialsDelivered && job.SurfaceId == intent.TargetSurfaceId &&
                                Math.Abs(job.WorldX - intent.TargetWorldX) < .1f &&
                                Math.Abs(job.WorldY - intent.TargetWorldY) < .1f)
                                return HostCharacterWorkVisual.For(HumanWorkMotion.Build);
                        return default;
                    case MortalActivity.Eat:
                        return civilian.FoodFetchPhase == FoodFetchPhase.Eating
                            ? HostCharacterWorkVisual.For(HumanWorkMotion.Eat) : default;
                    case MortalActivity.DedicatedDuty: return default; // Requires an actual running duty action.
                    default: return default; // Logging needs an actual in-range tree executor, not a profession.
                }
            }
            return default;
        }

        static HostCharacterWorkVisual FromRunningAction(SimulationWorld world, Entity entity, IAction active)
        {
            if (active is CultivateAction || active is RecoveryAction)
                return HostCharacterWorkVisual.For(HumanWorkMotion.Meditate);
            if (active is WorkAction work)
            {
                switch (work.Activity)
                {
                    case ScheduleActivity.Labor:
                        return world.TryGetWorkArea(work.TargetWorkAreaId, out var area)
                            ? FromTags(area.Tags) : HostCharacterWorkVisual.For(HumanWorkMotion.Generic);
                    case ScheduleActivity.Cultivate:
                        return HostCharacterWorkVisual.For(HumanWorkMotion.Meditate);
                    case ScheduleActivity.Eat:
                        return HostCharacterWorkVisual.For(HumanWorkMotion.Eat);
                    default:
                        return default; // Rest, patrol, inspection and idle are not manual labor.
                }
            }
            if (active is LaborAction)
            {
                if (entity.TryGet<EntityLocationComponent>(out var loc) && loc.HasLocation &&
                    WorldLocationQuery.TryGet(world, loc.LocationId, out var location))
                    return FromTags(location.Tags);
                return HostCharacterWorkVisual.For(HumanWorkMotion.Generic);
            }
            return default;
        }

        static HostCharacterWorkVisual FromTags(IReadOnlyList<string> tags)
        {
            if (Has(tags, "mine") || Has(tags, "ore"))
                return HostCharacterWorkVisual.For(HumanWorkMotion.Mine, HumanCommonRigView.Equipment.Pick);
            if (Has(tags, "wood") || Has(tags, "logging"))
                return HostCharacterWorkVisual.For(HumanWorkMotion.Chop, HumanCommonRigView.Equipment.Axe);
            if (Has(tags, "farm") || Has(tags, "grain") || Has(tags, "herb"))
                return HostCharacterWorkVisual.For(HumanWorkMotion.Tend);
            return HostCharacterWorkVisual.For(HumanWorkMotion.Generic);
        }

        static bool Has(IReadOnlyList<string> tags, string value)
        {
            if (tags != null)
                for (var i = 0; i < tags.Count; i++)
                    if (string.Equals(tags[i], value, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
