namespace XianXia.Core.World.Hex
{
    /// <summary>战略世界最小空间单位。</summary>
    public sealed class HexCell
    {
        public HexCoord Coord { get; set; }
        public HexTerrainType Terrain { get; set; } = HexTerrainType.Plain;
        public float MovementCost { get; set; }
        public bool IsRoad { get; set; }
        public bool IsPassable { get; set; } = true;
        public string WorldSiteId { get; set; } = string.Empty;
        public string ControlFactionId { get; set; } = string.Empty;

        /// <summary>兼容旧字段名。</summary>
        public string StrategicSiteId
        {
            get => WorldSiteId;
            set => WorldSiteId = value;
        }

        public bool HasSite => !string.IsNullOrEmpty(WorldSiteId);

        public bool IsTraversable => IsPassable;

        public float ResolveMovementCost() =>
            MovementCost > 0f ? MovementCost : HexTerrainCatalog.DefaultMovementCost(Terrain, IsRoad);
    }
}
