using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Opportunity;
using XianXia.Core.Simulation;

namespace XianXia.Core.Exploration
{
    /// <summary>需勘查才显形的洞口／机缘入口（有 enterLocalMap + opportunitySite）。</summary>
    public static class OpportunityEntranceRules
    {
        /// <summary>勘查半径＝神识 × 此倍率。</summary>
        public const float SurveyRadiusMultiplier = 2f;

        /// <summary>洞口足迹近似半宽；距离按「到中心 − padding」算，避免必须踩中心点。</summary>
        public const float EntranceHitPadding = 2.5f;

        /// <summary>走近洞府 toast 至少覆盖此半径；实际与角色勘查半径取较大。</summary>
        public const float DefaultHintRadius = 14f;

        public static float SurveyRadius(int spiritSense) =>
            System.Math.Max(0, spiritSense) * SurveyRadiusMultiplier;

        /// <summary>表现坐标到洞口是否在勘查半径内（含足迹 padding）。</summary>
        public static bool IsWithinSurveyRange(
            float centerX,
            float centerZ,
            float radius,
            WorldLocationState entrance)
        {
            if (entrance == null || radius < 0f)
                return false;
            var dx = entrance.PresentationX - centerX;
            var dz = entrance.PresentationZ - centerZ;
            var dist = (float)System.Math.Sqrt(dx * dx + dz * dz) - EntranceHitPadding;
            if (dist < 0f)
                dist = 0f;
            return dist <= radius;
        }

        /// <summary>
        /// 可选神识门槛（默认 0＝不额外卡）。半径仍＝神识×倍率；门槛只拦「够得着却看不穿」的秘洞。
        /// </summary>
        public static bool MeetsSenseRequirement(WorldLocationState entrance, int spiritSense)
        {
            if (entrance == null)
                return false;
            var need = entrance.SurveySenseRequired;
            if (need <= 0)
                return true;
            return spiritSense >= need;
        }

        public static bool IsHiddenEntrance(WorldLocationState loc) =>
            loc != null &&
            !string.IsNullOrEmpty(loc.EnterLocalMapId) &&
            !string.IsNullOrEmpty(loc.OpportunitySiteId);

        public static bool IsRevealed(SimulationWorld world, WorldLocationState loc)
        {
            if (world == null || loc == null || string.IsNullOrEmpty(loc.OpportunitySiteId))
                return true;
            if (!DefinitionId.TryParse(loc.OpportunitySiteId, out var siteId))
                return false;
            foreach (var e in world.Entities.All)
            {
                if (e == null)
                    continue;
                if ((e.Tags & EntityTag.Character) == 0)
                    continue;
                if (!e.TryGet<KnownSitesComponent>(out var known))
                    continue;
                if (known.Knows(siteId))
                    return true;
            }

            return false;
        }
    }
}
