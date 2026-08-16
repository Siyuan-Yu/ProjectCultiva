namespace XianXia.Core.Cultivation
{
    /// <summary>单门功法／斗技的熟练状态。</summary>
    public sealed class SkillMasteryState
    {
        public SkillMasteryTier Tier { get; set; } = SkillMasteryTier.Entry;
        public int Progress { get; set; }
        public int ProgressRequired { get; set; }

        public bool IsAtBottleneck =>
            ProgressRequired > 0 && Progress >= ProgressRequired;

        public static SkillMasteryState CreateEntry(SkillMasteryProfile profile = null)
        {
            return new SkillMasteryState
            {
                Tier = SkillMasteryTier.Entry,
                Progress = 0,
                ProgressRequired = SkillMasteryLookup.ProgressRequiredToNext(profile, SkillMasteryTier.Entry)
            };
        }

        public SkillMasteryState Clone() =>
            new SkillMasteryState
            {
                Tier = Tier,
                Progress = Progress,
                ProgressRequired = ProgressRequired
            };
    }
}
