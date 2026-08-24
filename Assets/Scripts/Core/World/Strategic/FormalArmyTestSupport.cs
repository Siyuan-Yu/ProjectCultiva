using System.Collections.Generic;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>EditMode ??????? FormalArmy ?? Hex ????? Runtime ???????</summary>
    public static class FormalArmyTestSupport
    {
        public static void AnchorOnHex(FormalArmy army, HexCoord hex, bool garrisoned = false)
        {
            if (army == null)
                return;
            ArmyHexTravelService.InitializeArmyAtHex(army, hex, garrisoned);
        }

        public static void SetHexMidTravel(
            SimulationWorld world,
            FormalArmy army,
            HexCoord from,
            HexCoord to,
            float stepProgress)
        {
            if (army == null)
                return;

            army.UsesHexStrategicPosition = true;
            army.CurrentHex = from;
            var path = new List<HexCoord>(8);
            if (world?.HexWorld != null && world.HexWorld.HasGrid &&
                HexPathfinder.TryFindPath(world.HexWorld, from, to, path) &&
                path.Count >= 2)
            {
                army.SetHexPath(path, to);
            }
            else
            {
                path.Add(from);
                path.Add(to);
                army.SetHexPath(path, to);
            }

            var t = System.Math.Max(0f, System.Math.Min(0.99f, stepProgress));
            army.StepProgress = t;
            army.StepRemainingTicks = System.Math.Max(1, (int)((1f - t) * army.StepTotalTicks));
        }

        public static void ScaleHexStepTicks(FormalArmy army, int divisor)
        {
            if (army == null || divisor < 1)
                return;

            var scaled = System.Math.Max(2, army.StepTotalTicks / divisor);
            army.StepTotalTicks = scaled;
            army.StepRemainingTicks = System.Math.Min(army.StepRemainingTicks, scaled);
        }
    }
}
