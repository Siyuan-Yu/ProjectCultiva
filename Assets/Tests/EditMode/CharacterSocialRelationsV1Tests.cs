using System.Linq;
using System.IO;
using NUnit.Framework;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Persistence;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Data.Serialization;
using XianXia.Data.Content;

namespace XianXia.Tests
{
    public sealed class CharacterSocialRelationsV1Tests
    {
        static string BaseGamePath
        {
            get
            {
                var current = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
                while (current != null)
                {
                    var candidate = Path.Combine(current.FullName, "Content", "BaseGame");
                    if (Directory.Exists(candidate))
                        return candidate;
                    current = current.Parent;
                }
                return Path.GetFullPath(Path.Combine("Content", "BaseGame"));
            }
        }

        static SimulationWorld World(out Entity a, out Entity b, out Entity c)
        {
            var world = new SimulationWorld();
            a = world.Entities.CreateNpc(new DefinitionId("test", "a"), "甲").Value;
            b = world.Entities.CreateNpc(new DefinitionId("test", "b"), "乙").Value;
            c = world.Entities.CreateNpc(new DefinitionId("test", "c"), "丙").Value;
            return world;
        }

        [Test]
        public void FiveAxesClampAndLegacyScoreMeansAffection()
        {
            var world = World(out var a, out var b, out _);
            var service = new RelationshipService();
            Assert.IsTrue(service.RecordAttitudeDelta(world, a.Id, b.Id,
                SocialAttitudeAxis.Affection, 130, "test", out var affection).IsSuccess);
            Assert.AreEqual(100, affection);
            Assert.IsTrue(service.RecordAttitudeDelta(world, a.Id, b.Id,
                SocialAttitudeAxis.Grudge, -10, "test", out var grudge).IsSuccess);
            Assert.AreEqual(0, grudge);
            Assert.IsTrue(service.RecordAttitudeDelta(world, a.Id, b.Id,
                SocialAttitudeAxis.Trust, -130, "test", out var trust).IsSuccess);
            Assert.AreEqual(-100, trust);
            Assert.AreEqual(100, world.Relationships.Score(a.Id, b.Id));
            Assert.AreEqual(-100, world.Relationships.GetAttitude(a.Id, b.Id).Trust);
        }

        [Test]
        public void BondsHaveDirectionCanonicalSymmetryAndRemovalRules()
        {
            var world = World(out var parent, out var child, out var third);
            var service = new SocialBondService();
            Assert.IsTrue(service.TryAddBond(world, SocialBondKind.ParentChild, parent.Id, child.Id).IsSuccess);
            Assert.AreEqual("子女", SocialBondQuery.GetRoleLabel(world.SocialBonds.All[0], parent.Id));
            Assert.AreEqual("父母", SocialBondQuery.GetRoleLabel(world.SocialBonds.All[0], child.Id));
            Assert.IsTrue(service.TryRemoveBond(world, SocialBondKind.ParentChild, parent.Id, child.Id).IsFailure);
            Assert.IsTrue(service.TryAddBond(world, SocialBondKind.Spouse, third.Id, child.Id).IsSuccess);
            Assert.IsTrue(world.SocialBonds.Contains(SocialBondKind.Spouse, child.Id, third.Id));
            Assert.IsTrue(service.TryRemoveBond(world, SocialBondKind.Spouse, child.Id, third.Id).IsSuccess);
            Assert.IsTrue(service.TryAddBond(world, SocialBondKind.MasterDisciple, parent.Id, third.Id).IsSuccess);
            var masterBond = world.SocialBonds.All[world.SocialBonds.Count - 1];
            Assert.AreEqual("徒弟", SocialBondQuery.GetRoleLabel(masterBond, parent.Id));
            Assert.AreEqual("师父", SocialBondQuery.GetRoleLabel(masterBond, third.Id));
        }

        [Test]
        public void AttackHelpAndRescueApplyExactDirectedConsequences()
        {
            var world = World(out var actor, out var target, out _);
            var events = new SocialEventService();
            Assert.IsTrue(events.RecordCharacterAttacked(world, actor.Id, target.Id).IsSuccess);
            var attitude = world.Relationships.GetAttitude(target.Id, actor.Id);
            Assert.AreEqual(-10, attitude.Affection);
            Assert.AreEqual(10, attitude.Grudge);
            Assert.IsTrue(events.RecordCharacterHelped(world, actor.Id, target.Id).IsSuccess);
            Assert.IsTrue(events.RecordCharacterRescued(world, actor.Id, target.Id).IsSuccess);
            attitude = world.Relationships.GetAttitude(target.Id, actor.Id);
            Assert.AreEqual(25, attitude.Affection);
            Assert.AreEqual(20, attitude.Trust);
            Assert.AreEqual(10, attitude.Grudge);
        }

        [Test]
        public void KillReactionUsesStrongestAttachmentAndPublishesOncePerReactor()
        {
            var world = World(out var parent, out var victim, out var killer);
            Assert.IsTrue(new SocialBondService().TryAddBond(
                world, SocialBondKind.ParentChild, parent.Id, victim.Id).IsSuccess);
            world.Events.Drain();
            Assert.IsTrue(new SocialEventService().RecordCharacterKilled(world, killer.Id, victim.Id).IsSuccess);
            var reaction = world.Relationships.GetAttitude(parent.Id, killer.Id);
            Assert.AreEqual(-45, reaction.Affection);
            Assert.AreEqual(70, reaction.Grudge);
            Assert.AreEqual(1, world.Events.Drain().Count(e =>
                e.Type == XianXia.Core.Events.EventType.SocialReaction && e.Actor == parent.Id));
        }

