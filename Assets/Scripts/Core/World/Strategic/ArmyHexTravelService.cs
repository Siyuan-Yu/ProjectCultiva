using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>FormalArmy Hex 战略移动入口（Phase 3 委托连续旅行服务）。</summary>
    public static class ArmyHexTravelService
    {
        public static Result MoveArmyToHex(SimulationWorld world, string armyId, HexCoord destination) =>
            FormalArmyContinuousTravelService.MoveArmyToHex(world, armyId, destination);

        public static Result MoveArmyToSite(SimulationWorld world, string armyId, string siteId) =>
            FormalArmyContinuousTravelService.MoveArmyToWorldSite(world, armyId, siteId);

        public static void AdvanceHexTravel(SimulationWorld world, FormalArmy army, int ticks)
        {
            if (world == null || army == null || ticks < 1)
                return;
            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            FormalArmyContinuousTravelService.AdvanceDistanceBudget(
                world,
                army,
                PlayerPartyHexTravelService.WorldUnitsPerTick(hexSize) * ticks);
        }

        public static void AdvanceAll(SimulationWorld world, int ticks) =>
            FormalArmyContinuousTravelService.AdvanceAll(world, ticks);

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
            if (world == null || army == null)
                return;

            if (world.Strategic.Sites.TryGetAtHex(hex, out var site) && site != null)
            {
                FormalArmyContinuousTravelService.InitializeAtWorldSite(world, army, site.SiteId);
                if (garrisoned)
                    army.State = FormalArmyState.Garrisoned;
                return;
            }

            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            HexMath.ToWorldPosition(hex, hexSize, out var x, out var y);
            army.UsesHexStrategicPosition = true;
            army.WorldMotion.SetAtWorldPosition(new WorldVec2(x, y), hex);
            army.SyncLegacyFromWorldMotion();
            army.State = garrisoned ? FormalArmyState.Garrisoned : FormalArmyState.Idle;
            SyncMemberPresenceToArmyHex(world, army);
        }

        public static void SyncMemberPresenceToArmyHex(SimulationWorld world, FormalArmy army) =>
            FormalArmyMemberPresenceSync.SyncAll(world, army);
    }
}
