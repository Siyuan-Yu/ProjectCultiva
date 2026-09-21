using System;
using System.Collections.Generic;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Npc;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    public sealed class StrategicCharacterRosterRow
    {
        public EntityId CharacterId;
        public string DisplayName = string.Empty;
        public string FactionId = string.Empty;
        public string LifeStateLabel = string.Empty;
        public string SiteId = string.Empty;
        public string SiteLabel = string.Empty;
        /// <summary>玩家看到的行标签（PlayerParty member：canonical party location；其余：SiteLabel）。</summary>
        public string LocationLabel = string.Empty;
        public string ArmyId = string.Empty;
        public bool IsGrouped;
    }

    /// <summary>Host ?????????????????? Domain??</summary>
    public static class HostStrategicRosterQueries
    {
        public static string ResolvePlayerFactionId(
            SimulationWorld world,
            IReadOnlyList<EntityId> partyCharacterIds)
        {
            if (!string.IsNullOrEmpty(world?.Strategic?.PlayerFactionId))
                return world.Strategic.PlayerFactionId;
            var fromParty = HousingAssignmentService.ResolvePlayerFactionId(world, partyCharacterIds);
            if (!string.IsNullOrEmpty(fromParty))
                return fromParty;
            return StrategicFactionCatalog.PlayerFactionId;
        }

        public static void CollectPlayerCharacters(
            SimulationWorld world,
            string playerFactionId,
            IReadOnlyList<EntityId> partyCharacterIds,
            List<StrategicCharacterRosterRow> into,
            PlayerPartyRuntime partyRuntime = null)
        {
            into.Clear();
            if (world == null || into == null)
                return;

            var seen = new HashSet<ulong>();
            if (partyCharacterIds != null)
            {
                for (var i = 0; i < partyCharacterIds.Count; i++)
                    TryAddCharacter(world, playerFactionId, partyCharacterIds[i], into, seen, partyRuntime);
            }

            foreach (var entity in world.Entities.All)
            {
                if (entity == null)
                    continue;
                if (!entity.TryGet<FactionMembershipComponent>(out var mem) || !mem.IsAffiliated)
                    continue;
                if (!string.IsNullOrEmpty(playerFactionId) &&
                    !string.Equals(mem.FactionId, playerFactionId, StringComparison.Ordinal))
                    continue;
                TryAddCharacter(world, playerFactionId, entity.Id, into, seen, partyRuntime);
            }
        }

        static void TryAddCharacter(
            SimulationWorld world,
            string playerFactionId,
            EntityId id,
            List<StrategicCharacterRosterRow> into,
            HashSet<ulong> seen,
            PlayerPartyRuntime partyRuntime)
        {
            if (id.IsNone || seen.Contains(id.Value))
                return;
            if (!world.Entities.TryGet(id, out var entity) || entity == null)
                return;
            if (entity.TryGet<LifecycleComponent>(out var life))
            {
                if (life.IsRemoved)
                    return;
                if (life.IsDead && !CombatLifeStateService.HasVisibleCorpse(entity))
                    return;
            }
            if (!string.IsNullOrEmpty(playerFactionId) &&
                entity.TryGet<FactionMembershipComponent>(out var mem) &&
                mem.IsAffiliated &&
                !string.Equals(mem.FactionId, playerFactionId, StringComparison.Ordinal))
                return;

            seen.Add(id.Value);
            var row = new StrategicCharacterRosterRow
            {
                CharacterId = id,
                DisplayName = string.IsNullOrWhiteSpace(entity.DisplayName) ? id.ToString() : entity.DisplayName,
                FactionId = entity.TryGet<FactionMembershipComponent>(out var fm) && fm.IsAffiliated
                    ? fm.FactionId
                    : string.Empty,
                LifeStateLabel = CombatLifeStateService.FormatLifeStateWithCountdown(world, entity) ?? "存活"
            };
            var currentParty = partyRuntime ?? world.Strategic.PlayerPartyContext;
            if (currentParty?.IsMember(id) != true &&
                world.Strategic.Squads.TryGetForCharacter(id, out var army) &&
                world.Strategic.SquadWorldMotions.TryGet(army.SquadId, out var groupMotion) &&
                SquadWorldMotionService.IsActiveNpcSquadAuthority(world, army, groupMotion))
            {
                row.ArmyId = army.SquadId;
                row.IsGrouped = true;
            }

            // CORRECTION V1（roster “?”）：PlayerParty member（非 FormalArmy）的位置不是 individual
            // Site-only query（ArmyService.ResolveCharacterFormationLocationId 是旧 FormalArmy/individual
            // presence query，对 canonical party 位置会返回空 → “?”）。改用 PlayerPartyWorldLocationQuery：
            // AtWorldSite → row.SiteId = resolved Site（可 focus）；AtWorldPosition（Wilderness）→
            // SiteId 留空 + LocationLabel = Hex 标签（正常状态，不是 Unknown）。
            if (partyRuntime != null &&
                partyRuntime.IsMember(id) &&
                !row.IsGrouped &&
                PlayerPartyWorldLocationQuery.TryResolve(world, partyRuntime, out var resolved) &&
                resolved.HasValue)
            {
                if (resolved.LocationKind == PlayerPartyLocationKind.AtWorldSite &&
                    !string.IsNullOrEmpty(resolved.SiteId))
                {
                    row.SiteId = resolved.SiteId;
                    row.SiteLabel = ResolveSiteLabel(world, resolved.SiteId);
                    row.LocationLabel = row.SiteLabel;
                }
                else
                {
                    row.SiteId = string.Empty;
                    row.SiteLabel = "?";
                    row.LocationLabel = DescribeHexLabel(world, resolved.DerivedHex);
                }
            }
            else
            {
                if (CharacterWorldPresenceQuery.TryDescribe(
                        world, id, out var state, out var siteId, out var worldHex, out _) &&
                    state == CharacterWorldPresenceQuery.PresenceState.AtWorldSite &&
                    !string.IsNullOrEmpty(siteId))
                {
                    row.SiteId = siteId;
                    row.SiteLabel = ResolveSiteLabel(world, siteId);
                    row.LocationLabel = row.SiteLabel;
                }
                else if (CharacterWorldPresenceQuery.TryGetWorldHex(world, id, out worldHex))
                {
                    row.SiteId = string.Empty;
                    row.SiteLabel = string.Empty;
                    row.LocationLabel = DescribeHexLabel(world, worldHex);
                }
                else
                {
                    row.SiteId = string.Empty;
                    row.SiteLabel = "?";
                    row.LocationLabel = "?";
                }
            }

            into.Add(row);
        }

        public static string ResolveSiteLabel(SimulationWorld world, string siteId) =>
            ResolveNodeLabel(world, siteId);

        public static string ResolveNodeLabel(SimulationWorld world, string siteId)
        {
            if (string.IsNullOrEmpty(siteId))
                return "?";
            if (world?.Strategic?.Sites != null &&
                world.Strategic.Sites.TryGet(siteId, out var site) &&
                site != null &&
                !string.IsNullOrEmpty(site.DisplayName))
                return site.DisplayName;
            return siteId;
        }

        public static string DescribeHexLabel(SimulationWorld world, HexCoord hex)
        {
            if (world?.Strategic?.Sites != null &&
                world.Strategic.Sites.TryGetAtHex(hex, out var site) &&
                site != null &&
                !string.IsNullOrEmpty(site.DisplayName))
                return site.DisplayName;
            return hex.ToString();
        }

    }
}
