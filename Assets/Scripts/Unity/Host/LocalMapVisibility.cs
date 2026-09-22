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
    /// <summary>按当Active LocalMap／宏观所在节点过滤实体／地点是否应显示/summary>
    public static class LocalMapVisibility
    {
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

        static bool TryResolveVisibilityFocusSite(SimulationWorld world, out WorldSite site)
        {
            return StrategicWorldSitePopulationService.TryResolvePartyFocusSite(world, out site) &&
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
                (world.LocalMap == null || !world.LocalMap.IsInInterior))
                return EvaluateContinuousMaterializedVisibility(world, id, out _);

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
            // 禁止世界其它地点的 NPC／Squad 成员落到同一张图（含开局荒村）
            // WorldPresence、仅LocationId 的场NPC（守卫／商人等）仍走下方地点过滤
            if (StrategicWorldSitePopulationService.TryResolvePartyFocusSite(world, out var siteFocus) &&
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
                if (wp.Mode == PartyWorldPresenceMode.AtWorldPosition && wp.HasContinuousWorldPosition)
                {
                    if (world.ContinuousOutdoorMaterialization.IsMaterialized(id))
                        return true;
                    return false;
                }

                if (wp.Mode == PartyWorldPresenceMode.InEncounter)
                    return false;

                // Continuous Outdoor：runtime 的 materialize 集合就是「物理在当前 loaded scope」的权威，
                // 与 legacy map population materialization 同义；其中包含由当前
                // SquadWorldMotion / Site context 掌权的驻守成员。
                // 必须在下方「残留 AtSite presence」守卫之前放行，否则会出现
                // 「Expected=N Materialized=N Views=N-1」——materialized 却永远没有 EntityView。
                if (wp.Mode == PartyWorldPresenceMode.AtSite &&
                    world.Strategic?.Sites != null &&
                    world.Strategic.Sites.TryGet(wp.SiteId, out var materializedSite) &&
                    materializedSite != null &&
                    WorldSiteOutdoorMigrationPolicy.UsesContinuousOutdoorSurface(materializedSite) &&
                    world.ContinuousOutdoorMaterialization.IsMaterialized(id))
                    return true;

                // Legacy AtSite residue 不得凭 SiteId 误进任意 LocalMap。
                if (IsTravelingSquadMember(world, id))
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
                        return true;
                    return false;
                }
            }

            if (!entity.TryGet<EntityLocationComponent>(out var loc) || !loc.HasLocation)
            {
                if (IsCaveBoundNpc(entity) && !world.LocalMap.IsInInterior)
                    return false;
                // 有宏Presence 但无地点、又未过 WorldSite 硬门不显
                if (world.WorldPresence != null && world.WorldPresence.TryGet(id, out _))
                    return false;
                return false;
            }

            // 地点不在当前地点表（例如已从荒村切到保底节点）：必须隐藏，禁止残留旧场景 NPC
            if (!world.LocalPlaces.TryGet(loc.LocationId, out var place))
                return false;

            return IsLocationOnActiveMap(world, place);
        }

        static bool IsEntityVisibleInSeparateSpace(SimulationWorld world, EntityId id, Entity entity)
        {
            var session = world.LocalMap;
            var activeMap = session.ActiveMapLayoutId;

            // Occupant authority first.
            if (session.ContainsOccupant(id))
                return true;

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
        /// Exact Continuous Outdoor visibility gate shared by gameplay and materialization
        /// diagnostics. A true result still flows through EntityViewSpawner, never a second spawner.
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
