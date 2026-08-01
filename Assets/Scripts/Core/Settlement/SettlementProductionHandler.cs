using XianXia.Core.Cultivation;
using XianXia.Core.Entities;
using XianXia.Core.Events;
using XianXia.Core.Simulation;

namespace XianXia.Core.Settlement
{
    /// <summary>
    /// Day-end production／cultivate bonus from work roles + facilities.
    /// </summary>
    public sealed class SettlementProductionHandler : IDayBoundaryHandler
    {
        /// <summary>Fallback Progress when Cultivate role has no facility bonus.</summary>
        public const int DefaultCultivateProgressPerDay = 3;

        public void OnDayStarted(SimulationWorld world, ulong startedDayIndex)
        {
        }

        public void OnDayEnded(SimulationWorld world, ulong endedDayIndex)
        {
            if (world == null || world.Settlements.All.Count == 0)
                return;

            foreach (var kv in world.Settlements.All)
            {
                var settlement = kv.Value;
                var laborWorkers = 0;
                var gatherWorkers = 0;
                var cultivateWorkers = 0;

                foreach (var entity in world.Entities.All)
                {
                    if (!entity.TryGet<WorkAssignmentComponent>(out var work) || !work.IsAssigned)
                        continue;
                    if (!string.Equals(work.SettlementId, settlement.Id, System.StringComparison.Ordinal))
                        continue;

                    switch (work.Role)
                    {
                        case WorkRoleKind.Labor:
                            laborWorkers++;
                            break;
                        case WorkRoleKind.Gather:
                            gatherWorkers++;
                            break;
                        case WorkRoleKind.Cultivate:
                            cultivateWorkers++;
                            ApplyCultivateProgress(entity, settlement);
                            break;
                    }
                }

                ApplyRoleProduction(world, settlement, WorkRoleKind.Labor, laborWorkers);
                ApplyRoleProduction(world, settlement, WorkRoleKind.Gather, gatherWorkers);

                if (laborWorkers + gatherWorkers + cultivateWorkers > 0)
                {
                    world.Events.Publish(
                        EventType.SettlementProductionResolved,
                        world.Tick,
                        payload: settlement.Id +
                                 ";day=" + endedDayIndex +
                                 ";labor=" + laborWorkers +
                                 ";gather=" + gatherWorkers +
                                 ";cultivate=" + cultivateWorkers);
                }
            }
        }

        static void ApplyRoleProduction(
            SimulationWorld world,
            SettlementState settlement,
            WorkRoleKind role,
            int workers)
        {
            if (workers <= 0 || settlement.Facilities.Count == 0)
                return;

            foreach (var facility in settlement.Facilities)
            {
                string resourceId = null;
                var per = 0;
                if (role == WorkRoleKind.Labor)
                {
                    resourceId = facility.LaborResourceId;
                    per = facility.LaborAmountPerWorker;
                }
                else if (role == WorkRoleKind.Gather)
                {
                    resourceId = facility.GatherResourceId;
                    per = facility.GatherAmountPerWorker;
                }

                if (string.IsNullOrEmpty(resourceId) || per <= 0)
                    continue;

                var gained = per * workers;
                settlement.AddStock(resourceId, gained);
                world.Events.Publish(
                    EventType.SettlementStockChanged,
                    world.Tick,
                    payload: settlement.Id + ":" + resourceId + ":+" + gained);
            }
        }

        static void ApplyCultivateProgress(Entity entity, SettlementState settlement)
        {
            if (!entity.TryGet<CultivationComponent>(out var cultivation))
                return;

            var bonus = DefaultCultivateProgressPerDay;
            foreach (var facility in settlement.Facilities)
            {
                if (facility.CultivateProgressBonusPerWorker > bonus)
                    bonus = facility.CultivateProgressBonusPerWorker;
            }

            cultivation.Progress += bonus;
        }
    }
}
