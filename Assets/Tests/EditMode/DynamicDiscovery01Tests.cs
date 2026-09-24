using NUnit.Framework;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Opportunity;
using XianXia.Core.Persistence;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;
using XianXia.Core.World.Surface;
using XianXia.Data.Content;
using XianXia.Data.Serialization;
using System;
using System.Collections.Generic;
using System.IO;

namespace XianXia.Tests
{
    public sealed class DynamicDiscovery01Tests
    {
        const string Definition = "test:dynamic_object";
        const string Surface = "test:surface";

        [Test]
        public void LegacyDefinitionDefaultsToNpcAndWeightZeroIsNotDirectorEligible()
        {
            Assert.AreEqual("npc", new WorldOpportunityDefinition().SpawnKind);
            var spec = Spec(WorldOpportunityDiscoveryMode.WorldVisible); spec.Weight = 0;
            Assert.AreEqual(0, spec.Weight);
            Assert.IsTrue(new SimulationWorld().WorldOpportunities.RegisterSpec(spec));
        }

        [Test]
        public void BoardEnforcesExclusiveNpcAndWorldObjectIdentityAndStableLookup()
        {
            var board = new WorldOpportunityBoard();
            var id = board.AllocateInstanceId();
            var obj = Instance(id, WorldOpportunityDiscoveryMode.WorldVisible, true, 2, 3);
            Assert.IsTrue(board.AddInstance(obj));
            Assert.AreEqual("worldObject:opportunity:1", obj.WorldObjectInstanceId);
            Assert.IsTrue(board.TryGetByWorldObject(obj.WorldObjectInstanceId, out var found));
            Assert.AreSame(obj, found);
            Assert.IsFalse(board.AddInstance(new WorldOpportunityInstance
            {
                InstanceId = "opportunity:2", SpawnKind = WorldOpportunitySpawnKind.WorldObject,
                SpawnedEntityId = new EntityId(1), WorldObjectInstanceId = "worldObject:opportunity:2"
            }));
        }

        [Test]
        public void HiddenDiscoveryUsesLivingPartyDomainPositionAndOnlyFiresOnce()
        {
            var world = WorldWithPartyAt(0, 0);
            var spec = Spec(WorldOpportunityDiscoveryMode.HiddenUntilDiscovered); spec.DiscoveryRadiusWorld = 1.25f;
            world.WorldOpportunities.RegisterSpec(spec);
            var instance = Instance("opportunity:1", spec.DiscoveryMode, false, 2, 0);
            Assert.IsTrue(world.WorldOpportunities.AddInstance(instance));
            var service = new WorldOpportunityDiscoveryService();
            Assert.AreEqual(0, service.Tick(world));
            world.PlayerPartyTravel.SetAtSurfacePosition(Surface, new WorldVec2(.75f, 0));
            Assert.AreEqual(1, service.Tick(world));
            Assert.IsTrue(instance.IsDiscovered);
            Assert.IsTrue(world.WorldActivities.TryGetActiveBySource(WorldActivitySourceKind.WorldOpportunity,
                instance.InstanceId, out _));
            var eventCount = world.Events.Count;
            Assert.AreEqual(0, service.Tick(world));
            Assert.AreEqual(eventCount, world.Events.Count);
        }

