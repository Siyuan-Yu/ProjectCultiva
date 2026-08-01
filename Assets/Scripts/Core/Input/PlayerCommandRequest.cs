using XianXia.Core.Domain.Ids;

namespace XianXia.Core.Input
{
    /// <summary>
    /// RTS-style command: select Entity + intent. Phase A uses Labor (Rest/Observe reserved).
    /// </summary>
    public sealed class PlayerCommandRequest
    {
        public PlayerCommandRequest(EntityId subject, PlayerCommandKind kind, ulong durationTicks)
        {
            Subject = subject;
            Kind = kind;
            DurationTicks = durationTicks;
        }

        public EntityId Subject { get; }

        public PlayerCommandKind Kind { get; }

        public ulong DurationTicks { get; }
    }
}
