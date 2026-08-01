using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;

namespace XianXia.Core.Opportunity
{
    /// <summary>Per-entity discovered OpportunitySites (VS0.3).</summary>
    public sealed class KnownSitesComponent : IComponent
    {
        readonly HashSet<string> _known = new HashSet<string>(System.StringComparer.Ordinal);

        public IReadOnlyCollection<string> KnownIds => _known;

        public bool Knows(DefinitionId siteId) =>
            !string.IsNullOrEmpty(siteId.Namespace) && _known.Contains(siteId.ToString());

        public bool Knows(string siteId) => !string.IsNullOrEmpty(siteId) && _known.Contains(siteId);

        public bool Discover(DefinitionId siteId)
        {
            if (string.IsNullOrEmpty(siteId.Namespace) || string.IsNullOrEmpty(siteId.LocalId))
                return false;
            return _known.Add(siteId.ToString());
        }

        public void Restore(IEnumerable<string> siteIds)
        {
            _known.Clear();
            if (siteIds == null)
                return;
            foreach (var id in siteIds)
            {
                if (!string.IsNullOrEmpty(id))
                    _known.Add(id);
            }
        }
    }
}
