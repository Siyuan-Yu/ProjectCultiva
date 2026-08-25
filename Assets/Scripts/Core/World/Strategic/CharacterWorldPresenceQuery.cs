using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Phase 2A：Character → WorldHex 权威查询。
    /// AtSite 存 SiteId，WorldHex 派生自 WorldSite.PresenceHex（不另存可漂移 Hex）。
    /// FormalArmy 成员战略位置跟随 Army.CurrentHex。
    /// </summary>
    public static class CharacterWorldPresenceQuery
    {
        public enum PresenceState
        {
            Unknown = 0,
            AtWorldSite = 1,
            AtWildernessHex = 2,
            FormalArmyMember = 3,
            InEncounter = 4,
        }

        public static bool TryGetWorldHex(SimulationWorld world, EntityId characterId, out HexCoord worldHex)
        {
            worldHex = default;
            if (world == null || characterId.IsNone)
                return false;

            var motion = world.PlayerPartyTravel;
            if (motion != null &&
                motion.HasPosition &&
                IsTravelingMember(motion, characterId))
            {
                if (motion.LocationKind == PlayerPartyLocationKind.AtWorldSite &&
                    !string.IsNullOrEmpty(motion.SiteId) &&
                    world.Strategic.Sites.TryResolveSitePresenceHex(motion.SiteId, out worldHex))
                    return true;

                worldHex = motion.CurrentHex;
                return true;
            }

            if (ArmyService.TryGetArmyForCharacter(world, characterId, out var army) &&
                army != null &&
                army.UsesHexStrategicPosition)
            {
                worldHex = army.CurrentHex;
                return true;
            }

            if (!world.WorldPresence.TryGet(characterId, out var presence) || presence == null)
                return false;

            if (presence.UsesHexPresence)
            {
                worldHex = presence.ResidualHex;
                return true;
            }

            if (presence.Mode == PartyWorldPresenceMode.AtSite &&
                !string.IsNullOrEmpty(presence.SiteId))
                return world.Strategic.Sites.TryResolveSitePresenceHex(presence.SiteId, out worldHex);

            return false;
        }

        public static bool TryGetPartyWorldHex(
            SimulationWorld world,
            PlayerPartyRuntime party,
            out HexCoord worldHex)
        {
            worldHex = default;
            if (world == null || party == null || !party.HasActive)
                return false;
            return TryGetWorldHex(world, party.ActiveCharacterId, out worldHex);
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

            if (ArmyService.TryGetArmyForCharacter(world, characterId, out var army) &&
                army != null &&
                army.UsesHexStrategicPosition)
            {
                state = PresenceState.FormalArmyMember;
                worldHex = army.CurrentHex;
                if (world.Strategic.Sites.TryGetAtHex(worldHex, out var armySite) && armySite != null)
                    siteId = armySite.SiteId;
                localMapLoaded = IsLocalMapLoadedForSite(world, siteId);
                return true;
            }

            if (!world.WorldPresence.TryGet(characterId, out var presence) || presence == null)
                return false;

            if (presence.Mode == PartyWorldPresenceMode.InEncounter)
            {
                state = PresenceState.InEncounter;
                siteId = presence.SiteId ?? string.Empty;
                if (!string.IsNullOrEmpty(siteId))
                    world.Strategic.Sites.TryResolveSitePresenceHex(siteId, out worldHex);
                localMapLoaded = IsLocalMapLoadedForSite(world, siteId);
                return true;
            }

            if (presence.UsesHexPresence)
            {
                state = PresenceState.AtWildernessHex;
                worldHex = presence.ResidualHex;
                if (world.Strategic.Sites.TryGetAtHex(worldHex, out var atHexSite) && atHexSite != null)
                    siteId = atHexSite.SiteId;
                return true;
            }

            if (presence.Mode == PartyWorldPresenceMode.AtSite &&
                !string.IsNullOrEmpty(presence.SiteId))
            {
                state = PresenceState.AtWorldSite;
                siteId = presence.SiteId;
                if (!world.Strategic.Sites.TryResolveSitePresenceHex(siteId, out worldHex))
                    return false;
                localMapLoaded = IsLocalMapLoadedForSite(world, siteId);
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
    }
}
