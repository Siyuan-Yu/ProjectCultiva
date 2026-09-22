using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Read-only PlayerParty exact Surface location query.</summary>
    public static class PlayerPartyWorldLocationQuery
    {
        public readonly struct Resolved
        {
            public Resolved(
                PlayerPartyLocationKind locationKind,
                string siteId,
                string surfaceId,
                WorldVec2 worldPosition)
            {
                LocationKind = locationKind;
                SiteId = siteId ?? string.Empty;
                SurfaceId = surfaceId ?? string.Empty;
                WorldPosition = worldPosition;
                HasValue = true;
            }

            public PlayerPartyLocationKind LocationKind { get; }
            public string SiteId { get; }
            public string SurfaceId { get; }
            public WorldVec2 WorldPosition { get; }
            public bool HasValue { get; }
        }

        public static bool TryResolve(
            SimulationWorld world,
            PlayerPartyRuntime party,
            out Resolved resolved,
            bool healDrift = false)
        {
            resolved = default;
            var motion = world?.PlayerPartyTravel;
            if (motion == null)
                return false;
            if (healDrift)
                TryHealStartupOnly(world, party, motion);
            if (!motion.HasPosition ||
                motion.LocationKind != PlayerPartyLocationKind.AtWorldPosition ||
                string.IsNullOrEmpty(motion.SurfaceId) ||
                !Finite(motion.WorldPosition))
                return false;
            resolved = new Resolved(
                motion.LocationKind,
                motion.CurrentOutdoorWorldSiteId,
                motion.SurfaceId,
                motion.WorldPosition);
            return true;
        }

        /// <summary>
        /// Initializes an absent party motion from an exact active-character presence only.
        /// Site-only or grid-only state requires offline conversion.
        /// </summary>
        public static bool TryHealStartupOnly(
            SimulationWorld world,
            PlayerPartyRuntime party,
            PlayerPartyWorldMotion motion)
        {
            if (world == null || motion == null || motion.IsMoving || motion.HasPosition ||
                party == null || !party.HasActive)
                return false;
            var activeId = party.ActiveCharacterId;
            if (!world.WorldPresence.TryGet(activeId, out var presence) || presence == null ||
                presence.Mode != PartyWorldPresenceMode.AtWorldPosition ||
                !presence.HasContinuousWorldPosition ||
                string.IsNullOrEmpty(presence.PersonalSurfaceId) ||
                !world.SurfaceGround.TryGet(presence.PersonalSurfaceId, out var surface) ||
                surface == null ||
                !surface.Contains(presence.WorldPosX, presence.WorldPosY))
                return false;
            motion.SetAtSurfacePosition(surface.SurfaceId, presence.ContinuousWorldPosition);
            motion.SetCurrentOutdoorWorldSiteContext(
                WorldSitePhysicalRegionQuery.ResolveSiteIdOrEmpty(
                    world, presence.ContinuousWorldPosition));
            PlayerPartyTransitionMembership.CaptureTravelingMembersForPartyTransition(world, party);
            return true;
        }

        static bool Finite(WorldVec2 position) =>
            !float.IsNaN(position.X) && !float.IsInfinity(position.X) &&
            !float.IsNaN(position.Y) && !float.IsInfinity(position.Y);
    }

}
