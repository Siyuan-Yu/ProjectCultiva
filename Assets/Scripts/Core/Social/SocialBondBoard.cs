using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Core.Social
{
    public sealed class SocialBondBoard
    {
        readonly List<SocialBond> _bonds = new List<SocialBond>();

        public IReadOnlyList<SocialBond> All => _bonds;
        public int Count => _bonds.Count;

        public bool Contains(SocialBondKind kind, EntityId from, EntityId to)
        {
            var candidate = new SocialBond(kind, from, to);
            for (var i = 0; i < _bonds.Count; i++)
            {
                var bond = _bonds[i];
                if (bond.Kind == candidate.Kind && bond.From == candidate.From && bond.To == candidate.To)
                    return true;
            }
            return false;
        }

        internal bool TryAdd(SocialBond bond)
        {
            if (bond == null || Contains(bond.Kind, bond.From, bond.To))
                return false;
            _bonds.Add(bond);
            return true;
        }

        internal bool TryRemove(SocialBondKind kind, EntityId from, EntityId to)
        {
            var candidate = new SocialBond(kind, from, to);
            for (var i = 0; i < _bonds.Count; i++)
            {
                var bond = _bonds[i];
                if (bond.Kind != candidate.Kind || bond.From != candidate.From || bond.To != candidate.To)
                    continue;
                _bonds.RemoveAt(i);
                return true;
            }
            return false;
        }

        public void Clear() => _bonds.Clear();
    }
}
