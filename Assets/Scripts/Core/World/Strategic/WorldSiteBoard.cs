using System;
using System.Collections.Generic;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    public sealed class WorldSiteBoard
    {
        readonly Dictionary<string, WorldSite> _sitesById =
            new Dictionary<string, WorldSite>(StringComparer.Ordinal);
        readonly Dictionary<HexCoord, List<string>> _siteIdsByLegacyHex =
            new Dictionary<HexCoord, List<string>>();

        public IReadOnlyDictionary<string, WorldSite> Sites => _sitesById;

        public void Clear()
        {
            _sitesById.Clear();
            _siteIdsByLegacyHex.Clear();
        }

        public void Register(WorldSite site)
        {
            if (site == null || string.IsNullOrEmpty(site.SiteId))
                throw new ArgumentException("WorldSite requires SiteId.");

            if (_sitesById.ContainsKey(site.SiteId))
                throw new InvalidOperationException("WorldSite identity already registered: " + site.SiteId);
            _sitesById[site.SiteId] = site;
            site.EnsureLegacyPresenceHexValid();
            foreach (var hex in site.EnumerateLegacyFootprintHexes())
            {
                if (!_siteIdsByLegacyHex.TryGetValue(hex, out var ids))
                {
                    ids = new List<string>();
                    _siteIdsByLegacyHex.Add(hex, ids);
                }
                ids.Add(site.SiteId);
                ids.Sort(StringComparer.Ordinal);
            }
        }

        public bool TryGet(string siteId, out WorldSite site) =>
            _sitesById.TryGetValue(siteId, out site) && site != null;

        public bool TryGetAtLegacyHex(HexCoord coord, out WorldSite site)
        {
            site = null;
            if (!_siteIdsByLegacyHex.TryGetValue(coord, out var ids))
                return false;
            for (var i = 0; i < ids.Count; i++)
                if (TryGet(ids[i], out site) &&
                    !(site.IsRuntimeCreated && !site.IsCoreActive))
                    return true;
            return false;
        }

        public IReadOnlyList<string> GetSiteIdsAtLegacyHex(HexCoord coord)
        {
            if (!_siteIdsByLegacyHex.TryGetValue(coord, out var ids)) return Array.Empty<string>();
            var active = new List<string>(ids.Count);
            for (var i = 0; i < ids.Count; i++)
                if (TryGet(ids[i], out var site) &&
                    !(site.IsRuntimeCreated && !site.IsCoreActive))
                    active.Add(ids[i]);
            return active;
        }

        public bool RemoveRuntimeSite(string siteId)
        {
            if (!_sitesById.TryGetValue(siteId ?? string.Empty, out var site) || site == null ||
                !site.IsRuntimeCreated)
                return false;
            _sitesById.Remove(siteId);
            foreach (var hex in site.EnumerateLegacyFootprintHexes())
            {
                if (!_siteIdsByLegacyHex.TryGetValue(hex, out var ids))
                    continue;
                ids.Remove(siteId);
                if (ids.Count == 0)
                    _siteIdsByLegacyHex.Remove(hex);
            }
            return true;
        }

        public void RemoveAllRuntimeSites()
        {
            var ids = new List<string>();
            foreach (var pair in _sitesById)
                if (pair.Value != null && pair.Value.IsRuntimeCreated)
                    ids.Add(pair.Key);
            for (var i = 0; i < ids.Count; i++)
                RemoveRuntimeSite(ids[i]);
        }

        /// <summary>
        /// Character World Presence 旧格兼容：Site → LegacyPresenceHex；
        /// 当前 invariant 为 LegacyPresenceHex == LegacyAnchorHex。
        /// </summary>
        public bool TryResolveLegacySitePresenceHex(string siteId, out HexCoord coord)
        {
            coord = default;
            if (!TryGet(siteId, out var site) || site == null)
                return false;
            site.EnsureLegacyPresenceHexValid();
            coord = site.LegacyPresenceHex;
            return true;
        }
    }
}
