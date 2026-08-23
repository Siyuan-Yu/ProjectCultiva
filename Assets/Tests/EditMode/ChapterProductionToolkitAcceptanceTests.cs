using System.IO;
using NUnit.Framework;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Input;
using XianXia.Core.Persistence;
using XianXia.Core.Settlement;
using XianXia.Data.Bootstrap;
using XianXia.Data.Content;

namespace XianXia.Tests
{
    /// <summary>
    /// Chapter Production Toolkit: templates load path, reference validation, Ch1 harness.
    /// </summary>
    public sealed class ChapterProductionToolkitAcceptanceTests
    {
        static string BaseGamePath =>
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        static string TemplatesPath =>
            Path.Combine(BaseGamePath, "Authoring", "Templates");

        [Test]
        public void Toolkit_TemplatesExist_AndBaseGameReferencesValid()
        {
            Assert.IsTrue(Directory.Exists(TemplatesPath), TemplatesPath);
            Assert.IsTrue(File.Exists(Path.Combine(TemplatesPath, "ch01_chapter.template.json")));
            Assert.IsTrue(File.Exists(Path.Combine(TemplatesPath, "ch01_quest_chain.template.json")));
            Assert.IsTrue(File.Exists(Path.Combine(TemplatesPath, "ch01_event_chain.template.json")));
            Assert.IsTrue(File.Exists(Path.Combine(TemplatesPath, "ch01_story_flags.template.json")));

            var loaded = new ContentPackageLoader().Load(new[] { BaseGamePath });
            Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : "");

            var report = new ContentReferenceValidator().Validate(loaded.Value.Registry);
            Assert.IsTrue(report.IsValid, report.IsValid ? "" : report.Errors[0].ToString());
            Assert.IsTrue(loaded.Value.Registry.TryGetChapter(
                new DefinitionId("base", "chapter_ch01_shell"), out _));
            Assert.IsTrue(loaded.Value.Registry.TryGetOpeningScenario(
                new DefinitionId("base", "scenario_chapter1_harness"), out _));
        }

        [Test]
        public void Toolkit_Chapter1Harness_QuestAndEventChain_WithDayAdvance()
        {
            var started = new PlayableDayBootstrap().Start(
                BaseGamePath,
                new PlayableDayOptions
                {
                    OpeningScenarioId = "base:scenario_chapter1_harness"
                });
            Assert.IsTrue(started.IsSuccess, started.IsFailure ? started.Error.ToString() : "");

            var world = started.Value.World;
            var port = started.Value.Port;
            var subject = started.Value.CharacterIds[0];
            var debug = new ContentDebugService();

            Assert.AreEqual("base:chapter_ch01_shell", world.Chapters.ActiveChapterId);
            Assert.IsTrue(world.Flags.Has("story:ch01_harness_started"));
            Assert.IsTrue(world.Quests.TryGet("base:quest_ch01_harness_arrive", out var arrive));
            Assert.AreEqual(QuestStatus.Active, arrive.Status);

            Assert.IsTrue(port.Submit(new PlayerCommandRequest(
                subject, PlayerCommandKind.Travel, 1, EntityId.None, WorkRoleKind.None,
                "base:loc_village_edge")).IsSuccess);
            Assert.IsTrue(port.Submit(new PlayerCommandRequest(
                subject, PlayerCommandKind.Explore, 1)).IsSuccess);

            Assert.AreEqual(QuestStatus.ReadyToClaim, arrive.Status);
            Assert.IsTrue(world.ContentEvents.HasActive);
            Assert.AreEqual("base:event_ch01_harness_ping", world.ContentEvents.ActiveEventId);
            Assert.IsTrue(port.Submit(new PlayerCommandRequest(
                subject, PlayerCommandKind.ClaimQuestRewards, 1, EntityId.None, WorkRoleKind.None,
                null, null, "base:quest_ch01_harness_arrive")).IsSuccess);
            Assert.AreEqual(QuestStatus.Completed, arrive.Status);

            Assert.IsTrue(port.Submit(new PlayerCommandRequest(
                subject, PlayerCommandKind.ResolveContentChoice, 1, EntityId.None, WorkRoleKind.None,
                null, "ack", null)).IsSuccess);
            Assert.IsTrue(world.Flags.Has("event:ch01_harness_resolved"));
            Assert.IsTrue(world.Quests.TryGet("base:quest_ch01_harness_follow", out var follow));
            Assert.AreEqual(QuestStatus.ReadyToClaim, follow.Status);
            Assert.IsTrue(port.Submit(new PlayerCommandRequest(
                subject, PlayerCommandKind.ClaimQuestRewards, 1, EntityId.None, WorkRoleKind.None,
                null, null, "base:quest_ch01_harness_follow")).IsSuccess);
            Assert.AreEqual(QuestStatus.Completed, follow.Status);
            Assert.IsTrue(world.Flags.Has("story:ch01_harness_chain_ok"));

            Assert.IsTrue(debug.AdvanceDays(started.Value.Loop, 1).IsSuccess);
            Assert.IsTrue(world.Flags.Has("story:ch01_harness_day1"));
            Assert.IsTrue(debug.AdvanceDays(started.Value.Loop, 1).IsSuccess);
            Assert.IsTrue(world.Flags.Has("story:ch01_harness_ready"));

            var dump = debug.Dump(world, subject);
            StringAssert.Contains("chapter=base:chapter_ch01_shell", dump);
            Assert.AreEqual(4, WorldSnapshot.CurrentSchemaVersion);
        }
    }
}
