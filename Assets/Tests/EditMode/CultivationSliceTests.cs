using System.IO;
using System.Linq;
using NUnit.Framework;
using XianXia.Core.Attributes;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Events;
using XianXia.Core.Persistence;
using XianXia.Core.Simulation;
using XianXia.Data.Content;
using XianXia.Data.Cultivation;
using XianXia.Data.Serialization;

namespace XianXia.Tests
{
    public sealed class CultivationSliceTests
    {
        static readonly DefinitionId QingyunId = new DefinitionId("base", "cultivation_qingyun_manual");

        static string BaseGamePath =>
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        [Test]
        public void Learn_QingyunManual_Succeeds_AndAppliesModifiers()
        {
            var (world, _, manual) = CreateReadyWorld();
            var entity = world.Entities.CreateCharacter(new DefinitionId("base", "character_protagonist"), "主角").Value;
            entity.Get<AttributesComponent>().SetBase(AttributeId.MaxHp, 100);
            entity.Get<AttributesComponent>().SetBase(AttributeId.Attack, 10);

            var learned = new CultivationService().LearnManual(world, entity.Id, manual);
            Assert.IsTrue(learned.IsSuccess, learned.IsFailure ? learned.Error.ToString() : "");

            var cultivation = entity.Get<CultivationComponent>();
            Assert.IsTrue(cultivation.HasLearnedManual);
            Assert.AreEqual(QingyunId, cultivation.LearnedManualId.Value);
            Assert.AreEqual(25, cultivation.CultivationSpeed);
            Assert.AreEqual(100, cultivation.BreakthroughProgressRequired);
            Assert.AreEqual(130, entity.Get<AttributesComponent>().GetFinal(AttributeId.MaxHp));
            Assert.AreEqual(15, entity.Get<AttributesComponent>().GetFinal(AttributeId.Attack));
            Assert.IsTrue(world.Events.Drain().Exists(e => e.Type == EventType.ModifierAdded));
        }

        [Test]
        public void CultivateAction_AdvancesProgress_AndTicksWorld()
        {
            var (world, loop, manual) = CreateReadyWorld();
            var entity = PrepareLearner(world, manual);

            Assert.IsTrue(loop.EnqueueOrder(loop.CreateCultivateOrder(entity.Id, 4)).IsSuccess);
            loop.TickOnce();
            loop.TickOnce();

            Assert.AreEqual(2UL, world.Tick.Value);
            Assert.AreEqual(50, entity.Get<CultivationComponent>().Progress);
            Assert.AreEqual(RealmStage.Mortal, entity.Get<CultivationComponent>().Realm);
            Assert.IsTrue(entity.Get<ActionStateComponent>().HasActiveAction);
        }

        [Test]
        public void Cultivate_Completes_Breakthrough_Mortal_To_QiRefining()
        {
            var (world, loop, manual) = CreateReadyWorld();
            var entity = PrepareLearner(world, manual);

            Assert.IsTrue(loop.EnqueueOrder(loop.CreateCultivateOrder(entity.Id, 4)).IsSuccess);
            for (var i = 0; i < 4; i++)
                loop.TickOnce();

            Assert.AreEqual(4UL, world.Tick.Value);
            Assert.AreEqual(100, entity.Get<CultivationComponent>().Progress);
            Assert.AreEqual(RealmStage.QiRefining, entity.Get<CultivationComponent>().Realm);
            Assert.IsFalse(entity.Get<ActionStateComponent>().HasActiveAction);

            var events = world.Events.Drain();
            Assert.IsTrue(events.Exists(e => e.Type == EventType.ActionCompleted));
            Assert.IsTrue(events.Exists(e =>
                e.Type == EventType.Breakthrough &&
                e.Payload.Contains("Mortal") &&
                e.Payload.Contains("QiRefining")));
        }

