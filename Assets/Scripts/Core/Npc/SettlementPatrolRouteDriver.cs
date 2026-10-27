using System.Collections.Generic;
using XianXia.Core.Actions;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Orders;
using XianXia.Core.Schedule;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Npc
{
    /// <summary>Individual route progress; locomotion remains on the shared Core/Host movement bridge.</summary>
    public sealed class SettlementPatrolRouteDriver
    {
        public const string IntentPrefix = "settlement-patrol:";
        SimulationWorld _world;
        readonly Dictionary<EntityId, ulong> _lastTick = new Dictionary<EntityId, ulong>();
        readonly Dictionary<EntityId, ulong> _retryAfter = new Dictionary<EntityId, ulong>();

        public static bool UsesRoute(Entity actor) => SettlementPatrolService.Shift(actor) != 0 || SupervisorRoutineDriver.Applies(actor);
        public static void ClearOwnedIntent(Entity actor)
        {
            if (actor.TryGet<MovementIntentComponent>(out var move) && move.DestinationKey.StartsWith(IntentPrefix)) move.Clear();
        }

        public static string Describe(SimulationWorld world, Entity actor)
        {
            if (!UsesRoute(actor) || SupervisorAngerDriver.OwnsPursuit(world, actor.Id) ||
                world.Strategic.PlayerPartyContext?.IsMember(actor.Id) == true ||
                !actor.TryGet<ScheduleComponent>(out var binding) || !world.TryGetSchedule(binding.DefinitionId, out var definition) ||
                !definition.TryResolve(world.Tick, out var block) ||
                (block.Activity != ScheduleActivity.Patrol && block.Activity != ScheduleActivity.Inspect)) return null;
            string text = null;
            if (world.Civilians.TryGet(actor.Id, out var civilian) && civilian.Activity == MortalActivity.DedicatedDuty)
                text = civilian.ScheduleStatus;
            else if (actor.TryGet<PersonalLifeComponent>(out var personal) && personal.Activity == PersonalLifeActivity.None)
                text = personal.Status;
            return text != null && (text.StartsWith("巡视") || text.StartsWith("未设置巡视点")) ? text : null;
        }

        public bool Drive(SimulationWorld world, SimulationLoop loop, Entity actor, ScheduleActivity activity)
        {
            if (!UsesRoute(actor) || (activity != ScheduleActivity.Patrol && activity != ScheduleActivity.Inspect)) return false;
            if (!ReferenceEquals(_world, world)) { _world = world; _lastTick.Clear(); _retryAfter.Clear(); }
            if (!CombatLifeStateService.CanFight(actor) || PartyRestService.IsResting(world, actor) ||
                MortalTransportConstraintService.ValidateUnburdenedAction(world, actor.Id).IsFailure ||
                CharacterEncounterService.OwnsParticipantSpatialState(world, actor.Id) ||
                SupervisorAngerDriver.OwnsPursuit(world, actor.Id) ||
                world.Strategic.PlayerPartyContext?.IsMember(actor.Id) == true ||
                SquadCommandService.OwnsIndividualSchedule(world, actor.Id) ||
                SeparateSpaceTransitionService.IsOwnedByActiveSeparateSpace(world, actor.Id) ||
                world.Civilians.TryGet(actor.Id, out var civilianState) &&
                    (civilianState.Disposition != CivilianDisposition.Normal || !civilianState.RescueTargetId.IsNone) ||
                world.BackgroundCharacterTravel.IsTraveling(actor.Id))
            { ClearOwnedIntent(actor); return true; }
            var queue = world.GetOrCreateOrderQueue(actor.Id);
            if (queue.HasSource(OrderSource.Player) || queue.HasSource(OrderSource.Event))
            { ClearOwnedIntent(actor); return true; }
            if (actor.TryGet<ActionStateComponent>(out var active) && active.HasActiveAction && active.ActiveOrderSource != OrderSource.Schedule)
                return true;
            var wasScheduledMove = active != null && active.HasActiveAction && active.ActiveOrderSource == OrderSource.Schedule;
            loop.CancelOrdersFromSource(actor.Id, OrderSource.Schedule);
            if (wasScheduledMove && actor.TryGet<MovementIntentComponent>(out var oldMove) &&
                !string.IsNullOrEmpty(oldMove.TargetWorkAreaId)) oldMove.Clear();
            world.WorkAreaOccupancy.Release(actor.Id);
            if (_lastTick.TryGetValue(actor.Id, out var last) && last == world.Tick.Value) return true;
            _lastTick[actor.Id] = world.Tick.Value;
            var siteId = MortalCivilianQuery.ResolveCurrentSiteId(world, actor.Id);
            if (string.IsNullOrEmpty(siteId) || !world.Strategic.Sites.TryGet(siteId, out var site) ||
                !CharacterWorldPresenceQuery.TryResolve(world, actor.Id, out var at) || !at.HasWorldPosition ||
                at.SurfaceId != site.CoreSurfaceId || !world.SurfaceGround.TryGet(at.SurfaceId, out var ground))
            { ClearOwnedIntent(actor); Status(world, actor, "巡视受阻：没有本城地面位置"); return true; }
            var points = SettlementPatrolRoutes.Get(world, siteId);
            if (points.Count == 0)
            { ClearOwnedIntent(actor); Status(world, actor, "未设置巡视点，等待安排"); return true; }
            var key = SettlementPatrolRoutes.Key(siteId) + ":actor:" + actor.Id.Value;
            var counters = world.ContentCounters;
            var revision = SettlementPatrolRoutes.Revision(world, siteId);
            if (counters.Get(key + ":revision") != revision)
            {
                counters.Set(key + ":revision", revision);
                counters.Set(key + ":cursor", SupervisorRoutineDriver.Applies(actor) ? world.Random.NextInt(0, points.Count) : 0);
                counters.Set(key + ":phase", 0); counters.Set(key + ":wait", 0);
                ClearOwnedIntent(actor); _retryAfter.Remove(actor.Id);
            }
            var cursor = counters.Get(key + ":cursor") % points.Count;
            var goal = points[cursor];
            var radius = ground.CellSize * .65f;
            var arrived = WorldVec2.Distance(at.WorldPosition, goal) <= radius;
            if (arrived)
            {
                ClearOwnedIntent(actor);
                if (counters.Get(key + ":phase") == 0)
                { counters.Set(key + ":phase", 1); counters.Set(key + ":wait", SettlementPatrolRoutes.WaitTicks(points.Count)); }
                else
                {
                    var wait = counters.Get(key + ":wait") - 1;
                    counters.Set(key + ":wait", wait);
                    if (wait <= 0)
                    {
                        var next = (cursor + 1) % points.Count;
                        if (SupervisorRoutineDriver.Applies(actor) && points.Count > 1)
                        { next = world.Random.NextInt(0, points.Count - 1); if (next >= cursor) next++; }
                        counters.Set(key + ":cursor", next); counters.Set(key + ":phase", 0);
                        Status(world, actor, "巡视：前往第" + (next + 1) + "点"); return true;
                    }
                }
                Status(world, actor, "巡视：第" + (cursor + 1) + "点停留"); return true;
            }
            counters.Set(key + ":phase", 0);
            if (!actor.TryGet<MovementIntentComponent>(out var move))
            { move = new MovementIntentComponent(); actor.AddComponent(move); }
            var destination = IntentPrefix + siteId + ":" + revision + ":" + cursor;
            if (move.DestinationKey == destination && (move.HostArrived ||
                move.HostPathState == MovementHostPathState.PermanentFailure || move.HostPathState == MovementHostPathState.RetryablePathUnavailable))
            { move.Clear(); _retryAfter[actor.Id] = world.Tick.Value + 8; Status(world, actor, "巡视点受阻，等待重试"); return true; }
            if (_retryAfter.TryGetValue(actor.Id, out var retry) && world.Tick.Value < retry) return true;
            if (!world.ContinuousOutdoorMaterialization.IsMaterialized(actor.Id))
            {
                if (!NpcColdMovement.Advance(world, actor, at.SurfaceId, goal, radius, out _))
                { _retryAfter[actor.Id] = world.Tick.Value + 8; Status(world, actor, "巡视点受阻，等待重试"); }
                else Status(world, actor, "巡视：前往第" + (cursor + 1) + "点");
                return true;
            }
            if (!move.Active || move.DestinationKey != destination)
                move.BeginWorldPoint(at.SurfaceId, goal.X, goal.Y, destinationKey: destination, arrivalRadiusWorld: radius);
            Status(world, actor, "巡视：前往第" + (cursor + 1) + "点"); return true;
        }

        static void Status(SimulationWorld world, Entity actor, string text)
        {
            if (world.Civilians.TryGet(actor.Id, out var state)) state.ScheduleStatus = text;
            if (actor.TryGet<PersonalLifeComponent>(out var personal))
            { personal.Activity = PersonalLifeActivity.None; personal.Status = text; }
        }
    }
}
