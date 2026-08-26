using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Data.Content
{
    /// <summary>
    /// Grid map layout authored by MapEditor. Cell (0,0) covers
    /// [OriginX, OriginX+CellSize) × [OriginY, OriginY+CellSize).
    /// </summary>
    public sealed class MapLayoutDefinition
    {
        public DefinitionId Id { get; set; }
        public string Name { get; set; }
        public string WorldRegionId { get; set; }
        public float OriginX { get; set; }
        public float OriginY { get; set; }
        public float CellSize { get; set; } = 1f;
        public int Width { get; set; }
        public int Height { get; set; }
        /// <summary>
        /// Surface Exit Trigger 向内深度（world units）。≤0＝运行时用默认值。
        /// Gameplay Detection 参数，非 Presentation。
        /// </summary>
        public float ExitTriggerDepth { get; set; }
        public List<MapPlacement> Placements { get; set; } = new List<MapPlacement>();
    }

    public sealed class MapPlacement
    {
        public string Id { get; set; }
        public string Kind { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int W { get; set; } = 1;
        public int H { get; set; } = 1;
        public bool BlocksMovement { get; set; }
        public string BoundLocationId { get; set; }
        public string Label { get; set; }
        /// <summary>kind=loot 时：拾取进背包的 item id。</summary>
        public string LootItemId { get; set; }

        /// <summary>kind=spawnZone：刷怪表 id（type=spawnTable）。</summary>
        public string SpawnTableId { get; set; }

        /// <summary>kind=spawnZone：本次刷出总数；0＝按表 entries 的 countMin～Max 合计。</summary>
        public int SpawnCount { get; set; }
    }
}
