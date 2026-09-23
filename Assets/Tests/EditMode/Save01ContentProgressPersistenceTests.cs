using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Persistence;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Data.Serialization;
using XianXia.Data.Content;
using XianXia.Data.Bootstrap;

namespace XianXia.Tests
{
    public sealed class Save01ContentProgressPersistenceTests
    {
        const string SpiritHerbId = "base:resource_spirit_herb";

        [Test]
        public void RemoveStockSucceedsAndPublishesInventoryChange()
        {
            var world = new SimulationWorld();
            world.InventoryCatalog.Register(SpiritHerbId, "灵药", 99, null);
            Assert.IsTrue(world.Inventory.TryAddAll(SpiritHerbId, 2));

            var result = ContentOutcomeApplier.Apply(world, EntityId.None,
                new ContentOutcome { Kind = "removeStock", Id = SpiritHerbId, Amount = 1 });

            Assert.IsTrue(result.IsSuccess, result.IsFailure ? result.Error.ToString() : string.Empty);
            Assert.AreEqual(1, world.Inventory.GetCount(SpiritHerbId));
            Assert.AreEqual(1, world.Events.Count);
        }

        [Test]
        public void RemoveStockInsufficientFailsWithoutChangingInventory()
        {
            var world = new SimulationWorld();
            world.InventoryCatalog.Register(SpiritHerbId, "灵药", 99, null);

            var result = ContentOutcomeApplier.Apply(world, EntityId.None,
                new ContentOutcome { Kind = "removeStock", Id = SpiritHerbId, Amount = 1 });

            Assert.IsTrue(result.IsFailure);
            Assert.AreEqual(0, world.Inventory.GetCount(SpiritHerbId));
            Assert.AreEqual(0, world.Events.Count);
        }

        [Test]
        public void RemoveStockRollsBackWhenLaterOutcomeFails()
        {
            var world = new SimulationWorld();
            world.InventoryCatalog.Register(SpiritHerbId, "灵药", 99, null);
            Assert.IsTrue(world.Inventory.TryAddAll(SpiritHerbId, 1));

            var result = ContentOutcomeApplier.ApplyAll(world, EntityId.None, new[]
            {
                new ContentOutcome { Kind = "removeStock", Id = SpiritHerbId, Amount = 1 },
                new ContentOutcome { Kind = "unknown" }
            });

            Assert.IsTrue(result.IsFailure);
            Assert.AreEqual(1, world.Inventory.GetCount(SpiritHerbId));
            Assert.AreEqual(0, world.Events.Count);
        }

        [Test]
        public void ActiveReadyCompletedAndFailedQuestsRoundTripWithoutApplyingRewards()
        {
            var world = new SimulationWorld();
            var quests = new QuestService();

            var activeSpec = CounterQuest("test:active", "test:active_counter", 5, deadlineDays: 2);
            world.Quests.Register(activeSpec);
            Assert.IsTrue(quests.TryStart(world, activeSpec.Id, EntityId.None).IsSuccess);
            world.ContentCounters.Set("test:active_counter", 2);
            quests.Evaluate(world, EntityId.None);

            var readySpec = CounterQuest("test:ready", "test:ready_counter", 1);
            readySpec.Rewards.Add(new ContentOutcome { Kind = "setFlag", Id = "test:reward_applied" });
            world.Quests.Register(readySpec);
            Assert.IsTrue(quests.TryStart(world, readySpec.Id, EntityId.None).IsSuccess);
            world.ContentCounters.Set("test:ready_counter", 1);
            quests.Evaluate(world, EntityId.None);

            var completedSpec = CounterQuest("test:completed", "test:completed_counter", 1);
            world.Quests.Register(completedSpec);
            Assert.IsTrue(quests.TryStart(world, completedSpec.Id, EntityId.None).IsSuccess);
            world.ContentCounters.Set("test:completed_counter", 1);
            quests.Evaluate(world, EntityId.None);
            Assert.IsTrue(quests.TryClaimRewards(world, completedSpec.Id, EntityId.None).IsSuccess);

            var failedSpec = CounterQuest("test:failed", "test:never", 1);
            failedSpec.FailConditions.Add(new ContentCondition { Kind = "hasFlag", Id = "test:fail_now" });
            world.Quests.Register(failedSpec);
            Assert.IsTrue(quests.TryStart(world, failedSpec.Id, EntityId.None).IsSuccess);
            world.Flags.Set("test:fail_now");
            quests.Evaluate(world, EntityId.None);

            var restored = RoundTrip(world).world;
            AssertQuest(restored, activeSpec.Id, QuestStatus.Active, 2, 5, 0, 2);
            AssertQuest(restored, readySpec.Id, QuestStatus.ReadyToClaim, 1, 1, 0, 0);
            AssertQuest(restored, completedSpec.Id, QuestStatus.Completed, 1, 1, 0, 0);
            AssertQuest(restored, failedSpec.Id, QuestStatus.Failed, 0, 1, 0, 0);
            Assert.IsFalse(restored.Flags.Has("test:reward_applied"), "Restore must not claim a ReadyToClaim quest.");
        }

