namespace XianXia.Core.Domain.Ids
{
    /// <summary>
    /// Source categories for <see cref="SourceRef"/> (2C whitelist, Phase 2 enum only).
    /// </summary>
    public enum SourceKind : byte
    {
        Talent = 0,
        SpiritRoot = 1,
        Manual = 2,
        Skill = 3,
        Realm = 4,
        Environment = 5,
        Building = 6,
        StatusEffect = 7,
        Equipment = 8,
        Event = 9
    }
}
