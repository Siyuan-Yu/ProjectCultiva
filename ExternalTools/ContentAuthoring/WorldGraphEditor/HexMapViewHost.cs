using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ContentAuthoring.Shared.HexWorld;

namespace WorldGraphEditor;

/// <summary>
/// Chunked DrawingVisual map host: world-space geometry cache + viewport transform.
/// Pan/zoom updates transform / visibility only; terrain edits rebuild dirty chunks.
/// </summary>
public sealed class HexMapViewHost : FrameworkElement
{
    const double CellFillScale = 0.92;
    /// <summary>WorldSite 名称始终显示；仅控制字号随缩放微调的下限。</summary>
    const double LabelMinScale = 0.5;

    readonly VisualCollection _visuals;
    readonly Dictionary<(int Cx, int Cy), DrawingVisual> _chunkVisuals = new();
    readonly DrawingVisual _overlayVisual = new();
    readonly DrawingVisual _territoryVisual = new();
    readonly DrawingVisual _brushPreviewVisual = new();
    readonly DrawingVisual _labelOverlayVisual = new();
    readonly Dictionary<int, SolidColorBrush> _brushCache = new();
    readonly HexEditorRenderCache _cache = new();

    HexWorldDefinitionDto? _world;
    HexMapViewport? _viewport;
    HexCoordDto? _selected;
    HexCoordDto? _hover;
    string? _selectedSiteId;
    bool _editFootprintMode;
    bool _territoryVisible;
    string? _territoryBrushFactionId;
    readonly Dictionary<string, string> _factionColorById = new(StringComparer.Ordinal);
    IReadOnlyList<HexCoordDto>? _brushPreviewHexes;
    Color _brushPreviewColor;
    MatrixTransform? _worldToScreen;
    int _visibleAttached;
    double _lastRebuildMs;
    double _lastSyncMs;
    long _repaintTicks;

    public HexMapViewHost()
    {
        _visuals = new VisualCollection(this) { _territoryVisual, _brushPreviewVisual, _overlayVisual, _labelOverlayVisual };
        Focusable = false;
        ClipToBounds = true;
        SnapsToDevicePixels = true;
    }

    static readonly SolidColorBrush MapBackground = CreateFrozenBrush(0x2A, 0x26, 0x20);

    static SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    public HexEditorRenderCache Cache => _cache;
    public int VisibleChunkCount => _visibleAttached;
    public double LastRebuildMs => _lastRebuildMs;
    public double LastSyncMs => _lastSyncMs;
    public HexCoordDto? HoverHex => _hover;

    protected override int VisualChildrenCount => _visuals.Count;

    protected override Visual GetVisualChild(int index) => _visuals[index];

    public void SetWorld(HexWorldDefinitionDto world, HexMapViewport viewport, bool fullRebuild = true)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
        _viewport.HexSize = world.HexSize;
        if (fullRebuild || _cache.MapWidth != world.Width || _cache.MapHeight != world.Height)
        {
            DetachAllChunks();
            _chunkVisuals.Clear();
            _cache.ResetForWorld(world.Width, world.Height);
        }