        [Test]
        public void ExplicitResolveIsTransactionalAndInspectWithoutResolveKeepsInstance()
        {
            var world = new SimulationWorld();
            var instance = Instance("opportunity:1", WorldOpportunityDiscoveryMode.PublicNotice, true, 1, 1);
            world.WorldOpportunities.AddInstance(instance);
            world.WorldActivities.CreateActive(WorldActivitySourceKind.WorldOpportunity, instance.InstanceId,
                "Title", "Body", 0);
            var context = Context(instance);
            var failed = ContentOutcomeApplier.ApplyAll(world, EntityId.None, new[]
            {
                new ContentOutcome { Kind = "resolveCurrentOpportunity" },
                new ContentOutcome { Kind = "unknown" }
            }, context);
            Assert.IsTrue(failed.IsFailure);
            Assert.IsTrue(world.WorldOpportunities.TryGetInstance(instance.InstanceId, out _));
            Assert.IsTrue(world.WorldActivities.TryGetActiveBySource(WorldActivitySourceKind.WorldOpportunity,
                instance.InstanceId, out _));

            Assert.IsTrue(ContentOutcomeApplier.ApplyAll(world, EntityId.None,
                System.Array.Empty<ContentOutcome>(), context).IsSuccess);
            Assert.IsTrue(world.WorldOpportunities.TryGetInstance(instance.InstanceId, out _));
            Assert.IsTrue(ContentOutcomeApplier.Apply(world, EntityId.None,
                new ContentOutcome { Kind = "resolveCurrentOpportunity" }).IsFailure,
                "The outcome cannot resolve without its bound interaction context.");
            Assert.IsTrue(ContentOutcomeApplier.ApplyAll(world, EntityId.None,
                new[] { new ContentOutcome { Kind = "resolveCurrentOpportunity" } }, context).IsSuccess);
            Assert.IsFalse(world.WorldOpportunities.TryGetInstance(instance.InstanceId, out _));
        }

        [Test] public void SnapshotV9RoundTripsUndiscoveredObject() => AssertSnapshotRoundTrip(false);
        [Test] public void SnapshotV9RoundTripsDiscoveredObject() => AssertSnapshotRoundTrip(true);

        [Test]
        public void SnapshotV8IsExplicitlyRejectedWithoutMigrationGuessing()
        {
            var result = new SnapshotService(new JsonSnapshotSerializer()).Restore(
                new WorldSnapshot { SchemaVersion = WorldSnapshot.LegacySchemaVersionV8 });
            Assert.IsTrue(result.IsFailure);
            Assert.AreEqual(ErrorCode.SnapshotVersionMismatch, result.Error.Code);
            StringAssert.Contains("v1-v8", result.Error.Message);
        }

        void AssertSnapshotRoundTrip(bool discovered)
        {
            var world = new SimulationWorld();
            var instance = Instance(world.WorldOpportunities.AllocateInstanceId(), WorldOpportunityDiscoveryMode.HiddenUntilDiscovered,
                discovered, 4.25f, -3.5f);
            world.WorldOpportunities.AddInstance(instance);
            if (discovered)
                world.WorldActivities.CreateActive(WorldActivitySourceKind.WorldOpportunity, instance.InstanceId,
                    "发现", "已经发现", 0);
            var service = new SnapshotService(new JsonSnapshotSerializer());
            var json = service.CaptureJson(world, PlayableSimulationLoopFactory.Create(world));
            Assert.IsTrue(json.IsSuccess, json.IsFailure ? json.Error.ToString() : string.Empty);
            StringAssert.Contains("\"schemaVersion\":9", json.Value.Replace(" ", string.Empty));
            var restored = service.RestoreJson(json.Value);
            Assert.IsTrue(restored.IsSuccess, restored.IsFailure ? restored.Error.ToString() : string.Empty);
            Assert.IsTrue(restored.Value.world.WorldOpportunities.TryGetByWorldObject(
                instance.WorldObjectInstanceId, out var value));
            Assert.AreEqual(4.25f, value.WorldX); Assert.AreEqual(-3.5f, value.WorldY);
            Assert.AreEqual(discovered, value.IsDiscovered);
        }

        [Test]
        public void UndiscoveredHiddenExpiryDoesNotLeakActivityHistory()
        {
            var world = new SimulationWorld();
            var instance = Instance("opportunity:1", WorldOpportunityDiscoveryMode.HiddenUntilDiscovered, false, 1, 1);
            world.WorldOpportunities.AddInstance(instance);
            Assert.IsFalse(world.WorldActivities.ResolveSource(WorldActivitySourceKind.WorldOpportunity,
                instance.InstanceId, 1));
            Assert.AreEqual(0, world.WorldActivities.Entries.Count);
        }

