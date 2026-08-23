using System;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Phase B：Formal Army WorldMap 投影规则（EditMode 可测）。位置真源 = FormalArmy.StrategicPosition。
    /// </summary>
    public static class ArmyWorldMapPresentation
    {
        /// <summary>Leader 头像派生源；不引入第二 Portrait 字段。</summary>
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
            if (world.HexWorld.HasGrid)
            {
                if (!army.UsesHexStrategicPosition || !world.HexWorld.Contains(army.CurrentHex))
                    return false;
            }
            else if (army.UsesHexStrategicPosition)
            {
                if (!world.HexWorld.Contains(army.CurrentHex))
                    return false;
            }
            else
            {
                if (string.IsNullOrEmpty(army.NodeId))
                    return false;
                if (!world.WorldGraph.TryGetNode(army.NodeId, out var node) || node == null)
                    return false;
            }
            if (ResolvePortraitLeader(army).IsNone)
                return false;
            if (!ArmyPostBattleSyncService.HasMacroOrderLivingMember(world, army))
                return false;
            var leader = ResolvePortraitLeader(army);
            if (!LingeringBattlefieldPartyService.IsLivingForMacroOrder(world, leader))
                return false;
            return army.State == FormalArmyState.AtNode ||
                   army.State == FormalArmyState.Garrisoned ||
                   army.State == FormalArmyState.OnRoute ||
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
            if (world == null || army == null)
                return false;

            if (army.State == FormalArmyState.OnRoute || army.IsRouteAnchored)
            {
                if (string.IsNullOrEmpty(army.RouteId) ||
                    !world.WorldGraph.TryGetRoute(army.RouteId, out var route) ||
                    route == null)
                    return false;
                if (!world.WorldGraph.TryGetNode(route.FromNodeId, out var fromNode) || fromNode == null)
                    return false;
                if (!world.WorldGraph.TryGetNode(route.ToNodeId, out var toNode) || toNode == null)
                    return false;
                fromX = fromNode.WorldX;
                fromY = fromNode.WorldY;
                toX = toNode.WorldX;
                toY = toNode.WorldY;
                return true;
            }

            return TryResolveArmyWorldPoint(world, army, out fromX, out fromY) &&
                   (toX = fromX) == fromX &&
                   (toY = fromY) == fromY;
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

        /// <summary>带渲染来源元数据的 FormalArmy 大地图坐标解析（真源 = StrategicPosition）。</summary>
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

        /// <summary>
        /// Phase B 正式战略显示：AtNode 驻留的 Player Character 不单独画头像（由 Army 或未组军 Resident 规则处理）。
        /// Phase D：Army OnRoute 时成员 presence 为投影，不单独画独立头像。
        /// 追击／legacy 散装 Travel 时，Formal Army 成员也只显示军团头像（队长派生）。
        /// </summary>
        public static bool ShouldDrawIndependentCharacterPortrait(SimulationWorld world, EntityId characterId)
        {
            if (world == null || characterId.IsNone)
                return false;
            if (!world.WorldPresence.TryGet(characterId, out var presence) || presence == null)
                return false;

            // Hex Residual：聚合 Marker 负责；禁止再画独立头像
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

            if (IsLegacyRoutePresentation(presence))
                return true;

            if (presence.Mode == PartyWorldPresenceMode.AtNode)
                return false;

            if (presence.Mode == PartyWorldPresenceMode.InEncounter && !presence.HasRoutePresentation)
                return false;

            return false;
        }

        public static bool IsCharacterGroupedFormalResidentAtNode(SimulationWorld world, EntityId characterId)
        {
            if (!ArmyService.TryGetArmyForCharacter(world, characterId, out var army) || army == null)
                return false;
            var node = ArmyService.ResolveCharacterNodeId(world, characterId);
            return string.Equals(node, army.NodeId, StringComparison.Ordinal);
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
            if (army.State == FormalArmyState.OnRoute)
                return true;
            if (presence.Mode == PartyWorldPresenceMode.AtNode)
                return true;
            if (presence.IsCombatPursuing)
                return true;
            if (presence.HasRoutePresentation)
                return true;
            return IsLegacyRoutePresentation(presence);
        }

        static bool IsLegacyRoutePresentation(WorldAgentPresence presence)
        {
            if (presence == null)
                return false;
            if (presence.Mode == PartyWorldPresenceMode.Traveling ||
                presence.Mode == PartyWorldPresenceMode.RouteAnchored)
                return true;
            return presence.Mode == PartyWorldPresenceMode.InEncounter && presence.HasRoutePresentation;
        }
    }
}
