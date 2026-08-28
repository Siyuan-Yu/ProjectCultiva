using System;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Hex ??????????? / ?? / Presence ????? EncounterHex?</summary>
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

            if (ArmyStackAdapter.TryGetFormalArmy(world, stack, out var formal) && formal != null &&
                formal.UsesHexStrategicPosition)
            {
                SetBattleAnchorHex(snap, formal.CurrentHex);
                return;
            }

            if (TryResolveHexForSite(world, stack.SiteId, out var nodeHex))
                SetBattleAnchorHex(snap, nodeHex);
            else
                ClearBattleAnchorHex(snap);
        }

        public static void ApplyFormalArmyBattleAnchor(
            SimulationWorld world,
            BattleParticipantSnapshot snap,
            FormalArmy army)
        {
            if (snap == null || army == null || !army.UsesHexStrategicPosition)
            {
                ClearBattleAnchorHex(snap);
                return;
            }

            SetBattleAnchorHex(snap, army.CurrentHex);
        }

        public static void ParkArmyAtBattleAnchor(
            SimulationWorld world,
            FormalArmy army,
            BattleParticipantSnapshot snap)
        {
            if (army == null || snap == null)
                return;

            if (TryResolveParkingHex(world, army, snap, out var hex))
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
                return;
            }

            army.State = FormalArmyState.Idle;
        }

        public static void ParkStackAtBattleAnchor(
            SimulationWorld world,
            ArmyStack stack,
            BattleParticipantSnapshot snap)
        {
            if (stack == null || snap == null)
                return;

            if (TryResolveParkingHex(world, null, snap, out var hex))
                stack.SiteId = ResolveSiteIdForHex(world, hex, stack.SiteId);
        }

        public static void PlacePresenceAtBattleAnchor(
            SimulationWorld world,
            WorldAgentPresence wp,
            BattleParticipantSnapshot snap)
        {
            if (wp == null || snap == null)
                return;

            if (TryResolveParkingHex(world, null, snap, out var hex))
            {
                wp.SetAtHex(hex);
                return;
            }

            wp.Mode = PartyWorldPresenceMode.AtSite;
            wp.SiteId = ResolveSiteIdForHex(world, default, wp.SiteId);
        }

        public static bool TryDetectHexContact(
            SimulationWorld world,
            FormalArmy pursuer,
            FormalArmy target)
        {
            if (world == null || pursuer == null || target == null)
                return false;

            return BattleEngagementTriggerService.TryDetectEngagementContact(
                world, pursuer, target);
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
            if (TryGetBattleAnchorHex(snap, out hex) && world.HexWorld.Contains(hex))
                return true;
            if (army != null && army.UsesHexStrategicPosition)
            {
                hex = army.CurrentHex;
                return world.HexWorld.Contains(hex);
            }

            return false;
        }
    }
}
