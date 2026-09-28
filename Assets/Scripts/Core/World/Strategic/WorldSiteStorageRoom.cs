using System;
using System.Collections.Generic;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Physical storage facility binding. It owns no inventory or resource quantity.</summary>
    public sealed class WorldSiteStorageRoomState
    {
        public string StorageRoomId { get; set; } = string.Empty;
        public string SiteId { get; set; } = string.Empty;
        public string SurfaceId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = "储藏室";
        /// <summary>Authored footprint center, retained for diagnostics and map presentation.</summary>
        public float WorldX { get; set; }
        public float WorldY { get; set; }
        public float FootprintWorldX { get; set; }
        public float FootprintWorldY { get; set; }
        public float WorldWidth { get; set; }
        public float WorldHeight { get; set; }

        public const int AccessCandidateCount = 8;

        /// <summary>Stable footprint-derived slots: two on each side, clockwise.</summary>
        public bool TryGetAccessCandidate(int index, out WorldVec2 access)
        {
            access = default;
            if (!Finite(FootprintWorldX) || !Finite(FootprintWorldY) ||
                !Finite(WorldWidth) || !Finite(WorldHeight) || WorldWidth <= 0f || WorldHeight <= 0f)
                return false;
            var clearance = Math.Min(.5f, Math.Max(.01f, Math.Min(WorldWidth, WorldHeight) * .25f));
            var minX = FootprintWorldX;
            var maxX = FootprintWorldX + WorldWidth;
            var minY = FootprintWorldY;
            var maxY = FootprintWorldY + WorldHeight;
            var x1 = minX + WorldWidth * .33f;
            var x2 = minX + WorldWidth * .67f;
            var y1 = minY + WorldHeight * .33f;
            var y2 = minY + WorldHeight * .67f;
            switch ((index % AccessCandidateCount + AccessCandidateCount) % AccessCandidateCount)
            {
                case 0: access = new WorldVec2(x1, minY - clearance); break;
                case 1: access = new WorldVec2(x2, minY - clearance); break;
                case 2: access = new WorldVec2(maxX + clearance, y1); break;
                case 3: access = new WorldVec2(maxX + clearance, y2); break;
                case 4: access = new WorldVec2(x2, maxY + clearance); break;
                case 5: access = new WorldVec2(x1, maxY + clearance); break;
                case 6: access = new WorldVec2(minX - clearance, y2); break;
                default: access = new WorldVec2(minX - clearance, y1); break;
            }
            return true;
        }

        /// <summary>Compatibility nearest-point query. Food movement rotates stable slots instead.</summary>
        public bool TryResolveAccessPoint(WorldVec2 from, out WorldVec2 access)
        {
            access = default;
            if (!Finite(FootprintWorldX) || !Finite(FootprintWorldY) ||
                !Finite(WorldWidth) || !Finite(WorldHeight) || WorldWidth <= 0f || WorldHeight <= 0f)
                return false;

            // Derived from the footprint so authored surfaces with small world units do not
            // place the interaction target many cells away from the building.
            var clearance = Math.Min(.5f, Math.Max(.01f, Math.Min(WorldWidth, WorldHeight) * .25f));
            var minX = FootprintWorldX;
            var maxX = FootprintWorldX + WorldWidth;
            var minY = FootprintWorldY;
            var maxY = FootprintWorldY + WorldHeight;
            var clampedX = Clamp(from.X, minX, maxX);
            var clampedY = Clamp(from.Y, minY, maxY);
            var candidates = new[] {
                new WorldVec2(minX - clearance, clampedY),
                new WorldVec2(maxX + clearance, clampedY),
                new WorldVec2(clampedX, minY - clearance),
                new WorldVec2(clampedX, maxY + clearance)
            };
            var best = 0;
            var bestDistance = DistanceSquared(from, candidates[0]);
            for (var i = 1; i < candidates.Length; i++)
            {
                var distance = DistanceSquared(from, candidates[i]);
                if (distance >= bestDistance) continue;
                best = i;
                bestDistance = distance;
            }
            access = candidates[best];
            return true;
        }

        static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;
        static float DistanceSquared(WorldVec2 a, WorldVec2 b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
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
                float.IsNaN(room.FootprintWorldX) || float.IsInfinity(room.FootprintWorldX) ||
                float.IsNaN(room.FootprintWorldY) || float.IsInfinity(room.FootprintWorldY) ||
                float.IsNaN(room.WorldWidth) || float.IsInfinity(room.WorldWidth) || room.WorldWidth <= 0f ||
                float.IsNaN(room.WorldHeight) || float.IsInfinity(room.WorldHeight) || room.WorldHeight <= 0f ||
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
