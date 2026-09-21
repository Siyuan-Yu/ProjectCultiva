using System.Collections.Generic;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Geometry helper retained only for legacy Hex content adapters.</summary>
    public static class LegacyHexRingUtility
    {
        public static List<HexCoord> ExpandOneRing(IEnumerable<HexCoord> bases)
        {
            var set = new HashSet<HexCoord>();
            if (bases != null)
                foreach (var hex in bases)
                {
                    set.Add(hex);
                    for (var direction = 0; direction < 6; direction++)
                        set.Add(HexMath.Neighbor(hex, direction));
                }
            return new List<HexCoord>(set);
        }
    }
}
