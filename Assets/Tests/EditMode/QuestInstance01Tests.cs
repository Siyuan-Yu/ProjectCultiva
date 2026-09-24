using System.Collections.Generic;
using NUnit.Framework;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Persistence;
using XianXia.Core.Simulation;
using XianXia.Data.Content;
using System;
using System.IO;
using System.Linq;

namespace XianXia.Tests
{
    public sealed class QuestInstance01Tests
    {
        const string QuestId = "test:merchant_herb";
        const string HerbId = "test:spirit_herb";

        [Test]
        public void SameTemplateTwoIssuersStayIndependentAndJournalCannotStartTemplate()
        {
            var fixture = Create();
            Assert.IsTrue(fixture.Service.TryAcceptFromTarget(fixture.World, QuestId, fixture.A).IsSuccess);
            Assert.IsTrue(fixture.Service.TryAcceptFromTarget(fixture.World, QuestId, fixture.B).IsSuccess);
            Assert.IsTrue(fixture.World.Quests.TryGetForIssuer(QuestId, fixture.A.TargetEntityId, out var qa));
            Assert.IsTrue(fixture.World.Quests.TryGetForIssuer(QuestId, fixture.B.TargetEntityId, out var qb));
            Assert.AreNotEqual(qa.QuestInstanceId, qb.QuestInstanceId);
            Assert.IsTrue(fixture.Service.TryStart(fixture.World, QuestId, fixture.Actor).IsFailure);
            var journal = new List<QuestListEntry>();
            QuestJournalQuery.Collect(fixture.World, fixture.Actor, journal);
            Assert.AreEqual(2, journal.Count);
            Assert.IsTrue(journal.Exists(x => x.QuestId == qa.QuestInstanceId && x.IssuerDisplayName == "行商甲"));
            Assert.IsTrue(journal.Exists(x => x.QuestId == qb.QuestInstanceId && x.IssuerDisplayName == "行商乙"));

            fixture.World.Inventory.TryAddAll(HerbId, 2);
            Assert.IsTrue(fixture.Service.TryDeliverToTarget(fixture.World, QuestId, fixture.A).IsSuccess);
            Assert.AreEqual(QuestStatus.ReadyToClaim, qa.Status);
            Assert.AreEqual(QuestStatus.Active, qb.Status);
            Assert.AreEqual(1, fixture.World.Inventory.GetCount(HerbId));
            Assert.IsTrue(fixture.Service.TryClaimRewards(fixture.World, qa.QuestInstanceId, fixture.Actor).IsSuccess);
            Assert.AreEqual(QuestStatus.Completed, qa.Status);
            Assert.AreEqual(1, fixture.World.ContentCounters.Get("test:commission_rewards"));
            Assert.IsTrue(fixture.Service.TryClaimRewards(fixture.World, qa.QuestInstanceId, fixture.Actor).IsFailure);
            Assert.IsTrue(fixture.Service.TryDeliverToTarget(fixture.World, QuestId,
                new ContentInteractionContext { ActorId = fixture.Actor, TargetEntityId = fixture.Other }).IsFailure);
            Assert.AreEqual(1, fixture.World.Inventory.GetCount(HerbId));
        }

