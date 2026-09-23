namespace XianXia.Core.World.Surface
{
    /// <summary>Shared player-centered Continuous Surface streaming policy.</summary>
    public static class ContinuousSurfaceStreamingPolicy
    {
        public const int ActiveRadiusChunks = 2;
        public const int ActiveDiameterChunks = ActiveRadiusChunks * 2 + 1;
    }
}
