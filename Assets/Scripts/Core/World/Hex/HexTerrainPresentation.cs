using System.Collections.Generic;

namespace XianXia.Core.World.Hex
{
    /// <summary>Hex WorldMap 地形视觉语义（V1）：不改变 Domain 规则，仅统一配色与图例。</summary>
    public static class HexTerrainPresentation
    {
        public readonly struct LegendEntry
        {
            public LegendEntry(string label, HexRgb color)
            {
                Label = label;
                Color = color;
            }

            public string Label { get; }
            public HexRgb Color { get; }
        }

        static readonly HexRgb Plain = new HexRgb(0.93f, 0.89f, 0.78f);
        static readonly HexRgb Forest = new HexRgb(0.72f, 0.84f, 0.62f);
        static readonly HexRgb Water = new HexRgb(0.68f, 0.82f, 0.94f);
        static readonly HexRgb Mountain = new HexRgb(0.72f, 0.68f, 0.62f);
        static readonly HexRgb Road = new HexRgb(0.88f, 0.78f, 0.52f);
        static readonly HexRgb ImpassableTint = new HexRgb(0.58f, 0.54f, 0.50f);

        static readonly LegendEntry[] Legend =
        {
            new LegendEntry("平原", Plain),
            new LegendEntry("森林", Forest),
            new LegendEntry("水域", Water),
            new LegendEntry("岩地", Mountain),
            new LegendEntry("道路", Road),
        };

        public static IReadOnlyList<LegendEntry> LegendEntries => Legend;

        public static string GetDisplayName(HexCell tile)
        {
            if (tile == null)
                return "未知";

            if (tile.IsRoad || tile.Terrain == HexTerrainType.Road)
                return "道路";

            return tile.Terrain switch
            {
                HexTerrainType.Forest => "森林",
                HexTerrainType.Water => "水域",
                HexTerrainType.Mountain => "岩地",
                HexTerrainType.Plain => "平原",
                _ => tile.Terrain.ToString(),
            };
        }

        public static HexRgb ResolveRgb(HexCell tile)
        {
            if (tile == null)
                return Plain;

            HexRgb baseColor;
            if (tile.IsRoad || tile.Terrain == HexTerrainType.Road)
                baseColor = Road;
            else
            {
                baseColor = tile.Terrain switch
                {
                    HexTerrainType.Forest => Forest,
                    HexTerrainType.Water => Water,
                    HexTerrainType.Mountain => Mountain,
                    _ => Plain,
                };
            }

            if (!tile.IsPassable)
                baseColor = Lerp(baseColor, ImpassableTint, 0.42f);

            return baseColor;
        }

        static HexRgb Lerp(HexRgb a, HexRgb b, float t) =>
            new HexRgb(
                a.R + (b.R - a.R) * t,
                a.G + (b.G - a.G) * t,
                a.B + (b.B - a.B) * t);
    }
}