        [Test]
        public void DeliveryAndRelationshipRollbackTogetherAndRetryIsIdempotent()
        {
            var f = Create();
            Assert.IsTrue(f.Service.TryAcceptFromTarget(f.World, QuestId, f.A).IsSuccess);
            f.World.Inventory.TryAddAll(HerbId, 1);
            var failed = ContentOutcomeApplier.ApplyAll(f.World, f.Actor, new[]
            {
                new ContentOutcome { Kind = "deliverQuestToTarget", Id = QuestId },
                Relation(),
                new ContentOutcome { Kind = "unknown" }
            }, f.A);
            Assert.IsTrue(failed.IsFailure);
            Assert.AreEqual(1, f.World.Inventory.GetCount(HerbId));
            Assert.AreEqual(0, f.World.Relationships.EventCount);
            Assert.IsTrue(f.World.Quests.TryGetForIssuer(QuestId, f.A.TargetEntityId, out var runtime));
            Assert.AreEqual(QuestStatus.Active, runtime.Status);
            Assert.IsFalse(runtime.DeliveryCompleted);

            Assert.IsTrue(ContentOutcomeApplier.ApplyAll(f.World, f.Actor, new[]
            { new ContentOutcome { Kind = "deliverQuestToTarget", Id = QuestId }, Relation() }, f.A).IsSuccess);
            Assert.AreEqual(QuestStatus.ReadyToClaim, runtime.Status);
            Assert.AreEqual(1, f.World.Relationships.Score(f.A.TargetEntityId, f.Actor));
            Assert.IsTrue(ContentConditionEvaluator.Pass(f.World, f.Actor,
                new ContentCondition { Kind = "affectionAtLeast", Id = "@target", CharacterId = "@actor", Amount = 1 }, f.A));
            Assert.IsFalse(ContentConditionEvaluator.Pass(f.World, f.Actor,
                new ContentCondition { Kind = "affectionAtLeast", Id = "@actor", CharacterId = "@target", Amount = 1 }, f.A));
            Assert.IsTrue(f.Service.TryDeliverToTarget(f.World, QuestId, f.A).IsFailure);
            Assert.AreEqual(1, f.World.Relationships.EventCount);
        }

        [Test]
        public void AbandonReacceptKeepsDeadlineAndIssuerLossOnlyFailsUndelivered()
        {
            var f = Create();
            Assert.IsTrue(f.Service.TryAcceptFromTarget(f.World, QuestId, f.A).IsSuccess);
            Assert.IsTrue(f.World.Quests.TryGetForIssuer(QuestId, f.A.TargetEntityId, out var a));
            var deadline = a.DeadlineDayIndexExclusive;
            Assert.IsTrue(f.Service.TryAbandon(f.World, a.QuestInstanceId, f.Actor).IsSuccess);
            f.World.Tick = new WorldTick((ulong)WorldTick.TicksPerDay);
            Assert.IsTrue(f.Service.TryAcceptFromTarget(f.World, QuestId, f.A).IsSuccess);
            Assert.AreEqual(deadline, a.DeadlineDayIndexExclusive);

            Assert.IsTrue(f.Service.TryAcceptFromTarget(f.World, QuestId, f.B).IsSuccess);
            f.World.Inventory.TryAddAll(HerbId, 1);
            Assert.IsTrue(f.Service.TryDeliverToTarget(f.World, QuestId, f.B).IsSuccess);
            Assert.IsTrue(f.World.Quests.TryGetForIssuer(QuestId, f.B.TargetEntityId, out var b));
            f.Service.FailIssuerCommissions(f.World, f.A.TargetEntityId, "", "issuer_removed");
            f.Service.FailIssuerCommissions(f.World, f.B.TargetEntityId, "", "issuer_removed");
            Assert.AreEqual(QuestStatus.Failed, a.Status);
            Assert.AreEqual("issuer_removed", a.FailureReason);
            Assert.AreEqual(QuestStatus.ReadyToClaim, b.Status);
        }

        [Test]
        public void SnapshotRoundTripPreservesInstancesIssuerDeliveryAndSequence()
        {
            var f = Create();
            f.Service.TryAcceptFromTarget(f.World, QuestId, f.A);
            f.Service.TryAcceptFromTarget(f.World, QuestId, f.B);
            f.World.Inventory.TryAddAll(HerbId, 1);
            f.Service.TryDeliverToTarget(f.World, QuestId, f.A);
            var dto = ContentProgressSnapshotHelper.Capture(f.World);

            var restored = Create();
            Assert.IsTrue(ContentProgressSnapshotHelper.Restore(restored.World, dto).IsSuccess);
            Assert.IsTrue(ContentProgressSnapshotHelper.ValidateDefinitions(restored.World).IsSuccess);
            Assert.AreEqual(dto.NextQuestInstanceSequence, restored.World.Quests.NextInstanceSequence);
            Assert.IsTrue(restored.World.Quests.TryGetForIssuer(QuestId, restored.A.TargetEntityId, out var a));
            Assert.IsTrue(a.DeliveryCompleted);
            Assert.AreEqual(QuestStatus.ReadyToClaim, a.Status);
            Assert.IsTrue(restored.World.Quests.TryGetForIssuer(QuestId, restored.B.TargetEntityId, out var b));
            Assert.AreEqual(QuestStatus.Active, b.Status);
        }

