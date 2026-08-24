using System;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Formal Army WorldMap 投影规则（Hex-only）�?/summary>
    public static class ArmyWorldMapPresentation
    {
        public static EntityId ResolvePortraitLeader(FormalArmy army)
        {
            if (army == null || army.LeaderCharacterId.IsNone)
                return EntityId.None;
            return army.LeaderCharacterId;
        }

        public static bool ShouldDrawFormalArmyPortrait(SimulationWorld world, FormalArmy army)
        {
            if (world == null || army == null || string.IsNullOrEmpty(army.ArmyId))
                return false;
            if (!world.Strategic.FormalArmies.TryGet(army.ArmyId, out _))
                return false;
            if (!world.HexWorld.HasGrid)
                return false;
            if (!army.UsesHexStrategicPosition || !world.HexWorld.Contains(army.CurrentHex))
                return false;
            if (ResolvePortraitLeader(army).IsNone)
                return false;
            if (!ArmyPostBattleSyncService.HasMacroOrderLivingMember(world, army))
                return false;
            var leader = ResolvePortraitLeader(army);
            if (!LingeringBattlefieldPartyService.IsLivingForMacroOrder(world, leader))
                return false;
            return army.State == FormalArmyState.Idle ||
                   army.State == FormalArmyState.Garrisoned ||
                   army.State == FormalArmyState.Moving ||
                   army.State == FormalArmyState.Idle ||
                   army.State == FormalArmyState.Moving;
        }

        public static bool TryResolveArmyTravelWorldPoints(
            SimulationWorld world,
            FormalArmy army,
            out float fromX,
            out float fromY,
            out float toX,
            out float toY)
        {
            fromX = fromY = toX = toY = 0f;
            if (!TryResolveArmyWorldPoint(world, army, out fromX, out fromY))
                return false;
            toX = fromX;
            toY = fromY;
            return true;
        }

        public static bool TryResolveArmyWorldPoint(
            SimulationWorld world,
            FormalArmy army,
            out float worldX,
            out float worldY)
        {
            worldX = 0f;
            worldY = 0f;
            if (army.UsesHexStrategicPosition &&
                FormalArmyHexWorldPositionResolver.TryResolve(world, army, out worldX, out worldY))
                return true;

            if (!FormalArmyWorldPositionResolver.TryResolve(world, army, out worldX, out worldY, out var info))
                return false;

            ArmyWorldMapRenderDiagnostics.Record(world, army, info);
            return true;
        }

        public static bool TryResolveArmyWorldPointDetailed(
            SimulationWorld world,
            FormalArmy army,
            out FormalArmyWorldPositionResolver.WorldPositionInfo info) =>
            FormalArmyWorldPositionResolver.TryResolve(
                world,
                army,
                out _,
                out _,
                out info);

        public static bool ShouldDrawIndependentCharacterPortrait(SimulationWorld world, EntityId characterId)
        {
            if (world == null || characterId.IsNone)
                return false;
            if (!world.WorldPresence.TryGet(characterId, out var presence) || presence == null)
                return false;

            if (LingeringBattlefieldPartyService.IsLingeringDowned(world, characterId))
            {
                if (StrategicResidualPresenceService.TryGetResidualHex(world, characterId, out _))
                    return false;
                return true;
            }

            if (ArmyService.TryGetArmyForCharacter(world, characterId, out var army) &&
                army != null &&
                ShouldSuppressIndependentPortraitForFormalArmyMember(world, characterId, army, presence))
                return false;

            if (presence.Mode == PartyWorldPresenceMode.AtSite)
                return false;

            if (presence.Mode == PartyWorldPresenceMode.InEncounter)
                return false;

            return false;
        }

        public static bool IsCharacterGroupedFormalResidentAtSite(SimulationWorld world, EntityId characterId)
        {
            if (!ArmyService.TryGetArmyForCharacter(world, characterId, out var army) || army == null)
                return false;

            if (army.UsesHexStrategicPosition && HexStrategicRuntime.IsActive(world))
            {
                if (!ArmyService.TryResolveArmySiteId(world, army, out var formationSiteId) ||
                    !world.Strategic.Sites.TryGet(formationSiteId, out var formationSite) ||
                    formationSite == null)
                    return false;
                return formationSite.OccupiesHex(army.CurrentHex);
            }

            var siteId = ArmyService.ResolveCharacterSiteId(world, characterId);
            if (!string.IsNullOrEmpty(siteId) &&
                ArmyService.TryResolveArmySiteId(world, army, out var armySiteId))
                return string.Equals(siteId, armySiteId, StringComparison.Ordinal);
            return false;
        }

        static bool ShouldSuppressIndependentPortraitForFormalArmyMember(
            SimulationWorld world,
            EntityId characterId,
            FormalArmy army,
            WorldAgentPresence presence)
        {
            if (army == null || presence == null)
                return false;
            if (LingeringBattlefieldPartyService.IsLingeringDowned(world, characterId))
                return false;
            if (army.State == FormalArmyState.Moving || army.State == FormalArmyState.Moving)
                return true;
            if (presence.Mode == PartyWorldPresenceMode.AtSite)
                return true;
            if (presence.IsCombatPursuing)
                return true;
            return false;
        }
    }
}
