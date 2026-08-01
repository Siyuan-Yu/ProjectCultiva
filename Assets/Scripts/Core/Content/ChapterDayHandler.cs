using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;

namespace XianXia.Core.Content
{
    /// <summary>Applies active chapter day beats on DayStarted.</summary>
    public sealed class ChapterDayHandler : IDayBoundaryHandler
    {
        readonly ChapterService _chapters = new ChapterService();

        public void OnDayStarted(SimulationWorld world, ulong startedDayIndex)
        {
            if (world == null || !world.Chapters.HasActive)
                return;

            var subject = FindPrimarySubject(world);
            _chapters.OnChapterDay(world, subject, startedDayIndex);
        }

        public void OnDayEnded(SimulationWorld world, ulong endedDayIndex)
        {
        }

        static EntityId FindPrimarySubject(SimulationWorld world)
        {
            foreach (var e in world.Entities.All)
            {
                if ((e.Tags & EntityTag.Npc) == 0)
                    return e.Id;
            }

            return EntityId.None;
        }
    }
}
