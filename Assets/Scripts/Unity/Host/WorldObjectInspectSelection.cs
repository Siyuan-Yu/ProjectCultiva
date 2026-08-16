namespace XianXia.Unity.Host
{
    public enum WorldObjectInspectKind
    {
        None = 0,
        ControlCore = 1,
        Housing = 2,
        WorkArea = 3,
        Plot = 4,
        Destructible = 5
    }

    /// <summary>左键点空后的世界物检视目标（只读况栏；无指令球）。</summary>
    public sealed class WorldObjectInspectSelection
    {
        public WorldObjectInspectKind Kind { get; private set; }
        public string WorkAreaId { get; private set; } = string.Empty;
        public HostMapPlotCell Plot { get; private set; }
        public HostMapDestructible Destructible { get; private set; }

        public bool HasTarget => Kind != WorldObjectInspectKind.None;

        public void Clear()
        {
            Kind = WorldObjectInspectKind.None;
            WorkAreaId = string.Empty;
            Plot = null;
            Destructible = null;
        }

        public void SetControlCore(string workAreaId)
        {
            Clear();
            Kind = WorldObjectInspectKind.ControlCore;
            WorkAreaId = workAreaId ?? string.Empty;
        }

        public void SetHousing(string workAreaId)
        {
            Clear();
            Kind = WorldObjectInspectKind.Housing;
            WorkAreaId = workAreaId ?? string.Empty;
        }

        public void SetWorkArea(string workAreaId)
        {
            Clear();
            Kind = WorldObjectInspectKind.WorkArea;
            WorkAreaId = workAreaId ?? string.Empty;
        }

        public void SetPlot(HostMapPlotCell plot)
        {
            Clear();
            if (plot == null)
                return;
            Kind = WorldObjectInspectKind.Plot;
            Plot = plot;
        }

        public void SetDestructible(HostMapDestructible d)
        {
            Clear();
            if (d == null || d.IsDestroyed)
                return;
            Kind = WorldObjectInspectKind.Destructible;
            Destructible = d;
        }
    }
}
