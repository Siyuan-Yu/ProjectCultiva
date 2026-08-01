using XianXia.Core.Attributes;
using XianXia.Core.Cultivation;
using XianXia.Core.Entities;
using XianXia.Core.Social;

namespace XianXia.Core.Content
{
    /// <summary>Thin talent → cultivation growth hooks (content tags, Core interprets).</summary>
    public static class TalentGrowthRules
    {
        public const string TagMixedRoot = "talent_mixed_root";
        public const string TagMetalRoot = "talent_metal_root";
        public const string TagFireRoot = "talent_fire_root";
        public const string TagHerbSense = "talent_herb_sense";
        public const string TagEnduring = "talent_enduring";

        /// <summary>Extra Progress granted on Cultivate ticks / Cultivate work-day bonus.</summary>
        public static int ExtraCultivateProgress(PersonalityProfileComponent profile)
        {
            if (profile == null)
                return 0;
            var bonus = 0;
            if (profile.HasTag(TagMixedRoot))
                bonus += 1;
            if (profile.HasTag(TagMetalRoot) || profile.HasTag(TagFireRoot))
                bonus += 2;
            if (profile.HasTag(TagHerbSense))
                bonus += 1;
            if (profile.HasTag(TagEnduring))
                bonus += 1;
            return bonus;
        }

        /// <summary>Flat MaxHp granted once on Mortal→QiRefining breakthrough.</summary>
        public static int BreakthroughMaxHpBonus(PersonalityProfileComponent profile)
        {
            if (profile == null)
                return 0;
            if (profile.HasTag(TagEnduring))
                return 10;
            if (profile.HasTag(TagMixedRoot))
                return 5;
            return 0;
        }

        public static void ApplyBreakthroughBonuses(
            PersonalityProfileComponent profile,
            AttributesComponent attributes)
        {
            if (attributes == null)
                return;
            var hp = BreakthroughMaxHpBonus(profile);
            if (hp > 0)
                attributes.SetBase(AttributeId.MaxHp, attributes.GetBase(AttributeId.MaxHp) + hp);
        }
    }
}
