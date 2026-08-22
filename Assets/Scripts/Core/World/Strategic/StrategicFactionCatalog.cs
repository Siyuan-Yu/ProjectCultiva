using System;

namespace XianXia.Core.World.Strategic
{
    /// <summary>战略层帮派显示名与地图色（第一刀硬编码；后续可迁 Content）。</summary>
    public static class StrategicFactionCatalog
    {
        public const string PlayerFactionId = "base:faction_player";
        public const string HuangcunLaborId = "base:sect_huangcun_labor";
        /// <summary>海角三角：渔村／水寨／海角。</summary>
        public const string FisherVillageId = "base:faction_fisher_village";
        /// <summary>无领土游荡敌对（Prototype 山匪）。</summary>
        public const string BanditId = "base:faction_bandits";
        /// <summary>南村／庄院。</summary>
        public const string NanYanLeagueId = "base:faction_nan_yan";
        /// <summary>北村／山口。</summary>
        public const string ShuoFengFortId = "base:faction_shuofeng";
        /// <summary>东林／山神庙／古道驿。</summary>
        public const string DongLinGuildId = "base:faction_donglin";
        /// <summary>西渡／药田谷。</summary>
        public const string XiJinGuildId = "base:faction_xijin";

        /// <summary>Ch01 区域势力（不含主角团／压迫宗门／山匪）。</summary>
        public static readonly string[] Ch01RegionalFactionIds =
        {
            FisherVillageId,
            NanYanLeagueId,
            ShuoFengFortId,
            DongLinGuildId,
            XiJinGuildId
        };

        public static string DisplayName(string factionId)
        {
            if (string.IsNullOrEmpty(factionId))
                return "无归属";
            if (string.Equals(factionId, PlayerFactionId, StringComparison.Ordinal))
                return "主角团";
            if (string.Equals(factionId, HuangcunLaborId, StringComparison.Ordinal))
                return "压迫宗门";
            if (string.Equals(factionId, FisherVillageId, StringComparison.Ordinal))
                return "沧澜渔盟";
            if (string.Equals(factionId, NanYanLeagueId, StringComparison.Ordinal))
                return "南堰庄盟";
            if (string.Equals(factionId, ShuoFengFortId, StringComparison.Ordinal))
                return "朔风堡";
            if (string.Equals(factionId, DongLinGuildId, StringComparison.Ordinal))
                return "东林海会";
            if (string.Equals(factionId, XiJinGuildId, StringComparison.Ordinal))
                return "西津渡帮";
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

            if (string.Equals(factionId, NanYanLeagueId, StringComparison.Ordinal))
            {
                r = 0.82f;
                g = 0.62f;
                b = 0.28f;
                return;
            }

            if (string.Equals(factionId, ShuoFengFortId, StringComparison.Ordinal))
            {
                r = 0.55f;
                g = 0.68f;
                b = 0.78f;
                return;
            }

            if (string.Equals(factionId, DongLinGuildId, StringComparison.Ordinal))
            {
                r = 0.28f;
                g = 0.62f;
                b = 0.38f;
                return;
            }

            if (string.Equals(factionId, XiJinGuildId, StringComparison.Ordinal))
            {
                r = 0.58f;
                g = 0.45f;
                b = 0.72f;
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
