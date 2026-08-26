using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Surface LocalMap Edge Transition 防抖：TransitionInProgress + Disarm/Rearm。
    /// 不修改 WorldPosition；只控制是否允许再次跨边。
    /// </summary>
    public sealed class PlayerPartySurfaceEdgeGate
    {
        public bool TransitionInProgress { get; private set; }

        /// <summary>未 Armed 时禁止跨边检测（刚进入目的图、尚在 Entry 近缘）。</summary>
        public bool EdgeArmed { get; private set; } = true;

        public int EntryEdgeDirection { get; private set; }

        public bool HasEntryEdge { get; private set; }

        public float LastLocalX { get; private set; }

        public float LastLocalY { get; private set; }

        public bool HasLastLocal { get; private set; }

        public int LastExitDirection { get; private set; } = -1;

        public HexCoord LastExitDestinationHex { get; private set; }

        public HexCoord LastExitSourceFootprintHex { get; private set; }

        public bool HasExitBoundaryContext { get; private set; }

        public bool CanAttemptEdgeTransition => !TransitionInProgress && EdgeArmed;

        public void BeginTransition(int exitDirection)
        {
            BeginTransition(exitDirection, default, default, false);
        }

        public void BeginTransition(
            int exitDirection,
            HexCoord destinationHex,
            HexCoord sourceFootprintHex,
            bool hasBoundaryContext)
        {
            TransitionInProgress = true;
            EdgeArmed = false;
            LastExitDirection = exitDirection;
            HasExitBoundaryContext = hasBoundaryContext;
            if (hasBoundaryContext)
            {
                LastExitDestinationHex = destinationHex;
                LastExitSourceFootprintHex = sourceFootprintHex;
            }

            HasLastLocal = false;
        }

        /// <summary>Materialize 完成后调用：Disarm，记录 Entry 边与出生 Local 点。</summary>
        public void CompleteTransition(int exitDirection, float spawnLocalX, float spawnLocalY)
        {
            TransitionInProgress = false;
            EdgeArmed = false;
            LastExitDirection = exitDirection;
            EntryEdgeDirection = WildernessLocalWorldProjection.OppositeDirection(exitDirection);
            HasEntryEdge = true;
            LastLocalX = spawnLocalX;
            LastLocalY = spawnLocalY;
            HasLastLocal = true;
        }

        public void NoteLocalPosition(float localX, float localY)
        {
            LastLocalX = localX;
            LastLocalY = localY;
            HasLastLocal = true;
        }

        public void TickRearm(
            float localX,
            float localY,
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds)
        {
            if (TransitionInProgress || EdgeArmed)
                return;
            if (WildernessLocalWorldProjection.IsInSafeInterior(localX, localY, bounds))
                EdgeArmed = true;
        }

        public void ClearEdgeState()
        {
            TransitionInProgress = false;
            EdgeArmed = true;
            HasEntryEdge = false;
            HasLastLocal = false;
            LastExitDirection = -1;
            HasExitBoundaryContext = false;
        }
    }
}
