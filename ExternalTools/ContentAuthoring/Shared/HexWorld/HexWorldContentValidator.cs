using System.Text.Json.Nodes;

namespace ContentAuthoring.Shared.HexWorld;

public sealed class HexWorldValidationIssue
{
    public required string Level { get; init; }
    public required string Message { get; init; }
    public int? Q { get; init; }
    public int? R { get; init; }
    public string? SiteId { get; init; }
    public string? ObjectKind { get; init; }
    public string? ObjectId { get; init; }
}

public static class HexWorldContentValidator
{
    public static List<HexWorldValidationIssue> Validate(HexWorldDefinitionDto world) =>
        Validate(world, factions: null);

    /// <summary>
    /// factions 为 null 时跳过 faction 引用 / territorySelectable 校验（Core roundtrip / 无 Content 场景）。
    /// 编辑器（WorldGraphEditor）必须传入正式 factions 目录。
    /// </summary>
    public static List<HexWorldValidationIssue> Validate(
        HexWorldDefinitionDto world,
        IReadOnlyCollection<StrategicFactionAuthoringDto>? factions)
    {
        var issues = new List<HexWorldValidationIssue>();
        if (world == null)
        {
            issues.Add(Error("Hex world is null."));
            return issues;
        }

        if (world.Width < 10 || world.Width > 500)
            issues.Add(Error($"Width out of range (10–500): {world.Width}."));
        if (world.Height < 10 || world.Height > 500)
            issues.Add(Error($"Height out of range (10–500): {world.Height}."));
        if (!HexTerrainIds.IsKnown(world.DefaultTerrain))
            issues.Add(Error($"Unknown default terrain: {world.DefaultTerrain}."));

        var expectedCells = world.Width * world.Height;
        if (world.Cells.Count != expectedCells)
            issues.Add(Warn($"Cell count {world.Cells.Count} != width*height {expectedCells}."));

        var siteIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var site in world.Sites)
        {
            if (string.IsNullOrWhiteSpace(site.SiteId))
            {
                issues.Add(Error("Site with empty SiteId."));
                continue;
            }

            if (!siteIds.Add(site.SiteId))
                issues.Add(Error($"Duplicate SiteId: {site.SiteId}.", siteId: site.SiteId));

            if (!IsInBounds(world, site.AnchorQ, site.AnchorR))
                issues.Add(Error($"Site anchor out of bounds: {site.SiteId} ({site.AnchorQ},{site.AnchorR}).", site.SiteId, site.AnchorQ, site.AnchorR));

            var footprint = site.Footprint.Count > 0
                ? site.Footprint
                : new List<HexCoordDto> { new(site.AnchorQ, site.AnchorR) };
            var anchorInFootprint = footprint.Any(h => h.Q == site.AnchorQ && h.R == site.AnchorR);
            if (!anchorInFootprint)
                issues.Add(Error($"Site anchor not in footprint: {site.SiteId}.", site.SiteId, site.AnchorQ, site.AnchorR));

            HexWorldPresenceRules.EnsurePresenceDefaults(site);
            var presenceQ = site.PresenceQ!.Value;
            var presenceR = site.PresenceR!.Value;
            if (presenceQ != site.AnchorQ || presenceR != site.AnchorR)
            {
                issues.Add(Warn(
                    $"PresenceHex != AnchorHex for {site.SiteId} ({presenceQ},{presenceR}) vs anchor ({site.AnchorQ},{site.AnchorR}); will be corrected on save/load.",
                    site.SiteId,
                    presenceQ,
                    presenceR));
            }

            if (!footprint.Any(h => h.Q == presenceQ && h.R == presenceR))
            {
                issues.Add(Error(
                    $"Site PresenceHex not in footprint: {site.SiteId} ({presenceQ},{presenceR}).",
                    site.SiteId,
                    presenceQ,
                    presenceR));
            }

            if (!IsInBounds(world, presenceQ, presenceR))
            {
                issues.Add(Error(
                    $"Site PresenceHex out of bounds: {site.SiteId} ({presenceQ},{presenceR}).",
                    site.SiteId,
                    presenceQ,
                    presenceR));
            }

            foreach (var hex in footprint)
            {
                if (!IsInBounds(world, hex.Q, hex.R))
                    issues.Add(Error($"Site footprint out of bounds: {site.SiteId} ({hex.Q},{hex.R}).", site.SiteId, hex.Q, hex.R));
            }

            if (footprint.Count > 1 && !HexWorldFootprintRules.IsConnected(footprint))
                issues.Add(Error($"Site footprint not connected: {site.SiteId}.", site.SiteId, site.AnchorQ, site.AnchorR));
        }

