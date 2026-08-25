namespace ContentAuthoring.Shared.HexWorld;

/// <summary>WorldSite PresenceHex authoring helpers（与 AnchorHex 分工）。</summary>
public static class HexWorldPresenceRules
{
    public static HexCoordDto ResolvePresenceHex(HexWorldSiteDto site)
    {
        EnsurePresenceDefaults(site);
        return new HexCoordDto(site.PresenceQ!.Value, site.PresenceR!.Value);
    }

    public static void EnsurePresenceDefaults(HexWorldSiteDto site)
    {
        if (site.PresenceQ.HasValue && site.PresenceR.HasValue)
            return;
        site.PresenceQ = site.AnchorQ;
        site.PresenceR = site.AnchorR;
    }

    public static FootprintEditResult ValidatePresenceHex(HexWorldSiteDto site)
    {
        EnsurePresenceDefaults(site);
        var presence = new HexCoordDto(site.PresenceQ!.Value, site.PresenceR!.Value);
        if (!HexWorldFootprintRules.ContainsFootprint(site, presence))
            return FootprintEditResult.Fail("PresenceHex 必须属于 Footprint。");
        return FootprintEditResult.Ok();
    }
}
