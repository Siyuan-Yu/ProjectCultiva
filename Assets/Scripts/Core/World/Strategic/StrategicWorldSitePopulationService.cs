using System;
using System.Collections.Generic;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// WorldSite LocalMap 人口：按地点物理在场解析 CharacterId（与 EnteringArmy / Focus 分离）。
    /// </summary>
    public static class StrategicWorldSitePopulationService
    {
        public static bool TryResolvePartyFocusSite(SimulationWorld world, out WorldSite site)
        {
            site = null;
            var siteId = world?.PartyWorld?.SiteId;
            if (string.IsNullOrEmpty(siteId) || world.Strategic?.Sites == null)
                return false;
            return world.Strategic.Sites.TryGet(siteId, out site) && site != null;
        }

        public static bool IsCharacterPresentAtWorldSite(
            SimulationWorld world,
            EntityId characterId,
            WorldSite site)
        {
            if (world == null || site == null || characterId.IsNone)
                return false;

            if (IsArmyMemberPhysicallyAtSite(world, characterId, site))
                return true;

            return IsUngroupedResidentAtSite(world, characterId, site);
        }

        public static bool HasFriendlyCharacterPresentAtWorldSite(
            SimulationWorld world,
            IReadOnlyList<EntityId> characterIds,
            WorldSite site)
        {
            if (world == null || site == null || characterIds == null)
                return false;

            var playerFaction = world.Strategic?.PlayerFactionId;
            for (var i = 0; i < characterIds.Count; i++)
            {
                var id = characterIds[i];
                if (id.IsNone)
                    continue;
                if (!string.IsNullOrEmpty(playerFaction))
                {
                    var faction = ArmyService.ResolveCharacterFactionId(world, id);
                    if (!string.IsNullOrEmpty(faction) &&
                        !string.Equals(faction, playerFaction, StringComparison.Ordinal))
                        continue;
                }

                if (IsCharacterPresentAtWorldSite(world, id, site))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 解析应在 WorldSite LocalMap 出现的可控 Character（Resident + 足迹内 FormalArmy 成员，按 CharacterId 去重）。
        /// </summary>
        public static void CollectCharacterIdsPresentAtWorldSite(
            SimulationWorld world,
            WorldSite site,
            IReadOnlyList<EntityId> candidateCharacterIds,
            List<EntityId> into)
        {
            into?.Clear();
            if (world == null || site == null || into == null)
                return;

            var seen = new HashSet<ulong>();
            CollectArmyMemberIdsAtSite(world, site, into, seen);

            if (candidateCharacterIds != null)
            {
                for (var i = 0; i < candidateCharacterIds.Count; i++)
                {
                    var id = candidateCharacterIds[i];
                    if (id.IsNone || !seen.Add(id.Value))
                        continue;
                    if (IsUngroupedResidentAtSite(world, id, site))
                        into.Add(id);
                }
            }
            else if (world.WorldPresence != null)
            {
                foreach (var kv in world.WorldPresence.All)
                {
                    var presence = kv.Value;
                    if (presence == null || presence.EntityId.IsNone)
                        continue;
                    var id = presence.EntityId;
                    if (!seen.Add(id.Value))
                        continue;
                    if (IsUngroupedResidentAtSite(world, id, site))
                        into.Add(id);
                }
            }
        }

        static void CollectArmyMemberIdsAtSite(
            SimulationWorld world,
            WorldSite site,
            List<EntityId> into,
            HashSet<ulong> seen)
        {
            if (world?.Strategic?.FormalArmies?.Armies == null)
                return;

            foreach (var kv in world.Strategic.FormalArmies.Armies)
            {
                var army = kv.Value;
                if (army == null || !IsArmyPhysicallyAtSite(world, army, site))
                    continue;

                for (var i = 0; i < army.MemberCharacterIds.Count; i++)
                {
                    var memberId = new EntityId(army.MemberCharacterIds[i]);
                    if (memberId.IsNone || !seen.Add(memberId.Value))
                        continue;
                    if (!LingeringBattlefieldPartyService.IsLivingForMacroOrder(world, memberId))
                        continue;
                    if (world.Entities.TryGet(memberId, out var entity) &&
                        CombatLifeStateService.ShouldHideFromSpawn(entity))
                        continue;
                    into.Add(memberId);
                }
            }
        }

        static bool IsArmyMemberPhysicallyAtSite(
            SimulationWorld world,
            EntityId characterId,
            WorldSite site)
        {
            if (!ArmyService.TryGetArmyForCharacter(world, characterId, out var army) || army == null)
                return false;
            if (!army.ContainsMember(characterId))
                return false;
            if (!IsArmyPhysicallyAtSite(world, army, site))
                return false;
            return LingeringBattlefieldPartyService.IsLivingForMacroOrder(world, characterId);
        }

        static bool IsArmyPhysicallyAtSite(SimulationWorld world, FormalArmy army, WorldSite site)
        {
            if (army == null || site == null)
                return false;

            if (army.UsesHexStrategicPosition)
                return site.OccupiesHex(army.CurrentHex);

            if (ArmyFormationSitePolicy.TryGetSiteForLegacyNode(world, army.NodeId, out var armySite) &&
                armySite != null)
                return string.Equals(armySite.SiteId, site.SiteId, StringComparison.Ordinal);

            return false;
        }

        static bool IsUngroupedResidentAtSite(
            SimulationWorld world,
            EntityId characterId,
            WorldSite site)
        {
            if (ArmyService.TryGetArmyForCharacter(world, characterId, out _))
                return false;

            if (world?.WorldPresence == null ||
                !world.WorldPresence.TryGet(characterId, out var presence) ||
                presence == null)
                return false;

            if (presence.Mode == PartyWorldPresenceMode.Traveling ||
                presence.Mode == PartyWorldPresenceMode.RouteAnchored ||
                presence.Mode == PartyWorldPresenceMode.InEncounter ||
                presence.Mode == PartyWorldPresenceMode.AtHex)
                return false;

            if (presence.Mode != PartyWorldPresenceMode.AtNode)
                return false;

            if (!ArmyFormationSitePolicy.TryGetSiteForLegacyNode(world, presence.NodeId, out var residentSite) ||
                residentSite == null)
                return false;

            return string.Equals(residentSite.SiteId, site.SiteId, StringComparison.Ordinal);
        }
    }
}
