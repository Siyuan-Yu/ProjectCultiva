namespace XianXia.Core.World.Hex
{
    /// <summary>
    /// Legacy Hex compatibility travel mode。V1 仅 Ground；不是正常 Continuous Surface 的移动模式 authority。
    /// </summary>
    public enum HexTravelMode
    {
        Ground = 0,
        // Flight = 1, // Phase 后续
    }
}
