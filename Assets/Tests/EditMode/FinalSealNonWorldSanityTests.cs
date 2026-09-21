using System.Collections.Generic;
using NUnit.Framework;
using XianXia.Core.Actions;
using XianXia.Core.Attributes;
using XianXia.Core.Combat;
using XianXia.Core.Content;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Inventory;
using XianXia.Core.Labor;
using XianXia.Core.Orders;
using XianXia.Core.Npc;
using XianXia.Core.Random;
using XianXia.Core.Schedule;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;
using XianXia.Core.World.Surface;

namespace XianXia.Tests
{
    public sealed class FinalSealNonWorldSanityTests
    {
        [Test]
        public void TryAddAllIsAtomicAndGroupedOutcomesRollbackEveryEarlyMutation()
        {
            var catalog = new InventoryCatalog();
            catalog.Register("a", "A", 1, null);
            catalog.Register("b", "B", 1, null);
            var bag = new PartyInventory(catalog, 1);
            Assert.IsFalse(bag.TryAddAll("a", 2));
            Assert.AreEqual(0, bag.GetCount("a"));

            var world = new SimulationWorld();
            world.Inventory.SetSlotCapacity(1);
            world.InventoryCatalog.Register("a", "A", 1, null);
            world.InventoryCatalog.Register("b", "B", 1, null);
            var outcomes = new[] {
                new ContentOutcome { Kind = "addStock", Id = "a", Amount = 1 },
                new ContentOutcome { Kind = "addStock", Id = "b", Amount = 1 }
            };
            Assert.IsTrue(ContentOutcomeApplier.ApplyAll(world, EntityId.None, outcomes).IsFailure);
            Assert.AreEqual(0, world.Inventory.GetCount("a"));
            Assert.AreEqual(0, world.Inventory.GetCount("b"));
            Assert.AreEqual(0, world.Events.Count);
        }

        [Test]
        public void OutcomeRollbackRestoresFlagsCountersRelationsAndEventQueue()
        {
            var world = new SimulationWorld();
            var from = CreateCharacter(world, "from");
            var to = CreateCharacter(world, "to");
            var outcomes = new[] {
                new ContentOutcome { Kind = "setFlag", Id = "early" },
                new ContentOutcome { Kind = "addCounter", Id = "wins", Amount = 2 },
                new ContentOutcome { Kind = "relationDelta", FromDefinitionId = "test:from", ToDefinitionId = "test:to", Amount = 7 },
                new ContentOutcome { Kind = "unknown" }
            };
            Assert.IsTrue(ContentOutcomeApplier.ApplyAll(world, from.Id, outcomes).IsFailure);
            Assert.IsFalse(world.Flags.Has("early"));
            Assert.AreEqual(0, world.ContentCounters.Get("wins"));
            Assert.AreEqual(0, world.Relationships.Score(from.Id, to.Id));
            Assert.AreEqual(0, world.Events.Count);
        }

