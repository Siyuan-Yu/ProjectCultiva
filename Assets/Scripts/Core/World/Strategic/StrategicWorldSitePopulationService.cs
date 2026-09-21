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

            // Residual physical authority is personal presence, regardless of legacy organization.
            if (IsPersonalResidualPresentAtSite(world, characterId, site))
                return true;

            if (IsArmyMemberPhysicallyAtSite(world, characterId, site))
                return true;

            return IsPersonalResidentAtSite(world, characterId, site);
        }

        /// <summary>
        /// Incapacitated / visible corpse at a Site is represented by its own AtSite presence.
        /// Squad and LegacyArmy membership remain intact but cannot deny this spatial authority.
        /// </summary>
        public static bool IsPersonalResidualPresentAtSite(
            SimulationWorld world,
            EntityId characterId,
            WorldSite site) =>
            TryResolvePersonalResidualAtSite(world, characterId, site, out _);

        public static bool TryResolvePersonalResidualAtSite(
            SimulationWorld world,
            EntityId characterId,
            WorldSite site,
            out string rejectionReason)
        {
            rejectionReason = string.Empty;
            if (world == null || site == null || characterId.IsNone)
            {
                rejectionReason = "MissingWorldCharacterOrSite";
                return false;
            }
            if (!world.Entities.TryGet(characterId, out var entity) || entity == null)
            {
                rejectionReason = "EntityMissing";
                return false;
            }
            if (CombatLifeStateService.ShouldHideFromSpawn(entity))
            {
                rejectionReason = "RemovedOrHidden";
                return false;
            }
            if (!StrategicResidualPresenceService.IsResidualLifeCandidate(world, characterId))
            {
                rejectionReason = "NotResidualLifeCandidate";
                return false;
            }
            if (world.WorldPresence == null ||
                !world.WorldPresence.TryGet(characterId, out var presence) ||
                presence == null)
            {
                rejectionReason = "WorldPresenceMissing";
                return false;
            }
            if (presence.Mode != PartyWorldPresenceMode.AtSite)
            {
                rejectionReason = "ReturnedMode=" + presence.Mode;
                return false;
            }
            if (!string.Equals(presence.SiteId, site.SiteId, StringComparison.Ordinal))
            {
                rejectionReason = "DifferentSite=" + (presence.SiteId ?? string.Empty);
                return false;
            }

            return true;
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
                    var faction = CharacterStrategicQuery.ResolveFactionId(world, id);
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
        /// 解析应在 WorldSite LocalMap 出现的 Character
        /// （personal resident / residual + physically present FormalArmy member，按 CharacterId 去重）。
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
                    if (id.IsNone || seen.Contains(id.Value))
                        continue;
                    if (IsPersonalResidentAtSite(world, id, site) && seen.Add(id.Value))
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
                    if (seen.Contains(id.Value))
                        continue;
                    if (IsPersonalResidentAtSite(world, id, site) && seen.Add(id.Value))
                        into.Add(id);
                }
            }
        }

        static void CollectArmyMemberIdsAtSite(
            SimulationWorld world, WorldSite site, List<EntityId> into, HashSet<ulong> seen)
        {
            if (world?.Strategic?.Squads == null) return;
            foreach (var pair in world.Strategic.Squads.Squads)
            {
                var squad = pair.Value;
                if (squad == null || !world.Strategic.SquadWorldMotions.TryGet(squad.SquadId, out var motion) ||
                    !SquadWorldMotionService.IsActiveNpcSquadAuthority(world, squad, motion) ||
                    !string.Equals(motion.SiteId, site.SiteId, StringComparison.Ordinal)) continue;
                for (var i = 0; i < squad.MemberCharacterIds.Count; i++)
                {
                    var memberId = new EntityId(squad.MemberCharacterIds[i]);
                    if (memberId.IsNone || seen.Contains(memberId.Value) ||
                        !LingeringBattlefieldPartyService.IsLivingForMacroOrder(world, memberId) ||
                        !world.Entities.TryGet(memberId, out var entity) || entity == null ||
                        CombatLifeStateService.ShouldHideFromSpawn(entity)) continue;
                    if (seen.Add(memberId.Value)) into.Add(memberId);
                }
            }
        }

        static bool IsArmyMemberPhysicallyAtSite(
            SimulationWorld world, EntityId characterId, WorldSite site)
        {
            if (!CharacterStrategicQuery.TryGetSquad(world, characterId, out var squad) || squad == null ||
                !world.Strategic.SquadWorldMotions.TryGet(squad.SquadId, out var motion) ||
                !SquadWorldMotionService.IsActiveNpcSquadAuthority(world, squad, motion) ||
                !string.Equals(motion.SiteId, site.SiteId, StringComparison.Ordinal)) return false;
            return LingeringBattlefieldPartyService.IsLivingForMacroOrder(world, characterId);
        }

        static bool IsPersonalResidentAtSite(
            SimulationWorld world,
            EntityId characterId,
            WorldSite site)
        {
            if (world == null || site == null || characterId.IsNone ||
                !world.Entities.TryGet(characterId, out var entity) || entity == null ||
                CombatLifeStateService.ShouldHideFromSpawn(entity))
                return false;

            if (world.WorldPresence == null ||
                !world.WorldPresence.TryGet(characterId, out var presence) ||
                presence == null)
                return false;

            if (presence.Mode == PartyWorldPresenceMode.InEncounter ||
                presence.Mode == PartyWorldPresenceMode.AtHex)
                return false;

            if (presence.Mode != PartyWorldPresenceMode.AtSite)
                return false;

            if (!string.Equals(presence.SiteId, site.SiteId, StringComparison.Ordinal))
                return false;

            // A residual uses personal AtSite presence even while legacy membership remains.
            if (StrategicResidualPresenceService.IsResidualLifeCandidate(world, characterId))
                return true;

            // Living Squad members with group motion are projected by the group authority pass.
            return !SquadWorldMotionService.OwnsCharacter(world, characterId);
        }
    }
}
