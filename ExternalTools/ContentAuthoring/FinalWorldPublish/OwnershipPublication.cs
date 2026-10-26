using System.Text.Json.Nodes;
using SurfaceAuthoring.Core;

/// <summary>Metadata-only publication; refuses any unbaked geometry/source change.</summary>
static class OwnershipPublication
{
    public static void Publish(WorldCompositionDocument source, LinkedSourceSet linked,
        string contentRoot, string baselinePath)
    {
        var baseline = SurfaceAuthoringJson.LoadWorldComposition(Path.GetFullPath(baselinePath));
        var before = JsonNode.Parse(SurfaceAuthoringJson.Serialize(baseline))!;
        var after = JsonNode.Parse(SurfaceAuthoringJson.Serialize(source))!;
        var oldSites = before["runtimeLayout"]!["siteRegions"]!.AsArray();
        var newSites = after["runtimeLayout"]!["siteRegions"]!.AsArray();
        var owners = newSites.ToDictionary(n => n!["siteId"]!.GetValue<string>(),
            n => n!["ownerFactionId"]?.GetValue<string>() ?? string.Empty);
        foreach (var site in oldSites) site!.AsObject().Remove("ownerFactionId");
        foreach (var site in newSites) site!.AsObject().Remove("ownerFactionId");
        if (!JsonNode.DeepEquals(before, after))
            throw new InvalidDataException("Ownership-only publication refuses non-ownership source changes.");
        var oldHash = ContinuousSurfaceBaker.SourceHash(baseline, linked);
        var newHash = ContinuousSurfaceBaker.SourceHash(source, linked);
        var folder = Path.Combine(contentRoot, "BaseGame", "Data", "Worlds");
        string[] names = ["main_wilderness_surface_v1.json", "w2a_surface_geography_baked_v1.json", "main_world_surface_map_v1.json"];
        var paths = names.Select(n => Path.Combine(folder, n)).ToArray();
        var originals = paths.Select(File.ReadAllText).ToArray();
        var roots = originals.Select(s => JsonNode.Parse(s)!).ToArray();
        var defs = roots.Select(r => r["definitions"]![0]!.AsObject()).ToArray();
        if (defs[1]["sourceHash"]!.GetValue<string>() != oldHash ||
            defs[2]["sourceHash"]!.GetValue<string>() != oldHash)
            throw new InvalidDataException("Published caches do not match the baseline and linked geometry.");
        var runtimeSites = defs[0]["siteRegions"]!.AsArray();
        var baselineSites = baseline.RuntimeLayout!["siteRegions"]!.AsArray()
            .ToDictionary(n => n!["siteId"]!.GetValue<string>());
        if (runtimeSites.Count != owners.Count) throw new InvalidDataException("Site counts differ.");
        var changed = 0;
        foreach (var node in runtimeSites)
        {
            var site = node!.AsObject();
            var id = site["siteId"]!.GetValue<string>();
            if (!owners.TryGetValue(id, out var owner) || !baselineSites.TryGetValue(id, out var old))
                throw new InvalidDataException("Unknown runtime site: " + id);
            var previous = site["ownerFactionId"]?.GetValue<string>() ?? string.Empty;
            if (previous != (old!["ownerFactionId"]?.GetValue<string>() ?? string.Empty))
                throw new InvalidDataException("Runtime ownership differs from author baseline: " + id);
            if (previous == owner) continue;
            changed++;
            if (owner.Length == 0) site.Remove("ownerFactionId"); else site["ownerFactionId"] = owner;
        }
        defs[1]["sourceHash"] = newHash;
        defs[2]["sourceHash"] = newHash;
        var backup = Path.Combine(contentRoot, "BaseGame", "_backups", "faction-ownership",
            DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(backup);
        for (var i = 0; i < paths.Length; i++)
        {
            File.WriteAllText(Path.Combine(backup, names[i]), originals[i]);
            File.WriteAllText(Path.Combine(backup, names[i] + ".candidate"), roots[i].ToJsonString(SurfaceAuthoringJson.Options));
        }
        try
        {
            for (var i = 0; i < paths.Length; i++)
                File.Copy(Path.Combine(backup, names[i] + ".candidate"), paths[i], true);
        }
        catch
        {
            for (var i = 0; i < paths.Length; i++) File.Copy(Path.Combine(backup, names[i]), paths[i], true);
            throw;
        }
        Console.WriteLine($"Published ownership only: {changed} / {owners.Count} sites; unchanged terrain/navigation. Hash {newHash}; backup {backup}");
    }
}
