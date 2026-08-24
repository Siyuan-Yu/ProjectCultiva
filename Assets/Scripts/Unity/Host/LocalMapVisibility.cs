using System.Collections.Generic;
using XianXia.Core.Combat;
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
        /// <summary>
        /// 我方角色是否仍占用指定 LocalMap（上路／路锚不算）。
        /// 遭遇图实例只认 InEncounter；普通节点图按 Node→LocalMapId 对齐。
        /// </summary>
        public static bool IsFriendlyCharacterOnMapLayout(
            SimulationWorld world,
            EntityId id,
            string mapLayoutId)
        {
            if (world?.WorldPresence == null || id.IsNone || string.IsNullOrWhiteSpace(mapLayoutId))
                return false;

            var mapId = mapLayoutId.Trim();
            if (IsEncounterMapInstance(world, mapId))
            {
                if (!world.WorldPresence.TryGet(id, out var encWp) || encWp == null)
                    return false;
                return encWp.Mode == PartyWorldPresenceMode.InEncounter;
            }

            if (StrategicWorldSitePopulationService.TryResolvePartyFocusSite(world, out var focusSite) &&
                string.Equals(
                    WorldTravelService.ResolveWorldSiteLocalMapId(focusSite),
                    mapId,
                    System.StringComparison.Ordinal) &&
                StrategicWorldSitePopulationService.IsCharacterPresentAtWorldSite(world, id, focusSite))
                return true;

            if (!world.WorldPresence.TryGet(id, out var wp) || wp == null)
                return false;

            if (wp.Mode == PartyWorldPresenceMode.Traveling ||
                wp.Mode == PartyWorldPresenceMode.RouteAnchored ||
                wp.Mode == PartyWorldPresenceMode.InEncounter)
                return false;

            if (wp.Mode == PartyWorldPresenceMode.AtSite &&
                !string.IsNullOrEmpty(wp.SiteId) &&
                world.Strategic.Sites.TryGet(wp.SiteId, out var site) &&
                site != null)
            {
                return string.Equals(
                    WorldTravelService.ResolveWorldSiteLocalMapId(site),
                    mapId,
                    System.StringComparison.Ordinal);
            }

            return false;
        }

        public static bool HasFriendlyCharacterOnMapLayout(
            SimulationWorld world,
            IReadOnlyList<EntityId> characterIds,
            string mapLayoutId)
        {
            if (world == null || characterIds == null || string.IsNullOrWhiteSpace(mapLayoutId))
                return false;
            for (var i = 0; i < characterIds.Count; i++)
            {
                if (IsFriendlyCharacterOnMapLayout(world, characterIds[i], mapLayoutId))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 装图用：只要有人 AtNode 落在该 LocalMapId，或遭遇中人在遭遇图，就允许加载。
        /// （不因遭遇残留把同保底图 id 的村庄节点误判成空图）
        /// </summary>
        public static bool CanLoadMapLayoutForParty(
            SimulationWorld world,
            IReadOnlyList<EntityId> characterIds,
            string mapLayoutId)
        {
            if (world == null || characterIds == null || string.IsNullOrWhiteSpace(mapLayoutId))
                return false;
            var mapId = mapLayoutId.Trim();
            if (world.Strategic?.Encounter != null && world.Strategic.Encounter.SpawnOnNextMapLoad)
                return true;
            if (world.Strategic?.Encounter != null &&
                world.Strategic.Encounter.HasEngagedParty &&
                string.Equals(
                    mapId,
                    BattleOfferService.ResolveActiveEncounterLocalMapId(world),
                    System.StringComparison.Ordinal))
                return true;

            if (StrategicWorldSitePopulationService.TryResolvePartyFocusSite(world, out var focusSite) &&
                string.Equals(
                    WorldTravelService.ResolveWorldSiteLocalMapId(focusSite),
                    mapId,
                    System.StringComparison.Ordinal) &&
                StrategicWorldSitePopulationService.HasFriendlyCharacterPresentAtWorldSite(
                    world, characterIds, focusSite))
                return true;

            for (var i = 0; i < characterIds.Count; i++)
            {
                var id = characterIds[i];
                if (id.IsNone || !world.WorldPresence.TryGet(id, out var wp) || wp == null)
                    continue;
                if (wp.Mode == PartyWorldPresenceMode.Traveling ||
                    wp.Mode == PartyWorldPresenceMode.RouteAnchored)
                    continue;
                if (wp.Mode == PartyWorldPresenceMode.InEncounter)
                {
                    if (string.Equals(
                            mapId,
                            BattleOfferService.ResolveActiveEncounterLocalMapId(world),
                            System.StringComparison.Ordinal))
                        return true;
                    continue;
                }

                if (wp.Mode == PartyWorldPresenceMode.AtSite &&
                    !string.IsNullOrEmpty(wp.SiteId) &&
                    world.Strategic.Sites.TryGet(wp.SiteId, out var site) &&
                    site != null &&
                    string.Equals(
                        WorldTravelService.ResolveWorldSiteLocalMapId(site),
                        mapId,
                        System.StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 保底图 id 与遭遇图共用时：只要遭遇运行时仍挂着参战／刷怪／待刷，就按遭遇实例计。
        /// </summary>
        static bool IsEncounterMapInstance(SimulationWorld world, string mapLayoutId)
        {
            if (world == null || string.IsNullOrEmpty(mapLayoutId))
                return false;
            if (!string.Equals(
                    mapLayoutId,
                    StrategicEncounterCatalog.DefaultEncounterLocalMapId,
                    System.StringComparison.Ordinal))
                return false;

            var enc = world.Strategic?.Encounter;
            if (enc != null)
            {
                if (enc.SpawnOnNextMapLoad || enc.HasEngagedParty || enc.SpawnedEntityIds.Count > 0)
                    return true;
            }

            if (world.PartyWorld != null &&
                !string.IsNullOrEmpty(world.PartyWorld.EncounterId))
                return true;

            if (world.WorldPresence != null)
            {
                foreach (var kv in world.WorldPresence.All)
                {
                    if (kv.Value != null &&
                        kv.Value.Mode == PartyWorldPresenceMode.InEncounter)
                        return true;
                }
            }

            return false;
        }

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

            // 尸体腐烂 Removed：LocalMap 不再显示
            if (CombatLifeStateService.ShouldHideFromSpawn(entity))
                return false;

            var onEncounterMap = IsActiveStrategicEncounterMap(world);

            // WorldSite LocalMap 硬门禁：有宏观 Presence 的实体只按「是否物理在当前 Site」显示。
            // 禁止世界其它地点的 NPC／Army 成员落到同一张图（含开局荒村）。
            // 无 WorldPresence、仅绑 LocationId 的场景 NPC（守卫／商人等）仍走下方地点过滤。
            if (!onEncounterMap &&
                StrategicWorldSitePopulationService.TryResolvePartyFocusSite(world, out var siteFocus) &&
                world.WorldPresence != null &&
                world.WorldPresence.TryGet(id, out _))
            {
                return StrategicWorldSitePopulationService.IsCharacterPresentAtWorldSite(
                    world, id, siteFocus);
            }

            if (onEncounterMap && IsForeignBattlefieldEntity(world, id))
                return false;

            // 遭遇图上：未进场的我方可控角色隐藏；敌军刷怪／弥留也有 WorldPresence，绝不能误伤
            if (onEncounterMap &&
                world.Strategic?.Encounter != null &&
                world.Strategic.Encounter.HasEngagedParty &&
                (entity.Tags & EntityTag.Npc) == 0 &&
                world.WorldPresence != null &&
                world.WorldPresence.TryGet(id, out _) &&
                !world.Strategic.Encounter.IsEngaged(id))
                return false;

            // 手动遭遇：参战者已落点（PresentationOverride）即显示；禁止 Hex/WorldSite 空 NodeId 误杀
            if (onEncounterMap &&
                world.Strategic?.Encounter != null &&
                world.Strategic.Encounter.IsEngaged(id) &&
                entity.TryGet<EntityLocationComponent>(out var engagedSpawnLoc) &&
                engagedSpawnLoc.HasPresentationOverride)
                return true;

            // 有宏观在场记录的可控角色：只显示「当前焦点节点上、未上路」的人
            if (world.WorldPresence != null &&
                world.WorldPresence.TryGet(id, out var wp) &&
                wp != null)
            {
                // 敌军弥留宏观钉在路锚，再进 LocalMap 时仍应显示（与我方弥留同一套「人还在接战点」）
                if ((wp.Mode == PartyWorldPresenceMode.Traveling ||
                     wp.Mode == PartyWorldPresenceMode.RouteAnchored ||
                     wp.Mode == PartyWorldPresenceMode.AtHex) &&
                    IsStrategicEncounterSpawn(world, id) &&
                    onEncounterMap &&
                    entity.TryGet<EntityLocationComponent>(out var spawnLoc) &&
                    spawnLoc.HasPresentationOverride)
                    return true;

                if (wp.Mode == PartyWorldPresenceMode.Traveling)
                    return false;
                if (wp.Mode == PartyWorldPresenceMode.RouteAnchored)
                    return false;

                if (wp.Mode == PartyWorldPresenceMode.AtHex)
                {
                    // 遭遇图上：非本场 scoped spawn 的 Hex residual 不得靠 LocationId 漏进来
                    if (onEncounterMap)
                        return false;
                    // 非遭遇 LocalMap：Hex 宏观单位不进场景（除非上面 WorldSite 硬门禁已放行）
                    return false;
                }

                if (wp.Mode == PartyWorldPresenceMode.InEncounter)
                {
                    if (!onEncounterMap)
                        return false;
                    var enc = world.Strategic?.Encounter;
                    var allowed = (enc != null && enc.IsEngaged(id)) ||
                                  IsStrategicEncounterSpawn(world, id);
                    if (!allowed)
                        return false;
                    if (entity.TryGet<EntityLocationComponent>(out var encounterLoc) &&
                        encounterLoc.HasPresentationOverride)
                        return true;
                    return false;
                }

                // Hex FormalArmy 成员若仍残留 AtNode Presence，不得凭 NodeId 误进任意 LocalMap
                if (!onEncounterMap && IsHexStrategicArmyMember(world, id))
                    return false;

                var focus = world.PartyWorld != null ? world.PartyWorld.NodeId : null;
                var focusSite = world.PartyWorld != null ? world.PartyWorld.SiteId : null;
                if (wp.Mode == PartyWorldPresenceMode.AtSite)
                {
                    if (!string.IsNullOrEmpty(focusSite) &&
                        string.Equals(wp.SiteId, focusSite, System.StringComparison.Ordinal))
                        return !onEncounterMap ||
                               StrategicEncounterHostilityService.IsVisibleOnEncounterLocalMap(world, id);
                    return false;
                }

                if (string.IsNullOrEmpty(focus) ||
                    !string.Equals(wp.NodeId, focus, System.StringComparison.Ordinal))
                    return false;

                // 遭遇图：禁止同 Node AtNode 的非参战战略 NPC 漏进战场（如 WeakBandit 战场的 BanditLeader/A/B/C）
                if (wp.Mode == PartyWorldPresenceMode.AtNode)
                {
                    if (onEncounterMap)
                        return StrategicEncounterHostilityService.IsVisibleOnEncounterLocalMap(world, id);
                    return true;
                }
            }

            if (!entity.TryGet<EntityLocationComponent>(out var loc) || !loc.HasLocation)
            {
                if (IsStrategicEncounterSpawn(world, id) &&
                    entity.TryGet<EntityLocationComponent>(out var spawnLoc2) &&
                    spawnLoc2.HasPresentationOverride &&
                    onEncounterMap)
                    return true;
                if (IsCaveBoundNpc(entity) && !world.LocalMap.IsInInterior)
                    return false;
                // 有宏观 Presence 但无地点、又未过 WorldSite 硬门禁 → 不显示
                if (world.WorldPresence != null && world.WorldPresence.TryGet(id, out _))
                {
                    if (onEncounterMap)
                        return StrategicEncounterHostilityService.IsVisibleOnEncounterLocalMap(world, id);
                    return false;
                }
                return false;
            }

            if (IsStrategicEncounterSpawn(world, id) &&
                loc.HasPresentationOverride &&
                onEncounterMap)
                return true;

            // 遭遇图：禁止用「LocationId 落在遭遇图地点表」把其他战场 NPC 带进来
            if (onEncounterMap &&
                (entity.Tags & EntityTag.Npc) != 0 &&
                !IsStrategicEncounterSpawn(world, id))
                return false;

            // 地点不在当前地点表（例如已从荒村切到保底节点）：必须隐藏，禁止残留旧场景 NPC
            if (!world.WorldRegion.TryGet(loc.LocationId, out var place))
                return false;

            return IsLocationOnActiveMap(world, place);
        }

        /// <summary>
        /// 属于其他 Lingering Battlefield 的 tracked entity，不得在当前遭遇 LocalMap 显示。
        /// </summary>
        static bool IsForeignBattlefieldEntity(SimulationWorld world, EntityId id)
        {
            if (world?.Strategic?.Encounter == null || id.IsNone)
                return false;

            var rt = world.Strategic.Encounter;
            if (string.IsNullOrEmpty(rt.ActiveBattlefieldId))
                return false;

            if (!BattlefieldSpawnScope.TryFindOwningBattlefieldId(world, id, out var ownerId))
                return false;

            return !string.Equals(ownerId, rt.ActiveBattlefieldId, System.StringComparison.Ordinal);
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
            if (!string.Equals(world.LocalMap.ActiveMapLayoutId, mapId, System.StringComparison.Ordinal))
                return false;
            // 仅遭遇图实例（base:map_world_node_stub + 活跃 Encounter 状态），
            // 禁止把青石荒村等普通 LocalMap 误判为遭遇图（否则 AtNode 村民会被 Participant 过滤隐藏）。
            return IsEncounterMapInstance(world, mapId);
        }

        static bool IsStrategicEncounterSpawn(SimulationWorld world, EntityId id) =>
            BattlefieldSpawnScope.IsTrackedInCurrentLocalMapScope(world, id);

        static bool IsHexStrategicArmyMember(SimulationWorld world, EntityId id)
        {
            if (world == null || id.IsNone)
                return false;
            if (!ArmyService.TryGetArmyForCharacter(world, id, out var army) || army == null)
                return false;
            return army.UsesHexStrategicPosition;
        }
    }
}
