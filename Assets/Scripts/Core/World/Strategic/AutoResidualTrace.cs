using System.Text;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Auto Battle → Residual 一次性 Development Trace（禁止每帧 spam）。</summary>
    public static class AutoResidualTrace
    {
        public static string LastTrace { get; private set; } = string.Empty;

        public static void EmitAfterAutoBind(
            SimulationWorld world,
            BattleParticipantSnapshot snap,
            bool playerWon)
        {
            if (!playerWon || world?.Strategic == null)
                return;

            var sb = new StringBuilder(512);
            sb.AppendLine("[AUTO-RESIDUAL-TRACE]");
            sb.Append("BattleOfferId=").Append(world.Strategic.Encounter?.EncounterLinkId ?? string.Empty)
                .AppendLine();

            HexCoord hex = default;
            var hasHex = StrategicResidualPresenceService.TryResolveEncounterHex(world, snap, out hex);
            sb.Append("EncounterHex=").Append(hasHex ? hex.ToString() : "NONE").AppendLine();

            var stackId = snap?.PrimaryEnemyStackId ?? world.Strategic.Encounter?.ArmyStackId ?? string.Empty;
            sb.Append("EnemyArmyStackId=").Append(stackId).AppendLine();

            var reported = 0;
            if (!string.IsNullOrEmpty(stackId) &&
                world.Strategic.Armies.TryGet(stackId, out var stack) &&
                stack != null)
                reported = stack.IncapacitatedMemberCount;

            sb.Append("Enemy reported incap count=").Append(reported).AppendLine();

            var realIds = new System.Collections.Generic.List<EntityId>(8);
            CollectEnemyLingeringCharacters(world, stackId, realIds);
            sb.Append("Enemy real CharacterIds=[");
            for (var i = 0; i < realIds.Count; i++)
            {
                if (i > 0)
                    sb.Append(',');
                sb.Append(realIds[i].Value);
            }

            sb.AppendLine("]");
            sb.Append("RealEnemyCharacters=").Append(realIds.Count).AppendLine();

            var lifeDowned = 0;
            var withHex = 0;
            var stillInArmy = 0;
            for (var i = 0; i < realIds.Count; i++)
            {
                var id = realIds[i];
                if (LingeringBattlefieldPartyService.IsIncapacitated(world, id))
                    lifeDowned++;
                if (StrategicResidualPresenceService.TryGetResidualHex(world, id, out _))
                    withHex++;
                if (ArmyService.TryGetArmyForCharacter(world, id, out _))
                    stillInArmy++;
            }

            sb.Append("LifeStateDowned=").Append(lifeDowned).AppendLine();
            sb.Append("WithResidualHex=").Append(withHex).AppendLine();
            sb.Append("StillInFormalArmy=").Append(stillInArmy).AppendLine();

            var groups = StrategicResidualPresentationQuery.Query(world);
            var enemyCandidates = 0;
            var enemyGroups = 0;
            for (var i = 0; i < groups.Count; i++)
            {
                var g = groups[i];
                if (g == null)
                    continue;
                if (g.Relation != StrategicRelationBucket.Enemy &&
                    g.Relation != StrategicRelationBucket.Other)
                    continue;
                if (g.State != ResidualStateBucket.Downed && g.State != ResidualStateBucket.Dead)
                    continue;
                enemyGroups++;
                enemyCandidates += g.Count;
            }

            sb.Append("ResidualQueryCandidates(enemy/other)=").Append(enemyCandidates).AppendLine();
            sb.Append("ResidualGroups(enemy/other)=").Append(enemyGroups).AppendLine();
            sb.Append("DrawnMarkers(expected)=").Append(enemyGroups).AppendLine();

            LastTrace = sb.ToString();
            System.Diagnostics.Debug.WriteLine(LastTrace);
        }

        static void CollectEnemyLingeringCharacters(
            SimulationWorld world,
            string stackId,
            System.Collections.Generic.List<EntityId> into)
        {
            into.Clear();
            if (world?.Entities == null)
                return;

            string enemyFaction = null;
            if (!string.IsNullOrEmpty(stackId) &&
                world.Strategic.Armies.TryGet(stackId, out var stack) &&
                stack != null)
                enemyFaction = stack.FactionId;

            foreach (var ent in world.Entities.All)
            {
                if (ent == null || ent.Id.IsNone)
                    continue;
                if (!LingeringBattlefieldPartyService.IsLingeringDowned(world, ent.Id))
                    continue;
                var faction = ArmyService.ResolveCharacterFactionId(world, ent.Id);
                if (string.IsNullOrEmpty(faction))
                    continue;
                if (!string.IsNullOrEmpty(enemyFaction) &&
                    !string.Equals(faction, enemyFaction, System.StringComparison.Ordinal))
                    continue;
                if (LingeringBattlefieldPartyService.IsFriendlyCharacterForLingeringVisit(world, ent.Id))
                    continue;
                into.Add(ent.Id);
            }
        }
    }
}
