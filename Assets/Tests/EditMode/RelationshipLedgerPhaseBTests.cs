using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Events;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using CoreEventType = XianXia.Core.Events.EventType;

namespace XianXia.Tests
{
    public sealed class RelationshipLedgerPhaseBTests
    {
        [Test]
        public void Record_AggregatesDirectedScore_AndRefreshesCache()
        {
            var world = new SimulationWorld();
            var a = world.Entities.CreateCharacter(new DefinitionId("base", "a"), "甲").Value;
            var b = world.Entities.CreateCharacter(new DefinitionId("base", "b"), "乙").Value;
            var service = new RelationshipService();

            Assert.IsTrue(service.Record(world, a.Id, b.Id, 30, "rescue").IsSuccess);
            Assert.IsTrue(service.Record(world, b.Id, a.Id, -50, "betray").IsSuccess);

            Assert.AreEqual(30, world.Relationships.Score(a.Id, b.Id));
            Assert.AreEqual(-50, world.Relationships.Score(b.Id, a.Id));
            Assert.AreEqual(30, a.Get<RelationshipComponent>().GetCachedToward(b.Id));
            Assert.AreEqual(-50, b.Get<RelationshipComponent>().GetCachedToward(a.Id));
            Assert.AreEqual(2, world.Relationships.EventCount);
        }

        [Test]
        public void Record_SameDirection_Sums()
        {
            var world = new SimulationWorld();
            var a = world.Entities.CreateCharacter(new DefinitionId("base", "a"), "甲").Value;
            var b = world.Entities.CreateCharacter(new DefinitionId("base", "b"), "乙").Value;
            var service = new RelationshipService();

            Assert.IsTrue(service.Record(world, a.Id, b.Id, 30, "rescue").IsSuccess);
            Assert.IsTrue(service.Record(world, a.Id, b.Id, -50, "betray").IsSuccess);
            Assert.AreEqual(-20, world.Relationships.Score(a.Id, b.Id));
            Assert.AreEqual(-20, a.Get<RelationshipComponent>().GetCachedToward(b.Id));
        }

        [Test]
        public void Record_PublishesRelationshipChanged()
        {
            var world = new SimulationWorld();
            var a = world.Entities.CreateCharacter(new DefinitionId("base", "a"), "甲").Value;
            var b = world.Entities.CreateCharacter(new DefinitionId("base", "b"), "乙").Value;

            Assert.IsTrue(new RelationshipService().Record(world, a.Id, b.Id, 10, "help").IsSuccess);
            var drained = world.Events.Drain();
            Assert.IsTrue(drained.Exists(e => e.Type == CoreEventType.RelationshipChanged));
        }

        [Test]
        public void Record_RejectsSelf_AndMissingEntity()
        {
            var world = new SimulationWorld();
            var a = world.Entities.CreateCharacter(new DefinitionId("base", "a"), "甲").Value;
            var service = new RelationshipService();

            Assert.IsTrue(service.Record(world, a.Id, a.Id, 1, "x").IsFailure);
            Assert.IsTrue(service.Record(world, a.Id, new EntityId(99), 1, "x").IsFailure);
            Assert.IsTrue(service.Record(world, a.Id, new EntityId(1), 1, "").IsFailure);
            Assert.AreEqual(0, world.Relationships.EventCount);
        }

        [Test]
        public void Cache_IsNotSourceOfTruth_LedgerWins()
        {
            var world = new SimulationWorld();
            var a = world.Entities.CreateCharacter(new DefinitionId("base", "a"), "甲").Value;
            var b = world.Entities.CreateCharacter(new DefinitionId("base", "b"), "乙").Value;
            var service = new RelationshipService();
            Assert.IsTrue(service.Record(world, a.Id, b.Id, 15, "seed").IsSuccess);

            // Illicit cache poke must not change Ledger score.
            a.Get<RelationshipComponent>().ReplaceCachedToward(b.Id, 999);
            Assert.AreEqual(15, world.Relationships.Score(a.Id, b.Id));
            Assert.AreEqual(999, a.Get<RelationshipComponent>().GetCachedToward(b.Id));

            RelationshipService.RefreshPairCaches(world, a.Id, b.Id);
            Assert.AreEqual(15, a.Get<RelationshipComponent>().GetCachedToward(b.Id));
        }
    }
}
