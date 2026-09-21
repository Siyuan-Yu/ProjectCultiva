using System.Collections.Generic;
using XianXia.Core.Combat;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Simulation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>按当Active LocalMap／宏观所在节点过滤实体／地点是否应显示/summary>
    public static class LocalMapVisibility
    {
        /// <summary>
        /// 我方角色是否仍占用指LocalMap（上路／路锚不算）
        /// 遭遇图实例只InEncounter；普通节点图Node→LocalMapId 对齐
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

            if (wp.Mode == PartyWorldPresenceMode.InEncounter)
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

            if (wp.Mode == PartyWorldPresenceMode.AtHex &&
                wp.UsesHexPresence &&
                string.Equals(
                    world.PartyWorld?.LocalMapId?.Trim(),
                    mapId,
                    System.StringComparison.Ordinal))
                return true;

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
        /// 装图用：只要有人 AtSite 落在LocalMapId，或遭遇中人在遭遇图，就允许加载
        /// （不因遭遇残留把同保底图 id 的村庄节点误判成空图
        /// </summary>
        public static bool CanLoadMapLayoutForParty(
            SimulationWorld world,
            IReadOnlyList<EntityId> characterIds,
            string mapLayoutId)
        {
            if (world == null || characterIds == null || string.IsNullOrWhiteSpace(mapLayoutId))
                return false;
            var mapId = mapLayoutId.Trim();
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
                if (wp.Mode == PartyWorldPresenceMode.InEncounter)
                {
                    if (string.Equals(
                            mapId,
                            LegacyStrategicMapCatalog.DefaultEncounterLocalMapId,
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

                // Compatibility-only legacy Outdoor LocalMap: AtHex member and PartyWorld map
                // focus must agree. Normal Continuous Outdoor materializes by Surface position.
                if (wp.Mode == PartyWorldPresenceMode.AtHex &&
                    wp.UsesHexPresence &&
                    string.Equals(
                        world.PartyWorld?.LocalMapId?.Trim(),
                        mapId,
                        System.StringComparison.Ordinal))
                    return true;

                if (wp.Mode == PartyWorldPresenceMode.AtWorldPosition &&
                    world.PlayerPartyTravel != null &&
                    world.PlayerPartyTravel.HasPosition &&
                    world.PlayerPartyTravel.LocationKind == PlayerPartyLocationKind.AtWorldPosition &&
                    string.Equals(
                        world.PartyWorld?.LocalMapId?.Trim(),
                        mapId,
                        System.StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 保底id 与遭遇图共用时：只要遭遇运行时仍挂着参战／刷怪／待刷，就按遭遇实例计
        /// </summary>
        static bool IsEncounterMapInstance(SimulationWorld world, string mapLayoutId)
        {
            if (world == null || string.IsNullOrEmpty(mapLayoutId))
                return false;
            if (!string.Equals(
                    mapLayoutId,
                    LegacyStrategicMapCatalog.DefaultEncounterLocalMapId,
                    System.StringComparison.Ordinal))
                return false;

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
                // 洞内／秘境地点：地表绝不显示（即LocalMapId 漏填，也interior 标签
                if (IsInteriorOnlyLocation(loc))
                    return false;
                return string.IsNullOrEmpty(loc.LocalMapId) ||
                       string.Equals(loc.LocalMapId, lm.ActiveMapLayoutId, System.StringComparison.Ordinal);
            }

            return string.Equals(loc.LocalMapId, lm.ActiveMapLayoutId, System.StringComparison.Ordinal);
        }

        /// <summary>
        /// 解析「当前 LocalMap 可见性焦点 WorldSite」。
        /// 优先 PartyWorld.SiteId（正式 EnterWorldSiteScene / AtSite）；当玩家以 AtHex / AtWorldPosition
        /// （Wilderness 邻接 / travel 呈现）停留在某 Site footprint hex 上时，画面已是该 Site LocalMap，
        /// 按玩家物理 hex 反查所属 Site 作为焦点 —— 与 garrison materialize 的 wilderness reconcile
        /// （army.CurrentHex == 玩家 hex）同源一致。荒野 hex（不属于任何 Site）→ false（resident 不泄漏）。
        /// </summary>
        static bool TryResolveVisibilityFocusSite(SimulationWorld world, out WorldSite site)
        {
            if (StrategicWorldSitePopulationService.TryResolvePartyFocusSite(world, out site) && site != null)
                return true;

            var travel = world?.PlayerPartyTravel;
            if (travel == null || !travel.HasPosition)
                return false;

            var hex = travel.LocationKind == PlayerPartyLocationKind.AtWorldPosition
                ? HexMath.WorldToHex(
                    travel.WorldPosition.X,
                    travel.WorldPosition.Y,
                    world.HexWorld != null && world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f)
                : travel.CurrentHex;

            return world.Strategic?.Sites != null &&
                   world.Strategic.Sites.TryGetAtHex(hex, out site) &&
                   site != null;
        }

        public static bool IsEntityVisible(SimulationWorld world, EntityId id)
        {
            if (world == null || id.IsNone || !world.Entities.TryGet(id, out var entity))
                return false;

            // 尸体腐烂 Removed：LocalMap 不再显示
            if (CombatLifeStateService.ShouldHideFromSpawn(entity))
                return false;

            // SPACE-01：Separate Space 拥有最高 playable presentation 优先级。
            // Outdoor WorldSite／Surface presence／chunk materialization 一律不参与当前显示。
            if (world.LocalMap != null && world.LocalMap.IsActive)
                return IsEntityVisibleInSeparateSpace(world, id, entity);

            // Independent Encounter presentation is an explicit takeover scope. It must be
            // resolved before ordinary Continuous/LocalMap rules: InEncounter correctly hides a
            // person from the normal world, while this exact bound encounter must show it.
            if (world.ContinuousOutdoorMaterialization.HasIndependentEncounterBinding &&
                (entity.Tags & (EntityTag.Character | EntityTag.Npc)) != 0)
                return EvaluateIndependentEncounterVisibility(world, id, out _);

            // A bound Continuous manual battle is an isolated character scope. This deny must
            // precede every legacy Site/Location/occupant exception so hidden bystanders cannot
            // leak back through another presentation rule.
            var continuousCombat = world.Strategic?.ContinuousManualCombat;
            if (continuousCombat != null && continuousCombat.IsActive &&
                (entity.Tags & (EntityTag.Character | EntityTag.Npc)) != 0 &&
                !string.IsNullOrEmpty(continuousCombat.SurfaceId) &&
                PlayerPartyLocalCoPresenceQuery.IsContinuousOutdoorPresentationScope(world) &&
                (world.LocalMap == null || !world.LocalMap.IsInInterior) &&
                !IsActiveStrategicEncounterMap(world))
                return EvaluateContinuousMaterializedVisibility(world, id, out _);

            var onEncounterMap = IsActiveStrategicEncounterMap(world);

            // Continuous materialization is the current loaded physical scope. It must be
            // evaluated before WorldPresence/LocationId legacy gates: Squad-derived presence is
            // derived and may be absent during a repair boundary, while the runtime already has
            // a legal placement. The shared predicate also prevents stale materialization from
            // leaking into Interior or Encounter-owned presentation.
            if (EvaluateContinuousMaterializedVisibility(world, id, out _))
                return true;

            // 普通战略人口（living NPC Squad member / Strategic Residual）
            // 已作为正常 LocalMap population materialize 到当前 Loaded Real LocalMap。
            // 物理在场 → 继续显示，不依赖 Battle Encounter / ParticipantSnapshot /
            // BattlefieldSpawnScope —— 这是「实体物理上就在这张地图」，不是战斗临时
            // visibility exception。必须在 WorldSite 硬门禁之前判定。
            // WorldSite LocalMap 硬门禁：有宏Presence 的实体只按「是否物理在当前 Site」显示
            // 禁止世界其它地点NPC／Army 成员落到同一张图（含开局荒村）
            // WorldPresence、仅LocationId 的场NPC（守卫／商人等）仍走下方地点过滤
            if (!onEncounterMap &&
                StrategicWorldSitePopulationService.TryResolvePartyFocusSite(world, out var siteFocus) &&
                world.WorldPresence != null &&
                world.WorldPresence.TryGet(id, out _))
            {
                return StrategicWorldSitePopulationService.IsCharacterPresentAtWorldSite(
                    world, id, siteFocus);
            }

            // 有宏观在场记录的可控角色：只显示「当前焦点节点上、未上路」的
            if (world.WorldPresence != null &&
                world.WorldPresence.TryGet(id, out var wp) &&
                wp != null)
            {
                if (wp.Mode == PartyWorldPresenceMode.AtHex)
                {
                    // 遭遇图上：非本场 scoped spawn Hex residual 不得LocationId 漏进
                    if (onEncounterMap)
                        return false;
                    if (world.ContinuousOutdoorMaterialization.IsMaterialized(id))
                        return true;
                    // Compatibility-only legacy Outdoor LocalMap visibility for an AtHex party.
                    return PlayerPartyLocalMapMaterializationService.IsWildernessPartyMemberVisibleOnActiveLocalMap(
                        world, id, wp);
                }

                if (wp.Mode == PartyWorldPresenceMode.AtWorldPosition && wp.HasContinuousWorldPosition)
                {
                    if (onEncounterMap)
                        return false;
                    if (world.ContinuousOutdoorMaterialization.IsMaterialized(id))
                        return true;
                    return LoadedDestinationArrivalMaterializer.IsBackgroundCharacterVisibleOnLoadedWildernessLocalMap(
                        world, id);
                }

                if (wp.Mode == PartyWorldPresenceMode.InEncounter)
                {
                    if (!onEncounterMap)
                        return false;
                    var enc = world.Strategic?.CharacterEncounter;
                    var allowed = enc?.Find(id.Value) != null;
                    if (!allowed)
                        return false;
                    if (entity.TryGet<EntityLocationComponent>(out var encounterLoc) &&
                        encounterLoc.HasPresentationOverride)
                        return true;
                    return false;
                }

                // Continuous Outdoor：runtime 的 materialize 集合就是「物理在当前 loaded scope」的权威，
                // 与 legacy map population materialization 同义；其中包含由当前
                // SquadWorldMotion / Site context 掌权的驻守成员。
                // 必须在下方「残留 AtSite presence」守卫之前放行，否则会出现
                // 「Expected=N Materialized=N Views=N-1」——materialized 却永远没有 EntityView。
                if (!onEncounterMap &&
                    wp.Mode == PartyWorldPresenceMode.AtSite &&
                    world.Strategic?.Sites != null &&
                    world.Strategic.Sites.TryGet(wp.SiteId, out var materializedSite) &&
                    materializedSite != null &&
                    WorldSiteOutdoorMigrationPolicy.UsesContinuousOutdoorSurface(materializedSite) &&
                    world.ContinuousOutdoorMaterialization.IsMaterialized(id))
                    return true;

                // Legacy AtSite residue 不得凭 SiteId 误进任意 LocalMap。
                if (!onEncounterMap && IsTravelingSquadMember(world, id))
                    return false;

                var focusSite = TryResolveVisibilityFocusSite(world, out var focusSiteState)
                    ? focusSiteState.SiteId
                    : null;
                if (wp.Mode == PartyWorldPresenceMode.AtSite)
                {
                    if (world.Strategic.Sites.TryGet(wp.SiteId, out var continuousSite) &&
                        WorldSiteOutdoorMigrationPolicy.UsesContinuousOutdoorSurface(continuousSite) &&
                        world.ContinuousOutdoorMaterialization.IsMaterialized(id))
                        return true;
                    if (!string.IsNullOrEmpty(focusSite) &&
                        string.Equals(wp.SiteId, focusSite, System.StringComparison.Ordinal))
                        return !onEncounterMap ||
                               StrategicEncounterHostilityService.IsVisibleOnEncounterLocalMap(world, id);
                    return false;
                }
            }

            if (!entity.TryGet<EntityLocationComponent>(out var loc) || !loc.HasLocation)
            {
                if (IsCaveBoundNpc(entity) && !world.LocalMap.IsInInterior)
                    return false;
                // 有宏Presence 但无地点、又未过 WorldSite 硬门不显
                if (world.WorldPresence != null && world.WorldPresence.TryGet(id, out _))
                {
                    if (onEncounterMap)
                        return StrategicEncounterHostilityService.IsVisibleOnEncounterLocalMap(world, id);
                    return false;
                }
                return false;
            }

            // 遭遇图：禁止用「LocationId 落在遭遇图地点表」把其他战场 NPC 带进
            if (onEncounterMap &&
                (entity.Tags & EntityTag.Npc) != 0)
                return false;

            // 地点不在当前地点表（例如已从荒村切到保底节点）：必须隐藏，禁止残留旧场景 NPC
            if (!world.LocalPlaces.TryGet(loc.LocationId, out var place))
                return false;

            return IsLocationOnActiveMap(world, place);
        }

        /// <summary>
        /// SPACE-01 正式入口：当前 playable space 可见性。
        /// Separate Space active 时只认 occupant／Active MapLayout／LocalPlaceSet／遭遇特例。
        /// </summary>
        public static bool IsEntityVisibleInCurrentPlayableSpace(SimulationWorld world, EntityId id) =>
            IsEntityVisible(world, id);

        static bool IsEntityVisibleInSeparateSpace(SimulationWorld world, EntityId id, Entity entity)
        {
            var session = world.LocalMap;
            var activeMap = session.ActiveMapLayoutId;

            // Occupant authority first.
            if (session.ContainsOccupant(id))
                return true;

            if (IsActiveStrategicEncounterMap(world))
            {
                if (world.Strategic?.CharacterEncounter?.Find(id.Value) != null &&
                    entity.TryGet<EntityLocationComponent>(out var engagedLoc) &&
                    engagedLoc.HasPresentationOverride)
                    return true;
            }

            // Cave / Separate Space residents：地点属于 Active MapLayout。
            if (entity.TryGet<EntityLocationComponent>(out var loc) && loc.HasLocation &&
                world.LocalPlaces.TryGet(loc.LocationId, out var place) &&
                IsLocationOnActiveMap(world, place))
                return true;

            // Cave-bound NPC without location still hidden outdoors; inside, Personality tag cave
            // alone is not enough — must belong to active layout via location or occupant.
            if (IsCaveBoundNpc(entity) &&
                entity.TryGet<EntityLocationComponent>(out var caveLoc) &&
                caveLoc.HasLocation &&
                world.LocalPlaces.TryGet(caveLoc.LocationId, out var cavePlace) &&
                string.Equals(cavePlace.LocalMapId, activeMap, System.StringComparison.Ordinal))
                return true;

            return false;
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

        /// <summary>
        /// Exact Continuous Outdoor visibility gate shared by gameplay and the one-shot Army
        /// diagnostic. A true result still flows through EntityViewSpawner, never a second spawner.
        /// </summary>
        public static bool EvaluateContinuousMaterializedVisibility(
            SimulationWorld world,
            EntityId id,
            out string reason)
        {
            if (world == null || id.IsNone || !world.Entities.TryGet(id, out var entity))
            {
                reason = "EntityMissing";
                return false;
            }
            if (CombatLifeStateService.ShouldHideFromSpawn(entity))
            {
                reason = "Removed";
                return false;
            }
            var continuousCombat = world.Strategic?.ContinuousManualCombat;
            var boundContinuousCombatParticipant = continuousCombat != null &&
                                                   continuousCombat.IsActive &&
                                                   continuousCombat.Contains(id);
            if (IsActiveStrategicEncounterMap(world))
            {
                reason = "EncounterMapOwnsPresentation";
                return false;
            }
            if (!PlayerPartyLocalCoPresenceQuery.IsContinuousOutdoorPresentationScope(world))
            {
                reason = world.LocalMap != null && world.LocalMap.IsInInterior
                    ? "InteriorOwnsPresentation"
                    : "ContinuousOutdoorScopeInactive";
                return false;
            }
            if (PlayerPartyLocalCoPresenceQuery.IsIndependentSpacePresence(world, id))
            {
                reason = "IndependentSpacePresence";
                return false;
            }
            if (!world.ContinuousOutdoorMaterialization.IsMaterialized(id))
            {
                reason = "NotContinuousMaterialized";
                return false;
            }
            if (!entity.TryGet<EntityLocationComponent>(out var loc) ||
                loc == null || !loc.HasPresentationOverride)
            {
                reason = "PresentationOverrideMissing";
                return false;
            }

            reason = boundContinuousCombatParticipant
                ? "ContinuousCombatParticipantWithLegalPresentation"
                : "ContinuousMaterializedWithLegalPresentation";
            return true;
        }

        public static bool EvaluateIndependentEncounterVisibility(
            SimulationWorld world,
            EntityId id,
            out string reason)
        {
            reason = "IndependentEncounterUnavailable";
            if (world == null || id.IsNone || !world.Entities.TryGet(id, out var entity))
            {
                reason = "EntityMissing";
                return false;
            }
            if (CombatLifeStateService.ShouldHideFromSpawn(entity))
            {
                reason = "Removed";
                return false;
            }
            var binding = world.ContinuousOutdoorMaterialization;
            var state = world.Strategic?.CharacterEncounter;
            if (!binding.HasIndependentEncounterBinding || state == null ||
                !string.Equals(binding.IndependentEncounterId, state.EncounterId,
                    System.StringComparison.Ordinal) ||
                !string.Equals(binding.IndependentEncounterSurfaceId, state.SourceSurfaceId,
                    System.StringComparison.Ordinal) ||
                (state.Phase != CharacterEncounterPhase.Active &&
                 state.Phase != CharacterEncounterPhase.ReadyToEnd))
            {
                reason = "IndependentEncounterBindingMismatch";
                return false;
            }
            if (world.LocalMap != null && world.LocalMap.IsInInterior)
            {
                reason = "InteriorOwnsPresentation";
                return false;
            }
            var participant = state.Find(id.Value);
            if (participant == null)
            {
                reason = "NotCurrentEncounterParticipant";
                return false;
            }
            if (!world.WorldPresence.TryGet(id, out var presence) || presence == null)
            {
                reason = "EncounterPresenceMismatch";
                return false;
            }
            if (presence.Mode != PartyWorldPresenceMode.InEncounter ||
                !string.Equals(presence.PersonalSurfaceId, state.SourceSurfaceId,
                    System.StringComparison.Ordinal))
            {
                reason = "EncounterPresenceMismatch";
                return false;
            }
            if (!world.ContinuousOutdoorMaterialization.IsMaterialized(id))
            {
                reason = "NotEncounterMaterialized";
                return false;
            }
            if (!entity.TryGet<EntityLocationComponent>(out var location) || location == null ||
                !location.HasPresentationOverride)
            {
                reason = "PresentationOverrideMissing";
                return false;
            }
            reason = "CurrentIndependentEncounterParticipant";
            return true;
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
            // 仅独立遭遇战术图实例（且有活跃 Encounter 状态）
            // 禁止把青石荒村等普LocalMap 误判为遭遇图（否AtSite 村民会被 Participant 过滤隐藏）
            return IsEncounterMapInstance(world, mapId);
        }

        static bool IsTravelingSquadMember(SimulationWorld world, EntityId id)
        {
            if (world == null || id.IsNone)
                return false;
            if (!world.Strategic.Squads.TryGetForCharacter(id, out var squad) || squad == null ||
                !world.Strategic.SquadWorldMotions.TryGet(squad.SquadId, out var motion) || motion == null)
                return false;
            return SquadWorldMotionService.OwnsCharacter(world, id) &&
                   motion.HasPosition && string.IsNullOrEmpty(motion.SiteId);
        }
    }
}
