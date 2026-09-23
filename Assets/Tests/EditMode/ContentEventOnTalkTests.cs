using NUnit.Framework;
using System.Linq;
using XianXia.Core.Cultivation;
using XianXia.Core.Entities;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;
using XianXia.Data.Content;
using XianXia.Data.Bootstrap;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;

namespace XianXia.Tests
{
    public sealed class ContentEventOnTalkTests
    {
        [Test]
        public void OnTalk_TriggersMatchingNpcEvent()
        {
            var world = new SimulationWorld();
            var spec = new ContentEventSpec
            {
                Id = "base:event_talk_test",
                Name = "测试对话",
                Body = "你好。",
                Trigger = "onTalk",
                NpcDefinitionId = "base:character_test_npc",
                Once = false
            };
            spec.Choices.Add(new ContentEventChoiceSpec { Id = "ok", Text = "好" });
            world.ContentEvents.Register(spec);

            var svc = new ContentEventService();
            Assert.IsTrue(svc.TryTalkToNpc(world, EntityId.None, "base:character_test_npc").IsSuccess);
            Assert.IsTrue(world.ContentEvents.HasActive);
            Assert.AreEqual("base:event_talk_test", world.ContentEvents.ActiveEventId);
        }

        [Test]
        public void OnTalk_IgnoresDifferentNpc()
        {
            var world = new SimulationWorld();
            var spec = new ContentEventSpec
            {
                Id = "base:event_talk_test",
                Trigger = "onTalk",
                NpcDefinitionId = "base:character_a"
            };
            world.ContentEvents.Register(spec);

            var svc = new ContentEventService();
            svc.TryTalkToNpc(world, EntityId.None, "base:character_b");
            Assert.IsFalse(world.ContentEvents.HasActive);
        }

        // EVENT-01A: small existing-fixture additions requested by the producer; compile only in this delivery.
        [Test]
        public void InteractionKeepsOnlyHighestPriorityAndSortsTiedTopics()
        {
            var world = new SimulationWorld();
            foreach (var pair in new[] { ("z", 5), ("low", 0), ("a", 5) })
                world.ContentEvents.Register(new ContentEventSpec { Id = "test:" + pair.Item1, Trigger = "onTalk", Priority = pair.Item2, Once = false });
            var service = new ContentEventService();
            var candidates = service.ResolveInteractionCandidates(world, EntityId.None, EntityId.None, "onTalk", "test:npc");
            CollectionAssert.AreEqual(new[] { "test:a", "test:z" }, candidates.Select(x => x.Id).ToArray());
            service.TryTalkToNpc(world, EntityId.None, "test:npc");
            Assert.IsFalse(world.ContentEvents.HasActive, "Tied topics must not auto-begin.");
        }

        [Test]
        public void PartyEligibilityRequiresOneMemberToPassTheEntireSet()
        {
            var world = new SimulationWorld();
            var actor = world.Entities.CreateCharacter(new DefinitionId("test", "actor"), "actor").Value;
            var ally = world.Entities.CreateCharacter(new DefinitionId("test", "ally"), "ally").Value;
            Assert.IsTrue(actor.TryGet<CultivationComponent>(out var actorCultivation));
            actorCultivation.LearnedManualId = new DefinitionId("test", "manual");
            Assert.IsTrue(ally.TryGet<CultivationComponent>(out var allyCultivation));
            allyCultivation.Realm = RealmStage.QiRefining;
            var squad = SquadMembershipService.Create(world, SquadMembershipService.PlayerSquadId, new[] { actor.Id, ally.Id }, actor.Id);
            Assert.IsTrue(squad.IsSuccess);
            var party = new PlayerPartyRuntime(); party.BindWorld(world);
            Assert.IsTrue(party.TryBindControlledSquad(squad.Value.SquadId, actor.Id, out _));
            var realm = new ContentCondition { Kind = "realmAtLeast", Realm = "QiRefining" };
            Assert.IsFalse(ContentConditionEvaluator.AllPass(world, actor.Id, new[] { realm }));
            Assert.IsTrue(ContentEventService.InteractionConditionsPass(world, actor.Id, new[] { realm }));
            Assert.IsFalse(ContentEventService.InteractionConditionsPass(world, actor.Id,
                new[] { realm, new ContentCondition { Kind = "hasManual", Id = "test:manual" } }));
        }

