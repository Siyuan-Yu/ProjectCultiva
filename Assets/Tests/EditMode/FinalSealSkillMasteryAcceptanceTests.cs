using System.IO;
using NUnit.Framework;
using XianXia.Core.Actions;
using XianXia.Core.Attributes;
using XianXia.Core.Combat;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Orders;
using XianXia.Core.Persistence;
using XianXia.Core.Random;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Data.Content;
using XianXia.Data.Cultivation;
using XianXia.Data.Serialization;

namespace XianXia.Tests
{
    public sealed class FinalSealSkillMasteryAcceptanceTests
    {
        const string NoPath = "当前档未配置后续突破，无法继续灌注";
        public static string HeadlessContentRoot;

        [Test]
        public void MinorTierWithoutConfiguredPathRejectsManualAndArtWithoutSpending()
        {
            var fixture = CreateFixture(Profile(100));
            fixture.Cult.Progress = 50;
            fixture.Cult.ManualMastery = State(SkillMasteryTier.Minor, 0, 0);
            fixture.Arts.SetMastery(fixture.ArtId, State(SkillMasteryTier.Minor, 0, 0));

            Assert.IsFalse(fixture.Service.CanInfuseManual(fixture.World, fixture.Subject.Id, 10, out var manualReason));
            Assert.AreEqual(NoPath, manualReason);
            Assert.IsTrue(fixture.Service.TryInfuseManual(fixture.World, fixture.Subject.Id, 10, out var manualDetail).IsFailure);
            Assert.AreEqual(NoPath, manualDetail);

            Assert.IsFalse(fixture.Service.CanInfuseArt(fixture.World, fixture.Subject.Id, fixture.ArtId, 10, out var artReason));
            Assert.AreEqual(NoPath, artReason);
            Assert.IsTrue(fixture.Service.TryInfuseArt(fixture.World, fixture.Subject.Id, fixture.ArtId, 10, out var artDetail).IsFailure);
            Assert.AreEqual(NoPath, artDetail);
            Assert.AreEqual(50, fixture.Cult.Progress);
            Assert.AreEqual(0, fixture.Cult.ManualMastery.Progress);
            Assert.AreEqual(0, fixture.Arts.GetMastery(fixture.ArtId).Progress);
        }

        [Test]
        public void EntryInfusionUsesEffectiveThresholdAndFullStateRejectsBeforeSpend()
        {
            var fixture = CreateFixture(Profile(100));
            fixture.Cult.Progress = 50;
            fixture.Cult.ManualMastery = State(SkillMasteryTier.Entry, 40, 0);

            Assert.IsTrue(fixture.Service.TryInfuseManual(
                fixture.World, fixture.Subject.Id, 10, out var detail).IsSuccess);
            Assert.AreEqual(40, fixture.Cult.Progress);
            Assert.AreEqual(60, fixture.Cult.ManualMastery.Progress);
            Assert.AreEqual(100, fixture.Cult.ManualMastery.ProgressRequired);
            StringAssert.Contains("功法熟练 +20", detail);

            fixture.Cult.ManualMastery.Progress = 100;
            fixture.Cult.Progress = 40;
            Assert.IsFalse(fixture.Service.CanInfuseManual(
                fixture.World, fixture.Subject.Id, 10, out var reason));
            Assert.AreEqual("熟练已满，请先突破", reason);
            Assert.IsTrue(fixture.Service.TryInfuseManual(
                fixture.World, fixture.Subject.Id, 10, out _).IsFailure);
            Assert.AreEqual(40, fixture.Cult.Progress);
            Assert.AreEqual(100, fixture.Cult.ManualMastery.Progress);
        }