        [Test]
        public void FlagsHistoryCountersDailyAndLocationLaborRoundTrip()
        {
            var world = new SimulationWorld();
            world.Flags.Set("story:a");
            world.Flags.RecordHistory("first");
            world.Flags.RecordHistory("second");
            world.ContentCounters.Set("counter:a", 7);
            world.Tick = new WorldTick((ulong)WorldTick.TicksPerDay * 4 + 12);
            world.ContentDaily.MarkToday("daily:a", world.Tick);
            world.LocationLabor.Add("base:character_a", "base:location_a", 9);
            world.LocationLabor.AddHarvest("base:character_a", "base:location_a", 2);

            var restored = RoundTrip(world).world;
            Assert.IsTrue(restored.Flags.Has("story:a"));
            CollectionAssert.AreEqual(new[] { "first", "second" }, restored.Flags.History);
            Assert.AreEqual(7, restored.ContentCounters.Get("counter:a"));
            Assert.IsTrue(restored.ContentDaily.IsMarkedToday("daily:a", restored.Tick));
            Assert.AreEqual(9, restored.LocationLabor.GetTicks("base:character_a", "base:location_a"));
            Assert.AreEqual(2, restored.LocationLabor.GetHarvests("base:character_a", "base:location_a"));
        }

        [Test]
        public void GlobalPerTargetAndPerActorTargetFiredKeysRoundTripAndRemainIneligible()
        {
            var world = new SimulationWorld();
            var actor = world.Entities.CreateCharacter(new DefinitionId("test", "actor"), "actor").Value.Id;
            var target = world.Entities.CreateCharacter(new DefinitionId("test", "target"), "target").Value.Id;
            var specs = new[]
            {
                OnceEvent("test:global", "global"),
                OnceEvent("test:target_once", "perTarget"),
                OnceEvent("test:pair_once", "perActorTarget")
            };
            foreach (var spec in specs)
            {
                world.ContentEvents.Register(spec);
                world.ContentEvents.MarkFired(world.ContentEvents.FiredKey(spec, actor, target));
            }

            var restored = RoundTrip(world).world;
            foreach (var spec in specs)
            {
                restored.ContentEvents.Register(spec);
                Assert.IsTrue(restored.ContentEvents.HasFired(restored.ContentEvents.FiredKey(spec, actor, target)));
            }
            var candidates = new ContentEventService().ResolveInteractionCandidates(
                restored, actor, target, "onTalk", "test:target");
            Assert.AreEqual(0, candidates.Count);
            Assert.IsFalse(restored.ContentEvents.HasActive);
        }

        [Test]
        public void ChapterRuntimeRoundTripsAndAppliedBeatDoesNotReplay()
        {
            var world = new SimulationWorld();
            world.Chapters.Activate("test:chapter", 3);
            world.Chapters.MarkBeatApplied("test:chapter", 1);

            var restored = RoundTrip(world).world;
            var chapter = new ChapterSpec { Id = "test:chapter" };
            var beat = new ChapterDayBeatSpec { DayIndex = 1 };
            beat.SetFlags.Add("test:beat_replayed");
            chapter.DayBeats.Add(beat);
            restored.Chapters.Register(chapter);

            Assert.AreEqual("test:chapter", restored.Chapters.ActiveChapterId);
            Assert.AreEqual(3UL, restored.Chapters.ChapterStartDayIndex);
            Assert.IsTrue(restored.Chapters.HasAppliedBeat("test:chapter", 1));
            new ChapterService().OnChapterDay(restored, EntityId.None, 4);
            Assert.IsFalse(restored.Flags.Has("test:beat_replayed"));
        }

