using System;
using XianXia.Core.Domain.Ids;

namespace XianXia.Core.Exploration
{
    /// <summary>
    /// 从 MapLayout metadata／入口约定解析 SpaceKind。禁止业务代码写死 map id。
    /// </summary>
    public static class SeparateSpaceResolver
    {
        public static SeparateSpaceKind ParseSpaceKind(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return SeparateSpaceKind.SeparateMap;
            switch (raw.Trim().ToLowerInvariant())
            {
                case "cave":
                    return SeparateSpaceKind.Cave;
                case "interior":
                    return SeparateSpaceKind.Interior;
                case "dungeon":
                    return SeparateSpaceKind.Dungeon;
                case "separatemap":
                case "separate_map":
                case "separate-map":
                    return SeparateSpaceKind.SeparateMap;
                default:
                    return SeparateSpaceKind.SeparateMap;
            }
        }

        public static string ToContentToken(SeparateSpaceKind kind)
        {
            switch (kind)
            {
                case SeparateSpaceKind.Cave:
                    return "cave";
                case SeparateSpaceKind.Interior:
                    return "interior";
                case SeparateSpaceKind.Dungeon:
                    return "dungeon";
                case SeparateSpaceKind.SeparateMap:
                    return "separateMap";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// 启发式：入口 tags／map id 含 cave 时标 Cave；否则 SeparateMap。
        /// Host／Content 有显式 spaceKind 时优先用显式值。
        /// </summary>
        public static SeparateSpaceKind InferFromEntrance(
            WorldLocationState entrance,
            string explicitSpaceKind)
        {
            if (!string.IsNullOrWhiteSpace(explicitSpaceKind))
                return ParseSpaceKind(explicitSpaceKind);

            if (entrance?.Tags != null)
            {
                for (var i = 0; i < entrance.Tags.Count; i++)
                {
                    var tag = entrance.Tags[i];
                    if (string.Equals(tag, "cave", StringComparison.OrdinalIgnoreCase))
                        return SeparateSpaceKind.Cave;
                    if (string.Equals(tag, "interior", StringComparison.OrdinalIgnoreCase))
                        return SeparateSpaceKind.Interior;
                    if (string.Equals(tag, "dungeon", StringComparison.OrdinalIgnoreCase))
                        return SeparateSpaceKind.Dungeon;
                }
            }

            var mapId = entrance?.EnterLocalMapId ?? string.Empty;
            if (mapId.IndexOf("cave", StringComparison.OrdinalIgnoreCase) >= 0)
                return SeparateSpaceKind.Cave;
            if (mapId.IndexOf("dungeon", StringComparison.OrdinalIgnoreCase) >= 0)
                return SeparateSpaceKind.Dungeon;
            return SeparateSpaceKind.SeparateMap;
        }
    }
}
