using System;
using System.Collections.Generic;
using XianXia.Core.Cultivation;
using XianXia.Core.Results;
using XianXia.Data.Serialization;

namespace XianXia.Data.Content
{
    /// <summary>Content 侧熟练表 DTO（映射到 Core SkillMasteryProfile）。</summary>
    public sealed class SkillMasteryProfileDefinition
    {
        public List<SkillMasteryTierDefinition> Tiers { get; set; } = new List<SkillMasteryTierDefinition>();
        public List<SkillMasteryBreakthroughDefinition> Breakthroughs { get; set; } =
            new List<SkillMasteryBreakthroughDefinition>();
    }

    public sealed class SkillMasteryTierDefinition
    {
        public string Tier { get; set; }
        public int? CultivationSpeed { get; set; }
        public double? DamageAttackMult { get; set; }
        public double? AttackBonusPercent { get; set; }
        public int? DamageFlat { get; set; }
    }

    public sealed class SkillMasteryBreakthroughDefinition
    {
        public string From { get; set; }
        public string To { get; set; }
        public int ProgressRequired { get; set; }
        public List<SkillMasteryCostDefinition> Costs { get; set; } = new List<SkillMasteryCostDefinition>();
    }

    public sealed class SkillMasteryCostDefinition
    {
        public string ItemId { get; set; }
        public int Count { get; set; }
    }

    public static class SkillMasteryProfileParser
    {
        public static readonly HashSet<string> MasteryRootFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "tiers", "breakthroughs"
        };

