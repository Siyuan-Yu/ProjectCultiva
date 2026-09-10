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
        /// <summary>当前整组地点所属的外层物理 MapLayout；不改变单个地点 LocalMapId 的 Interior 语义。</summary>
        public string ActiveMapLayoutId { get; set; } = string.Empty;

        public IReadOnlyDictionary<string, WorldLocationState> Locations => _locations;

        public bool TryGet(string id, out WorldLocationState location)
        {
            location = null;
            if (string.IsNullOrEmpty(id))
                return false;
            return _locations.TryGetValue(id, out location);
        }

        public void ClearLocations()
        {
            _locations.Clear();
            ActiveMapLayoutId = string.Empty;
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

    /// <summary>Unified location lookup for active continuous places and legacy WorldRegion.</summary>
    public static class WorldLocationQuery
    {
        public static bool TryGet(
            XianXia.Core.Simulation.SimulationWorld world,
            string locationId,
            out WorldLocationState location)
        {
            location = null;
            if (world == null || string.IsNullOrEmpty(locationId))
                return false;
            return world.ContinuousOutdoorMaterialization.TryGetAnyPlace(locationId, out location) ||
                   world.WorldRegion.TryGet(locationId, out location);
        }
    }

    /// <summary>
    /// Transient presentation scope for the loaded continuous outdoor neighborhood. It is
    /// independent from the single Active LocalMap/WorldRegion compatibility boards.
    /// </summary>
    public sealed class ContinuousOutdoorMaterializationBoard
    {
        readonly HashSet<XianXia.Core.Domain.Ids.EntityId> _entities =
            new HashSet<XianXia.Core.Domain.Ids.EntityId>();
        readonly HashSet<string> _loadedSites = new HashSet<string>(StringComparer.Ordinal);
        readonly Dictionary<string, WorldLocationState> _places =
            new Dictionary<string, WorldLocationState>(StringComparer.Ordinal);
        readonly Dictionary<string, WorldLocationState> _placesByLocationId =
            new Dictionary<string, WorldLocationState>(StringComparer.Ordinal);
        readonly List<XianXia.Core.Domain.Ids.EntityId> _entityScratch =
            new List<XianXia.Core.Domain.Ids.EntityId>();
        public int PlaceRevision { get; private set; }
        public int EntityReconcileRevision { get; private set; }

        public IReadOnlyCollection<XianXia.Core.Domain.Ids.EntityId> Entities => _entities;
        public IReadOnlyCollection<string> LoadedSiteIds => _loadedSites;
        public int PlaceCount => _places.Count;
        public IReadOnlyDictionary<string, WorldLocationState> PlacesByLocationId => _placesByLocationId;

        public void Clear()
        {
            _entities.Clear();
            ClearPlaces();
        }

        public void ClearPlaces()
        {
            PlaceRevision++;
            _loadedSites.Clear();
            _places.Clear();
            _placesByLocationId.Clear();
        }

        public void RegisterLoadedSite(string siteId)
        {
            if (!string.IsNullOrEmpty(siteId)) _loadedSites.Add(siteId);
        }

        public bool IsSiteLoaded(string siteId) =>
            !string.IsNullOrEmpty(siteId) && _loadedSites.Contains(siteId);

        public void Materialize(XianXia.Core.Domain.Ids.EntityId id)
        {
            if (!id.IsNone) _entities.Add(id);
        }

        public bool IsMaterialized(XianXia.Core.Domain.Ids.EntityId id) =>
            !id.IsNone && _entities.Contains(id);

        public void ReconcileEntities(
            IEnumerable<XianXia.Core.Domain.Ids.EntityId> desired,
            Action<XianXia.Core.Domain.Ids.EntityId> onAdd,
            Action<XianXia.Core.Domain.Ids.EntityId> onRemove)
        {
            EntityReconcileRevision++;
            var desiredSet = desired as HashSet<XianXia.Core.Domain.Ids.EntityId> ??
                             new HashSet<XianXia.Core.Domain.Ids.EntityId>(desired ??
                                 Array.Empty<XianXia.Core.Domain.Ids.EntityId>());
            _entityScratch.Clear();
            foreach (var id in _entities)
                if (!desiredSet.Contains(id)) _entityScratch.Add(id);
            for (var i = 0; i < _entityScratch.Count; i++)
            {
                var id = _entityScratch[i];
                onRemove?.Invoke(id);
                _entities.Remove(id);
            }
            foreach (var id in desiredSet)
            {
                if (id.IsNone || !_entities.Add(id)) continue;
                onAdd?.Invoke(id);
            }
            _entityScratch.Clear();
        }

        public void RegisterPlace(string siteId, WorldLocationState place)
        {
            if (string.IsNullOrEmpty(siteId) || place == null || string.IsNullOrEmpty(place.Id)) return;
            _places[siteId + "\n" + place.Id] = place;
            _placesByLocationId[place.Id] = place;
        }

        public bool TryGetPlace(string siteId, string locationId, out WorldLocationState place)
        {
            place = null;
            return !string.IsNullOrEmpty(siteId) && !string.IsNullOrEmpty(locationId) &&
                   _places.TryGetValue(siteId + "\n" + locationId, out place);
        }

        public bool TryGetAnyPlace(string locationId, out WorldLocationState place)
        {
            place = null;
            return !string.IsNullOrEmpty(locationId) && _placesByLocationId.TryGetValue(locationId, out place);
        }

        public void CopyPlacesTo(WorldRegionBoard target)
        {
            if (target == null) return;
            foreach (var pair in _placesByLocationId) target.Register(pair.Value);
        }
    }
}
