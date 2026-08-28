using System;
using System.Text;
using System.Text.RegularExpressions;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Persistence;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>LevelTester Snapshot 页签最小计数摘要。</summary>
    public static class HostLevelTesterSnapshotSummary
    {
        public sealed class Counts
        {
            public int CharacterCount;
            public int PlayerPartyCount;
            public int FormalArmyCount;
            public string WorldLocation = string.Empty;
            public string PlayerPartyDetail = string.Empty;
            public string LocalPlacementsDetail = string.Empty;
        }

        public static Counts LastSaved { get; private set; } = new Counts();
        public static Counts LastRuntime { get; private set; } = new Counts();

        public static void RecordSavedFromJson(string json)
        {
            LastSaved = ParseJsonCounts(json);
        }

        public static void RecordRuntime(SimulationWorld world, PlayableHostSession session)
        {
            LastRuntime = BuildRuntimeCounts(world, session);
        }

        static Counts ParseJsonCounts(string json)
        {
            var counts = new Counts();
            if (string.IsNullOrEmpty(json))
                return counts;

            counts.CharacterCount = CountRegexMatches(json, "\"definitionId\"\\s*:");
            counts.FormalArmyCount = CountRegexMatches(json, "\"armyId\"\\s*:");
            counts.PlayerPartyCount = CountPlayerPartyMembersInJson(json);
            counts.WorldLocation = ExtractTravelSite(json);
            counts.PlayerPartyDetail = ExtractSavedPlayerPartyDetail(json);
            counts.LocalPlacementsDetail = ExtractSavedLocalPlacementsDetail(json);
            return counts;
        }

        static Counts BuildRuntimeCounts(SimulationWorld world, PlayableHostSession session)
        {
            var counts = new Counts();
            if (world == null)
                return counts;

            foreach (var entity in world.Entities.All)
            {
                if (entity != null && (entity.Tags & EntityTag.Character) != 0)
                    counts.CharacterCount++;
            }

            counts.PlayerPartyCount = session?.PlayerParty?.Count ?? 0;
            counts.FormalArmyCount = world.Strategic?.FormalArmies?.Armies?.Count ?? 0;
            counts.PlayerPartyDetail = BuildRuntimePlayerPartyDetail(session);
            counts.LocalPlacementsDetail = BuildRuntimeLocalPlacementsDetail(world, session);

            var travel = world.PlayerPartyTravel;
            if (travel != null && travel.HasPosition)
            {
                counts.WorldLocation = travel.LocationKind == PlayerPartyLocationKind.AtWorldSite
                    ? "Site:" + (travel.SiteId ?? string.Empty)
                    : "WorldPos(" + travel.WorldPosition.X.ToString("0.#") + "," +
                      travel.WorldPosition.Y.ToString("0.#") + ")";
            }
            else if (!string.IsNullOrEmpty(world.PartyWorld?.SiteId))
            {
                counts.WorldLocation = "Site:" + world.PartyWorld.SiteId;
            }
            else if (!string.IsNullOrEmpty(world.LocalMap?.ActiveMapLayoutId))
            {
                counts.WorldLocation = "Map:" + world.LocalMap.ActiveMapLayoutId;
            }

            return counts;
        }

        static string BuildRuntimePlayerPartyDetail(PlayableHostSession session)
        {
            var party = session?.PlayerParty;
            if (party == null || !party.HasActive)
                return "Restored PlayerParty: (none)";

            var sb = new StringBuilder();
            sb.Append("Restored PlayerParty Active=").Append(party.ActiveCharacterId.Value);
            sb.Append(" Members=");
            for (var i = 0; i < party.Members.Count; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.Append(party.Members[i].Value);
            }

            return sb.ToString();
        }

        static string BuildRuntimeLocalPlacementsDetail(SimulationWorld world, PlayableHostSession session)
        {
            if (world?.LocalMap == null)
                return string.Empty;

            var mapId = world.LocalMap.ActiveMapLayoutId?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(mapId))
                return string.Empty;

            var party = session?.PlayerParty;
            if (party == null || party.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            sb.Append("Restored Local Placements:");
            var any = false;
            for (var i = 0; i < party.Members.Count; i++)
            {
                var id = party.Members[i];
                if (!world.Entities.TryGet(id, out var ent) ||
                    !ent.TryGet<EntityLocationComponent>(out var loc) ||
                    !loc.HasPresentationOverride)
                    continue;
                if (!world.LocalMap.ContainsOccupant(id))
                    continue;

                any = true;
                sb.Append(' ').Append(id.Value).Append('=').Append(mapId).Append('/')
                    .Append('(').Append(loc.PresentationOverrideX.ToString("0.#")).Append(',')
                    .Append(loc.PresentationOverrideZ.ToString("0.#")).Append(')');
            }

            return any ? sb.ToString() : string.Empty;
        }

        static string ExtractSavedPlayerPartyDetail(string json)
        {
            var activeMatch = Regex.Match(
                json,
                "\"playerParty\"\\s*:\\s*\\{[^}]*\"activeCharacterId\"\\s*:\\s*(?<active>\"?\\d+\"?)",
                RegexOptions.CultureInvariant);
            var membersMatch = Regex.Match(
                json,
                "\"memberCharacterIds\"\\s*:\\s*\\[(?<body>[^\\]]*)\\]",
                RegexOptions.CultureInvariant);
            if (!activeMatch.Success && !membersMatch.Success)
                return string.Empty;

            var active = activeMatch.Success
                ? activeMatch.Groups["active"].Value.Trim('"')
                : "?";
            var members = membersMatch.Success
                ? membersMatch.Groups["body"].Value.Trim()
                : string.Empty;
            return "Saved PlayerParty Active=" + active + " Members=[" + members + "]";
        }

        static string ExtractSavedLocalPlacementsDetail(string json)
        {
            var matches = Regex.Matches(
                json,
                "\"characterId\"\\s*:\\s*(?<id>\"?\\d+\"?)[^}]*\"localMapId\"\\s*:\\s*\"(?<map>[^\"]*)\"[^}]*\"localX\"\\s*:\\s*(?<x>-?[\\d.]+)[^}]*\"localZ\"\\s*:\\s*(?<z>-?[\\d.]+)",
                RegexOptions.CultureInvariant);
            if (matches.Count == 0)
                return string.Empty;

            var sb = new StringBuilder("Saved Local Placements:");
            for (var i = 0; i < matches.Count; i++)
            {
                var m = matches[i];
                sb.Append(' ').Append(m.Groups["id"].Value.Trim('"')).Append('=')
                    .Append(m.Groups["map"].Value).Append('/')
                    .Append('(').Append(m.Groups["x"].Value).Append(',')
                    .Append(m.Groups["z"].Value).Append(')');
            }

            return sb.ToString();
        }

        static int CountRegexMatches(string text, string pattern)
        {
            try
            {
                return Regex.Matches(text, pattern, RegexOptions.CultureInvariant).Count;
            }
            catch
            {
                return 0;
            }
        }

        static int CountPlayerPartyMembersInJson(string json)
        {
            var explicitMembers = CountRegexMatches(json, "\"memberCharacterIds\"\\s*:");
            if (explicitMembers > 0)
            {
                var m = Regex.Match(
                    json,
                    "\"playerParty\"\\s*:\\s*\\{[^}]*\"memberCharacterIds\"\\s*:\\s*\\[(?<body>[^\\]]*)\\]",
                    RegexOptions.CultureInvariant);
                if (m.Success)
                {
                    var body = m.Groups["body"].Value;
                    return string.IsNullOrWhiteSpace(body)
                        ? 0
                        : CountRegexMatches(body, "\"\\d+\"|\\d+");
                }
            }

            return CountRegexMatches(json, "\"characterId\"\\s*:");
        }

        static string ExtractTravelSite(string json)
        {
            var m = Regex.Match(
                json,
                "\"playerPartyTravel\"\\s*:\\s*\\{[^}]*\"siteId\"\\s*:\\s*\"(?<site>[^\"]*)\"",
                RegexOptions.CultureInvariant);
            return m.Success ? "Site:" + m.Groups["site"].Value : string.Empty;
        }
    }
}
