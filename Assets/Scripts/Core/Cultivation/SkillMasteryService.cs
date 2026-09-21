using System;
using System.Collections.Generic;
using XianXia.Core.Attributes;
using XianXia.Core.Combat;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Events;
using XianXia.Core.Results;
using XianXia.Core.Inventory;
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

        public bool CanBeginManualStudy(
            SimulationWorld world,
            EntityId subject,
            string itemId,
            CultivationManualSpec manual,
            out string reason)
        {
            return TryValidateManualStudy(
                world, subject, itemId, manual, true,
                out _, out _, out _, out reason);
        }

        public bool CanBeginArtStudy(
            SimulationWorld world,
            EntityId subject,
            string itemId,
            out string reason)
        {
            return TryValidateArtStudy(
                world, subject, itemId, true,
                out _, out _, out _, out reason);
        }

        /// <summary>蓄势结束：掷骰学习功法。失败不授予。战斗释放时不调用。</summary>
        public Result TryFinishManualStudy(
            SimulationWorld world,
            EntityId subject,
            string itemId,
            CultivationManualSpec manual,
            out SkillStudyReport report)
        {
            report = new SkillStudyReport();
            if (!TryValidateManualStudy(
                    world, subject, itemId, manual, false,
                    out var entity, out var cult, out var registered, out var reason))
                return Result.Failure(ErrorCode.InvalidOperation, reason, itemId);
            if (cult.LearnedManualId.HasValue && cult.LearnedManualId.Value.Equals(registered.Id))
            {
                report.Success = true;
                report.Title = "已会此法";
                report.Body = "「" + DisplayName(registered.Name, registered.Id) + "」早已学会。";
                report.TierAfter = cult.ManualMastery?.Tier ?? SkillMasteryTier.Entry;
                return Result.Success();
            }

            var chance = EvaluateManualLearnChance(world, subject, registered);
            report.ChanceUsed = chance;
            if (world.Random.NextDouble() > chance)
            {
                report.Success = false;
                report.Title = "研读未悟";
                report.Body = "学习「" + DisplayName(registered.Name, registered.Id) +
                              "」失败。秘籍仍在，可再试。";
                return Result.Success();
            }

            var learned = _cultivation.LearnManual(world, subject, registered);
            if (learned.IsFailure)
                return learned;

            cult.ManualMastery = SkillMasteryState.CreateEntry(
                SkillMasteryLookup.EnsureOrDefaultManual(registered));
            _cultivation.ReapplyManualModifiers(world, subject);

            report.Success = true;
            report.TierAfter = SkillMasteryTier.Entry;
            report.Title = "功法入门";
            var speed = SkillMasteryLookup.ResolveCultivationSpeed(registered, SkillMasteryTier.Entry);
            report.Body = "已学会「" + DisplayName(registered.Name, registered.Id) +
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
            if (!TryValidateArtStudy(
                    world, subject, itemId, false,
                    out _, out var arts, out var art, out var reason))
                return Result.Failure(ErrorCode.InvalidOperation, reason, itemId);
            var artId = art.Id;

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
            if (world.Random.NextDouble() > chance)
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
                EventType.PartyInventoryChanged,
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
            var profile = ResolveManualProfile(world, cult);
            if (profile == null)
                return Result.Failure(ErrorCode.NotFound, "Manual definition missing.");
            if (cult.ManualMastery == null)
                cult.ManualMastery = SkillMasteryState.CreateEntry(profile);
            SkillMasteryLookup.SyncProgressCap(cult.ManualMastery, profile);
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
            if (!world.TryGetCombatArt(artId, out var art) || art == null)
                return Result.Failure(ErrorCode.NotFound, "Art definition missing.", artId.ToString());
            var m = arts.GetOrCreateMastery(artId);
            SkillMasteryLookup.SyncProgressCap(m, SkillMasteryLookup.EnsureOrDefaultArt(art));

            AddProgress(m, amount);
            return Result.Success();
        }

        /// <summary>扣修为进度，换功法熟练。</summary>
        public bool CanInfuseManual(
            SimulationWorld world,
            EntityId subject,
            int spendProgress,
            out string reason)
        {
            return TryBuildManualInfusionPlan(
                world, subject, spendProgress, out _, out _, out _, out reason);
        }

        public Result TryInfuseManual(SimulationWorld world, EntityId subject, int spendProgress, out string detail)
        {
            detail = string.Empty;
            if (!TryBuildManualInfusionPlan(
                    world, subject, spendProgress, out var cult, out var mastery,
                    out var plan, out detail))
                return Result.Failure(
                    spendProgress <= 0 ? ErrorCode.InvalidArgument : ErrorCode.InvalidOperation,
                    detail);

            cult.Progress -= spendProgress;
            mastery.ProgressRequired = plan.ProgressRequired;
            mastery.Progress += plan.ActualGain;
            detail = "灌注 " + spendProgress + " 修为 → 功法熟练 +" + plan.ActualGain;
            return Result.Success();
        }

        public bool CanInfuseArt(
            SimulationWorld world,
            EntityId subject,
            DefinitionId artId,
            int spendProgress,
            out string reason)
        {
            return TryBuildArtInfusionPlan(
                world, subject, artId, spendProgress, out _, out _, out _, out reason);
        }

        public Result TryInfuseArt(
            SimulationWorld world,
            EntityId subject,
            DefinitionId artId,
            int spendProgress,
            out string detail)
        {
            detail = string.Empty;
            if (!TryBuildArtInfusionPlan(
                    world, subject, artId, spendProgress, out var cult, out var mastery,
                    out var plan, out detail))
                return Result.Failure(
                    spendProgress <= 0 ? ErrorCode.InvalidArgument : ErrorCode.InvalidOperation,
                    detail);

            cult.Progress -= spendProgress;
            mastery.ProgressRequired = plan.ProgressRequired;
            mastery.Progress += plan.ActualGain;
            detail = "灌注 " + spendProgress + " 修为 → 斗技熟练 +" + plan.ActualGain;
            return Result.Success();
        }

        static bool TryBuildManualInfusionPlan(
            SimulationWorld world,
            EntityId subject,
            int spendProgress,
            out CultivationComponent cultivation,
            out SkillMasteryState mastery,
            out InfusionPlan plan,
            out string reason)
        {
            cultivation = null;
            mastery = null;
            plan = default;
            reason = string.Empty;
            if (!IsAliveStudySubject(world, subject, out var entity) ||
                !entity.TryGet<CultivationComponent>(out cultivation) ||
                !cultivation.HasLearnedManual)
            {
                reason = "未学功法";
                return false;
            }

            if (!cultivation.LearnedManualId.HasValue ||
                !world.TryGetManual(cultivation.LearnedManualId.Value, out var manual) ||
                manual == null)
            {
                reason = "功法定义不存在";
                return false;
            }

            mastery = cultivation.ManualMastery;
            return TryBuildInfusionPlan(
                cultivation, mastery, SkillMasteryLookup.EnsureOrDefaultManual(manual),
                spendProgress, out plan, out reason);
        }

        static bool TryBuildArtInfusionPlan(
            SimulationWorld world,
            EntityId subject,
            DefinitionId artId,
            int spendProgress,
            out CultivationComponent cultivation,
            out SkillMasteryState mastery,
            out InfusionPlan plan,
            out string reason)
        {
            cultivation = null;
            mastery = null;
            plan = default;
            reason = string.Empty;
            if (!IsAliveStudySubject(world, subject, out var entity) ||
                !entity.TryGet<CultivationComponent>(out cultivation) ||
                !entity.TryGet<CombatArtsComponent>(out var arts) ||
                !arts.Knows(artId))
            {
                reason = "未学该斗技";
                return false;
            }

            if (!world.TryGetCombatArt(artId, out var art) || art == null)
            {
                reason = "斗技定义不存在";
                return false;
            }

            mastery = arts.GetMastery(artId);
            return TryBuildInfusionPlan(
                cultivation, mastery, SkillMasteryLookup.EnsureOrDefaultArt(art),
                spendProgress, out plan, out reason);
        }

        static bool TryBuildInfusionPlan(
            CultivationComponent cultivation,
            SkillMasteryState mastery,
            SkillMasteryProfile profile,
            int spendProgress,
            out InfusionPlan plan,
            out string reason)
        {
            plan = default;
            reason = string.Empty;
            if (spendProgress <= 0)
            {
                reason = "灌注修为必须大于 0";
                return false;
            }

            var requestedGain = (long)spendProgress * SkillMasteryRules.InfuseProgressPerPoint;
            if (requestedGain <= 0 || requestedGain > int.MaxValue)
            {
                reason = "灌注数值无效";
                return false;
            }

            if (mastery == null)
            {
                reason = "熟练数据不存在";
                return false;
            }

            if (profile == null ||
                !profile.TryGetBreakthroughFrom(mastery.Tier, out var breakthrough) ||
                breakthrough == null ||
                breakthrough.ProgressRequired <= 0)
            {
                reason = mastery.Tier == SkillMasteryTier.Transcendent
                    ? "已到最高档"
                    : "当前档未配置后续突破，无法继续灌注";
                return false;
            }

            var required = breakthrough.ProgressRequired;
            if (mastery.Progress >= required)
            {
                reason = "熟练已满，请先突破";
                return false;
            }

            var actualGain = Math.Min((int)requestedGain, required - mastery.Progress);
            if (actualGain <= 0)
            {
                reason = "熟练已满，请先突破";
                return false;
            }

            if (cultivation == null || cultivation.Progress < spendProgress)
            {
                reason = "修为不足";
                return false;
            }

            plan = new InfusionPlan(required, actualGain);
            return true;
        }

        readonly struct InfusionPlan
        {
            public InfusionPlan(int progressRequired, int actualGain)
            {
                ProgressRequired = progressRequired;
                ActualGain = actualGain;
            }

            public int ProgressRequired { get; }
            public int ActualGain { get; }
        }

        public bool CanBreakthroughManual(SimulationWorld world, EntityId subject, out string reason)
        {
            reason = string.Empty;
            if (!IsAliveStudySubject(world, subject, out var e) ||
                !e.TryGet<CultivationComponent>(out var cult) ||
                !cult.HasLearnedManual)
            {
                reason = "未学功法";
                return false;
            }

            var profile = ResolveManualProfile(world, cult);
            if (profile == null)
            {
                reason = "功法定义不存在";
                return false;
            }
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
            if (!IsAliveStudySubject(world, subject, out var e) ||
                !e.TryGet<CombatArtsComponent>(out var arts) ||
                !arts.Knows(artId))
            {
                reason = "未学该斗技";
                return false;
            }

            if (!world.TryGetCombatArt(artId, out var art) || art == null)
            {
                reason = "斗技定义不存在";
                return false;
            }
            var profile = SkillMasteryLookup.EnsureOrDefaultArt(art);
            return CanBreakthroughState(world, arts.GetMastery(artId), profile, out reason);
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

            if (world.Random.NextDouble() > chance)
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

            if (world.Random.NextDouble() > chance)
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

        /// <summary>
        /// Snapshot 已恢复、静态 Content 已重新注册后的规范化边界。
        /// 保留 Tier/Progress，以当前 profile 修正缓存门槛并幂等重挂功法效果。
        /// </summary>
        public Result NormalizeLoadedMasteryState(SimulationWorld world)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "World null.");
            foreach (var entity in world.Entities.All)
            {
                if (entity.TryGet<CultivationComponent>(out var cultivation) &&
                    cultivation.HasLearnedManual && cultivation.LearnedManualId.HasValue)
                {
                    if (!world.TryGetManual(cultivation.LearnedManualId.Value, out var manual) || manual == null)
                        return Result.Failure(
                            ErrorCode.NotFound, "Restored manual definition missing.",
                            cultivation.LearnedManualId.Value.ToString());
                    var profile = SkillMasteryLookup.EnsureOrDefaultManual(manual);
                    if (cultivation.ManualMastery == null)
                        cultivation.ManualMastery = SkillMasteryState.CreateEntry(profile);
                    else
                        SkillMasteryLookup.SyncProgressCap(cultivation.ManualMastery, profile);
                    var reapplied = _cultivation.ReapplyManualModifiers(world, entity.Id);
                    if (reapplied.IsFailure)
                        return reapplied;
                }

                if (!entity.TryGet<CombatArtsComponent>(out var arts))
                    continue;
                for (var i = 0; i < arts.Learned.Count; i++)
                {
                    var artId = arts.Learned[i];
                    if (!world.TryGetCombatArt(artId, out var art) || art == null)
                        return Result.Failure(
                            ErrorCode.NotFound, "Restored art definition missing.", artId.ToString());
                    var mastery = arts.GetOrCreateMastery(artId);
                    SkillMasteryLookup.SyncProgressCap(
                        mastery, SkillMasteryLookup.EnsureOrDefaultArt(art));
                }
            }
            return Result.Success();
        }

        bool TryValidateManualStudy(
            SimulationWorld world,
            EntityId subject,
            string itemId,
            CultivationManualSpec requested,
            bool rejectAlreadyKnown,
            out Entity entity,
            out CultivationComponent cultivation,
            out CultivationManualSpec registered,
            out string reason)
        {
            entity = null;
            cultivation = null;
            registered = null;
            reason = string.Empty;
            if (requested == null || string.IsNullOrEmpty(itemId) ||
                !IsAliveStudySubject(world, subject, out entity) ||
                !entity.TryGet<CultivationComponent>(out cultivation) ||
                !entity.TryGet<AttributesComponent>(out _))
            {
                reason = "当前角色无法研读功法";
                return false;
            }
            if (world.Inventory.GetCount(itemId) < 1 ||
                !DefinitionId.TryParse(world.InventoryCatalog.GetTeachesManualId(itemId), out var taughtId) ||
                !taughtId.Equals(requested.Id) ||
                !world.TryGetManual(taughtId, out registered) || registered == null)
            {
                reason = "所需秘籍或功法定义不存在";
                return false;
            }
            var alreadyKnown = cultivation.LearnedManualId.HasValue &&
                               cultivation.LearnedManualId.Value.Equals(registered.Id);
            if (alreadyKnown)
            {
                if (rejectAlreadyKnown)
                {
                    reason = "已学会该功法";
                    return false;
                }
                return true;
            }
            var eligible = _cultivation.ValidateLearnManual(world, subject, registered);
            if (eligible.IsFailure)
            {
                reason = eligible.Error != null ? eligible.Error.Message : "当前角色无法研读功法";
                return false;
            }
            return true;
        }

        static bool TryValidateArtStudy(
            SimulationWorld world,
            EntityId subject,
            string itemId,
            bool rejectAlreadyKnown,
            out Entity entity,
            out CombatArtsComponent arts,
            out CombatArtSpec art,
            out string reason)
        {
            entity = null;
            arts = null;
            art = null;
            reason = string.Empty;
            if (string.IsNullOrEmpty(itemId) ||
                !IsAliveStudySubject(world, subject, out entity) ||
                !entity.TryGet<CombatArtsComponent>(out arts) ||
                !entity.TryGet<AttributesComponent>(out _))
            {
                reason = "当前角色无法研读斗技";
                return false;
            }
            var artIdText = world.InventoryCatalog.GetTeachesArtId(itemId);
            if (world.Inventory.GetCount(itemId) < 1 ||
                !DefinitionId.TryParse(artIdText, out var artId) ||
                !world.TryGetCombatArt(artId, out art) || art == null)
            {
                reason = "所需秘本或斗技定义不存在";
                return false;
            }
            if (rejectAlreadyKnown && arts.Knows(artId))
            {
                reason = "已学会该斗技";
                return false;
            }
            return true;
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
            var next = (long)m.Progress + amount;
            m.Progress = next >= m.ProgressRequired ? m.ProgressRequired : (int)next;
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
                reason = m.Tier == SkillMasteryTier.Transcendent
                    ? "已到最高档"
                    : "当前档未配置后续突破";
                return false;
            }

            var required = SkillMasteryLookup.ProgressRequiredToNext(profile, m.Tier);
            if (required <= 0 || m.Progress < required)
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
            var totals = AggregateCosts(costs, out var invalid);
            if (invalid || totals == null)
            {
                reason = "突破材料配置无效";
                return false;
            }
            if (totals.Count == 0)
                return true;
            var parts = new System.Text.StringBuilder();
            foreach (var pair in totals)
            {
                var have = world.InventoryCatalog.HasTag(pair.Key, "resource")
                    ? PlayerStrategicResourceService.GetAvailableCount(world, pair.Key)
                    : world.Inventory.GetCount(pair.Key);
                if (have < pair.Value)
                {
                    if (parts.Length > 0)
                        parts.Append("、");
                    parts.Append(ShortItem(pair.Key)).Append("×").Append(pair.Value);
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

            var totals = AggregateCosts(costs, out var invalid);
            if (invalid || totals == null)
            {
                fail = "突破材料配置无效";
                return false;
            }
            if (totals.Count == 0)
                return true;
            var spentResources = new List<StrategicResourceWithdrawalReceipt>();
            var spentBagItems = new List<SkillMasteryCostSpec>();
            foreach (var pair in totals)
            {
                if (world.InventoryCatalog.HasTag(pair.Key, "resource"))
                {
                    if (PlayerStrategicResourceService.TryConsume(
                            world, pair.Key, pair.Value, out var receipt).IsSuccess)
                    { spentResources.Add(receipt); continue; }
                }
                else if (world.Inventory.TryRemoveAll(pair.Key, pair.Value))
                {
                    spentBagItems.Add(new SkillMasteryCostSpec { ItemId = pair.Key, Count = pair.Value });
                    continue;
                }

                for (var r = spentResources.Count - 1; r >= 0; r--)
                    PlayerStrategicResourceService.Rollback(world, spentResources[r]);
                for (var b = spentBagItems.Count - 1; b >= 0; b--)
                    world.Inventory.TryAddAll(spentBagItems[b].ItemId, spentBagItems[b].Count);
                fail = "材料扣除失败，已回滚";
                return false;
            }

            return true;
        }

        static Dictionary<string, int> AggregateCosts(
            IReadOnlyList<SkillMasteryCostSpec> costs,
            out bool invalid)
        {
            invalid = false;
            var totals = new Dictionary<string, int>(StringComparer.Ordinal);
            if (costs == null)
                return totals;
            for (var i = 0; i < costs.Count; i++)
            {
                var cost = costs[i];
                if (cost == null || string.IsNullOrEmpty(cost.ItemId) || cost.Count <= 0)
                {
                    invalid = true;
                    return null;
                }
                totals.TryGetValue(cost.ItemId, out var prior);
                var total = (long)prior + cost.Count;
                if (total > int.MaxValue)
                {
                    invalid = true;
                    return null;
                }
                totals[cost.ItemId] = (int)total;
            }
            return totals;
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

        static bool IsAliveStudySubject(SimulationWorld world, EntityId subject, out Entity entity)
        {
            entity = null;
            return world != null && !subject.IsNone && world.Entities.TryGet(subject, out entity) &&
                   entity.TryGet<LifecycleComponent>(out var life) && life.State == LifecycleState.Alive;
        }
    }
}
