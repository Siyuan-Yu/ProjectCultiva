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
            : this(subject, kind, durationTicks, target, WorkRoleKind.None, null)
        {
        }

        public PlayerCommandRequest(
            EntityId subject,
            PlayerCommandKind kind,
            ulong durationTicks,
            EntityId target,
            WorkRoleKind workRole)
            : this(subject, kind, durationTicks, target, workRole, null)
        {
        }

        public PlayerCommandRequest(
            EntityId subject,
            PlayerCommandKind kind,
            ulong durationTicks,
            EntityId target,
            WorkRoleKind workRole,
            string targetLocationId)
        {
            Subject = subject;
            Kind = kind;
            DurationTicks = durationTicks;
            Target = target;
            WorkRole = workRole;
            TargetLocationId = targetLocationId ?? string.Empty;
        }

        public EntityId Subject { get; }

        public EntityId Target { get; }

        public PlayerCommandKind Kind { get; }

        public ulong DurationTicks { get; }

        public WorkRoleKind WorkRole { get; }

        /// <summary>VS0.9: used when Kind == Travel.</summary>
        public string TargetLocationId { get; }

        public bool IsSocialIntent =>
            Kind == PlayerCommandKind.Help ||
            Kind == PlayerCommandKind.Slight ||
            Kind == PlayerCommandKind.Recruit;

        public bool IsSettlementIntent => Kind == PlayerCommandKind.AssignWork;

        public bool IsExplorationIntent =>
            Kind == PlayerCommandKind.Explore ||
            Kind == PlayerCommandKind.Travel;
    }
}