        [Test]
        public void QuestClaimAndEventChoiceCommitStateOnceAndRemainRetryableOnFailure()
        {
            var world = new SimulationWorld();
            world.InventoryCatalog.Register("reward", "reward", 1, null);
            var quest = new QuestSpec { Id = "quest" };
            quest.Rewards.Add(new ContentOutcome { Kind = "addStock", Id = "reward", Amount = 1 });
            world.Quests.Register(quest);
            world.Quests.TryGet("quest", out var runtime);
            runtime.Status = QuestStatus.ReadyToClaim;
            var quests = new QuestService();
            Assert.IsTrue(quests.TryClaimRewards(world, "quest", EntityId.None).IsSuccess);
            Assert.AreEqual(QuestStatus.Completed, runtime.Status);
            Assert.AreEqual(1, world.Inventory.GetCount("reward"));
            Assert.IsTrue(quests.TryClaimRewards(world, "quest", EntityId.None).IsFailure);
            Assert.AreEqual(1, world.Inventory.GetCount("reward"));

            var evt = new ContentEventSpec { Id = "evt", Trigger = "manual", Once = true };
            var choice = new ContentEventChoiceSpec { Id = "choice" };
            choice.Outcomes.Add(new ContentOutcome { Kind = "setFlag", Id = "before-failure" });
            choice.Outcomes.Add(new ContentOutcome { Kind = "unknown" });
            evt.Choices.Add(choice);
            world.ContentEvents.Register(evt);
            world.ContentEvents.SetActive(evt.Id);
            var events = new ContentEventService();
            Assert.IsTrue(events.ResolveChoice(world, EntityId.None, choice.Id).IsFailure);
            Assert.IsTrue(world.ContentEvents.HasActive);
            Assert.IsFalse(world.ContentEvents.HasFired(evt.Id));
            Assert.IsFalse(world.Flags.Has("before-failure"));
            choice.Outcomes.RemoveAt(1);
            Assert.IsTrue(events.ResolveChoice(world, EntityId.None, choice.Id).IsSuccess);
            Assert.IsFalse(world.ContentEvents.HasActive);
            Assert.IsTrue(world.ContentEvents.HasFired(evt.Id));
            Assert.IsTrue(world.Flags.Has("before-failure"));
        }

        [Test]
        public void FourMasteryJudgmentsUseWorldRandomAndInvalidAttemptDoesNotAdvanceIt()
        {
            var rng = new CountingRandom(1.0);
            var world = new SimulationWorld(random: rng);
            var subject = CreateCharacter(world, "student");
            subject.Get<AttributesComponent>().SetBase(AttributeId.Comprehension, 0);
            var cult = subject.Get<CultivationComponent>();
            cult.Realm = RealmStage.QiRefining;
            var manual = new CultivationManualSpec {
                Id = new DefinitionId("test", "manual"), RequiredRealm = "炼气",
                CultivationSpeed = 2, BreakthroughProgress = 100,
                Mastery = Profile("cost")
            };
            var art = new CombatArtSpec {
                Id = new DefinitionId("test", "art"), DamageAttackMult = 1,
                Mastery = Profile("cost")
            };
            world.RegisterManual(manual);
            world.RegisterCombatArt(art);
            world.InventoryCatalog.Register("manual-item", "manual", 1, null, manual.Id.ToString());
            world.InventoryCatalog.Register("art-item", "art", 1, null, null, art.Id.ToString());
            world.InventoryCatalog.Register("cost", "cost", 99, null);
            Assert.IsTrue(world.Inventory.TryAddAll("manual-item", 1));
            Assert.IsTrue(world.Inventory.TryAddAll("art-item", 1));
            Assert.IsTrue(world.Inventory.TryAddAll("cost", 2));
            var service = new SkillMasteryService();

            service.EvaluateManualLearnChance(world, subject.Id, manual);
            Assert.AreEqual(0, rng.DoubleCalls);
            Assert.IsTrue(service.TryFinishManualStudy(world, subject.Id, "missing", manual, out _).IsFailure);
            Assert.AreEqual(0, rng.DoubleCalls);
            Assert.IsTrue(service.TryFinishManualStudy(world, subject.Id, "manual-item", manual, out var manualStudy).IsSuccess);
            Assert.IsFalse(manualStudy.Success);
            Assert.IsTrue(service.TryFinishArtStudy(world, subject.Id, "art-item", out var artStudy).IsSuccess);
            Assert.IsFalse(artStudy.Success);

            cult.LearnedManualId = manual.Id;
            cult.ManualMastery = AtBottleneck();
            var arts = subject.Get<CombatArtsComponent>();
            arts.TryLearn(art.Id);
            arts.SetMastery(art.Id, AtBottleneck());
            Assert.IsTrue(service.TryBreakthroughManual(world, subject.Id, out var manualBreak).IsSuccess);
            Assert.IsFalse(manualBreak.Success);
            Assert.IsTrue(service.TryBreakthroughArt(world, subject.Id, art.Id, out var artBreak).IsSuccess);
            Assert.IsFalse(artBreak.Success);
            Assert.AreEqual(4, rng.DoubleCalls);
            Assert.AreEqual(0, world.Inventory.GetCount("cost"), "valid failed breakthroughs still consume costs");
        }