        [Test]
        public void NegativeAttachmentMakesKillerMoreLikedWithoutReducingGrudge()
        {
            var world = World(out var reactor, out var victim, out var killer);
            var relationships = new RelationshipService();
            Assert.IsTrue(relationships.Record(world, reactor.Id, victim.Id, -80, "fixture").IsSuccess);
            Assert.IsTrue(relationships.RecordAttitudeDelta(world, reactor.Id, killer.Id,
                SocialAttitudeAxis.Grudge, 30, "fixture", out _).IsSuccess);
            Assert.IsTrue(new SocialEventService().RecordCharacterKilled(world, killer.Id, victim.Id).IsSuccess);
            var reaction = world.Relationships.GetAttitude(reactor.Id, killer.Id);
            Assert.AreEqual(25, reaction.Affection);
            Assert.AreEqual(30, reaction.Grudge);
        }

        [Test]
        public void UnrelatedCharacterHasNoKillReaction()
        {
            var world = World(out var unrelated, out var victim, out var killer);
            Assert.IsTrue(new SocialEventService().RecordCharacterKilled(world, killer.Id, victim.Id).IsSuccess);
            Assert.IsTrue(world.Relationships.GetAttitude(unrelated.Id, killer.Id).IsZero);
        }

        [Test]
        public void SnapshotPreservesAxesBondsAndBleedoutAttribution()
        {
            var world = World(out var parent, out var victim, out var killer);
            Assert.IsTrue(new SocialBondService().TryAddBond(
                world, SocialBondKind.ParentChild, parent.Id, victim.Id).IsSuccess);
            Assert.IsTrue(new RelationshipService().RecordAttitudeDelta(world, parent.Id, victim.Id,
                SocialAttitudeAxis.Respect, 17, "fixture", out _).IsSuccess);
            Assert.IsTrue(new RelationshipService().RecordAttitudeDelta(world, parent.Id, killer.Id,
                SocialAttitudeAxis.Trust, 4, "context_fixture", out _, null, victim.Id).IsSuccess);
            Assert.IsTrue(CombatLifeStateService.TryEnterIncapacitated(world, victim, killer.Id));
            var snapshots = new SnapshotService(new JsonSnapshotSerializer());
            var json = snapshots.CaptureJson(world, new SimulationLoop(world));
            Assert.IsTrue(json.IsSuccess, json.IsFailure ? json.Error.ToString() : string.Empty);
            var restored = snapshots.RestoreJson(json.Value);
            Assert.IsTrue(restored.IsSuccess, restored.IsFailure ? restored.Error.ToString() : string.Empty);
            Assert.AreEqual(1, restored.Value.world.SocialBonds.Count);
            Assert.AreEqual(17, restored.Value.world.Relationships.GetAttitude(parent.Id, victim.Id).Respect);
            Assert.IsTrue(restored.Value.world.Relationships.Events.Any(e =>
                e.Axis == SocialAttitudeAxis.Trust && e.ContextEntityId == victim.Id));
            Assert.IsTrue(restored.Value.world.Entities.TryGet(victim.Id, out var restoredVictim));
            var life = restoredVictim.Get<LifecycleComponent>();
            restored.Value.world.Tick = new WorldTick(life.BleedOutAfterTick);
            CombatLifeStateService.TickLifeStateDecay(restored.Value.world);
            Assert.AreEqual(-45, restored.Value.world.Relationships.GetAttitude(parent.Id, killer.Id).Affection);
        }

        [Test]
        public void LegacyRelationshipEventWithoutAxisDefaultsToAffection()
        {
            var serializer = new JsonSnapshotSerializer();
            var dto = new WorldSnapshot();
            dto.RelationshipEvents.Add(new RelationshipEventSnapshotDto
            {
                FromEntityId = 1, ToEntityId = 2, Delta = 7, ReasonTag = "legacy"
            });
            var json = serializer.Serialize(dto);
            Assert.IsTrue(json.IsSuccess);
            var legacyJson = json.Value.Replace("\"axis\":0,", string.Empty);
            var read = serializer.Deserialize(legacyJson);
            Assert.IsTrue(read.IsSuccess);
            Assert.IsFalse(read.Value.RelationshipEvents[0].HasAxis);
            Assert.AreEqual(0, read.Value.RelationshipEvents[0].Axis);
        }

        [Test]
        public void OpeningBondAcceptanceFixtureLoadsThroughStrictContentPipeline()
        {
            var loaded = new ContentPackageLoader().Load(new[] { BaseGamePath });
            Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : string.Empty);
            Assert.IsTrue(loaded.Value.Registry.TryGetOpeningScenario(
                new DefinitionId("base", "scenario_ch01_reference"), out var scenario));
            Assert.AreEqual(4, scenario.OpeningBonds.Count);
            Assert.IsTrue(scenario.OpeningBonds.Any(b => b.Kind == SocialBondKind.ParentChild));
            Assert.IsTrue(scenario.OpeningBonds.Any(b => b.Kind == SocialBondKind.Sibling));
            Assert.IsTrue(scenario.OpeningBonds.Any(b =>
                b.Kind == SocialBondKind.ParentChild &&
                b.FromDefinitionId == "base:character_ch01_ref_mortal_a" &&
                b.ToDefinitionId == "base:character_ch01_ref_farmer_b"));
            Assert.IsTrue(scenario.OpeningBonds.Any(b =>
                b.Kind == SocialBondKind.ParentChild &&
                b.FromDefinitionId == "base:character_ch01_ref_herb_b" &&
                b.ToDefinitionId == "base:character_ch01_ref_farmer_b"));
        }
    }
}
