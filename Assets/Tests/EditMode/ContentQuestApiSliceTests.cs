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

    }
}
