using System;
using System.Collections.Generic;

namespace XianXia.Core.Cultivation
{
    /// <summary>突破消耗：道具 id＋数量。</summary>
    public sealed class SkillMasteryCostSpec
    {
        public string ItemId { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    /// <summary>单档绝对值效果（不连乘）。未填的字段回落到定义上的基础值。</summary>
    public sealed class SkillMasteryTierSpec
    {
        public SkillMasteryTier Tier { get; set; }

        /// <summary>功法：该档打坐每 tick 修为（绝对值）。</summary>
        public int? CultivationSpeed { get; set; }

        /// <summary>斗技主动：攻击力倍率绝对值（2＝200%）。</summary>
        public double? DamageAttackMult { get; set; }

        /// <summary>斗技被动：普攻加成绝对值（0.12＝+12%）。</summary>
        public double? AttackBonusPercent { get; set; }

        /// <summary>斗技被动：固伤绝对值。</summary>
        public int? DamageFlat { get; set; }
    }

    /// <summary>从某档冲击下一档：熟练进度门槛＋材料。</summary>
    public sealed class SkillMasteryBreakthroughSpec
    {
        public SkillMasteryTier From { get; set; }
        public SkillMasteryTier To { get; set; }
        public int ProgressRequired { get; set; }
        public List<SkillMasteryCostSpec> Costs { get; set; } = new List<SkillMasteryCostSpec>();
    }

    /// <summary>功法／斗技共用的熟练配置表。</summary>
    public sealed class SkillMasteryProfile
    {
        public List<SkillMasteryTierSpec> Tiers { get; set; } = new List<SkillMasteryTierSpec>();
        public List<SkillMasteryBreakthroughSpec> Breakthroughs { get; set; } =
            new List<SkillMasteryBreakthroughSpec>();

        public bool TryGetTier(SkillMasteryTier tier, out SkillMasteryTierSpec spec)
        {
            spec = null;
            if (Tiers == null)
                return false;
            for (var i = 0; i < Tiers.Count; i++)
            {
                if (Tiers[i] != null && Tiers[i].Tier == tier)
                {
                    spec = Tiers[i];
                    return true;
                }
            }

            return false;
        }

        public bool TryGetBreakthroughFrom(SkillMasteryTier from, out SkillMasteryBreakthroughSpec spec)
        {
            spec = null;
            if (Breakthroughs == null)
                return false;
            for (var i = 0; i < Breakthroughs.Count; i++)
            {
                if (Breakthroughs[i] != null && Breakthroughs[i].From == from)
                {
                    spec = Breakthroughs[i];
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>按配置解析熟练效果／门槛（缺省回落旧硬编码）。</summary>
    public static class SkillMasteryLookup
    {
        public static int ResolveCultivationSpeed(CultivationManualSpec manual, SkillMasteryTier tier)
        {
            if (manual == null)
                return 1;
            if (manual.Mastery != null &&
                manual.Mastery.TryGetTier(tier, out var row) &&
                row.CultivationSpeed.HasValue &&
                row.CultivationSpeed.Value > 0)
                return row.CultivationSpeed.Value;
            return Math.Max(1, manual.CultivationSpeed);
        }

        public static double ResolveDamageAttackMult(Combat.CombatArtSpec art, SkillMasteryTier tier)
        {
            if (art == null)
                return 0;
            if (art.Mastery != null &&
                art.Mastery.TryGetTier(tier, out var row) &&
                row.DamageAttackMult.HasValue &&
                row.DamageAttackMult.Value > 0)
                return row.DamageAttackMult.Value;
            return art.DamageAttackMult;
        }

        public static double ResolveAttackBonusPercent(Combat.CombatArtSpec art, SkillMasteryTier tier)
        {
            if (art == null)
                return 0;
            if (art.Mastery != null &&
                art.Mastery.TryGetTier(tier, out var row) &&
                row.AttackBonusPercent.HasValue)
                return row.AttackBonusPercent.Value;
            return art.AttackBonusPercent;
        }

        public static int ResolveDamageFlat(Combat.CombatArtSpec art, SkillMasteryTier tier)
        {
            if (art == null)
                return 0;
            if (art.Mastery != null &&
                art.Mastery.TryGetTier(tier, out var row) &&
                row.DamageFlat.HasValue)
                return row.DamageFlat.Value;
            return art.DamageFlat;
        }

        public static void SyncProgressCap(SkillMasteryState state, SkillMasteryProfile profile)
        {
            if (state == null)
                return;
            if (!CanBreakthrough(profile, state.Tier))
            {
                state.ProgressRequired = 0;
                return;
            }

            var req = ProgressRequiredToNext(profile, state.Tier);
            state.ProgressRequired = req;
            if (state.Progress > state.ProgressRequired)
                state.Progress = state.ProgressRequired;
        }

        public static int ProgressRequiredToNext(SkillMasteryProfile profile, SkillMasteryTier tier)
        {
            if (profile != null &&
                profile.TryGetBreakthroughFrom(tier, out var b) &&
                b.ProgressRequired > 0)
                return b.ProgressRequired;
            return SkillMasteryRules.ProgressRequiredToNext(tier);
        }

        public static bool CanBreakthrough(SkillMasteryProfile profile, SkillMasteryTier tier)
        {
            if (profile != null && profile.TryGetBreakthroughFrom(tier, out var b))
                return b != null && b.ProgressRequired > 0;
            return SkillMasteryRules.CanBreakthrough(tier);
        }

        public static SkillMasteryTier NextTier(SkillMasteryProfile profile, SkillMasteryTier from)
        {
            if (profile != null && profile.TryGetBreakthroughFrom(from, out var b))
                return b.To;
            return SkillMasteryRules.NextTier(from);
        }

        public static IReadOnlyList<SkillMasteryCostSpec> BreakthroughCosts(
            SkillMasteryProfile profile,
            SkillMasteryTier from)
        {
            if (profile != null && profile.TryGetBreakthroughFrom(from, out var b) && b.Costs != null && b.Costs.Count > 0)
                return b.Costs;
            return SkillMasteryRules.DefaultBreakthroughCosts;
        }

        /// <summary>无配置时：入门＝基础值，小成＝基础略增，并带默认入门→小成突破。</summary>
        public static SkillMasteryProfile EnsureOrDefaultManual(CultivationManualSpec manual)
        {
            if (manual == null)
                return CreateDefaultManualProfile(1);
            if (manual.Mastery != null &&
                (manual.Mastery.Tiers.Count > 0 || manual.Mastery.Breakthroughs.Count > 0))
                return manual.Mastery;
            var profile = CreateDefaultManualProfile(Math.Max(1, manual.CultivationSpeed));
            manual.Mastery = profile;
            return profile;
        }

        public static SkillMasteryProfile EnsureOrDefaultArt(Combat.CombatArtSpec art)
        {
            if (art == null)
                return CreateDefaultArtProfile(0, 0, 0);
            if (art.Mastery != null &&
                (art.Mastery.Tiers.Count > 0 || art.Mastery.Breakthroughs.Count > 0))
                return art.Mastery;
            var profile = CreateDefaultArtProfile(art.DamageAttackMult, art.AttackBonusPercent, art.DamageFlat);
            art.Mastery = profile;
            return profile;
        }

        public static SkillMasteryProfile CreateDefaultManualProfile(int baseSpeed)
        {
            var entry = Math.Max(1, baseSpeed);
            var minor = Math.Max(entry + 1, (int)Math.Round(entry * 1.25));
            return new SkillMasteryProfile
            {
                Tiers =
                {
                    new SkillMasteryTierSpec { Tier = SkillMasteryTier.Novice, CultivationSpeed = Math.Max(1, entry - 1) },
                    new SkillMasteryTierSpec { Tier = SkillMasteryTier.Entry, CultivationSpeed = entry },
                    new SkillMasteryTierSpec { Tier = SkillMasteryTier.Minor, CultivationSpeed = minor },
                    new SkillMasteryTierSpec { Tier = SkillMasteryTier.Major, CultivationSpeed = minor + 2 },
                    new SkillMasteryTierSpec { Tier = SkillMasteryTier.Perfect, CultivationSpeed = minor + 4 },
                    new SkillMasteryTierSpec { Tier = SkillMasteryTier.Transcendent, CultivationSpeed = minor + 8 }
                },
                Breakthroughs =
                {
                    DefaultEntryToMinorBreakthrough()
                }
            };
        }

        public static SkillMasteryProfile CreateDefaultArtProfile(
            double damageMult,
            double attackBonusPct,
            int damageFlat)
        {
            var profile = new SkillMasteryProfile();
            if (damageMult > 0)
            {
                profile.Tiers.Add(new SkillMasteryTierSpec
                {
                    Tier = SkillMasteryTier.Novice,
                    DamageAttackMult = Math.Max(0.1, damageMult * 0.9)
                });
                profile.Tiers.Add(new SkillMasteryTierSpec
                {
                    Tier = SkillMasteryTier.Entry,
                    DamageAttackMult = damageMult
                });
                profile.Tiers.Add(new SkillMasteryTierSpec
                {
                    Tier = SkillMasteryTier.Minor,
                    DamageAttackMult = damageMult + 0.2
                });
                profile.Tiers.Add(new SkillMasteryTierSpec
                {
                    Tier = SkillMasteryTier.Major,
                    DamageAttackMult = damageMult + 0.4
                });
                profile.Tiers.Add(new SkillMasteryTierSpec
                {
                    Tier = SkillMasteryTier.Perfect,
                    DamageAttackMult = damageMult + 0.6
                });
                profile.Tiers.Add(new SkillMasteryTierSpec
                {
                    Tier = SkillMasteryTier.Transcendent,
                    DamageAttackMult = damageMult + 1.0
                });
            }
            else
            {
                profile.Tiers.Add(new SkillMasteryTierSpec
                {
                    Tier = SkillMasteryTier.Novice,
                    AttackBonusPercent = Math.Max(0, attackBonusPct - 0.02),
                    DamageFlat = Math.Max(0, damageFlat - 1)
                });
                profile.Tiers.Add(new SkillMasteryTierSpec
                {
                    Tier = SkillMasteryTier.Entry,
                    AttackBonusPercent = attackBonusPct,
                    DamageFlat = damageFlat
                });
                profile.Tiers.Add(new SkillMasteryTierSpec
                {
                    Tier = SkillMasteryTier.Minor,
                    AttackBonusPercent = attackBonusPct + 0.03,
                    DamageFlat = damageFlat + 1
                });
                profile.Tiers.Add(new SkillMasteryTierSpec
                {
                    Tier = SkillMasteryTier.Major,
                    AttackBonusPercent = attackBonusPct + 0.06,
                    DamageFlat = damageFlat + 2
                });
                profile.Tiers.Add(new SkillMasteryTierSpec
                {
                    Tier = SkillMasteryTier.Perfect,
                    AttackBonusPercent = attackBonusPct + 0.09,
                    DamageFlat = damageFlat + 3
                });
                profile.Tiers.Add(new SkillMasteryTierSpec
                {
                    Tier = SkillMasteryTier.Transcendent,
                    AttackBonusPercent = attackBonusPct + 0.12,
                    DamageFlat = damageFlat + 4
                });
            }

            profile.Breakthroughs.Add(DefaultEntryToMinorBreakthrough());
            return profile;
        }

        public static SkillMasteryBreakthroughSpec DefaultEntryToMinorBreakthrough() =>
            new SkillMasteryBreakthroughSpec
            {
                From = SkillMasteryTier.Entry,
                To = SkillMasteryTier.Minor,
                ProgressRequired = SkillMasteryRules.ProgressEntryToMinor,
                Costs = new List<SkillMasteryCostSpec>(SkillMasteryRules.DefaultBreakthroughCosts)
            };
    }
}
