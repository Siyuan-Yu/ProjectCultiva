namespace XianXia.Core.Attributes
{
    /// <summary>Unified character attributes (2B). Spirit-root affinities are separate (SpiritRootKind).</summary>
    public enum AttributeId
    {
        // Body
        MaxHp = 0,
        Attack = 1,
        Defense = 2,
        Speed = 3,
        Stamina = 4,

        // Soul
        SpiritSense = 10,
        Comprehension = 11,

        // Cultivation resources (not spirit-root affinities)
        SpiritPower = 20,
        Cultivation = 21,
        MindState = 22
    }
}
