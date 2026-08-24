using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Hex 战略：WorldSite LocalMap 准入（真源 = WorldSite + FormalArmy 足迹）。</summary>
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

        public static Result CanEnterWorldSiteLocalMap(
            SimulationWorld world,
            string siteId,
            string formalArmyId)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld is null.");
            if (StrategicClockFreezeService.IsModalEncounter(world))
                return Result.Failure(ErrorCode.InvalidOperation, "遭遇中锁定，无法进入地点。");
            if (string.IsNullOrEmpty(siteId))
                return Result.Failure(ErrorCode.InvalidArgument, "siteId required.");
            if (!world.Strategic.Sites.TryGet(siteId, out var site) || site == null)
                return Result.Failure(ErrorCode.NotFound, "WorldSite missing.", siteId);

            if (string.IsNullOrWhiteSpace(site.LocalMapId))
                return Result.Failure(
                    ErrorCode.InvalidOperation,
                    "WorldSite 未配置 LocalMap，无法进入。",
                    siteId);

            // 开局 / 无选中军团：队伍已在 Site 即可进入（Playable bootstrap）。
            if (string.IsNullOrEmpty(formalArmyId))
            {
                if (!StrategicNodeAccessService.HasPartyMemberAtSite(world, siteId))
                {
                    return Result.Failure(
                        ErrorCode.InvalidOperation,
                        "无己方角色在此地点，无法进入场景。");
                }

                return Result.Success();
            }

            if (!world.Strategic.FormalArmies.TryGet(formalArmyId, out var army) ||
                army == null)
                return Result.Failure(ErrorCode.InvalidOperation, "请先左键选中我方军团。");

            if (!IsSelfFormalArmy(world, army))
                return Result.Failure(ErrorCode.InvalidOperation, "仅我方军团可进入地点。");

            if (army.State == FormalArmyState.Moving)
                return Result.Failure(ErrorCode.InvalidOperation, "军团移动中，无法进入地点。");

            if (!IsFormalArmyAtSiteFootprint(army, site))
                return Result.Failure(ErrorCode.InvalidOperation, "军团不在该地点，无法进入。");

            return Result.Success();
        }

        public static string BuildEnterSiteMenuLabel(WorldSite site)
        {
            if (site == null)
                return "进入地点";
            var name = string.IsNullOrEmpty(site.DisplayName) ? site.SiteId : site.DisplayName;
            return "进入" + name;
        }
    }

}
