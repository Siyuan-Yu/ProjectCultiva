using System.Collections.Generic;
using XianXia.Data.Content;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Align worldRegion location presentation with mapLayout placement centers
    /// (via placement.boundLocationId) so MapEditor layout matches Host.
    /// Multiple placements sharing one location average to the cluster center.
    /// </summary>
    public static class MapLayoutPresentationSync
    {
        public static int Apply(PlayableHostSession session)
        {
            if (session?.World?.WorldRegion?.Locations == null)
                return 0;
            if (!MapLayoutPick.TryGet(session, out var layout) || layout?.Placements == null)
                return 0;

            var cs = layout.CellSize > 0f ? layout.CellSize : 1f;
            var sums = new Dictionary<string, Acc>(System.StringComparer.Ordinal);
            foreach (var p in layout.Placements)
            {
                if (p == null || string.IsNullOrWhiteSpace(p.BoundLocationId))
                    continue;
                if (!session.World.WorldRegion.Locations.ContainsKey(p.BoundLocationId))
                    continue;

                var pw = p.W < 1 ? 1 : p.W;
                var ph = p.H < 1 ? 1 : p.H;
                var cx = layout.OriginX + (p.X + pw * 0.5f) * cs;
                var cz = layout.OriginY + (p.Y + ph * 0.5f) * cs;
                if (sums.TryGetValue(p.BoundLocationId, out var acc))
                {
                    acc.Sx += cx;
                    acc.Sz += cz;
                    acc.N++;
                    sums[p.BoundLocationId] = acc;
                }
                else
                {
                    sums[p.BoundLocationId] = new Acc { Sx = cx, Sz = cz, N = 1 };
                }
            }

            var applied = 0;
            foreach (var kv in sums)
            {
                if (!session.World.WorldRegion.Locations.TryGetValue(kv.Key, out var loc) ||
                    loc == null ||
                    kv.Value.N <= 0)
                    continue;
                loc.PresentationX = kv.Value.Sx / kv.Value.N;
                loc.PresentationZ = kv.Value.Sz / kv.Value.N;
                applied++;
            }

            return applied;
        }

        public static bool TryGetLayout(PlayableHostSession session, out MapLayoutDefinition layout) =>
            MapLayoutPick.TryGet(session, out layout);

        struct Acc
        {
            public float Sx;
            public float Sz;
            public int N;
        }
    }
}
