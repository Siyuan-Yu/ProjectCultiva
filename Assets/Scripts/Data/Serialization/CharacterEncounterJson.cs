using System;
using System.Collections.Generic;
using XianXia.Core.World.Strategic;
namespace XianXia.Data.Serialization
{
    public static class CharacterEncounterJson
    {
        public static JsonValue Write(CharacterEncounterState state)
        {
            if (state == null) return JsonValue.FromObject(new Dictionary<string, JsonValue>());
            var participants = new List<JsonValue>();
            var objectiveSquads = new List<JsonValue>();
            foreach (var id in state.ObjectiveDefenderSquads) objectiveSquads.Add(JsonValue.FromString(id));
            foreach (var p in state.Participants)
                participants.Add(WritePerson(p));
            var candidates = new List<JsonValue>();
            foreach (var c in state.Candidates)
            {
                var members = new List<JsonValue>();
                foreach (var p in c.Members) members.Add(WritePerson(p));
                candidates.Add(JsonValue.FromObject(new Dictionary<string, JsonValue>
                {
                    ["squadId"] = JsonValue.FromString(c.SquadId),
                    ["phase"] = JsonValue.FromNumber((int)c.Phase),
                    ["enemy"] = JsonValue.FromBool(c.Enemy),
                    ["roll"] = JsonValue.FromNumber(c.Roll),
                    ["affinityDifference"] = JsonValue.FromNumber(c.AffinityDifference),
                    ["arriveAt"] = JsonValue.FromNumber(c.ArriveAt),
                    ["members"] = JsonValue.FromArray(members)
                }));
            }
            return JsonValue.FromObject(new Dictionary<string, JsonValue>
            {
                ["version"] = JsonValue.FromNumber(state.Version),
                ["objective"] = WriteObjective(state.Objective),
                ["objectiveDefenderSquads"] = JsonValue.FromArray(objectiveSquads),
                ["id"] = JsonValue.FromString(state.EncounterId),
                ["sourceSurfaceId"] = JsonValue.FromString(state.SourceSurfaceId),
                ["sourceSiteId"] = JsonValue.FromString(state.SourceSiteId),
                ["centerX"] = JsonValue.FromNumber(state.CenterX),
                ["centerY"] = JsonValue.FromNumber(state.CenterY),
                ["width"] = JsonValue.FromNumber(state.Width),
                ["height"] = JsonValue.FromNumber(state.Height),
                ["elapsedSeconds"] = JsonValue.FromNumber(state.ElapsedSeconds),
                ["decayAccumulator"] = JsonValue.FromNumber(state.DecayAccumulator),
                ["phase"] = JsonValue.FromNumber((int)state.Phase),
                ["playerWon"] = JsonValue.FromBool(state.PlayerWon),
                ["rosterVersion"] = JsonValue.FromNumber(state.RosterVersion),
                ["continuationUsed"] = JsonValue.FromBool(state.ContinuationUsed),
                ["decisionAt"] = JsonValue.FromNumber(state.DecisionAt),
                ["arrivalDelay"] = JsonValue.FromNumber(state.ArrivalDelay),
                ["relationThreshold"] = JsonValue.FromNumber(state.RelationThreshold),
                ["chanceBasisPoints"] = JsonValue.FromNumber(state.ChanceBasisPoints),
                ["candidates"] = JsonValue.FromArray(candidates),
                ["participants"] = JsonValue.FromArray(participants)
            });
        }
        public static CharacterEncounterState Read(JsonValue value)
        {
            RequireFields(value, "version", "id", "sourceSurfaceId", "sourceSiteId", "centerX", "centerY", "width", "height",
                "elapsedSeconds", "decayAccumulator", "phase", "playerWon", "rosterVersion", "continuationUsed",
                "decisionAt", "arrivalDelay", "relationThreshold", "chanceBasisPoints", "participants", "candidates");
            if (!value.TryGetProperty("version", out _)) throw new FormatException("Independent encounter version missing.");
            var format = value.GetNumber("version", 0);
            if (format != 1 && format != 2 && format != 3 &&
                format != CharacterEncounterState.Format)
                throw new FormatException("Unsupported encounter format.");
            var state = new CharacterEncounterState
            {
                DecisionAt = (float)value.GetNumber("decisionAt", -1),
                ArrivalDelay = (float)value.GetNumber("arrivalDelay", -1),
                RelationThreshold = (int)value.GetNumber("relationThreshold", -1),
                ChanceBasisPoints = (int)value.GetNumber("chanceBasisPoints", -1),
                Version = (int)value.GetNumber("version", 0),
                EncounterId = value.GetString("id", ""),
                SourceSurfaceId = value.GetString("sourceSurfaceId", ""),
                SourceSiteId = value.GetString("sourceSiteId", ""),
                CenterX = (float)value.GetNumber("centerX", 0),
                CenterY = (float)value.GetNumber("centerY", 0),
                Width = (float)value.GetNumber("width", 0),
                Height = (float)value.GetNumber("height", 0),
                ElapsedSeconds = (float)value.GetNumber("elapsedSeconds", 0),
                DecayAccumulator = (float)value.GetNumber("decayAccumulator", 0),
                Phase = (CharacterEncounterPhase)value.GetNumber("phase", 0),
                PlayerWon = value.GetBool("playerWon", false),
                RosterVersion = (int)value.GetNumber("rosterVersion", 0),
                ContinuationUsed = value.GetBool("continuationUsed", false),
            };
            if (state.Version == 1)
            {
                if (value.TryGetProperty("objective", out _) || value.TryGetProperty("objectiveDefenderSquads", out _))
                    throw new FormatException("Legacy encounter contains new objective fields.");
                state.Version = CharacterEncounterState.Format;
            }
            else if (state.Version == 2 || state.Version == 3 ||
                     state.Version == CharacterEncounterState.Format)
            {
                if (!value.TryGetProperty("objective", out var objective) || objective.Kind != JsonValueKind.Object ||
                    !value.TryGetProperty("objectiveDefenderSquads", out var squads) || squads.Kind != JsonValueKind.Array)
                    throw new FormatException("Encounter objective fields missing.");
                if (objective.Object.Count > 0)
                {
                    RequireFields(objective, "kind", "siteId", "assetId", "attackerFactionId", "defenderFactionId", "resolved");
                    var kind = objective.GetNumber("kind", -1);
                    if (kind != 1 && kind != 2) throw new FormatException("Invalid objective kind.");
                    state.Objective = new SiteCoreEncounterObjective { Kind = (SiteCoreObjectiveKind)kind,
                        SiteId = objective.GetString("siteId", ""), AssetId = objective.GetString("assetId", ""),
                        AttackerFactionId = objective.GetString("attackerFactionId", ""), DefenderFactionId = objective.GetString("defenderFactionId", ""),
                        Resolved = objective.GetBool("resolved", false) };
                }
                foreach (var squad in squads.Array)
                {
                    if (squad.Kind != JsonValueKind.String) throw new FormatException("Invalid objective squad id.");
                    state.ObjectiveDefenderSquads.Add(squad.String);
                }
                state.Version = CharacterEncounterState.Format;
            }
            else throw new FormatException("Unsupported encounter format.");
            if (!value.TryGetProperty("participants", out var rows) || rows.Kind != JsonValueKind.Array)
                throw new FormatException("Independent encounter participant list missing.");
            foreach (var row in rows.Array)
                state.Participants.Add(ReadPerson(row, (int)format));
            if (!value.TryGetProperty("candidates", out var candidates) || candidates.Kind != JsonValueKind.Array)
                throw new FormatException("Encounter candidates missing.");
            foreach (var row in candidates.Array)
            {
                RequireFields(row, "squadId", "phase", "enemy", "roll", "affinityDifference", "arriveAt", "members");
                var c = new EncounterCandidate {
                    SquadId = row.GetString("squadId", ""), Phase = (EncounterCandidatePhase)row.GetNumber("phase", -1),
                    Enemy = row.GetBool("enemy", false), Roll = (int)row.GetNumber("roll", -2),
                    AffinityDifference = (int)row.GetNumber("affinityDifference", 0), ArriveAt = (float)row.GetNumber("arriveAt", -1)
                };
                if (!row.TryGetProperty("members", out var members) || members.Kind != JsonValueKind.Array)
                    throw new FormatException("Candidate members missing.");
                foreach (var member in members.Array) c.Members.Add(ReadPerson(member, (int)format));
                state.Candidates.Add(c);
            }
            return state;
        }
        static JsonValue WritePerson(EncounterCharacter p) => JsonValue.FromObject(new Dictionary<string, JsonValue>
                {
                ["characterId"] = JsonValue.FromString(p.CharacterId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                ["squadId"] = JsonValue.FromString(p.SquadId),
                ["enemy"] = JsonValue.FromBool(p.Enemy),
                ["sourceSiteId"] = JsonValue.FromString(p.SourceSiteId),
                ["sourceMode"] = JsonValue.FromNumber(p.SourceMode),
                ["sourceSpatialOwnerKind"] = JsonValue.FromNumber((int)p.SourceSpatialOwnerKind),
                ["sourceSquadId"] = JsonValue.FromString(p.SourceSquadId ?? string.Empty),
                // Stable wire key retained without reintroducing retired runtime state.
                ["sourceFormalArmyId"] = JsonValue.FromString(string.Empty),
                ["originX"] = JsonValue.FromNumber(p.OriginX),
                ["originY"] = JsonValue.FromNumber(p.OriginY),
                ["returnX"] = JsonValue.FromNumber(p.ReturnX),
                ["returnY"] = JsonValue.FromNumber(p.ReturnY),
                ["tacticalX"] = JsonValue.FromNumber(p.TacticalX),
                ["tacticalY"] = JsonValue.FromNumber(p.TacticalY),
                ["targetId"] = JsonValue.FromString(p.TargetId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                ["cooldown"] = JsonValue.FromNumber(p.Cooldown),
                ["artCooldowns"] = WriteCooldowns(p.ArtCooldowns),
                ["joinedAt"] = JsonValue.FromNumber(p.JoinedAt),
                ["entryCondition"] = JsonValue.FromNumber((int)p.EntryCondition),
                ["entryHpAvailable"] = JsonValue.FromBool(p.EntryHpAvailable),
                ["entryHp"] = JsonValue.FromNumber(p.EntryHp),
                ["entryMaxHp"] = JsonValue.FromNumber(p.EntryMaxHp),
                });
        static EncounterCharacter ReadPerson(JsonValue row, int format)
        {
            RequireFields(row, "characterId", "squadId", "enemy", "sourceSiteId", "sourceMode", "originX", "originY",
                "tacticalX", "tacticalY", "targetId", "cooldown", "artCooldowns", "joinedAt", "entryCondition", "entryHpAvailable", "entryHp", "entryMaxHp");
            if (format >= 3) RequireFields(row, "sourceSpatialOwnerKind", "sourceSquadId", "sourceFormalArmyId");
            if (format >= 4) RequireFields(row, "returnX", "returnY");
            var originX = (float)row.GetNumber("originX", 0);
            var originY = (float)row.GetNumber("originY", 0);
            var ownerKind = format >= 3
                ? (int)row.GetNumber("sourceSpatialOwnerKind", 0)
                : (int)EncounterSpatialOwnerKind.Personal;
            var sourceFormalArmyId = format >= 3
                ? row.GetString("sourceFormalArmyId", "")
                : "";
            if (ownerKind == 2 || !string.IsNullOrEmpty(sourceFormalArmyId))
                throw new FormatException(
                    "Legacy FormalArmy encounter spatial metadata requires offline conversion before restore.");
            var sourceMode = (int)row.GetNumber("sourceMode", 0);
            if (sourceMode == 1 || sourceMode == 2)
                throw new FormatException(
                    "Legacy encounter character presence mode requires offline conversion before restore.");
            return new EncounterCharacter
                {
                CharacterId = ulong.Parse(row.GetString("characterId", "0"), System.Globalization.CultureInfo.InvariantCulture),
                SquadId = row.GetString("squadId", ""),
                Enemy = row.GetBool("enemy", false),
                SourceSiteId = row.GetString("sourceSiteId", ""),
                SourceMode = sourceMode,
                SourceSpatialOwnerKind = (EncounterSpatialOwnerKind)ownerKind,
                SourceSquadId = format >= 3 ? row.GetString("sourceSquadId", "") : "",
                OriginX = originX,
                OriginY = originY,
                ReturnX = format >= 4 ? (float)row.GetNumber("returnX", 0) : originX,
                ReturnY = format >= 4 ? (float)row.GetNumber("returnY", 0) : originY,
                TacticalX = (float)row.GetNumber("tacticalX", 0),
                TacticalY = (float)row.GetNumber("tacticalY", 0),
                TargetId = ulong.Parse(row.GetString("targetId", "0"), System.Globalization.CultureInfo.InvariantCulture),
                Cooldown = (float)row.GetNumber("cooldown", 0),
                ArtCooldowns = ReadCooldowns(row),
                JoinedAt = (float)row.GetNumber("joinedAt", 0),
                EntryCondition = (ManualBattleReportCondition)row.GetNumber("entryCondition", 0),
                EntryHpAvailable = row.GetBool("entryHpAvailable", false),
                EntryHp = (int)row.GetNumber("entryHp", 0),
                EntryMaxHp = (int)row.GetNumber("entryMaxHp", 0),
                };
        }
        static JsonValue WriteObjective(SiteCoreEncounterObjective o)
        {
            var fields = new Dictionary<string, JsonValue>();
            if (o != null)
            {
                fields["kind"] = JsonValue.FromNumber((int)o.Kind);
                fields["siteId"] = JsonValue.FromString(o.SiteId);
                fields["assetId"] = JsonValue.FromString(o.AssetId);
                fields["attackerFactionId"] = JsonValue.FromString(o.AttackerFactionId);
                fields["defenderFactionId"] = JsonValue.FromString(o.DefenderFactionId);
                fields["resolved"] = JsonValue.FromBool(o.Resolved);
            }
            return JsonValue.FromObject(fields);
        }

        static JsonValue WriteCooldowns(float[] cooldowns)
        {
            var result = new List<JsonValue>();
            foreach (var cooldown in cooldowns) result.Add(JsonValue.FromNumber(cooldown));
            return JsonValue.FromArray(result);
        }
        static float[] ReadCooldowns(JsonValue row)
        {
            if (!row.TryGetProperty("artCooldowns", out var values) || values.Kind != JsonValueKind.Array ||
                values.Array.Count != XianXia.Core.Combat.CombatArtsComponent.MaxEquippedSlots)
                throw new FormatException("Invalid skill cooldown slots.");
            var result = new float[values.Array.Count];
            for (var i = 0; i < result.Length; i++)
            {
                var value = values.Array[i];
                if (value.Kind != JsonValueKind.Number || double.IsNaN(value.Number) || double.IsInfinity(value.Number) || value.Number < 0)
                    throw new FormatException("Invalid skill cooldown.");
                result[i] = (float)value.Number;
            }
            return result;
        }
        static void RequireFields(JsonValue row, params string[] fields)
        {
            if (row.Kind != JsonValueKind.Object) throw new FormatException("Encounter object required.");
            foreach (var field in fields)
            {
                if (!row.TryGetProperty(field, out var value)) throw new FormatException("Encounter field missing: " + field);
                var expected = JsonValueKind.Number;
                switch (field)
                {
                    case "id": case "characterId": case "targetId": case "squadId": case "sourceSurfaceId": case "sourceSiteId":
                    case "sourceSquadId": case "sourceFormalArmyId":
                    case "siteId": case "assetId": case "attackerFactionId": case "defenderFactionId":
                        expected = JsonValueKind.String; break;
                    case "enemy": case "playerWon": case "continuationUsed": case "entryHpAvailable":
                    case "resolved":
                        expected = JsonValueKind.Boolean; break;
                    case "participants": case "candidates": case "members": case "artCooldowns":
                        expected = JsonValueKind.Array; break;
                }
                if (value.Kind != expected || (expected == JsonValueKind.Number &&
                    (double.IsNaN(value.Number) || double.IsInfinity(value.Number))))
                    throw new FormatException("Invalid encounter field type/value: " + field);
            }
        }
    }
}
