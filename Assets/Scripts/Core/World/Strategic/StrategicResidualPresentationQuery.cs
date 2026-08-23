using System;
using System.Collections.Generic;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    public enum ResidualStateBucket
    {
        Downed = 0,
        Dead = 1
    }

    /// <summary>WorldMap Residual Marker 派生视图（非 Domain Entity）。</summary>
    public sealed class ResidualMarkerGroupView
    {
        public HexCoord Hex { get; set; }
        public StrategicRelationBucket Relation { get; set; }
        public ResidualStateBucket State { get; set; }
        public int Count => Characters?.Count ?? 0;
        public List<ResidualCharacterRowView> Characters { get; } = new List<ResidualCharacterRowView>(8);

        /// <summary>视觉 / Hit 优先级（数值越大越优先、越后画覆盖）。</summary>
        public int VisualPriority => ComputeVisualPriority(Relation, State);

        public static int ComputeVisualPriority(
            StrategicRelationBucket relation,
            ResidualStateBucket state)
        {
            // SELF > ALLY > OTHER > ENEMY；同 Relation DEAD > DOWNED
            int relRank;
            switch (relation)
            {
                case StrategicRelationBucket.Self:
                    relRank = 3;
                    break;
                case StrategicRelationBucket.Ally:
                    relRank = 2;
                    break;
                case StrategicRelationBucket.Other:
                    relRank = 1;
                    break;
                default:
                    relRank = 0;
                    break;
            }

            var stateRank = state == ResidualStateBucket.Dead ? 1 : 0;
            return relRank * 2 + stateRank;
        }
    }

    public sealed class ResidualCharacterRowView
    {
        public EntityId CharacterId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string FactionId { get; set; } = string.Empty;
        public string FactionDisplayName { get; set; } = string.Empty;
        public ResidualStateBucket State { get; set; }
        public string LifeStateLabel { get; set; } = string.Empty;
    }

    public sealed class ResidualHexCounts
    {
        public int SelfDowned { get; set; }
        public int SelfDead { get; set; }
        public int EnemyDowned { get; set; }
        public int EnemyDead { get; set; }
        public int AllyDowned { get; set; }
        public int AllyDead { get; set; }
        public int OtherDowned { get; set; }
        public int OtherDead { get; set; }

        public int EnemyTotal => EnemyDowned + EnemyDead;
        public int SelfTotal => SelfDowned + SelfDead;
    }

    /// <summary>纯派生 Presentation Query：Hex × Relation × ResidualState。</summary>
    public static class StrategicResidualPresentationQuery
    {
        public static List<ResidualMarkerGroupView> Query(SimulationWorld world)
        {
            var groups = new Dictionary<string, ResidualMarkerGroupView>(32);
            if (world?.Entities == null)
                return new List<ResidualMarkerGroupView>();

            foreach (var ent in world.Entities.All)
            {
                if (ent == null || ent.Id.IsNone)
                    continue;
                if (!TryClassify(world, ent, out var hex, out var relation, out var state))
                    continue;

                var key = BuildKey(hex, relation, state);
                if (!groups.TryGetValue(key, out var group))
                {
                    group = new ResidualMarkerGroupView
                    {
                        Hex = hex,
                        Relation = relation,
                        State = state
                    };
                    groups[key] = group;
                }

                group.Characters.Add(BuildRow(world, ent, state));
            }

            var list = new List<ResidualMarkerGroupView>(groups.Count);
            foreach (var kv in groups)
                list.Add(kv.Value);
            list.Sort(CompareByPriorityAscending);
            return list;
        }

        public static ResidualHexCounts CountAtHex(SimulationWorld world, HexCoord hex)
        {
            var counts = new ResidualHexCounts();
            if (world?.Entities == null)
                return counts;

            foreach (var ent in world.Entities.All)
            {
                if (ent == null || ent.Id.IsNone)
                    continue;
                if (!TryClassify(world, ent, out var at, out var relation, out var state))
                    continue;
                if (!at.Equals(hex))
                    continue;

                switch (relation)
                {
                    case StrategicRelationBucket.Self:
                        if (state == ResidualStateBucket.Dead)
                            counts.SelfDead++;
                        else
                            counts.SelfDowned++;
                        break;
                    case StrategicRelationBucket.Ally:
                        if (state == ResidualStateBucket.Dead)
                            counts.AllyDead++;
                        else
                            counts.AllyDowned++;
                        break;
                    case StrategicRelationBucket.Enemy:
                        if (state == ResidualStateBucket.Dead)
                            counts.EnemyDead++;
                        else
                            counts.EnemyDowned++;
                        break;
                    default:
                        if (state == ResidualStateBucket.Dead)
                            counts.OtherDead++;
                        else
                            counts.OtherDowned++;
                        break;
                }
            }

            return counts;
        }

        public static bool HasEnemyResidualAtHex(SimulationWorld world, HexCoord hex)
        {
            var counts = CountAtHex(world, hex);
            return counts.EnemyTotal > 0;
        }

        public static bool TryClassify(
            SimulationWorld world,
            Entity entity,
            out HexCoord hex,
            out StrategicRelationBucket relation,
            out ResidualStateBucket state)
        {
            hex = default;
            relation = StrategicRelationBucket.Other;
            state = ResidualStateBucket.Downed;
            if (entity == null || !StrategicResidualPresenceService.IsStrategicResidualCandidate(world, entity.Id))
                return false;
            if (!StrategicResidualPresenceService.TryGetResidualHex(world, entity.Id, out hex))
                return false;

            if (LingeringBattlefieldPartyService.IsIncapacitated(world, entity.Id))
                state = ResidualStateBucket.Downed;
            else if (LingeringBattlefieldPartyService.IsVisibleCorpse(world, entity.Id))
                state = ResidualStateBucket.Dead;
            else
                return false;

            var factionId = ArmyService.ResolveCharacterFactionId(world, entity.Id);
            relation = StrategicRelationQuery.GetRelationToPlayer(world, factionId);
            return true;
        }

        static ResidualCharacterRowView BuildRow(
            SimulationWorld world,
            Entity entity,
            ResidualStateBucket state)
        {
            var factionId = ArmyService.ResolveCharacterFactionId(world, entity.Id) ?? string.Empty;
            return new ResidualCharacterRowView
            {
                CharacterId = entity.Id,
                DisplayName = string.IsNullOrEmpty(entity.DisplayName)
                    ? entity.Id.ToString()
                    : entity.DisplayName,
                FactionId = factionId,
                FactionDisplayName = ResolveFactionDisplayName(world, factionId),
                State = state,
                LifeStateLabel = CombatLifeStateService.FormatLifeStateWithCountdown(world, entity)
                    ?? (state == ResidualStateBucket.Dead ? "阵亡" : "弥留")
            };
        }

        static string ResolveFactionDisplayName(SimulationWorld world, string factionId)
        {
            if (string.IsNullOrEmpty(factionId))
                return "未知势力";
            return StrategicFactionCatalog.DisplayName(factionId);
        }

        public static string BuildKey(
            HexCoord hex,
            StrategicRelationBucket relation,
            ResidualStateBucket state) =>
            hex.Q + ":" + hex.R + ":" + (int)relation + ":" + (int)state;

        static int CompareByPriorityAscending(ResidualMarkerGroupView a, ResidualMarkerGroupView b)
        {
            if (a == null && b == null)
                return 0;
            if (a == null)
                return -1;
            if (b == null)
                return 1;
            var cmp = a.VisualPriority.CompareTo(b.VisualPriority);
            if (cmp != 0)
                return cmp;
            cmp = a.Hex.Q.CompareTo(b.Hex.Q);
            if (cmp != 0)
                return cmp;
            cmp = a.Hex.R.CompareTo(b.Hex.R);
            if (cmp != 0)
                return cmp;
            cmp = ((int)a.Relation).CompareTo((int)b.Relation);
            if (cmp != 0)
                return cmp;
            return ((int)a.State).CompareTo((int)b.State);
        }
    }
}