        [Test]
        public void JiangLaoConfiguredEasyPathsReachTranscendentAndThenStop()
        {
            var profile = Profile(100);
            profile.Breakthroughs.Add(new SkillMasteryBreakthroughSpec {
                From = SkillMasteryTier.Minor,
                To = SkillMasteryTier.Major,
                ProgressRequired = 30,
                Costs = {
                    new SkillMasteryCostSpec { ItemId = "base:resource_spirit_herb", Count = 2 },
                    new SkillMasteryCostSpec { ItemId = "base:resource_rough_wood", Count = 2 }
                }
            });
            profile.Breakthroughs.Add(new SkillMasteryBreakthroughSpec {
                From = SkillMasteryTier.Major,
                To = SkillMasteryTier.Perfect,
                ProgressRequired = 40
            });
            profile.Breakthroughs.Add(new SkillMasteryBreakthroughSpec {
                From = SkillMasteryTier.Perfect,
                To = SkillMasteryTier.Transcendent,
                ProgressRequired = 50
            });
            var fixture = CreateFixture(profile);
            fixture.World.InventoryCatalog.Register("base:resource_spirit_herb", "herb", 99, null);
            fixture.World.InventoryCatalog.Register("base:resource_rough_wood", "wood", 99, null);
            Assert.IsTrue(fixture.World.Inventory.TryAddAll("base:resource_spirit_herb", 2));
            Assert.IsTrue(fixture.World.Inventory.TryAddAll("base:resource_rough_wood", 2));
            fixture.Cult.Progress = 100;
            fixture.Cult.ManualMastery = State(SkillMasteryTier.Minor, 0, 0);

            Assert.IsTrue(fixture.Service.TryInfuseManual(
                fixture.World, fixture.Subject.Id, 10, out _).IsSuccess);
            Assert.AreEqual(30, fixture.Cult.ManualMastery.ProgressRequired);
            Assert.AreEqual(20, fixture.Cult.ManualMastery.Progress);

            fixture.Cult.ManualMastery.Progress = 30;
            Assert.IsTrue(fixture.Service.CanBreakthroughManual(
                fixture.World, fixture.Subject.Id, out _));

            fixture.Cult.ManualMastery = State(SkillMasteryTier.Major, 0, 30);
            Assert.IsTrue(fixture.Service.CanInfuseManual(
                fixture.World, fixture.Subject.Id, 10, out _));
            fixture.Cult.ManualMastery = State(SkillMasteryTier.Transcendent, 0, 50);
            Assert.IsFalse(fixture.Service.CanInfuseManual(
                fixture.World, fixture.Subject.Id, 10, out var reason));
            Assert.AreEqual("已到最高档", reason);
        }

        [Test]
        public void NearCapSpendsRequestedCultivationButReportsOnlyActualGain()
        {
            var fixture = CreateFixture(Profile(100));
            fixture.Cult.Progress = 30;
            fixture.Cult.ManualMastery = State(SkillMasteryTier.Entry, 95, 100);

            Assert.IsTrue(fixture.Service.TryInfuseManual(
                fixture.World, fixture.Subject.Id, 10, out var detail).IsSuccess);
            Assert.AreEqual(20, fixture.Cult.Progress);
            Assert.AreEqual(100, fixture.Cult.ManualMastery.Progress);
            StringAssert.Contains("功法熟练 +5", detail);
        }

        [Test]
        public void InsufficientCultivationHasSpecificReasonAndNoStateChange()
        {
            var fixture = CreateFixture(Profile(100));
            fixture.Cult.Progress = 9;
            fixture.Cult.ManualMastery = State(SkillMasteryTier.Entry, 40, 100);

            Assert.IsFalse(fixture.Service.CanInfuseManual(
                fixture.World, fixture.Subject.Id, 10, out var reason));
            Assert.AreEqual("修为不足", reason);
            Assert.IsTrue(fixture.Service.TryInfuseManual(
                fixture.World, fixture.Subject.Id, 10, out var detail).IsFailure);
            Assert.AreEqual("修为不足", detail);
            Assert.AreEqual(9, fixture.Cult.Progress);
            Assert.AreEqual(40, fixture.Cult.ManualMastery.Progress);
        }

        [Test]
        public void DirectArtInfusionRecomputesStaleThresholdWithoutUiSync()
        {
            var fixture = CreateFixture(Profile(100));
            fixture.Cult.Progress = 50;
            fixture.Arts.SetMastery(fixture.ArtId, State(SkillMasteryTier.Entry, 40, 999));

            Assert.IsTrue(fixture.Service.TryInfuseArt(
                fixture.World, fixture.Subject.Id, fixture.ArtId, 10, out var detail).IsSuccess);
            var mastery = fixture.Arts.GetMastery(fixture.ArtId);
            Assert.AreEqual(100, mastery.ProgressRequired);
            Assert.AreEqual(60, mastery.Progress);
            Assert.AreEqual(40, fixture.Cult.Progress);
            StringAssert.Contains("斗技熟练 +20", detail);
        }

