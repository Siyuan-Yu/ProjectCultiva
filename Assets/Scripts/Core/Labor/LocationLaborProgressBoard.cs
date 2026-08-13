using System;
using System.Collections.Generic;

namespace XianXia.Core.Labor
{
    /// <summary>
    /// Per-character cumulative player Labor ticks／harvests at a world location (quest objectives).
    /// </summary>
    public sealed class LocationLaborProgressBoard
    {
        /// <summary>Matches Host default <c>secondsPerAutoTickAt1x</c> for content amount→ticks.</summary>
        public const int DefaultSecondsPerTick = 3;

        readonly Dictionary<string, int> _ticks =
            new Dictionary<string, int>(StringComparer.Ordinal);
        readonly Dictionary<string, int> _harvests =
            new Dictionary<string, int>(StringComparer.Ordinal);

        public void Add(string characterDefinitionId, string locationId, int amount = 1)
        {
            if (amount <= 0 ||
                string.IsNullOrEmpty(characterDefinitionId) ||
                string.IsNullOrEmpty(locationId))
                return;
            var key = Key(characterDefinitionId, locationId);
            _ticks.TryGetValue(key, out var cur);
            _ticks[key] = cur + amount;
        }

        public void AddHarvest(string characterDefinitionId, string locationId, int amount = 1)
        {
            if (amount <= 0 ||
                string.IsNullOrEmpty(characterDefinitionId) ||
                string.IsNullOrEmpty(locationId))
                return;
            var key = Key(characterDefinitionId, locationId);
            _harvests.TryGetValue(key, out var cur);
            _harvests[key] = cur + amount;
        }

        public int GetTicks(string characterDefinitionId, string locationId)
        {
            if (string.IsNullOrEmpty(characterDefinitionId) || string.IsNullOrEmpty(locationId))
                return 0;
            return _ticks.TryGetValue(Key(characterDefinitionId, locationId), out var v) ? v : 0;
        }

        public int GetHarvests(string characterDefinitionId, string locationId)
        {
            if (string.IsNullOrEmpty(characterDefinitionId) || string.IsNullOrEmpty(locationId))
                return 0;
            return _harvests.TryGetValue(Key(characterDefinitionId, locationId), out var v) ? v : 0;
        }

        /// <summary>Content <c>amount</c> is wall seconds at default tick pace.</summary>
        public static int SecondsToRequiredTicks(int seconds)
        {
            if (seconds <= 0)
                return 1;
            return (seconds + DefaultSecondsPerTick - 1) / DefaultSecondsPerTick;
        }

        public bool MeetsSeconds(string characterDefinitionId, string locationId, int seconds) =>
            GetTicks(characterDefinitionId, locationId) >= SecondsToRequiredTicks(seconds);

        public bool HasHarvested(string characterDefinitionId, string locationId, int min = 1) =>
            GetHarvests(characterDefinitionId, locationId) >= min;

        static string Key(string characterDefinitionId, string locationId) =>
            characterDefinitionId + "|" + locationId;
    }
}