        [Test]
        public void Snapshot_Restore_KeepsCultivationBreakthroughResult()
        {
            var (world, loop, manual) = CreateReadyWorld();
            var entity = PrepareLearner(world, manual);
            Assert.IsTrue(loop.EnqueueOrder(loop.CreateCultivateOrder(entity.Id, 4)).IsSuccess);
            for (var i = 0; i < 4; i++)
                loop.TickOnce();

            var service = new SnapshotService(new JsonSnapshotSerializer());
            var json = service.CaptureJson(world, loop);
            Assert.IsTrue(json.IsSuccess, json.IsFailure ? json.Error.ToString() : "");

            var restored = service.RestoreJson(json.Value, expectedPackageVersion: world.EnabledPackageVersion);
            Assert.IsTrue(restored.IsSuccess, restored.IsFailure ? restored.Error.ToString() : "");

            var e2 = restored.Value.Item1.Entities.All.First();
            var cultivation = e2.Get<CultivationComponent>();
            Assert.AreEqual(RealmStage.QiRefining, cultivation.Realm);
            Assert.AreEqual(100, cultivation.Progress);
            Assert.AreEqual(QingyunId, cultivation.LearnedManualId.Value);
            Assert.AreEqual(130, e2.Get<AttributesComponent>().GetFinal(AttributeId.MaxHp));
        }

        [Test]
        public void Snapshot_MidCultivate_RestoresProgressAndContinuesBreakthrough()
        {
            var (world, loop, manual) = CreateReadyWorld();
            var entity = PrepareLearner(world, manual);
            Assert.IsTrue(loop.EnqueueOrder(loop.CreateCultivateOrder(entity.Id, 4)).IsSuccess);
            loop.TickOnce();
            loop.TickOnce();

            var service = new SnapshotService(new JsonSnapshotSerializer());
            var json = service.CaptureJson(world, loop);
            Assert.IsTrue(json.IsSuccess);

            var restored = service.RestoreJson(json.Value, expectedPackageVersion: world.EnabledPackageVersion);
            Assert.IsTrue(restored.IsSuccess, restored.IsFailure ? restored.Error.ToString() : "");
            var (world2, loop2) = restored.Value;
            var e2 = world2.Entities.All.First();
            Assert.AreEqual(50, e2.Get<CultivationComponent>().Progress);
            Assert.AreEqual(RealmStage.Mortal, e2.Get<CultivationComponent>().Realm);
            Assert.IsTrue(e2.Get<ActionStateComponent>().HasActiveAction);

            loop2.TickOnce();
            loop2.TickOnce();
            Assert.AreEqual(4UL, world2.Tick.Value);
            Assert.AreEqual(RealmStage.QiRefining, e2.Get<CultivationComponent>().Realm);
            Assert.IsTrue(world2.Events.Drain().Exists(e => e.Type == EventType.Breakthrough));
        }

        static (SimulationWorld world, SimulationLoop loop, CultivationManualSpec manual) CreateReadyWorld()
        {
            var loaded = new ContentPackageLoader().Load(new[] { BaseGamePath });
            Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : "");
            Assert.IsTrue(loaded.Value.Registry.TryGetCultivation(QingyunId, out var def));
            var mapped = CultivationManualMapper.ToManualSpec(def);
            Assert.IsTrue(mapped.IsSuccess, mapped.IsFailure ? mapped.Error.ToString() : "");

            var world = new SimulationWorld
            {
                EnabledPackageId = loaded.Value.Manifests[0].ModId,
                EnabledPackageVersion = loaded.Value.Manifests[0].Version.Value
            };
            return (world, new SimulationLoop(world), mapped.Value);
        }

        static Entity PrepareLearner(SimulationWorld world, CultivationManualSpec manual)
        {
            var entity = world.Entities.CreateCharacter(new DefinitionId("base", "character_protagonist"), "主角").Value;
            entity.Get<AttributesComponent>().SetBase(AttributeId.MaxHp, 100);
            entity.Get<AttributesComponent>().SetBase(AttributeId.Attack, 10);
            var learned = new CultivationService().LearnManual(world, entity.Id, manual);
            Assert.IsTrue(learned.IsSuccess, learned.IsFailure ? learned.Error.ToString() : "");
            world.Events.Drain();
            return entity;
        }
    }
}
