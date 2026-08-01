using System.Text;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Results;
using XianXia.Core.Settlement;
using XianXia.Core.Simulation;
using XianXia.Core.Social;

namespace XianXia.Core.Content
{
    /// <summary>Authoring／QA helpers: jump time, flags, force events, state dump.</summary>
    public sealed class ContentDebugService
    {
        public const int MaxAdvanceDays = 30;

        readonly ChapterService _chapters = new ChapterService();
        readonly ContentEventService _events = new ContentEventService();
        readonly QuestService _quests = new QuestService();

        public Result AdvanceDays(SimulationLoop loop, int days)
        {
            if (loop == null)
                return Result.Failure(ErrorCode.InvalidArgument, "Loop null.");
            if (days <= 0)
                return Result.Success();
            if (days > MaxAdvanceDays)
                return Result.Failure(ErrorCode.InvalidArgument, "Advance days exceeds max.", days.ToString());

            var world = loop.World;
            var currentDay = world.Tick.Value / (ulong)WorldTick.TicksPerDay;
            var targetTick = (currentDay + (ulong)days) * (ulong)WorldTick.TicksPerDay;
            var guard = 0;
            var maxTicks = days * WorldTick.TicksPerDay + 8;
            while (world.Tick.Value < targetTick)
            {
                var r = loop.TickOnce();
                if (r.IsFailure)
                    return r;
                if (++guard > maxTicks)
                    return Result.Failure(ErrorCode.InvalidOperation, "AdvanceDays tick guard tripped.");
            }

            return Result.Success();
        }

        public Result JumpToDay(SimulationLoop loop, ulong targetDayIndex)
        {
            if (loop == null)
                return Result.Failure(ErrorCode.InvalidArgument, "Loop null.");
            var currentDay = loop.World.Tick.Value / (ulong)WorldTick.TicksPerDay;
            if (targetDayIndex <= currentDay)
                return Result.Success();
            var delta = (int)(targetDayIndex - currentDay);
            return AdvanceDays(loop, delta);
        }

        public Result SetFlag(SimulationWorld world, string flag, EntityId subject = default)
        {
            if (world == null || string.IsNullOrEmpty(flag))
                return Result.Failure(ErrorCode.InvalidArgument, "Flag invalid.");
            if (world.Flags.Has(flag))
                return Result.Success();
            StoryFlagService.Set(world, flag, subject);
            return Result.Success();
        }

        public Result ClearFlag(SimulationWorld world, string flag, EntityId subject = default)
        {
            if (world == null || string.IsNullOrEmpty(flag))
                return Result.Failure(ErrorCode.InvalidArgument, "Flag invalid.");
            if (!world.Flags.Has(flag))
                return Result.Success();
            StoryFlagService.Clear(world, flag, subject);
            return Result.Success();
        }

        public Result ForcePresentEvent(SimulationWorld world, EntityId subject, string eventId) =>
            _events.TryPresentById(world, subject, eventId, force: true);

        public Result TriggerExplore(SimulationWorld world, EntityId subject, string locationId) =>
            _events.TryTrigger(world, subject, "onExplore", locationId);

        public Result StartQuest(SimulationWorld world, EntityId subject, string questId) =>
            _quests.TryStart(world, questId, subject);

        public Result ActivateChapter(SimulationWorld world, EntityId subject, string chapterId) =>
            _chapters.Activate(world, chapterId, subject);

        public string Dump(SimulationWorld world, EntityId subject)
        {
            var sb = new StringBuilder(1024);
            if (world == null)
                return "world=null";

            var day = world.Tick.Value / (ulong)WorldTick.TicksPerDay;
            var tickInDay = (int)(world.Tick.Value % (ulong)WorldTick.TicksPerDay);
            sb.Append("tick=").Append(world.Tick.Value)
                .Append(" day=").Append(day)
                .Append(" tickInDay=").Append(tickInDay).Append('\n');

            if (world.Chapters.TryGetActive(out var chapter))
            {
                var chapterDay = (int)(day - world.Chapters.ChapterStartDayIndex);
                sb.Append("chapter=").Append(chapter.Id)
                    .Append(" name=").Append(chapter.Name)
                    .Append(" chapterDay=").Append(chapterDay)
                    .Append('/').Append(chapter.PlannedDays).Append('\n');
                sb.Append("questChain=");
                for (var i = 0; i < chapter.QuestChainIds.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(chapter.QuestChainIds[i]);
                }

                sb.Append('\n');
            }
            else
            {
                sb.Append("chapter=(none)\n");
            }

            sb.Append("flags=");
            var first = true;
            foreach (var f in world.Flags.All)
            {
                if (!first) sb.Append(',');
                sb.Append(f);
                first = false;
            }

            sb.Append('\n');
            sb.Append("flagHistory=");
            var hist = world.Flags.History;
            var start = hist.Count > 12 ? hist.Count - 12 : 0;
            for (var i = start; i < hist.Count; i++)
            {
                if (i > start) sb.Append('|');
                sb.Append(hist[i]);
            }

            sb.Append('\n');

            sb.Append("quests=");
            first = true;
            foreach (var kv in world.Quests.Runtime)
            {
                if (!first) sb.Append(';');
                sb.Append(kv.Key).Append('=').Append(kv.Value.Status);
                first = false;
            }

            sb.Append('\n');
            sb.Append("activeEvent=")
                .Append(world.ContentEvents.HasActive ? world.ContentEvents.ActiveEventId : "(none)")
                .Append('\n');

            if (!subject.IsNone && world.Entities.TryGet(subject, out var entity))
            {
                sb.Append("subject=").Append(entity.DisplayName).Append(' ').Append(subject).Append('\n');
                if (entity.TryGet<CultivationComponent>(out var cult))
                {
                    sb.Append("realm=").Append(cult.Realm)
                        .Append(" progress=").Append(cult.Progress)
                        .Append(" manual=").Append(cult.HasLearnedManual).Append('\n');
                }

                if (entity.TryGet<PersonalityProfileComponent>(out var profile))
                {
                    sb.Append("tags=");
                    var ti = 0;
                    foreach (var tag in profile.Tags)
                    {
                        if (ti++ > 0) sb.Append(',');
                        sb.Append(tag);
                    }

                    sb.Append('\n');
                }

                if (entity.TryGet<EntityLocationComponent>(out var loc))
                    sb.Append("location=").Append(loc.LocationId).Append('\n');
                if (entity.TryGet<WorkAssignmentComponent>(out var work))
                    sb.Append("work=").Append(work.Role).Append('@').Append(work.SettlementId).Append('\n');
            }

            if (world.Settlements.TryGetPrimary(out var settlement))
            {
                sb.Append("settlement=").Append(settlement.Name)
                    .Append(" wood=").Append(settlement.GetStock("base:resource_rough_wood"))
                    .Append(" herb=").Append(settlement.GetStock("base:resource_spirit_herb"))
                    .Append('\n');
            }

            return sb.ToString();
        }
    }
}
