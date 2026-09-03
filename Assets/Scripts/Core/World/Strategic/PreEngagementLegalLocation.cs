using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>接战前最后一个合法世界位置（Retreat V1 真源）。</summary>
    public sealed class PreEngagementLegalLocation
    {
        public FormalArmyLocationKind ArmyLocationKind { get; set; } = FormalArmyLocationKind.Unknown;
        public PlayerPartyLocationKind PartyLocationKind { get; set; }
        public string SiteId { get; set; } = string.Empty;
        public float WorldX { get; set; }
        public float WorldY { get; set; }
        public int HexQ { get; set; } = ArmyHexBattleAnchorService.InvalidHexComponent;
        public int HexR { get; set; } = ArmyHexBattleAnchorService.InvalidHexComponent;
        public bool IsPlayerParty { get; set; }

        public bool HasHex =>
            HexQ != ArmyHexBattleAnchorService.InvalidHexComponent &&
            HexR != ArmyHexBattleAnchorService.InvalidHexComponent;

        public HexCoord Hex => new HexCoord(HexQ, HexR);

        public static PreEngagementLegalLocation CaptureFormalArmy(SimulationWorld world, FormalArmy army)
        {
            var loc = new PreEngagementLegalLocation();
            if (world == null || army?.WorldMotion == null || !army.WorldMotion.HasPosition)
                return loc;

            var motion = army.WorldMotion;
            loc.ArmyLocationKind = motion.LocationKind;
            loc.SiteId = motion.SiteId ?? string.Empty;
            loc.WorldX = motion.WorldPosition.X;
            loc.WorldY = motion.WorldPosition.Y;
            loc.HexQ = motion.CurrentHex.Q;
            loc.HexR = motion.CurrentHex.R;
            return loc;
        }

        public static PreEngagementLegalLocation CapturePlayerParty(SimulationWorld world, PlayerPartyRuntime party)
        {
            var loc = new PreEngagementLegalLocation { IsPlayerParty = true };
            if (world?.PlayerPartyTravel == null ||
                !PlayerPartyWorldLocationQuery.TryResolve(world, party, out var resolved) ||
                !resolved.HasValue)
                return loc;

            loc.PartyLocationKind = resolved.LocationKind;
            loc.SiteId = resolved.SiteId ?? string.Empty;
            loc.WorldX = resolved.WorldPosition.X;
            loc.WorldY = resolved.WorldPosition.Y;
            loc.HexQ = resolved.DerivedHex.Q;
            loc.HexR = resolved.DerivedHex.R;
            return loc;
        }

        public void ApplyRetreatToFormalArmy(SimulationWorld world, FormalArmy army)
        {
            if (world == null || army?.WorldMotion == null || !HasHex)
                return;

            var hexSize = world.HexWorld != null && world.HexWorld.HexSize > 0f
                ? world.HexWorld.HexSize
                : 1f;

            army.WorldMotion.ClearTravel();
            army.WorldMotion.ClearOrderTarget();

            if (ArmyLocationKind == FormalArmyLocationKind.AtWorldSite &&
                !string.IsNullOrEmpty(SiteId) &&
                world.Strategic.Sites.TryGet(SiteId, out var site) &&
                site != null)
            {
                army.WorldMotion.SetAtWorldSite(SiteId, site.PresenceHex, hexSize);
            }
            else
            {
                army.WorldMotion.SetWorldPositionInternal(new WorldVec2(WorldX, WorldY), Hex);
            }

            army.State = FormalArmyState.Idle;
            ArmyPresenceAdapter.SyncFromArmy(world, army);
            ArmyStackAdapter.SyncAllLinkedStacksFromFormalArmies(world);
        }

        public void ApplyRetreatToPlayerParty(SimulationWorld world, PlayerPartyRuntime party)
        {
            if (world?.PlayerPartyTravel == null || !HasHex)
                return;

            var motion = world.PlayerPartyTravel;
            var hexSize = world.HexWorld != null && world.HexWorld.HexSize > 0f
                ? world.HexWorld.HexSize
                : 1f;

            motion.CancelAutoTravelPreservePosition();

            if (PartyLocationKind == PlayerPartyLocationKind.AtWorldSite &&
                !string.IsNullOrEmpty(SiteId))
            {
                if (world.Strategic.Sites.TryGet(SiteId, out var site) && site != null)
                    motion.SetAtWorldSite(SiteId, site.PresenceHex, hexSize);
            }
            else
            {
                motion.SetWorldPositionInternal(new WorldVec2(WorldX, WorldY), Hex);
            }

            if (party != null)
                PlayerPartyTransitionMembership.CaptureTravelingMembersForPartyTransition(world, party);
        }
    }
}
