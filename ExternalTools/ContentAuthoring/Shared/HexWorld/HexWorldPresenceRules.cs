namespace ContentAuthoring.Shared.HexWorld;

/// <summary>WorldSite PresenceHex authoring helpers（兼容字段；必须与 AnchorHex 相同）。</summary>
public static class HexWorldPresenceRules
{
    public static HexCoordDto ResolvePresenceHex(HexWorldSiteDto site)
    {
        SyncPresenceToAnchor(site);
        return new HexCoordDto(site.AnchorQ, site.AnchorR);
    }

    public static void EnsurePresenceDefaults(HexWorldSiteDto site)
    {
        if (!site.PresenceQ.HasValue)
            site.PresenceQ = site.AnchorQ;
        if (!site.PresenceR.HasValue)
            site.PresenceR = site.AnchorR;
        SyncPresenceToAnchor(site);
    }

    /// <summary>强制 PresenceHex == AnchorHex；返回是否曾不一致。</summary>
    public static bool SyncPresenceToAnchor(HexWorldSiteDto site)
    {
        EnsurePresenceDefaultsWithoutSync(site);
        var mismatched = site.PresenceQ != site.AnchorQ || site.PresenceR != site.AnchorR;
        site.PresenceQ = site.AnchorQ;
        site.PresenceR = site.AnchorR;
        return mismatched;
    }

    static void EnsurePresenceDefaultsWithoutSync(HexWorldSiteDto site)
    {
        if (!site.PresenceQ.HasValue)
            site.PresenceQ = site.AnchorQ;
        if (!site.PresenceR.HasValue)
            site.PresenceR = site.AnchorR;
    }

    public static FootprintEditResult ValidatePresenceHex(HexWorldSiteDto site)
    {
        SyncPresenceToAnchor(site);
        var presence = new HexCoordDto(site.AnchorQ, site.AnchorR);
        if (!HexWorldFootprintRules.ContainsFootprint(site, presence))
            return FootprintEditResult.Fail("PresenceHex 必须属于 Footprint。");
        return FootprintEditResult.Ok();
    }
}
