namespace ContentAuthoring.Shared.HexWorld;

public sealed class FootprintEditResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;

    public static FootprintEditResult Ok(string message = "") =>
        new() { Success = true, Message = message };

    public static FootprintEditResult Fail(string message) =>
        new() { Success = false, Message = message };
}

/// <summary>WorldSite Footprint 编辑/校验共享规则（Editor + Content Validator）。</summary>
public static class HexWorldFootprintRules
{
    public static List<HexCoordDto> ResolveFootprint(HexWorldSiteDto site)
    {
        if (site.Footprint.Count > 0)
            return site.Footprint;
        return new List<HexCoordDto> { new(site.AnchorQ, site.AnchorR) };
    }

    public static bool ContainsFootprint(HexWorldSiteDto site, HexCoordDto hex) =>
        ResolveFootprint(site).Any(h => h.Q == hex.Q && h.R == hex.R);

    public static bool IsConnected(IReadOnlyList<HexCoordDto> footprint)
    {
        if (footprint.Count <= 1)
            return footprint.Count == 1;

        var set = new HashSet<(int Q, int R)>();
        foreach (var hex in footprint)
            set.Add((hex.Q, hex.R));

        var start = (footprint[0].Q, footprint[0].R);
        var visited = new HashSet<(int Q, int R)> { start };
        var stack = new Stack<(int Q, int R)>();
        stack.Push(start);
        while (stack.Count > 0)
        {
            var cur = stack.Pop();
            for (var d = 0; d < 6; d++)
            {
                var n = HexWorldLayoutShared.Neighbor(new HexCoordDto(cur.Q, cur.R), d);
                var key = (n.Q, n.R);
                if (!set.Contains(key) || !visited.Add(key))
                    continue;
                stack.Push(key);
            }
        }

        return visited.Count == set.Count;
    }

    public static bool IsAdjacentToFootprint(IReadOnlyList<HexCoordDto> footprint, HexCoordDto hex)
    {
        for (var d = 0; d < 6; d++)
        {
            var n = HexWorldLayoutShared.Neighbor(hex, d);
            if (footprint.Any(h => h.Q == n.Q && h.R == n.R))
                return true;
        }

        return false;
    }

    public static bool WouldRemainConnectedAfterRemove(IReadOnlyList<HexCoordDto> footprint, HexCoordDto remove)
    {
        var remaining = footprint.Where(h => h.Q != remove.Q || h.R != remove.R).ToList();
        return IsConnected(remaining);
    }

    public static HexWorldSiteDto? FindOccupant(
        HexWorldDefinitionDto world,
        HexCoordDto hex,
        string? exceptSiteId = null)
    {
        foreach (var site in world.Sites)
        {
            if (string.IsNullOrWhiteSpace(site.SiteId))
                continue;
            if (exceptSiteId != null &&
                string.Equals(site.SiteId, exceptSiteId, StringComparison.Ordinal))
                continue;
            if (ContainsFootprint(site, hex))
                return site;
        }

        return null;
    }

    public static FootprintEditResult ValidateSiteFootprint(HexWorldSiteDto site)
    {
        var footprint = ResolveFootprint(site);
        if (footprint.Count == 0)
            return FootprintEditResult.Fail("Footprint 不能为空。");
        if (!ContainsFootprint(site, new HexCoordDto(site.AnchorQ, site.AnchorR)))
            return FootprintEditResult.Fail("AnchorHex 必须属于 Footprint。");
        var presenceCheck = HexWorldPresenceRules.ValidatePresenceHex(site);
        if (!presenceCheck.Success)
            return presenceCheck;
        if (footprint.Count > 1 && !IsConnected(footprint))
            return FootprintEditResult.Fail("Footprint 必须连续。");
        return FootprintEditResult.Ok();
    }
}
