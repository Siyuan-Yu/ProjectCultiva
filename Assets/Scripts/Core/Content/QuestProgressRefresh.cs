using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;

namespace XianXia.Core.Content
{
    /// <summary>
    /// 背包／地点劳作变化后刷新任务进度与完成判定（避免 ProgressCount 滞后于 Inventory）。
    /// </summary>
    public static class QuestProgressRefresh
    {
        static readonly QuestService Quests = new QuestService();

        public static void AfterWorldChange(SimulationWorld world, EntityId subject)
        {
            if (world == null)
                return;
            var who = subject.IsNone ? FindPrimarySubject(world) : subject;
            if (who.IsNone)
                return;
            Quests.Evaluate(world, who);
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
