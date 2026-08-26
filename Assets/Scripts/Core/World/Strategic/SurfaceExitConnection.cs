using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    public enum SurfaceExitDestinationKind
    {
        WildernessHex = 0,
        WorldSite = 1,
    }

    /// <summary>沿边界的精确覆盖矩形（Presentation = Detection 同一几何的离散覆盖）。</summary>
    public readonly struct SurfaceExitCoverageRect
    {
        public SurfaceExitCoverageRect(float minX, float maxX, float minY, float maxY)
        {
            MinX = minX;
            MaxX = maxX;
            MinY = minY;
            MaxY = maxY;
        }

        public float MinX { get; }
        public float MaxX { get; }
        public float MinY { get; }
        public float MaxY { get; }

        public float Width => MaxX - MinX;
        public float Height => MaxY - MinY;
    }

    /// <summary>
    /// 一个实际合法 Surface Exit Connection（数量由 Footprint／邻接推导，非固定六槽）。
    /// </summary>
    public readonly struct SurfaceExitConnection
    {
        public SurfaceExitConnection(
            HexCoord sourceHex,
            HexCoord destinationHex,
            int directionIndex,
            SurfaceExitDestinationKind destinationKind,
            string destinationSiteId,
            float localDirectionX,
            float localDirectionY,
            float exitCenterLocalX,
            float exitCenterLocalY,
            SurfaceExitCoverageRect slotRect,
            float boundaryContactWorldX = 0f,
            float boundaryContactWorldY = 0f)
        {
            SourceHex = sourceHex;
            DestinationHex = destinationHex;
            DirectionIndex = directionIndex;
            DestinationKind = destinationKind;
            DestinationSiteId = destinationSiteId ?? string.Empty;
            LocalDirectionX = localDirectionX;
            LocalDirectionY = localDirectionY;
            ExitCenterLocalX = exitCenterLocalX;
            ExitCenterLocalY = exitCenterLocalY;
            SlotRect = slotRect;
            BoundaryContactWorldX = boundaryContactWorldX;
            BoundaryContactWorldY = boundaryContactWorldY;
        }

        public HexCoord SourceHex { get; }
        public HexCoord DestinationHex { get; }
        public int DirectionIndex { get; }
        public SurfaceExitDestinationKind DestinationKind { get; }
        public string DestinationSiteId { get; }
        public float LocalDirectionX { get; }
        public float LocalDirectionY { get; }
        public float ExitCenterLocalX { get; }
        public float ExitCenterLocalY { get; }
        public SurfaceExitCoverageRect SlotRect { get; }
        public float BoundaryContactWorldX { get; }
        public float BoundaryContactWorldY { get; }
    }
}
