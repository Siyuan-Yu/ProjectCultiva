using System.IO;
using ContentAuthoring.Shared.HexWorld;
using Xunit;

namespace ContentAuthoring.Shared.Tests;

public sealed class MultiHexFootprintValidationTests
{
    [Fact]
    public void Ch01HexWorld_MultiHexSamples_PassFootprintValidation()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", ".."));
        var path = Path.Combine(repoRoot, "Content", "BaseGame", "Data", "Worlds", "ch01_hex_world.json");
        Assert.True(File.Exists(path), $"Missing {path}");

        var def = HexWorldContentJson.LoadDefinition(path);
        var byId = def.Sites.ToDictionary(s => s.SiteId, StringComparer.Ordinal);

        foreach (var (siteId, expectedCount, anchorQ, anchorR) in new (string SiteId, int Count, int Q, int R)[]
                 {
                     ("base:site_a", 6, 68, 40),
                     ("base:site_b", 6, 106, 26),
                     ("base:site_chengzhen", 4, 118, 46),
                     ("base:site_zhuangyuan", 4, 118, 32),
                 })
        {
            Assert.True(byId.TryGetValue(siteId, out var site), siteId);
            Assert.Equal(expectedCount, FootprintCount(site));
            Assert.Equal(anchorQ, site.AnchorQ);
            Assert.Equal(anchorR, site.AnchorR);
            Assert.Contains(site.Footprint, h => h.Q == site.AnchorQ && h.R == site.AnchorR);

            var footprint = site.Footprint.Count > 0
                ? site.Footprint
                : new List<HexCoordDto> { new(site.AnchorQ, site.AnchorR) };
            Assert.True(IsFootprintConnected(footprint), siteId);
        }

        var overlapIssues = ValidateFootprintOverlapOnly(def);
        Assert.True(overlapIssues.Count == 0, string.Join("\n", overlapIssues));
    }

    static bool IsFootprintConnected(IReadOnlyList<HexCoordDto> footprint)
    {
        if (footprint.Count <= 1)
            return true;

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

    static List<string> ValidateFootprintOverlapOnly(HexWorldDefinitionDto world)
    {
        var issues = new List<string>();
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
                    issues.Add($"overlap {other} vs {site.SiteId} at ({hex.Q},{hex.R})");
                else
                    owner[key] = site.SiteId;
            }
        }

        return issues;
    }

    static int FootprintCount(HexWorldSiteDto site) =>
        site.Footprint.Count > 0 ? site.Footprint.Count : 1;
}
