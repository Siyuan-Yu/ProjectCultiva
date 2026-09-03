using ContentAuthoring.Shared.HexWorld;
using Xunit;

namespace ContentAuthoring.Shared.Tests;

public sealed class HexWorldTerritoryEditorTests
{
    static HexWorldEditorDocument MakeDocument()
    {
        var world = HexWorldContentGenerator.CreateBlank("test:world", "test", 20, 20, HexTerrainIds.Plain, true);
        world.Sites.Add(new HexWorldSiteDto { SiteId = "base:site_a", DisplayName = "甲", OwnerFactionId = "base:faction_a", AnchorQ = 3, AnchorR = 3, Footprint = new List<HexCoordDto> { new(3, 3) } });
        world.Sites.Add(new HexWorldSiteDto { SiteId = "base:site_b", DisplayName = "乙", OwnerFactionId = "base:faction_b", AnchorQ = 8, AnchorR = 8, Footprint = new List<HexCoordDto> { new(8, 8) } });
        var doc = new HexWorldEditorDocument(); doc.Load(world, null);
        Assert.True(doc.CreateTerritoryForSite("base:site_a", "base:territory_a").Success);
        Assert.True(doc.CreateTerritoryForSite("base:site_b", "base:territory_b").Success);
        return doc;
    }

    [Fact]
    public void Assign_Reassign_And_FootprintProtection_Work()
    {
        var doc = MakeDocument(); var hex = new HexCoordDto(5, 5);
        var assignB = doc.AssignTerritoryHex("base:territory_b", hex); Assert.True(assignB.Success, assignB.Message);
        var assignA = doc.AssignTerritoryHex("base:territory_a", hex); Assert.True(assignA.Success, assignA.Message);
        Assert.Equal("base:territory_a", doc.FindTerritoryAt(hex)?.RegionId);
        Assert.DoesNotContain(hex, doc.GetTerritoryRegion("base:territory_b")!.Hexes);
        Assert.False(doc.AssignTerritoryHex("base:territory_a", new HexCoordDto(8, 8)).Success);
        Assert.False(doc.RemoveTerritoryHex(new HexCoordDto(3, 3)).Success);
    }

    [Fact]
    public void Undo_And_Roundtrip_Preserve_Territory()
    {
        var doc = MakeDocument(); var hex = new HexCoordDto(5, 5);
        doc.PushUndo(); Assert.True(doc.AssignTerritoryHex("base:territory_a", hex, false).Success);
        Assert.True(doc.Undo()); Assert.Null(doc.FindTerritoryAt(hex));
        var text = HexWorldContentJson.Serialize(doc.World);
        var restored = HexWorldContentJson.LoadDefinition(HexWorldContentJson.Load(text));
        Assert.Equal(2, restored.TerritoryRegions.Count);
        Assert.Equal("base:territory_a", restored.Sites.Single(s => s.SiteId == "base:site_a").TerritoryRegionId);
    }

    [Fact]
    public void ExpandOneRing_Uses_OddR_Neighbors()
    {
        var doc = MakeDocument(); var result = doc.ExpandTerritoryOneRing("base:territory_a");
        Assert.True(result.Success); var region = doc.GetTerritoryRegion("base:territory_a")!;
        foreach (var d in Enumerable.Range(0, 6)) Assert.Contains(HexWorldLayoutShared.Neighbor(new HexCoordDto(3, 3), d), region.Hexes);
    }
}
