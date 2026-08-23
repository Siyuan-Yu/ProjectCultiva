namespace ContentAuthoring.Shared.HexWorld;

public enum HexEditorTool
{
    Select,
    Terrain,
    Road,
    Site,
    Erase,
}

public sealed class HexWorldEditorDocument
{
    readonly Stack<string> _undo = new();
    readonly Stack<string> _redo = new();

    public HexWorldDefinitionDto World { get; private set; } = HexWorldContentGenerator.CreateBlank(
        "base:hex_world_new",
        "New Hex World",
        HexWorldLayoutShared.DefaultWidth,
        HexWorldLayoutShared.DefaultHeight,
        HexTerrainIds.Mountain,
        passable: false);

    public string? FilePath { get; private set; }
    public bool IsDirty { get; private set; }
    public HexEditorTool ActiveTool { get; set; } = HexEditorTool.Select;
    public string ActiveTerrain { get; set; } = HexTerrainIds.Mountain;
    public string ActiveSiteType { get; set; } = "Village";
    public int BrushRadius { get; set; } = 0;
    public string? SelectedSiteId { get; set; }
    public HexCoordDto? SelectedHex { get; set; }
    public bool EditFootprintMode { get; set; }

    public event Action? Changed;

    public void NewWorld(int width, int height, string defaultTerrain, bool passable)
    {
        PushUndo();
        World = HexWorldContentGenerator.CreateBlank(
            "base:hex_world_new",
            "New Hex World",
            width,
            height,
            defaultTerrain,
            passable);
        FilePath = null;
        MarkDirty(false);
        Notify();
    }

    public void Load(HexWorldDefinitionDto world, string? path)
    {
        _undo.Clear();
        _redo.Clear();
        World = world;
        FilePath = path;
        MarkDirty(false);
        Notify();
    }

    public void Save(string path)
    {
        HexWorldContentJson.NormalizeForSave(World);
        HexWorldContentJson.SaveFile(path, World);
        FilePath = path;
        MarkDirty(false);
    }

    public void MarkDirty(bool dirty = true)
    {
        IsDirty = dirty;
        Changed?.Invoke();
    }

    public void Notify() => Changed?.Invoke();

    public void PushUndo()
    {
        _undo.Push(HexWorldContentJson.Serialize(World));
        _redo.Clear();
    }

    public bool Undo()
    {
        if (_undo.Count == 0)
            return false;
        _redo.Push(HexWorldContentJson.Serialize(World));
        World = HexWorldContentJson.Load(_undo.Pop()).Definitions[0];
        MarkDirty(true);
        Notify();
        return true;
    }

    public bool Redo()
    {
        if (_redo.Count == 0)
            return false;
        _undo.Push(HexWorldContentJson.Serialize(World));
        World = HexWorldContentJson.Load(_redo.Pop()).Definitions[0];
        MarkDirty(true);
        Notify();
        return true;
    }

    public IEnumerable<HexCoordDto> CollectBrushHexes(HexCoordDto center)
    {
        yield return center;
        if (BrushRadius <= 0)
            yield break;
        var pending = new Queue<HexCoordDto>();
        var seen = new HashSet<(int Q, int R)> { (center.Q, center.R) };
        pending.Enqueue(center);
        for (var step = 0; step < BrushRadius; step++)
        {
            var layer = pending.Count;
            for (var i = 0; i < layer; i++)
            {
                var current = pending.Dequeue();
                for (var d = 0; d < 6; d++)
                {
                    var n = HexWorldLayoutShared.Neighbor(current, d);
                    if (n.Q < 0 || n.R < 0 || n.Q >= World.Width || n.R >= World.Height)
                        continue;
                    if (!seen.Add((n.Q, n.R)))
                        continue;
                    yield return n;
                    pending.Enqueue(n);
                }
            }
        }
    }

    public void ApplyTerrainBrush(HexCoordDto center, bool singleStrokeUndo = true)
    {
        if (singleStrokeUndo)
            PushUndo();
        foreach (var hex in CollectBrushHexes(center))
            HexWorldContentGenerator.SetTerrain(World, hex.Q, hex.R, ActiveTerrain);
        MarkDirty(true);
        Notify();
    }