        RebuildDirtyChunks();
        SyncViewport(rebuildGeometry: false);
        RedrawOverlay();
        RedrawTerritoryOverlay();
    }

    public void MarkHexesDirty(IEnumerable<(int Q, int R)> hexes)
    {
        _cache.MarkHexesDirty(hexes);
        RebuildDirtyChunks();
        RedrawOverlay();
    }

    public void MarkAllDirtyAndRebuild()
    {
        _cache.MarkAllDirty();
        RebuildDirtyChunks();
        SyncViewport(rebuildGeometry: false);
        RedrawOverlay();
    }

    /// <summary>Pan / zoom / resize: transform + culling only (no terrain geometry rebuild).</summary>
    public void SyncViewport(bool rebuildGeometry = false)
    {
        if (_world == null || _viewport == null)
            return;

        var sw = Stopwatch.StartNew();
        _viewport.SetViewportSize(ActualWidth, ActualHeight);
        if (rebuildGeometry)
        {
            _cache.MarkAllDirty();
            RebuildDirtyChunks();
        }
        else
        {
            _cache.NoteViewportSyncWithoutRebuild();
        }

        ApplyWorldToScreenTransform();
        UpdateChunkVisibility();
        RedrawOverlay();
        sw.Stop();
        _lastSyncMs = sw.Elapsed.TotalMilliseconds;
        _repaintTicks++;
        InvalidateVisual();
    }

    public void SetSelection(HexCoordDto? selected)
    {
        _selected = selected;
        RedrawOverlay();
        RedrawTerritoryOverlay();
    }

    public void SetSiteOverlay(string? selectedSiteId, bool editFootprintMode)
    {
        _selectedSiteId = selectedSiteId;
        _editFootprintMode = editFootprintMode;
        RedrawOverlay();
        RedrawTerritoryOverlay();
    }

    /// <summary>Authoring-only Territory 填色，独立于 hover overlay，绝不进入游戏表现。factionId = 当前笔刷势力（用于预览描边）。</summary>
    public void SetTerritoryOverlay(string? brushFactionId, bool visible)
    {
        _territoryBrushFactionId = brushFactionId;
        _territoryVisible = visible;
        RedrawTerritoryOverlay();
        RedrawBrushPreview();
    }

    public void RebuildTerritoryOverlay() => RedrawTerritoryOverlay();

    /// <summary>Editor / Game 同色：factionId → #RRGGBB（来自 factions.json，禁 hash 调色板）。</summary>
    public void SetFactionColors(IEnumerable<KeyValuePair<string, string>> colorByFactionId)
    {
        _factionColorById.Clear();
        foreach (var kv in colorByFactionId)
            _factionColorById[kv.Key] = kv.Value;
        RedrawTerritoryOverlay();
    }

    /// <summary>Territory Brush hover 预览：显示本次 stroke 将影响的格（单格或整片默认辖区）。hexes=null 清除。</summary>
    public void SetBrushPreview(IReadOnlyList<HexCoordDto>? hexes, Color color)
    {
        _brushPreviewHexes = hexes;
        _brushPreviewColor = color;
        RedrawBrushPreview();
    }

    /// <returns>True when hovered hex changed (caller may update status / inspector).</returns>
    public bool SetHover(HexCoordDto? hover)
    {
        var changed = !Nullable.Equals(_hover, hover);
        if (!changed)
            return false;
        _hover = hover;
        RedrawOverlay();
        return true;
    }

    public string FormatPerfStatus()
    {
        if (_world == null)
            return "Map —";
        return string.Format(
            CultureInfo.InvariantCulture,
            "World {0}×{1} · Cells {2} · Chunks {3}/{4} vis · Dirty {5} · Rebuild {6:F1}ms · Sync {7:F1}ms · GeoRebuilds {8}",
            _world.Width,
            _world.Height,
            _world.Width * _world.Height,
            _visibleAttached,
            _cache.TotalChunks,
            _cache.DirtyChunkCount,
            _lastRebuildMs,
            _lastSyncMs,
            _cache.GeometryRebuildCount);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        drawingContext.DrawRectangle(MapBackground, null, new Rect(RenderSize));
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        SyncViewport(rebuildGeometry: false);
    }

    void RebuildDirtyChunks()
    {
        if (_world == null)
            return;

        var dirty = _cache.SnapshotDirtyChunks();
        if (dirty.Count == 0)
            return;

        var sw = Stopwatch.StartNew();
        foreach (var chunk in dirty)
        {
            RebuildChunk(chunk.Cx, chunk.Cy);
            _cache.ClearDirty(chunk);
        }

        sw.Stop();
        _lastRebuildMs = sw.Elapsed.TotalMilliseconds;
        _cache.NoteGeometryRebuild(dirty.Count);
    }

    void RebuildChunk(int cx, int cy)
    {
        if (_world == null)
            return;

        if (!_chunkVisuals.TryGetValue((cx, cy), out var visual))
        {
            visual = new DrawingVisual();
            _chunkVisuals[(cx, cy)] = visual;
        }

        _cache.ChunkCellRange(cx, cy, out var q0, out var r0, out var q1, out var r1);
        var hexSize = _world.HexSize;
        var radius = Math.Max(0.05, hexSize * CellFillScale);

        using (var dc = visual.RenderOpen())
        {
            if (q1 < q0 || r1 < r0)
                return;

            for (var r = r0; r <= r1; r++)
            {
                for (var q = q0; q <= q1; q++)
                {
                    var cell = HexWorldContentGenerator.GetCell(_world, q, r);
                    if (cell == null)
                        continue;

                    HexWorldLayoutShared.CoordToWorldCenter(new HexCoordDto(q, r), hexSize, out var wx, out var wy);
                    var passable = cell.Passable ?? HexTerrainPalette.DefaultPassable(cell.Terrain);
                    var rgb = HexTerrainPalette.ResolveRgb(cell.Terrain, cell.IsRoad, passable);
                    var brush = GetBrush(rgb.R, rgb.G, rgb.B);
                    dc.DrawGeometry(brush, null, BuildHexGeometry(wx, wy, radius));
                }
            }
        }

        if (_worldToScreen != null)
            visual.Transform = _worldToScreen;
    }

    void ApplyWorldToScreenTransform()
    {
        if (_viewport == null)
            return;

        var scale = _viewport.Scale;
        var cx = _viewport.ViewportWidth * 0.5;
        var cy = _viewport.ViewportHeight * 0.5;
        var matrix = new Matrix(
            scale,
            0,
            0,
            -scale,
            cx - _viewport.ViewCenterX * scale,
            cy + _viewport.ViewCenterY * scale);
        _worldToScreen = new MatrixTransform(matrix);
        _worldToScreen.Freeze();

        foreach (var visual in _chunkVisuals.Values)
            visual.Transform = _worldToScreen;
        _overlayVisual.Transform = _worldToScreen;
        _territoryVisual.Transform = _worldToScreen;
        _brushPreviewVisual.Transform = _worldToScreen;
        _labelOverlayVisual.Transform = Transform.Identity;
    }

    void UpdateChunkVisibility()
    {
        if (_world == null || _viewport == null)
            return;

        HexEditorRenderCache.ComputeVisibleWorldRect(_viewport, out var minWx, out var maxWx, out var minWy, out var maxWy);
        var visible = _cache.CollectVisibleChunks(minWx, maxWx, minWy, maxWy, _world.HexSize);
        var visibleSet = new HashSet<(int Cx, int Cy)>(visible);

        // Detach hidden
        for (var i = _visuals.Count - 1; i >= 0; i--)
        {
            var v = _visuals[i];
            if (IsDecorationVisual(v))
                continue;
            var key = FindChunkKey(v);
            if (key == null || !visibleSet.Contains(key.Value))
                _visuals.Remove(v);
        }

        foreach (var chunk in visible)
        {
            if (!_chunkVisuals.TryGetValue(chunk, out var visual))
            {
                RebuildChunk(chunk.Cx, chunk.Cy);
                visual = _chunkVisuals[chunk];
            }

            if (!_visuals.Contains(visual))
            {
                // Keep Territory / interaction overlays on top.
                _visuals.Insert(Math.Max(0, _visuals.IndexOf(_territoryVisual)), visual);
            }
        }

        _visibleAttached = visible.Count;
        EnsureLabelOverlayOnTop();
    }

    bool IsDecorationVisual(Visual visual) =>
        ReferenceEquals(visual, _brushPreviewVisual) ||
        ReferenceEquals(visual, _territoryVisual) ||
        ReferenceEquals(visual, _overlayVisual) ||
        ReferenceEquals(visual, _labelOverlayVisual);

    (int Cx, int Cy)? FindChunkKey(Visual visual)
    {
        foreach (var kv in _chunkVisuals)
        {
            if (ReferenceEquals(kv.Value, visual))
                return kv.Key;
        }

        return null;
    }

    void DetachAllChunks()
    {
        for (var i = _visuals.Count - 1; i >= 0; i--)
        {
            if (!IsDecorationVisual(_visuals[i]))
                _visuals.RemoveAt(i);
        }
    }

    void RedrawOverlay()
    {
        if (_world == null || _viewport == null)
            return;

        var hexSize = _world.HexSize;
        var radius = Math.Max(0.05, hexSize * CellFillScale);
        var scale = Math.Max(0.0001, _viewport.Scale);
        var outlinePen = new Pen(new SolidColorBrush(Color.FromArgb(220, 255, 180, 40)), 2.0 / scale);
        outlinePen.Freeze();
        var hoverPen = new Pen(new SolidColorBrush(Color.FromArgb(200, 240, 220, 80)), 1.5 / scale);
        hoverPen.Freeze();
        var footprintFill = new SolidColorBrush(Color.FromArgb(90, 255, 230, 80));
        footprintFill.Freeze();
        var footprintPen = new Pen(new SolidColorBrush(Color.FromArgb(230, 255, 150, 30)), 2.6 / scale);
        footprintPen.Freeze();
        var anchorPen = new Pen(new SolidColorBrush(Color.FromArgb(255, 255, 60, 60)), 3.2 / scale);
        anchorPen.Freeze();
        var anchorFill = new SolidColorBrush(Color.FromArgb(120, 255, 90, 90));
        anchorFill.Freeze();

        using var dc = _overlayVisual.RenderOpen();

        HexWorldSiteDto? selectedSite = null;
        if (!string.IsNullOrEmpty(_selectedSiteId))
            selectedSite = _world.Sites.FirstOrDefault(s => s.SiteId == _selectedSiteId);

        if (selectedSite != null)
        {
            var presence = HexWorldPresenceRules.ResolvePresenceHex(selectedSite);
            var presenceFill = new SolidColorBrush(Color.FromArgb(110, 70, 160, 220));
            presenceFill.Freeze();
            var presencePen = new Pen(new SolidColorBrush(Color.FromRgb(70, 160, 220)), 1.6);
            presencePen.Freeze();
            foreach (var hex in HexWorldFootprintRules.ResolveFootprint(selectedSite))
            {
                HexWorldLayoutShared.CoordToWorldCenter(hex, hexSize, out var fx, out var fy);
                var isAnchor = hex.Q == selectedSite.AnchorQ && hex.R == selectedSite.AnchorR;
                var isPresence = hex.Q == presence.Q && hex.R == presence.R;
                if (isPresence && !isAnchor)
                {
                    dc.DrawGeometry(presenceFill, presencePen, BuildHexGeometry(fx, fy, radius * 1.02));
                    continue;
                }

                dc.DrawGeometry(
                    isAnchor ? anchorFill : footprintFill,
                    isAnchor ? anchorPen : footprintPen,
                    BuildHexGeometry(fx, fy, radius * 1.02));
            }
        }

        if (_hover is { Q: >= 0 } hover)
        {
            HexWorldLayoutShared.CoordToWorldCenter(hover, hexSize, out var hx, out var hy);
            dc.DrawGeometry(null, hoverPen, BuildHexGeometry(hx, hy, radius * 1.02));
        }

        if (_selected is { Q: >= 0 } sel)
        {
            HexWorldLayoutShared.CoordToWorldCenter(sel, hexSize, out var sx, out var sy);
            dc.DrawGeometry(null, outlinePen, BuildHexGeometry(sx, sy, radius * 1.05));
        }

        foreach (var site in _world.Sites)
        {
            var footprint = HexWorldFootprintRules.ResolveFootprint(site);
            var isSelected = selectedSite != null &&
                             string.Equals(site.SiteId, selectedSite.SiteId, StringComparison.Ordinal);
            foreach (var hex in footprint)
            {
                HexWorldLayoutShared.CoordToWorldCenter(hex, hexSize, out var wx, out var wy);
                DrawSiteIconUpright(dc, wx, wy, hexSize * 1.55);
            }

            HexWorldLayoutShared.CoordToWorldCenter(new HexCoordDto(site.AnchorQ, site.AnchorR), hexSize, out var ax, out var ay);
            if (_editFootprintMode && isSelected)
                dc.DrawGeometry(null, anchorPen, BuildHexGeometry(ax, ay, radius * 1.08));
        }

        RedrawLabelOverlay(hexSize);
    }

    void RedrawTerritoryOverlay()
    {
        if (_world == null || _viewport == null) return;
        using var dc = _territoryVisual.RenderOpen();
        if (!_territoryVisible) return;
        var radius = Math.Max(.05, _world.HexSize * CellFillScale);
        var claims = new List<(long Order, string Id, string FactionId, IEnumerable<HexCoordDto> Hexes)>();
        foreach (var site in _world.Sites)
            if (!string.IsNullOrEmpty(site.OwnerFactionId))
                claims.Add((site.ControlEstablishedOrder, site.SiteId, site.OwnerFactionId, NominalSiteRange(site)));
        foreach (var flag in _world.FactionFlags)
            if (!string.IsNullOrEmpty(flag.FactionId))
                claims.Add((flag.EstablishedOrder, flag.FlagId, flag.FactionId, NominalFlagRange(flag)));

        var effective = new Dictionary<HexCoordDto, string>();
        foreach (var claim in claims.OrderBy(c => c.Order).ThenBy(c => c.Id, StringComparer.Ordinal))
        {
            foreach (var hex in claim.Hexes)
                if (!effective.ContainsKey(hex)) effective[hex] = claim.FactionId;
        }
        foreach (var pair in effective)
        {
            if (!FactionColor(pair.Value, out var color)) continue;
            var isBrush = !string.IsNullOrEmpty(_territoryBrushFactionId) && string.Equals(_territoryBrushFactionId, pair.Value, StringComparison.Ordinal);
            var fill = new SolidColorBrush(Color.FromArgb(isBrush ? (byte)112 : (byte)62, color.R, color.G, color.B)); fill.Freeze();
            var pen = isBrush ? new Pen(new SolidColorBrush(Color.FromArgb(210, color.R, color.G, color.B)), .055) : null;
            if (pen != null) pen.Freeze();
            HexWorldLayoutShared.CoordToWorldCenter(pair.Key, _world.HexSize, out var x, out var y);
            dc.DrawGeometry(fill, pen, BuildHexGeometry(x, y, radius * .9));
        }

        IEnumerable<HexCoordDto>? selectedNominal = null;
        if (!string.IsNullOrEmpty(_selectedSiteId))
        {
            var selectedSite = _world.Sites.FirstOrDefault(s => string.Equals(s.SiteId, _selectedSiteId, StringComparison.Ordinal));
            if (selectedSite != null) selectedNominal = NominalSiteRange(selectedSite);
        }
        if (_selected is { Q: >= 0 } selectedHex)
        {
            var selectedFlag = _world.FactionFlags.FirstOrDefault(f => f.AnchorQ == selectedHex.Q && f.AnchorR == selectedHex.R);
            if (selectedFlag != null) selectedNominal = NominalFlagRange(selectedFlag);
        }
        if (selectedNominal != null)
        {
            var nominalPen = new Pen(Brushes.White, Math.Max(.04, radius * .08)) { DashStyle = DashStyles.Dash };
            nominalPen.Freeze();
            foreach (var hex in selectedNominal)
            {
                HexWorldLayoutShared.CoordToWorldCenter(hex, _world.HexSize, out var x, out var y);
                dc.DrawGeometry(null, nominalPen, BuildHexGeometry(x, y, radius));
            }
        }
        foreach (var flag in _world.FactionFlags)
        {
            if (!FactionColor(flag.FactionId, out var color)) color = Colors.White;
            HexWorldLayoutShared.CoordToWorldCenter(new HexCoordDto(flag.AnchorQ, flag.AnchorR), _world.HexSize, out var x, out var y);
            var brush = new SolidColorBrush(Color.FromArgb(235, color.R, color.G, color.B)); brush.Freeze();
            var pole = new Pen(brush, Math.Max(.04, radius * .09)); pole.Freeze();
            dc.DrawLine(pole, new Point(x, y - radius * .55), new Point(x, y + radius * .55));
            dc.DrawRectangle(brush, null, new Rect(x, y - radius * .55, radius * .55, radius * .34));
        }
    }

    IEnumerable<HexCoordDto> NominalSiteRange(HexWorldSiteDto site)
    {
        var world = _world!;
        var set = new HashSet<HexCoordDto>();
        foreach (var hex in HexWorldFootprintRules.ResolveFootprint(site))
        {
            if (hex.Q >= 0 && hex.R >= 0 && hex.Q < world.Width && hex.R < world.Height) set.Add(hex);
            for (var d = 0; d < 6; d++)
            {
                var n = HexWorldLayoutShared.Neighbor(hex, d);
                if (n.Q >= 0 && n.R >= 0 && n.Q < world.Width && n.R < world.Height) set.Add(n);
            }
        }
        return set;
    }

    IEnumerable<HexCoordDto> NominalFlagRange(HexWorldFactionFlagDto flag)
    {
        var anchor = new HexCoordDto(flag.AnchorQ, flag.AnchorR);
        var set = new HashSet<HexCoordDto> { anchor };
        for (var d = 0; d < 6; d++)
        {
            var n = HexWorldLayoutShared.Neighbor(anchor, d);
            if (n.Q >= 0 && n.R >= 0 && n.Q < _world!.Width && n.R < _world.Height) set.Add(n);
        }
        return set;
    }

    void RedrawBrushPreview()
    {
        if (_world == null || _viewport == null) return;
        using var dc = _brushPreviewVisual.RenderOpen();
        if (_brushPreviewHexes == null || _brushPreviewHexes.Count == 0 || !_territoryVisible) return;
        var radius = Math.Max(.05, _world.HexSize * CellFillScale);
        var fill = new SolidColorBrush(Color.FromArgb(90, _brushPreviewColor.R, _brushPreviewColor.G, _brushPreviewColor.B));
        fill.Freeze();
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(230, _brushPreviewColor.R, _brushPreviewColor.G, _brushPreviewColor.B)), .05);
        pen.Freeze();
        foreach (var hex in _brushPreviewHexes)
        {
            if (hex.Q < 0 || hex.Q >= _world.Width || hex.R < 0 || hex.R >= _world.Height) continue;
            HexWorldLayoutShared.CoordToWorldCenter(hex, _world.HexSize, out var x, out var y);
            dc.DrawGeometry(fill, pen, BuildHexGeometry(x, y, radius));
        }
    }

    bool FactionColor(string factionId, out Color color)
    {
        if (_factionColorById.TryGetValue(factionId, out var hex))
        {
            try
            {
                color = (Color)ColorConverter.ConvertFromString(hex);
                return true;
            }
            catch
            {
                // fallthrough to neutral
            }
        }

        color = Color.FromRgb(0xB3, 0x94, 0x5C);
        return false;
    }

    void RedrawLabelOverlay(double hexSize)
    {
        if (_world == null || _viewport == null)
            return;

        using var dc = _labelOverlayVisual.RenderOpen();
        if (_viewport.Scale < LabelMinScale)
            return;

        var labelBrush = GetBrush(0xE8, 0xE4, 0xDC);
        var haloBrush = GetBrush(0x1A, 0x18, 0x14);
        var fontSize = Math.Clamp(_viewport.Scale * 2.4, 10.0, 14.0);
        var hexScreenRadius = hexSize * _viewport.Scale * CellFillScale;

        foreach (var site in _world.Sites)
        {
            var name = site.DisplayName;
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var (sx, sy) = _viewport.ProjectHexCenter(new HexCoordDto(site.AnchorQ, site.AnchorR));
            var text = new FormattedText(
                name,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                fontSize,
                labelBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            var padX = 4.0;
            var padY = 2.0;
            var labelX = sx - text.Width * 0.5;
            var labelY = sy - hexScreenRadius * 2.0 - text.Height - padY;
            if (labelY < 2.0)
                labelY = sy + hexScreenRadius * 1.2;
            var bg = new Rect(labelX - padX, labelY - padY, text.Width + padX * 2, text.Height + padY * 2);
            dc.DrawRoundedRectangle(haloBrush, null, bg, 3, 3);
            dc.DrawText(text, new Point(labelX, labelY));
        }

        EnsureLabelOverlayOnTop();
    }

    void EnsureLabelOverlayOnTop()
    {
        if (_visuals.Contains(_labelOverlayVisual))
        {
            _visuals.Remove(_labelOverlayVisual);
            _visuals.Add(_labelOverlayVisual);
        }
    }

    void DrawSiteIconUpright(DrawingContext dc, double cx, double cy, double size)
    {
        dc.PushTransform(new MatrixTransform(new Matrix(1, 0, 0, -1, 0, 2 * cy)));
        DrawSiteIcon(dc, cx, cy, size);
        dc.Pop();
    }

    void DrawSiteIcon(DrawingContext dc, double cx, double cy, double size)
    {
        var bodyBrush = GetBrush(0xD6, 0xB8, 0x80);
        var roofBrush = GetBrush(0x8F, 0x52, 0x33);
        var stroke = new Pen(roofBrush, size * 0.02);
        stroke.Freeze();
        var body = new Rect(cx - size * 0.275, cy - size * 0.35 * 0.5, size * 0.55, size * 0.45);
        dc.DrawRectangle(bodyBrush, stroke, body);

        var roof = new StreamGeometry();
        using (var ctx = roof.Open())
        {
            ctx.BeginFigure(new Point(cx, cy - size * 0.55), true, true);
            ctx.LineTo(new Point(cx - size * 0.34, cy - size * 0.18), true, false);
            ctx.LineTo(new Point(cx + size * 0.34, cy - size * 0.18), true, false);
        }

        roof.Freeze();
        dc.DrawGeometry(roofBrush, null, roof);
    }

    SolidColorBrush GetBrush(byte r, byte g, byte b)
    {
        var key = (r << 16) | (g << 8) | b;
        if (_brushCache.TryGetValue(key, out var brush))
            return brush;
        brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        _brushCache[key] = brush;
        return brush;
    }

    static StreamGeometry BuildHexGeometry(double cx, double cy, double radius)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            for (var i = 0; i < 6; i++)
            {
                var angle = Math.PI / 3.0 * i + Math.PI / 6.0;
                var p = new Point(cx + radius * Math.Cos(angle), cy + radius * Math.Sin(angle));
                if (i == 0)
                    ctx.BeginFigure(p, isFilled: true, isClosed: true);
                else
                    ctx.LineTo(p, isStroked: true, isSmoothJoin: false);
            }
        }

        geometry.Freeze();
        return geometry;
    }
}