        [Test]
        public void ActiveDialogueBlocksSaveAndCompletedDialogueAllowsSave()
        {
            var world = new SimulationWorld();
            var loop = PlayableSimulationLoopFactory.Create(world);
            var service = new SnapshotService(new JsonSnapshotSerializer());
            world.ContentEvents.SetActive("test:event");

            var blocked = service.CaptureJson(world, loop);
            Assert.IsTrue(blocked.IsFailure);
            Assert.AreEqual(ErrorCode.InvalidOperation, blocked.Error.Code);

            world.ContentEvents.ClearActive();
            Assert.IsTrue(service.CaptureJson(world, loop).IsSuccess);
        }

        [Test]
        public void LoadedPlayableLoopRunsQuestDeadlineAndChapterHandlersOnce()
        {
            var world = new SimulationWorld();
            world.Tick = new WorldTick((ulong)WorldTick.TicksPerDay - 1);
            var deadline = CounterQuest("test:deadline", "test:never", 1, deadlineDays: 1);
            deadline.FailResults.Add(new ContentOutcome { Kind = "addCounter", Id = "test:deadline_fail_count", Amount = 1 });
            world.Quests.Register(deadline);
            Assert.IsTrue(new QuestService().TryStart(world, deadline.Id, EntityId.None).IsSuccess);

            var chapter = new ChapterSpec { Id = "test:chapter_next_day" };
            var beat = new ChapterDayBeatSpec { DayIndex = 1 };
            beat.SetFlags.Add("test:chapter_day_one");
            chapter.DayBeats.Add(beat);
            world.Chapters.Register(chapter);
            world.Chapters.Activate(chapter.Id, 0);

            var restored = RoundTrip(world);
            restored.world.Quests.Register(deadline);
            restored.world.Chapters.Register(chapter);
            Assert.IsTrue(restored.loop.TickOnce().IsSuccess);

            AssertQuest(restored.world, deadline.Id, QuestStatus.Failed, 0, 1, 0, 1);
            Assert.AreEqual(1, restored.world.ContentCounters.Get("test:deadline_fail_count"));
            Assert.IsTrue(restored.world.Flags.Has("test:chapter_day_one"));
            Assert.IsTrue(restored.world.Chapters.HasAppliedBeat(chapter.Id, 1));
        }

        [Test]
        public void LegacyV6AndMissingV7AuthorityAreRejected()
        {
            var service = new SnapshotService(new JsonSnapshotSerializer());
            var legacy = service.Restore(new WorldSnapshot { SchemaVersion = 6 });
            Assert.IsTrue(legacy.IsFailure);
            Assert.AreEqual(ErrorCode.SnapshotVersionMismatch, legacy.Error.Code);

            var world = new SimulationWorld();
            var snapshot = service.Capture(world, PlayableSimulationLoopFactory.Create(world));
            snapshot.ContentProgress = null;
            var missing = service.Restore(snapshot);
            Assert.IsTrue(missing.IsFailure);
            Assert.AreEqual(ErrorCode.SnapshotInvalid, missing.Error.Code);
        }

        [Test]
        public void CorruptContentProgressAndMissingDefinitionsAreRejected()
        {
            var world = new SimulationWorld();
            var dto = ContentProgressSnapshotHelper.Capture(world);
            dto.Counters.Add(new ContentIntEntrySnapshotDto { Key = "duplicate", Value = 1 });
            dto.Counters.Add(new ContentIntEntrySnapshotDto { Key = "duplicate", Value = 2 });
            Assert.AreEqual(ErrorCode.SnapshotInvalid,
                ContentProgressSnapshotHelper.Restore(new SimulationWorld(), dto).Error.Code);

            var restored = new SimulationWorld();
            var missingDefinitions = new ContentProgressSnapshotDto { HasAuthority = true };
            missingDefinitions.Quests.Add(new QuestRuntimeSnapshotDto { QuestId = "test:missing", Status = (int)QuestStatus.Active });
            Assert.IsTrue(ContentProgressSnapshotHelper.Restore(restored, missingDefinitions).IsSuccess);
            Assert.AreEqual(ErrorCode.SnapshotInvalid,
                ContentProgressSnapshotHelper.ValidateDefinitions(restored).Error.Code);
        }