        [Test]
        public void InvalidInfusionDoesNotTouchRandomInventoryModifiersOrMasteryCache()
        {
            var rng = new CountingRandom();
            var fixture = CreateFixture(Profile(100), rng);
            fixture.Cult.Progress = 200;
            fixture.Cult.ManualMastery = State(SkillMasteryTier.Minor, 7, 777);
            fixture.World.InventoryCatalog.Register("test:item", "item", 99, null);
            Assert.IsTrue(fixture.World.Inventory.TryAddAll("test:item", 3));
            var attrs = fixture.Subject.Get<AttributesComponent>();
            attrs.AddModifier(AttributeId.Attack, ModifierOperation.Fixed, 1,
                new SourceRef(SourceKind.Event, new DefinitionId("test", "modifier"), fixture.Subject.Id));

            Assert.IsTrue(fixture.Service.TryInfuseManual(
                fixture.World, fixture.Subject.Id, 10, out _).IsFailure);
            Assert.AreEqual(0, rng.DoubleCalls);
            Assert.AreEqual(3, fixture.World.Inventory.GetCount("test:item"));
            Assert.AreEqual(1, attrs.Modifiers.Count);
            Assert.AreEqual(200, fixture.Cult.Progress);
            Assert.AreEqual(SkillMasteryTier.Minor, fixture.Cult.ManualMastery.Tier);
            Assert.AreEqual(7, fixture.Cult.ManualMastery.Progress);
            Assert.AreEqual(777, fixture.Cult.ManualMastery.ProgressRequired);
        }

        [Test]
        public void LegacyDefinitionWithoutMasteryStillGetsOnlyDefaultEntryPath()
        {
            var fixture = CreateFixture(null);
            fixture.Cult.Progress = 50;
            fixture.Cult.ManualMastery = State(SkillMasteryTier.Entry, 0, 0);
            Assert.IsTrue(fixture.Service.CanInfuseManual(
                fixture.World, fixture.Subject.Id, 10, out _));

            fixture.Cult.ManualMastery = State(SkillMasteryTier.Minor, 0, 0);
            Assert.IsFalse(fixture.Service.CanInfuseManual(
                fixture.World, fixture.Subject.Id, 10, out var reason));
            Assert.AreEqual(NoPath, reason);
        }

        [Test]
        public void RepeatingKnownManualIsIdempotentAndDoesNotRollRandomOrResetMastery()
        {
            var rng = new CountingRandom(1);
            var fixture = CreateFixture(Profile(100), rng);
            var manualId = fixture.Cult.LearnedManualId.Value;
            fixture.World.TryGetManual(manualId, out var manual);
            fixture.World.InventoryCatalog.Register(
                "manual-item", "manual", 1, null, manualId.ToString());
            Assert.IsTrue(fixture.World.Inventory.TryAddAll("manual-item", 1));
            fixture.Cult.ManualMastery = State(SkillMasteryTier.Minor, 37, 0);
            var attrs = fixture.Subject.Get<AttributesComponent>();
            var beforeModifiers = attrs.Modifiers.Count;

            Assert.IsTrue(fixture.Service.TryFinishManualStudy(
                fixture.World, fixture.Subject.Id, "manual-item", manual, out var report).IsSuccess);
            Assert.IsTrue(report.Success);
            Assert.AreEqual(0, rng.DoubleCalls);
            Assert.AreEqual(SkillMasteryTier.Minor, fixture.Cult.ManualMastery.Tier);
            Assert.AreEqual(37, fixture.Cult.ManualMastery.Progress);
            Assert.AreEqual(beforeModifiers, attrs.Modifiers.Count);
        }