        [TestCase("global", false, false)]
        [TestCase("perTarget", false, true)]
        [TestCase("perActorTarget", true, true)]
        public void OnceScopesUseActualInstances(string scope, bool otherActor, bool otherTarget)
        {
            var world = new SimulationWorld();
            var actor = world.Entities.CreateCharacter(new DefinitionId("test", "actor"), "actor").Value.Id;
            var ally = world.Entities.CreateCharacter(new DefinitionId("test", "ally"), "ally").Value.Id;
            var target = world.Entities.CreateCharacter(new DefinitionId("test", "npc"), "npc").Value.Id;
            var target2 = world.Entities.CreateCharacter(new DefinitionId("test", "npc"), "npc2").Value.Id;
            var spec = new ContentEventSpec { Id = "test:event", Trigger = "onTalk", OnceScope = scope, Body = "legacy" };
            world.ContentEvents.Register(spec);
            var service = new ContentEventService();
            Assert.IsTrue(service.BeginInteraction(world, actor, target, spec.Id, "onTalk", "test:npc").IsSuccess);
            Assert.AreEqual("legacy", ContentEventService.ActiveStep(world).Text);
            Assert.AreEqual("@target", ContentEventService.ActiveStep(world).SpeakerRef);
            Assert.IsTrue(service.ResolveChoice(world, actor, "").IsSuccess);
            Assert.AreEqual(scope == "global", world.ContentEvents.HasFired(spec.Id));
            Assert.AreEqual(otherActor, service.ResolveInteractionCandidates(world, ally, target, "onTalk", "test:npc").Count > 0);
            Assert.AreEqual(otherTarget, service.ResolveInteractionCandidates(world, actor, target2, "onTalk", "test:npc").Count > 0);
        }

        [Test]
        public void StepsKeepBoundActorAndTargetAndOnlyFireAtFinish()
        {
            var world = new SimulationWorld();
            var actor = world.Entities.CreateCharacter(new DefinitionId("test", "actor"), "玩家").Value;
            var target = world.Entities.CreateCharacter(new DefinitionId("test", "npc"), "对象").Value.Id;
            var spec = new ContentEventSpec { Id = "test:event", Trigger = "onTalk", EntryStepId = "entry" };
            var entry = new ContentEventStepSpec { Id = "entry", SpeakerRef = "@target" };
            entry.Choices.Add(new ContentEventChoiceSpec { Id = "truth", NextStepId = "actor" });
            spec.Steps.Add(entry);
            spec.Steps.Add(new ContentEventStepSpec { Id = "actor", SpeakerRef = "@actor", NextStepId = "target" });
            var last = new ContentEventStepSpec { Id = "target", SpeakerRef = "@target" };
            last.Outcomes.Add(new ContentOutcome { Kind = "grantProgress", Amount = 3 }); spec.Steps.Add(last);
            world.ContentEvents.Register(spec);
            var service = new ContentEventService();
            Assert.IsTrue(service.BeginInteraction(world, actor.Id, target, spec.Id, "onTalk", "test:npc").IsSuccess);
            Assert.IsTrue(service.ResolveChoice(world, target, "truth").IsSuccess);
            Assert.AreEqual("actor", world.ContentEvents.ActiveStepId);
            Assert.AreEqual(actor.Id, world.ContentEvents.ActiveActorId);
            Assert.AreEqual(target, world.ContentEvents.ActiveTargetEntityId);
            Assert.IsFalse(world.ContentEvents.HasFired(spec.Id));
            Assert.IsTrue(ContentEventService.ResolveSpeaker(world, "@actor", out var speaker).IsSuccess);
            Assert.AreEqual("玩家", speaker);
            Assert.IsTrue(service.ResolveChoice(world, target, "").IsSuccess);
            Assert.IsTrue(service.ResolveChoice(world, target, "").IsSuccess);
            Assert.IsTrue(world.ContentEvents.HasFired(spec.Id));
            Assert.IsFalse(world.ContentEvents.HasActive);
            Assert.IsTrue(actor.TryGet<CultivationComponent>(out var cultivation));
            Assert.AreEqual(3, cultivation.Progress);
        }

        [Test]
        public void FailedCombinedOutcomesRestoreStepAndFlags()
        {
            var world = new SimulationWorld();
            var spec = new ContentEventSpec { Id = "test:event", EntryStepId = "entry" };
            var step = new ContentEventStepSpec { Id = "entry" };
            step.Outcomes.Add(new ContentOutcome { Kind = "setFlag", Id = "test:temporary" });
            var choice = new ContentEventChoiceSpec { Id = "bad" };
            choice.Outcomes.Add(new ContentOutcome { Kind = "startQuest", Id = "test:missing" });
            step.Choices.Add(choice); spec.Steps.Add(step); world.ContentEvents.Register(spec);
            world.ContentEvents.SetActive(spec.Id, new EntityId(1), new EntityId(2), true);
            Assert.IsTrue(new ContentEventService().ResolveChoice(world, EntityId.None, "bad").IsFailure);
            Assert.IsFalse(world.Flags.Has("test:temporary"));
            Assert.AreEqual("entry", world.ContentEvents.ActiveStepId);
            Assert.AreEqual(new EntityId(1), world.ContentEvents.ActiveActorId);
            Assert.AreEqual(new EntityId(2), world.ContentEvents.ActiveTargetEntityId);
            Assert.IsTrue(world.ContentEvents.ActiveInteraction);
            Assert.IsFalse(world.ContentEvents.HasFired(spec.Id));
        }