        [Test]
        public void ExplicitWeightZeroSpawnAppliesAllThreeVisibilityAndActivityRules()
        {
            AssertExplicitSpawn(WorldOpportunityDiscoveryMode.HiddenUntilDiscovered, false, false);
            AssertExplicitSpawn(WorldOpportunityDiscoveryMode.PublicNotice, true, true);
            AssertExplicitSpawn(WorldOpportunityDiscoveryMode.WorldVisible, true, false);
        }

        [Test]
        public void DiscoveredHiddenExpiryMovesKnownActivityToHistory()
        {
            var world = new SimulationWorld();
            var spec = Spec(WorldOpportunityDiscoveryMode.HiddenUntilDiscovered);
            Assert.IsTrue(world.WorldOpportunities.RegisterSpec(spec));
            var instance = Instance("opportunity:1", spec.DiscoveryMode, true, 1, 1);
            instance.ExpireDayIndexExclusive = 1;
            Assert.IsTrue(world.WorldOpportunities.AddInstance(instance));
            var activity = world.WorldActivities.CreateActive(WorldActivitySourceKind.WorldOpportunity,
                instance.InstanceId, "发现", "已经发现", 0);
            world.Tick = new WorldTick((ulong)WorldTick.TicksPerDay);
            new WorldOpportunityDriver().Tick(world);
            Assert.IsFalse(world.WorldOpportunities.TryGetInstance(instance.InstanceId, out _));
            Assert.AreEqual(WorldActivityState.History, activity.State);
        }

        [Test]
        public void NpcOpportunityRoundTripsAndExpiryKeepsExistingLifecycleSemantics()
        {
            var world = new SimulationWorld();
            RegisterGround(world);
            var npc = world.Entities.CreateCharacter(new DefinitionId("test", "npc"), "NPC").Value;
            world.WorldPresence.SetAtWorldPosition(npc.Id, new WorldVec2(3, 3), Surface);
            var instance = new WorldOpportunityInstance
            {
                InstanceId = world.WorldOpportunities.AllocateInstanceId(),
                OpportunityDefinitionId = "test:npc_opportunity", SurfaceId = Surface,
                SpawnKind = WorldOpportunitySpawnKind.Npc, SpawnedEntityId = npc.Id,
                CreatedDayIndex = 0, ExpireDayIndexExclusive = 1,
                DiscoveryMode = WorldOpportunityDiscoveryMode.WorldVisible, IsDiscovered = true
            };
            Assert.IsTrue(world.WorldOpportunities.AddInstance(instance));
            var service = new SnapshotService(new JsonSnapshotSerializer());
            var snapshot = service.CaptureJson(world, PlayableSimulationLoopFactory.Create(world));
            Assert.IsTrue(snapshot.IsSuccess, snapshot.IsFailure ? snapshot.Error.ToString() : string.Empty);
            var restored = service.RestoreJson(snapshot.Value);
            Assert.IsTrue(restored.IsSuccess, restored.IsFailure ? restored.Error.ToString() : string.Empty);
            Assert.IsTrue(restored.Value.world.WorldOpportunities.TryGetByEntity(npc.Id, out var restoredInstance));
            Assert.AreEqual(instance.InstanceId, restoredInstance.InstanceId);
            restored.Value.world.Tick = new WorldTick((ulong)WorldTick.TicksPerDay);
            new WorldOpportunityDriver().Tick(restored.Value.world);
            Assert.IsFalse(restored.Value.world.WorldOpportunities.TryGetByEntity(npc.Id, out _));
            Assert.IsTrue(restored.Value.world.Entities.TryGet(npc.Id, out var restoredNpc));
            Assert.AreEqual(LifecycleState.Removed, restoredNpc.Get<LifecycleComponent>().State);
        }