        [Test]
        public void InvalidStudyPreconditionsFailBeforeRandom()
        {
            var rng = new CountingRandom(0);
            var fixture = CreateFixture(Profile(100), rng);
            var manualId = fixture.Cult.LearnedManualId.Value;
            fixture.World.TryGetManual(manualId, out var manual);
            fixture.Cult.LearnedManualId = null;
            fixture.Cult.ManualMastery = null;
            fixture.Cult.Realm = RealmStage.Mortal;
            fixture.World.InventoryCatalog.Register(
                "manual-item", "manual", 1, null, manualId.ToString());
            Assert.IsTrue(fixture.World.Inventory.TryAddAll("manual-item", 1));

            Assert.IsTrue(fixture.Service.TryFinishManualStudy(
                fixture.World, fixture.Subject.Id, "manual-item", manual, out _).IsFailure);
            Assert.AreEqual(0, rng.DoubleCalls);

            fixture.Subject.Get<LifecycleComponent>().State = LifecycleState.Incapacitated;
            Assert.IsTrue(fixture.Service.TryFinishManualStudy(
                fixture.World, fixture.Subject.Id, "manual-item", manual, out _).IsFailure);
            Assert.AreEqual(0, rng.DoubleCalls);
        }

        [Test]
        public void DuplicateBreakthroughCostsAreAggregatedForQueryAndConsumption()
        {
            var profile = Profile(100);
            profile.Breakthroughs[0].Costs.Add(
                new SkillMasteryCostSpec { ItemId = "test:same", Count = 6 });
            profile.Breakthroughs[0].Costs.Add(
                new SkillMasteryCostSpec { ItemId = "test:same", Count = 6 });
            var fixture = CreateFixture(profile);
            fixture.World.InventoryCatalog.Register("test:same", "same", 99, null);
            Assert.IsTrue(fixture.World.Inventory.TryAddAll("test:same", 10));
            fixture.Cult.ManualMastery = State(SkillMasteryTier.Entry, 100, 100);

            Assert.IsFalse(fixture.Service.CanBreakthroughManual(
                fixture.World, fixture.Subject.Id, out _));
            Assert.AreEqual(10, fixture.World.Inventory.GetCount("test:same"));
            Assert.IsTrue(fixture.World.Inventory.TryAddAll("test:same", 2));
            Assert.IsTrue(fixture.Service.CanBreakthroughManual(
                fixture.World, fixture.Subject.Id, out _));
        }

        [Test]
        public void LegalFailedBreakthroughConsumesOnceAndCannotDoubleSettleWithoutMaterials()
        {
            var rng = new CountingRandom(1);
            var profile = Profile(100);
            profile.Breakthroughs[0].Costs.Add(
                new SkillMasteryCostSpec { ItemId = "test:cost", Count = 1 });
            var fixture = CreateFixture(profile, rng);
            fixture.World.InventoryCatalog.Register("test:cost", "cost", 99, null);
            Assert.IsTrue(fixture.World.Inventory.TryAddAll("test:cost", 1));
            fixture.Cult.ManualMastery = State(SkillMasteryTier.Entry, 100, 100);

            Assert.IsTrue(fixture.Service.TryBreakthroughManual(
                fixture.World, fixture.Subject.Id, out var report).IsSuccess);
            Assert.IsFalse(report.Success);
            Assert.AreEqual(1, rng.DoubleCalls);
            Assert.AreEqual(0, fixture.World.Inventory.GetCount("test:cost"));
            Assert.IsTrue(fixture.Service.TryBreakthroughManual(
                fixture.World, fixture.Subject.Id, out _).IsFailure);
            Assert.AreEqual(1, rng.DoubleCalls);
            Assert.AreEqual(SkillMasteryTier.Entry, fixture.Cult.ManualMastery.Tier);
            Assert.AreEqual(100, fixture.Cult.ManualMastery.Progress);
        }

