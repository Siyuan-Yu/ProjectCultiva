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
    readonly Dictionary<(int Q, int R), string> _territoryByHex = new();
    readonly Dictionary<(int Q, int R), string> _derivedSiteTerritoryOwnerByHex = new();

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

    /// <summary>Title / inspector refresh. Does not imply full map geometry rebuild.</summary>
    public event Action? Changed;

    /// <summary>Terrain/road cells mutated — rebuild only affected render chunks.</summary>
    public event Action<IReadOnlyList<(int Q, int R)>>? CellsMutated;

    /// <summary>World instance replaced (load / undo / redo / new) — full map rebuild.</summary>
    public event Action? WorldReplaced;

    /// <summary>Site footprint / selection changed — overlay refresh only.</summary>
    public event Action? SitesMutated;
    public event Action? TerritoriesMutated;

    public string? LastFootprintEditMessage { get; private set; } = string.Empty;

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
        WorldReplaced?.Invoke();
        RebuildTerritoryLookup();
        Notify();
    }

    public void Load(HexWorldDefinitionDto world, string? path)
    {
        _undo.Clear();
        _redo.Clear();
        World = world;
        RebuildTerritoryLookup();
        FilePath = path;
        MarkDirty(false);
        WorldReplaced?.Invoke();
        Notify();
    }

    public void Save(string path)
    {
        HexWorldContentJson.NormalizeForSave(World);
        HexWorldContentJson.SaveFile(path, World);
        FilePath = path;
        MarkDirty(false);
        Notify();
    }

    public void MarkDirty(bool dirty = true)
    {
        IsDirty = dirty;
        Changed?.Invoke();
    }

    public void Notify() => Changed?.Invoke();

    public void PushUndo()
    {
        PushUndoFromSnapshot(HexWorldContentJson.Serialize(World));
    }

    void PushUndoFromSnapshot(string snapshot)
    {
        _undo.Push(snapshot);
        _redo.Clear();
    }

    public bool Undo()
    {
        if (_undo.Count == 0)
            return false;
        _redo.Push(HexWorldContentJson.Serialize(World));
        World = HexWorldContentJson.Load(_undo.Pop()).Definitions[0];
        RebuildTerritoryLookup();
        IsDirty = true;
        WorldReplaced?.Invoke();
        Notify();
        return true;
    }

    public bool Redo()
    {
        if (_redo.Count == 0)
            return false;
        _undo.Push(HexWorldContentJson.Serialize(World));
        World = HexWorldContentJson.Load(_redo.Pop()).Definitions[0];
        RebuildTerritoryLookup();
        IsDirty = true;
        WorldReplaced?.Invoke();
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
        var touched = new List<(int Q, int R)>();
        foreach (var hex in CollectBrushHexes(center))
        {
            HexWorldContentGenerator.SetTerrain(World, hex.Q, hex.R, ActiveTerrain);
            touched.Add((hex.Q, hex.R));
        }

        RaiseCellsMutated(touched);
    }

    public void ApplyRoadBrush(HexCoordDto center, bool singleStrokeUndo = true)
    {
        if (singleStrokeUndo)
            PushUndo();
        var touched = new List<(int Q, int R)>();
        foreach (var hex in CollectBrushHexes(center))
        {
            HexWorldContentGenerator.PaintRoadTile(World, hex.Q, hex.R);
            touched.Add((hex.Q, hex.R));
        }

        RaiseCellsMutated(touched);
    }

    public void ApplyEraseBrush(HexCoordDto center, bool singleStrokeUndo = true)
    {
        if (singleStrokeUndo)
            PushUndo();
        var touched = new List<(int Q, int R)>();
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
            touched.Add((hex.Q, hex.R));
        }

        RaiseCellsMutated(touched);
    }

    public HexWorldSiteDto? FindSiteAt(HexCoordDto hex)
    {
        foreach (var site in World.Sites)
        {
            var footprint = site.Footprint.Count > 0
                ? site.Footprint
                : new List<HexCoordDto> { new(site.AnchorQ, site.AnchorR) };
            foreach (var h in footprint)
            {
                if (h.Q == hex.Q && h.R == hex.R)
                    return site;
            }
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
            PresenceQ = hex.Q,
            PresenceR = hex.R,
            Footprint = new List<HexCoordDto> { hex },
            ControlEstablishedOrder = NextControlEstablishedOrder(),
        };
        World.Sites.Add(site);
        RebuildDerivedSiteTerritoryOwnerLookup();
        SelectedSiteId = id;
        RaiseSitesMutated();
        return site;
    }

    public void DeleteSite(string siteId)
    {
        PushUndo();
        var removed = World.Sites.FirstOrDefault(s => string.Equals(s.SiteId, siteId, StringComparison.Ordinal));
        World.Sites.RemoveAll(s => string.Equals(s.SiteId, siteId, StringComparison.Ordinal));
        if (!string.IsNullOrEmpty(removed?.TerritoryRegionId))
            World.TerritoryRegions.RemoveAll(r => string.Equals(r.RegionId, removed.TerritoryRegionId, StringComparison.Ordinal));
        RebuildTerritoryLookup();
        if (string.Equals(SelectedSiteId, siteId, StringComparison.Ordinal))
            SelectedSiteId = null;
        IsDirty = true;
        WorldReplaced?.Invoke();
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
            var moved = new List<HexCoordDto>(site.Footprint.Count);
            foreach (var h in site.Footprint)
                moved.Add(new HexCoordDto(h.Q + dq, h.R + dr));
            site.Footprint = moved;
        }

        HexWorldPresenceRules.SyncPresenceToAnchor(site);
        RebuildDerivedTerritoryForSite(siteId);

        var touched = new List<(int Q, int R)>();
        foreach (var hex in site.Footprint)
            touched.Add((hex.Q, hex.R));

        RaiseSitesMutated(touched);
    }

    public FootprintEditResult ToggleFootprintHex(string siteId, HexCoordDto hex, bool add)
    {
        var site = World.Sites.FirstOrDefault(s => string.Equals(s.SiteId, siteId, StringComparison.Ordinal));
        if (site == null)
            return FootprintEditResult.Fail("未找到 WorldSite。");

        var snapshot = HexWorldContentJson.Serialize(World);
        FootprintEditResult result;
        if (add)
            result = HexWorldEditorFootprintService.TryAddFootprintHex(site, hex, World);
        else
            result = HexWorldEditorFootprintService.TryRemoveFootprintHex(site, hex);

        if (!result.Success)
        {
            World = HexWorldContentJson.Load(snapshot).Definitions[0];
            LastFootprintEditMessage = result.Message;
            Notify();
            return result;
        }

        PushUndoFromSnapshot(snapshot);
        RebuildDerivedTerritoryForSite(siteId);
        LastFootprintEditMessage = result.Message;
        RaiseSitesMutated(new[] { (hex.Q, hex.R) });
        return result;
    }

    public FootprintEditResult SetSiteAnchor(string siteId, HexCoordDto hex)
    {
        var site = World.Sites.FirstOrDefault(s => string.Equals(s.SiteId, siteId, StringComparison.Ordinal));
        if (site == null)
            return FootprintEditResult.Fail("未找到 WorldSite。");

        var snapshot = HexWorldContentJson.Serialize(World);
        var result = HexWorldEditorFootprintService.TrySetAnchorHex(site, hex);
        if (!result.Success)
        {
            World = HexWorldContentJson.Load(snapshot).Definitions[0];
            LastFootprintEditMessage = result.Message;
            Notify();
            return result;
        }

        PushUndoFromSnapshot(snapshot);
        RebuildDerivedTerritoryForSite(siteId);
        LastFootprintEditMessage = result.Message;
        RaiseSitesMutated(new[] { (hex.Q, hex.R) });
        return result;
    }

    public FootprintEditResult SetSitePresence(string siteId, HexCoordDto hex)
    {
        var site = World.Sites.FirstOrDefault(s => string.Equals(s.SiteId, siteId, StringComparison.Ordinal));
        if (site == null)
            return FootprintEditResult.Fail("未找到 WorldSite。");

        var snapshot = HexWorldContentJson.Serialize(World);
        var result = HexWorldEditorFootprintService.TrySetPresenceHex(site, hex);
        if (!result.Success)
        {
            World = HexWorldContentJson.Load(snapshot).Definitions[0];
            LastFootprintEditMessage = result.Message;
            Notify();
            return result;
        }

        PushUndoFromSnapshot(snapshot);
        LastFootprintEditMessage = result.Message;
        RaiseSitesMutated(new[] { (hex.Q, hex.R) });
        return result;
    }

    public HexWorldSiteDto? GetSelectedSite()
    {
        if (string.IsNullOrEmpty(SelectedSiteId))
            return null;
        return World.Sites.FirstOrDefault(s => string.Equals(s.SiteId, SelectedSiteId, StringComparison.Ordinal));
    }

    public HexWorldTerritoryRegionDto? GetTerritoryRegion(string regionId) =>
        World.TerritoryRegions.FirstOrDefault(r => string.Equals(r.RegionId, regionId, StringComparison.Ordinal));

    public HexWorldTerritoryRegionDto? GetTerritoryForSite(string siteId)
    {
        var site = World.Sites.FirstOrDefault(s => s.SiteId == siteId);
        return string.IsNullOrEmpty(site?.TerritoryRegionId) ? null : GetTerritoryRegion(site.TerritoryRegionId);
    }

    public HexWorldTerritoryRegionDto? FindTerritoryAt(HexCoordDto hex) =>
        _territoryByHex.TryGetValue((hex.Q, hex.R), out var id) ? GetTerritoryRegion(id) : null;

    public HexWorldSiteDto? FindFootprintOwnerAt(HexCoordDto hex) => FindSiteAt(hex);

    /// <summary>编辑期默认 Site 辖区：完整 Footprint 加其全部 Odd-R 一格邻居。</summary>
    public IReadOnlyCollection<HexCoordDto> ComputeDefaultSiteTerritory(HexWorldSiteDto site)
    {
        var result = new HashSet<HexCoordDto>(HexWorldFootprintRules.ResolveFootprint(site));
        foreach (var hex in HexWorldFootprintRules.ResolveFootprint(site))
            for (var d = 0; d < 6; d++)
            {
                var n = HexWorldLayoutShared.Neighbor(hex, d);
                if (n.Q >= 0 && n.R >= 0 && n.Q < World.Width && n.R < World.Height) result.Add(n);
            }
        return result;
    }

    public HexWorldSiteDto? TryResolveDefaultSiteTerritoryAtHex(HexCoordDto hex) =>
        _derivedSiteTerritoryOwnerByHex.TryGetValue((hex.Q, hex.R), out var id)
            ? World.Sites.FirstOrDefault(site => site.SiteId == id) : null;

    /// <summary>该 Hex 是否处于某 WorldSite 的「默认辖区」（footprint ∪ 外一圈，编辑期派生）。</summary>
    public bool IsInDefaultSiteTerritory(HexCoordDto hex) =>
        _derivedSiteTerritoryOwnerByHex.ContainsKey((hex.Q, hex.R));

    public HexWorldStandaloneHexControlDto? FindStandaloneAt(HexCoordDto hex) =>
        World.StandaloneTerritoryHexes.FirstOrDefault(c => c.Q == hex.Q && c.R == hex.R);

    /// <summary>当前 Hex 固化属于哪个 Region（若其位于某固化 Region 的 Hex 列表中）。</summary>
    public HexWorldTerritoryRegionDto? FindFrozenRegionAt(HexCoordDto hex) =>
        _territoryByHex.TryGetValue((hex.Q, hex.R), out var regionId)
            ? GetTerritoryRegion(regionId) : null;

    /// <summary>
    /// 统一 Territory 涂刷入口（WorldGraphEditor Territory Brush / 测试共用）：
    /// 命中 WorldSite 默认辖区 → 整片 footprint+ring macro；
    /// 否则 → 单格 standalone（不属于任何固化 Region 才允许）。
    /// 不在此处 PushUndo —— 一次 stroke 的 undo 由 UI 层控制（singleStrokeUndo 参数仍保留给直接调用方）。
    /// </summary>
    public TerritoryStrokeResult PaintTerritory(HexCoordDto hex, string factionId, bool singleStrokeUndo = true)
    {
        if (string.IsNullOrWhiteSpace(factionId))
            return TerritoryStrokeResult.Fail("请先选择势力。");
        if (hex.Q < 0 || hex.R < 0 || hex.Q >= World.Width || hex.R >= World.Height)
            return TerritoryStrokeResult.Fail($"Hex ({hex.Q},{hex.R}) 越界。");

        var defaultSiteId = _derivedSiteTerritoryOwnerByHex.TryGetValue((hex.Q, hex.R), out var ds) ? ds : null;
        if (defaultSiteId != null)
        {
            // 冲突防护：hex 已固化属于其它 Region（非本 Site 默认辖区内容），禁止抢走。
            var frozen = _territoryByHex.TryGetValue((hex.Q, hex.R), out var frozenRegionId) ? frozenRegionId : null;
            var siteRegionId = World.Sites.FirstOrDefault(s => s.SiteId == defaultSiteId)?.TerritoryRegionId;
            if (frozen != null && !string.Equals(frozen, siteRegionId, StringComparison.Ordinal))
                return TerritoryStrokeResult.Fail(
                    $"Hex ({hex.Q},{hex.R}) 已固化属于 Region '{frozen}'，不能并入「{defaultSiteId}」默认辖区。");

            var result = AssignFactionToSiteTerritory(defaultSiteId, factionId, singleStrokeUndo);
            if (!result.Success)
                return TerritoryStrokeResult.Fail(result.Message);
            return TerritoryStrokeResult.SiteMacro(defaultSiteId, result.Message);
        }

        // 普通荒野 Hex：单格涂；不得与任何固化 Region 重叠。
        if (_territoryByHex.ContainsKey((hex.Q, hex.R)))
            return TerritoryStrokeResult.Fail(
                $"Hex ({hex.Q},{hex.R}) 属于固化 TerritoryRegion，不能作为独立势力范围单格覆盖。");
        if (FindFootprintOwnerAt(hex) != null)
            return TerritoryStrokeResult.Fail(
                $"Hex ({hex.Q},{hex.R}) 是 WorldSite Footprint，必须整片辖区一起涂。");

        if (singleStrokeUndo)
            PushUndo();
        var existing = FindStandaloneAt(hex);
        if (existing != null)
            existing.ControlFactionId = factionId ?? string.Empty;
        else
            World.StandaloneTerritoryHexes.Add(new HexWorldStandaloneHexControlDto
            {
                Q = hex.Q,
                R = hex.R,
                ControlFactionId = factionId ?? string.Empty,
            });
        RaiseTerritoriesMutated();
        return TerritoryStrokeResult.Standalone($"Hex ({hex.Q},{hex.R}) → {factionId}");
    }

    /// <summary>统一擦除：WorldSite 默认辖区 → 整片置无主（保留 Region 结构）；荒野 standalone → 移除；固化 Region 非默认辖区 → 拒绝。</summary>
    public TerritoryStrokeResult EraseTerritory(HexCoordDto hex, bool singleStrokeUndo = true)
    {
        if (hex.Q < 0 || hex.R < 0 || hex.Q >= World.Width || hex.R >= World.Height)
            return TerritoryStrokeResult.Fail($"Hex ({hex.Q},{hex.R}) 越界。");

        var defaultSiteId = _derivedSiteTerritoryOwnerByHex.TryGetValue((hex.Q, hex.R), out var ds) ? ds : null;
        if (defaultSiteId != null)
        {
            var frozen = _territoryByHex.TryGetValue((hex.Q, hex.R), out var frozenRegionId) ? frozenRegionId : null;
            var siteRegionId = World.Sites.FirstOrDefault(s => s.SiteId == defaultSiteId)?.TerritoryRegionId;
            if (frozen != null && !string.Equals(frozen, siteRegionId, StringComparison.Ordinal))
                return TerritoryStrokeResult.Fail(
                    $"Hex ({hex.Q},{hex.R}) 已固化属于 Region '{frozen}'，不是「{defaultSiteId}」默认辖区，不能擦除。");

            var result = AssignFactionToSiteTerritory(defaultSiteId, string.Empty, singleStrokeUndo);
            if (!result.Success)
                return TerritoryStrokeResult.Fail(result.Message);
            return TerritoryStrokeResult.SiteCleared(defaultSiteId, result.Message);
        }

        if (FindStandaloneAt(hex) == null)
            return TerritoryStrokeResult.StandaloneCleared($"Hex ({hex.Q},{hex.R}) 当前已无势力控制。");
        if (_territoryByHex.ContainsKey((hex.Q, hex.R)))
            return TerritoryStrokeResult.Fail(
                $"Hex ({hex.Q},{hex.R}) 属于固化 TerritoryRegion，不能作为独立势力范围擦除。");
        if (singleStrokeUndo)
            PushUndo();
        World.StandaloneTerritoryHexes.RemoveAll(c => c.Q == hex.Q && c.R == hex.R);
        RaiseTerritoriesMutated();
        return TerritoryStrokeResult.StandaloneCleared($"Hex ({hex.Q},{hex.R}) 已清除势力范围。");
    }

    public FootprintEditResult AssignFactionToSiteTerritory(string siteId, string factionId, bool singleStrokeUndo = true)
    {
        var site = World.Sites.FirstOrDefault(s => s.SiteId == siteId);
        if (site == null) return FootprintEditResult.Fail("未找到 WorldSite。");
        var region = GetTerritoryForSite(siteId);
        if (region != null && !string.Equals(region.PrimaryWorldSiteId, siteId, StringComparison.Ordinal))
            return FootprintEditResult.Fail(
                $"Site '{siteId}' 引用的 Region '{region.RegionId}' PrimaryWorldSiteId 不是该 Site，拒绝写入（数据冲突）。");
        if (singleStrokeUndo) PushUndo();
        if (region == null)
        {
            var suffix = siteId.Contains(':') ? siteId[(siteId.IndexOf(':') + 1)..].Replace("site_", "", StringComparison.Ordinal) : siteId;
            var regionId = "base:territory_" + suffix;
            if (GetTerritoryRegion(regionId) != null) return FootprintEditResult.Fail("自动生成 RegionId 与现有 Region 冲突。");
            site.TerritoryRegionId = regionId;
            region = new HexWorldTerritoryRegionDto { RegionId = regionId, PrimaryWorldSiteId = siteId };
            World.TerritoryRegions.Add(region);
        }

        // Footprint 冲突防护：Site footprint 若已固化属于其它 Region（非本 site Region），拒绝抢走。
        foreach (var hex in HexWorldFootprintRules.ResolveFootprint(site))
        {
            if (_territoryByHex.TryGetValue((hex.Q, hex.R), out var owner) &&
                !string.Equals(owner, region.RegionId, StringComparison.Ordinal))
                return FootprintEditResult.Fail(
                    $"Site '{site.DisplayName}' footprint hex ({hex.Q},{hex.R}) 已固化属于 Region '{owner}'，不能覆盖。");
        }

        site.OwnerFactionId = factionId ?? string.Empty;
        region.PrimaryWorldSiteId = siteId;
        region.ControlFactionId = factionId ?? string.Empty;
        region.Hexes = ComputeDefaultSiteTerritory(site).OrderBy(h => h.R).ThenBy(h => h.Q).ToList();
        RebuildTerritoryLookup();
        RaiseSitesMutated(); RaiseTerritoriesMutated();
        var controllerName = string.IsNullOrWhiteSpace(factionId) ? "无势力" : factionId;
        return FootprintEditResult.Ok($"已将「{site.DisplayName}」及其默认辖区（{region.Hexes.Count} Hex）设为「{controllerName}」。");
    }

    public void RebuildDerivedTerritoryForSite(string siteId)
    {
        var site = World.Sites.FirstOrDefault(s => s.SiteId == siteId); var region = site == null ? null : GetTerritoryForSite(siteId);
        if (site == null || region == null) { RebuildDerivedSiteTerritoryOwnerLookup(); return; }
        region.Hexes = ComputeDefaultSiteTerritory(site).OrderBy(h => h.R).ThenBy(h => h.Q).ToList();
        region.ControlFactionId = site.OwnerFactionId ?? string.Empty;
        RebuildTerritoryLookup(); RebuildDerivedSiteTerritoryOwnerLookup(); RaiseTerritoriesMutated();
    }

    public FootprintEditResult AssignTerritoryHex(string regionId, HexCoordDto hex, bool singleStrokeUndo = true)
    {
        var target = GetTerritoryRegion(regionId);
        if (target == null) return FootprintEditResult.Fail("未找到 TerritoryRegion。");
        if (hex.Q < 0 || hex.R < 0 || hex.Q >= World.Width || hex.R >= World.Height) return FootprintEditResult.Fail("Hex 越界。");
        var footprint = FindFootprintOwnerAt(hex);
        if (footprint != null && !string.Equals(footprint.SiteId, target.PrimaryWorldSiteId, StringComparison.Ordinal))
            return FootprintEditResult.Fail($"拒绝：Hex ({hex.Q},{hex.R}) 是「{footprint.DisplayName}」Footprint。");
        if (_territoryByHex.TryGetValue((hex.Q, hex.R), out var old) && old == regionId) return FootprintEditResult.Fail("该 Hex 已属于当前 Territory。");
        if (singleStrokeUndo) PushUndo();
        if (!string.IsNullOrEmpty(old)) GetTerritoryRegion(old)?.Hexes.RemoveAll(h => h.Equals(hex));
        target.Hexes.Add(hex); _territoryByHex[(hex.Q, hex.R)] = regionId;
        RaiseTerritoriesMutated();
        return FootprintEditResult.Ok($"Hex ({hex.Q},{hex.R}) → {regionId}");
    }

    public FootprintEditResult RemoveTerritoryHex(HexCoordDto hex, bool singleStrokeUndo = true)
    {
        if (!_territoryByHex.TryGetValue((hex.Q, hex.R), out var id)) return FootprintEditResult.Fail("该 Hex 没有 Territory。");
        var region = GetTerritoryRegion(id); if (region == null) return FootprintEditResult.Fail("Territory 数据错误。");
        var footprint = FindFootprintOwnerAt(hex);
        if (footprint != null) return FootprintEditResult.Fail("WorldSite footprint 必须属于自己的 TerritoryRegion，不能擦除。");
        if (singleStrokeUndo) PushUndo(); region.Hexes.RemoveAll(h => h.Equals(hex)); _territoryByHex.Remove((hex.Q, hex.R));
        RaiseTerritoriesMutated(); return FootprintEditResult.Ok($"已擦除 Hex ({hex.Q},{hex.R}) Territory。");
    }

    public FootprintEditResult EnsureSiteFootprintInTerritory(string regionId)
    {
        var region = GetTerritoryRegion(regionId); var site = region == null ? null : World.Sites.FirstOrDefault(s => s.SiteId == region.PrimaryWorldSiteId);
        if (region == null || site == null) return FootprintEditResult.Fail("未找到 Region 对应 WorldSite。");
        var conflicts = HexWorldFootprintRules.ResolveFootprint(site).Where(h => _territoryByHex.TryGetValue((h.Q, h.R), out var owner) && owner != regionId).ToList();
        if (conflicts.Count > 0) return FootprintEditResult.Fail("Footprint 已属于其它 Region，请手工处理冲突：" + string.Join("、", conflicts));
        PushUndo(); foreach (var hex in HexWorldFootprintRules.ResolveFootprint(site)) { if (!region.Hexes.Contains(hex)) region.Hexes.Add(hex); _territoryByHex[(hex.Q, hex.R)] = regionId; }
        RaiseTerritoriesMutated(); return FootprintEditResult.Ok("已补齐 Site Footprint。");
    }

    public FootprintEditResult ExpandTerritoryOneRing(string regionId)
    {
        var region = GetTerritoryRegion(regionId); var site = region == null ? null : World.Sites.FirstOrDefault(s => s.SiteId == region.PrimaryWorldSiteId);
        if (region == null || site == null) return FootprintEditResult.Fail("未找到 Region 对应 WorldSite。");
        var wanted = new HashSet<HexCoordDto>(HexWorldFootprintRules.ResolveFootprint(site));
        foreach (var h in HexWorldFootprintRules.ResolveFootprint(site)) for (var d = 0; d < 6; d++) { var n = HexWorldLayoutShared.Neighbor(h, d); if (n.Q >= 0 && n.R >= 0 && n.Q < World.Width && n.R < World.Height) wanted.Add(n); }
        var added = 0; var conflicts = 0; PushUndo();
        foreach (var h in wanted) { if (FindFootprintOwnerAt(h) is { } other && other.SiteId != site.SiteId) { conflicts++; continue; } if (_territoryByHex.TryGetValue((h.Q, h.R), out var owner) && owner != regionId) { conflicts++; continue; } if (region.Hexes.Contains(h)) continue; region.Hexes.Add(h); _territoryByHex[(h.Q, h.R)] = regionId; added++; }
        if (added == 0) { Undo(); return FootprintEditResult.Fail($"外围一圈未新增；冲突 {conflicts}。"); }
        RaiseTerritoriesMutated(); return FootprintEditResult.Ok($"外围一圈完成：新增 {added}，冲突 {conflicts}（未修改）。");
    }

    public FootprintEditResult CreateTerritoryForSite(string siteId, string regionId)
    {
        var site = World.Sites.FirstOrDefault(s => s.SiteId == siteId); if (site == null) return FootprintEditResult.Fail("未找到 WorldSite。");
        if (!string.IsNullOrEmpty(site.TerritoryRegionId) || GetTerritoryRegion(regionId) != null) return FootprintEditResult.Fail("Site 已有 Territory 或 RegionId 已存在。");
        foreach (var h in HexWorldFootprintRules.ResolveFootprint(site)) if (_territoryByHex.ContainsKey((h.Q, h.R))) return FootprintEditResult.Fail("Footprint 已有 Territory 冲突，请先手工处理。");
        PushUndo(); site.TerritoryRegionId = regionId; var region = new HexWorldTerritoryRegionDto { RegionId = regionId, PrimaryWorldSiteId = siteId, ControlFactionId = site.OwnerFactionId, Hexes = HexWorldFootprintRules.ResolveFootprint(site).ToList() }; World.TerritoryRegions.Add(region); RebuildTerritoryLookup(); RaiseTerritoriesMutated(); return FootprintEditResult.Ok("已创建 TerritoryRegion。");
    }

    public void RenameSelectedSite(string oldId, string newId, string displayName, string siteType, string localMapId,
        string? ownerFactionId = null, long? controlEstablishedOrder = null)
    {
        var site = World.Sites.FirstOrDefault(s => s.SiteId == oldId); if (site == null || string.IsNullOrWhiteSpace(newId)) return;
        PushUndo(); if (site.TerritoryRegionId is { Length: > 0 } id && GetTerritoryRegion(id) is { } region) region.PrimaryWorldSiteId = newId;
        site.SiteId = newId; site.DisplayName = displayName; site.SiteType = siteType; site.LocalMapId = localMapId;
        if (ownerFactionId != null) site.OwnerFactionId = ownerFactionId;
        if (controlEstablishedOrder.HasValue && controlEstablishedOrder.Value > 0) site.ControlEstablishedOrder = controlEstablishedOrder.Value;
        if (site.TerritoryRegionId is { Length: > 0 } regionId && GetTerritoryRegion(regionId) is { } ownedRegion)
            ownedRegion.ControlFactionId = site.OwnerFactionId;
        SelectedSiteId = newId; RaiseSitesMutated(); RaiseTerritoriesMutated();
    }

    public long NextControlEstablishedOrder()
    {
        var siteMax = World.Sites.Count == 0 ? 0 : World.Sites.Max(s => s.ControlEstablishedOrder);
        var flagMax = World.FactionFlags.Count == 0 ? 0 : World.FactionFlags.Max(f => f.EstablishedOrder);
        return Math.Max(siteMax, flagMax) + 1;
    }

    public FactionFlagCreateResult CreateFactionFlag(HexCoordDto anchor, string flagId, string factionId,
        float localX = 0f, float localZ = 0f, bool hasLocalPosition = false, long? establishedOrder = null)
    {
        if (string.IsNullOrWhiteSpace(flagId))
            return FactionFlagCreateResult.Fail("无法建立阵营旗：FlagId 为空。");
        var duplicate = World.FactionFlags.FirstOrDefault(f => string.Equals(f.FlagId, flagId, StringComparison.Ordinal));
        if (duplicate != null)
            return FactionFlagCreateResult.Fail(
                $"无法建立阵营旗：FlagId '{flagId}' 已存在。现有位置：({duplicate.AnchorQ},{duplicate.AnchorR})。");
        var atAnchor = World.FactionFlags.FirstOrDefault(f => f.AnchorQ == anchor.Q && f.AnchorR == anchor.R);
        if (atAnchor != null)
            return FactionFlagCreateResult.Fail(
                $"当前 Hex ({anchor.Q},{anchor.R}) 已存在 FactionFlag '{atAnchor.FlagId}'。");
        var cell = World.Cells.FirstOrDefault(c => c.Q == anchor.Q && c.R == anchor.R);
        if (cell == null)
            return FactionFlagCreateResult.Fail($"无法建立阵营旗：Anchor ({anchor.Q},{anchor.R}) 越界或缺少 Cell 数据。");
        var passable = cell.Passable ?? HexTerrainPalette.DefaultPassable(cell.Terrain);
        if (!passable)
            return FactionFlagCreateResult.Fail(
                $"无法建立阵营旗：Anchor ({anchor.Q},{anchor.R}) Terrain={HexTerrainPalette.ResolveLabel(cell.Terrain)}/{cell.Terrain}, Passable=false。");
        var occupant = HexWorldFootprintRules.FindOccupant(World, anchor);
        if (occupant != null)
            return FactionFlagCreateResult.Fail(
                $"无法建立阵营旗：Anchor ({anchor.Q},{anchor.R}) 位于 WorldSite '{occupant.SiteId}' footprint 内。");

        PushUndo();
        var flag = new HexWorldFactionFlagDto
        {
            FlagId = flagId,
            FactionId = factionId,
            AnchorQ = anchor.Q,
            AnchorR = anchor.R,
            EstablishedOrder = establishedOrder.HasValue && establishedOrder.Value > 0
                ? establishedOrder.Value : NextControlEstablishedOrder(),
            LocalX = localX,
            LocalZ = localZ,
            HasLocalPosition = hasLocalPosition,
        };
        World.FactionFlags.Add(flag);
        RaiseTerritoriesMutated();
        return FactionFlagCreateResult.Ok(flag);
    }

    public bool DeleteFactionFlag(string flagId)
    {
        var flag = World.FactionFlags.FirstOrDefault(f => string.Equals(f.FlagId, flagId, StringComparison.Ordinal));
        if (flag == null) return false;
        PushUndo();
        World.FactionFlags.Remove(flag);
        RaiseTerritoriesMutated();
        return true;
    }

    void RebuildTerritoryLookup()
    {
        _territoryByHex.Clear(); foreach (var region in World.TerritoryRegions) foreach (var hex in region.Hexes) if (!_territoryByHex.ContainsKey((hex.Q, hex.R))) _territoryByHex[(hex.Q, hex.R)] = region.RegionId;
        RebuildDerivedSiteTerritoryOwnerLookup();
    }

    void RebuildDerivedSiteTerritoryOwnerLookup()
    {
        _derivedSiteTerritoryOwnerByHex.Clear();
        foreach (var site in World.Sites)
            foreach (var hex in ComputeDefaultSiteTerritory(site))
                if (!_derivedSiteTerritoryOwnerByHex.ContainsKey((hex.Q, hex.R))) _derivedSiteTerritoryOwnerByHex[(hex.Q, hex.R)] = site.SiteId;
    }

    void RaiseTerritoriesMutated() { IsDirty = true; TerritoriesMutated?.Invoke(); Changed?.Invoke(); }

    void RaiseSitesMutated(IReadOnlyList<(int Q, int R)>? touched = null)
    {
        IsDirty = true;
        if (touched != null && touched.Count > 0)
            CellsMutated?.Invoke(touched);
        SitesMutated?.Invoke();
        Changed?.Invoke();
    }

    void RaiseCellsMutated(IReadOnlyList<(int Q, int R)> touched)
    {
        IsDirty = true;
        CellsMutated?.Invoke(touched);
        Changed?.Invoke();
    }
}
