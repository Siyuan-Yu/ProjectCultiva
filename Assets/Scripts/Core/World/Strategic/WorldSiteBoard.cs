using System;
using System.Collections.Generic;

namespace XianXia.Core.World.Strategic
{
    public sealed class WorldSiteBoard
    {
        readonly Dictionary<string, WorldSite> _sitesById =
            new Dictionary<string, WorldSite>(StringComparer.Ordinal);
        public IReadOnlyDictionary<string, WorldSite> Sites => _sitesById;

        public void Clear() => _sitesById.Clear();

        public void Register(WorldSite site)
        {
            if (site == null || string.IsNullOrEmpty(site.SiteId))
                throw new ArgumentException("WorldSite requires SiteId.");

            if (_sitesById.ContainsKey(site.SiteId))
                throw new InvalidOperationException("WorldSite identity already registered: " + site.SiteId);
            _sitesById[site.SiteId] = site;
        }

        public bool TryGet(string siteId, out WorldSite site) =>
            _sitesById.TryGetValue(siteId, out site) && site != null;

        public bool RemoveRuntimeSite(string siteId)
        {
            if (!_sitesById.TryGetValue(siteId ?? string.Empty, out var site) || site == null ||
                !site.IsRuntimeCreated)
                return false;
            return _sitesById.Remove(siteId);
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
    }
}