        [Test]
        public void SnapshotRoundTripNormalizesThresholdsAndReappliesManualEffectsOnce()
        {
            var profile = Profile(100);
            profile.Breakthroughs.Add(new SkillMasteryBreakthroughSpec {
                From = SkillMasteryTier.Minor,
                To = SkillMasteryTier.Major,
                ProgressRequired = 150
            });
            var fixture = CreateFixture(profile);
            var manualId = fixture.Cult.LearnedManualId.Value;
            fixture.World.TryGetManual(manualId, out var manual);
            manual.GrantedModifiers.Add(new ModifierGrantSpec {
                TargetAttribute = AttributeId.Attack,
                Operation = ModifierOperation.Fixed,
                Value = 3
            });
            fixture.Cult.ManualMastery = State(SkillMasteryTier.Minor, 40, 999);
            fixture.Arts.SetMastery(fixture.ArtId, State(SkillMasteryTier.Entry, 40, 999));
            Assert.IsTrue(new CultivationService().ReapplyManualModifiers(
                fixture.World, fixture.Subject.Id).IsSuccess);
            var randomBefore = fixture.World.Random.CaptureState();
            var snapshots = new SnapshotService(new JsonSnapshotSerializer());
            var saved = snapshots.CaptureJson(fixture.World, new SimulationLoop(fixture.World));
            Assert.IsTrue(saved.IsSuccess, saved.IsFailure ? saved.Error.ToString() : string.Empty);
            var restored = snapshots.RestoreJson(saved.Value);
            Assert.IsTrue(restored.IsSuccess, restored.IsFailure ? restored.Error.ToString() : string.Empty);
            var loaded = restored.Value.world;
            loaded.RegisterManual(manual);
            fixture.World.TryGetCombatArt(fixture.ArtId, out var art);
            loaded.RegisterCombatArt(art);

            var normalized = fixture.Service.NormalizeLoadedMasteryState(loaded);
            Assert.IsTrue(normalized.IsSuccess, normalized.IsFailure ? normalized.Error.ToString() : string.Empty);
            Assert.IsTrue(loaded.Entities.TryGet(fixture.Subject.Id, out var loadedSubject));
            var loadedCult = loadedSubject.Get<CultivationComponent>();
            var loadedArts = loadedSubject.Get<CombatArtsComponent>();
            Assert.AreEqual(SkillMasteryTier.Minor, loadedCult.ManualMastery.Tier);
            Assert.AreEqual(40, loadedCult.ManualMastery.Progress);
            Assert.AreEqual(150, loadedCult.ManualMastery.ProgressRequired);
            Assert.AreEqual(100, loadedArts.GetMastery(fixture.ArtId).ProgressRequired);
            Assert.AreEqual(1, loadedSubject.Get<AttributesComponent>().Modifiers.Count);
            Assert.AreEqual(
                SkillMasteryLookup.ResolveCultivationSpeed(manual, SkillMasteryTier.Minor),
                loadedCult.CultivationSpeed);
            var randomAfter = loaded.Random.CaptureState();
            Assert.AreEqual(randomBefore.S0, randomAfter.S0);
            Assert.AreEqual(randomBefore.S1, randomAfter.S1);
        }

        [Test]
        public void FullMasteryDoesNotStopNormalCultivationOrActiveArtUse()
        {
            var fixture = CreateFixture(Profile(100));
            fixture.Cult.Progress = 0;
            fixture.Cult.BreakthroughProgressRequired = 1000;
            fixture.Cult.CultivationSpeed = 5;
            fixture.Cult.ManualMastery = State(SkillMasteryTier.Entry, 100, 100);
            fixture.Arts.SetMastery(fixture.ArtId, State(SkillMasteryTier.Entry, 100, 100));
            var cultivate = new CultivateAction(
                new ActionId(1), fixture.Subject.Id, new OrderId(1), 1);
            Assert.IsTrue(cultivate.Start(fixture.World).IsSuccess);
            Assert.IsTrue(cultivate.Advance(fixture.World).IsSuccess);
            Assert.AreEqual(5, fixture.Cult.Progress);
            Assert.AreEqual(100, fixture.Cult.ManualMastery.Progress);

            var targetResult = fixture.World.Entities.CreateCharacter(
                new DefinitionId("test", "target"), "target");
            Assert.IsTrue(targetResult.IsSuccess);
            fixture.Subject.Get<AttributesComponent>().SetBase(AttributeId.Attack, 10);
            targetResult.Value.Get<AttributesComponent>().SetBase(AttributeId.MaxHp, 100);
            CombatDamageRules.EnsureVitals(targetResult.Value);
            Assert.IsTrue(new MeleeCombatService().CastEquippedArt(
                fixture.World, fixture.Subject.Id, targetResult.Value.Id, 0,
                out _, out var hits, out _).IsSuccess);
            Assert.Greater(hits, 0);
            Assert.AreEqual(100, fixture.Arts.GetMastery(fixture.ArtId).Progress);
        }

