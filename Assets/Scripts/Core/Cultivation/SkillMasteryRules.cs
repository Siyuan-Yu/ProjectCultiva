using System;
using System.Collections.Generic;

namespace XianXia.Core.Cultivation
{
    /// <summary>熟练度／研读缺省约定；正式数值优先读定义上的 <see cref="SkillMasteryProfile"/>。</summary>
    public static class SkillMasteryRules
    {
        public const int ProgressEntryToMinor = 100;
        public const int UseArtProgressGain = 3;
        public const int CultivateManualProgressGain = 1;
        public const int InfuseProgressPerPoint = 2;
        public const int BreakthroughHerbCost = 10;
        public const int BreakthroughWoodCost = 10;
        public const string HerbResourceId = "base:resource_spirit_herb";
        public const string WoodResourceId = "base:resource_rough_wood";

        public static readonly IReadOnlyList<SkillMasteryCostSpec> DefaultBreakthroughCosts =
            new List<SkillMasteryCostSpec>
            {
                new SkillMasteryCostSpec { ItemId = HerbResourceId, Count = BreakthroughHerbCost },
                new SkillMasteryCostSpec { ItemId = WoodResourceId, Count = BreakthroughWoodCost }
            };

        /// <summary>无配置表时的缺省：仅入门→小成。</summary>
        public static int ProgressRequiredToNext(SkillMasteryTier tier)
        {
            if (tier == SkillMasteryTier.Entry)
                return ProgressEntryToMinor;
            return 0;
        }

        public static bool CanBreakthrough(SkillMasteryTier tier) =>
            tier == SkillMasteryTier.Entry;

        public static SkillMasteryTier NextTier(SkillMasteryTier tier)
        {
            if (tier >= SkillMasteryTier.Transcendent)
                return tier;
            return (SkillMasteryTier)((int)tier + 1);
        }

        /// <summary>
        /// 研读学习成功率 0..1。悟性越高越好；品阶越高越难；适配加分。
        /// 仅用于点学／入门掷骰，与战斗释放无关。
        /// </summary>
        public static double LearnSuccessChance(int comprehension, string grade, double affinityBonus)
        {
            var chance = 0.45 + Math.Max(0, comprehension) * 0.012;
            chance += GradeEase(grade);
            chance += Math.Max(0, Math.Min(0.2, affinityBonus));
            if (chance < 0.12) chance = 0.12;
            if (chance > 0.92) chance = 0.92;
            return chance;
        }

        public static double MasteryBreakthroughChance(int comprehension)
        {
            var chance = 0.72 + Math.Max(0, comprehension) * 0.008;
            if (chance < 0.4) chance = 0.4;
            if (chance > 0.95) chance = 0.95;
            return chance;
        }

        static double GradeEase(string grade)
        {
            if (string.IsNullOrEmpty(grade))
                return 0.08;
            if (grade.IndexOf("黄", StringComparison.Ordinal) >= 0)
                return 0.18;
            if (grade.IndexOf("玄", StringComparison.Ordinal) >= 0)
                return 0.05;
            if (grade.IndexOf("地", StringComparison.Ordinal) >= 0)
                return -0.08;
            if (grade.IndexOf("天", StringComparison.Ordinal) >= 0)
                return -0.18;
            return 0.05;
        }
    }
}
