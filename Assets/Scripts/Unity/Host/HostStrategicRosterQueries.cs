using System;
using System.Collections.Generic;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Npc;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    public sealed class StrategicCharacterRosterRow
    {
        public EntityId CharacterId;
        public string DisplayName = string.Empty;
        public string FactionId = string.Empty;
        public string LifeStateLabel = string.Empty;
        public string NodeId = string.Empty;
        public string NodeLabel = string.Empty;
        public string ArmyId = string.Empty;
        public bool IsGrouped;
        /// <summary>未编组角色是否可勾选「组建军队」（弥留／尸体仅展示，不可选）。</summary>
        public bool CanSelectForArmyCreation;
    }

    public sealed class StrategicArmyRosterRow
    {
        public string ArmyId = string.Empty;
        public EntityId LeaderId;
        public string LeaderLabel = string.Empty;
        public int MemberCount;
        public FormalArmyState State;
        public string NodeId = string.Empty;
        public string NodeLabel = string.Empty;
        public string DestNodeId = string.Empty;
        public string DestNodeLabel = string.Empty;
        public int CombatPower;
        public bool IsPlayerFaction;
    }

    /// <summary>Host 只读：战略层角色／军队列表数据（不写 Domain）。</summary>
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
            List<StrategicCharacterRosterRow> into)
        {
            into.Clear();
            if (world == null || into == null)
                return;

            var seen = new HashSet<ulong>();
            if (partyCharacterIds != null)
            {
                for (var i = 0; i < partyCharacterIds.Count; i++)
                    TryAddCharacter(world, playerFactionId, partyCharacterIds[i], into, seen);
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
                TryAddCharacter(world, playerFactionId, entity.Id, into, seen);
            }
        }

        static void TryAddCharacter(
            SimulationWorld world,
            string playerFactionId,
            EntityId id,
            List<StrategicCharacterRosterRow> into,
            HashSet<ulong> seen)
        {
            if (id.IsNone || seen.Contains(id.Value))
                return;
            if (!world.Entities.TryGet(id, out var entity) || entity == null)
                return;
            if (entity.TryGet<LifecycleComponent>(out var life))
            {
                if (life.IsRemoved)
                    return;
                // 仅排除已腐烂尸体；弥留与可见尸体仍进名单（不可勾选组队）
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
                LifeStateLabel = CombatLifeStateService.ResolveLifeStateLabel(entity) ?? "存活",
                NodeId = ArmyService.ResolveCharacterNodeId(world, id) ?? string.Empty
            };
            row.NodeLabel = ResolveNodeLabel(world, row.NodeId);
            if (ArmyService.TryGetArmyForCharacter(world, id, out var army) && army != null)
            {
                row.ArmyId = army.ArmyId;
                row.IsGrouped = true;
            }

            row.CanSelectForArmyCreation =
                !row.IsGrouped &&
                LingeringBattlefieldPartyService.IsLivingForMacroOrder(world, id);

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

                var row = new StrategicArmyRosterRow
                {
                    ArmyId = army.ArmyId,
                    LeaderId = army.LeaderCharacterId,
                    MemberCount = army.MemberCharacterIds.Count,
                    State = army.State,
                    NodeId = army.NodeId ?? string.Empty,
                    DestNodeId = army.DestNodeId ?? string.Empty,
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

                row.NodeLabel = ResolveNodeLabel(world, row.NodeId);
                row.DestNodeLabel = ResolveNodeLabel(world, row.DestNodeId);
                into.Add(row);
            }

            into.Sort((a, b) => string.CompareOrdinal(a.ArmyId, b.ArmyId));
        }

        public static void CollectUngroupedPlayerCharacters(
            SimulationWorld world,
            string playerFactionId,
            IReadOnlyList<EntityId> partyCharacterIds,
            List<EntityId> into)
        {
            into.Clear();
            if (world == null || into == null)
                return;

            var rows = new List<StrategicCharacterRosterRow>(32);
            CollectPlayerCharacters(world, playerFactionId, partyCharacterIds, rows);
            for (var i = 0; i < rows.Count; i++)
            {
                if (!rows[i].IsGrouped && rows[i].CanSelectForArmyCreation)
                    into.Add(rows[i].CharacterId);
            }
        }

        public static void CollectUngroupedCharactersAtNode(
            SimulationWorld world,
            string nodeId,
            string factionId,
            IReadOnlyList<EntityId> partyCharacterIds,
            List<EntityId> into)
        {
            into.Clear();
            if (world == null || into == null || string.IsNullOrEmpty(nodeId))
                return;

            var scratchResidents = new List<EntityId>(8);
            var scratchArmies = new List<FormalArmy>(4);
            ArmyService.CollectResidentsAtNode(
                world, nodeId, factionId, partyCharacterIds, scratchResidents, scratchArmies);
            for (var i = 0; i < scratchResidents.Count; i++)
                into.Add(scratchResidents[i]);
        }

        public static string ResolveNodeLabel(SimulationWorld world, string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
                return "—";
            if (world?.WorldGraph != null &&
                world.WorldGraph.TryGetNode(nodeId, out var node) &&
                node != null &&
                !string.IsNullOrEmpty(node.Name))
                return node.Name;
            return nodeId;
        }

        public static string DescribeArmyTravel(StrategicArmyRosterRow row)
        {
            if (row == null)
                return string.Empty;
            if (row.State == FormalArmyState.OnRoute && !string.IsNullOrEmpty(row.DestNodeLabel))
                return row.NodeLabel + " → " + row.DestNodeLabel;
            return row.NodeLabel;
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
