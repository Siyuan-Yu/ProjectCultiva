using System;
using System.IO;
using System.Linq;
using ContentAuthoring.Shared.HexWorld;
using Xunit;

namespace ContentAuthoring.Shared.Tests;

/// <summary>规格 §27 H–O：Territory Brush 纯文档层逻辑（single / site macro / erase / stroke undo / standalone roundtrip）。</summary>
public sealed class TerritoryBrushDocumentTests
{
    static HexWorldEditorDocument NewDocument()
    {
        var doc = new HexWorldEditorDocument();
        doc.NewWorld(20, 15, HexTerrainIds.Plain, passable: true);
        return doc;
    }

    static HexWorldSiteDto AddSite(HexWorldEditorDocument doc, int q, int r)
    {
        return doc.CreateSite(new HexCoordDto(q, r));
    }

    static string DumpState(string label) => label;

    [Fact]
    public void H_WildernessHex_PaintSingle()
    {
        var doc = NewDocument();
        var result = doc.PaintTerritory(new HexCoordDto(14, 10), "base:faction_nan_yan");
        Assert.True(result.Success);
        Assert.Equal(TerritoryStrokeKind.Standalone, result.Kind);
        var control = doc.FindStandaloneAt(new HexCoordDto(14, 10));
        Assert.NotNull(control);
        Assert.Equal("base:faction_nan_yan", control!.ControlFactionId);
    }

    [Fact]
    public void I_WildernessHex_RepaintAToB()
    {
        var doc = NewDocument();
        doc.PaintTerritory(new HexCoordDto(14, 10), "base:faction_nan_yan");
        var result = doc.PaintTerritory(new HexCoordDto(14, 10), "base:faction_shuofeng");
        Assert.True(result.Success);
        Assert.Equal(1, doc.World.StandaloneTerritoryHexes.Count(c => c.Q == 14 && c.R == 10));
        Assert.Equal("base:faction_shuofeng", doc.FindStandaloneAt(new HexCoordDto(14, 10))!.ControlFactionId);
    }

    [Fact]
    public void J_WildernessHex_Erase()
    {
        var doc = NewDocument();
        doc.PaintTerritory(new HexCoordDto(14, 10), "base:faction_nan_yan");
        var result = doc.EraseTerritory(new HexCoordDto(14, 10));
        Assert.True(result.Success);
        Assert.Equal(TerritoryStrokeKind.StandaloneCleared, result.Kind);
        Assert.Null(doc.FindStandaloneAt(new HexCoordDto(14, 10)));
    }

    [Fact]
    public void L_WorldSiteFootprintClick_MacroPaint()
    {
        var doc = NewDocument();
        var site = AddSite(doc, 5, 5);
        var result = doc.PaintTerritory(new HexCoordDto(5, 5), "base:faction_nan_yan");
        Assert.True(result.Success);
        Assert.Equal(TerritoryStrokeKind.SiteMacro, result.Kind);
        Assert.Equal(site.SiteId, result.SiteId);

        var reloaded = doc.World;
        var region = reloaded.TerritoryRegions.Single();
        Assert.Equal(site.SiteId, region.PrimaryWorldSiteId);
        Assert.Equal("base:faction_nan_yan", region.ControlFactionId);
        Assert.Equal("base:faction_nan_yan", site.OwnerFactionId);
        Assert.Equal(region.RegionId, site.TerritoryRegionId);
        // footprint(1) + outer ring(6) = 7 hexes
        Assert.Equal(7, region.Hexes.Count);
    }

    [Fact]
    public void M_WorldSiteRingClick_MacroPaintWholeTerritory()
    {
        var doc = NewDocument();
        var site = AddSite(doc, 5, 5);
        // ring hex = neighbor (6,5)
        var result = doc.PaintTerritory(new HexCoordDto(6, 5), "base:faction_shuofeng");
        Assert.True(result.Success);
        Assert.Equal(TerritoryStrokeKind.SiteMacro, result.Kind);
        Assert.Equal(site.SiteId, result.SiteId);
        var region = doc.World.TerritoryRegions.Single();
        Assert.Equal("base:faction_shuofeng", region.ControlFactionId);
        Assert.Equal(7, region.Hexes.Count);
    }

    [Fact]
    public void N_FootprintEdit_AutoRecomputesRegionGeometry()
    {
        var doc = NewDocument();
        var site = AddSite(doc, 5, 5);
        doc.PaintTerritory(new HexCoordDto(5, 5), "base:faction_nan_yan");
        Assert.Equal(7, doc.GetTerritoryForSite(site.SiteId)!.Hexes.Count);

        // 扩 footprint：加 (5,6)
        doc.ToggleFootprintHex(site.SiteId, new HexCoordDto(5, 6), add: true);
        var region = doc.GetTerritoryForSite(site.SiteId)!;
        var expected = doc.ComputeDefaultSiteTerritory(site).Count;
        Assert.Equal(expected, region.Hexes.Count);
        Assert.Contains(region.Hexes, h => h.Q == 5 && h.R == 6);
        // 仍保持 controller（footprint 修改不丢势力）
        Assert.Equal("base:faction_nan_yan", region.ControlFactionId);
    }

    [Fact]
    public void O_MixedStroke_SingleUndoRestoresAll()
    {
        var doc = NewDocument();
        // UI stroke 语义：拖拽开始 PushUndo 一次，之后同一 stroke 内多格 paint 不再 push。
        doc.PushUndo();
        doc.PaintTerritory(new HexCoordDto(14, 10), "base:faction_shuofeng", singleStrokeUndo: false);
        doc.PaintTerritory(new HexCoordDto(14, 11), "base:faction_shuofeng", singleStrokeUndo: false);
        doc.PaintTerritory(new HexCoordDto(3, 3), "base:faction_shuofeng", singleStrokeUndo: false);
        Assert.Equal(3, doc.World.StandaloneTerritoryHexes.Count);

        Assert.True(doc.Undo());
        // 整 stroke 一次恢复（Ctrl+Z 一次 = 全部三格）
        Assert.Empty(doc.World.StandaloneTerritoryHexes);
    }

    [Fact]
    public void K_StandaloneJson_RoundtripDeterministic()
    {
        var doc = NewDocument();
        doc.PaintTerritory(new HexCoordDto(14, 10), "base:faction_nan_yan");
        doc.PaintTerritory(new HexCoordDto(3, 2), "base:faction_xijin");
        HexWorldContentJson.NormalizeForSave(doc.World);
        var json = HexWorldContentJson.Serialize(doc.World);
        var reloaded = HexWorldContentJson.Load(json);
        var def2 = HexWorldContentJson.LoadDefinition(reloaded);
        HexWorldContentJson.NormalizeForSave(def2);
        var json2 = HexWorldContentJson.Serialize(def2);
        Assert.Equal(json, json2);
        Assert.Equal(2, def2.StandaloneTerritoryHexes.Count);
        Assert.Contains(def2.StandaloneTerritoryHexes, c => c.Q == 14 && c.R == 10 && c.ControlFactionId == "base:faction_nan_yan");
        // 序列化键必须是 standaloneTerritoryHexes（Runtime loader 读取同一键）
        Assert.Contains("\"standaloneTerritoryHexes\"", json);
    }
}