        [Test]
        public void IncapacitationCancelsRunningAutonomousWorkReleasesSlotAndDropsQueuedWork()
        {
            var world = new SimulationWorld();
            var subject = CreateCharacter(world, "worker");
            subject.AddComponent(new DailyTaskComponent());
            world.RegisterWorkArea(new WorkAreaDefinition { Id = "area", LocationId = "location", Capacity = 1 });
            var loop = new SimulationLoop(world);
            var work = new Order(loop.AllocateOrderId(), subject.Id, OrderType.Work,
                OrderSource.Player, 5, targetRef: "area", activity: ScheduleActivity.Labor);
            Assert.IsTrue(loop.EnqueueOrder(work).IsSuccess);
            Assert.IsTrue(world.WorkAreaOccupancy.TryGet(subject.Id, out _, out _));
            world.GetOrCreateOrderQueue(subject.Id).Enqueue(new Order(
                loop.AllocateOrderId(), subject.Id, OrderType.Cultivate, OrderSource.Player, 5));
            Assert.IsTrue(CombatLifeStateService.TryEnterIncapacitated(world, subject));
            Assert.IsFalse(subject.Get<ActionStateComponent>().HasActiveAction);
            Assert.IsFalse(world.WorkAreaOccupancy.TryGet(subject.Id, out _, out _));
            Assert.AreEqual(0, world.GetOrCreateOrderQueue(subject.Id).Count);
            Assert.AreEqual(0, subject.Get<DailyTaskComponent>().CompletedAmount);
        }

        [Test]
        public void IncapacitatedScheduledNpcCannotPreclaimAWorkSlot()
        {
            var world = new SimulationWorld();
            world.RegisterWorkArea(new WorkAreaDefinition
            {
                Id = "area",
                LocationId = "location",
                Capacity = 1,
                AllowedActivities = { "Labor" }
            });
            world.RegisterSchedule(new ScheduleDefinition("schedule")
                .AddBlock(0, 100, ScheduleActivity.Labor, 8));
            var npc = world.Entities.CreateNpc(new DefinitionId("test", "npc"), "npc").Value;
            npc.AddComponent(new ScheduleComponent("schedule"));
            npc.AddComponent(new EntityLocationComponent { LocationId = "location" });
            npc.AddComponent(new JobComponent());
            npc.AddComponent(new ActivityTendencyComponent());
            npc.Get<ActivityTendencyComponent>().SetCapability(ScheduleActivity.Labor, true);
            npc.Get<LifecycleComponent>().State = LifecycleState.Incapacitated;

            Assert.IsTrue(new SimulationLoop(world).TickOnce().IsSuccess);
            Assert.IsFalse(world.WorkAreaOccupancy.TryGet(npc.Id, out _, out _));
            Assert.AreEqual(0, world.GetOrCreateOrderQueue(npc.Id).Count);
            Assert.IsFalse(npc.Get<ActionStateComponent>().HasActiveAction);
        }

        [Test]
        public void RestoredRunningActionIsCancelledBeforeFirstAdvanceButPassiveActionIsNotBlanketCancelled()
        {
            var world = new SimulationWorld();
            var subject = CreateCharacter(world, "restored");
            var action = new CultivateAction(new ActionId(77), subject.Id, new OrderId(9), 3);
            Assert.IsTrue(action.Start(world).IsSuccess);
            world.ActiveActions[action.Id] = action;
            subject.Get<ActionStateComponent>().ActiveActionId = action.Id;
            subject.Get<ActionStateComponent>().ActiveClock = action.Clock;
            subject.Get<LifecycleComponent>().State = LifecycleState.Incapacitated;
            Assert.IsTrue(new SimulationLoop(world).TickOnce().IsSuccess);
            Assert.AreEqual(0, subject.Get<CultivationComponent>().Progress);
            Assert.IsFalse(subject.Get<ActionStateComponent>().HasActiveAction);

            var passive = new ApplyModifierAction(new ActionId(78), subject.Id, new OrderId(10),
                AttributeId.Attack, ModifierOperation.Fixed, 1,
                new SourceRef(SourceKind.Event, new DefinitionId("test", "passive"), subject.Id));
            world.ActiveActions[passive.Id] = passive;
            subject.Get<ActionStateComponent>().ActiveActionId = passive.Id;
            Assert.IsFalse(AutonomousActionContinuationService.CancelInvalid(world, subject.Id));
            Assert.IsTrue(world.ActiveActions.ContainsKey(passive.Id));
        }

