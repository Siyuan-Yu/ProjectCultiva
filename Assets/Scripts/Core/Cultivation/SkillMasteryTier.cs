namespace XianXia.Core.Cultivation
{
    /// <summary>功法／斗技掌握档位（文档：初学→入门→小成→大成→圆满→化境）。</summary>
    public enum SkillMasteryTier
    {
        Novice = 0,
        Entry = 1,
        Minor = 2,
        Major = 3,
        Perfect = 4,
        Transcendent = 5
    }

    public static class SkillMasteryTierNames
    {
        public static string Display(SkillMasteryTier tier)
        {
            switch (tier)
            {
                case SkillMasteryTier.Novice: return "初学";
                case SkillMasteryTier.Entry: return "入门";
                case SkillMasteryTier.Minor: return "小成";
                case SkillMasteryTier.Major: return "大成";
                case SkillMasteryTier.Perfect: return "圆满";
                case SkillMasteryTier.Transcendent: return "化境";
                default: return tier.ToString();
            }
        }
    }
}
