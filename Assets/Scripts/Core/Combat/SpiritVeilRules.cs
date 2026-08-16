using XianXia.Core.Cultivation;

namespace XianXia.Core.Combat
{
    /// <summary>
    /// 斗气纱衣规则：固定绝对值开销／射程（非比例）。
    /// 筑基激活约等于当时典型满灵力的 1/3；后期境界再加行时占比自然变小。
    /// </summary>
    public static class SpiritVeilRules
    {
        public const string DisplayName = "斗气纱衣";
        public const RealmStage MinimumRealm = RealmStage.Foundation;

        /// <summary>未开纱衣时的普攻交战距离（与 Host 近战一致）。</summary>
        public const float MeleeEngageRange = 1.85f;

        /// <summary>筑基开纱衣后的普攻半径。</summary>
        public const float FoundationRangedEngageRange = 7f;

        /// <summary>
        /// 筑基召唤固定灵力开销（约满灵力 180 的 1/3）。
        /// 必须留下至少 1 点灵力，否则召唤后立刻因「打空」卸下。
        /// </summary>
        public const int FoundationActivateSpiritCost = 60;

        public static bool CanUseRealm(RealmStage realm) => realm >= MinimumRealm;

        public static int ActivateSpiritCost(RealmStage realm)
        {
            if (realm >= RealmStage.Foundation)
                return FoundationActivateSpiritCost;
            return 0;
        }

        public static float RangedEngageRange(RealmStage realm)
        {
            if (realm >= RealmStage.Foundation)
                return FoundationRangedEngageRange;
            return MeleeEngageRange;
        }
    }
}
