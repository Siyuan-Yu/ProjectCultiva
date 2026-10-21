using System;
using System.Collections.Generic;
using XianXia.Core.Actions;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Orders;
using XianXia.Core.Schedule;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Npc
{
    public sealed class ChessBoardSpot
    {
        public string Id = string.Empty, SiteId = string.Empty, SurfaceId = string.Empty;
        public WorldVec2 Center, SeatA, SeatB;
        public float ArrivalRadius;
    }

    public sealed class ChessGameState
    {
        public string BoardId = string.Empty;
        public ulong PlayerA, PlayerB, WaitingSinceTick, LastProcessedTick, CooldownUntilTick;
        public int PlayedTicks, PlayedTicksB;
        public ulong StartedTickB, CooldownUntilTickB;
        public bool Playing;
        public ChessGameState Copy() => (ChessGameState)MemberwiseClone();
    }

    /// <summary>Two independent recreation seats, physical arrival and WorldTick-owned play time.</summary>
    public static class ChessRecreationService
    {
        public const int GameTicks = 24;
        const ulong WaitLimit = 60;

        public static bool TryGetGame(SimulationWorld world, EntityId actor,
            out ChessGameState game, out ChessBoardSpot board)
        {
            game = null; board = null;
            if (world == null || actor.IsNone) return false;
            foreach (var state in world.ChessGames.Values)
                if ((state.PlayerA == actor.Value || state.PlayerB == actor.Value) &&
                    world.ChessBoards.TryGetValue(state.BoardId, out board))
                { game = state; return true; }
            return false;
        }

        public static bool Owns(SimulationWorld world, EntityId actor) => TryGetGame(world, actor, out _, out _);

        public static bool IsPlaying(SimulationWorld world, EntityId actor, out bool placing, out WorldVec2 direction)
        {
            placing = false; direction = default;
            if (!TryGetGame(world, actor, out var game, out var board) ||
                !AtSeat(world, actor, board, game.PlayerA == actor.Value)) return false;
            var ticks = game.PlayerA == actor.Value ? game.PlayedTicks : game.PlayedTicksB;
            placing = ticks / 3 % 2 == 0;
            var seat = game.PlayerA == actor.Value ? board.SeatA : board.SeatB;
            direction = new WorldVec2(board.Center.X-seat.X, board.Center.Y-seat.Y);
            return true;
        }

        public static bool CanParticipate(SimulationWorld world, Entity e)
        {
            if (world == null || e == null || !CombatLifeStateService.CanFight(e) ||
                PartyRestService.BlocksMovement(e) || world.Strategic.PlayerPartyContext?.IsMember(e.Id) == true ||
                CharacterEncounterService.OwnsParticipantSpatialState(world, e.Id) ||
                SeparateSpaceCombatPolicy.IsEntityInActiveSpace(world, e.Id) ||
                SquadCommandService.OwnsIndividualSchedule(world, e.Id) ||
                world.BackgroundCharacterTravel.IsTraveling(e.Id) ||
                MortalTransportConstraintService.ValidateUnburdenedAction(world, e.Id).IsFailure) return false;
            var siteId = MortalCivilianQuery.ResolveCurrentSiteId(world, e.Id);
            if (string.IsNullOrEmpty(siteId) || !world.Strategic.Sites.TryGet(siteId, out var safeSite) || !safeSite.IsCoreActive ||
                e.TryGet<XianXia.Core.Social.FactionMembershipComponent>(out var faction) &&
                WarGateService.IsAtWar(world, faction.FactionId, safeSite.OwnerFactionId)) return false;
            var queue = world.GetOrCreateOrderQueue(e.Id);
            if (queue.HasSource(OrderSource.Player) || queue.HasSource(OrderSource.Event)) return false;
            if (e.TryGet<ActionStateComponent>(out var action) && action.HasActiveAction &&
                action.ActiveOrderSource != OrderSource.Ai && action.ActiveOrderSource != OrderSource.Schedule) return false;
            if (e.TryGet<NpcAutonomyState>(out var combat) && combat.Retreating) return false;
            if (world.Civilians.TryGet(e.Id, out var mortal) && MortalCivilianQuery.IsManagedCivilian(world, e))
            {
                if (mortal.Disposition != CivilianDisposition.Normal || mortal.Satiety <= MortalCivilianTuning.HungerThreshold ||
                    mortal.Energy <= MortalCivilianTuning.SleepThreshold || !mortal.RescueTargetId.IsNone ||
                    !mortal.MedicTargetId.IsNone || mortal.MedicCarryingHerb ||
                    FactionMortalSchedule.Current(world, e, out _, out _, out _) != FactionMortalSchedulePhase.Free) return false;
                // Rescue and medicine are evaluated by the civilian authority before leisure.
                if (mortal.Activity != MortalActivity.Idle && mortal.Activity != MortalActivity.Chess) return false;
            }
            else
            {
                var life = PersonalLifeService.Ensure(world, e);
                if (life == null || life.Satiety <= 35 || life.Energy <= 30) return false;
                if ((e.Tags & EntityTag.Npc) != 0 && e.TryGet<ScheduleComponent>(out var schedule) &&
                    !string.IsNullOrEmpty(schedule.DefinitionId) && world.TryGetSchedule(schedule.DefinitionId, out var definition) &&
                    definition.TryResolve(world.Tick, out var block) && block.Activity != ScheduleActivity.Idle) return false;
            }
            if (!CharacterWorldPresenceQuery.TryResolve(world, e.Id, out var at) || !at.HasWorldPosition ||
                !world.SurfaceGround.TryGet(at.SurfaceId, out var ground) || ground == null) return false;
            foreach (var other in world.Entities.All)
                if (other.Id != e.Id && NpcHostilityService.CanAutoAttack(world, other.Id, e.Id) &&
                    CharacterWorldPresenceQuery.TryResolve(world, other.Id, out var enemy) && enemy.HasWorldPosition &&
                    enemy.SurfaceId == at.SurfaceId && WorldVec2.Distance(enemy.WorldPosition, at.WorldPosition) <= ground.CellSize*6)
                    return false;
            return true;
        }

        public static bool TryJoin(SimulationWorld world, SimulationLoop loop, Entity e)
        {
            if (Owns(world, e.Id)) return true;
            if (!CanParticipate(world, e) ||
                !CharacterWorldPresenceQuery.TryResolve(world, e.Id, out var at)) return false;
            var site = MortalCivilianQuery.ResolveCurrentSiteId(world, e.Id);
            var boards = new List<ChessBoardSpot>(world.ChessBoards.Values);
            boards.Sort((a, b) =>
            {
                var d = WorldVec2.Distance(at.WorldPosition, a.Center).CompareTo(WorldVec2.Distance(at.WorldPosition, b.Center));
                return d == 0 ? string.CompareOrdinal(a.Id, b.Id) : d;
            });
            foreach (var board in boards)
            {
                if (board.SurfaceId != at.SurfaceId || board.SiteId != site) continue;
                if (!world.ChessGames.TryGetValue(board.Id, out var game))
                    world.ChessGames.Add(board.Id, game = new ChessGameState { BoardId = board.Id });
                bool seatA = game.PlayerA == 0 && game.CooldownUntilTick <= world.Tick.Value;
                bool seatB = game.PlayerB == 0 && game.CooldownUntilTickB <= world.Tick.Value;
                if (!seatA && !seatB) continue;
                var otherId = seatA ? game.PlayerB : game.PlayerA;
                if (otherId != 0 && world.Entities.TryGet(new EntityId(otherId), out var other) &&
                    (NpcHostilityService.CanAutoAttack(world, other.Id, e.Id) ||
                     NpcHostilityService.CanAutoAttack(world, e.Id, other.Id))) continue;
                var seat = seatA ? board.SeatA : board.SeatB;
                var route = new List<WorldVec2>();
                if (!world.SurfaceGround.TryGet(board.SurfaceId, out var nav) ||
                    !NpcLocalNavigation.TryRoute(world, nav, at.WorldPosition, seat, route)) continue;
                loop.CancelOrdersFromSource(e.Id, OrderSource.Ai);
                loop.CancelOrdersFromSource(e.Id, OrderSource.Schedule);
                if (seatA)
                { game.PlayerA = e.Id.Value; game.WaitingSinceTick = world.Tick.Value; game.PlayedTicks = 0; }
                else
                { game.PlayerB = e.Id.Value; game.StartedTickB = world.Tick.Value; game.PlayedTicksB = 0; }
                SetStatus(world, e, "前往棋盘", false);
                // Reserve the AI action channel while walking; only arrival enables recreation.
                loop.EnqueueOrder(new Order(loop.AllocateOrderId(), e.Id, OrderType.Wait, OrderSource.Ai,
                    waitTicks: WaitLimit*2 + (ulong)GameTicks));
                return true;
            }
            return false;
        }

        public static void Drive(SimulationWorld world, SimulationLoop loop)
        {
            // Off-duty mortals can participate without acquiring a second needs or schedule authority.
            var mortals = new List<EntityId>();
            foreach (var pair in world.Civilians.All)
                if (pair.Value.Activity == MortalActivity.Idle || pair.Value.Activity == MortalActivity.Chess) mortals.Add(pair.Key);
            foreach (var e in world.Entities.All)
            {
                if (MortalCivilianQuery.IsManagedCivilian(world, e) || (e.Tags & EntityTag.Npc) == 0 ||
                    !e.TryGet<PersonalLifeComponent>(out var personal) ||
                    !e.TryGet<ScheduleComponent>(out var schedule) || string.IsNullOrEmpty(schedule.DefinitionId) ||
                    !world.TryGetSchedule(schedule.DefinitionId, out var definition) ||
                    !definition.TryResolve(world.Tick, out var block) || block.Activity != ScheduleActivity.Idle) continue;
                if (personal.Preference == PersonalLifePreference.Recreation || personal.CurrentMood < 50 ||
                    personal.Preference == PersonalLifePreference.Balanced && personal.LastCultivationTick > personal.LastRecreationTick)
                    mortals.Add(e.Id);
            }
            mortals.Sort((a, b) => a.Value.CompareTo(b.Value));
            foreach (var id in mortals)
                if (world.Entities.TryGet(id, out var e)) TryJoin(world, loop, e);
            foreach (var game in new List<ChessGameState>(world.ChessGames.Values))
            {
                if (!world.ChessBoards.TryGetValue(game.BoardId, out var board))
                { EndSeat(world, loop, game, true); EndSeat(world, loop, game, false); continue; }
                var advance = game.LastProcessedTick != world.Tick.Value;
                DriveSeat(world, loop, game, board, true, advance);
                DriveSeat(world, loop, game, board, false, advance);
                game.LastProcessedTick = world.Tick.Value;
                game.Playing = game.PlayerA != 0 && AtSeat(world, new EntityId(game.PlayerA), board, true) ||
                    game.PlayerB != 0 && AtSeat(world, new EntityId(game.PlayerB), board, false);
            }
        }

        static void DriveSeat(SimulationWorld world, SimulationLoop loop, ChessGameState game,
            ChessBoardSpot board, bool a, bool advance)
        {
            var id = a ? game.PlayerA : game.PlayerB;
            if (id == 0) return;
            if (!world.Entities.TryGet(new EntityId(id), out var e) || !CanParticipate(world, e))
            { EndSeat(world, loop, game, a); return; }
            var ready = Approach(world, e, board, a, out var failed);
            var started = a ? game.WaitingSinceTick : game.StartedTickB;
            if (failed || !ready && world.Tick.Value - started >= WaitLimit*2)
            { EndSeat(world, loop, game, a); return; }
            SetStatus(world, e, ready ? "下棋娱乐" : "前往棋盘", ready);
            if (!ready) return;
            if (advance) { if (a) game.PlayedTicks++; else game.PlayedTicksB++; }
            var ticks = a ? game.PlayedTicks : game.PlayedTicksB;
            if (!e.TryGet<ActionStateComponent>(out var action) || !action.HasActiveAction)
                loop.EnqueueOrder(new Order(loop.AllocateOrderId(), e.Id, OrderType.Wait, OrderSource.Ai,
                    waitTicks: (ulong)Math.Max(1, GameTicks-ticks+1)));
            if (ticks >= GameTicks) EndSeat(world, loop, game, a);
        }

        public static void Interrupt(SimulationWorld world, SimulationLoop loop, EntityId actor)
        {
            if (TryGetGame(world, actor, out var game, out _))
                EndSeat(world, loop, game, game.PlayerA == actor.Value);
        }

        static bool Approach(SimulationWorld world, Entity e, ChessBoardSpot board, bool a, out bool failed)
        {
            failed = false;
            var goal = a ? board.SeatA : board.SeatB;
            if (AtSeat(world, e.Id, board, a))
            {
                if (e.TryGet<MovementIntentComponent>(out var arrived) && arrived.DestinationKey.StartsWith("chess:", StringComparison.Ordinal)) arrived.Clear();
                return true;
            }
            if (!world.ContinuousOutdoorMaterialization.IsMaterialized(e.Id))
            {
                var route = NpcColdMovement.Advance(world, e, board.SurfaceId, goal, board.ArrivalRadius, out var arrived);
                failed = !route;
                return route && arrived;
            }
            if (!e.TryGet<MovementIntentComponent>(out var intent)) { intent = new MovementIntentComponent(); e.AddComponent(intent); }
            var key = "chess:"+board.Id+(a ? ":a" : ":b");
            if (!intent.Active || intent.DestinationKey != key)
                intent.BeginWorldPoint(board.SurfaceId, goal.X, goal.Y, destinationKey: key, arrivalRadiusWorld: board.ArrivalRadius);
            failed = intent.HostArrived || intent.HostPathState == MovementHostPathState.PermanentFailure ||
                     intent.HostPathState == MovementHostPathState.RetryablePathUnavailable;
            return false;
        }

        static bool AtSeat(SimulationWorld world, EntityId actor, ChessBoardSpot board, bool a) =>
            CharacterWorldPresenceQuery.TryResolve(world, actor, out var at) && at.HasWorldPosition &&
            at.SurfaceId == board.SurfaceId && WorldVec2.Distance(at.WorldPosition, a ? board.SeatA : board.SeatB) <= board.ArrivalRadius;

        static void SetStatus(SimulationWorld world, Entity e, string status, bool playing)
        {
            if (e.TryGet<PersonalLifeComponent>(out var personal))
            { personal.Activity = playing ? PersonalLifeActivity.Recreational : PersonalLifeActivity.None; personal.Status = status; }
            if (world.Civilians.TryGet(e.Id, out var mortal))
            { mortal.Activity = MortalActivity.Chess; mortal.ScheduleStatus = status; }
        }

        static void EndSeat(SimulationWorld world, SimulationLoop loop, ChessGameState game, bool a)
        {
            var value = a ? game.PlayerA : game.PlayerB;
            if (value == 0) return;
            if (world.Entities.TryGet(new EntityId(value), out var e))
            {
                if (e.TryGet<MovementIntentComponent>(out var move) && move.DestinationKey.StartsWith("chess:", StringComparison.Ordinal)) move.Clear();
                loop.CancelOrdersFromSource(e.Id, OrderSource.Ai);
                if (e.TryGet<PersonalLifeComponent>(out var personal))
                { personal.Activity = PersonalLifeActivity.None; personal.Status = "下棋结束"; personal.LastRecreationTick = world.Tick.Value; }
                if (world.Civilians.TryGet(e.Id, out var mortal) && mortal.Activity == MortalActivity.Chess)
                { mortal.Activity = MortalActivity.Idle; mortal.ScheduleStatus = "自由活动"; }
            }
            if (a) { game.PlayerA = 0; game.PlayedTicks = 0; game.CooldownUntilTick = world.Tick.Value + 6; }
            else { game.PlayerB = 0; game.PlayedTicksB = 0; game.CooldownUntilTickB = world.Tick.Value + 6; }
            if (game.PlayerA == 0 && game.PlayerB == 0) game.Playing = false;
        }
    }
}
