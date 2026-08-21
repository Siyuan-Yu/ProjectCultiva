using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>同 Tick 多接战排队（确定性 FIFO，按入队序号）。</summary>
    public sealed class BattleInterruptQueue
    {
        readonly List<QueuedBattleOffer> _items = new List<QueuedBattleOffer>(8);
        ulong _nextSeq;

        public int Count => _items.Count;
        public bool IsEmpty => _items.Count == 0;

        public void Clear()
        {
            _items.Clear();
            _nextSeq = 0;
        }

            public ulong Enqueue(
            string title,
            string armyStackId,
            IReadOnlyList<EntityId> mandatoryParty,
            ulong eventSeqHint = 0)
        {
            // 同栈不重复入队
            if (!string.IsNullOrEmpty(armyStackId))
            {
                for (var i = 0; i < _items.Count; i++)
                {
                    if (string.Equals(_items[i].ArmyStackId, armyStackId, StringComparison.Ordinal))
                        return _items[i].Sequence;
                }
            }

            var seq = eventSeqHint != 0 ? eventSeqHint : ++_nextSeq;
            if (eventSeqHint != 0 && eventSeqHint > _nextSeq)
                _nextSeq = eventSeqHint;

            var item = new QueuedBattleOffer
            {
                Sequence = seq,
                Title = title ?? string.Empty,
                ArmyStackId = armyStackId ?? string.Empty
            };
            if (mandatoryParty != null)
            {
                for (var i = 0; i < mandatoryParty.Count; i++)
                {
                    if (!mandatoryParty[i].IsNone)
                        item.MandatoryPartyIds.Add(mandatoryParty[i].Value);
                }
            }

            // 按 Sequence 插入保持确定性
            var insertAt = _items.Count;
            for (var i = 0; i < _items.Count; i++)
            {
                if (seq < _items[i].Sequence)
                {
                    insertAt = i;
                    break;
                }
            }

            _items.Insert(insertAt, item);
            return seq;
        }

        public bool TryPeek(out QueuedBattleOffer item)
        {
            item = null;
            if (_items.Count == 0)
                return false;
            item = _items[0];
            return true;
        }

        public bool TryDequeue(out QueuedBattleOffer item)
        {
            item = null;
            if (_items.Count == 0)
                return false;
            item = _items[0];
            _items.RemoveAt(0);
            return true;
        }
    }

    public sealed class QueuedBattleOffer
    {
        public ulong Sequence { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ArmyStackId { get; set; } = string.Empty;
        public List<ulong> MandatoryPartyIds { get; } = new List<ulong>(8);

        public List<EntityId> ToPartyList()
        {
            var list = new List<EntityId>(MandatoryPartyIds.Count);
            for (var i = 0; i < MandatoryPartyIds.Count; i++)
                list.Add(new EntityId(MandatoryPartyIds[i]));
            return list;
        }
    }

    /// <summary>从当前世界构建 BattleParticipantSnapshot。</summary>
    public static class BattleParticipantSnapshotBuilder
    {
        public static BattleParticipantSnapshot Build(
            SimulationWorld world,
            IReadOnlyList<EntityId> mandatoryAttackers,
            ArmyStack primaryEnemy,
            string offerId)
        {
            var snap = new BattleParticipantSnapshot
            {
                OfferId = offerId ?? string.Empty,
                PrimaryEnemyStackId = primaryEnemy?.Id ?? string.Empty,
                EncounterLocalMapId = StrategicEncounterCatalog.DefaultEncounterLocalMapId
            };

            if (primaryEnemy != null)
                ApplyBattleAnchor(snap, primaryEnemy);

            // Mandatory
            if (mandatoryAttackers != null)
            {
                for (var i = 0; i < mandatoryAttackers.Count; i++)
                {
                    var id = mandatoryAttackers[i];
                    if (id.IsNone || !world.WorldPresence.TryGet(id, out var wp) || wp == null)
                        continue;
                    if (!world.Entities.TryGet(id, out var ent) || ent == null)
                        continue;
                    if ((ent.Tags & EntityTag.Npc) != 0)
                        continue;

                    snap.Add(new BattleParticipantRecord
                    {
                        Kind = BattleParticipantKind.MandatoryFriendly,
                        EntityId = id,
                        DisplayLabel = string.IsNullOrEmpty(ent.DisplayName) ? id.ToString() : ent.DisplayName,
                        CombatPower = CombatPowerCalculator.ForEntity(world, id),
                        Selected = true,
                        PreBattle = PreBattleWorldPresence.Capture(wp)
                    });
                }
            }

            // Optional DirectControl（CharacterIds 语义：非 Npc 标签且有 WorldPresence）
            CollectOptionalFriendly(world, snap, mandatoryAttackers);

            // Enemy primary + reinforcements
            if (primaryEnemy != null)
            {
                snap.Add(new BattleParticipantRecord
                {
                    Kind = BattleParticipantKind.EnemyPrimary,
                    ArmyStackId = primaryEnemy.Id,
                    DisplayLabel = string.IsNullOrEmpty(primaryEnemy.DisplayName)
                        ? primaryEnemy.Id
                        : primaryEnemy.DisplayName,
                    CombatPower = CombatPowerCalculator.ForArmyStack(primaryEnemy),
                    Selected = true
                });

                CollectEnemyReinforcements(world, snap, primaryEnemy);
            }

            return snap;
        }

        static void ApplyBattleAnchor(BattleParticipantSnapshot snap, ArmyStack stack)
        {
            if (stack.IsRoutePositioned)
            {
                snap.BattleAnchorRouteId = stack.RouteId ?? string.Empty;
                snap.BattleAnchorProgress = stack.GetRouteDisplayProgress();
                snap.BattleAnchorNodeId = stack.NodeId ?? string.Empty;
                snap.BattleAnchorDestNodeId = stack.DestNodeId ?? string.Empty;
            }
            else
            {
                snap.BattleAnchorNodeId = stack.NodeId ?? string.Empty;
                snap.BattleAnchorDestNodeId = string.Empty;
                snap.BattleAnchorRouteId = string.Empty;
                snap.BattleAnchorProgress = -1f;
            }
        }

        static void CollectOptionalFriendly(
            SimulationWorld world,
            BattleParticipantSnapshot snap,
            IReadOnlyList<EntityId> mandatory)
        {
            foreach (var kv in world.WorldPresence.All)
            {
                var id = new EntityId(kv.Key);
                var wp = kv.Value;
                if (wp == null || id.IsNone)
                    continue;
                if (ContainsId(mandatory, id))
                    continue;
                if (!world.Entities.TryGet(id, out var ent) || ent == null)
                    continue;
                if ((ent.Tags & EntityTag.Npc) != 0)
                    continue;
                // DirectControl ≈ 玩家可控角色（有 WorldPresence 且非 Npc）
                if (!ReinforcementRangeService.IsWithinReinforcementRange(
                        world,
                        wp,
                        snap.BattleAnchorNodeId,
                        snap.BattleAnchorRouteId,
                        snap.BattleAnchorProgress))
                    continue;

                snap.Add(new BattleParticipantRecord
                {
                    Kind = BattleParticipantKind.OptionalFriendly,
                    EntityId = id,
                    DisplayLabel = string.IsNullOrEmpty(ent.DisplayName) ? id.ToString() : ent.DisplayName,
                    CombatPower = CombatPowerCalculator.ForEntity(world, id),
                    Selected = false,
                    PreBattle = PreBattleWorldPresence.Capture(wp)
                });
            }
        }

        static void CollectEnemyReinforcements(
            SimulationWorld world,
            BattleParticipantSnapshot snap,
            ArmyStack primary)
        {
            foreach (var kv in world.Strategic.Armies.Stacks)
            {
                var stack = kv.Value;
                if (stack == null || string.Equals(stack.Id, primary.Id, StringComparison.Ordinal))
                    continue;
                if (!string.Equals(stack.FactionId, primary.FactionId, StringComparison.Ordinal))
                    continue;
                if (!ReinforcementRangeService.IsStackWithinRange(
                        world,
                        stack,
                        snap.BattleAnchorNodeId,
                        snap.BattleAnchorRouteId,
                        snap.BattleAnchorProgress))
                    continue;

                snap.Add(new BattleParticipantRecord
                {
                    Kind = BattleParticipantKind.EnemyReinforcement,
                    ArmyStackId = stack.Id,
                    DisplayLabel = string.IsNullOrEmpty(stack.DisplayName) ? stack.Id : stack.DisplayName,
                    CombatPower = CombatPowerCalculator.ForArmyStack(stack),
                    Selected = true // 第一版自动加入
                });
            }
        }

        static bool ContainsId(IReadOnlyList<EntityId> list, EntityId id)
        {
            if (list == null || id.IsNone)
                return false;
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] == id)
                    return true;
            }

            return false;
        }
    }
}
