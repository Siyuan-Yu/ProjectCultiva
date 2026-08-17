using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Simulation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>按当前 Active LocalMap／宏观所在节点过滤实体／地点是否应显示。</summary>
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

            if (IsActiveStrategicEncounterMap(world) &&
                world.Strategic?.Encounter != null &&
                world.Strategic.Encounter.HasEngagedParty &&
                world.WorldPresence != null &&
                world.WorldPresence.TryGet(id, out _) &&
                !world.Strategic.Encounter.IsEngaged(id))
                return false;

            // 有宏观在场记录的可控角色：只显示「当前焦点节点上、未上路」的人
            if (world.WorldPresence != null &&
                world.WorldPresence.TryGet(id, out var wp) &&
                wp != null)
            {
                if (wp.Mode == PartyWorldPresenceMode.Traveling)
                    return false;
                if (wp.Mode == PartyWorldPresenceMode.RouteAnchored)
                    return false;
                if (wp.Mode == PartyWorldPresenceMode.InEncounter)
                {
                    if (IsActiveStrategicEncounterMap(world) &&
                        entity.TryGet<EntityLocationComponent>(out var encounterLoc) &&
                        encounterLoc.HasPresentationOverride)
                        return true;
                    return false;
                }
                var focus = world.PartyWorld != null ? world.PartyWorld.NodeId : null;
                if (string.IsNullOrEmpty(focus) ||
                    !string.Equals(wp.NodeId, focus, System.StringComparison.Ordinal))
                    return false;

                // 同节点可控者：有表现坐标即可显示（进保底图时 Location 可能刚写上）
                if (entity.TryGet<EntityLocationComponent>(out var partyLoc) &&
                    partyLoc.HasPresentationOverride)
                    return true;
            }

            if (!entity.TryGet<EntityLocationComponent>(out var loc) || !loc.HasLocation)
            {
                if (IsStrategicEncounterSpawn(world, id) &&
                    entity.TryGet<EntityLocationComponent>(out var spawnLoc) &&
                    spawnLoc.HasPresentationOverride &&
                    IsActiveStrategicEncounterMap(world))
                    return true;
                if (IsCaveBoundNpc(entity) && !world.LocalMap.IsInInterior)
                    return false;
                if (world.WorldPresence != null && world.WorldPresence.TryGet(id, out _))
                    return true;
                return false;
            }

            if (IsStrategicEncounterSpawn(world, id) &&
                loc.HasPresentationOverride &&
                IsActiveStrategicEncounterMap(world))
                return true;

            // 地点不在当前地点表（例如已从荒村切到保底节点）：必须隐藏，禁止残留旧场景 NPC
            if (!world.WorldRegion.TryGet(loc.LocationId, out var place))
                return false;

            return IsLocationOnActiveMap(world, place);
        }

        public static bool IsInteriorOnlyLocation(WorldLocationState loc)
        {
            if (loc == null)
                return false;
            if (HasLocationTag(loc, "interior"))
                return true;
            return !string.IsNullOrEmpty(loc.LocalMapId);
        }

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

        static bool IsActiveStrategicEncounterMap(SimulationWorld world)
        {
            if (world?.LocalMap == null || world.PartyWorld == null)
                return false;
            var mapId = world.PartyWorld.LocalMapId;
            if (string.IsNullOrEmpty(mapId))
                return false;
            return string.Equals(world.LocalMap.ActiveMapLayoutId, mapId, System.StringComparison.Ordinal);
        }

        static bool IsStrategicEncounterSpawn(SimulationWorld world, EntityId id)
        {
            if (world?.Strategic?.Encounter == null || id.IsNone)
                return false;
            var spawned = world.Strategic.Encounter.SpawnedEntityIds;
            for (var i = 0; i < spawned.Count; i++)
            {
                if (spawned[i] == id.Value)
                    return true;
            }

            return false;
        }
    }
}
