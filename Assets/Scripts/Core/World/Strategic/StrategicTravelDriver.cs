using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Travel tick 后的战略层推进。
    /// 接战弹窗优先于到站提示；追击抵达／探望弥留到站走 BattleOffer，普通最终到站走 ArrivalNotice。
    /// </summary>
    public static class StrategicTravelDriver
    {
        static readonly List<EntityId> ArrivedScratch = new List<EntityId>(16);
        static readonly List<EntityId> RosterScratch = new List<EntityId>(16);

        public static void AfterTravelTick(SimulationWorld world, int ticks = 1)
        {
            if (world?.Strategic == null || ticks < 1)
                return;

            if (!world.HexWorld.HasGrid)
                return;

            ArmyHexTravelService.AdvanceAll(world, ticks);
            PlayerPartyHexTravelService.AdvanceAll(world, ticks);
            BackgroundSimulationScheduler.AfterSimulationTick(world, ticks);
            ArmyStackAdapter.SyncAllLinkedStacksFromFormalArmies(world);
            ArmyHexPursuitService.AfterTravelTick(world);
            if (!world.Strategic.HasBattleOffer)
                ArmyHexLingeringArrivalService.AfterTravelTick(world);
            if (!world.Strategic.HasBattleOffer)
                TryResolvePendingLingeringVisit(world);
        }

        static void TryResolvePendingLingeringVisit(SimulationWorld world)
        {
            if (world.Strategic.PendingLingeringVisitIncapId == 0)
                return;
            CollectPlayerRoster(world, RosterScratch);
            BattleOfferService.TryResolvePendingLingeringVisitOffer(world, RosterScratch);
            RosterScratch.Clear();
        }

        static void CollectPlayerRoster(SimulationWorld world, List<EntityId> into)
        {
            into.Clear();
            if (world?.WorldPresence?.All == null)
                return;
            foreach (var kv in world.WorldPresence.All)
            {
                var id = new EntityId(kv.Key);
                if (id.IsNone || !world.Entities.TryGet(id, out var ent) || ent == null)
                    continue;
                if ((ent.Tags & EntityTag.Npc) != 0)
                    continue;
                into.Add(id);
            }
        }

        /// <summary>由 SimulationLoop 在 AdvanceTravel 前清空、AdvanceTravel 写入到站名单。</summary>
        public static List<EntityId> BeginArrivalCapture()
        {
            ArrivedScratch.Clear();
            return ArrivedScratch;
        }
    }
}