        [Test]
        public void NormalOutdoorAuthorizationUsesExactActualControlNotStaleSiteContext()
        {
            var world = new SimulationWorld();
            world.Strategic.PlayerFactionId = "player";
            world.SurfaceGround.Register(Surface("surface", 20, 10));
            var site = new WorldSite {
                SiteId = "owned", OwnerFactionId = "player", IsCoreActive = true,
                UsesContinuousOutdoorSurface = true, CoreAssetId = "core", CoreSurfaceId = "surface",
                HasCoreWorldPosition = true, CoreWorldX = 3, CoreWorldY = 3,
                CoreLevel = 1, CoreRangeWidth = 4, CoreRangeHeight = 4
            };
            world.Strategic.Sites.Register(site);
            Assert.IsTrue(TerritoryClaimService.CreateInitialClaim(world, site).IsSuccess);
            world.PlayerPartyTravel.SetAtSurfacePosition("surface", new WorldVec2(12, 3), new HexCoord(0, 0));
            world.PlayerPartyTravel.SetCurrentOutdoorWorldSiteContext("owned");
            Assert.IsFalse(PlayerStrategicResourceService.TryResolveCurrentManagingSite(world, out _));
            world.PlayerPartyTravel.SetAtSurfacePosition("surface", new WorldVec2(3, 3), new HexCoord(0, 0));
            Assert.IsTrue(PlayerStrategicResourceService.TryResolveCurrentManagingSite(world, out var resolved));
            Assert.AreEqual("owned", resolved.SiteId);
            site.OwnerFactionId = "enemy";
            Assert.IsFalse(PlayerStrategicResourceService.TryResolveCurrentManagingSite(world, out _));
        }

        static Entity CreateCharacter(SimulationWorld world, string id)
        {
            var result = world.Entities.CreateCharacter(new DefinitionId("test", id), id);
            Assert.IsTrue(result.IsSuccess);
            return result.Value;
        }

        static SkillMasteryProfile Profile(string cost) => new SkillMasteryProfile {
            Breakthroughs = { new SkillMasteryBreakthroughSpec {
                From = SkillMasteryTier.Entry, To = SkillMasteryTier.Minor, ProgressRequired = 1,
                Costs = { new SkillMasteryCostSpec { ItemId = cost, Count = 1 } }
            } }
        };

        static SkillMasteryState AtBottleneck() => new SkillMasteryState {
            Tier = SkillMasteryTier.Entry, Progress = 1, ProgressRequired = 1
        };

        static SurfaceGroundNavigation Surface(string id, int width, int height)
        {
            var cells = new List<SurfaceGroundCellKind>();
            for (var i = 0; i < width * height; i++) cells.Add(SurfaceGroundCellKind.Ground);
            return new SurfaceGroundNavigation(id, "rev", id, 0, 0, 1, width, height, cells);
        }

        sealed class CountingRandom : IRandomSource
        {
            readonly double _value;
            public CountingRandom(double value) { _value = value; }
            public int DoubleCalls { get; private set; }
            public RandomStreamId StreamId => RandomStreamId.World;
            public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
            public double NextDouble() { DoubleCalls++; return _value; }
            public RandomState CaptureState() => new RandomState((ulong)(DoubleCalls + 1), 1, StreamId);
            public void RestoreState(RandomState state) { DoubleCalls = (int)state.S0 - 1; }
        }
    }
}
