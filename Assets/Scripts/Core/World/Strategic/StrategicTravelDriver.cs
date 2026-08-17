using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Travel tick 后的战略层推进。
    /// 接战弹窗优先于到站提示；追击抵达走 BattleOffer，普通最终到站走 ArrivalNotice。
    /// </summary>
    public static class StrategicTravelDriver
    {
        static readonly List<EntityId> ArrivedScratch = new List<EntityId>(16);

        public static void AfterTravelTick(SimulationWorld world, int ticks = 1)
        {
            if (world?.Strategic == null || ticks < 1)
                return;

            ArmyStackService.AdvanceAll(world, ticks);
            StrategicFollowService.AfterTravelTick(world);

            // 接战优先：即使已有到站弹窗也允许追击接战盖过去
            StrategicPursuitService.AfterTravelTick(world);
            if (world.Strategic.HasBattleOffer)
            {
                world.Strategic.ClearArrivalNotice();
                ArrivedScratch.Clear();
                return;
            }

            if (!world.Strategic.HasArrivalNotice)
                ArrivalNoticeService.AfterTravelTick(world, ArrivedScratch);

            ArrivedScratch.Clear();
        }

        /// <summary>由 SimulationLoop 在 AdvanceTravel 前清空、AdvanceTravel 写入到站名单。</summary>
        public static List<EntityId> BeginArrivalCapture()
        {
            ArrivedScratch.Clear();
            return ArrivedScratch;
        }
    }
}
