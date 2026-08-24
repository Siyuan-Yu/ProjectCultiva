using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Hex 模式接战锚点：战后返回 / 撤退 / Presence 落点均使用 EncounterHex，禁止 RouteAnchor。</summary>
    public static class ArmyHexBattleAnchorService
    {
        public const int InvalidHexComponent = int.MinValue;

        public static bool IsHexAnchorMode(SimulationWorld world) =>
            ArmyHexCommandService.IsHexStrategicActive(world);

        public static bool HasBattleAnchorHex(BattleParticipantSnapshot snap) =>
            snap != null && snap.BattleAnchorHexQ != InvalidHexComponent &&
            snap.BattleAnchorHexR != InvalidHexComponent;

        public static bool TryGetBattleAnchorHex(BattleParticipantSnapshot snap, out HexCoord hex)
        {
            hex = default;
            if (!HasBattleAnchorHex(snap))
                return false;
            hex = new HexCoord(snap.BattleAnchorHexQ, snap.BattleAnchorHexR);
            return true;
        }

        public static void SetBattleAnchorHex(BattleParticipantSnapshot snap, HexCoord hex)
        {
            if (snap == null)
                return;
            snap.BattleAnchorHexQ = hex.Q;
            snap.BattleAnchorHexR = hex.R;
            snap.BattleAnchorRouteId = string.Empty;
            snap.BattleAnchorProgress = -1f;
        }

        public static void ClearBattleAnchorHex(BattleParticipantSnapshot snap)
        {
            if (snap == null)
                return;
            snap.BattleAnchorHexQ = InvalidHexComponent;
            snap.BattleAnchorHexR = InvalidHexComponent;
        }

        public static void ApplyStackBattleAnchor(
            SimulationWorld world,
            BattleParticipantSnapshot snap,
            ArmyStack stack)
        {
            if (snap == null || stack == null)
                return;

            snap.BattleAnchorNodeId = stack.NodeId ?? string.Empty;
            snap.BattleAnchorDestNodeId = stack.DestNodeId ?? string.Empty;
            if (!IsHexAnchorMode(world))
            {
                ClearBattleAnchorHex(snap);
                if (stack.IsRoutePositioned)
                {
                    snap.BattleAnchorRouteId = stack.RouteId ?? string.Empty;
                    snap.BattleAnchorProgress = stack.GetRouteDisplayProgress();
                }
                else
                {
                    snap.BattleAnchorRouteId = string.Empty;
                    snap.BattleAnchorProgress = -1f;
                }

                return;
            }

            snap.BattleAnchorRouteId = string.Empty;
            snap.BattleAnchorProgress = -1f;
            if (ArmyStackAdapter.TryGetFormalArmy(world, stack, out var formal) && formal != null &&
                formal.UsesHexStrategicPosition)
            {
                SetBattleAnchorHex(snap, formal.CurrentHex);
                snap.BattleAnchorNodeId = ResolveSiteIdForHex(world, formal.CurrentHex, snap.BattleAnchorNodeId);
                return;
            }

            if (TryResolveHexForNode(world, stack.NodeId, out var nodeHex))
            {
                SetBattleAnchorHex(snap, nodeHex);
                return;
            }

            ClearBattleAnchorHex(snap);
        }

        public static void ApplyFormalArmyBattleAnchor(
            SimulationWorld world,
            BattleParticipantSnapshot snap,
            FormalArmy army)
        {
            if (snap == null || army == null)
                return;

            snap.BattleAnchorNodeId = army.NodeId ?? string.Empty;
            snap.BattleAnchorDestNodeId = string.Empty;
            snap.BattleAnchorRouteId = string.Empty;
            snap.BattleAnchorProgress = -1f;
            if (!IsHexAnchorMode(world) || !army.UsesHexStrategicPosition)
            {
                ClearBattleAnchorHex(snap);
                return;
            }

            SetBattleAnchorHex(snap, army.CurrentHex);
            snap.BattleAnchorNodeId = ResolveSiteIdForHex(world, army.CurrentHex, snap.BattleAnchorNodeId);
        }

        public static void ParkArmyAtBattleAnchor(
            SimulationWorld world,
            FormalArmy army,
            BattleParticipantSnapshot snap)
        {
            if (army == null || snap == null)
                return;

            army.RemainingTravelTicks = 0;
            army.TravelTotalTicks = 0;
            army.ClearRouteSegment();
            army.RouteId = string.Empty;
            army.RouteAnchorProgress = -1f;
            army.DestNodeId = string.Empty;

            if (IsHexAnchorMode(world) && TryResolveParkingHex(world, army, snap, out var hex))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                var oldHex = army.UsesHexStrategicPosition ? army.CurrentHex : default;
                if (!oldHex.Equals(hex))
                {
                    LingeringExitPositionTrace.LogArmyHexMutation(
                        world,
                        army,
                        oldHex,
                        hex,
                        nameof(ParkArmyAtBattleAnchor),
                        "ParkArmyAtBattleAnchor");
                }
#endif
                ArmyHexTravelService.InitializeArmyAtHex(army, hex);
                army.NodeId = ResolveSiteIdForHex(world, hex, snap.BattleAnchorNodeId ?? army.NodeId);
                return;
            }

            army.State = FormalArmyState.AtNode;
            army.NodeId = snap.BattleAnchorNodeId ?? army.NodeId ?? string.Empty;
        }

        public static void ParkStackAtBattleAnchor(
            SimulationWorld world,
            ArmyStack stack,
            BattleParticipantSnapshot snap)
        {
            if (stack == null || snap == null)
                return;

            if (IsHexAnchorMode(world) && TryResolveParkingHex(world, null, snap, out var hex))
            {
                stack.RouteId = string.Empty;
                stack.RouteAnchorProgress = -1f;
                stack.ClearTravel();
                stack.NodeId = ResolveSiteIdForHex(world, hex, snap.BattleAnchorNodeId ?? stack.NodeId);
                stack.DestNodeId = stack.NodeId;
                return;
            }

            stack.NodeId = snap.BattleAnchorNodeId ?? stack.NodeId ?? string.Empty;
            stack.DestNodeId = snap.BattleAnchorDestNodeId ?? string.Empty;
        }

        public static void PlacePresenceAtBattleAnchor(
            SimulationWorld world,
            WorldAgentPresence wp,
            BattleParticipantSnapshot snap)
        {
            if (wp == null || snap == null)
                return;

            if (IsHexAnchorMode(world) && TryResolveParkingHex(world, null, snap, out var hex))
            {
                wp.SetAtHex(hex);
                return;
            }

            if (!string.IsNullOrEmpty(snap.BattleAnchorRouteId) && snap.BattleAnchorProgress >= 0f)
            {
                wp.Mode = PartyWorldPresenceMode.RouteAnchored;
                wp.RouteId = snap.BattleAnchorRouteId;
                wp.NodeId = snap.BattleAnchorNodeId ?? string.Empty;
                wp.DestNodeId = snap.BattleAnchorDestNodeId ?? string.Empty;
                wp.RouteAnchorProgress = Clamp01(snap.BattleAnchorProgress);
                wp.RemainingTravelTicks = 0;
                wp.TravelTotalTicks = 0;
                wp.ClearRouteSegment();
                return;
            }

            wp.Mode = PartyWorldPresenceMode.AtNode;
            wp.NodeId = snap.BattleAnchorNodeId ?? wp.NodeId ?? string.Empty;
            wp.RouteId = string.Empty;
            wp.DestNodeId = string.Empty;
            wp.RouteAnchorProgress = -1f;
            wp.RemainingTravelTicks = 0;
            wp.TravelTotalTicks = 0;
            wp.ClearRouteSegment();
        }

        public static bool TryDetectHexContact(FormalArmy pursuer, FormalArmy target)
        {
            if (pursuer == null || target == null)
                return false;
            if (!pursuer.UsesHexStrategicPosition || !target.UsesHexStrategicPosition)
                return false;
            if (pursuer.CurrentHex == target.CurrentHex)
                return true;
            return HexMath.Distance(pursuer.CurrentHex, target.CurrentHex) <= 1;
        }

        public static bool TryResolveHexForSite(SimulationWorld world, string siteId, out HexCoord hex)
        {
            hex = default;
            if (world == null || string.IsNullOrEmpty(siteId))
                return false;

            if (world.Strategic.Sites.TryGet(siteId, out var site) && site != null)
            {
                hex = site.AnchorHex;
                return world.HexWorld.Contains(hex);
            }

            return false;
        }

        public static bool TryResolveHexForNode(SimulationWorld world, string nodeId, out HexCoord hex) =>
            TryResolveHexForSite(world, nodeId, out hex);

        public static string ResolveSiteIdForHex(
            SimulationWorld world,
            HexCoord hex,
            string fallbackSiteId)
        {
            if (world?.Strategic?.Sites != null &&
                world.Strategic.Sites.TryGetAtHex(hex, out var site) &&
                site != null &&
                !string.IsNullOrEmpty(site.SiteId))
                return site.SiteId;

            return fallbackSiteId ?? string.Empty;
        }

        static bool TryResolveParkingHex(
            SimulationWorld world,
            FormalArmy army,
            BattleParticipantSnapshot snap,
            out HexCoord hex)
        {
            hex = default;
            // 本场 Participants 锚点优先：第二场 Contact H2 不得被第一场残留 H1 覆盖
            if (TryGetBattleAnchorHex(snap, out hex) && world.HexWorld.Contains(hex))
                return true;
            if (army != null && army.UsesHexStrategicPosition)
            {
                hex = army.CurrentHex;
                return world.HexWorld.Contains(hex);
            }
            if (!IsHexAnchorMode(world) &&
                !string.IsNullOrEmpty(snap.BattleAnchorNodeId) &&
                TryResolveHexForNode(world, snap.BattleAnchorNodeId, out hex))
                return true;
            return false;
        }

        static float Clamp01(float v)
        {
            if (v < 0f)
                return 0f;
            if (v > 1f)
                return 1f;
            return v;
        }
    }
}
