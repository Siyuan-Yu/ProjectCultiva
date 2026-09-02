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

        /// <summary>
        /// Phase 5R-B3C1.2：正式 ingress connection 的 transient context —— 跨 Core ingress（
        /// TryCrossWildernessEdge / LocalVisible）→ Host 展开 → Materialize 存活，供 Safe Landing
        /// 解析 inward 方向。只保存本次 transition 所需；不落 Save、不是新 Position truth。
        /// </summary>
        public HexCoord IngressFootprintHex { get; private set; }

        public HexCoord IngressFromWildernessHex { get; private set; }

        /// <summary>Local 平面 outward 方向（footprint 格 → 来向荒野格）；inward = 取反。</summary>
        public float IngressDirectionLocalX { get; private set; }

        public float IngressDirectionLocalY { get; private set; }

        public float IngressBoundaryWorldX { get; private set; }

        public float IngressBoundaryWorldY { get; private set; }

        public bool HasIngressContext { get; private set; }

        public void SetIngressContext(SurfaceExitConnection connection)
        {
            IngressFootprintHex = connection.SourceHex;
            IngressFromWildernessHex = connection.DestinationHex;
            IngressDirectionLocalX = connection.LocalDirectionX;
            IngressDirectionLocalY = connection.LocalDirectionY;
            IngressBoundaryWorldX = connection.BoundaryContactWorldX;
            IngressBoundaryWorldY = connection.BoundaryContactWorldY;
            HasIngressContext = true;
        }

        /// <summary>
        /// IngressContext 是 one-shot：本次 destination materialize + final landing 完成后消费，
        /// 防止 WorldSite→WorldSite / 无新 SetIngressContext 的 materialize 读到上一 Site 的
        /// 旧 ingress direction。只清 ingress 字段；不动 TransitionInProgress / EdgeArmed /
        /// LastLocal / LastExit* 等其它 gate state。
        /// </summary>
        public void ConsumeIngressContext()
        {
            IngressFootprintHex = default;
            IngressFromWildernessHex = default;
            IngressDirectionLocalX = 0f;
            IngressDirectionLocalY = 0f;
            IngressBoundaryWorldX = 0f;
            IngressBoundaryWorldY = 0f;
            HasIngressContext = false;
        }

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
            IngressFootprintHex = default;
            IngressFromWildernessHex = default;
            IngressDirectionLocalX = 0f;
            IngressDirectionLocalY = 0f;
            IngressBoundaryWorldX = 0f;
            IngressBoundaryWorldY = 0f;
            HasIngressContext = false;
        }
    }
}