        [Test]
        public void BaseGameOpportunityContentWiresBothEventsToHandInQuests()
        {
            var root = Environment.GetEnvironmentVariable("XIANXIA_BASEGAME") ??
                       Path.GetFullPath(Path.Combine("Content", "BaseGame"));
            var loaded = new ContentPackageLoader().Load(new[] { root });
            Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : string.Empty);
            var registry = loaded.Value.Registry;

            Assert.IsTrue(registry.TryGetQuest(
                new DefinitionId("base", "quest_event02_wounded_cultivator_herb"), out var quest));
            Assert.AreEqual(2, quest.DeadlineDays);
            Assert.IsTrue(quest.Abandonable);
            Assert.IsTrue(quest.CompleteConditions.Any(c => c.Kind == "hasFlag" &&
                c.Id == "quest:event02_wounded_cultivator_herb_handed_in"));
            Assert.IsTrue(quest.Description.Contains("任意来源") && quest.Description.Contains("交给受伤散修"));
            Assert.IsTrue(quest.Rewards.Any(o => o.Kind == "grantProgress" && o.Amount == 3));
            Assert.IsTrue(quest.Rewards.Any(o => o.Kind == "setFlag" &&
                o.Id == "quest:event02_wounded_cultivator_herb_done"));

            Assert.IsTrue(registry.TryGetContentEvent(
                new DefinitionId("base", "event_event02_wounded_cultivator_talk"), out var talk));
            var accept = talk.Steps.SelectMany(step => step.Choices).Single(choice => choice.Id == "accept");
            Assert.IsTrue(accept.Outcomes.Any(o => o.Kind == "setFlag" &&
                o.Id == "story:event02_wounded_cultivator_herb_accepted"));
            Assert.IsTrue(accept.Outcomes.Any(o => o.Kind == "startQuest" &&
                o.Id == "base:quest_event02_wounded_cultivator_herb"));

            Assert.IsTrue(registry.TryGetContentEvent(
                new DefinitionId("base", "event_event02_wounded_cultivator_herb_active"), out var reminder));
            Assert.AreEqual(60, reminder.Priority);
            Assert.IsTrue(reminder.Conditions.Any(c => c.Kind == "questActive" && c.Id == quest.Id.ToString()));
            var woundedHandIn = reminder.Steps.SelectMany(step => step.Choices)
                .Single(choice => choice.Id == "hand_in");
            Assert.IsTrue(woundedHandIn.Conditions.Any(c => c.Kind == "stockAtLeast" &&
                c.Id == SpiritHerbId && c.Amount == 1));
            Assert.IsTrue(woundedHandIn.Outcomes.Any(o => o.Kind == "removeStock" &&
                o.Id == SpiritHerbId && o.Amount == 1));
            Assert.IsTrue(woundedHandIn.Outcomes.Any(o => o.Kind == "setFlag" &&
                o.Id == "quest:event02_wounded_cultivator_herb_handed_in"));
            Assert.AreEqual("disabled", woundedHandIn.UnavailableMode);
            Assert.AreEqual("需要灵药 ×1", woundedHandIn.RequirementText);
            Assert.IsTrue(registry.TryGetWorldOpportunity(
                new DefinitionId("base", "world_opportunity_event02_wounded_cultivator"), out var opportunity));
            Assert.AreEqual(3, opportunity.DurationDays);

            Assert.IsTrue(registry.TryGetQuest(
                new DefinitionId("base", "quest_event02_temporary_merchant_herb"), out var merchantQuest));
            Assert.AreEqual("临时行商·代采灵药", merchantQuest.Name);
            Assert.AreEqual(2, merchantQuest.DeadlineDays);
            Assert.IsTrue(merchantQuest.Abandonable);
            Assert.IsTrue(merchantQuest.CompleteConditions.Any(c => c.Kind == "hasFlag" &&
                c.Id == "quest:event02_temporary_merchant_herb_handed_in"));
            Assert.IsTrue(merchantQuest.Description.Contains("任意来源") && merchantQuest.Description.Contains("交给临时行商"));
            Assert.IsTrue(merchantQuest.Rewards.Any(o => o.Kind == "grantProgress" && o.Amount == 2));
            Assert.IsTrue(merchantQuest.Rewards.Any(o => o.Kind == "setFlag" &&
                o.Id == "quest:event02_temporary_merchant_herb_done"));
            Assert.IsTrue(registry.WorkAreas.Values.Any(area =>
                area.LocationId == "base:loc_ref_herb_field"));

