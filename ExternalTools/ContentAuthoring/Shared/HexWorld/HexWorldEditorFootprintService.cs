namespace ContentAuthoring.Shared.HexWorld;

public static class HexWorldEditorFootprintService
{
    public static FootprintEditResult TryAddFootprintHex(HexWorldSiteDto site, HexCoordDto hex, HexWorldDefinitionDto world)
    {
        if (HexWorldFootprintRules.ContainsFootprint(site, hex))
            return FootprintEditResult.Fail("该 Hex 已在当前 Footprint 中。");

        var occupant = HexWorldFootprintRules.FindOccupant(world, hex, site.SiteId);
        if (occupant != null)
        {
            var name = string.IsNullOrWhiteSpace(occupant.DisplayName) ? occupant.SiteId : occupant.DisplayName;
            return FootprintEditResult.Fail($"Hex ({hex.Q},{hex.R}) 已被 WorldSite「{name}」({occupant.SiteId}) 占用。");
        }

        var footprint = HexWorldFootprintRules.ResolveFootprint(site);
        if (footprint.Count > 0 && !HexWorldFootprintRules.IsAdjacentToFootprint(footprint, hex))
            return FootprintEditResult.Fail("新 Hex 必须与现有 Footprint 相邻，以保持连续。");

        site.Footprint.Add(hex);
        var connected = HexWorldFootprintRules.IsConnected(site.Footprint);
        return connected
            ? FootprintEditResult.Ok("已加入 Footprint。")
            : FootprintEditResult.Fail("加入后 Footprint 不连续。");
    }

    public static FootprintEditResult TryRemoveFootprintHex(HexWorldSiteDto site, HexCoordDto hex)
    {
        if (!HexWorldFootprintRules.ContainsFootprint(site, hex))
            return FootprintEditResult.Fail("该 Hex 不在 Footprint 中。");

        var footprint = HexWorldFootprintRules.ResolveFootprint(site);
        if (footprint.Count <= 1)
            return FootprintEditResult.Fail("Footprint 至少保留 1 格，不能删空。");

        if (hex.Q == site.AnchorQ && hex.R == site.AnchorR)
            return FootprintEditResult.Fail("不能删除 AnchorHex；请先将其他 Hex 设为 Anchor。");

        if (!HexWorldFootprintRules.WouldRemainConnectedAfterRemove(footprint, hex))
            return FootprintEditResult.Fail("删除此格会使 Footprint 断开，操作已拒绝。");

        site.Footprint.RemoveAll(h => h.Q == hex.Q && h.R == hex.R);
        return FootprintEditResult.Ok("已移出 Footprint。");
    }

    public static FootprintEditResult TrySetAnchorHex(HexWorldSiteDto site, HexCoordDto hex)
    {
        if (!HexWorldFootprintRules.ContainsFootprint(site, hex))
            return FootprintEditResult.Fail("AnchorHex 必须属于 Footprint。");

        site.AnchorQ = hex.Q;
        site.AnchorR = hex.R;
        HexWorldPresenceRules.SyncPresenceToAnchor(site);
        return FootprintEditResult.Ok($"AnchorHex 已设为 ({hex.Q},{hex.R})；PresenceHex 已同步。");
    }

    public static FootprintEditResult TrySetPresenceHex(HexWorldSiteDto site, HexCoordDto hex)
    {
        _ = site;
        _ = hex;
        return FootprintEditResult.Fail(
            "PresenceHex 已与 AnchorHex 锁定相同（兼容字段）；请修改 AnchorHex。");
    }
}
