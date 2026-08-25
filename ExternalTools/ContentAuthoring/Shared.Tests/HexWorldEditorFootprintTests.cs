using ContentAuthoring.Shared.HexWorld;
using Xunit;

namespace ContentAuthoring.Shared.Tests;

public sealed class HexWorldEditorFootprintTests
{
    static HexWorldSiteDto MakeSite(string id, int anchorQ, int anchorR, params (int Q, int R)[] footprint)
    {
        var site = new HexWorldSiteDto
        {
            SiteId = id,
            DisplayName = id,
            AnchorQ = anchorQ,
            AnchorR = anchorR,
            Footprint = footprint.Select(h => new HexCoordDto(h.Q, h.R)).ToList(),
        };
        return site;
    }

    [Fact]
    public void EditorFootprint_RejectsOverlap()
    {
        var world = new HexWorldDefinitionDto
        {
            Id = "test:world",
            Name = "Test",
            Width = 20,
            Height = 20,
        };
        var a = MakeSite("base:site_a", 5, 5, (5, 5), (6, 5));
        var b = MakeSite("base:site_b", 8, 8, (8, 8));
        world.Sites.Add(a);
        world.Sites.Add(b);

        var result = HexWorldEditorFootprintService.TryAddFootprintHex(b, new HexCoordDto(6, 5), world);
        Assert.False(result.Success);
        Assert.Contains("base:site_a", result.Message);
    }

    [Fact]
    public void EditorFootprint_RejectsDisconnectedAdd()
    {
        var world = new HexWorldDefinitionDto { Id = "test:world", Name = "Test", Width = 20, Height = 20 };
        var site = MakeSite("base:site_a", 5, 5, (5, 5));
        world.Sites.Add(site);

        var result = HexWorldEditorFootprintService.TryAddFootprintHex(site, new HexCoordDto(7, 5), world);
        Assert.False(result.Success);
    }

    [Fact]
    public void EditorFootprint_RejectsRemovingAnchor()
    {
        var site = MakeSite("base:site_a", 5, 5, (5, 5), (6, 5));
        var result = HexWorldEditorFootprintService.TryRemoveFootprintHex(site, new HexCoordDto(5, 5));
        Assert.False(result.Success);
    }

    [Fact]
    public void EditorFootprint_RejectsDisconnectingRemove()
    {
        var site = MakeSite("base:site_a", 5, 5, (5, 5), (6, 5), (5, 6));
        var result = HexWorldEditorFootprintService.TryRemoveFootprintHex(site, new HexCoordDto(5, 5));
        Assert.False(result.Success);
    }

    [Fact]
    public void EditorFootprint_AllowsSetAnchorInsideFootprint()
    {
        var site = MakeSite("base:site_a", 5, 5, (5, 5), (6, 5));
        var result = HexWorldEditorFootprintService.TrySetAnchorHex(site, new HexCoordDto(6, 5));
        Assert.True(result.Success);
        Assert.Equal(6, site.AnchorQ);
        Assert.Equal(5, site.AnchorR);
    }

    [Fact]
    public void EditorPresence_AllowsNonAnchorInsideFootprint()
    {
        var site = MakeSite("base:site_a", 5, 5, (5, 5), (6, 5), (5, 6));
        var result = HexWorldEditorFootprintService.TrySetPresenceHex(site, new HexCoordDto(6, 5));
        Assert.True(result.Success);
        Assert.Equal(6, site.PresenceQ);
        Assert.Equal(5, site.PresenceR);
        Assert.Equal(5, site.AnchorQ);
    }

    [Fact]
    public void EditorPresence_RejectsOutsideFootprint()
    {
        var site = MakeSite("base:site_a", 5, 5, (5, 5), (6, 5));
        var result = HexWorldEditorFootprintService.TrySetPresenceHex(site, new HexCoordDto(9, 9));
        Assert.False(result.Success);
    }

    [Fact]
    public void EditorPresence_RejectsRemovingPresenceHex()
    {
        var site = MakeSite("base:site_a", 5, 5, (5, 5), (6, 5), (5, 6));
        Assert.True(HexWorldEditorFootprintService.TrySetPresenceHex(site, new HexCoordDto(6, 5)).Success);
        var result = HexWorldEditorFootprintService.TryRemoveFootprintHex(site, new HexCoordDto(6, 5));
        Assert.False(result.Success);
        Assert.Contains("PresenceHex", result.Message);
    }

    [Fact]
    public void LegacySite_WithoutPresence_DefaultsToAnchor()
    {
        var site = MakeSite("base:site_legacy", 5, 5, (5, 5), (6, 5));
        Assert.Null(site.PresenceQ);
        var presence = HexWorldPresenceRules.ResolvePresenceHex(site);
        Assert.Equal(5, presence.Q);
        Assert.Equal(5, presence.R);
        Assert.True(HexWorldPresenceRules.ValidatePresenceHex(site).Success);
    }
}
