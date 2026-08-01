using XianXia.Core.Schedule;

namespace XianXia.Core.Social
{
    /// <summary>
    /// VS0.5 Phase E: deterministic personality micro-bias for Schedule-sourced orders.
    /// Does not invent AI, pathfinding, or combat. Player Orders remain supreme.
    /// </summary>
    public static class PersonalityScheduleBias
    {
        public const string TagBold = "personality_bold";
        public const string TagCautious = "personality_cautious";
        public const string TagCurious = "personality_curious";

        public static BiasedScheduleChoice Apply(
            ScheduleBlock block,
            PersonalityProfileComponent profile)
        {
            if (block == null)
                return new BiasedScheduleChoice(ScheduleActivity.Rest, 1);

            var activity = ResolveActivity(block.Activity, profile);
            var duration = AdjustDuration(activity, block.OrderDurationTicks, profile);
            return new BiasedScheduleChoice(activity, duration);
        }

        /// <summary>
        /// Bold may treat a Rest block as short Labor; cautious never skips Labor blocks
        /// (quota safety). Conflicting bold+cautious cancel the flip.
        /// </summary>
        public static ScheduleActivity ResolveActivity(
            ScheduleActivity scheduled,
            PersonalityProfileComponent profile)
        {
            if (profile == null)
                return scheduled;

            var bold = profile.HasTag(TagBold);
            var cautious = profile.HasTag(TagCautious);
            if (bold == cautious)
                return scheduled;

            if (scheduled == ScheduleActivity.Rest && bold)
                return ScheduleActivity.Labor;

            return scheduled;
        }

        public static ulong AdjustDuration(
            ScheduleActivity activity,
            ulong baseDuration,
            PersonalityProfileComponent profile)
        {
            if (baseDuration == 0)
                return 0;
            if (profile == null)
                return baseDuration;

            var delta = 0;
            var bold = profile.HasTag(TagBold);
            var cautious = profile.HasTag(TagCautious);
            var curious = profile.HasTag(TagCurious);

            if (activity == ScheduleActivity.Labor)
            {
                if (bold && !cautious)
                    delta += SocialAlphaConstants.BoldLaborDurationBonus;
                if (cautious && !bold)
                    delta -= SocialAlphaConstants.CautiousLaborDurationPenalty;
                if (curious && !cautious)
                    delta += SocialAlphaConstants.CuriousLaborDurationBonus;
            }
            else
            {
                if (bold && !cautious)
                    delta -= SocialAlphaConstants.BoldRestDurationPenalty;
                if (cautious && !bold)
                    delta += SocialAlphaConstants.CautiousRestDurationBonus;
            }

            var adjusted = (long)baseDuration + delta;
            return adjusted < 1 ? 1UL : (ulong)adjusted;
        }
    }

    public readonly struct BiasedScheduleChoice
    {
        public BiasedScheduleChoice(ScheduleActivity activity, ulong durationTicks)
        {
            Activity = activity;
            DurationTicks = durationTicks;
        }

        public ScheduleActivity Activity { get; }

        public ulong DurationTicks { get; }
    }
}
