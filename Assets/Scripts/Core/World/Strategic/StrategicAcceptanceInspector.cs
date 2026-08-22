using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    public sealed class StrategicAftermathReport
    {
        public readonly List<EntityId> Captured = new List<EntityId>(8);
        public readonly List<EntityId> Escaped = new List<EntityId>(8);
        public readonly List<RetreatingArmy> RetreatingArmies = new List<RetreatingArmy>(4);
    }

    /// <summary>Development acceptance 只读 Inspector；不修改 Domain。</summary>
    public static class StrategicAcceptanceInspector
    {
        public static void CollectKnownFactionIds(SimulationWorld world, List<string> into)
        {
            if (world == null || into == null)
                return;
            into.Clear();
            AddFaction(into, world.Strategic?.PlayerFactionId);
            AddFaction(into, StrategicFactionCatalog.PlayerFactionId);
            AddFaction(into, StrategicFactionCatalog.HuangcunLaborId);
            AddFaction(into, StrategicFactionCatalog.BanditId);
            for (var i = 0; i < StrategicFactionCatalog.Ch01RegionalFactionIds.Length; i++)
                AddFaction(into, StrategicFactionCatalog.Ch01RegionalFactionIds[i]);

            if (world.WorldGraph?.Nodes != null)
            {
                foreach (var kv in world.WorldGraph.Nodes)
                {
                    var node = kv.Value;
                    if (node != null)
                        AddFaction(into, node.OwnerId);
                }
            }

            if (world.Strategic?.FormalArmies?.Armies != null)
            {
                foreach (var kv in world.Strategic.FormalArmies.Armies)
                {
                    if (kv.Value != null)
                        AddFaction(into, kv.Value.FactionId);
                }
            }

            foreach (var entity in world.Entities.All)
            {
                if (entity != null &&
                    entity.TryGet<FactionMembershipComponent>(out var mem))
                    AddFaction(into, mem.FactionId);
            }

            if (world.Strategic?.Alliances?.All != null)
            {
                foreach (var kv in world.Strategic.Alliances.All)
                {
                    foreach (var member in kv.Value)
                        AddFaction(into, member);
                }
            }

            if (world.Strategic?.Vassalages?.All != null)
            {
                foreach (var kv in world.Strategic.Vassalages.All)
                {
                    AddFaction(into, kv.Key);
                    AddFaction(into, kv.Value);
                }
            }

            if (world.Strategic?.Wars?.All != null)
            {
                foreach (var kv in world.Strategic.Wars.All)
                {
                    var war = kv.Value;
                    if (war == null)
                        continue;
                    foreach (var a in war.Attackers)
                        AddFaction(into, a);
                    foreach (var d in war.Defenders)
                        AddFaction(into, d);
                }
            }

            into.Sort(StringComparer.Ordinal);
        }

        public static int CountOwnedNodes(SimulationWorld world, string factionId)
        {
            if (world?.WorldGraph?.Nodes == null || string.IsNullOrEmpty(factionId))
                return 0;
            var count = 0;
            foreach (var kv in world.WorldGraph.Nodes)
            {
                var node = kv.Value;
                if (node != null &&
                    string.Equals(node.OwnerId, factionId, StringComparison.Ordinal))
                    count++;
            }

            return count;
        }

        public static int CountFormalArmies(SimulationWorld world, string factionId)
        {
            if (world?.Strategic?.FormalArmies?.Armies == null || string.IsNullOrEmpty(factionId))
                return 0;
            var count = 0;
            foreach (var kv in world.Strategic.FormalArmies.Armies)
            {
                if (kv.Value != null &&
                    string.Equals(kv.Value.FactionId, factionId, StringComparison.Ordinal))
                    count++;
            }

            return count;
        }

        public static bool IsLandless(SimulationWorld world, string factionId) =>
            LandlessFactionService.IsLandless(world, factionId);

        public static string ResolveOwnerDisplay(string factionId)
        {
            if (string.IsNullOrEmpty(factionId))
                return "无归属";
            return StrategicFactionCatalog.DisplayName(factionId) + " (" + factionId + ")";
        }

        public static string BuildNodeOwnerLine(SimulationWorld world, WorldNodeState node)
        {
            if (node == null)
                return "Owner: —";
            return "Owner: " + ResolveOwnerDisplay(node.OwnerId);
        }

        public static void AppendCaptureObjectivesForNode(
            SimulationWorld world,
            WorldNodeState node,
            System.Text.StringBuilder sb)
        {
            if (world?.Strategic?.CaptureObjectives == null || node == null || sb == null)
                return;
            var ids = world.Strategic.CaptureObjectives.GetObjectiveIdsForNode(node.Id);
            if (ids == null || ids.Count == 0)
                return;

            sb.Append("\nCapture Objectives:");
            for (var i = 0; i < ids.Count; i++)
            {
                if (!world.Strategic.CaptureObjectives.TryGet(ids[i], out var obj) || obj == null)
                    continue;
                var label = obj.ObjectiveId;
                if (label.StartsWith("capture:", StringComparison.Ordinal))
                    label = label.Substring("capture:".Length);
                sb.Append('\n').Append(label).Append("    ");
                sb.Append(obj.Completed ? "Captured" : "Not Captured");
            }
        }

        public static StrategicAftermathReport BuildAftermathReport(SimulationWorld world)
        {
            var report = new StrategicAftermathReport();
            if (world == null)
                return report;

            var participants = world.Strategic?.Participants;
            var hasParticipants = participants != null && participants.Records.Count > 0;
            var retreatMemberIds = new HashSet<ulong>();

            if (world.Strategic?.RetreatingArmies?.All != null)
            {
                foreach (var kv in world.Strategic.RetreatingArmies.All)
                {
                    var army = kv.Value;
                    if (army == null)
                        continue;
                    report.RetreatingArmies.Add(army);
                    for (var i = 0; i < army.MemberCharacterIds.Count; i++)
                        retreatMemberIds.Add(army.MemberCharacterIds[i]);
                }
            }

            if (hasParticipants)
            {
                for (var i = 0; i < participants.Records.Count; i++)
                {
                    var record = participants.Records[i];
                    if (record.EntityId.IsNone ||
                        !world.Entities.TryGet(record.EntityId, out var entity) ||
                        entity == null ||
                        !entity.TryGet<LifecycleComponent>(out var life))
                        continue;

                    if (life.State == LifecycleState.Captured)
                    {
                        report.Captured.Add(record.EntityId);
                        continue;
                    }

                    if (record.Kind == BattleParticipantKind.EnemyPrimary ||
                        record.Kind == BattleParticipantKind.EnemyReinforcement)
                    {
                        if (!life.IsDead && !life.IsRemoved && life.State == LifecycleState.Alive)
                        {
                            if (retreatMemberIds.Contains(record.EntityId.Value))
                                continue;
                            report.Escaped.Add(record.EntityId);
                        }
                    }
                }
            }
            else
            {
                foreach (var entity in world.Entities.All)
                {
                    if (entity == null || !entity.TryGet<LifecycleComponent>(out var life))
                        continue;
                    if (life.State == LifecycleState.Captured)
                        report.Captured.Add(entity.Id);
                }
            }

            return report;
        }

        static void AddFaction(List<string> into, string factionId)
        {
            if (string.IsNullOrEmpty(factionId) || into.Contains(factionId))
                return;
            into.Add(factionId);
        }
    }
}
