using XianXia.Core.Domain.Ids;
using XianXia.Core.Exploration;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Character strategic Hex compatibility query. Modern physical authority is an exact
    /// Surface position or Interior EntityLocation. NPC Squad position derives from
    /// SquadWorldMotion; returned Hex values are compatibility metadata only.
    /// </summary>
    public static class CharacterWorldPresenceQuery
    {
        public enum PresenceState
        {
            Unknown = 0,
            AtWorldSite = 1,
            AtWildernessHex = 2,
            SquadMember = 3,
            InEncounter = 4,
            AtWorldPosition = 5,
        }

        public static bool TryGetWorldHex(SimulationWorld world, EntityId characterId, out HexCoord worldHex)
        {
            worldHex = default;
            if (world == null || characterId.IsNone)
                return false;

            if (world.WorldPresence.TryGet(characterId, out var ownedPresence) &&
                ownedPresence != null && ownedPresence.Mode == PartyWorldPresenceMode.InEncounter)
            {
                if (!string.IsNullOrEmpty(ownedPresence.SiteId))
                    return world.Strategic.Sites.TryResolveLegacySitePresenceHex(
                        ownedPresence.SiteId, out worldHex);
                return false;
            }
            if (SeparateSpaceTransitionService.IsOwnedByActiveSeparateSpace(world, characterId))
                return false;

            var motion = world.PlayerPartyTravel;
            if (motion != null &&
                motion.HasPosition &&
                IsTravelingMember(motion, characterId))
            {
                if (motion.LocationKind == PlayerPartyLocationKind.AtWorldSite &&
                    !string.IsNullOrEmpty(motion.SiteId) &&
                    world.Strategic.Sites.TryResolveLegacySitePresenceHex(motion.SiteId, out worldHex))
                    return true;

                worldHex = motion.LegacyCurrentHex;
                return true;
            }

            if (world.Strategic.Squads.TryGetForCharacter(characterId, out var squad) &&
                SquadWorldMotionService.OwnsCharacter(world, characterId) &&
                world.Strategic.SquadWorldMotions.TryGet(squad.SquadId, out var squadMotion) &&
                SquadWorldMotionService.IsActiveNpcSquadAuthority(world, squad, squadMotion))
            {
                worldHex = HexMath.WorldToHex(squadMotion.WorldPosition.X, squadMotion.WorldPosition.Y,
                    world.LegacyHexWorld != null && world.LegacyHexWorld.HexSize > 0f ? world.LegacyHexWorld.HexSize : 1f);
                return true;
            }

            if (!world.WorldPresence.TryGet(characterId, out var presence) || presence == null)
                return false;

            if (presence.UsesHexPresence)
            {
                worldHex = presence.ResidualHex;
                return true;
            }

            if (presence.Mode == PartyWorldPresenceMode.AtWorldPosition &&
                presence.HasContinuousWorldPosition &&
                Finite(presence.WorldPosX) && Finite(presence.WorldPosY))
            {
                worldHex = HexMath.WorldToHex(presence.WorldPosX, presence.WorldPosY,
                    world.LegacyHexWorld != null && world.LegacyHexWorld.HexSize > 0f ? world.LegacyHexWorld.HexSize : 1f);
                return true;
            }

            if (presence.Mode == PartyWorldPresenceMode.AtSite &&
                !string.IsNullOrEmpty(presence.SiteId))
                return world.Strategic.Sites.TryResolveLegacySitePresenceHex(presence.SiteId, out worldHex);

            return false;
        }

        public static bool TryGetPartyWorldHex(
            SimulationWorld world, PlayerPartyRuntime party, out HexCoord worldHex)
        {
            worldHex = default;
            if (world?.PlayerPartyTravel?.HasPosition == true)
            { worldHex = world.PlayerPartyTravel.LegacyCurrentHex; return true; }
            return party != null && party.HasActive && TryGetWorldHex(world, party.ActiveCharacterId, out worldHex);
        }

        public static bool TryDescribe(
            SimulationWorld world,
            EntityId characterId,
            out PresenceState state,
            out string siteId,
            out HexCoord worldHex,
            out bool localMapLoaded)
        {
            state = PresenceState.Unknown;
            siteId = string.Empty;
            worldHex = default;
            localMapLoaded = false;
            if (world == null || characterId.IsNone)
                return false;

            if (world.WorldPresence.TryGet(characterId, out var ownedPresence) &&
                ownedPresence != null && ownedPresence.Mode == PartyWorldPresenceMode.InEncounter)
            {
                state = PresenceState.InEncounter;
                siteId = ownedPresence.SiteId ?? string.Empty;
                if (!string.IsNullOrEmpty(siteId))
                    world.Strategic.Sites.TryResolveLegacySitePresenceHex(siteId, out worldHex);
                localMapLoaded = IsLocalMapLoadedForSite(world, siteId);
                return true;
            }

            if (SeparateSpaceTransitionService.IsOwnedByActiveSeparateSpace(world, characterId))
                return false;

            if (world.Strategic.Squads.TryGetForCharacter(characterId, out var squad) &&
                SquadWorldMotionService.OwnsCharacter(world, characterId) &&
                world.Strategic.SquadWorldMotions.TryGet(squad.SquadId, out var squadMotion) &&
                SquadWorldMotionService.IsActiveNpcSquadAuthority(world, squad, squadMotion))
            {
                state = PresenceState.SquadMember;
                worldHex = HexMath.WorldToHex(squadMotion.WorldPosition.X, squadMotion.WorldPosition.Y,
                    world.LegacyHexWorld != null && world.LegacyHexWorld.HexSize > 0f ? world.LegacyHexWorld.HexSize : 1f);
                siteId = squadMotion.SiteId;
                localMapLoaded = IsLocalMapLoadedForSite(world, siteId);
                return true;
            }

            if (!world.WorldPresence.TryGet(characterId, out var presence) || presence == null)
                return false;

            if (presence.UsesHexPresence)
            {
                state = PresenceState.AtWildernessHex;
                worldHex = presence.ResidualHex;
                if (world.Strategic.Sites.TryGetAtLegacyHex(worldHex, out var atHexSite) && atHexSite != null)
                    siteId = atHexSite.SiteId;
                return true;
            }

            if (presence.Mode == PartyWorldPresenceMode.AtSite &&
                !string.IsNullOrEmpty(presence.SiteId))
            {
                state = PresenceState.AtWorldSite;
                siteId = presence.SiteId;
                if (!world.Strategic.Sites.TryResolveLegacySitePresenceHex(siteId, out worldHex))
                    return false;
                localMapLoaded = IsLocalMapLoadedForSite(world, siteId);
                return true;
            }

            if (presence.Mode == PartyWorldPresenceMode.AtWorldPosition &&
                presence.HasContinuousWorldPosition &&
                Finite(presence.WorldPosX) && Finite(presence.WorldPosY))
            {
                state = PresenceState.AtWorldPosition;
                worldHex = HexMath.WorldToHex(presence.WorldPosX, presence.WorldPosY,
                    world.LegacyHexWorld != null && world.LegacyHexWorld.HexSize > 0f ? world.LegacyHexWorld.HexSize : 1f);
                return true;
            }

            return false;
        }

        static bool IsTravelingMember(PlayerPartyWorldMotion motion, EntityId characterId)
        {
            var members = motion.TravelingMembers;
            if (members == null || members.Count == 0)
                return false;
            for (var i = 0; i < members.Count; i++)
            {
                if (members[i] == characterId)
                    return true;
            }

            return false;
        }

        static bool IsLocalMapLoadedForSite(SimulationWorld world, string siteId)
        {
            if (world?.PartyWorld == null || string.IsNullOrEmpty(siteId))
                return false;
            if (!string.Equals(world.PartyWorld.SiteId, siteId, System.StringComparison.Ordinal))
                return false;
            return !string.IsNullOrEmpty(world.PartyWorld.LocalMapId);
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
