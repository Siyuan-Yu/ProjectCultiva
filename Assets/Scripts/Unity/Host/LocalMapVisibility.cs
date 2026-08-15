using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Simulation;
using XianXia.Core.Domain.Ids;

namespace XianXia.Unity.Host
{
    /// <summary>按当前 Active LocalMap 过滤实体／地点是否应显示。</summary>
    public static class LocalMapVisibility
    {
        public static bool IsLocationOnActiveMap(SimulationWorld world, WorldLocationState loc)
        {
            if (world?.LocalMap == null || loc == null)
                return true;

            var lm = world.LocalMap;
            if (!lm.IsInInterior)
            {
                return string.IsNullOrEmpty(loc.LocalMapId) ||
                       string.Equals(loc.LocalMapId, lm.ActiveMapLayoutId, System.StringComparison.Ordinal);
            }

            return string.Equals(loc.LocalMapId, lm.ActiveMapLayoutId, System.StringComparison.Ordinal);
        }

        public static bool IsEntityVisible(SimulationWorld world, EntityId id)
        {
            if (world == null || id.IsNone || !world.Entities.TryGet(id, out var entity))
                return false;
            if (!entity.TryGet<EntityLocationComponent>(out var loc) || !loc.HasLocation)
                return true;
            if (!world.WorldRegion.TryGet(loc.LocationId, out var place))
                return !world.LocalMap.IsInInterior;
            return IsLocationOnActiveMap(world, place);
        }
    }
}