        public static readonly HashSet<string> TierFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "tier", "cultivationSpeed", "damageAttackMult", "attackBonusPercent", "damageFlat"
        };

        public static readonly HashSet<string> BreakthroughFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "from", "to", "progressRequired", "costs"
        };

        public static readonly HashSet<string> CostFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "itemId", "count"
        };

        public static bool TryParse(
            JsonValue masteryNode,
            string context,
            ValidationReport report,
            out SkillMasteryProfileDefinition def)
        {
            def = null;
            if (masteryNode.Kind != JsonValueKind.Object)
            {
                report.Add(ErrorCode.ContentLoadFailed, "mastery must be object.", context);
                return false;
            }

            DefinitionSchema.RejectUnknownFields(masteryNode, MasteryRootFields, report, context + ".mastery");
            def = new SkillMasteryProfileDefinition();

            if (masteryNode.TryGetProperty("tiers", out var tiersNode))
            {
                if (tiersNode.Kind != JsonValueKind.Array)
                {
                    report.Add(ErrorCode.ContentLoadFailed, "mastery.tiers must be array.", context);
                    return false;
                }

                foreach (var row in tiersNode.Array)
                {
                    if (row.Kind != JsonValueKind.Object)
                    {
                        report.Add(ErrorCode.ContentLoadFailed, "mastery.tiers entry must be object.", context);
                        continue;
                    }

                    DefinitionSchema.RejectUnknownFields(row, TierFields, report, context + ".mastery.tier");
                    var tierText = row.GetString("tier", string.Empty);
                    if (!TryParseTier(tierText, out _))
                    {
                        report.Add(ErrorCode.InvalidArgument, "Unknown mastery tier.", context + ":" + tierText);
                        continue;
                    }

                    var tierDef = new SkillMasteryTierDefinition { Tier = tierText };
                    if (row.TryGetProperty("cultivationSpeed", out var cs) && cs.Kind == JsonValueKind.Number)
                        tierDef.CultivationSpeed = (int)cs.Number;
                    if (row.TryGetProperty("damageAttackMult", out var dm) && dm.Kind == JsonValueKind.Number)
                        tierDef.DamageAttackMult = dm.Number;
                    if (row.TryGetProperty("attackBonusPercent", out var ab) && ab.Kind == JsonValueKind.Number)
                        tierDef.AttackBonusPercent = ab.Number;
                    if (row.TryGetProperty("damageFlat", out var df) && df.Kind == JsonValueKind.Number)
                        tierDef.DamageFlat = (int)df.Number;
                    def.Tiers.Add(tierDef);
                }
            }

            if (masteryNode.TryGetProperty("breakthroughs", out var breaksNode))
            {
                if (breaksNode.Kind != JsonValueKind.Array)
                {
                    report.Add(ErrorCode.ContentLoadFailed, "mastery.breakthroughs must be array.", context);
                    return false;
                }

                foreach (var row in breaksNode.Array)
                {
                    if (row.Kind != JsonValueKind.Object)
                    {
                        report.Add(ErrorCode.ContentLoadFailed, "mastery.breakthroughs entry must be object.", context);
                        continue;
                    }

                    DefinitionSchema.RejectUnknownFields(row, BreakthroughFields, report, context + ".mastery.break");
                    var from = row.GetString("from", string.Empty);
                    var to = row.GetString("to", string.Empty);
                    if (!TryParseTier(from, out _) || !TryParseTier(to, out _))
                    {
                        report.Add(ErrorCode.InvalidArgument, "breakthrough from/to invalid.", context);
                        continue;
                    }

                    var b = new SkillMasteryBreakthroughDefinition
                    {
                        From = from,
                        To = to,
                        ProgressRequired = row.TryGetProperty("progressRequired", out var pr) &&
                                           pr.Kind == JsonValueKind.Number
                            ? (int)pr.Number
                            : 0
                    };
                    if (b.ProgressRequired <= 0)
                    {
                        report.Add(ErrorCode.InvalidArgument, "progressRequired must be > 0.", context);
                        continue;
                    }

                    if (row.TryGetProperty("costs", out var costsNode))
                    {
                        if (costsNode.Kind != JsonValueKind.Array)
                        {
                            report.Add(ErrorCode.ContentLoadFailed, "costs must be array.", context);
                            continue;
                        }

                        foreach (var cost in costsNode.Array)
                        {
                            if (cost.Kind != JsonValueKind.Object)
                                continue;
                            DefinitionSchema.RejectUnknownFields(cost, CostFields, report, context + ".cost");
                            var itemId = cost.GetString("itemId", string.Empty);
                            var count = cost.TryGetProperty("count", out var cn) && cn.Kind == JsonValueKind.Number
                                ? (int)cn.Number
                                : 0;
                            if (string.IsNullOrEmpty(itemId) || count <= 0)
                                continue;
                            b.Costs.Add(new SkillMasteryCostDefinition { ItemId = itemId, Count = count });
                        }
                    }

                    def.Breakthroughs.Add(b);
                }
            }

            return true;
        }

        public static SkillMasteryProfile ToCore(SkillMasteryProfileDefinition def)
        {
            var profile = new SkillMasteryProfile();
            if (def == null)
                return profile;
            if (def.Tiers != null)
            {
                for (var i = 0; i < def.Tiers.Count; i++)
                {
                    var t = def.Tiers[i];
                    if (t == null || !TryParseTier(t.Tier, out var tier))
                        continue;
                    profile.Tiers.Add(new SkillMasteryTierSpec
                    {
                        Tier = tier,
                        CultivationSpeed = t.CultivationSpeed,
                        DamageAttackMult = t.DamageAttackMult,
                        AttackBonusPercent = t.AttackBonusPercent,
                        DamageFlat = t.DamageFlat
                    });
                }
            }

            if (def.Breakthroughs != null)
            {
                for (var i = 0; i < def.Breakthroughs.Count; i++)
                {
                    var b = def.Breakthroughs[i];
                    if (b == null ||
                        !TryParseTier(b.From, out var from) ||
                        !TryParseTier(b.To, out var to))
                        continue;
                    var spec = new SkillMasteryBreakthroughSpec
                    {
                        From = from,
                        To = to,
                        ProgressRequired = b.ProgressRequired
                    };
                    if (b.Costs != null)
                    {
                        for (var c = 0; c < b.Costs.Count; c++)
                        {
                            var cost = b.Costs[c];
                            if (cost == null || string.IsNullOrEmpty(cost.ItemId) || cost.Count <= 0)
                                continue;
                            spec.Costs.Add(new SkillMasteryCostSpec
                            {
                                ItemId = cost.ItemId,
                                Count = cost.Count
                            });
                        }
                    }

                    profile.Breakthroughs.Add(spec);
                }
            }

            return profile;
        }

        public static bool TryParseTier(string text, out SkillMasteryTier tier)
        {
            tier = SkillMasteryTier.Entry;
            if (string.IsNullOrEmpty(text))
                return false;
            if (string.Equals(text, "novice", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "初学", StringComparison.Ordinal))
            {
                tier = SkillMasteryTier.Novice;
                return true;
            }

            if (string.Equals(text, "entry", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "入门", StringComparison.Ordinal))
            {
                tier = SkillMasteryTier.Entry;
                return true;
            }

            if (string.Equals(text, "minor", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "小成", StringComparison.Ordinal))
            {
                tier = SkillMasteryTier.Minor;
                return true;
            }

            if (string.Equals(text, "major", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "大成", StringComparison.Ordinal))
            {
                tier = SkillMasteryTier.Major;
                return true;
            }

            if (string.Equals(text, "perfect", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "圆满", StringComparison.Ordinal))
            {
                tier = SkillMasteryTier.Perfect;
                return true;
            }

            if (string.Equals(text, "transcendent", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "化境", StringComparison.Ordinal))
            {
                tier = SkillMasteryTier.Transcendent;
                return true;
            }

            return Enum.TryParse(text, true, out tier);
        }
    }
}
