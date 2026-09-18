namespace XianXia.Core.Exploration
{
    /// <summary>
    /// Separate playable space 种类（SPACE-01）。不做复杂 hierarchy；Cave 为第一份正式样板。
    /// </summary>
    public enum SeparateSpaceKind : byte
    {
        None = 0,
        Cave = 1,
        Interior = 2,
        Dungeon = 3,
        SeparateMap = 4
    }
}
