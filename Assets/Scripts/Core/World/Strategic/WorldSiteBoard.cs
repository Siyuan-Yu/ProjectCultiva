using System;
using System.Collections.Generic;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    public sealed class WorldSiteBoard
    {
        readonly Dictionary<string, WorldSite> _sitesById =
            new Dictionary<string, WorldSite>(StringComparer.Ordinal);
        readonly Dictionary<HexCoord, string> _siteIdByHex = new Dictionary<HexCoord, string>();

        public IReadOnlyDictionary<string, WorldSite> Sites => _sitesById;

        public void Clear()
        {
            _sitesById.Clear();
            _siteIdByHex.Clear();
        }

        public void Register(WorldSite site)
        {
            if (site == null || string.IsNullOrEmpty(site.SiteId))
                throw new ArgumentException("WorldSite requires SiteId.");

            foreach (var hex in site.EnumerateFootprintHexes())
            {
                if (_siteIdByHex.TryGetValue(hex, out var existingId) &&
                    !string.Equals(existingId, site.SiteId, StringComparison.Ordinal))
                    throw new InvalidOperationException("Hex already occupied by another site: " + hex);
            }

            _sitesById[site.SiteId] = site;
            foreach (var hex in site.EnumerateFootprintHexes())
                _siteIdByHex[hex] = site.SiteId;
        }

        public bool TryGet(string siteId, out WorldSite site) =>
            _sitesById.TryGetValue(siteId, out site) && site != null;

        public bool TryGetAtHex(HexCoord coord, out WorldSite site)
        {
            site = null;
            return _siteIdByHex.TryGetValue(coord, out var siteId) && TryGet(siteId, out site);
        }

        public bool TryResolveSiteAnchorHex(string siteId, out HexCoord coord)
        {
            coord = default;
            if (!TryGet(siteId, out var site) || site == null)
                return false;
            coord = site.AnchorHex;
            return true;
        }

        public bool TryResolveSiteHex(string siteId, out HexCoord coord) =>
            TryResolveSiteAnchorHex(siteId, out coord);
    }
}
