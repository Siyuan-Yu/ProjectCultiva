using System.Diagnostics;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// 非战略 Encounter 的 Legacy FormalArmy adapter 成员伤亡空间交接。
    /// 不改变 Squad/LegacyArmy membership；已有个人空间时严格保留，缺失时才从该 Army
    /// 自己的 motion 做一次兼容修复。
    /// </summary>
    public static class FormalArmyCasualtyService
    {
        public static bool TryHandleNonEncounterDefeat(
            SimulationWorld world,
            EntityId characterId)
        {
            if (ResidualSpatialAuthorityService.TryResolveStableResidualSpatialAuthority(
                    world, characterId, out _))
                return true;
            return TryDetachNonEncounterDefeat(world, characterId, out _);
        }

        /// <summary>
        /// 带倒下瞬间 LocalMap 坐标的版本。先冻结角色自己的精确点；不得让 legacy Army
        /// anchor 覆盖 personal placement。
        /// </summary>
        public static bool TryHandleNonEncounterDefeat(
            SimulationWorld world,
            EntityId characterId,
            float localX,
            float localZ,
            WildernessLocalWorldProjection.WildernessLocalMapBounds? wildernessBounds,
            WorldSiteSpatialMapping.WorldSiteLocalMapBounds? siteBounds)
        {
            var formerArmyId = ArmyService.TryGetArmyForCharacter(world, characterId, out var priorArmy) &&
                                priorArmy != null
                ? priorArmy.ArmyId
                : string.Empty;
            // Freeze the character's own mapped point before consulting the legacy army anchor.
            // This does not detach Squad/LegacyArmy membership.
            var precisePlaced = LocalCombatCasualtyHandoffService
                .TryPlacePreciseResidualFromLoadedLocalPosition(
                    world,
                    characterId,
                    localX,
                    localZ,
                    wildernessBounds,
                    siteBounds);
            var handled = precisePlaced ||
                          ResidualSpatialAuthorityService.TryResolveStableResidualSpatialAuthority(
                              world, characterId, out _) ||
                          TryDetachNonEncounterDefeat(world, characterId, out formerArmyId);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogPrecision(world, characterId, formerArmyId, localX, localZ, precisePlaced);
#endif
            return handled;
        }

        static bool TryDetachNonEncounterDefeat(
            SimulationWorld world,
            EntityId characterId,
            out string formerArmyId)
        {
            formerArmyId = string.Empty;
            if (world?.Strategic?.FormalArmies == null || characterId.IsNone ||
                !StrategicResidualPresenceService.IsResidualLifeCandidate(world, characterId) ||
                !ArmyService.TryGetArmyForCharacter(world, characterId, out var army) || army == null ||
                !army.WorldMotion.HasPosition)
                return false;

            var oldArmyId = army.ArmyId;
            formerArmyId = oldArmyId;
            var oldMemberCount = army.MemberCharacterIds.Count;
            var armyMotionKind = army.WorldMotion.LocationKind;
            var armyHex = army.WorldMotion.CurrentHex;
            var hadPresentationOverride = false;
            if (world.Entities.TryGet(characterId, out var entity) && entity != null &&
                entity.TryGet<EntityLocationComponent>(out var location) && location != null)
                hadPresentationOverride = location.HasPresentationOverride;
            var wasLocalOccupant = world.LocalMap != null && world.LocalMap.ContainsOccupant(characterId);

            var detachSuccess = ArmyService.DetachNonLivingMemberAtCurrentArmyLocation(
                world,
                army,
                characterId);
            var stillInFormalArmy = ArmyService.TryGetArmyForCharacter(world, characterId, out _);
            var residualCandidate = StrategicResidualPresenceService.IsStrategicResidualCandidate(
                world,
                characterId);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogHandoff(
                world,
                characterId,
                oldArmyId,
                oldMemberCount,
                armyMotionKind,
                armyHex,
                hadPresentationOverride,
                wasLocalOccupant,
                detachSuccess,
                stillInFormalArmy,
                residualCandidate);
#endif

            return detachSuccess &&
                   StrategicResidualPresenceService.IsResidualLifeCandidate(world, characterId);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static void LogPrecision(
            SimulationWorld world,
            EntityId characterId,
            string formerArmyId,
            float localX,
            float localZ,
            bool precisePlaced)
        {
            var surface = "None";
            var siteId = string.Empty;
            var residualHex = "(none)";
            var hasContinuous = false;
            var precise = "(none)";
            if (LoadedLocalMapBelongingQuery.TryResolveLoadedLocalMap(world, out var loaded))
            {
                surface = loaded.Kind.ToString();
                siteId = loaded.Site?.SiteId ?? string.Empty;
            }
            if (StrategicResidualPresenceService.TryGetResidualHex(world, characterId, out var hex))
                residualHex = hex.ToString();
            if (world.WorldPresence.TryGet(characterId, out var presence) && presence != null)
            {
                hasContinuous = presence.HasContinuousWorldPosition;
                if (hasContinuous)
                    precise = presence.ContinuousWorldPosition.ToString();
            }

            Debug.WriteLine(
                "[LocalResidualPrecision]" +
                " Entity=" + characterId +
                " FormerArmyId=" + formerArmyId +
                " Surface=" + surface +
                " SiteId=" + siteId +
                " GotLocal=true" +
                " Local=(" + localX.ToString("0.###") + "," + localZ.ToString("0.###") + ")" +
                " ResidualHex=" + residualHex +
                " HasContinuousWorldPosition=" + hasContinuous +
                " PreciseWorld=" + precise +
                " PrecisePlaced=" + precisePlaced);
        }
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static void LogHandoff(
            SimulationWorld world,
            EntityId characterId,
            string oldArmyId,
            int oldMemberCount,
            FormalArmyLocationKind armyMotionKind,
            HexCoord armyHex,
            bool hadPresentationOverride,
            bool wasLocalOccupant,
            bool detachSuccess,
            bool stillInFormalArmy,
            bool residualCandidate)
        {
            var name = characterId.ToString();
            var lifeState = "(entity missing)";
            if (world.Entities.TryGet(characterId, out var entity) && entity != null)
            {
                name = string.IsNullOrEmpty(entity.DisplayName) ? characterId.ToString() : entity.DisplayName;
                lifeState = CombatLifeStateService.ResolveLifeStateLabel(entity);
            }

            var loadedKind = "None";
            var loadedSiteId = string.Empty;
            var loadedWildernessHex = "(none)";
            if (LoadedLocalMapBelongingQuery.TryResolveLoadedLocalMap(world, out var loaded))
            {
                loadedKind = loaded.Kind.ToString();
                loadedSiteId = loaded.Site?.SiteId ?? string.Empty;
                loadedWildernessHex = loaded.WildernessHex.ToString();
            }

            var residualHex = StrategicResidualPresenceService.TryGetResidualHex(
                world,
                characterId,
                out var hex)
                ? hex.ToString()
                : "(none)";
            var visibleAfterHandoff = LoadedStrategicPopulationQuery
                .IsMaterializedStrategicCharacterOnLoadedMap(world, characterId);

            Debug.WriteLine(
                "[NonEncounterArmyCasualty]" +
                " EntityId=" + characterId +
                " Name=" + name +
                " OldArmyId=" + oldArmyId +
                " OldArmyMemberCount=" + oldMemberCount +
                " LifeState=" + lifeState +
                " ArmyMotionKind=" + armyMotionKind +
                " ArmyCurrentHex=" + armyHex +
                " LoadedSurfaceKind=" + loadedKind +
                " LoadedSiteId=" + loadedSiteId +
                " LoadedWildernessHex=" + loadedWildernessHex +
                " HadPresentationOverride=" + hadPresentationOverride +
                " WasLocalOccupant=" + wasLocalOccupant +
                " DetachSuccess=" + detachSuccess +
                " StillInFormalArmy=" + stillInFormalArmy +
                " ResidualCandidate=" + residualCandidate +
                " ResidualHex=" + residualHex +
                " VisibleAfterHandoff=" + visibleAfterHandoff);
        }
#endif
    }
}
