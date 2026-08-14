using NUnit.Framework;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Simulation;
using XianXia.Core.Social;

namespace XianXia.Tests
{
    public sealed class ContentOutcomeRelationDeltaTests
    {
        [Test]
        public void RelationDelta_ToDefinitionIds_AppliesToEachTarget()
        {
            var world = new SimulationWorld();
            var npc = world.Entities.CreateNpc(new DefinitionId("base", "supervisor"), "主管").Value;
            var hero = world.Entities.CreateCharacter(new DefinitionId("base", "hero"), "主角").Value;
            var mate = world.Entities.CreateCharacter(new DefinitionId("base", "mate"), "同伴").Value;

            var outcome = new ContentOutcome
            {
                Kind = "relationDelta",
                FromDefinitionId = npc.DefinitionId.ToString(),
                Amount = -5
            };
            outcome.ToDefinitionIds.Add(hero.DefinitionId.ToString());
            outcome.ToDefinitionIds.Add(mate.DefinitionId.ToString());

            Assert.IsTrue(ContentOutcomeApplier.Apply(world, EntityId.None, outcome).IsSuccess);
            Assert.AreEqual(-5, world.Relationships.Score(npc.Id, hero.Id));
            Assert.AreEqual(-5, world.Relationships.Score(npc.Id, mate.Id));
        }

        [Test]
        public void RelationDelta_PartyToken_AppliesToAllControllableCharacters()
        {
            var world = new SimulationWorld();
            var npc = world.Entities.CreateNpc(new DefinitionId("base", "supervisor"), "主管").Value;
            var hero = world.Entities.CreateCharacter(new DefinitionId("base", "hero"), "主角").Value;
            var mate = world.Entities.CreateCharacter(new DefinitionId("base", "mate"), "同伴").Value;

            var outcome = new ContentOutcome
            {
                Kind = "relationDelta",
                FromDefinitionId = npc.DefinitionId.ToString(),
                Amount = -3
            };
            outcome.ToDefinitionIds.Add("@party");

            Assert.IsTrue(ContentOutcomeApplier.Apply(world, EntityId.None, outcome).IsSuccess);
            Assert.AreEqual(-3, world.Relationships.Score(npc.Id, hero.Id));
            Assert.AreEqual(-3, world.Relationships.Score(npc.Id, mate.Id));
        }

        [Test]
        public void QuestFailResults_MultiTargetRelationDelta_AppliesOnDeadline()
        {
            var world = new SimulationWorld();
            var npc = world.Entities.CreateNpc(new DefinitionId("base", "character_ch01_ref_supervisor"), "主管").Value;
            var hero = world.Entities.CreateCharacter(new DefinitionId("base", "character_protagonist"), "主角").Value;
            var mate = world.Entities.CreateCharacter(new DefinitionId("base", "character_companion_a"), "甲").Value;

            const string questId = "base:quest_multi_relation_fail";
            var spec = new QuestSpec
            {
                Id = questId,
                DeadlineDays = 1
            };
            spec.CompleteConditions.Add(new ContentCondition { Kind = "hasFlag", Id = "never" });
            var fail = new ContentOutcome
            {
                Kind = "relationDelta",
                FromDefinitionId = npc.DefinitionId.ToString(),
                Amount = -5
            };
            fail.ToDefinitionIds.Add("@party");
            spec.FailResults.Add(fail);
            world.Quests.Register(spec);

            var quests = new QuestService();
            Assert.IsTrue(quests.TryStart(world, questId, hero.Id).IsSuccess);
            world.Tick = new WorldTick((ulong)WorldTick.TicksPerDay);
            Assert.IsTrue(quests.Evaluate(world, hero.Id).IsSuccess);

            Assert.AreEqual(-5, world.Relationships.Score(npc.Id, hero.Id));
            Assert.AreEqual(-5, world.Relationships.Score(npc.Id, mate.Id));
        }
    }
}
