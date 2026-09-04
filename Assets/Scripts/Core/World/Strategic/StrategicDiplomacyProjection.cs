using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>WarBoard 的只读外交投影；不拥有正式战争状态。</summary>
    public static class StrategicDiplomacyProjection
    {
        public static void RebuildWarStances(SimulationWorld world)
        {
            if (world?.Strategic?.Diplomacy == null || world.Strategic.Wars == null)
                return;
            world.Strategic.Diplomacy.Clear();
            foreach (var war in world.Strategic.Wars.EnumerateActive())
                foreach (var attacker in war.Attackers)
                    foreach (var defender in war.Defenders)
                        world.Strategic.Diplomacy.SetStance(attacker, defender, FactionStance.War);
        }
    }
}