        [Test]
        public void BaseGameDynamicDiscoveryContentPassesReferenceValidation()
        {
#if UNITY_EDITOR
            var root = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));
#else
            var root = Environment.GetEnvironmentVariable("XIANXIA_BASEGAME") ??
                       Path.GetFullPath(Path.Combine("Content", "BaseGame"));
#endif
            var loaded = new ContentPackageLoader().Load(new[] { root });
            Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : string.Empty);
            var report = new ContentReferenceValidator().Validate(loaded.Value.Registry);
            Assert.IsTrue(report.IsValid, string.Join("\n", report.Errors));
        }

        static WorldOpportunitySpec Spec(string discoveryMode) => new WorldOpportunitySpec
        {
            Id = Definition, Name = "动态物体", SurfaceId = Surface,
            SpawnKind = WorldOpportunitySpawnKind.WorldObject, WorldObjectKind = "rock",
            WorldObjectLabel = "动态物体", WorldObjectWorldWidth = 1, WorldObjectWorldHeight = 1,
            DiscoveryMode = discoveryMode, Weight = 1, MaxActive = 1, DurationDays = 2
        };

        static WorldOpportunityInstance Instance(string id, string mode, bool discovered, float x, float y) =>
            new WorldOpportunityInstance
            {
                InstanceId = id, OpportunityDefinitionId = Definition, SurfaceId = Surface,
                SpawnKind = WorldOpportunitySpawnKind.WorldObject,
                WorldObjectInstanceId = WorldOpportunityBoard.WorldObjectIdFor(id),
                WorldX = x, WorldY = y, CreatedDayIndex = 0, ExpireDayIndexExclusive = 2,
                DiscoveryMode = mode, IsDiscovered = discovered
            };

        static ContentInteractionContext Context(WorldOpportunityInstance instance) => new ContentInteractionContext
        {
            TargetKind = "opportunityObject",
            TargetKey = "opportunityObject:" + instance.WorldObjectInstanceId,
            TargetDefinitionId = instance.OpportunityDefinitionId,
            TargetDisplayName = "动态物体"
        };

        static SimulationWorld WorldWithPartyAt(float x, float y)
        {
            var world = new SimulationWorld();
            var actor = world.Entities.CreateCharacter(new DefinitionId("test", "actor"), "Actor").Value;
            var party = new PlayerPartyRuntime(); party.BindWorld(world);
            Assert.IsTrue(party.TryInitialize(actor.Id, out var error), error);
            world.Strategic.PlayerPartyContext = party;
            world.PlayerPartyTravel.SetAtSurfacePosition(Surface, new WorldVec2(x, y));
            PlayerPartyTransitionMembership.CaptureTravelingMembersForPartyTransition(world, party);
            return world;
        }

        static void AssertExplicitSpawn(string mode, bool expectedDiscovered, bool expectedActivity)
        {
            var world = WorldWithPartyAt(5, 5);
            RegisterGround(world);
            var spec = Spec(mode);
            spec.Weight = 0;
            spec.DiscoveryRadiusWorld = 1.25f;
            spec.PublicNoticeTitle = "公开消息";
            spec.PublicNoticeText = "发现了公开物体。";
            Assert.IsTrue(world.WorldOpportunities.RegisterSpec(spec));
            var spawned = WorldOpportunityDriver.SpawnAcceptanceInstances(world, spec.Id, 1, out _);
            Assert.IsTrue(spawned.IsSuccess, spawned.IsFailure ? spawned.Error.ToString() : string.Empty);
            Assert.AreEqual(1, world.WorldOpportunities.ActiveInstances.Count);
            foreach (var instance in world.WorldOpportunities.ActiveInstances.Values)
            {
                Assert.AreEqual(expectedDiscovered, instance.IsDiscovered);
                Assert.AreEqual(expectedActivity,
                    world.WorldActivities.TryGetActiveBySource(WorldActivitySourceKind.WorldOpportunity,
                        instance.InstanceId, out _));
            }
        }

        static void RegisterGround(SimulationWorld world)
        {
            if (world.SurfaceGround.TryGet(Surface, out _)) return;
            var cells = new List<SurfaceGroundCellKind>();
            for (var i = 0; i < 100; i++) cells.Add(SurfaceGroundCellKind.Ground);
            world.SurfaceGround.Register(new SurfaceGroundNavigation(Surface, "rev", "hash",
                0, 0, 1, 10, 10, cells));
        }
    }
}
