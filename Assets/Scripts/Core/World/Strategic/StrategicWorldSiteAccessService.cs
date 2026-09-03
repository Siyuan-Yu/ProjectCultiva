using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Hex \u6218\u7565\uff1aWorldSite LocalMap \u51c6\u5165\uff08\u771f\u6e90 = WorldSite + FormalArmy \u8db3\u8ff9\uff09\u3002</summary>
    public static class StrategicWorldSiteAccessService
    {
        public static bool TryGetEnterableWorldSiteAtHex(
            SimulationWorld world,
            HexCoord hex,
            out WorldSite site)
        {
            site = null;
            if (world?.Strategic?.Sites == null ||
                !world.Strategic.Sites.TryGetAtHex(hex, out site) ||
                site == null)
                return false;

            if (string.IsNullOrWhiteSpace(site.LocalMapId))
                return false;

            return true;
        }

        public static bool IsSelfFormalArmy(SimulationWorld world, FormalArmy army)
        {
            if (world == null || army == null || string.IsNullOrEmpty(army.FactionId))
                return false;
            var playerFaction = world.Strategic?.PlayerFactionId;
            if (string.IsNullOrEmpty(playerFaction))
                return false;
            return string.Equals(army.FactionId, playerFaction, System.StringComparison.Ordinal);
        }

        public static bool IsFormalArmyAtSiteFootprint(FormalArmy army, WorldSite site)
        {
            if (army == null || site == null || !army.UsesHexStrategicPosition)
                return false;
            return site.OccupiesHex(army.CurrentHex);
        }

        /// <summary>
        /// 已经位于地点后的 LocalMap 打开资格。此 API 依赖 Party／FormalArmy 已在 Site，
        /// 绝不可用于跨 Surface transition 的目标地点预检。
        /// </summary>
        public static Result CanOpenWorldSiteLocalMapFromPresence(
            SimulationWorld world,
            string siteId,
            string formalArmyId)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld is null.");
            if (StrategicClockFreezeService.IsModalEncounter(world))
                return Result.Failure(ErrorCode.InvalidOperation, "\u9047\u9047\u4e2d\u9501\u5b9a\uff0c\u65e0\u6cd5\u8fdb\u5165\u5730\u70b9\u3002");
            if (string.IsNullOrEmpty(siteId))
                return Result.Failure(ErrorCode.InvalidArgument, "siteId required.");
            if (!world.Strategic.Sites.TryGet(siteId, out var site) || site == null)
                return Result.Failure(ErrorCode.NotFound, "WorldSite missing.", siteId);

            if (string.IsNullOrWhiteSpace(site.LocalMapId))
                return Result.Failure(
                    ErrorCode.InvalidOperation,
                    "WorldSite \u672a\u914d\u7f6e LocalMap\uff0c\u65e0\u6cd5\u8fdb\u5165\u3002",
                    siteId);

            if (string.IsNullOrEmpty(formalArmyId))
            {
                if (!StrategicSiteAccessService.HasPartyMemberAtSite(world, siteId))
                {
                    return Result.Failure(
                        ErrorCode.InvalidOperation,
                        "\u65e0\u5df1\u65b9\u89d2\u8272\u5728\u6b64\u5730\u70b9\uff0c\u65e0\u6cd5\u8fdb\u5165\u573a\u666f\u3002");
                }

                return Result.Success();
            }

            if (!world.Strategic.FormalArmies.TryGet(formalArmyId, out var army) ||
                army == null)
                return Result.Failure(ErrorCode.InvalidOperation, "\u8bf7\u5148\u5de6\u952e\u9009\u4e2d\u6211\u65b9\u519b\u56e2\u3002");

            if (!IsSelfFormalArmy(world, army))
                return Result.Failure(ErrorCode.InvalidOperation, "\u4ec5\u6211\u65b9\u519b\u56e2\u53ef\u8fdb\u5165\u5730\u70b9\u3002");

            if (army.State == FormalArmyState.Moving)
                return Result.Failure(ErrorCode.InvalidOperation, "\u519b\u56e2\u79fb\u52a8\u4e2d\uff0c\u65e0\u6cd5\u8fdb\u5165\u5730\u70b9\u3002");

            if (!IsFormalArmyAtSiteFootprint(army, site))
                return Result.Failure(ErrorCode.InvalidOperation, "\u519b\u56e2\u4e0d\u5728\u8be5\u5730\u70b9\uff0c\u65e0\u6cd5\u8fdb\u5165\u3002");

            return Result.Success();
        }

        /// <summary>兼容旧调用名；语义仍是 already-present scene access，不是 transition admission。</summary>
        public static Result CanEnterWorldSiteLocalMap(
            SimulationWorld world,
            string siteId,
            string formalArmyId) =>
            CanOpenWorldSiteLocalMapFromPresence(world, siteId, formalArmyId);

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

        public static string BuildEnterSiteMenuLabel(WorldSite site)
        {
            if (site == null)
                return "\u8fdb\u5165\u5730\u70b9";
            var name = string.IsNullOrEmpty(site.DisplayName) ? site.SiteId : site.DisplayName;
            return "\u8fdb\u5165" + name;
        }
    }
}
