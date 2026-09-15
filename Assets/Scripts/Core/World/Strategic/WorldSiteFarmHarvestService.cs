using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.Social;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Commits one real NPC farm harvest to the Site that manages the cell at commit time.</summary>
    public static class WorldSiteFarmHarvestService
    {
        public static Result<int> TryDepositNpcHarvest(
            SimulationWorld world,
            EntityId workerId,
            string stableCellId,
            string resourceId,
            int amount = 1)
        {
            if (world == null || workerId.IsNone || string.IsNullOrWhiteSpace(stableCellId) ||
                string.IsNullOrWhiteSpace(resourceId) || amount <= 0 ||
                !world.Entities.TryGet(workerId, out var worker) ||
                (worker.Tags & EntityTag.Npc) == 0 ||
                !worker.TryGet<LifecycleComponent>(out var lifecycle) || lifecycle.State != LifecycleState.Alive ||
                !worker.TryGet<FactionMembershipComponent>(out var membership) ||
                !membership.IsAffiliated)
                return Result.Fail<int>(ErrorCode.InvalidArgument, "NPC farm harvest requires a real affiliated worker and cell.");

            var authorization = WorldAdministrativeAssetAuthorizationService.ResolveForFaction(
                world, stableCellId, membership.FactionId);
            if (!authorization.IsAllowed || authorization.ManagingSite == null)
                return Result.Fail<int>(ErrorCode.InvalidOperation, "NPC worker is no longer authorized for this farm cell.");

            return WorldSitePublicStockService.TryAdd(
                world, authorization.ManagingSite.SiteId, resourceId, amount, workerId);
        }
    }
}
