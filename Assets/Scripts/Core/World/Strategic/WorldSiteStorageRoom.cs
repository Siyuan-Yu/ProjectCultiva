using System;
using System.Collections.Generic;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Physical storage facility binding. It owns no inventory or resource quantity.</summary>
    public sealed class WorldSiteStorageRoomState
    {
        public string StorageRoomId { get; set; } = string.Empty;
        public string SiteId { get; set; } = string.Empty;
        public string SurfaceId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = "储藏室";
        public float WorldX { get; set; }
        public float WorldY { get; set; }
    }

    /// <summary>Runtime-derived lookup rebuilt from authored placements and constructed assets.</summary>
    public sealed class WorldSiteStorageRoomBoard
    {
        readonly Dictionary<string, WorldSiteStorageRoomState> _byId =
            new Dictionary<string, WorldSiteStorageRoomState>(StringComparer.Ordinal);
        readonly Dictionary<string, WorldSiteStorageRoomState> _bySite =
            new Dictionary<string, WorldSiteStorageRoomState>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, WorldSiteStorageRoomState> All => _byId;
        public void Clear() { _byId.Clear(); _bySite.Clear(); }

        public bool TryRegister(WorldSiteStorageRoomState room)
        {
            if (room == null || string.IsNullOrWhiteSpace(room.StorageRoomId) ||
                string.IsNullOrWhiteSpace(room.SiteId) || string.IsNullOrWhiteSpace(room.SurfaceId) ||
                float.IsNaN(room.WorldX) || float.IsInfinity(room.WorldX) ||
                float.IsNaN(room.WorldY) || float.IsInfinity(room.WorldY) ||
                _byId.ContainsKey(room.StorageRoomId) || _bySite.ContainsKey(room.SiteId)) return false;
            _byId.Add(room.StorageRoomId, room);
            _bySite.Add(room.SiteId, room);
            return true;
        }

        public bool TryGet(string storageRoomId, out WorldSiteStorageRoomState room)
        {
            room = null;
            return !string.IsNullOrEmpty(storageRoomId) && _byId.TryGetValue(storageRoomId, out room);
        }

        public bool TryGetBySite(string siteId, out WorldSiteStorageRoomState room)
        {
            room = null;
            return !string.IsNullOrEmpty(siteId) && _bySite.TryGetValue(siteId, out room);
        }

        public bool HasActiveStorageForSite(SimulationWorld world, string siteId) =>
            TryGetBySite(siteId, out _) && world?.Strategic?.Sites != null &&
            world.Strategic.Sites.TryGet(siteId, out var site) && site != null && site.IsCoreActive;

        internal void Remove(string storageRoomId)
        {
            if (!_byId.TryGetValue(storageRoomId ?? string.Empty, out var room)) return;
            _byId.Remove(storageRoomId);
            _bySite.Remove(room.SiteId);
        }
    }
}
