using System.Collections.Generic;
using System.Text;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Development trace：Lingering re-entry participant 解析（LINGERING-PARTICIPANT-TRACE）。</summary>
    public static class LingeringParticipantTrace
    {
        public static string LastTrace { get; private set; } = string.Empty;

        public static void Emit(
            SimulationWorld world,
            HexCoord? requestedHex,
            LingeringBattlefieldState battlefield,
            IList<EntityId> finalSpawnIds,
            string stage)
        {
            if (world?.Strategic == null)
                return;

            var rt = world.Strategic.Encounter;
            var sb = new StringBuilder(768);
            sb.AppendLine("[ LINGERING-PARTICIPANT-TRACE ]");
            sb.Append("Stage=").Append(stage ?? string.Empty).AppendLine();
            sb.Append("RequestedHex=")
                .Append(requestedHex.HasValue ? requestedHex.Value.ToString() : "NONE")
                .AppendLine();
            sb.Append("ResolvedEncounterRuntimeId=")
                .Append(rt?.ActiveBattlefieldId ?? string.Empty)
                .AppendLine();
            sb.Append("ResolvedBattleAnchorHex=");
            if (battlefield != null)
                sb.AppendLine(battlefield.BattleAnchorHex.ToString());
            else if (ArmyHexBattleAnchorService.TryGetBattleAnchorHex(
                         world.Strategic.Participants, out var snapHex))
                sb.AppendLine(snapHex.ToString());
            else
                sb.AppendLine("NONE");

            var storedParticipantIds = new List<EntityId>(8);
            var storedEnemyIds = new List<EntityId>(8);
            CollectAllStoredParticipantIds(battlefield, storedParticipantIds);
            CollectStoredEnemyParticipantIds(battlefield, storedEnemyIds);
            AppendIdList(sb, "Battlefield stored participant ids", storedParticipantIds);
            AppendIdList(sb, "Battlefield stored enemy participant ids", storedEnemyIds);

            var residualAtHex = new List<EntityId>(8);
            if (requestedHex.HasValue)
                CollectResidualCharacterIdsAtHex(world, requestedHex.Value, residualAtHex);
            AppendIdList(sb, "Residual CharacterIds at Hex", residualAtHex);

            AppendIdList(sb, "Final CharacterIds passed to LocalMap spawn", finalSpawnIds);

            LastTrace = sb.ToString();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            System.Diagnostics.Debug.WriteLine(LastTrace);
#endif
        }

        public static bool TryResolveBattlefield(
            SimulationWorld world,
            HexCoord? preferredHex,
            EntityId focusResidual,
            out LingeringBattlefieldState battlefield,
            out HexCoord resolvedHex)
        {
            battlefield = null;
            resolvedHex = default;
            if (world?.Strategic == null)
                return false;

            if (preferredHex.HasValue &&
                world.Strategic.LingeringBattlefields.TryGetAtHex(preferredHex.Value, out battlefield) &&
                battlefield != null)
            {
                resolvedHex = preferredHex.Value;
                return true;
            }

            if (!focusResidual.IsNone &&
                StrategicResidualPresenceService.TryGetResidualHex(world, focusResidual, out resolvedHex) &&
                world.Strategic.LingeringBattlefields.TryGetAtHex(resolvedHex, out battlefield) &&
                battlefield != null)
                return true;

            if (ArmyHexBattleAnchorService.TryGetBattleAnchorHex(
                    world.Strategic.Participants, out resolvedHex) &&
                world.Strategic.LingeringBattlefields.TryGetAtHex(resolvedHex, out battlefield) &&
                battlefield != null)
                return true;

            battlefield = null;
            return false;
        }

        public static string ResolveEnemyStackIdForLingeringHex(
            SimulationWorld world,
            HexCoord? lingeringHex,
            EntityId focusResidual)
        {
            if (TryResolveBattlefield(world, lingeringHex, focusResidual, out var battlefield, out _) &&
                battlefield != null &&
                !string.IsNullOrEmpty(battlefield.EnemyStackId))
                return battlefield.EnemyStackId;

            var rt = world?.Strategic?.Encounter;
            if (!string.IsNullOrEmpty(rt?.ArmyStackId))
                return rt.ArmyStackId;
            return world?.Strategic?.Participants?.PrimaryEnemyStackId ?? string.Empty;
        }

        public static void CollectStoredEnemyParticipantIds(
            LingeringBattlefieldState battlefield,
            IList<EntityId> into)
        {
            into?.Clear();
            if (battlefield?.Participants == null || into == null)
                return;

            var snap = battlefield.Participants;
            for (var i = 0; i < snap.Records.Count; i++)
            {
                var rec = snap.Records[i];
                if (rec.EntityId.IsNone)
                    continue;
                if (rec.Kind != BattleParticipantKind.EnemyPrimary &&
                    rec.Kind != BattleParticipantKind.EnemyReinforcement)
                    continue;
                if (!ContainsId(into, rec.EntityId))
                    into.Add(rec.EntityId);
            }
        }

        static void CollectAllStoredParticipantIds(
            LingeringBattlefieldState battlefield,
            IList<EntityId> into)
        {
            into?.Clear();
            if (battlefield?.Participants == null || into == null)
                return;

            for (var i = 0; i < battlefield.Participants.Records.Count; i++)
            {
                var rec = battlefield.Participants.Records[i];
                if (rec.EntityId.IsNone || ContainsId(into, rec.EntityId))
                    continue;
                into.Add(rec.EntityId);
            }
        }

        static void CollectResidualCharacterIdsAtHex(
            SimulationWorld world,
            HexCoord hex,
            IList<EntityId> into)
        {
            into.Clear();
            foreach (var kv in world.WorldPresence.All)
            {
                var id = new EntityId(kv.Key);
                if (!StrategicResidualPresenceService.IsStrategicResidualCandidate(world, id))
                    continue;
                if (!StrategicResidualPresenceService.TryGetResidualHex(world, id, out var rh) ||
                    !rh.Equals(hex))
                    continue;
                into.Add(id);
            }
        }

        static void AppendIdList(StringBuilder sb, string label, IList<EntityId> ids)
        {
            sb.Append(label).Append('=');
            if (ids == null || ids.Count == 0)
            {
                sb.AppendLine("[]");
                return;
            }

            sb.Append('[');
            for (var i = 0; i < ids.Count; i++)
            {
                if (i > 0)
                    sb.Append(',');
                sb.Append(ids[i].Value);
            }

            sb.AppendLine("]");
        }

        static bool ContainsId(IList<EntityId> list, EntityId id)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] == id)
                    return true;
            }

            return false;
        }
    }
}
