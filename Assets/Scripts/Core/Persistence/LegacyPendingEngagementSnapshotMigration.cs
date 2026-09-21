using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Persistence
{
    /// <summary>One-way bridge from a retired Army pending battle to real Characters and Squads.</summary>
    public static class LegacyPendingEngagementSnapshotMigration
    {
        public static Result Migrate(SimulationWorld world, WorldSnapshot snapshot)
        {
            if (world == null || snapshot == null || snapshot.CharacterEncounter != null)
                return Result.Success();
            var strategic = snapshot.Strategic;
            var legacy = strategic?.PendingEngagement;
            if (legacy == null || string.IsNullOrWhiteSpace(legacy.EngagementId))
                return Result.Success();
            if (legacy.ParticipantRecords == null || legacy.ParticipantRecords.Count == 0)
                return Invalid("active legacy engagement has no frozen Character participants");

            var entries = new List<EncounterCharacter>(legacy.ParticipantRecords.Count);
            var friendly = 0;
            var enemy = 0;
            var surfaceId = legacy.ParticipantBattleAnchorSurfaceId ?? string.Empty;
            var minX = float.MaxValue; var maxX = float.MinValue;
            var minY = float.MaxValue; var maxY = float.MinValue;
            for (var i = 0; i < legacy.ParticipantRecords.Count; i++)
            {
                var record = legacy.ParticipantRecords[i];
                if (record == null || record.EntityId == 0 ||
                    !world.Entities.TryGet(new EntityId(record.EntityId), out _))
                    return Invalid("participant Character is missing at index " + i);
                var kind = (BattleParticipantKind)record.Kind;
                var isEnemy = kind == BattleParticipantKind.EnemyPrimary ||
                              kind == BattleParticipantKind.EnemyReinforcement;
                var isActual = kind == BattleParticipantKind.MandatoryFriendly || isEnemy ||
                               (kind == BattleParticipantKind.OptionalFriendly && record.Selected);
                if (!isActual) continue;

                var characterId = new EntityId(record.EntityId);
                SquadState squad;
                if (!string.IsNullOrWhiteSpace(record.SquadId) &&
                    world.Strategic.Squads.TryGet(record.SquadId, out squad) && squad.Contains(characterId)) { }
                else if (!LegacyFormalArmySnapshotMigration.TryResolveSquad(
                             world, strategic, record.FormalArmyId, characterId, out squad) ||
                         squad == null || !squad.Contains(characterId))
                    return Invalid("participant cannot be mapped uniquely to a restored Squad: " + record.EntityId);

                if (!TryResolvePosition(world, strategic, record, out var point, out var participantSurface))
                    return Invalid("participant lacks an exact Surface position: " + record.EntityId);
                if (string.IsNullOrEmpty(surfaceId)) surfaceId = participantSurface;
                if (string.IsNullOrEmpty(surfaceId) || string.IsNullOrEmpty(participantSurface) ||
                    !string.Equals(surfaceId, participantSurface, StringComparison.Ordinal))
                    return Invalid("participants do not share one exact Surface");

                record.SquadId = squad.SquadId;
                var sourceMode = record.HasPreBattle &&
                                 Enum.IsDefined(typeof(PartyWorldPresenceMode), record.PreBattleMode) &&
                                 record.PreBattleMode != (int)PartyWorldPresenceMode.InEncounter
                    ? record.PreBattleMode : (int)PartyWorldPresenceMode.AtWorldPosition;
                entries.Add(new EncounterCharacter
                {
                    CharacterId = record.EntityId, SquadId = squad.SquadId, Enemy = isEnemy,
                    SourceMode = sourceMode, SourceSiteId = record.PreBattleSiteId ?? string.Empty,
                    SourceSpatialOwnerKind = EncounterSpatialOwnerKind.Squad,
                    SourceSquadId = squad.SquadId, SourceFormalArmyId = string.Empty,
                    OriginX = point.X, OriginY = point.Y,
                    ReturnX = point.X, ReturnY = point.Y,
                    TacticalX = point.X, TacticalY = point.Y
                });
                if (isEnemy) enemy++; else friendly++;
                minX = Math.Min(minX, point.X); maxX = Math.Max(maxX, point.X);
                minY = Math.Min(minY, point.Y); maxY = Math.Max(maxY, point.Y);
            }

            if (friendly == 0 || enemy == 0)
                return Invalid("frozen Character participants do not contain two sides");
            var rules = world.Strategic.SpatialRules;
            if (rules == null) return Invalid("world spatial rules are unavailable");
            ResolvedWorldSpatialRange range;
            try { range = rules.ResolveWildernessEncounter(world, surfaceId); }
            catch (Exception ex) { return Invalid("encounter range cannot be resolved: " + ex.Message); }
            var state = new CharacterEncounterState
            {
                EncounterId = "legacy:" + legacy.EngagementId.Trim(), SourceSurfaceId = surfaceId,
                CenterX = (minX + maxX) * .5f, CenterY = (minY + maxY) * .5f,
                Width = Math.Max(range.WidthWorld, maxX - minX + 2f),
                Height = Math.Max(range.HeightWorld, maxY - minY + 2f),
                Phase = CharacterEncounterPhase.Active,
                DecisionAt = rules.InterventionDecisionSeconds,
                ArrivalDelay = rules.InterventionArrivalSeconds,
                RelationThreshold = rules.InterventionRelationThreshold,
                ChanceBasisPoints = rules.InterventionChanceBasisPoints
            };
            state.Participants.AddRange(entries);
            snapshot.CharacterEncounter = state;
            strategic.PendingEngagement = null;
            return Result.Success();
        }

        static bool TryResolvePosition(SimulationWorld world, StrategicSnapshotDto strategic,
            PendingEngagementParticipantRecordDto record, out WorldVec2 point, out string surfaceId)
        {
            point = default; surfaceId = string.Empty;
            if (record.HasPreBattle && record.PreBattleHasWorldPosition &&
                Finite(record.PreBattleWorldX) && Finite(record.PreBattleWorldY) &&
                !string.IsNullOrWhiteSpace(record.PreBattleSurfaceId))
            {
                point = new WorldVec2(record.PreBattleWorldX, record.PreBattleWorldY);
                surfaceId = record.PreBattleSurfaceId.Trim();
                return true;
            }
            if (world.WorldPresence.TryGet(new EntityId(record.EntityId), out var presence) &&
                presence.HasContinuousWorldPosition && !string.IsNullOrWhiteSpace(presence.PersonalSurfaceId))
            {
                point = presence.ContinuousWorldPosition; surfaceId = presence.PersonalSurfaceId;
                return true;
            }
            if (strategic?.FormalArmies != null && !string.IsNullOrWhiteSpace(record.FormalArmyId))
                for (var i = 0; i < strategic.FormalArmies.Count; i++)
                {
                    var army = strategic.FormalArmies[i];
                    if (army != null && string.Equals(army.ArmyId, record.FormalArmyId, StringComparison.Ordinal) &&
                        !string.IsNullOrWhiteSpace(army.SurfaceId) && Finite(army.WorldX) && Finite(army.WorldY))
                    {
                        point = new WorldVec2(army.WorldX, army.WorldY); surfaceId = army.SurfaceId.Trim();
                        return true;
                    }
                }
            return false;
        }

        static Result Invalid(string reason) => Result.Failure(
            ErrorCode.SnapshotInvalid, "Legacy pending battle cannot migrate to CharacterEncounter.", reason);
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
