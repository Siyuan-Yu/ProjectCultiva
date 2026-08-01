using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Simulation;
using XianXia.Core.Social;

namespace XianXia.Core.Concealment
{
    /// <summary>
    /// Demo [49]/[32] exposure while cultivating: day high / night low / near supervisor.
    /// Display-only risk; no chase／punishment.
    /// </summary>
    public static class ConcealmentExposureRules
    {
        public static bool IsNight(WorldTick tick)
        {
            var hour = DayClock.FromWorldTick(tick).HourOfDay;
            return hour >= 19 || hour < 6;
        }

        public static int CultivateRiskDelta(SimulationWorld world, EntityId subject)
        {
            if (world == null)
                return 0;
            var delta = IsNight(world.Tick) ? 1 : 3;
            if (IsNearSupervisor(world, subject))
                delta += 2;
            return delta;
        }

        public static bool IsNearSupervisor(SimulationWorld world, EntityId subject)
        {
            if (!world.Entities.TryGet(subject, out var entity) ||
                !entity.TryGet<EntityLocationComponent>(out var loc) ||
                !loc.HasLocation)
                return false;

            foreach (var other in world.Entities.All)
            {
                if (other.Id == subject)
                    continue;
                var isSupervisor = false;
                if (other.TryGet<NpcAiRoleComponent>(out var ai) &&
                    ai.Role == NpcAiRoleKind.Supervisor)
                    isSupervisor = true;
                if (!isSupervisor &&
                    other.TryGet<FactionMembershipComponent>(out var mem) &&
                    mem.Role == FactionRoleKind.Supervisor)
                    isSupervisor = true;
                if (!isSupervisor)
                    continue;
                if (!other.TryGet<EntityLocationComponent>(out var otherLoc) || !otherLoc.HasLocation)
                    continue;
                if (string.Equals(otherLoc.LocationId, loc.LocationId, System.StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}
