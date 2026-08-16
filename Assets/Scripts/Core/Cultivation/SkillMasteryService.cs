using System;
using System.Collections.Generic;
using XianXia.Core.Attributes;
using XianXia.Core.Combat;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Events;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.Cultivation
{
    public sealed class SkillStudyReport
    {
        public bool Success { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public double ChanceUsed { get; set; }
        public SkillMasteryTier TierAfter { get; set; }
    }

    /// <summary>
    /// 功法／斗技研读结算、熟练增长、修为灌注、入门→小成材料突破。
    /// 「成功率」仅指学习／熟练突破掷骰，与战斗释放无关（释放不掷学习成功）。
    /// </summary>
    public sealed class SkillMasteryService
    {
        readonly CultivationService _cultivation = new CultivationService();
        readonly System.Random _rng = new System.Random();

        /// <summary>点学功法：学习成功率（悟性＋品阶＋适配）。</summary>
        public double EvaluateManualLearnChance(SimulationWorld world, EntityId subject, CultivationManualSpec manual)
        {
            if (world == null || manual == null || !world.Entities.TryGet(subject, out var e))
                return 0;
            var sense = 0;
            if (e.TryGet<AttributesComponent>(out var attrs))
                sense = attrs.GetFinal(AttributeId.Comprehension);
            return SkillMasteryRules.LearnSuccessChance(sense, manual.Grade, 0.08);
        }

        /// <summary>点学斗技：学习成功率（悟性＋品阶＋适配）；非释放命中率。</summary>
        public double EvaluateArtLearnChance(SimulationWorld world, EntityId subject, CombatArtSpec art)
        {
            if (world == null || art == null || !world.Entities.TryGet(subject, out var e))
                return 0;
            var sense = 0;
            if (e.TryGet<AttributesComponent>(out var attrs))
                sense = attrs.GetFinal(AttributeId.Comprehension);
            return SkillMasteryRules.LearnSuccessChance(sense, art.Grade, 0.08);
        }

        /// <summary>蓄势结束：掷骰学习功法。失败不授予。战斗释放时不调用。</summary>
        public Result TryFinishManualStudy(
            SimulationWorld world,
            EntityId subject,
            CultivationManualSpec manual,
            out SkillStudyReport report)
        {
            report = new SkillStudyReport();
            if (world == null || manual == null)
                return Result.Failure(ErrorCode.InvalidArgument, "Args null.");
            var chance = EvaluateManualLearnChance(world, subject, manual);
            report.ChanceUsed = chance;
            if (_rng.NextDouble() > chance)
            {
                report.Success = false;
                report.Title = "研读未悟";
                report.Body = "学习「" + DisplayName(manual.Name, manual.Id) +
                              "」失败。秘籍仍在，可再试。";
                return Result.Success();
            }

            var learned = _cultivation.LearnManual(world, subject, manual);
            if (learned.IsFailure)
                return learned;

            if (world.Entities.TryGet(subject, out var entity) &&
                entity.TryGet<CultivationComponent>(out var cult))
            {
                cult.ManualMastery = SkillMasteryState.CreateEntry(
                    SkillMasteryLookup.EnsureOrDefaultManual(manual));
                _cultivation.ReapplyManualModifiers(world, subject);
            }

            report.Success = true;
            report.TierAfter = SkillMasteryTier.Entry;
            report.Title = "功法入门";
            var speed = SkillMasteryLookup.ResolveCultivationSpeed(manual, SkillMasteryTier.Entry);
            report.Body = "已学会「" + DisplayName(manual.Name, manual.Id) +
                          "」·入门。\n打坐每 5 游戏分 +" + speed + " 修为。";
            return Result.Success();
        }

        /// <summary>蓄势结束：掷骰学习斗技。失败不授予。战斗释放不掷此骰。</summary>
        public Result TryFinishArtStudy(
            SimulationWorld world,
            EntityId subject,
            string itemId,
            out SkillStudyReport report)
        {
            report = new SkillStudyReport();
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "World null.");
            if (subject.IsNone)
                return Result.Failure(ErrorCode.InvalidArgument, "Subject required.");

            var artIdText = world.InventoryCatalog.GetTeachesArtId(itemId);
            if (string.IsNullOrEmpty(artIdText))
                return Result.Failure(ErrorCode.InvalidOperation, "Item is not an art tome.", itemId);
            if (!DefinitionId.TryParse(artIdText, out var artId))
                return Result.Failure(ErrorCode.InvalidDefinitionId, "teachesArtId invalid.", artIdText);
            if (!world.TryGetCombatArt(artId, out var art) || art == null)
                return Result.Failure(ErrorCode.NotFound, "Art missing.", artIdText);
            if (world.Inventory.GetCount(itemId) < 1)
                return Result.Failure(ErrorCode.InvalidOperation, "No tome in bag.", itemId);
            if (!world.Entities.TryGet(subject, out var entity))
                return Result.Failure(ErrorCode.EntityNotFound, "Subject missing.");
            if (!entity.TryGet<CombatArtsComponent>(out var arts))
            {
                arts = new CombatArtsComponent();
                entity.AddComponent(arts);
            }

            if (arts.Knows(artId))
            {
                report.Success = true;
                report.Title = "已会此技";
                report.Body = "「" + DisplayName(art.Name, art.Id) + "」早已学会。";
                report.TierAfter = arts.GetMastery(artId)?.Tier ?? SkillMasteryTier.Entry;
                return Result.Success();
            }

            var chance = EvaluateArtLearnChance(world, subject, art);
            report.ChanceUsed = chance;
            if (_rng.NextDouble() > chance)
            {
                report.Success = false;
                report.Title = "研读未悟";
                report.Body = "学习「" + DisplayName(art.Name, art.Id) +
                              "」失败。秘本仍在，可再试。";
                return Result.Success();
            }

            arts.TryLearn(artId);
            var profile = SkillMasteryLookup.EnsureOrDefaultArt(art);
            arts.SetMastery(artId, SkillMasteryState.CreateEntry(profile));
            QuestProgressRefresh.AfterWorldChange(world, subject);
            world.Events.Publish(
                EventType.SettlementStockChanged,
                world.Tick,
                target: subject,
                payload: "bag:" + itemId + ":0:learnArt");
            report.Success = true;
            report.TierAfter = SkillMasteryTier.Entry;
            report.Title = "斗技入门";
            var effectLine = art.IsActiveSkill
                ? "单段伤害 " + FormatPct(SkillMasteryLookup.ResolveDamageAttackMult(art, SkillMasteryTier.Entry))
                : "普攻加成 " + FormatPct(SkillMasteryLookup.ResolveAttackBonusPercent(art, SkillMasteryTier.Entry));
            report.Body = "已学会「" + DisplayName(art.Name, art.Id) +
                          "」·入门。\n" + effectLine;
            return Result.Success();
        }

        public Result AddManualMasteryProgress(SimulationWorld world, EntityId subject, int amount)
        {
            if (amount <= 0 || world == null)
                return Result.Success();
            if (!world.Entities.TryGet(subject, out var e) ||
                !e.TryGet<CultivationComponent>(out var cult) ||
                !cult.HasLearnedManual)
                return Result.Failure(ErrorCode.InvalidOperation, "No manual.");
            EnsureManualMastery(world, cult);
            AddProgress(cult.ManualMastery, amount);
            return Result.Success();
        }

        public Result AddArtMasteryProgress(SimulationWorld world, EntityId subject, DefinitionId artId, int amount)
        {
            if (amount <= 0 || world == null || string.IsNullOrEmpty(artId.Namespace))
                return Result.Success();
            if (!world.Entities.TryGet(subject, out var e) ||
                !e.TryGet<CombatArtsComponent>(out var arts) ||
                !arts.Knows(artId))
                return Result.Failure(ErrorCode.InvalidOperation, "Art unknown.");
            var m = arts.GetOrCreateMastery(artId);
            if (world.TryGetCombatArt(artId, out var art) && art != null)
                SkillMasteryLookup.SyncProgressCap(m, SkillMasteryLookup.EnsureOrDefaultArt(art));

            AddProgress(m, amount);
            return Result.Success();
        }

        /// <summary>扣修为进度，换功法熟练。</summary>
        public Result TryInfuseManual(SimulationWorld world, EntityId subject, int spendProgress, out string detail)
        {
            detail = string.Empty;
            if (spendProgress <= 0)
                return Result.Failure(ErrorCode.InvalidArgument, "Spend must be > 0.");
            if (!world.Entities.TryGet(subject, out var e) ||
                !e.TryGet<CultivationComponent>(out var cult) ||
                !cult.HasLearnedManual)
                return Result.Failure(ErrorCode.InvalidOperation, "No manual.");
            EnsureManualMastery(world, cult);
            if (cult.ManualMastery.IsAtBottleneck)
                return Result.Failure(ErrorCode.InvalidOperation, "Mastery at bottleneck.");
            if (cult.Progress < spendProgress)
                return Result.Failure(ErrorCode.InvalidOperation, "Not enough cultivation progress.");
            cult.Progress -= spendProgress;
            var gained = spendProgress * SkillMasteryRules.InfuseProgressPerPoint;
            AddProgress(cult.ManualMastery, gained);
            detail = "灌注 " + spendProgress + " 修为 → 功法熟练 +" + gained;
            return Result.Success();
        }

        public Result TryInfuseArt(
            SimulationWorld world,
            EntityId subject,
            DefinitionId artId,
            int spendProgress,
            out string detail)
        {
            detail = string.Empty;
            if (spendProgress <= 0)
                return Result.Failure(ErrorCode.InvalidArgument, "Spend must be > 0.");
            if (!world.Entities.TryGet(subject, out var e) ||
                !e.TryGet<CultivationComponent>(out var cult) ||
                !e.TryGet<CombatArtsComponent>(out var arts) ||
                !arts.Knows(artId))
                return Result.Failure(ErrorCode.InvalidOperation, "Cannot infuse art.");
            var m = arts.GetOrCreateMastery(artId);
            if (m.IsAtBottleneck)
                return Result.Failure(ErrorCode.InvalidOperation, "Mastery at bottleneck.");
            if (cult.Progress < spendProgress)
                return Result.Failure(ErrorCode.InvalidOperation, "Not enough cultivation progress.");
            cult.Progress -= spendProgress;
            var gained = spendProgress * SkillMasteryRules.InfuseProgressPerPoint;
            AddProgress(m, gained);
            detail = "灌注 " + spendProgress + " 修为 → 斗技熟练 +" + gained;
            return Result.Success();
        }

        public bool CanBreakthroughManual(SimulationWorld world, EntityId subject, out string reason)
        {
            reason = string.Empty;
            if (!world.Entities.TryGet(subject, out var e) ||
                !e.TryGet<CultivationComponent>(out var cult) ||
                !cult.HasLearnedManual)
            {
                reason = "未学功法";
                return false;
            }

            EnsureManualMastery(world, cult);
            var profile = ResolveManualProfile(world, cult);
            return CanBreakthroughState(world, cult.ManualMastery, profile, out reason);
        }

        /// <summary>熟练冲击下一档的成功率（悟性）；确认弹窗用，结果弹窗不重复显示。</summary>
        public double EvaluateMasteryBreakthroughChance(SimulationWorld world, EntityId subject)
        {
            if (world == null || !world.Entities.TryGet(subject, out var e))
                return 0;
            return MasteryBreakChance(e);
        }

        public bool CanBreakthroughArt(
            SimulationWorld world,
            EntityId subject,
            DefinitionId artId,
            out string reason)
        {
            reason = string.Empty;
            if (!world.Entities.TryGet(subject, out var e) ||
                !e.TryGet<CombatArtsComponent>(out var arts) ||
                !arts.Knows(artId))
            {
                reason = "未学该斗技";
                return false;
            }

            SkillMasteryProfile profile = null;
            if (world.TryGetCombatArt(artId, out var art) && art != null)
                profile = SkillMasteryLookup.EnsureOrDefaultArt(art);
            return CanBreakthroughState(world, arts.GetOrCreateMastery(artId), profile, out reason);
        }

        public Result TryBreakthroughManual(
            SimulationWorld world,
            EntityId subject,
            out SkillStudyReport report)
        {
            report = new SkillStudyReport();
            if (!CanBreakthroughManual(world, subject, out var reason))
                return Result.Failure(ErrorCode.InvalidOperation, reason);
            if (!world.Entities.TryGet(subject, out var e) ||
                !e.TryGet<CultivationComponent>(out var cult))
                return Result.Failure(ErrorCode.ComponentMissing, "Cultivation missing.");

            var profile = ResolveManualProfile(world, cult);
            var from = cult.ManualMastery.Tier;
            var costs = SkillMasteryLookup.BreakthroughCosts(profile, from);
            var chance = MasteryBreakChance(e);
            report.ChanceUsed = chance;
            if (!ConsumeCosts(world, costs, out var matFail))
                return Result.Failure(ErrorCode.InvalidOperation, matFail);

            if (_rng.NextDouble() > chance)
            {
                report.Success = false;
                report.Title = "熟练突破失败";
                report.Body = "冲击功法「" + SkillMasteryTierNames.Display(
                                  SkillMasteryLookup.NextTier(profile, from)) +
                              "」失败。材料已耗。";
                return Result.Success();
            }

            AdvanceTier(cult.ManualMastery, profile);
            _cultivation.ReapplyManualModifiers(world, subject);
            report.Success = true;
            report.TierAfter = cult.ManualMastery.Tier;
            report.Title = "功法" + SkillMasteryTierNames.Display(cult.ManualMastery.Tier);
            var speed = 0;
            if (cult.LearnedManualId.HasValue &&
                world.TryGetManual(cult.LearnedManualId.Value, out var manual) &&
                manual != null)
                speed = SkillMasteryLookup.ResolveCultivationSpeed(manual, cult.ManualMastery.Tier);
            report.Body = "功法突破至「" + SkillMasteryTierNames.Display(cult.ManualMastery.Tier) +
                          "」！打坐每 5 游戏分 +" + speed + " 修为。";
            return Result.Success();
        }

        public Result TryBreakthroughArt(
            SimulationWorld world,
            EntityId subject,
            DefinitionId artId,
            out SkillStudyReport report)
        {
            report = new SkillStudyReport();
            if (!CanBreakthroughArt(world, subject, artId, out var reason))
                return Result.Failure(ErrorCode.InvalidOperation, reason);
            if (!world.Entities.TryGet(subject, out var e) ||
                !e.TryGet<CombatArtsComponent>(out var arts))
                return Result.Failure(ErrorCode.ComponentMissing, "Arts missing.");

            world.TryGetCombatArt(artId, out var art);
            var profile = art != null ? SkillMasteryLookup.EnsureOrDefaultArt(art) : null;
            var m = arts.GetOrCreateMastery(artId);
            var from = m.Tier;
            var costs = SkillMasteryLookup.BreakthroughCosts(profile, from);
            var chance = MasteryBreakChance(e);
            report.ChanceUsed = chance;
            if (!ConsumeCosts(world, costs, out var matFail))
                return Result.Failure(ErrorCode.InvalidOperation, matFail);

            if (_rng.NextDouble() > chance)
            {
                report.Success = false;
                report.Title = "熟练突破失败";
                report.Body = "冲击斗技「" + SkillMasteryTierNames.Display(
                                  SkillMasteryLookup.NextTier(profile, from)) +
                              "」失败。材料已耗。";
                return Result.Success();
            }

            AdvanceTier(m, profile);
            report.Success = true;
            report.TierAfter = m.Tier;
            report.Title = "斗技" + SkillMasteryTierNames.Display(m.Tier);
            var name = artId.ToString();
            if (art != null && !string.IsNullOrEmpty(art.Name))
                name = art.Name;
            var effectLine = art != null && art.IsActiveSkill
                ? "单段伤害 " + FormatPct(SkillMasteryLookup.ResolveDamageAttackMult(art, m.Tier))
                : art != null
                    ? "普攻加成 " + FormatPct(SkillMasteryLookup.ResolveAttackBonusPercent(art, m.Tier))
                    : "";
            report.Body = "「" + name + "」突破至「" + SkillMasteryTierNames.Display(m.Tier) +
                          "」！" + effectLine;
            return Result.Success();
        }

        static SkillMasteryProfile ResolveManualProfile(SimulationWorld world, CultivationComponent cult)
        {
            if (cult == null || !cult.LearnedManualId.HasValue)
                return null;
            if (!world.TryGetManual(cult.LearnedManualId.Value, out var manual) || manual == null)
                return null;
            return SkillMasteryLookup.EnsureOrDefaultManual(manual);
        }

        static void EnsureManualMastery(SimulationWorld world, CultivationComponent cult)
        {
            var profile = ResolveManualProfile(world, cult);
            if (cult.ManualMastery == null)
                cult.ManualMastery = SkillMasteryState.CreateEntry(profile);
            SkillMasteryLookup.SyncProgressCap(cult.ManualMastery, profile);
        }

        static void AddProgress(SkillMasteryState m, int amount)
        {
            if (m == null || amount <= 0)
                return;
            if (m.ProgressRequired <= 0)
                return;
            m.Progress += amount;
            if (m.Progress > m.ProgressRequired)
                m.Progress = m.ProgressRequired;
        }

        static void AdvanceTier(SkillMasteryState m, SkillMasteryProfile profile)
        {
            m.Tier = SkillMasteryLookup.NextTier(profile, m.Tier);
            m.Progress = 0;
            SkillMasteryLookup.SyncProgressCap(m, profile);
        }

        static bool CanBreakthroughState(
            SimulationWorld world,
            SkillMasteryState m,
            SkillMasteryProfile profile,
            out string reason)
        {
            reason = string.Empty;
            if (m == null)
            {
                reason = "无熟练数据";
                return false;
            }

            if (!SkillMasteryLookup.CanBreakthrough(profile, m.Tier))
            {
                reason = "当前档不可突破";
                return false;
            }

            if (!m.IsAtBottleneck)
            {
                reason = "熟练未满";
                return false;
            }

            var costs = SkillMasteryLookup.BreakthroughCosts(profile, m.Tier);
            if (!HasCosts(world, costs, out reason))
                return false;

            return true;
        }

        static bool HasCosts(SimulationWorld world, IReadOnlyList<SkillMasteryCostSpec> costs, out string reason)
        {
            reason = string.Empty;
            if (costs == null || costs.Count == 0)
                return true;
            var parts = new System.Text.StringBuilder();
            for (var i = 0; i < costs.Count; i++)
            {
                var c = costs[i];
                if (c == null || string.IsNullOrEmpty(c.ItemId) || c.Count <= 0)
                    continue;
                if (world.Inventory.GetCount(c.ItemId) < c.Count)
                {
                    if (parts.Length > 0)
                        parts.Append("、");
                    parts.Append(ShortItem(c.ItemId)).Append("×").Append(c.Count);
                }
            }

            if (parts.Length == 0)
                return true;
            reason = "需" + parts;
            return false;
        }

        static bool ConsumeCosts(SimulationWorld world, IReadOnlyList<SkillMasteryCostSpec> costs, out string fail)
        {
            fail = string.Empty;
            if (!HasCosts(world, costs, out fail))
            {
                fail = "材料不足";
                return false;
            }

            if (costs == null)
                return true;
            for (var i = 0; i < costs.Count; i++)
            {
                var c = costs[i];
                if (c == null || string.IsNullOrEmpty(c.ItemId) || c.Count <= 0)
                    continue;
                world.Inventory.TryRemoveAll(c.ItemId, c.Count);
            }

            return true;
        }

        static string ShortItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return "?";
            if (itemId.IndexOf("spirit_herb", StringComparison.Ordinal) >= 0)
                return "灵药";
            if (itemId.IndexOf("rough_wood", StringComparison.Ordinal) >= 0)
                return "粗木";
            var colon = itemId.LastIndexOf(':');
            return colon >= 0 && colon + 1 < itemId.Length ? itemId.Substring(colon + 1) : itemId;
        }

        static double MasteryBreakChance(Entity e)
        {
            var c = 0;
            if (e.TryGet<AttributesComponent>(out var attrs))
                c = attrs.GetFinal(AttributeId.Comprehension);
            return SkillMasteryRules.MasteryBreakthroughChance(c);
        }

        static string DisplayName(string name, DefinitionId id) =>
            string.IsNullOrEmpty(name) ? id.ToString() : name;

        static string Pct(double chance) => ((int)Math.Round(chance * 100)).ToString();

        static string FormatPct(double mult) => ((int)Math.Round(mult * 100)).ToString() + "%";
    }
}
