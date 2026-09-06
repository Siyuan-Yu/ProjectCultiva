namespace ContentAuthoring.Shared.HexWorld;

public readonly record struct FactionFlagCreateResult(
    bool Success,
    string Message,
    HexWorldFactionFlagDto? Flag)
{
    public static FactionFlagCreateResult Ok(HexWorldFactionFlagDto flag) =>
        new(true, $"已创建 FactionFlag '{flag.FlagId}'，Anchor ({flag.AnchorQ},{flag.AnchorR})。", flag);

    public static FactionFlagCreateResult Fail(string message) => new(false, message, null);
}

public static class FactionFlagAuthoringIdGenerator
{
    public static string Generate(HexWorldDefinitionDto world, HexCoordDto anchor)
    {
        var stem = $"base:flag_q{anchor.Q}_r{anchor.R}";
        if (world == null || world.FactionFlags.All(f => !string.Equals(f.FlagId, stem, StringComparison.Ordinal)))
            return stem;
        for (var suffix = 2; ; suffix++)
        {
            var candidate = stem + "_" + suffix;
            if (world.FactionFlags.All(f => !string.Equals(f.FlagId, candidate, StringComparison.Ordinal)))
                return candidate;
        }
    }
}
