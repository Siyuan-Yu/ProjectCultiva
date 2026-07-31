namespace XianXia.Core.Random
{
    /// <summary>
    /// Injectable deterministic random source. Snapshots must persist full state via <see cref="CaptureState"/>.
    /// </summary>
    public interface IRandomSource
    {
        RandomStreamId StreamId { get; }

        int NextInt(int minInclusive, int maxExclusive);

        double NextDouble();

        RandomState CaptureState();

        void RestoreState(RandomState state);
    }
}
