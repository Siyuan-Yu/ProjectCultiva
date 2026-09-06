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
        public bool CanSelectForArmyCreation;
    }

    public sealed class StrategicArmyRosterRow
    {
        public string ArmyId = string.Empty;
        public EntityId LeaderId;
        public string LeaderLabel = string.Empty;
        public int MemberCount;
        public FormalArmyState State;
        public string SiteId = string.Empty;
        public string SiteLabel = string.Empty;
        public string DestHexLabel = string.Empty;
        public int CombatPower;
        public bool IsPlayerFaction;
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
            if (ArmyService.TryGetArmyForCharacter(world, id, out var army) && army != null)
            {
                row.ArmyId = army.ArmyId;
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

            row.CanSelectForArmyCreation =
                !row.IsGrouped &&
                LingeringBattlefieldPartyService.IsLivingForMacroOrder(world, id) &&
                ArmyService.IsEligibleFormalArmyCandidate(world, id, partyRuntime, out _);

            into.Add(row);
        }

        public static void CollectPlayerArmies(
            SimulationWorld world,
            string playerFactionId,
            List<StrategicArmyRosterRow> into)
        {
            into.Clear();
            if (world?.Strategic?.FormalArmies?.Armies == null || into == null)
                return;

            foreach (var kv in world.Strategic.FormalArmies.Armies)
            {
                var army = kv.Value;
                if (army == null)
                    continue;
                if (!string.IsNullOrEmpty(playerFactionId) &&
                    !string.Equals(army.FactionId, playerFactionId, StringComparison.Ordinal))
                    continue;

                ArmyService.TryResolveArmySiteId(world, army, out var siteId);
                var row = new StrategicArmyRosterRow
                {
                    ArmyId = army.ArmyId,
                    LeaderId = army.LeaderCharacterId,
                    MemberCount = army.MemberCharacterIds.Count,
                    State = army.State,
                    SiteId = siteId ?? string.Empty,
                    IsPlayerFaction = true,
                    CombatPower = EstimateArmyPower(world, army)
                };
                if (!row.LeaderId.IsNone &&
                    world.Entities.TryGet(row.LeaderId, out var leader) &&
                    leader != null &&
                    !string.IsNullOrWhiteSpace(leader.DisplayName))
                {
                    row.LeaderLabel = leader.DisplayName;
                }
                else
                {
                    row.LeaderLabel = row.LeaderId.IsNone ? "?" : row.LeaderId.ToString();
                }

                row.SiteLabel = !string.IsNullOrEmpty(row.SiteId)
                    ? ResolveSiteLabel(world, row.SiteId)
                    : DescribeHexLabel(world, army.CurrentHex);
                row.DestHexLabel = DescribeHexLabel(world, army.DestinationHex);
                into.Add(row);
            }

            into.Sort((a, b) => string.CompareOrdinal(a.ArmyId, b.ArmyId));
        }

        public static void CollectUngroupedPlayerCharacters(
            SimulationWorld world,
            string playerFactionId,
            IReadOnlyList<EntityId> partyCharacterIds,
            List<EntityId> into,
            PlayerPartyRuntime partyRuntime = null)
        {
            into.Clear();
            if (world == null || into == null)
                return;

            var rows = new List<StrategicCharacterRosterRow>(32);
            CollectPlayerCharacters(world, playerFactionId, partyCharacterIds, rows, partyRuntime);
            for (var i = 0; i < rows.Count; i++)
            {
                if (!rows[i].IsGrouped && rows[i].CanSelectForArmyCreation)
                    into.Add(rows[i].CharacterId);
            }
        }

        public static void CollectUngroupedCharactersAtSite(
            SimulationWorld world,
            string siteId,
            string factionId,
            IReadOnlyList<EntityId> partyCharacterIds,
            List<EntityId> into,
            PlayerPartyRuntime partyRuntime = null)
        {
            into.Clear();
            if (world == null || into == null || string.IsNullOrEmpty(siteId))
                return;

            var scratchResidents = new List<EntityId>(8);
            var scratchArmies = new List<FormalArmy>(4);
            ArmyService.CollectResidentsAtSite(
                world, siteId, factionId, partyCharacterIds, scratchResidents, scratchArmies, partyRuntime);
            for (var i = 0; i < scratchResidents.Count; i++)
                into.Add(scratchResidents[i]);
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

        public static string DescribeArmyTravel(StrategicArmyRosterRow row)
        {
            if (row == null)
                return string.Empty;
            if (row.State == FormalArmyState.Moving && !string.IsNullOrEmpty(row.DestHexLabel))
                return row.SiteLabel + " ? " + row.DestHexLabel;
            return row.SiteLabel;
        }

        public static int EstimateArmyPower(SimulationWorld world, FormalArmy army)
        {
            if (world == null || army == null)
                return 1;
            var sum = 0;
            for (var i = 0; i < army.MemberCharacterIds.Count; i++)
                sum += CombatPowerCalculator.ForEntity(world, new EntityId(army.MemberCharacterIds[i]));
            return Math.Max(1, sum);
        }
    }
}
