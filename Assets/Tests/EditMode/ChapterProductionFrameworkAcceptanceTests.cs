using System.IO;
using NUnit.Framework;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Input;
using XianXia.Core.Persistence;
using XianXia.Core.Settlement;
using XianXia.Data.Bootstrap;

namespace XianXia.Tests
{
    /// <summary>
    /// Chapter Production Framework: chapter／day beats／story flags／content debug.
    /// </summary>
    public sealed class ChapterProductionFrameworkAcceptanceTests
    {
        static string BaseGamePath =>
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        [Test]
        public void ChapterFramework_Activate_DayBeats_StoryFlags_DebugJumpAndForceEvent()
        {
            var started = new PlayableDayBootstrap().Start(BaseGamePath);
            Assert.IsTrue(started.IsSuccess, started.IsFailure ? started.Error.ToString() : "");

            var world = started.Value.World;
            var loop = started.Value.Loop;
            var subject = started.Value.CharacterIds[0];
            var debug = new ContentDebugService();

            // Chapter／Scenario 结构已开局激活
            Assert.IsTrue(world.Chapters.HasActive);
            Assert.AreEqual("base:chapter_scaffold_01", world.Chapters.ActiveChapterId);
            Assert.IsTrue(world.Flags.Has("story:chapter_scaffold_started"));
            Assert.IsTrue(world.Quests.TryGet("base:quest_scout_herb_slope", out var scout));
            Assert.AreEqual(QuestStatus.Active, scout.Status);

            // Story Flag 写入＋历史
            Assert.IsTrue(debug.SetFlag(world, "story:debug_probe", subject).IsSuccess);
            Assert.IsTrue(StoryFlagService.Has(world, "story:debug_probe"));
            Assert.IsTrue(world.Flags.History.Count > 0);

            // 跳日 → day1 beat
            Assert.IsTrue(debug.AdvanceDays(loop, 1).IsSuccess);
            Assert.IsTrue(world.Flags.Has("story:chapter_scaffold_day1"));

            // 推完探坡任务 → day2 条件 beat
            Assert.IsTrue(started.Value.Port.Submit(new PlayerCommandRequest(
                subject, PlayerCommandKind.Travel, 1, EntityId.None, WorkRoleKind.None,
                "base:loc_village_edge")).IsSuccess);
            Assert.IsTrue(started.Value.Port.Submit(new PlayerCommandRequest(
                subject, PlayerCommandKind.Explore, 1)).IsSuccess);
            Assert.IsTrue(started.Value.Port.Submit(new PlayerCommandRequest(
                subject, PlayerCommandKind.Travel, 1, EntityId.None, WorkRoleKind.None,
                "base:loc_herb_slope")).IsSuccess);
            Assert.IsTrue(started.Value.Port.Submit(new PlayerCommandRequest(
                subject, PlayerCommandKind.Explore, 1)).IsSuccess);
            Assert.IsTrue(world.Flags.Has("quest:scout_herb_done"));

            Assert.IsTrue(debug.AdvanceDays(loop, 1).IsSuccess);
            Assert.IsTrue(world.Flags.Has("story:chapter_scaffold_ready_for_authoring"));

            // Debug：强制弹出事件＋Dump 含章节／Flag／角色
            if (world.ContentEvents.HasActive)
                world.ContentEvents.ClearActive();
            Assert.IsTrue(debug.ForcePresentEvent(world, subject, "base:event_herb_whisper").IsSuccess);
            Assert.IsTrue(world.ContentEvents.HasActive);

            var dump = debug.Dump(world, subject);
            StringAssert.Contains("chapter=base:chapter_scaffold_01", dump);
            StringAssert.Contains("story:chapter_scaffold_started", dump);
            StringAssert.Contains("subject=", dump);

            Assert.AreEqual(5, WorldSnapshot.CurrentSchemaVersion);
        }
    }
}
