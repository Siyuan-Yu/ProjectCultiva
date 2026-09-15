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
            : this(subject, kind, durationTicks, target, null)
        {
        }

        public PlayerCommandRequest(
            EntityId subject,
            PlayerCommandKind kind,
            ulong durationTicks,
            EntityId target,
            string targetLocationId)
            : this(subject, kind, durationTicks, target, targetLocationId, null, null)
        {
        }

        public PlayerCommandRequest(
            EntityId subject,
            PlayerCommandKind kind,
            ulong durationTicks,
            EntityId target,
            string targetLocationId,
            string choiceId,
            string questId)
        {
            Subject = subject;
            Kind = kind;
            DurationTicks = durationTicks;
            Target = target;
            TargetLocationId = targetLocationId ?? string.Empty;
            ChoiceId = choiceId ?? string.Empty;
            QuestId = questId ?? string.Empty;
        }

        public EntityId Subject { get; }

        public EntityId Target { get; }

        public PlayerCommandKind Kind { get; }

        public ulong DurationTicks { get; }

        /// <summary>Travel 的地点 ID，或有目标世界交互的稳定引用（例如恢复处 ID）。</summary>
        public string TargetLocationId { get; }

        /// <summary>Content Ready: used when Kind == ResolveContentChoice.</summary>
        public string ChoiceId { get; }

        /// <summary>Content Ready: used when Kind == StartQuest.</summary>
        public string QuestId { get; }

        public bool IsSocialIntent =>
            Kind == PlayerCommandKind.Help ||
            Kind == PlayerCommandKind.Slight ||
            Kind == PlayerCommandKind.Recruit;

        public bool IsExplorationIntent =>
            Kind == PlayerCommandKind.Explore ||
            Kind == PlayerCommandKind.Travel ||
            Kind == PlayerCommandKind.EnterLocalMap ||
            Kind == PlayerCommandKind.LeaveLocalMap ||
            Kind == PlayerCommandKind.SurveyEntrance;

        public bool IsContentIntent =>
            Kind == PlayerCommandKind.ResolveContentChoice ||
            Kind == PlayerCommandKind.StartQuest ||
            Kind == PlayerCommandKind.ClaimQuestRewards ||
            Kind == PlayerCommandKind.AbandonQuest;

        public bool IsInstantUtilityIntent =>
            Kind == PlayerCommandKind.Stop ||
            Kind == PlayerCommandKind.UseConcealGrass;
    }
}
