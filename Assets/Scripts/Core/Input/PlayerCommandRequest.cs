using XianXia.Core.Domain.Ids;
using XianXia.Core.Settlement;

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
            : this(subject, kind, durationTicks, target, WorkRoleKind.None)
        {
        }

        public PlayerCommandRequest(
            EntityId subject,
            PlayerCommandKind kind,
            ulong durationTicks,
            EntityId target,
            WorkRoleKind workRole)
        {
            Subject = subject;
            Kind = kind;
            DurationTicks = durationTicks;
            Target = target;
            WorkRole = workRole;
        }

        public EntityId Subject { get; }

        public EntityId Target { get; }

        public PlayerCommandKind Kind { get; }

        public ulong DurationTicks { get; }

        /// <summary>VS0.8: used when Kind == AssignWork.</summary>
        public WorkRoleKind WorkRole { get; }

        public bool IsSocialIntent =>
            Kind == PlayerCommandKind.Help ||
            Kind == PlayerCommandKind.Slight ||
            Kind == PlayerCommandKind.Recruit;

        public bool IsSettlementIntent => Kind == PlayerCommandKind.AssignWork;
    }
}
