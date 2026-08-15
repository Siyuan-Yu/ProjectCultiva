using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Simulation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Social;

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
                // 洞内／秘境地点：地表绝不显示（即使 LocalMapId 漏填，也认 interior 标签）
                if (IsInteriorOnlyLocation(loc))
                    return false;
                return string.IsNullOrEmpty(loc.LocalMapId) ||
                       string.Equals(loc.LocalMapId, lm.ActiveMapLayoutId, System.StringComparison.Ordinal);
            }

            return string.Equals(loc.LocalMapId, lm.ActiveMapLayoutId, System.StringComparison.Ordinal);
        }

        public static bool IsEntityVisible(SimulationWorld world, EntityId id)
        {
            if (world == null || id.IsNone || !world.Entities.TryGet(id, out var entity))
                return false;

            // 内容标了 cave 的单位：无地点时地表不刷（防洞府威胁被误挂到村口）
            if (!entity.TryGet<EntityLocationComponent>(out var loc) || !loc.HasLocation)
            {
                if (IsCaveBoundNpc(entity) && !world.LocalMap.IsInInterior)
                    return false;
                return true;
            }

            if (!world.WorldRegion.TryGet(loc.LocationId, out var place))
            {
                if (IsCaveBoundNpc(entity) && !world.LocalMap.IsInInterior)
                    return false;
                return !world.LocalMap.IsInInterior;
            }

            return IsLocationOnActiveMap(world, place);
        }

        public static bool IsInteriorOnlyLocation(WorldLocationState loc)
        {
            if (loc == null)
                return false;
            // 洞口也有 cave 标签，不能单靠 cave；认 interior 或绑了内地图 id
            if (HasLocationTag(loc, "interior"))
                return true;
            return !string.IsNullOrEmpty(loc.LocalMapId);
        }

        /// <summary>
        /// 内容声明为洞府／秘境内威胁的角色（非「凡敌对都只能进洞」）。
        /// 地表敌、路上遭遇应另挂地表地点，不要打 cave 标签。
        /// </summary>
        public static bool IsCaveBoundNpc(Entity entity)
        {
            if (entity == null)
                return false;
            return entity.TryGet<PersonalityProfileComponent>(out var profile) &&
                   profile.HasTag("cave");
        }

        static bool HasLocationTag(WorldLocationState loc, string tag)
        {
            if (loc?.Tags == null || string.IsNullOrEmpty(tag))
                return false;
            for (var i = 0; i < loc.Tags.Count; i++)
            {
                if (string.Equals(loc.Tags[i], tag, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