        ValidateFootprintOverlap(world, issues);
        ValidateControlAssets(world, issues);
        ValidateTerritories(world, issues);
        ValidateStandalone(world, issues, factions);
        if (factions != null)
            ValidateFactionReferences(world, issues, factions);

        for (var i = 0; i < world.Cells.Count; i++)
        {
            var cell = world.Cells[i];
            if (!IsInBounds(world, cell.Q, cell.R))
                issues.Add(Error($"Cell out of bounds ({cell.Q},{cell.R}).", q: cell.Q, r: cell.R));
            if (!HexTerrainIds.IsKnown(cell.Terrain))
                issues.Add(Error($"Unknown terrain at ({cell.Q},{cell.R}): {cell.Terrain}.", q: cell.Q, r: cell.R));
        }

        ValidateRoadConnectivity(world, issues);

        return issues;
    }

    static void ValidateStandalone(
        HexWorldDefinitionDto world,
        List<HexWorldValidationIssue> issues,
        IReadOnlyCollection<StrategicFactionAuthoringDto>? factions)
    {
        var byHex = new Dictionary<(int Q, int R), string>();
        var regionByHex = new Dictionary<(int Q, int R), string>();
        foreach (var region in world.TerritoryRegions)
            foreach (var hex in region.Hexes)
                regionByHex[(hex.Q, hex.R)] = region.RegionId;

        foreach (var control in world.StandaloneTerritoryHexes)
        {
            var key = (control.Q, control.R);
            if (!IsInBounds(world, control.Q, control.R))
                issues.Add(Error($"Standalone hex out of bounds ({control.Q},{control.R}).", q: control.Q, r: control.R));
            if (byHex.TryGetValue(key, out _))
                issues.Add(Error($"Duplicate standalone hex ({control.Q},{control.R}).", q: control.Q, r: control.R));
            else
                byHex[key] = control.ControlFactionId;
            if (regionByHex.TryGetValue(key, out var regionId))
                issues.Add(Error($"Standalone hex ({control.Q},{control.R}) overlaps TerritoryRegion '{regionId}'.", q: control.Q, r: control.R));
            if (HexWorldFootprintRules.FindOccupant(world, new HexCoordDto(control.Q, control.R)) != null)
                issues.Add(Error($"Standalone hex ({control.Q},{control.R}) is inside a WorldSite footprint.", q: control.Q, r: control.R));
        }
    }

    static void ValidateFactionReferences(
        HexWorldDefinitionDto world,
        List<HexWorldValidationIssue> issues,
        IReadOnlyCollection<StrategicFactionAuthoringDto> factions)
    {
        var byId = factions.ToDictionary(f => f.Id, StringComparer.Ordinal);
        string? Resolve(string? id) => string.IsNullOrEmpty(id) ? null
            : byId.TryGetValue(id, out var f) ? f.Name : null;

        foreach (var site in world.Sites)
        {
            if (string.IsNullOrWhiteSpace(site.OwnerFactionId))
                continue;
            if (Resolve(site.OwnerFactionId) == null)
                issues.Add(Error($"Site '{site.SiteId}' references unknown faction '{site.OwnerFactionId}'.", site.SiteId));
            else if (!byId[site.OwnerFactionId].TerritorySelectable)
                issues.Add(Error($"Site '{site.SiteId}' owner '{site.OwnerFactionId}' is not territorySelectable.", site.SiteId));
        }

        foreach (var flag in world.FactionFlags)
        {
            if (string.IsNullOrWhiteSpace(flag.FactionId))
                issues.Add(Error($"FactionFlag '{flag.FlagId}' has empty FactionId.", q: flag.AnchorQ, r: flag.AnchorR));
            else if (Resolve(flag.FactionId) == null)
                issues.Add(Error($"FactionFlag '{flag.FlagId}' references unknown faction '{flag.FactionId}'.", q: flag.AnchorQ, r: flag.AnchorR));
            else if (!byId[flag.FactionId].TerritorySelectable)
                issues.Add(Error($"FactionFlag '{flag.FlagId}' faction '{flag.FactionId}' is not territorySelectable.", q: flag.AnchorQ, r: flag.AnchorR));
        }

        foreach (var region in world.TerritoryRegions)
        {
            if (string.IsNullOrWhiteSpace(region.ControlFactionId))
                continue;
            if (Resolve(region.ControlFactionId) == null)
                issues.Add(Error($"TerritoryRegion '{region.RegionId}' references unknown faction '{region.ControlFactionId}'."));
            else if (!byId[region.ControlFactionId].TerritorySelectable)
                issues.Add(Error($"TerritoryRegion '{region.RegionId}' controller '{region.ControlFactionId}' is not territorySelectable."));
        }

        foreach (var control in world.StandaloneTerritoryHexes)
        {
            if (string.IsNullOrWhiteSpace(control.ControlFactionId))
                continue;
            if (Resolve(control.ControlFactionId) == null)
                issues.Add(Error($"Standalone hex ({control.Q},{control.R}) references unknown faction '{control.ControlFactionId}'.", q: control.Q, r: control.R));
            else if (!byId[control.ControlFactionId].TerritorySelectable)
                issues.Add(Error($"Standalone hex ({control.Q},{control.R}) controller '{control.ControlFactionId}' is not territorySelectable.", q: control.Q, r: control.R));
        }
    }

    static void ValidateFootprintOverlap(HexWorldDefinitionDto world, List<HexWorldValidationIssue> issues)
    {
        var owner = new Dictionary<(int Q, int R), string>();
        foreach (var site in world.Sites)
        {
            if (string.IsNullOrWhiteSpace(site.SiteId))
                continue;

            var footprint = site.Footprint.Count > 0
                ? site.Footprint
                : new List<HexCoordDto> { new(site.AnchorQ, site.AnchorR) };
            foreach (var hex in footprint)
            {
                var key = (hex.Q, hex.R);
                if (owner.TryGetValue(key, out var other) && !string.Equals(other, site.SiteId, StringComparison.Ordinal))
                {
                    issues.Add(Error(
                        $"Site footprint overlap: {other} and {site.SiteId} at ({hex.Q},{hex.R}).",
                        site.SiteId,
                        hex.Q,
                        hex.R));
                }
                else
                {
                    owner[key] = site.SiteId;
                }
            }
        }
    }

    static void ValidateControlAssets(HexWorldDefinitionDto world, List<HexWorldValidationIssue> issues)
    {
        var orders = new Dictionary<long, string>();
        void CheckOrder(long order, string id)
        {
            if (order <= 0) { issues.Add(Error($"Control asset '{id}' must have positive established order.")); return; }
            if (orders.TryGetValue(order, out var other))
                issues.Add(Error($"Control asset established order {order} is shared by '{other}' and '{id}'."));
            else orders[order] = id;
        }

        foreach (var site in world.Sites)
            CheckOrder(site.ControlEstablishedOrder, site.SiteId);

        foreach (var group in world.FactionFlags
                     .Where(f => !string.IsNullOrWhiteSpace(f.FlagId))
                     .GroupBy(f => f.FlagId, StringComparer.Ordinal)
                     .Where(g => g.Count() > 1))
        {
            var anchors = string.Join(", ", group.Select(f => $"({f.AnchorQ},{f.AnchorR})"));
            issues.Add(Error($"Duplicate FactionFlag FlagId '{group.Key}': anchors {anchors}."));
        }
        var flagAnchors = new HashSet<(int Q, int R)>();
        var footprints = new HashSet<(int Q, int R)>(world.Sites.SelectMany(HexWorldFootprintRules.ResolveFootprint).Select(h => (h.Q, h.R)));
        foreach (var flag in world.FactionFlags)
        {
            if (string.IsNullOrWhiteSpace(flag.FlagId)) issues.Add(Error("FactionFlag with empty FlagId.", q: flag.AnchorQ, r: flag.AnchorR));
            CheckOrder(flag.EstablishedOrder, flag.FlagId);
            if (!IsInBounds(world, flag.AnchorQ, flag.AnchorR))
                issues.Add(Error($"FactionFlag '{flag.FlagId}' anchor out of bounds ({flag.AnchorQ},{flag.AnchorR}).", q: flag.AnchorQ, r: flag.AnchorR));
            if (!flagAnchors.Add((flag.AnchorQ, flag.AnchorR)))
                issues.Add(Error($"Duplicate FactionFlag anchor ({flag.AnchorQ},{flag.AnchorR}).", q: flag.AnchorQ, r: flag.AnchorR));
            if (footprints.Contains((flag.AnchorQ, flag.AnchorR)))
                issues.Add(Error($"FactionFlag '{flag.FlagId}' anchor is inside a WorldSite footprint.", q: flag.AnchorQ, r: flag.AnchorR));
            var cell = world.Cells.FirstOrDefault(c => c.Q == flag.AnchorQ && c.R == flag.AnchorR);
            var passable = cell == null ? world.DefaultPassable : cell.Passable ?? HexTerrainPalette.DefaultPassable(cell.Terrain);
            if (!passable)
                issues.Add(Error(
                    $"FactionFlag '{flag.FlagId}' anchor ({flag.AnchorQ},{flag.AnchorR}) is not passable " +
                    $"[terrain={cell?.Terrain ?? world.DefaultTerrain}, passable=false].",
                    q: flag.AnchorQ, r: flag.AnchorR));
        }

        var nominalAssets = new List<(string Id, string FactionId, HashSet<(int Q, int R)> Hexes)>();
        foreach (var site in world.Sites)
            if (!string.IsNullOrWhiteSpace(site.OwnerFactionId))
                nominalAssets.Add((site.SiteId, site.OwnerFactionId, ComputeDefaultRegionHexes(world, site)));
        foreach (var flag in world.FactionFlags)
            if (!string.IsNullOrWhiteSpace(flag.FactionId))
                nominalAssets.Add((flag.FlagId, flag.FactionId, NominalFlagRange(world, flag)));

        for (var i = 0; i < nominalAssets.Count; i++)
        for (var j = i + 1; j < nominalAssets.Count; j++)
        {
            var a = nominalAssets[i]; var b = nominalAssets[j];
            if (string.Equals(a.FactionId, b.FactionId, StringComparison.Ordinal)) continue;
            if (a.Hexes.Overlaps(b.Hexes))
                issues.Add(Warn($"Different-faction control asset nominal ranges overlap: '{a.Id}' and '{b.Id}'. Earlier established order wins each Hex."));
        }
    }

    static HashSet<(int Q, int R)> NominalFlagRange(HexWorldDefinitionDto world, HexWorldFactionFlagDto flag)
    {
        var result = new HashSet<(int Q, int R)> { (flag.AnchorQ, flag.AnchorR) };
        var anchor = new HexCoordDto(flag.AnchorQ, flag.AnchorR);
        for (var d = 0; d < 6; d++)
        {
            var n = HexWorldLayoutShared.Neighbor(anchor, d);
            if (IsInBounds(world, n.Q, n.R)) result.Add((n.Q, n.R));
        }
        return result;
    }

    static void ValidateRoadConnectivity(HexWorldDefinitionDto world, List<HexWorldValidationIssue> issues)
    {
        var roadCells = new HashSet<(int Q, int R)>();
        foreach (var cell in world.Cells)
        {
            if (cell.IsRoad)
                roadCells.Add((cell.Q, cell.R));
        }

        if (roadCells.Count == 0)
            return;

        var visited = new HashSet<(int Q, int R)>();
        var components = 0;
        foreach (var start in roadCells)
        {
            if (visited.Contains(start))
                continue;

            components++;
            var queue = new Queue<(int Q, int R)>();
            queue.Enqueue(start);
            visited.Add(start);
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                for (var d = 0; d < 6; d++)
                {
                    var n = HexWorldLayoutShared.Neighbor(new HexCoordDto(cur.Q, cur.R), d);
                    var key = (n.Q, n.R);
                    if (!roadCells.Contains(key) || visited.Contains(key))
                        continue;
                    visited.Add(key);
                    queue.Enqueue(key);
                }
            }
        }

        foreach (var cell in roadCells)
        {
            var neighborRoadCount = 0;
            for (var d = 0; d < 6; d++)
            {
                var n = HexWorldLayoutShared.Neighbor(new HexCoordDto(cell.Q, cell.R), d);
                if (roadCells.Contains((n.Q, n.R)))
                    neighborRoadCount++;
            }

            if (neighborRoadCount == 0)
            {
                issues.Add(Error(
                    $"ROAD DISCONNECTED: isolated road hex ({cell.Q},{cell.R}).",
                    q: cell.Q,
                    r: cell.R));
            }
        }

        if (components > 1)
        {
            issues.Add(Warn(
                $"Road network has {components} connected components ({roadCells.Count} road cells)."));
        }
    }

    static void ValidateTerritories(HexWorldDefinitionDto world, List<HexWorldValidationIssue> issues)
    {
        var sites = world.Sites.ToDictionary(site => site.SiteId, StringComparer.Ordinal);
        var regionIds = new HashSet<string>(StringComparer.Ordinal);
        var byHex = new Dictionary<(int Q, int R), string>();
        var byId = new Dictionary<string, HexWorldTerritoryRegionDto>(StringComparer.Ordinal);
        foreach (var region in world.TerritoryRegions)
        {
            if (string.IsNullOrWhiteSpace(region.RegionId)) { issues.Add(Error("TerritoryRegion with empty RegionId.")); continue; }
            if (!regionIds.Add(region.RegionId)) { issues.Add(Error($"Duplicate TerritoryRegion RegionId: {region.RegionId}.")); continue; }
            byId[region.RegionId] = region;
            if (!sites.TryGetValue(region.PrimaryWorldSiteId, out var site))
            {
                issues.Add(Error($"TerritoryRegion '{region.RegionId}' PrimaryWorldSiteId missing: {region.PrimaryWorldSiteId}."));
            }
            else
            {
                if (!string.Equals(site.TerritoryRegionId, region.RegionId, StringComparison.Ordinal))
                    issues.Add(Error($"Site '{site.SiteId}' TerritoryRegionId does not point back to '{region.RegionId}'.", site.SiteId));
                if (!string.Equals(site.OwnerFactionId, region.ControlFactionId, StringComparison.Ordinal))
                    issues.Add(Error($"Site '{site.SiteId}' OwnerFactionId differs from TerritoryRegion controller.", site.SiteId));
            }
            foreach (var hex in region.Hexes)
            {
                if (!IsInBounds(world, hex.Q, hex.R)) issues.Add(Error($"TerritoryRegion '{region.RegionId}' hex out of bounds ({hex.Q},{hex.R}).", q: hex.Q, r: hex.R));
                var key = (hex.Q, hex.R);
                if (byHex.TryGetValue(key, out var other) && other != region.RegionId)
                    issues.Add(Error($"Territory hex overlap ({hex.Q},{hex.R}): {other} and {region.RegionId}.", q: hex.Q, r: hex.R));
                else byHex[key] = region.RegionId;
            }
        }
        foreach (var site in world.Sites)
        {
            if (string.IsNullOrWhiteSpace(site.TerritoryRegionId)) continue;
            if (!byId.TryGetValue(site.TerritoryRegionId, out var region)) { issues.Add(Error($"Site '{site.SiteId}' references missing TerritoryRegion '{site.TerritoryRegionId}'.", site.SiteId)); continue; }
            // 旧 Region 仅作兼容几何；Runtime 由 Control Asset 重建有效覆盖。
            var expected = ComputeDefaultRegionHexes(world, site);
            var actual = new HashSet<(int Q, int R)>(region.Hexes.Select(h => (h.Q, h.R)));
            var missing = expected.Where(h => !actual.Contains((h.Q, h.R))).ToList();
            var extra = region.Hexes.Where(h => !expected.Contains((h.Q, h.R))).ToList();
            if (missing.Count > 0 || extra.Count > 0)
            {
                issues.Add(Warn($"Region geometry differs from default footprint+outer-ring for '{site.SiteId}': missing {missing.Count}, extra {extra.Count}. 请在 Footprint 页修改后重算辖区，或确认是有意手工微调。", site.SiteId));
            }
        }
    }

    static HashSet<(int Q, int R)> ComputeDefaultRegionHexes(HexWorldDefinitionDto world, HexWorldSiteDto site)
    {
        var result = new HashSet<(int Q, int R)>();
        foreach (var hex in HexWorldFootprintRules.ResolveFootprint(site))
        {
            result.Add((hex.Q, hex.R));
            for (var d = 0; d < 6; d++)
            {
                var n = HexWorldLayoutShared.Neighbor(hex, d);
                if (n.Q >= 0 && n.R >= 0 && n.Q < world.Width && n.R < world.Height)
                    result.Add((n.Q, n.R));
            }
        }

        return result;
    }

    static bool IsInBounds(HexWorldDefinitionDto world, int q, int r) =>
        q >= 0 && r >= 0 && q < world.Width && r < world.Height;

    static HexWorldValidationIssue Error(string message, string? siteId = null, int? q = null, int? r = null) =>
        new() { Level = "error", Message = message, SiteId = siteId, Q = q, R = r };

    static HexWorldValidationIssue Warn(string message, string? siteId = null, int? q = null, int? r = null) =>
        new() { Level = "warn", Message = message, SiteId = siteId, Q = q, R = r };
}
