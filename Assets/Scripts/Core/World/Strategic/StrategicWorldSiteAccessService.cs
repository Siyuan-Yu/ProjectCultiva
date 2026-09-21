using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Legacy WorldSite LocalMap compatibility admission.</summary>
    public static class StrategicWorldSiteAccessService
    {
        public static Result CanEnterWorldSiteLocalMap(
            SimulationWorld world, string siteId)
        {
            if (world == null) return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld is null.");
            if (StrategicClockFreezeService.IsModalEncounter(world))
                return Result.Failure(ErrorCode.InvalidOperation, "遭遇中锁定，无法进入地点。");
            if (string.IsNullOrEmpty(siteId))
                return Result.Failure(ErrorCode.InvalidArgument, "siteId required.");
            if (!world.Strategic.Sites.TryGet(siteId, out var site) || site == null)
                return Result.Failure(ErrorCode.NotFound, "WorldSite missing.", siteId);
            if (string.IsNullOrWhiteSpace(site.LocalMapId))
                return Result.Failure(ErrorCode.InvalidOperation, "WorldSite 未配置 LocalMap，无法进入。", siteId);
            var party = world.Strategic.PlayerPartyContext;
            if (party == null || !StrategicWorldSitePopulationService.HasFriendlyCharacterPresentAtWorldSite(
                    world, party.Members, site))
                return Result.Failure(ErrorCode.InvalidOperation, "无己方角色在此地点，无法进入场景。");
            return Result.Success();
        }

        /// <summary>
        /// PlayerParty 从相邻 Surface 进入目标 WorldSite 的无副作用准入检查。
        /// 进入动作本身才会创建 Party-at-Site presence，因此这里绝不检查该 presence。
        /// </summary>
        public static Result CanTransitionPlayerPartyIntoWorldSite(
            SimulationWorld world,
            string siteId)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld is null.");
            if (StrategicClockFreezeService.IsModalEncounter(world))
                return Result.Failure(ErrorCode.InvalidOperation, "遭遇中锁定，无法跨越地点边界。");
            if (string.IsNullOrEmpty(siteId))
                return Result.Failure(ErrorCode.InvalidArgument, "siteId required.");
            if (world.Strategic?.Sites == null ||
                !world.Strategic.Sites.TryGet(siteId, out var site) || site == null)
                return Result.Failure(ErrorCode.NotFound, "WorldSite missing.", siteId);
            if (string.IsNullOrWhiteSpace(WorldTravelService.ResolveWorldSiteLocalMapId(site)))
                return Result.Failure(
                    ErrorCode.InvalidOperation,
                    "WorldSite 未配置 LocalMap，无法跨越进入。",
                    siteId);
            return Result.Success();
        }

    }
}