        [Test]
        public void ContentParserReportsInvalidRowsButAllowsIntentionalMissingLaterPath()
        {
            var valid = XianXia.Data.Serialization.SimpleJson.Parse(
                "{\"tiers\":[{\"tier\":\"entry\"},{\"tier\":\"minor\"}]," +
                "\"breakthroughs\":[{\"from\":\"entry\",\"to\":\"minor\"," +
                "\"progressRequired\":100,\"costs\":[]}]}");
            var validReport = new ValidationReport();
            Assert.IsTrue(SkillMasteryProfileParser.TryParse(
                valid, "test:valid", validReport, out _));
            Assert.IsTrue(validReport.IsValid);

            var invalid = XianXia.Data.Serialization.SimpleJson.Parse(
                "{\"breakthroughs\":[{\"from\":\"entry\",\"to\":\"minor\"," +
                "\"progressRequired\":100,\"costs\":[7,{\"itemId\":\"\",\"count\":0}]}]}");
            var invalidReport = new ValidationReport();
            Assert.IsTrue(SkillMasteryProfileParser.TryParse(
                invalid, "test:invalid", invalidReport, out _));
            Assert.IsFalse(invalidReport.IsValid);
        }

        [Test]
        public void CurrentBaseGameLoadsEasyBreakthroughCostsForEveryManualAndArt()
        {
            var root = string.IsNullOrEmpty(HeadlessContentRoot)
                ? Path.Combine("Content", "BaseGame")
                : HeadlessContentRoot;
            var loaded = new ContentPackageLoader().Load(new[] { root });
            Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : string.Empty);
            Assert.IsTrue(loaded.Value.Registry.TryGetCultivation(
                new DefinitionId("base", "cultivation_dongfu_secret"), out var dongfuDef));
            Assert.IsTrue(loaded.Value.Registry.TryGetCultivation(
                new DefinitionId("base", "cultivation_jiang_lao_legacy"), out var jiangDef));
            var dongfu = CultivationManualMapper.ToManualSpec(dongfuDef).Value;
            var jiang = CultivationManualMapper.ToManualSpec(jiangDef).Value;

            foreach (var id in new[] {
                         "base:cultivation_basic_breath",
                         "base:cultivation_qingyun_manual",
                         "base:cultivation_wood_whisper",
                         "base:cultivation_jiang_lao_legacy",
                         "base:cultivation_dongfu_secret" })
            {
                Assert.IsTrue(loaded.Value.Registry.TryGetCultivation(
                    DefinitionId.Parse(id).Value, out var definition));
                var manual = CultivationManualMapper.ToManualSpec(definition).Value;
                Assert.AreEqual(4, manual.Mastery.Breakthroughs.Count, id);
                Assert.IsTrue(manual.Mastery.TryGetBreakthroughFrom(
                    SkillMasteryTier.Entry, out var first), id);
                Assert.AreEqual(20, first.ProgressRequired, id);
                Assert.AreEqual(1, first.Costs[0].Count, id);
                Assert.AreEqual(1, first.Costs[1].Count, id);
                AssertEasyLaterPaths(manual.Mastery, id);
            }

            foreach (var pair in loaded.Value.Registry.CombatArts)
            {
                var art = XianXia.Data.Combat.CombatArtMapper.ToSpec(pair.Value).Value;
                Assert.AreEqual(4, art.Mastery.Breakthroughs.Count, pair.Key.ToString());
                Assert.IsTrue(art.Mastery.TryGetBreakthroughFrom(
                    SkillMasteryTier.Entry, out var first), pair.Key.ToString());
                Assert.AreEqual(20, first.ProgressRequired, pair.Key.ToString());
                Assert.AreEqual(1, first.Costs[0].Count, pair.Key.ToString());
                Assert.AreEqual(1, first.Costs[1].Count, pair.Key.ToString());
                AssertEasyLaterPaths(art.Mastery, pair.Key.ToString());
            }

            Assert.AreEqual(4, dongfu.Mastery.Breakthroughs.Count);
            Assert.AreEqual(SkillMasteryTier.Entry, dongfu.Mastery.Breakthroughs[0].From);
            Assert.AreEqual(SkillMasteryTier.Minor, dongfu.Mastery.Breakthroughs[0].To);
            Assert.IsTrue(dongfu.Mastery.TryGetBreakthroughFrom(
                SkillMasteryTier.Minor, out var dongfuSecond));
            Assert.AreEqual(30, dongfuSecond.ProgressRequired);