    public void ApplyRoadBrush(HexCoordDto center, bool singleStrokeUndo = true)
    {
        if (singleStrokeUndo)
            PushUndo();
        foreach (var hex in CollectBrushHexes(center))
            HexWorldContentGenerator.PaintRoadTile(World, hex.Q, hex.R);
        MarkDirty(true);
        Notify();
    }

    public void ApplyEraseBrush(HexCoordDto center, bool singleStrokeUndo = true)
    {
        if (singleStrokeUndo)
            PushUndo();
        foreach (var hex in CollectBrushHexes(center))
        {
            HexWorldContentGenerator.SetTerrain(
                World,
                hex.Q,
                hex.R,
                World.DefaultTerrain,
                World.DefaultPassable);
            var cell = HexWorldContentGenerator.GetCell(World, hex.Q, hex.R);
            if (cell != null)
                cell.IsRoad = false;
        }

        MarkDirty(true);
        Notify();
    }

    public HexWorldSiteDto? FindSiteAt(HexCoordDto hex)
    {
        foreach (var site in World.Sites)
        {
            var footprint = site.Footprint.Count > 0
                ? site.Footprint
                : new List<HexCoordDto> { new(site.AnchorQ, site.AnchorR) };
            if (footprint.Any(h => h.Q == hex.Q && h.R == hex.R))
                return site;
        }

        return null;
    }

    public HexWorldSiteDto CreateSite(HexCoordDto hex)
    {
        PushUndo();
        var id = $"base:site_editor_{World.Sites.Count + 1}";
        var site = new HexWorldSiteDto
        {
            SiteId = id,
            DisplayName = "新地点",
            SiteType = ActiveSiteType,
            AnchorQ = hex.Q,
            AnchorR = hex.R,
            Footprint = new List<HexCoordDto> { hex },
        };
        World.Sites.Add(site);
        HexWorldContentGenerator.PaintRoadTile(World, hex.Q, hex.R);
        SelectedSiteId = id;
        MarkDirty(true);
        Notify();
        return site;
    }

    public void DeleteSite(string siteId)
    {
        PushUndo();
        World.Sites.RemoveAll(s => string.Equals(s.SiteId, siteId, StringComparison.Ordinal));
        if (string.Equals(SelectedSiteId, siteId, StringComparison.Ordinal))
            SelectedSiteId = null;
        MarkDirty(true);
        Notify();
    }

    public void MoveSite(string siteId, HexCoordDto newAnchor)
    {
        var site = World.Sites.FirstOrDefault(s => string.Equals(s.SiteId, siteId, StringComparison.Ordinal));
        if (site == null)
            return;
        PushUndo();
        var dq = newAnchor.Q - site.AnchorQ;
        var dr = newAnchor.R - site.AnchorR;
        site.AnchorQ = newAnchor.Q;
        site.AnchorR = newAnchor.R;
        if (site.Footprint.Count <= 1)
        {
            site.Footprint = new List<HexCoordDto> { newAnchor };
        }
        else
        {
            site.Footprint = site.Footprint
                .Select(h => new HexCoordDto(h.Q + dq, h.R + dr))
                .ToList();
        }

        foreach (var hex in site.Footprint)
            HexWorldContentGenerator.PaintRoadTile(World, hex.Q, hex.R);
        MarkDirty(true);
        Notify();
    }

    public void ToggleFootprintHex(string siteId, HexCoordDto hex, bool add)
    {
        var site = World.Sites.FirstOrDefault(s => string.Equals(s.SiteId, siteId, StringComparison.Ordinal));
        if (site == null)
            return;
        PushUndo();
        if (add)
        {
            if (!site.Footprint.Any(h => h.Q == hex.Q && h.R == hex.R))
                site.Footprint.Add(hex);
            HexWorldContentGenerator.PaintRoadTile(World, hex.Q, hex.R);
        }
        else
        {
            if (hex.Q == site.AnchorQ && hex.R == site.AnchorR)
                return;
            site.Footprint.RemoveAll(h => h.Q == hex.Q && h.R == hex.R);
        }

        MarkDirty(true);
        Notify();
    }
}
