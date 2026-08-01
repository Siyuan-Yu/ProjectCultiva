using XianXia.Core.Domain.Ids;

namespace XianXia.Core.Input
{
    /// <summary>
    /// RTS-style command: Subject (+ optional Target for social intents).
    /// </summary>
    public sealed class PlayerCommandRequest
    {
        public PlayerCommandRequest(EntityId subject, PlayerCommandKind kind, ulong durationTicks)
            : this(subject, kind, durationTicks, EntityId.None)
        {
        }

        public PlayerCommandRequest(
            EntityId subject,
            PlayerCommandKind kind,
            ulong durationTicks,
            EntityId target)
        {
            Subject = subject;
            Kind = kind;
            DurationTicks = durationTicks;
            Target = target;
        }

        public EntityId Subject { get; }

        public EntityId Target { get; }

        public PlayerCommandKind Kind { get; }

        public ulong DurationTicks { get; }

        public bool IsSocialIntent =>
            Kind == PlayerCommandKind.Help ||
            Kind == PlayerCommandKind.Slight ||
            Kind == PlayerCommandKind.Recruit;
    }
}