            Assert.AreEqual(4, jiang.Mastery.Breakthroughs.Count);
            Assert.IsTrue(jiang.Mastery.TryGetBreakthroughFrom(
                SkillMasteryTier.Minor, out var second));
            Assert.AreEqual(SkillMasteryTier.Major, second.To);
            Assert.AreEqual(30, second.ProgressRequired);
            Assert.AreEqual(2, second.Costs.Count);
            Assert.AreEqual("base:resource_spirit_herb", second.Costs[0].ItemId);
            Assert.AreEqual(2, second.Costs[0].Count);
            Assert.AreEqual("base:resource_rough_wood", second.Costs[1].ItemId);
            Assert.AreEqual(2, second.Costs[1].Count);
        }

        static void AssertEasyLaterPaths(SkillMasteryProfile profile, string context)
        {
            var from = new[] {
                SkillMasteryTier.Minor,
                SkillMasteryTier.Major,
                SkillMasteryTier.Perfect
            };
            var required = new[] { 30, 40, 50 };
            var costs = new[] { 2, 3, 4 };
            for (var i = 0; i < from.Length; i++)
            {
                Assert.IsTrue(profile.TryGetBreakthroughFrom(from[i], out var path), context);
                Assert.AreEqual(required[i], path.ProgressRequired, context);
                Assert.AreEqual(costs[i], path.Costs[0].Count, context);
                Assert.AreEqual(costs[i], path.Costs[1].Count, context);
            }
        }

        static Fixture CreateFixture(SkillMasteryProfile profile, IRandomSource random = null)
        {
            var world = random == null ? new SimulationWorld() : new SimulationWorld(random: random);
            var created = world.Entities.CreateCharacter(new DefinitionId("test", "student"), "student");
            Assert.IsTrue(created.IsSuccess);
            var subject = created.Value;
            var manualId = new DefinitionId("test", "manual");
            var artId = new DefinitionId("test", "art");
            world.RegisterManual(new CultivationManualSpec {
                Id = manualId,
                RequiredRealm = "炼气",
                CultivationSpeed = 2,
                Mastery = profile
            });
            world.RegisterCombatArt(new CombatArtSpec {
                Id = artId,
                DamageAttackMult = 1,
                Mastery = profile
            });

            var cultivation = subject.Get<CultivationComponent>();
            cultivation.Realm = RealmStage.QiRefining;
            cultivation.LearnedManualId = manualId;
            cultivation.ManualMastery = State(SkillMasteryTier.Entry, 0, 0);
            var arts = subject.Get<CombatArtsComponent>();
            Assert.IsTrue(arts.TryLearn(artId));
            return new Fixture(world, subject, cultivation, arts, artId);
        }

        static SkillMasteryProfile Profile(int required) => new SkillMasteryProfile {
            Breakthroughs = { new SkillMasteryBreakthroughSpec {
                From = SkillMasteryTier.Entry,
                To = SkillMasteryTier.Minor,
                ProgressRequired = required
            } }
        };

        static SkillMasteryState State(SkillMasteryTier tier, int progress, int required) =>
            new SkillMasteryState {
                Tier = tier,
                Progress = progress,
                ProgressRequired = required
            };

        sealed class Fixture
        {
            public Fixture(
                SimulationWorld world,
                Entity subject,
                CultivationComponent cultivation,
                CombatArtsComponent arts,
                DefinitionId artId)
            {
                World = world;
                Subject = subject;
                Cult = cultivation;
                Arts = arts;
                ArtId = artId;
            }

            public SimulationWorld World { get; }
            public Entity Subject { get; }
            public CultivationComponent Cult { get; }
            public CombatArtsComponent Arts { get; }
            public DefinitionId ArtId { get; }
            public SkillMasteryService Service { get; } = new SkillMasteryService();
        }

        sealed class CountingRandom : IRandomSource
        {
            readonly double _value;
            public CountingRandom(double value = 0) { _value = value; }
            public int DoubleCalls { get; private set; }
            public RandomStreamId StreamId => RandomStreamId.World;
            public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
            public double NextDouble() { DoubleCalls++; return _value; }
            public RandomState CaptureState() => new RandomState((ulong)(DoubleCalls + 1), 1, StreamId);
            public void RestoreState(RandomState state) { DoubleCalls = (int)state.S0 - 1; }
        }
    }
}