        [Test]
        public void BaseGameTwoTemplatesUseCommissionDeliveryWithoutPrototypeFlags()
        {
            var root = Environment.GetEnvironmentVariable("XIANXIA_BASEGAME") ??
                       Path.GetFullPath(Path.Combine("Content", "BaseGame"));
            var loaded = new ContentPackageLoader().Load(new[] { root });
            Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : string.Empty);
            foreach (var id in new[] { "quest_event02_wounded_cultivator_herb", "quest_event02_temporary_merchant_herb" })
            {
                Assert.IsTrue(loaded.Value.Registry.TryGetQuest(new DefinitionId("base", id), out var quest));
                Assert.AreEqual("characterCommission", quest.RuntimeMode);
                Assert.AreEqual("interaction", quest.AcceptanceMode);
                Assert.AreEqual(1, quest.DeliveryRequirements.Count);
                Assert.AreEqual("base:resource_spirit_herb", quest.DeliveryRequirements[0].ItemId);
                Assert.AreEqual(0, quest.CompleteConditions.Count);
                Assert.IsFalse(quest.Rewards.Any(x => x.Kind == "setFlag"));
            }
            var allOutcomes = loaded.Value.Registry.ContentEvents.Values.SelectMany(e => e.Steps)
                .SelectMany(s => s.Outcomes.Concat(s.Choices.SelectMany(c => c.Outcomes))).ToList();
            Assert.IsTrue(allOutcomes.Any(x => x.Kind == "acceptQuestFromTarget"));
            Assert.IsTrue(allOutcomes.Any(x => x.Kind == "deliverQuestToTarget"));
            Assert.IsFalse(allOutcomes.Any(x => x.Id != null &&
                (x.Id.Contains("_herb_accepted") || x.Id.Contains("_herb_handed_in") || x.Id.Contains("_herb_done"))));
        }

        static ContentOutcome Relation()
        {
            var result = new ContentOutcome { Kind = "relationDelta", FromDefinitionId = "@target", Amount = 1 };
            result.ToDefinitionIds.Add("@actor");
            return result;
        }

        static Fixture Create()
        {
            var world = new SimulationWorld();
            var actor = world.Entities.CreateCharacter(new DefinitionId("test", "actor"), "玩家").Value.Id;
            var a = world.Entities.CreateCharacter(new DefinitionId("test", "merchant"), "行商甲").Value.Id;
            var b = world.Entities.CreateCharacter(new DefinitionId("test", "merchant"), "行商乙").Value.Id;
            var other = world.Entities.CreateCharacter(new DefinitionId("test", "merchant"), "旁人").Value.Id;
            world.InventoryCatalog.Register(HerbId, "灵药", 99, new[] { "resource" });
            var spec = new QuestSpec
            {
                Id = QuestId, Name = "代采灵药", RuntimeMode = "characterCommission",
                AcceptanceMode = "interaction", Abandonable = true, DeadlineDays = 2
            };
            spec.DeliveryRequirements.Add(new QuestDeliveryRequirement { ItemId = HerbId, Amount = 1 });
            spec.Rewards.Add(new ContentOutcome { Kind = "addCounter", Id = "test:commission_rewards", Amount = 1 });
            world.Quests.Register(spec);
            return new Fixture(world, actor, a, b, other);
        }

        sealed class Fixture
        {
            public Fixture(SimulationWorld world, EntityId actor, EntityId a, EntityId b, EntityId other)
            {
                World = world; Actor = actor; Other = other; Service = new QuestService();
                A = ContentInteractionContext.ForNpc(actor, a, "test:merchant", "行商甲");
                B = ContentInteractionContext.ForNpc(actor, b, "test:merchant", "行商乙");
            }
            public SimulationWorld World { get; }
            public EntityId Actor { get; }
            public EntityId Other { get; }
            public QuestService Service { get; }
            public ContentInteractionContext A { get; }
            public ContentInteractionContext B { get; }
        }
    }
}
