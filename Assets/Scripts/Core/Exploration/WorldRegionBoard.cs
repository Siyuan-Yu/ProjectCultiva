using System;
using System.Collections.Generic;

namespace XianXia.Core.Exploration
{
    public sealed class WorldRegionBoard
    {
        readonly Dictionary<string, WorldLocationState> _locations =
            new Dictionary<string, WorldLocationState>(StringComparer.Ordinal);

        public string RegionId { get; set; } = string.Empty;
        public string RegionName { get; set; } = string.Empty;
        public string StartLocationId { get; set; } = string.Empty;

        public IReadOnlyDictionary<string, WorldLocationState> Locations => _locations;

        public bool TryGet(string id, out WorldLocationState location)
        {
            location = null;
            if (string.IsNullOrEmpty(id))
                return false;
            return _locations.TryGetValue(id, out location);
        }

        public void Register(WorldLocationState location)
        {
            if (location == null || string.IsNullOrEmpty(location.Id))
                throw new ArgumentException("WorldLocationState requires Id.");
            _locations[location.Id] = location;
        }

        public bool AreAdjacent(string fromId, string toId)
        {
            if (!TryGet(fromId, out var from))
                return false;
            for (var i = 0; i < from.AdjacentIds.Count; i++)
            {
                if (string.Equals(from.AdjacentIds[i], toId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}
