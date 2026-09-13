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
            foreach (var p in state.Participants)
                participants.Add(JsonValue.FromObject(new Dictionary<string, JsonValue>
                {
                ["characterId"] = JsonValue.FromString(p.CharacterId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                ["squadId"] = JsonValue.FromString(p.SquadId),
                ["enemy"] = JsonValue.FromBool(p.Enemy),
                ["sourceSiteId"] = JsonValue.FromString(p.SourceSiteId),
                ["sourceMode"] = JsonValue.FromNumber(p.SourceMode),
                ["originX"] = JsonValue.FromNumber(p.OriginX),
                ["originY"] = JsonValue.FromNumber(p.OriginY),
                ["tacticalX"] = JsonValue.FromNumber(p.TacticalX),
                ["tacticalY"] = JsonValue.FromNumber(p.TacticalY),
                ["targetId"] = JsonValue.FromString(p.TargetId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                ["cooldown"] = JsonValue.FromNumber(p.Cooldown),
                ["joinedAt"] = JsonValue.FromNumber(p.JoinedAt),
                ["entryCondition"] = JsonValue.FromNumber((int)p.EntryCondition),
                ["entryHpAvailable"] = JsonValue.FromBool(p.EntryHpAvailable),
                ["entryHp"] = JsonValue.FromNumber(p.EntryHp),
                ["entryMaxHp"] = JsonValue.FromNumber(p.EntryMaxHp),
                }));
            return JsonValue.FromObject(new Dictionary<string, JsonValue>
            {
                ["version"] = JsonValue.FromNumber(state.Version),
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
                ["participants"] = JsonValue.FromArray(participants)
            });
        }
        public static CharacterEncounterState Read(JsonValue value)
        {
            if (!value.TryGetProperty("version", out _)) throw new FormatException("Independent encounter version missing.");
            var state = new CharacterEncounterState
            {
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
            if (!value.TryGetProperty("participants", out var rows) || rows.Kind != JsonValueKind.Array)
                throw new FormatException("Independent encounter participant list missing.");
            foreach (var row in rows.Array)
                state.Participants.Add(new EncounterCharacter
                {
                CharacterId = ulong.Parse(row.GetString("characterId", "0"), System.Globalization.CultureInfo.InvariantCulture),
                SquadId = row.GetString("squadId", ""),
                Enemy = row.GetBool("enemy", false),
                SourceSiteId = row.GetString("sourceSiteId", ""),
                SourceMode = (int)row.GetNumber("sourceMode", 0),
                OriginX = (float)row.GetNumber("originX", 0),
                OriginY = (float)row.GetNumber("originY", 0),
                TacticalX = (float)row.GetNumber("tacticalX", 0),
                TacticalY = (float)row.GetNumber("tacticalY", 0),
                TargetId = ulong.Parse(row.GetString("targetId", "0"), System.Globalization.CultureInfo.InvariantCulture),
                Cooldown = (float)row.GetNumber("cooldown", 0),
                JoinedAt = (float)row.GetNumber("joinedAt", 0),
                EntryCondition = (ManualBattleReportCondition)row.GetNumber("entryCondition", 0),
                EntryHpAvailable = row.GetBool("entryHpAvailable", false),
                EntryHp = (int)row.GetNumber("entryHp", 0),
                EntryMaxHp = (int)row.GetNumber("entryMaxHp", 0),
                });
            return state;
        }
    }
}
