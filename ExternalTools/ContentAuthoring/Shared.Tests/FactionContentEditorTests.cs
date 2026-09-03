using System;
using System.IO;
using System.Linq;
using ContentAuthoring.Shared;
using Xunit;

namespace ContentAuthoring.Shared.Tests;

/// <summary>规格 §27 A–G：Faction List / 名称颜色 / roundtrip / 引用保护（可无 UI 测试的核心）。</summary>
public sealed class FactionContentEditorTests
{
    static string RepoRoot => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", ".."));

    static string FactionsPath => Path.Combine(RepoRoot, "Content", "BaseGame", "Data", "Factions", "factions.json");

    static string DataDir => Path.Combine(RepoRoot, "Content", "BaseGame", "Data");

    [Fact]
    public void A_FactionList_LoadsFromOfficialJson_AllEightPresent()
    {
        Assert.True(File.Exists(FactionsPath), $"Missing {FactionsPath}");
        var list = StrategicFactionAuthoring.LoadStrategicFactions(FactionsPath);
        Assert.Equal(8, list.Count);
        foreach (var id in new[]
                 {
                     "base:faction_player", "base:sect_huangcun_labor", "base:faction_fisher_village",
                     "base:faction_nan_yan", "base:faction_shuofeng", "base:faction_donglin",
                     "base:faction_xijin", "base:faction_bandits",
                 })
        {
            Assert.Contains(list, f => f.Id == id);
        }
    }

    [Fact]
    public void C_DisplayNameAndMapColor_MatchOfficialContent()
    {
        var list = StrategicFactionAuthoring.LoadStrategicFactions(FactionsPath);
        var nanYan = list.Single(f => f.Id == "base:faction_nan_yan");
        Assert.Equal("南堰庄盟", nanYan.Name);
        Assert.Equal("#D19E47", nanYan.MapColor);
        Assert.True(nanYan.TerritorySelectable);
        var shuofeng = list.Single(f => f.Id == "base:faction_shuofeng");
        Assert.Equal("朔风堡", shuofeng.Name);
        Assert.Equal("#8CADC7", shuofeng.MapColor);
        var bandits = list.Single(f => f.Id == "base:faction_bandits");
        Assert.False(bandits.TerritorySelectable);
        Assert.Equal(999, bandits.SortOrder);
    }

    [Fact]
    public void A_List_IsSortedBySortOrder()
    {
        var list = StrategicFactionAuthoring.LoadStrategicFactions(FactionsPath);
        var sorted = list.OrderBy(f => f.SortOrder).ThenBy(f => f.Id, StringComparer.Ordinal).ToList();
        Assert.Equal(sorted.Select(f => f.Id), list.Select(f => f.Id));
    }

    [Fact]
    public void D_NewFaction_Roundtrip_SaveAndReload()
    {
        var dir = Path.Combine(Path.GetTempPath(), "faction_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var tmpPath = Path.Combine(dir, "factions.json");
        try
        {
            File.Copy(FactionsPath, tmpPath);
            var list = StrategicFactionAuthoring.LoadStrategicFactions(tmpPath);
            list.Add(new StrategicFactionAuthoringDto
            {
                Id = "base:faction_test",
                Name = "测试势力",
                MapColor = "#112233",
                TerritorySelectable = true,
                SortOrder = 42,
            });
            StrategicFactionAuthoring.SaveStrategicFactions(tmpPath, list);

            var reloaded = StrategicFactionAuthoring.LoadStrategicFactions(tmpPath);
            var added = reloaded.Single(f => f.Id == "base:faction_test");
            Assert.Equal("测试势力", added.Name);
            Assert.Equal("#112233", added.MapColor);
            Assert.Equal(42, added.SortOrder);
            Assert.True(added.TerritorySelectable);
            Assert.Equal(9, reloaded.Count);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void E_MapColorChange_Roundtrips_ReferenceUnchanged()
    {
        var dir = Path.Combine(Path.GetTempPath(), "faction_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var tmpPath = Path.Combine(dir, "factions.json");
        try
        {
            File.Copy(FactionsPath, tmpPath);
            var list = StrategicFactionAuthoring.LoadStrategicFactions(tmpPath);
            var idx = list.FindIndex(f => f.Id == "base:faction_nan_yan");
            list[idx] = list[idx].With(mapColor: "#AABBCC", name: "南堰新盟");
            StrategicFactionAuthoring.SaveStrategicFactions(tmpPath, list);

            var reloaded = StrategicFactionAuthoring.LoadStrategicFactions(tmpPath).Single(f => f.Id == "base:faction_nan_yan");
            Assert.Equal("#AABBCC", reloaded.MapColor);
            Assert.Equal("南堰新盟", reloaded.Name);
            Assert.Empty(StrategicFactionAuthoring.Validate(new[] { reloaded }));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void E_InvalidMapColor_ValidationFails()
    {
        var bad = new[] { new StrategicFactionAuthoringDto { Id = "a", Name = "x", MapColor = "red", TerritorySelectable = true, SortOrder = 1 } };
        Assert.Contains(StrategicFactionAuthoring.Validate(bad), e => e.Contains("mapColor", StringComparison.Ordinal));
    }

    [Fact]
    public void F_DuplicateFactionId_ValidationFails()
    {
        var list = new[]
        {
            new StrategicFactionAuthoringDto { Id = "base:a", Name = "甲", MapColor = "#112233", TerritorySelectable = true, SortOrder = 1 },
            new StrategicFactionAuthoringDto { Id = "base:a", Name = "乙", MapColor = "#445566", TerritorySelectable = true, SortOrder = 2 },
        };
        Assert.Contains(StrategicFactionAuthoring.Validate(list), e => e.Contains("重复", StringComparison.Ordinal));
    }

    [Fact]
    public void G_DeleteProtected_ReferencesFoundInFormalArmyScenarioWorld()
    {
        // ch01 hexWorld / armies / scenarios 有 factionId 引用；扫描 Worlds/Armies/Scenarios/Rosters 应命中至少一处。
        var hits = FactionReferenceScanner.ScanPackage(DataDir, "base:faction_shuofeng");
        Assert.NotEmpty(hits);
        Assert.Contains(hits, h => !string.IsNullOrEmpty(h.Key));
        Assert.Contains(hits, h => !string.IsNullOrEmpty(h.Value));
    }

    [Fact]
    public void G_ReferenceScanner_FindsOwnerFactionIdInMiniHexWorld()
    {
        var dir = Path.Combine(Path.GetTempPath(), "faction_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var mini = Path.Combine(dir, "mini_world.json");
        try
        {
            File.WriteAllText(mini, """
                {
                  "schemaVersion": 1,
                  "definitions": [
                    {
                      "id": "base:mini",
                      "type": "hexWorld",
                      "sites": [ { "siteId": "base:site_zs", "ownerFactionId": "base:faction_nan_yan" } ],
                      "territoryRegions": [ { "regionId": "base:territory_zs", "controlFactionId": "base:faction_nan_yan" } ]
                    }
                  ]
                }
                """);
            var hits = FactionReferenceScanner.ScanFile(mini, "base:faction_nan_yan");
            Assert.Contains(hits, h => h.Key == "ownerFactionId");
            Assert.Contains(hits, h => h.Key == "controlFactionId");
            Assert.Empty(FactionReferenceScanner.ScanFile(mini, "base:faction_bandits"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
