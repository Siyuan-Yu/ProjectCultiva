namespace XianXia.Core.World.Hex
{
    /// <summary>Legacy grid／tool／prototype 的 Hex 单元；不是正常 Continuous Surface 的最小空间 authority。</summary>
    public sealed class HexCell
    {
        public HexCoord Coord { get; set; }
        public HexTerrainType Terrain { get; set; } = HexTerrainType.Plain;
        public float MovementCost { get; set; }
        public bool IsRoad { get; set; }
        public bool IsPassable { get; set; } = true;
        public string WorldSiteId { get; set; } = string.Empty;
        public string ControlFactionId { get; set; } = string.Empty;

        public bool HasSite => !string.IsNullOrEmpty(WorldSiteId);

        public bool IsTraversable => IsPassable;

        public float ResolveMovementCost() =>
            MovementCost > 0f ? MovementCost : HexTerrainCatalog.DefaultMovementCost(Terrain, IsRoad);
    }
}
