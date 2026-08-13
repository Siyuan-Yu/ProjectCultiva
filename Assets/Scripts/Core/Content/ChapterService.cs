using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Events;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.Content
{
    public sealed class ChapterService
    {
        readonly QuestService _quests = new QuestService();
        readonly ContentEventService _events = new ContentEventService();

        public Result Activate(
            SimulationWorld world,
            string chapterId,
            EntityId subject,
            ulong? startDayIndex = null)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "World null.");
            if (!world.Chapters.TryGet(chapterId, out _))
                return Result.Failure(ErrorCode.NotFound, "Chapter missing.", chapterId);

            var day = startDayIndex ?? (world.Tick.Value / (ulong)WorldTick.TicksPerDay);
            world.Chapters.Activate(chapterId, day);
            world.Events.Publish(
                EventType.ChapterActivated,
                world.Tick,
                target: subject,
                payload: chapterId + ";day=" + day);

            ApplyQuestChain(world, subject);
            return ApplyDayBeat(world, subject, 0);
        }

        public Result OnChapterDay(
            SimulationWorld world,
            EntityId subject,
            ulong worldDayIndex)
        {
            if (world == null || !world.Chapters.TryGetActive(out var chapter))
                return Result.Success();

            if (worldDayIndex < world.Chapters.ChapterStartDayIndex)
                return Result.Success();

            var chapterDay = (int)(worldDayIndex - world.Chapters.ChapterStartDayIndex);
            ApplyQuestChain(world, subject);
            return ApplyDayBeat(world, subject, chapterDay);
        }

        public void ApplyQuestChain(SimulationWorld world, EntityId subject)
        {
            if (world == null || !world.Chapters.TryGetActive(out var chapter))
                return;

            for (var i = 0; i < chapter.QuestChainIds.Count; i++)
            {
                var questId = chapter.QuestChainIds[i];
                if (i > 0)
                {
                    var prev = chapter.QuestChainIds[i - 1];
                    if (!world.Quests.TryGet(prev, out var prevRt) ||
                        !QuestStatusUtil.IsObjectivesDone(prevRt.Status))
                        break;
                }

                _quests.TryStart(world, questId, subject);
            }
        }

        public Result ApplyDayBeat(SimulationWorld world, EntityId subject, int chapterDayIndex)
        {
            if (world == null || !world.Chapters.TryGetActive(out var chapter))
                return Result.Success();
            if (world.Chapters.HasAppliedBeat(chapter.Id, chapterDayIndex))
                return Result.Success();

            ChapterDayBeatSpec beat = null;
            for (var i = 0; i < chapter.DayBeats.Count; i++)
            {
                if (chapter.DayBeats[i].DayIndex == chapterDayIndex)
                {
                    beat = chapter.DayBeats[i];
                    break;
                }
            }

            if (beat == null)
                return Result.Success();

            if (!ContentConditionEvaluator.AllPass(world, subject, beat.Conditions))
                return Result.Success();

            for (var i = 0; i < beat.SetFlags.Count; i++)
                StoryFlagService.Set(world, beat.SetFlags[i], subject);

            for (var i = 0; i < beat.QuestOfferIds.Count; i++)
                _quests.TryStart(world, beat.QuestOfferIds[i], subject);

            for (var i = 0; i < beat.ContentEventIds.Count; i++)
                _events.TryPresentById(world, subject, beat.ContentEventIds[i], force: false);

            world.Chapters.MarkBeatApplied(chapter.Id, chapterDayIndex);
            world.Events.Publish(
                EventType.ChapterDayBeatApplied,
                world.Tick,
                target: subject,
                payload: chapter.Id + ";chapterDay=" + chapterDayIndex);

            ApplyQuestChain(world, subject);
            return _quests.Evaluate(world, subject);
        }
    }
}
