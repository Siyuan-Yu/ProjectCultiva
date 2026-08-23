using System.Collections.Generic;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// 玩家战略命令层（INPUT CONTRACT 保留，MOVEMENT BACKEND = Hex）。
    /// Move / Attack / AttackSite 均不得调用 Route travel。
    /// </summary>
    public static class ArmyHexCommandService
    {
        static readonly List<HexCoord> PathScratch = new List<HexCoord>(64);

        public static bool IsHexStrategicActive(SimulationWorld world) =>
            HexStrategicRuntime.IsActive(world);

        public static void EnsureArmyOnHex(SimulationWorld world, FormalArmy army)
        {
            if (world == null || army == null || army.UsesHexStrategicPosition)
                return;

            if (ArmyHexMigrationHelper.TryResolveHexForArmy(world, army, out var hex))
            {
                ArmyHexTravelService.InitializeArmyAtHex(army, hex);
                return;
            }

            if (world.HexWorld.Contains(new HexCoord(0, 0)))
                ArmyHexTravelService.InitializeArmyAtHex(army, new HexCoord(0, 0));
        }

        public static Result MoveArmy(SimulationWorld world, string armyId, HexCoord destination)
        {
            if (!IsHexStrategicActive(world))
                return Result.Failure(ErrorCode.InvalidOperation, "Hex strategic map is not active.");
            if (!world.Strategic.FormalArmies.TryGet(armyId, out var army) || army == null)
                return Result.Failure(ErrorCode.NotFound, "Army not found.", armyId);

            ArmyHexPursuitService.CancelPursuitForAttacker(world, armyId);
            EnsureArmyOnHex(world, army);
            return ArmyHexTravelService.MoveArmyToHex(world, armyId, destination);
        }

        public static Result MoveArmyToSite(SimulationWorld world, string armyId, string siteId)
        {
            if (!world.Strategic.Sites.TryResolveSiteHex(siteId, out var hex))
                return Result.Failure(ErrorCode.NotFound, "Strategic site not found.", siteId);
            return MoveArmy(world, armyId, hex);
        }

        public static Result AttackArmy(SimulationWorld world, string attackerArmyId, string targetArmyId) =>
            ArmyHexPursuitService.BeginAttackArmy(world, attackerArmyId, targetArmyId);

        public static Result AttackStack(SimulationWorld world, string attackerArmyId, ArmyStack stack) =>
            ArmyHexPursuitService.BeginAttackStack(world, attackerArmyId, stack);

        public static bool TryBuildPathPreview(
            SimulationWorld world,
            FormalArmy army,
            HexCoord destination,
            List<HexCoord> pathOut)
        {
            pathOut?.Clear();
            if (!IsHexStrategicActive(world) || army == null || pathOut == null)
                return false;
            EnsureArmyOnHex(world, army);
            return HexPathfinder.TryFindPath(world.HexWorld, army.CurrentHex, destination, pathOut);
        }

        public static bool TryResolveDestinationHex(
            SimulationWorld world,
            WorldTravelTarget target,
            out HexCoord destination,
            out string label)
        {
            destination = default;
            label = string.Empty;
            if (world == null)
                return false;

            if (target.IsHex)
            {
                destination = target.HexCoord;
                label = target.Describe(world.WorldGraph, world);
                return world.HexWorld.Contains(destination);
            }

            if (!string.IsNullOrEmpty(target.NodeId))
            {
                foreach (var kv in world.Strategic.Sites.Sites)
                {
                    var site = kv.Value;
                    if (site == null ||
                        !string.Equals(site.LegacyNodeId, target.NodeId, System.StringComparison.Ordinal))
                        continue;
                    destination = site.HexCoord;
                    label = site.DisplayName;
                    return true;
                }

                if (world.WorldGraph.TryGetNode(target.NodeId, out var node) &&
                    node != null &&
                    node.HasHexCoord)
                {
                    destination = new HexCoord(node.HexQ, node.HexR);
                    label = string.IsNullOrEmpty(node.Name) ? node.Id : node.Name;
                    return world.HexWorld.Contains(destination);
                }
            }

            return false;
        }
    }
}
