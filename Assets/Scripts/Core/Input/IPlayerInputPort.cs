using XianXia.Core.Results;

namespace XianXia.Core.Input
{
    /// <summary>
    /// Sole Core entry for player behavior changes (VS0.2). UI/Host must not mutate components directly.
    /// </summary>
    public interface IPlayerInputPort
    {
        Result Submit(PlayerCommandRequest request);
    }
}