            Assert.IsTrue(registry.TryGetContentEvent(
                new DefinitionId("base", "event_event02_temporary_merchant_talk"), out var merchantTalk));
            Assert.AreEqual(50, merchantTalk.Priority);
            Assert.IsFalse(merchantTalk.Once);
            Assert.IsTrue(merchantTalk.Conditions.Any(c => c.Kind == "missingFlag" &&
                c.Id == "story:event02_temporary_merchant_herb_accepted"));
            var merchantAccept = merchantTalk.Steps.SelectMany(step => step.Choices)
                .Single(choice => choice.Id == "accept");
            Assert.IsTrue(merchantAccept.Outcomes.Any(o => o.Kind == "setFlag" &&
                o.Id == "story:event02_temporary_merchant_herb_accepted"));
            Assert.IsTrue(merchantAccept.Outcomes.Any(o => o.Kind == "startQuest" &&
                o.Id == "base:quest_event02_temporary_merchant_herb"));
            var merchantLeave = merchantTalk.Steps.SelectMany(step => step.Choices)
                .Single(choice => choice.Id == "leave");
            Assert.AreEqual(0, merchantLeave.Outcomes.Count, "Refusing must not set the accepted flag or start the quest.");

            Assert.IsTrue(registry.TryGetContentEvent(
                new DefinitionId("base", "event_event02_temporary_merchant_herb_active"), out var merchantReminder));
            Assert.AreEqual(60, merchantReminder.Priority);
            Assert.IsFalse(merchantReminder.Once);
            Assert.IsTrue(merchantReminder.Conditions.Any(c => c.Kind == "questActive" &&
                c.Id == merchantQuest.Id.ToString()));
            var merchantHandIn = merchantReminder.Steps.SelectMany(step => step.Choices)
                .Single(choice => choice.Id == "hand_in");
            Assert.IsTrue(merchantHandIn.Conditions.Any(c => c.Kind == "stockAtLeast" &&
                c.Id == SpiritHerbId && c.Amount == 1));
            Assert.IsTrue(merchantHandIn.Outcomes.Any(o => o.Kind == "removeStock" &&
                o.Id == SpiritHerbId && o.Amount == 1));
            Assert.IsTrue(merchantHandIn.Outcomes.Any(o => o.Kind == "setFlag" &&
                o.Id == "quest:event02_temporary_merchant_herb_handed_in"));
            Assert.AreEqual("disabled", merchantHandIn.UnavailableMode);
            Assert.AreEqual("需要灵药 ×1", merchantHandIn.RequirementText);
            Assert.IsFalse(merchantReminder.Steps.SelectMany(step => step.Outcomes)
                .Concat(merchantReminder.Steps.SelectMany(step => step.Choices).SelectMany(choice => choice.Outcomes))
                .Any(o => o.Kind == "startQuest"));
            Assert.IsTrue(registry.TryGetWorldOpportunity(
                new DefinitionId("base", "world_opportunity_event02_temporary_merchant"), out var merchantOpportunity));
            Assert.AreEqual(3, merchantOpportunity.DurationDays);

            var runtimeWorld = new SimulationWorld();
            Assert.IsTrue(ContentRuntimeBootstrap.RehydrateContentDefinitions(runtimeWorld, registry).IsSuccess);
            var events = new ContentEventService();
            runtimeWorld.ContentEvents.SetActive(merchantTalk.Id.ToString());
            Assert.IsTrue(events.ResolveChoice(runtimeWorld, EntityId.None, "leave").IsSuccess);
            Assert.IsFalse(runtimeWorld.Flags.Has("story:event02_temporary_merchant_herb_accepted"));
            Assert.IsTrue(runtimeWorld.Quests.TryGet(merchantQuest.Id.ToString(), out var declinedQuest));
            Assert.AreEqual(QuestStatus.Inactive, declinedQuest.Status);

            runtimeWorld.ContentEvents.SetActive(merchantTalk.Id.ToString());
            Assert.IsTrue(events.ResolveChoice(runtimeWorld, EntityId.None, "accept").IsSuccess);
            Assert.IsTrue(runtimeWorld.Flags.Has("story:event02_temporary_merchant_herb_accepted"));
            Assert.IsTrue(runtimeWorld.Quests.TryGet(merchantQuest.Id.ToString(), out var acceptedQuest));
            Assert.AreEqual(QuestStatus.Active, acceptedQuest.Status);
            var journal = new System.Collections.Generic.List<QuestListEntry>();
            QuestJournalQuery.Collect(runtimeWorld, EntityId.None, journal);
            var merchantEntry = journal.Single(entry => entry.QuestId == merchantQuest.Id.ToString());
            Assert.IsTrue(merchantEntry.ObjectivesSummary.Contains("交给临时行商"));
            Assert.IsFalse(merchantEntry.ObjectivesSummary.Contains("quest:event02"));

