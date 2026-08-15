namespace XianXia.Core.Attributes
{
    /// <summary>Unified character attributes (2B). Spirit-root affinities are separate (SpiritRootKind).</summary>
    public enum AttributeId
    {
        // Body
        /// <summary>生命上限（血条）；不是「体魄」。</summary>
        MaxHp = 0,
        Attack = 1,
        Defense = 2,
        Speed = 3,
        Stamina = 4,
        /// <summary>体魄：肉身属性（负重／劳作／部分近战与门槛）；非血条。</summary>
        Physique = 5,

        // Soul
        SpiritSense = 10,
        Comprehension = 11,

        // Cultivation resources (not spirit-root affinities)
        SpiritPower = 20,
        Cultivation = 21,
        MindState = 22
    }
}
