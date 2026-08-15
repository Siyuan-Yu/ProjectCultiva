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
            Assert.AreEqual(
                CultivationProgressRules.BaseProgressPerTick * 2,
                entity.Get<CultivationComponent>().Progress);
            Assert.AreEqual(RealmStage.Mortal, entity.Get<CultivationComponent>().Realm);
            Assert.IsTrue(entity.Get<ActionStateComponent>().HasActiveAction);
        }

        [Test]
        public void Cultivate_Completes_DoesNotAutoBreakthrough()
        {
            var (world, loop, manual) = CreateReadyWorld();
            var entity = PrepareLearner(world, manual);
            var need = entity.Get<CultivationComponent>().BreakthroughProgressRequired;
            var per = CultivationProgressRules.BaseProgressPerTick;
            var ticks = (ulong)((need + per - 1) / per);

            Assert.IsTrue(loop.EnqueueOrder(loop.CreateCultivateOrder(entity.Id, ticks)).IsSuccess);
            for (var i = 0UL; i < ticks; i++)
                loop.TickOnce();

            Assert.AreEqual(ticks, world.Tick.Value);
            Assert.AreEqual(need, entity.Get<CultivationComponent>().Progress);
            Assert.AreEqual(RealmStage.Mortal, entity.Get<CultivationComponent>().Realm);
            Assert.AreEqual(0, entity.Get<CultivationComponent>().MinorStage);
            Assert.IsTrue(entity.Get<CultivationComponent>().IsAtBottleneck);
            Assert.IsFalse(entity.Get<ActionStateComponent>().HasActiveAction);
        }

        [Test]
        public void Breakthrough_MortalEarly_To_Mid()
        {
            var (world, loop, manual) = CreateReadyWorld();
            var entity = PrepareLearner(world, manual);
            entity.Get<CultivationComponent>().Progress = 100;

            var broke = new CultivationService().TryBreakthrough(world, entity.Id);
            Assert.IsTrue(broke.IsSuccess, broke.IsFailure ? broke.Error.ToString() : "");
            Assert.IsTrue(broke.Value.Succeeded);
            Assert.AreEqual(RealmStage.Mortal, entity.Get<CultivationComponent>().Realm);
            Assert.AreEqual(1, entity.Get<CultivationComponent>().MinorStage);
            Assert.AreEqual(0, entity.Get<CultivationComponent>().Progress);
            Assert.AreEqual(200, entity.Get<CultivationComponent>().BreakthroughProgressRequired);
            Assert.IsTrue(world.Events.Drain().Exists(e =>
                e.Type == EventType.Breakthrough &&
                e.Payload.Contains("前期") &&
                e.Payload.Contains("中期")));
        }

        [Test]
        public void Breakthrough_Mortal_DoesNotRequireManual_QiRefiningDoes()
        {
            var (world, _, _) = CreateReadyWorld();
            var entity = world.Entities.CreateCharacter(new DefinitionId("base", "character_protagonist"), "主角").Value;
            var cult = entity.Get<CultivationComponent>();
            cult.Realm = RealmStage.Mortal;
            cult.MinorStage = 0;
            cult.LearnedManualId = null;
            new CultivationService().SyncProgressRequired(world, cult);
            cult.Progress = cult.BreakthroughProgressRequired;

            var svc = new CultivationService();
            Assert.IsTrue(svc.CanAttemptBreakthrough(world, entity.Id, out _), "感应境无需功法");
            Assert.IsTrue(svc.TryBreakthrough(world, entity.Id).IsSuccess);

            cult.Realm = RealmStage.QiRefining;
            cult.MinorStage = 1;
            cult.LearnedManualId = null;
            svc.SyncProgressRequired(world, cult);
            cult.Progress = cult.BreakthroughProgressRequired;
            Assert.IsFalse(svc.CanAttemptBreakthrough(world, entity.Id, out var reason));
            Assert.IsTrue(reason.Contains("功法"), reason);
        }

        [Test]
        public void FailBreakthroughChannel_LosesProgress_NoRealmChange()
        {
            var (world, _, _) = CreateReadyWorld();
            var entity = world.Entities.CreateCharacter(new DefinitionId("base", "character_protagonist"), "主角").Value;
            var cult = entity.Get<CultivationComponent>();
            cult.Realm = RealmStage.Mortal;
            cult.MinorStage = 0;
            new CultivationService().SyncProgressRequired(world, cult);
            cult.Progress = cult.BreakthroughProgressRequired;
            var before = cult.Progress;

            var r = new CultivationService().FailBreakthroughChannel(world, entity.Id, "测试打断");
            Assert.IsTrue(r.IsSuccess);
            Assert.IsFalse(r.Value.Succeeded);
            Assert.AreEqual(RealmStage.Mortal, cult.Realm);
            Assert.Less(cult.Progress, before);
            Assert.IsTrue(r.Value.Detail.Contains("打断"));
        }

        [Test]
        public void Breakthrough_MortalLate_To_QiRefining_GrantsSpiritPower()
        {
            var (world, _, manual) = CreateReadyWorld();
            var entity = PrepareLearner(world, manual);
            var cult = entity.Get<CultivationComponent>();
            cult.MinorStage = 2;
            new CultivationService().SyncProgressRequired(world, cult);
            cult.Progress = cult.BreakthroughProgressRequired;

            var broke = new CultivationService().TryBreakthrough(world, entity.Id);
            Assert.IsTrue(broke.IsSuccess, broke.IsFailure ? broke.Error.ToString() : "");
            Assert.IsTrue(broke.Value.Succeeded, broke.Value.Detail);
            Assert.AreEqual(RealmStage.QiRefining, cult.Realm);
            Assert.AreEqual(1, cult.MinorStage);
            Assert.GreaterOrEqual(entity.Get<AttributesComponent>().GetFinal(AttributeId.SpiritPower), 50);
            Assert.IsTrue(entity.TryGet<XianXia.Core.Combat.CombatVitalsComponent>(out var vitals));
            Assert.Greater(vitals.CurrentSpiritPower, 0);
        }

        [Test]
        public void CombatDamage_DrainsSpiritBeforeHp()
        {
            var (world, _, manual) = CreateReadyWorld();
            var entity = PrepareLearner(world, manual);
            var cult = entity.Get<CultivationComponent>();
            cult.Realm = RealmStage.QiRefining;
            cult.MinorStage = 1;
            entity.Get<AttributesComponent>().SetBase(AttributeId.SpiritPower, 40);
            entity.Get<AttributesComponent>().SetBase(AttributeId.MaxHp, 100);
            XianXia.Core.Combat.CombatDamageRules.EnsureVitals(entity);
            var vitals = entity.Get<XianXia.Core.Combat.CombatVitalsComponent>();
            vitals.CurrentHp = 100;
            vitals.CurrentSpiritPower = 40;

            XianXia.Core.Combat.CombatDamageRules.ApplyIncoming(entity, 25);
            Assert.AreEqual(15, vitals.CurrentSpiritPower);
            Assert.AreEqual(100, vitals.CurrentHp);

            XianXia.Core.Combat.CombatDamageRules.ApplyIncoming(entity, 30);
            Assert.AreEqual(0, vitals.CurrentSpiritPower);
            Assert.AreEqual(85, vitals.CurrentHp);
        }

        [Test]
        public void Snapshot_Restore_KeepsCultivationBreakthroughResult()
        {
            var (world, _, manual) = CreateReadyWorld();
            var entity = PrepareLearner(world, manual);
            entity.Get<CultivationComponent>().Progress = 100;
            Assert.IsTrue(new CultivationService().TryBreakthrough(world, entity.Id).Value.Succeeded);

            var service = new SnapshotService(new JsonSnapshotSerializer());
            var json = service.CaptureJson(world, new SimulationLoop(world));
            Assert.IsTrue(json.IsSuccess, json.IsFailure ? json.Error.ToString() : "");

            var restored = service.RestoreJson(json.Value, expectedPackageVersion: world.EnabledPackageVersion);
            Assert.IsTrue(restored.IsSuccess, restored.IsFailure ? restored.Error.ToString() : "");

            var e2 = restored.Value.Item1.Entities.All.First();
            var cultivation = e2.Get<CultivationComponent>();
            Assert.AreEqual(RealmStage.Mortal, cultivation.Realm);
            Assert.AreEqual(1, cultivation.MinorStage);
            Assert.AreEqual(QingyunId, cultivation.LearnedManualId.Value);
        }

        [Test]
        public void Snapshot_MidCultivate_RestoresProgressWithoutAutoBreakthrough()
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
            Assert.AreEqual(CultivationProgressRules.BaseProgressPerTick * 2, e2.Get<CultivationComponent>().Progress);
            Assert.AreEqual(RealmStage.Mortal, e2.Get<CultivationComponent>().Realm);
            Assert.IsTrue(e2.Get<ActionStateComponent>().HasActiveAction);

            loop2.TickOnce();
            loop2.TickOnce();
            Assert.AreEqual(4UL, world2.Tick.Value);
            Assert.AreEqual(RealmStage.Mortal, e2.Get<CultivationComponent>().Realm);
            Assert.AreEqual(CultivationProgressRules.BaseProgressPerTick * 4, e2.Get<CultivationComponent>().Progress);
            Assert.IsFalse(world2.Events.Drain().Exists(e =>
                e.Type == EventType.Breakthrough && !e.Payload.StartsWith("fail:")));
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
            if (loaded.Value.Registry.TryGetPrimaryRealmLadder(out var ladderDef))
            {
                var board = RealmLadderMapper.ToBoard(ladderDef);
                Assert.IsTrue(board.IsSuccess, board.IsFailure ? board.Error.ToString() : "");
                world.RealmLadder = board.Value;
            }

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
