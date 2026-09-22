using XianXia.Core.Domain.Ids;
using XianXia.Core.Exploration;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Resolves current character location without exposing retired grid geometry.
    /// Outdoor results always carry exact position and Surface provenance.
    /// </summary>
    public static class CharacterWorldPresenceQuery
    {
        public enum PresenceState
        {
            Unknown = 0,
            AtWorldSite = 1,
            SquadMember = 2,
            InEncounter = 3,
            AtWorldPosition = 4,
            InSeparateSpace = 5,
        }

        public readonly struct ResolvedPresence
        {
            public ResolvedPresence(
                PresenceState state,
                string siteId,
                string surfaceId,
                WorldVec2 worldPosition,
                bool hasWorldPosition)
            {
                State = state;
                SiteId = siteId ?? string.Empty;
                SurfaceId = surfaceId ?? string.Empty;
                WorldPosition = worldPosition;
                HasWorldPosition = hasWorldPosition;
            }

            public PresenceState State { get; }
            public string SiteId { get; }
            public string SurfaceId { get; }
            public WorldVec2 WorldPosition { get; }
            public bool HasWorldPosition { get; }
        }

        public static bool TryResolve(
            SimulationWorld world,
            EntityId characterId,
            out ResolvedPresence resolved)
        {
            resolved = default;
            if (world == null || characterId.IsNone)
                return false;

            if (CharacterEncounterService.OwnsParticipantSpatialState(world, characterId))
            {
                resolved = new ResolvedPresence(
                    PresenceState.InEncounter, string.Empty, string.Empty, default, false);
                return true;
            }

            if (SeparateSpaceTransitionService.IsOwnedByActiveSeparateSpace(world, characterId))
            {
                resolved = new ResolvedPresence(
                    PresenceState.InSeparateSpace, string.Empty, string.Empty, default, false);
                return true;
            }

            var partyMotion = world.PlayerPartyTravel;
            if (partyMotion != null && partyMotion.HasPosition &&
                IsTravelingMember(partyMotion, characterId))
            {
                resolved = new ResolvedPresence(
                    PresenceState.AtWorldPosition,
                    partyMotion.CurrentOutdoorWorldSiteId,
                    partyMotion.SurfaceId,
                    partyMotion.WorldPosition,
                    true);
                return true;
            }

            if (world.Strategic.Squads.TryGetForCharacter(characterId, out var squad) &&
                SquadWorldMotionService.OwnsCharacter(world, characterId) &&
                world.Strategic.SquadWorldMotions.TryGet(squad.SquadId, out var squadMotion) &&
                SquadWorldMotionService.IsActiveNpcSquadAuthority(world, squad, squadMotion))
            {
                resolved = new ResolvedPresence(
                    PresenceState.SquadMember,
                    squadMotion.SiteId,
                    squadMotion.SurfaceId,
                    squadMotion.WorldPosition,
                    true);
                return true;
            }

            if (!world.WorldPresence.TryGet(characterId, out var presence) || presence == null)
                return false;

            if (presence.Mode == PartyWorldPresenceMode.AtSite &&
                !string.IsNullOrEmpty(presence.SiteId))
            {
                resolved = new ResolvedPresence(
                    PresenceState.AtWorldSite,
                    presence.SiteId,
                    presence.PersonalSurfaceId,
                    presence.ContinuousWorldPosition,
                    presence.HasContinuousWorldPosition);
                return true;
            }

            if (presence.Mode == PartyWorldPresenceMode.AtWorldPosition &&
                presence.HasContinuousWorldPosition &&
                Finite(presence.WorldPosX) && Finite(presence.WorldPosY) &&
                !string.IsNullOrEmpty(presence.PersonalSurfaceId))
            {
                resolved = new ResolvedPresence(
                    PresenceState.AtWorldPosition,
                    string.Empty,
                    presence.PersonalSurfaceId,
                    presence.ContinuousWorldPosition,
                    true);
                return true;
            }

            return false;
        }

        static bool IsTravelingMember(PlayerPartyWorldMotion motion, EntityId characterId)
        {
            var members = motion.TravelingMembers;
            for (var i = 0; i < members.Count; i++)
                if (members[i] == characterId)
                    return true;
            return false;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