        [Test]
        public void OnInspectUsesSharedArbitrationAndExactStableObjectBinding()
        {
            var world = new SimulationWorld();
            world.ContentEvents.Register(new ContentEventSpec
            {
                Id = "test:kind", Trigger = "onInspect", WorldObjectKind = "controlCore",
                Priority = 10, Once = false
            });
            world.ContentEvents.Register(new ContentEventSpec
            {
                Id = "test:exact", Trigger = "onInspect", WorldObjectKind = "controlCore",
                WorldObjectId = "test:core_a", Priority = 20, Once = false
            });
            var context = new ContentInteractionContext
            {
                ActorId = new EntityId(1), TargetKind = "controlCore",
                TargetDefinitionId = "test:core_a", TargetKey = "controlCore:test:core_a",
                TargetDisplayName = "议政厅"
            };
            var service = new ContentEventService();
            CollectionAssert.AreEqual(new[] { "test:exact" },
                service.ResolveInteractionCandidates(world, context, "onInspect").Select(x => x.Id).ToArray());
            Assert.IsTrue(service.BeginInteraction(world, context, "test:exact", "onInspect").IsSuccess);
            Assert.AreEqual(context.TargetKey, world.ContentEvents.ActiveTargetKey);
            Assert.IsTrue(ContentEventService.ResolveSpeaker(world, "@target", out var speaker).IsSuccess);
            Assert.AreEqual("议政厅", speaker);
        }

        [Test]
        public void ObjectOnceScopesUseStableTargetKey()
        {
            void Verify(string scope, bool otherActor, bool otherTarget)
            {
                var world = new SimulationWorld();
                var spec = new ContentEventSpec
                {
                    Id = "test:inspect", Trigger = "onInspect", WorldObjectKind = "controlCore",
                    OnceScope = scope, Body = "inspect"
                };
                world.ContentEvents.Register(spec);
                ContentInteractionContext Context(int actor, string id) => new ContentInteractionContext
                {
                    ActorId = new EntityId((ulong)actor), TargetKind = "controlCore",
                    TargetDefinitionId = id, TargetKey = "controlCore:" + id, TargetDisplayName = id
                };
                var service = new ContentEventService();
                Assert.IsTrue(service.BeginInteraction(world, Context(1, "core_a"), spec.Id, "onInspect").IsSuccess);
                Assert.IsTrue(service.ResolveChoice(world, new EntityId(99), "").IsSuccess);
                Assert.AreEqual(otherActor,
                    service.ResolveInteractionCandidates(world, Context(2, "core_a"), "onInspect").Count > 0);
                Assert.AreEqual(otherTarget,
                    service.ResolveInteractionCandidates(world, Context(1, "core_b"), "onInspect").Count > 0);
            }
            Verify("perTarget", false, true);
            Verify("perActorTarget", true, true);
        }

        [Test]
        public void DefinitionsOnlyRehydrateRegistersNarrativeSpecsWithoutActivatingChapter()
        {
            var registry = new DefinitionRegistry();
            Assert.IsTrue(registry.RegisterQuest(new QuestDefinition
                { Id = new DefinitionId("test", "quest") }).IsSuccess);
            Assert.IsTrue(registry.RegisterContentEvent(new ContentEventDefinition
            {
                Id = new DefinitionId("test", "event"), Trigger = "onInspect",
                WorldObjectKind = "controlCore", WorldObjectId = "test:core"
            }).IsSuccess);
            Assert.IsTrue(registry.RegisterChapter(new ChapterDefinition
                { Id = new DefinitionId("test", "chapter") }).IsSuccess);
            var world = new SimulationWorld();
            Assert.IsTrue(ContentRuntimeBootstrap.RehydrateContentDefinitions(world, registry).IsSuccess);
            Assert.IsTrue(ChapterRuntimeBootstrap.ApplyDefinitions(world, registry).IsSuccess);
            Assert.IsTrue(world.Quests.TryGetSpec("test:quest", out _));
            Assert.IsTrue(world.ContentEvents.TryGet("test:event", out var spec));
            Assert.AreEqual("test:core", spec.WorldObjectId);
            Assert.IsTrue(world.Chapters.TryGet("test:chapter", out _));
            Assert.IsFalse(world.Chapters.HasActive, "Definitions-only load must not activate an opening chapter.");
            Assert.IsFalse(world.ContentEvents.HasActive, "Definitions-only load must not present an opening event.");
        }

        [TestCase("missing", "nextStepId missing")]
        [TestCase("entry", "Step cycle")]
        public void InvalidStepGraphIsRejected(string next, string diagnostic)
        {
            var registry = new DefinitionRegistry();
            var definition = new ContentEventDefinition { Id = new DefinitionId("test", "event"), EntryStepId = "entry" };
            definition.Steps.Add(new ContentEventStepSpec { Id = "entry", NextStepId = next });
            registry.RegisterContentEvent(definition);
            var report = new ContentReferenceValidator().Validate(registry);
            Assert.IsTrue(report.Errors.Any(e => e.Message.Contains(diagnostic)));
        }
    }
}
