using NUnit.Framework;
using XianXia.Core.Attributes;
using XianXia.Core.Content;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;

namespace XianXia.Tests
{
    public sealed class ContentQuestApiSliceTests
    {
        [Test]
        public void Counter_And_DailyFlag_Drive_Chess_Style_Progress()
        {
            var world = new SimulationWorld();
            var subject = world.Entities.CreateCharacter(
                new DefinitionId("base", "character_protagonist"), "主角").Value;

            Assert.IsTrue(ContentConditionEvaluator.Pass(
                world, subject.Id,
                new ContentCondition { Kind = "missingDailyFlag", Id = "chess_today" }));

            Assert.IsTrue(ContentOutcomeApplier.Apply(
                world, subject.Id,
                new ContentOutcome { Kind = "addCounter", Id = "chess_wins", Amount = 1 }).IsSuccess);
            Assert.IsTrue(ContentOutcomeApplier.Apply(
                world, subject.Id,
                new ContentOutcome { Kind = "setDailyFlag", Id = "chess_today" }).IsSuccess);

            Assert.AreEqual(1, world.ContentCounters.Get("chess_wins"));
            Assert.IsFalse(ContentConditionEvaluator.Pass(
                world, subject.Id,
                new ContentCondition { Kind = "missingDailyFlag", Id = "chess_today" }));
            Assert.IsTrue(ContentConditionEvaluator.Pass(
                world, subject.Id,
                new ContentCondition { Kind = "hasDailyFlag", Id = "chess_today" }));

            world.Tick = new WorldTick((ulong)WorldTick.TicksPerDay);
            Assert.IsTrue(ContentConditionEvaluator.Pass(
                world, subject.Id,
                new ContentCondition { Kind = "missingDailyFlag", Id = "chess_today" }));

            ContentOutcomeApplier.Apply(world, subject.Id, new ContentOutcome { Kind = "addCounter", Id = "chess_wins", Amount = 1 });
            ContentOutcomeApplier.Apply(world, subject.Id, new ContentOutcome { Kind = "addCounter", Id = "chess_wins", Amount = 1 });
            Assert.IsTrue(ContentConditionEvaluator.Pass(
                world, subject.Id,
                new ContentCondition { Kind = "counterAtLeast", Id = "chess_wins", Amount = 3 }));
        }

        [Test]
        public void StartMinigame_Outcome_Is_Accepted_NoOp()
        {
            var world = new SimulationWorld();
            var subject = world.Entities.CreateCharacter(
                new DefinitionId("base", "character_protagonist"), "主角").Value;
            Assert.IsTrue(ContentOutcomeApplier.Apply(
                world, subject.Id,
                new ContentOutcome { Kind = "startMinigame", Id = "ticTacToe" }).IsSuccess);
        }

        [Test]
        public void EncounterCleared_And_LearnManual_Outcomes()
        {
            var world = new SimulationWorld();
            var manualId = new DefinitionId("base", "cultivation_qingyun_manual");
            world.RegisterManual(new CultivationManualSpec
            {
                Id = manualId,
                RequiredRealm = "Mortal",
                CultivationSpeed = 10,
                BreakthroughProgress = 100
            });

            var subject = world.Entities.CreateCharacter(
                new DefinitionId("base", "character_protagonist"), "主角").Value;
            subject.Get<AttributesComponent>().SetBase(AttributeId.MaxHp, 100);

            Assert.IsFalse(ContentConditionEvaluator.Pass(
                world, subject.Id,
                new ContentCondition { Kind = "encounterCleared", Id = "loc_ref_cave" }));

            Assert.IsTrue(ContentOutcomeApplier.Apply(
                world, subject.Id,
                new ContentOutcome { Kind = "setEncounterCleared", Id = "loc_ref_cave" }).IsSuccess);
            Assert.IsTrue(ContentConditionEvaluator.Pass(
                world, subject.Id,
                new ContentCondition { Kind = "encounterCleared", Id = "loc_ref_cave" }));

            Assert.IsTrue(ContentOutcomeApplier.Apply(
                world, subject.Id,
                new ContentOutcome { Kind = "learnManual", Id = manualId.ToString() }).IsSuccess);
            Assert.IsTrue(subject.Get<CultivationComponent>().HasLearnedManual);
            Assert.IsTrue(ContentConditionEvaluator.Pass(
                world, subject.Id,
                new ContentCondition { Kind = "hasManual", Id = manualId.ToString() }));
        }

        [Test]
        public void ManualTome_Not_Consumed_When_Learned()
        {
            var world = new SimulationWorld();
            var manualId = new DefinitionId("base", "cultivation_jiang_lao_legacy");
            world.RegisterManual(new CultivationManualSpec
            {
                Id = manualId,
                Name = "将老残谱",
                Grade = "黄阶中级",
                RequiredRealm = "炼气",
                CultivationSpeed = 8,
                BreakthroughProgress = 100
            });

            const string itemId = "base:item_manual_jiang_lao_legacy";
            world.InventoryCatalog.Register(
                itemId, "将老残谱（秘籍）", 1,
                new[] { "manual_tome" },
                manualId.ToString());

            var subject = world.Entities.CreateCharacter(
                new DefinitionId("base", "character_protagonist"), "主角").Value;
            subject.Get<AttributesComponent>().SetBase(AttributeId.MaxHp, 100);
            var cult = subject.Get<CultivationComponent>();
            cult.Realm = RealmStage.QiRefining;
            cult.MinorStage = 1;

            Assert.AreEqual(1, world.Inventory.TryAdd(itemId, 1));
            Assert.IsTrue(new ManualItemLearnService().TryLearnFromItem(world, subject.Id, itemId).IsSuccess);
            Assert.AreEqual(1, world.Inventory.GetCount(itemId));
            Assert.IsTrue(cult.HasLearnedManual);
            Assert.AreEqual(8, cult.CultivationSpeed);

            var mate = world.Entities.CreateCharacter(
                new DefinitionId("base", "character_mate"), "同伴").Value;
            mate.Get<AttributesComponent>().SetBase(AttributeId.MaxHp, 100);
            mate.Get<CultivationComponent>().Realm = RealmStage.QiRefining;
            Assert.IsTrue(new ManualItemLearnService().TryLearnFromItem(world, mate.Id, itemId).IsSuccess);
            Assert.AreEqual(1, world.Inventory.GetCount(itemId));
            Assert.IsTrue(mate.Get<CultivationComponent>().HasLearnedManual);
        }

        [Test]
        public void ManualTome_Rejected_Before_QiRefining()
        {
            var world = new SimulationWorld();
            var manualId = new DefinitionId("base", "cultivation_jiang_lao_legacy");
            world.RegisterManual(new CultivationManualSpec
            {
                Id = manualId,
                RequiredRealm = "炼气",
                CultivationSpeed = 8,
                BreakthroughProgress = 100
            });
            const string itemId = "base:item_manual_jiang_lao_legacy";
            world.InventoryCatalog.Register(itemId, "秘籍", 1, new[] { "consumable" }, manualId.ToString());
            var subject = world.Entities.CreateCharacter(
                new DefinitionId("base", "character_protagonist"), "主角").Value;
            subject.Get<AttributesComponent>().SetBase(AttributeId.MaxHp, 100);
            world.Inventory.TryAdd(itemId, 1);

            Assert.IsTrue(new ManualItemLearnService().TryLearnFromItem(world, subject.Id, itemId).IsFailure);
            Assert.AreEqual(1, world.Inventory.GetCount(itemId));
        }
    }
}
