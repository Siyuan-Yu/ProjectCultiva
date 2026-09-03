using System;
using System.Collections.Generic;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// 战略层帮派展示元数据唯一查询入口。正式数据源 = Content（factions.json，经
    /// <see cref="Install"/> 安装）；Content 尚未 bootstrap 的纯 Core 场景回退到本文件
    /// 尾部 hardcoded 表。调用方（DisplayName / MapTint）无需感知数据来源差异。
    /// </summary>
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

        // ------------------------------------------------------------------
        // Installed Content（唯一 installed 表；fallback 不进入此表）
        // ------------------------------------------------------------------

        static readonly Dictionary<string, StrategicFactionPresentation> _installedById =
            new Dictionary<string, StrategicFactionPresentation>(StringComparer.Ordinal);
        static IReadOnlyList<StrategicFactionPresentation> _installedOrdered =
            Array.Empty<StrategicFactionPresentation>();

        /// <summary>是否已安装过 Content faction（空安装后回到 fallback 语义）。</summary>
        public static bool HasInstalledContent => _installedById.Count > 0;

        /// <summary>
        /// 安装 Content faction 表（factions.json 转换后的 presentation）。幂等全量替换：
        /// 每次 Content bootstrap 调用一次；调用方在 Data 层，Core 不反向依赖 Data。
        /// 安装顺序：SortOrder 升序（同序按 FactionId ordinal）。
        /// </summary>
        public static void Install(IEnumerable<StrategicFactionPresentation> factions)
        {
            _installedById.Clear();
            if (factions != null)
            {
                foreach (var p in factions)
                {
                    if (string.IsNullOrEmpty(p.FactionId))
                        continue;
                    _installedById[p.FactionId] = p;
                }
            }

            var list = new List<StrategicFactionPresentation>(_installedById.Values);
            list.Sort(CompareBySortOrder);
            _installedOrdered = list;
        }

        static int CompareBySortOrder(StrategicFactionPresentation a, StrategicFactionPresentation b)
        {
            var bySort = a.SortOrder.CompareTo(b.SortOrder);
            if (bySort != 0)
                return bySort;
            return string.CompareOrdinal(a.FactionId, b.FactionId);
        }

        /// <summary>清空已安装 Content → 全部回到 hardcoded fallback（纯 Core tests 用）。</summary>
        public static void ResetInstall()
        {
            _installedById.Clear();
            _installedOrdered = Array.Empty<StrategicFactionPresentation>();
        }

        public static bool TryGetInstalled(string factionId, out StrategicFactionPresentation presentation)
        {
            if (string.IsNullOrEmpty(factionId))
            {
                presentation = default;
                return false;
            }
            return _installedById.TryGetValue(factionId, out presentation);
        }

        /// <summary>已安装 Content 势力（SortOrder 升序）；空 = 未安装。</summary>
        public static IReadOnlyList<StrategicFactionPresentation> InstalledFactions => _installedOrdered;

        // ------------------------------------------------------------------
        // 查询 API（content-first；miss 回 fallback）
        // ------------------------------------------------------------------

        public static string DisplayName(string factionId)
        {
            if (string.IsNullOrEmpty(factionId))
                return "无归属";
            if (_installedById.TryGetValue(factionId, out var p))
                return p.DisplayName;
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

        /// <summary>RGBA 0..1 for Host map tint（content-first；miss 回 fallback）。</summary>
        public static void MapTint(string factionId, out float r, out float g, out float b)
        {
            if (!string.IsNullOrEmpty(factionId) && _installedById.TryGetValue(factionId, out var p))
            {
                r = p.R;
                g = p.G;
                b = p.B;
                return;
            }

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

        /// <summary>
        /// 解析 Content mapColor 文本（"#RRGGBB"）到 0..1。非法返回 false 且不写输出。
        /// 放在 Core 供 Data loader 校验与安装转换共用（单点 hex 语义，不另建第二套解析）。
        /// </summary>
        public static bool TryParseMapColor(string text, out float r, out float g, out float b)
        {
            r = g = b = 0f;
            if (string.IsNullOrEmpty(text) || text.Length != 7 || text[0] != '#')
                return false;
            for (var i = 1; i < 7; i++)
            {
                var c = text[i];
                var hex = c >= '0' && c <= '9' ? c - '0'
                    : c >= 'a' && c <= 'f' ? c - 'a' + 10
                    : c >= 'A' && c <= 'F' ? c - 'A' + 10
                    : -1;
                if (hex < 0)
                    return false;
            }

            r = ParseHexByte(text, 1) / 255f;
            g = ParseHexByte(text, 3) / 255f;
            b = ParseHexByte(text, 5) / 255f;
            return true;
        }

        static float ParseHexByte(string text, int start)
        {
            var hi = HexValue(text[start]);
            var lo = HexValue(text[start + 1]);
            return hi * 16 + lo;
        }

        static int HexValue(char c)
        {
            if (c >= '0' && c <= '9')
                return c - '0';
            if (c >= 'a' && c <= 'f')
                return c - 'a' + 10;
            if (c >= 'A' && c <= 'F')
                return c - 'A' + 10;
            return 0;
        }
    }

    /// <summary>
    /// Content faction 的 Core presentation（Data 层由 StrategicFactionDefinition 转换而来）。
    /// 只含展示/作者元数据；不含成员、领土、WorldSite（那些属于各自 authority）。
    /// </summary>
    public readonly struct StrategicFactionPresentation
    {
        public readonly string FactionId;
        public readonly string DisplayName;
        public readonly float R;
        public readonly float G;
        public readonly float B;
        public readonly bool TerritorySelectable;
        public readonly int SortOrder;

        public StrategicFactionPresentation(
            string factionId,
            string displayName,
            float r,
            float g,
            float b,
            bool territorySelectable,
            int sortOrder)
        {
            FactionId = factionId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            R = r;
            G = g;
            B = b;
            TerritorySelectable = territorySelectable;
            SortOrder = sortOrder;
        }
    }
}
