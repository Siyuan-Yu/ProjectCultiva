using System;

namespace XianXia.Core.World.Strategic
{
    /// <summary>战略层帮派显示名与地图色（第一刀硬编码；后续可迁 Content）。</summary>
    public static class StrategicFactionCatalog
    {
        public const string PlayerFactionId = "base:faction_player";
        public const string HuangcunLaborId = "base:sect_huangcun_labor";
        public const string FisherVillageId = "base:faction_fisher_village";
        public const string BanditId = "base:faction_bandits";

        public static string DisplayName(string factionId)
        {
            if (string.IsNullOrEmpty(factionId))
                return "无归属";
            if (string.Equals(factionId, PlayerFactionId, StringComparison.Ordinal))
                return "玩家";
            if (string.Equals(factionId, HuangcunLaborId, StringComparison.Ordinal))
                return "压迫宗门";
            if (string.Equals(factionId, FisherVillageId, StringComparison.Ordinal))
                return "渔村";
            if (string.Equals(factionId, BanditId, StringComparison.Ordinal))
                return "山匪";
            var slash = factionId.LastIndexOf(':');
            return slash >= 0 ? factionId.Substring(slash + 1) : factionId;
        }

        /// <summary>RGBA 0..1 for Host map tint.</summary>
        public static void MapTint(string factionId, out float r, out float g, out float b)
        {
            if (string.IsNullOrEmpty(factionId))
            {
                r = g = b = 0.55f;
                return;
            }

            if (string.Equals(factionId, PlayerFactionId, StringComparison.Ordinal))
            {
                r = 0.35f;
                g = 0.72f;
                b = 0.42f;
                return;
            }

            if (string.Equals(factionId, HuangcunLaborId, StringComparison.Ordinal))
            {
                r = 0.78f;
                g = 0.32f;
                b = 0.28f;
                return;
            }

            if (string.Equals(factionId, FisherVillageId, StringComparison.Ordinal))
            {
                r = 0.32f;
                g = 0.52f;
                b = 0.82f;
                return;
            }

            if (string.Equals(factionId, BanditId, StringComparison.Ordinal))
            {
                r = 0.62f;
                g = 0.28f;
                b = 0.62f;
                return;
            }

            r = 0.70f;
            g = 0.58f;
            b = 0.36f;
        }
    }
}
