using NUnit.Framework;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Simulation;

namespace XianXia.Tests
{
    public sealed class QuestDeadlineTests
    {
        [Test]
        public void QuestDeadline_ExpiresAfterConfiguredDays()
        {
            var world = new SimulationWorld();
            const string questId = "base:quest_deadline_test";
            var spec = new QuestSpec
            {
                Id = questId,
                Name = "时限测试",
                DeadlineDays = 1
            };
            spec.CompleteConditions.Add(new ContentCondition { Kind = "hasFlag", Id = "never" });
            spec.FailResults.Add(new ContentOutcome { Kind = "setFlag", Id = "quest:deadline_failed" });
            world.Quests.Register(spec);

            var quests = new QuestService();
            var subject = EntityId.None;
            Assert.IsTrue(quests.TryStart(world, questId, subject).IsSuccess);
            Assert.IsTrue(world.Quests.TryGet(questId, out var active));
            Assert.AreEqual(QuestStatus.Active, active.Status);
            Assert.AreEqual(0UL, active.AcceptedAtDayIndex);
            Assert.AreEqual(1UL, active.DeadlineDayIndexExclusive);

            Assert.IsTrue(quests.Evaluate(world, subject).IsSuccess);
            Assert.AreEqual(QuestStatus.Active, active.Status);

            world.Tick = new WorldTick((ulong)WorldTick.TicksPerDay);
            Assert.IsTrue(quests.Evaluate(world, subject).IsSuccess);
            Assert.AreEqual(QuestStatus.Failed, active.Status);
            Assert.IsTrue(world.Flags.Has("quest:deadline_failed"));
        }

        [Test]
        public void QuestDeadline_ThreeDayWindow_AllowsUntilExclusiveDay()
        {
            var world = new SimulationWorld();
            const string questId = "base:quest_deadline_three";
            var spec = new QuestSpec
            {
                Id = questId,
                DeadlineDays = 3
            };
            spec.CompleteConditions.Add(new ContentCondition { Kind = "hasFlag", Id = "never" });
            world.Quests.Register(spec);

            var quests = new QuestService();
            var subject = EntityId.None;
            Assert.IsTrue(quests.TryStart(world, questId, subject).IsSuccess);
            Assert.IsTrue(world.Quests.TryGet(questId, out var rt));

            world.Tick = new WorldTick((ulong)WorldTick.TicksPerDay * 2);
            quests.Evaluate(world, subject);
            Assert.AreEqual(QuestStatus.Active, rt.Status);
            Assert.AreEqual(1, QuestDeadline.RemainingDaysInclusive(world, rt));

            world.Tick = new WorldTick((ulong)WorldTick.TicksPerDay * 3);
            quests.Evaluate(world, subject);
            Assert.AreEqual(QuestStatus.Failed, rt.Status);
        }
    }
}