            runtimeWorld.InventoryCatalog.Register(SpiritHerbId, "灵药", 99, null);
            Assert.IsTrue(runtimeWorld.Inventory.TryAddAll(SpiritHerbId, 1),
                "Any legal acquisition path only needs to add the herb to PartyInventory.");
            new QuestService().Evaluate(runtimeWorld, EntityId.None);
            Assert.AreEqual(QuestStatus.Active, acceptedQuest.Status,
                "Owning the herb must not complete the quest before hand-in.");
            Assert.IsTrue(ContentConditionEvaluator.AllPass(runtimeWorld, EntityId.None, merchantHandIn.Conditions));

            runtimeWorld.ContentEvents.SetActive(merchantReminder.Id.ToString());
            Assert.IsTrue(events.ResolveChoice(runtimeWorld, EntityId.None, "hand_in").IsSuccess);
            Assert.AreEqual(0, runtimeWorld.Inventory.GetCount(SpiritHerbId));
            Assert.IsTrue(runtimeWorld.Flags.Has("quest:event02_temporary_merchant_herb_handed_in"));
            Assert.AreEqual(QuestStatus.Active, acceptedQuest.Status,
                "The quest evaluates when the hand-in dialogue finishes.");
            Assert.IsTrue(events.ResolveChoice(runtimeWorld, EntityId.None, string.Empty).IsSuccess);
            Assert.IsTrue(events.ResolveChoice(runtimeWorld, EntityId.None, string.Empty).IsSuccess);
            Assert.AreEqual(QuestStatus.ReadyToClaim, acceptedQuest.Status);

            var restored = RoundTrip(runtimeWorld).world;
            Assert.AreEqual(0, restored.Inventory.GetCount(SpiritHerbId));
            Assert.IsTrue(restored.Flags.Has("quest:event02_temporary_merchant_herb_handed_in"));
            Assert.IsTrue(restored.Quests.TryGet(merchantQuest.Id.ToString(), out var restoredQuest));
            Assert.AreEqual(QuestStatus.ReadyToClaim, restoredQuest.Status);
        }

        static QuestSpec CounterQuest(string id, string counter, int amount, int deadlineDays = 0)
        {
            var spec = new QuestSpec { Id = id, DeadlineDays = deadlineDays };
            spec.CompleteConditions.Add(new ContentCondition { Kind = "counterAtLeast", Id = counter, Amount = amount });
            return spec;
        }

        static ContentEventSpec OnceEvent(string id, string scope) => new ContentEventSpec
        {
            Id = id,
            Trigger = "onTalk",
            Once = true,
            OnceScope = scope,
            Body = "test"
        };

        static void AssertQuest(SimulationWorld world, string id, QuestStatus status, int progress, int max,
            ulong acceptedDay, ulong deadlineDay)
        {
            Assert.IsTrue(world.Quests.TryGet(id, out var runtime), id);
            Assert.AreEqual(status, runtime.Status, id);
            Assert.AreEqual(progress, runtime.ProgressCount, id);
            Assert.AreEqual(max, runtime.ProgressMax, id);
            Assert.AreEqual(acceptedDay, runtime.AcceptedAtDayIndex, id);
            Assert.AreEqual(deadlineDay, runtime.DeadlineDayIndexExclusive, id);
        }

        static (SimulationWorld world, SimulationLoop loop) RoundTrip(SimulationWorld world)
        {
            var service = new SnapshotService(new JsonSnapshotSerializer());
            var captured = service.CaptureJson(world, PlayableSimulationLoopFactory.Create(world));
            Assert.IsTrue(captured.IsSuccess, captured.IsFailure ? captured.Error.ToString() : string.Empty);
            var restored = service.RestoreJson(captured.Value);
            Assert.IsTrue(restored.IsSuccess, restored.IsFailure ? restored.Error.ToString() : string.Empty);
            return restored.Value;
        }
    }
}
