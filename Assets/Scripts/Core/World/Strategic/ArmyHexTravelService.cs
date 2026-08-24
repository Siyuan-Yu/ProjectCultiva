using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>FormalArmy Hex 战略移动（替代 Route 模型的正式路径）。</summary>
    public static class ArmyHexTravelService
    {
        static readonly List<HexCoord> PathScratch = new List<HexCoord>(64);

        public static Result MoveArmyToHex(SimulationWorld world, string armyId, HexCoord destination)
        {
            if (world == null || string.IsNullOrWhiteSpace(armyId))
                return Result.Failure(ErrorCode.InvalidArgument, "Invalid army move order.");
            if (!world.Strategic.FormalArmies.TryGet(armyId, out var army) || army == null)
                return Result.Failure(ErrorCode.NotFound, "Army not found.", armyId);
            if (!world.HexWorld.HasGrid)
                return Result.Failure(ErrorCode.InvalidOperation, "Hex grid not loaded.");
            if (!world.HexWorld.TryGetTile(destination, out var destTile) || destTile == null || !destTile.IsPassable)
                return Result.Failure(ErrorCode.InvalidArgument, "Destination hex is not passable.");

            if (army.CurrentHex == destination && army.State != FormalArmyState.Moving)
                return Result.Failure(ErrorCode.InvalidArgument, "Already at destination hex.");

            if (!HexPathfinder.TryFindPath(world.HexWorld, army.CurrentHex, destination, PathScratch) ||
                PathScratch.Count < 1)
                return Result.Failure(ErrorCode.InvalidOperation, "No hex path to destination.");

            army.SetHexPath(PathScratch, destination);
            return Result.Success();
        }

        public static Result MoveArmyToSite(SimulationWorld world, string armyId, string siteId)
        {
            if (!world.Strategic.Sites.TryResolveSiteHex(siteId, out var hex))
                return Result.Failure(ErrorCode.NotFound, "Strategic site not found.", siteId);
            return MoveArmyToHex(world, armyId, hex);
        }

        public static void AdvanceHexTravel(SimulationWorld world, FormalArmy army, int ticks)
        {
            if (world == null || army == null || ticks < 1 || !army.UsesHexStrategicPosition)
                return;
            if (army.State != FormalArmyState.Moving)
                return;

            for (var i = 0; i < ticks; i++)
            {
                if (!AdvanceOneTick(world, army))
                    break;
            }
        }

        public static void AdvanceAll(SimulationWorld world, int ticks)
        {
            if (world?.Strategic?.FormalArmies == null || ticks < 1)
                return;

            foreach (var kv in world.Strategic.FormalArmies.Armies)
            {
                var army = kv.Value;
                if (army == null || !army.UsesHexStrategicPosition)
                    continue;
                AdvanceHexTravel(world, army, ticks);
            }
        }

        static bool AdvanceOneTick(SimulationWorld world, FormalArmy army)
        {
            if (!army.TryGetActiveStepHexes(out var from, out var to))
            {
                army.CompleteHexMove();
                return false;
            }

            if (!world.HexWorld.TryGetTile(to, out var tile) || tile == null)
            {
                army.CompleteHexMove();
                return false;
            }

            var stepTicks = ComputeStepTicks(tile);
            if (stepTicks < 1)
                stepTicks = 1;

            army.StepTotalTicks = stepTicks;
            army.StepRemainingTicks = Math.Max(0, army.StepRemainingTicks - 1);
            army.StepProgress = army.StepTotalTicks <= 0
                ? 1f
                : 1f - (float)army.StepRemainingTicks / army.StepTotalTicks;

            if (army.StepRemainingTicks > 0)
                return true;

            army.CurrentHex = to;
            army.StepProgress = 0f;
            army.StepRemainingTicks = 0;
            army.StepTotalTicks = 0;
            army.CurrentPathIndex++;

            if (army.CurrentPathIndex >= army.HexPathCount)
            {
                army.CompleteHexMove();
                return false;
            }

            if (!army.TryGetActiveStepHexes(out _, out var nextTo) ||
                !world.HexWorld.TryGetTile(nextTo, out var nextTile) || nextTile == null)
            {
                army.CompleteHexMove();
                return false;
            }

            army.StepTotalTicks = ComputeStepTicks(nextTile);
            army.StepRemainingTicks = army.StepTotalTicks;
            army.StepProgress = 0f;
            return true;
        }

        static int ComputeStepTicks(HexCell tile)
        {
            var cost = tile?.ResolveMovementCost() ?? 1f;
            return Math.Max(4, (int)Math.Round(cost * 8f));
        }

        public static void InitializeArmyAtHex(FormalArmy army, HexCoord hex, bool garrisoned = false)
        {
            if (army == null)
                return;
            army.UsesHexStrategicPosition = true;
            army.CurrentHex = hex;
            army.DestinationHex = hex;
            army.ClearHexPath();
            army.State = garrisoned ? FormalArmyState.Garrisoned : FormalArmyState.Idle;
        }

        public static void InitializeArmyAtHex(
            SimulationWorld world,
            FormalArmy army,
            HexCoord hex,
            bool garrisoned = false)
        {
            InitializeArmyAtHex(army, hex, garrisoned);
            SyncMemberPresenceToArmyHex(world, army);
        }

        /// <summary>
        /// Hex 战略：Army 成员 WorldPresence 与 FormalArmy.CurrentHex 对齐（禁止仍留 AtSite 他处）。
        /// </summary>
        public static void SyncMemberPresenceToArmyHex(SimulationWorld world, FormalArmy army)
        {
            if (world?.WorldPresence == null || army == null || !army.UsesHexStrategicPosition)
                return;

            for (var i = 0; i < army.MemberCharacterIds.Count; i++)
            {
                var memberId = new EntityId(army.MemberCharacterIds[i]);
                if (memberId.IsNone)
                    continue;
                world.WorldPresence.SetAtHex(memberId, army.CurrentHex);
            }
        }
    }
}
