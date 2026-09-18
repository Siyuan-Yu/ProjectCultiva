using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// SPACE-01：Separate Space 内原地战斗策略（Cave／Interior／Dungeon／SeparateMap／秘境共用）。
    /// Continuous Outdoor 仍走 CharacterEncounter／Independent Field；此处禁止混入。
    /// </summary>
    public static class SeparateSpaceCombatPolicy
    {
        public static bool IsInPlaceCombatSpace(SimulationWorld world) =>
            world?.LocalMap != null &&
            world.LocalMap.IsActive &&
            world.LocalMap.SpaceKind != SeparateSpaceKind.None;

        public static bool IsEntityInActiveSpace(SimulationWorld world, EntityId id)
        {
            if (world?.LocalMap == null || id.IsNone || !IsInPlaceCombatSpace(world))
                return false;

            if (world.LocalMap.ContainsOccupant(id))
                return true;

            if (!world.Entities.TryGet(id, out var entity) || entity == null)
                return false;

            if (!entity.TryGet<EntityLocationComponent>(out var loc) || !loc.HasLocation)
                return false;
            if (!world.LocalPlaces.TryGet(loc.LocationId, out var place) || place == null)
                return false;

            var activeMap = world.LocalMap.ActiveMapLayoutId;
            return !string.IsNullOrEmpty(activeMap) &&
                   !string.IsNullOrEmpty(place.LocalMapId) &&
                   string.Equals(place.LocalMapId, activeMap, System.StringComparison.Ordinal);
        }

        public static bool AreBothInActiveSeparateSpace(
            SimulationWorld world,
            EntityId attacker,
            EntityId target) =>
            IsInPlaceCombatSpace(world) &&
            IsEntityInActiveSpace(world, attacker) &&
            IsEntityInActiveSpace(world, target);
    }
}
