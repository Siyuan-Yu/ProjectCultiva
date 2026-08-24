using System;
using System.IO;
using ContentAuthoring.Shared.HexWorld;
using Xunit;

namespace ContentAuthoring.Shared.Tests;

public sealed class Ch01HexWorldRoundtripTests
{
    [Fact]
    public void Ch01HexWorld_NoOpRoundtrip_Pass()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", ".."));
        var path = Path.Combine(repoRoot, "Content", "BaseGame", "Data", "Worlds", "ch01_hex_world.json");
        Assert.True(File.Exists(path), $"Missing {path}");

        var loaded = HexWorldContentJson.LoadFile(path);
        var def = HexWorldContentJson.LoadDefinition(loaded);
        HexWorldContentJson.NormalizeForSave(def);
        var roundtrip = HexWorldContentJson.Serialize(def);

        var reloaded = HexWorldContentJson.Load(roundtrip);
        var def2 = HexWorldContentJson.LoadDefinition(reloaded);
        HexWorldContentJson.NormalizeForSave(def2);
        var roundtrip2 = HexWorldContentJson.Serialize(def2);
        Assert.Equal(roundtrip, roundtrip2);

        var origDef = HexWorldContentJson.LoadDefinition(HexWorldContentJson.Load(File.ReadAllText(path)));
        HexWorldContentJson.NormalizeForSave(origDef);
        var normalizedOrig = HexWorldContentJson.Serialize(origDef);
        Assert.Equal(normalizedOrig, roundtrip);
    }
}
