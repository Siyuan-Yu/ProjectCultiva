using System;
using System.IO;
using System.Linq;
using ContentAuthoring.Shared;
using ContentAuthoring.Shared.HexWorld;
using Xunit;

namespace ContentAuthoring.Shared.Tests;

/// <summary>规格 §27 P/Q + §23：undefined faction / non-selectable / standalone 几何与重叠校验。</summary>
public sealed class TerritoryValidatorTests
{
    static string RepoRoot => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", ".."));

    static List<StrategicFactionAuthoringDto> RealFactions() =>
        StrategicFactionAuthoring.LoadStrategicFactions(Path.Combine(RepoRoot, "Content", "BaseGame", "Data", "Factions", "factions.json"));

    static HexWorldDefinitionDto BlankWorld()
    {
        var world = HexWorldContentGenerator.CreateBlank("base:t", "T", 20, 15, HexTerrainIds.Plain, passable: true);
        return world;
    }

    [Fact]
    public void Q_NonTerritorySelectable_RejectedAsTerritoryController()
    {
        var world = BlankWorld();
        world.StandaloneTerritoryHexes.Add(new HexWorldStandaloneHexControlDto
        {
            Q = 10,
            R = 10,
            ControlFactionId = "base:faction_bandits", // territorySelectable=false
        });
        var issues = HexWorldContentValidator.Validate(world, RealFactions());
        Assert.Contains(issues, i => i.Level == "error" && i.Message.Contains("not territorySelectable", StringComparison.Ordinal));
    }

    [Fact]
    public void P_UndefinedFaction_RejectedInStandaloneRegionSite()
    {
        var world = BlankWorld();
        world.StandaloneTerritoryHexes.Add(new HexWorldStandaloneHexControlDto { Q = 10, R = 10, ControlFactionId = "base:faction_nope" });
        var site = new HexWorldSiteDto
        {
            SiteId = "base:site_a",
            DisplayName = "A",
            AnchorQ = 2,
            AnchorR = 2,
            PresenceQ = 2,
            PresenceR = 2,
            Footprint = new System.Collections.Generic.List<HexCoordDto> { new(2, 2) },
            OwnerFactionId = "base:faction_nope",
        };
        world.Sites.Add(site);

        var issues = HexWorldContentValidator.Validate(world, RealFactions());
        Assert.Contains(issues, i => i.Level == "error" && i.Message.Contains("unknown faction", StringComparison.Ordinal));
    }

    [Fact]
    public void StandaloneHex_OutOfBounds_Error()
    {
        var world = BlankWorld();
        world.StandaloneTerritoryHexes.Add(new HexWorldStandaloneHexControlDto { Q = 99, R = 99, ControlFactionId = "base:faction_nan_yan" });
        var issues = HexWorldContentValidator.Validate(world, RealFactions());
        Assert.Contains(issues, i => i.Level == "error" && i.Message.Contains("out of bounds", StringComparison.Ordinal));
    }

    [Fact]
    public void StandaloneHex_Duplicate_Error()
    {
        var world = BlankWorld();
        world.StandaloneTerritoryHexes.Add(new HexWorldStandaloneHexControlDto { Q = 5, R = 5, ControlFactionId = "base:faction_nan_yan" });
        world.StandaloneTerritoryHexes.Add(new HexWorldStandaloneHexControlDto { Q = 5, R = 5, ControlFactionId = "base:faction_shuofeng" });
        var issues = HexWorldContentValidator.Validate(world, RealFactions());
        Assert.Contains(issues, i => i.Level == "error" && i.Message.Contains("Duplicate standalone", StringComparison.Ordinal));
    }

    [Fact]
    public void StandaloneHex_OverlapsRegion_Error()
    {
        var world = BlankWorld();
        world.Sites.Add(new HexWorldSiteDto
        {
            SiteId = "base:site_a",
            DisplayName = "A",
            AnchorQ = 5,
            AnchorR = 5,
            PresenceQ = 5,
            PresenceR = 5,
            Footprint = new System.Collections.Generic.List<HexCoordDto> { new(5, 5) },
            OwnerFactionId = "base:faction_nan_yan",
            TerritoryRegionId = "base:territory_a",
        });
        world.TerritoryRegions.Add(new HexWorldTerritoryRegionDto
        {
            RegionId = "base:territory_a",
            PrimaryWorldSiteId = "base:site_a",
            ControlFactionId = "base:faction_nan_yan",
            Hexes = new System.Collections.Generic.List<HexCoordDto>
            {
                new(5, 5), new(6, 5), new(4, 5), new(5, 6), new(5, 4), new(6, 4), new(4, 6),
            },
        });
        world.StandaloneTerritoryHexes.Add(new HexWorldStandaloneHexControlDto { Q = 6, R = 5, ControlFactionId = "base:faction_shuofeng" });
        var issues = HexWorldContentValidator.Validate(world, RealFactions());
        Assert.Contains(issues, i => i.Level == "error" && i.Message.Contains("overlaps TerritoryRegion", StringComparison.Ordinal));
    }

    [Fact]
    public void StandaloneInsideWorldSiteFootprint_Error()
    {
        var world = BlankWorld();
        world.Sites.Add(new HexWorldSiteDto
        {
            SiteId = "base:site_a",
            DisplayName = "A",
            AnchorQ = 5,
            AnchorR = 5,
            PresenceQ = 5,
            PresenceR = 5,
            Footprint = new System.Collections.Generic.List<HexCoordDto> { new(5, 5), new(6, 5) },
        });
        world.StandaloneTerritoryHexes.Add(new HexWorldStandaloneHexControlDto { Q = 6, R = 5, ControlFactionId = "base:faction_nan_yan" });
        var issues = HexWorldContentValidator.Validate(world, RealFactions());
        Assert.Contains(issues, i => i.Level == "error" && i.Message.Contains("WorldSite footprint", StringComparison.Ordinal));
    }

    [Fact]
    public void RegionGeometry_MismatchDefault_IsWarning()
    {
        var world = BlankWorld();
        world.Sites.Add(new HexWorldSiteDto
        {
            SiteId = "base:site_a",
            DisplayName = "A",
            AnchorQ = 5,
            AnchorR = 5,
            PresenceQ = 5,
            PresenceR = 5,
            Footprint = new System.Collections.Generic.List<HexCoordDto> { new(5, 5) },
            OwnerFactionId = "base:faction_nan_yan",
            TerritoryRegionId = "base:territory_a",
        });
        world.TerritoryRegions.Add(new HexWorldTerritoryRegionDto
        {
            RegionId = "base:territory_a",
            PrimaryWorldSiteId = "base:site_a",
            ControlFactionId = "base:faction_nan_yan",
            Hexes = new System.Collections.Generic.List<HexCoordDto> { new(5, 5) }, // 只有 footprint，缺外圈
        });
        var issues = HexWorldContentValidator.Validate(world, RealFactions());
        Assert.Contains(issues, i => i.Level == "warn" && i.Message.Contains("geometry differs", StringComparison.Ordinal));
    }
}
