using NUnit.Framework;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;

namespace XianXia.Tests
{
    public sealed class ContentEventSupervisorTalkTests
    {
        const string SupervisorDef = "base:character_ch01_ref_supervisor";
        const string PenaltyQuestId = "base:quest_ch01_ref_supervisor_herb_penalty";

        [Test]
        public void SilentChoice_StartsHerbPenaltyQuest_AndSetsAssignedFlag()
        {
            var world = BuildWorldWithSupervisorTalk();
            var subject = EntityId.None;

            Assert.IsTrue(new ContentEventService().TryTalkToNpc(world, subject, SupervisorDef).IsSuccess);
            Assert.AreEqual("base:event_ch01_ref_supervisor_talk", world.ContentEvents.ActiveEventId);

            var resolved = new ContentEventService().ResolveChoice(world, subject, "silent");
            Assert.IsTrue(resolved.IsSuccess, resolved.Error.ToString());
            Assert.IsFalse(world.ContentEvents.HasActive);
            Assert.IsTrue(world.Flags.Has("quest:ch01_ref_supervisor_penalty_assigned"));
            Assert.IsTrue(world.Quests.TryGet(PenaltyQuestId, out var runtime));
            Assert.AreEqual(QuestStatus.Active, runtime.Status);
            Assert.AreEqual(2UL, runtime.DeadlineDayIndexExclusive);
        }

        [Test]
        public void AfterPenalty_TalkUsesHurryEvent()
        {
            var world = BuildWorldWithSupervisorTalk();
            StoryFlagService.Set(world, "quest:ch01_ref_supervisor_penalty_assigned", EntityId.None);
            Assert.IsTrue(
                new ContentEventService()
                    .TryTalkToNpc(world, EntityId.None, SupervisorDef)
                    .IsSuccess);
            Assert.AreEqual("base:event_ch01_ref_supervisor_talk_hurry", world.ContentEvents.ActiveEventId);
        }

        static SimulationWorld BuildWorldWithSupervisorTalk()
        {
            var world = new SimulationWorld();

            var talk = new ContentEventSpec
            {
                Id = "base:event_ch01_ref_supervisor_talk",
                Trigger = "onTalk",
                NpcDefinitionId = SupervisorDef,
                Once = false
            };
            talk.Conditions.Add(new ContentCondition
            {
                Kind = "missingFlag",
                Id = "quest:ch01_ref_supervisor_penalty_assigned"
            });
            var ack = new ContentEventChoiceSpec { Id = "ack", Text = "遵命。" };
            ack.Outcomes.Add(new ContentOutcome { Kind = "setFlag", Id = "event:ch01_ref_supervisor_ack" });
            talk.Choices.Add(ack);

            var silent = new ContentEventChoiceSpec { Id = "silent", Text = "……（不语）" };
            silent.Outcomes.Add(new ContentOutcome { Kind = "setFlag", Id = "event:ch01_ref_supervisor_silent" });
            silent.Outcomes.Add(new ContentOutcome
            {
                Kind = "setFlag",
                Id = "quest:ch01_ref_supervisor_penalty_assigned"
            });
            silent.Outcomes.Add(new ContentOutcome
            {
                Kind = "startQuest",
                Id = PenaltyQuestId
            });
            talk.Choices.Add(silent);
            world.ContentEvents.Register(talk);

            var hurry = new ContentEventSpec
            {
                Id = "base:event_ch01_ref_supervisor_talk_hurry",
                Trigger = "onTalk",
                NpcDefinitionId = SupervisorDef,
                Once = false
            };
            hurry.Conditions.Add(new ContentCondition
            {
                Kind = "hasFlag",
                Id = "quest:ch01_ref_supervisor_penalty_assigned"
            });
            hurry.Conditions.Add(new ContentCondition
            {
                Kind = "missingFlag",
                Id = "quest:ch01_ref_supervisor_penalty_done"
            });
            hurry.Choices.Add(new ContentEventChoiceSpec { Id = "go", Text = "……这就去。" });
            world.ContentEvents.Register(hurry);

            var quest = new QuestSpec
            {
                Id = PenaltyQuestId,
                Name = "惩戒·百份灵药",
                DeadlineDays = 2
            };
            quest.CompleteConditions.Add(new ContentCondition
            {
                Kind = "stockAtLeast",
                Id = "base:resource_spirit_herb",
                Amount = 100
            });
            world.Quests.Register(quest);

            return world;
        }
    }
}
