using System.Collections.Generic;
using System.Text;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Development trace：BattleOffer → ParticipantSnapshot → LocalMap spawn（ENCOUNTER-ASSEMBLY-TRACE）。</summary>
    public static class EncounterAssemblyTrace
    {
        public static string LastTrace { get; private set; } = string.Empty;

        public static void Emit(
            SimulationWorld world,
            ArmyStack targetStack,
            string stage,
            IList<EntityId> aboutToSpawn = null,
            IList<EntityId> finalLocalMapActors = null)
        {
            if (world?.Strategic == null)
                return;

            var offer = world.Strategic.BattleOffer;
            var snap = world.Strategic.Participants;
            var sb = new StringBuilder(2048);
            sb.AppendLine("[ ENCOUNTER-ASSEMBLY-TRACE ]");
            sb.Append("Stage=").Append(stage ?? string.Empty).AppendLine();

            var targetArmyId = string.Empty;
            var targetMembers = new List<ulong>(8);
            if (targetStack != null &&
                ArmyStackAdapter.TryGetFormalArmy(world, targetStack, out var targetFormal) &&
                targetFormal != null)
            {
                targetArmyId = targetFormal.ArmyId ?? string.Empty;
                for (var i = 0; i < targetFormal.MemberCharacterIds.Count; i++)
                    targetMembers.Add(targetFormal.MemberCharacterIds[i]);
            }
            else if (targetStack != null)
            {
                targetArmyId = targetStack.Id ?? string.Empty;
            }

            sb.Append("Target ArmyId=").Append(targetArmyId).AppendLine();
            AppendUlongList(sb, "Target Army MemberCharacterIds", targetMembers);

            var attackerIds = new List<EntityId>(8);
            var defenderIds = new List<EntityId>(8);
            if (snap != null)
            {
                for (var i = 0; i < snap.Records.Count; i++)
                {
                    var rec = snap.Records[i];
                    if (rec.EntityId.IsNone)
                        continue;
                    if (rec.Kind == BattleParticipantKind.MandatoryFriendly ||
                        (rec.Kind == BattleParticipantKind.OptionalFriendly && rec.Selected))
                        attackerIds.Add(rec.EntityId);
                    if (rec.Kind == BattleParticipantKind.EnemyPrimary)
                        defenderIds.Add(rec.EntityId);
                }
            }

            AppendIdList(sb, "BattleOffer Attacker CharacterIds", attackerIds);
            AppendIdList(sb, "BattleOffer Defender CharacterIds", defenderIds);

            var supportArmyIds = new List<string>(4);
            var supportCharIds = new List<EntityId>(8);
            if (snap != null)
            {
                for (var i = 0; i < snap.Records.Count; i++)
                {
                    var rec = snap.Records[i];
                    if (rec.Kind != BattleParticipantKind.EnemyReinforcement)
                        continue;
                    if (!string.IsNullOrEmpty(rec.ArmyStackId) &&
                        !supportArmyIds.Contains(rec.ArmyStackId))
                        supportArmyIds.Add(rec.ArmyStackId);
                    if (!rec.EntityId.IsNone)
                        supportCharIds.Add(rec.EntityId);
                }
            }

            AppendStringList(sb, "Support / Reinforcement ArmyIds", supportArmyIds);
            AppendIdList(sb, "Support / Reinforcement CharacterIds", supportCharIds);

            var snapshotIds = new List<EntityId>(8);
            snap?.CollectEnemyEntityIds(snapshotIds);
            AppendIdList(sb, "Encounter ParticipantSnapshot CharacterIds", snapshotIds);

            AppendIdList(sb, "LocalMap Characters About To Spawn", aboutToSpawn);
            AppendIdList(sb, "Final LocalMap Actor CharacterIds", finalLocalMapActors);

            if (ArmyHexBattleAnchorService.TryGetBattleAnchorHex(snap, out var battleHex))
            {
                sb.Append("WeakBandit BattleHex=").Append(battleHex).AppendLine();
                AppendFourBanditDistance(world, sb, battleHex);
            }
            else
            {
                sb.AppendLine("WeakBandit BattleHex=NONE");
            }

            sb.Append("Current Support Radius=").Append("1 HEX (hex anchor)").AppendLine();
            sb.Append("Reinforcement System=").Append("YES").AppendLine();
            sb.Append("Chain Support=").Append("NO").AppendLine();

            LastTrace = sb.ToString();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            System.Diagnostics.Debug.WriteLine(LastTrace);
#endif
        }

        static void AppendFourBanditDistance(SimulationWorld world, StringBuilder sb, HexCoord battleHex)
        {
            if (world?.Strategic?.FormalArmies == null)
                return;

            if (!world.Strategic.FormalArmies.TryGet(
                    ArmyStackAdapter.BanditPatrolFormalArmyId, out var fourArmy) ||
                fourArmy == null ||
                !fourArmy.UsesHexStrategicPosition)
            {
                sb.AppendLine("FourBandit Army CurrentHex=NONE");
                sb.AppendLine("HexDistance(H1,H2)=UNKNOWN");
                return;
            }

            var h2 = fourArmy.CurrentHex;
            sb.Append("FourBandit Army CurrentHex=").Append(h2).AppendLine();
            sb.Append("HexDistance(H1,H2)=")
                .Append(HexMath.Distance(battleHex, h2))
                .AppendLine();
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

        static void AppendUlongList(StringBuilder sb, string label, IList<ulong> ids)
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
                sb.Append(ids[i]);
            }

            sb.AppendLine("]");
        }

        static void AppendStringList(StringBuilder sb, string label, IList<string> values)
        {
            sb.Append(label).Append('=');
            if (values == null || values.Count == 0)
            {
                sb.AppendLine("[]");
                return;
            }

            sb.Append('[');
            for (var i = 0; i < values.Count; i++)
            {
                if (i > 0)
                    sb.Append(',');
                sb.Append(values[i]);
            }

            sb.AppendLine("]");
        }
    }
}
