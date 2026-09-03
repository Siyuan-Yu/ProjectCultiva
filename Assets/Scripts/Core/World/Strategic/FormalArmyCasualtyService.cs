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
    /// 非战略 Encounter 的 FormalArmy 成员伤亡交接。
    /// 战略 Encounter participant 必须由调用方先交给 StrategicEncounterSpawner；仅当其
    /// 返回未处理时，才允许在这里立即解除 Army membership 并转为独立 StrategicResidual。
    /// </summary>
    public static class FormalArmyCasualtyService
    {
        public static bool TryHandleNonEncounterDefeat(
            SimulationWorld world,
            EntityId characterId)
        {
            if (world?.Strategic?.FormalArmies == null || characterId.IsNone ||
                !StrategicResidualPresenceService.IsResidualLifeCandidate(world, characterId) ||
                !ArmyService.TryGetArmyForCharacter(world, characterId, out var army) || army == null ||
                !army.WorldMotion.HasPosition)
                return false;

            var oldArmyId = army.ArmyId;
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

            return detachSuccess && !stillInFormalArmy && residualCandidate;
        }

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
