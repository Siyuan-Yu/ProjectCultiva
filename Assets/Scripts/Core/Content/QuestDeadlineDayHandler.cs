using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;

namespace XianXia.Core.Content
{
    /// <summary>Re-evaluate active quests on each DayStarted so deadline expiry moves them to Failed.</summary>
    public sealed class QuestDeadlineDayHandler : IDayBoundaryHandler
    {
        readonly QuestService _quests = new QuestService();

        public void OnDayStarted(SimulationWorld world, ulong startedDayIndex)
        {
            if (world == null)
                return;
            _quests.Evaluate(world, FindPrimarySubject(world));
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
