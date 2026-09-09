using System.Collections.Generic;
using UnityEngine;

namespace XianXia.Unity.Host
{
    /// <summary>地图刷出时可检视／可破坏物体的运行时登记（随 TileMap Rebuild 清空）。</summary>
    public static class HostMapObjectRegistry
    {
        static readonly List<HostMapPlotCell> Plots = new List<HostMapPlotCell>(256);
        static readonly List<HostMapDestructible> Destructibles = new List<HostMapDestructible>(128);
        static readonly Dictionary<Object, string> OwnerByObject = new Dictionary<Object, string>();
        static string _currentOwner = string.Empty;

        public static IReadOnlyList<HostMapPlotCell> AllPlots => Plots;
        public static IReadOnlyList<HostMapDestructible> AllDestructibles => Destructibles;

        /// <summary>Legacy single-map wrapper. New incremental users must use owner APIs.</summary>
        public static void BeginRebuild()
        {
            ClearAll();
        }

        public static void ClearAll()
        {
            Plots.Clear();
            Destructibles.Clear();
            OwnerByObject.Clear();
            _currentOwner = string.Empty;
        }

        public static void BeginOwnerBuild(string ownerKey)
        {
            _currentOwner = ownerKey ?? string.Empty;
            RemoveOwner(_currentOwner);
        }

        public static void RemoveOwner(string ownerKey)
        {
            ownerKey = ownerKey ?? string.Empty;
            for (var i = Plots.Count - 1; i >= 0; i--)
                if (Plots[i] == null || IsOwnedBy(Plots[i], ownerKey))
                {
                    OwnerByObject.Remove(Plots[i]);
                    Plots.RemoveAt(i);
                }
            for (var i = Destructibles.Count - 1; i >= 0; i--)
                if (Destructibles[i] == null || IsOwnedBy(Destructibles[i], ownerKey))
                {
                    OwnerByObject.Remove(Destructibles[i]);
                    Destructibles.RemoveAt(i);
                }
        }

        public static void Register(HostMapPlotCell plot)
            => Register(_currentOwner, plot);

        public static void Register(string ownerKey, HostMapPlotCell plot)
        {
            if (plot != null && !Plots.Contains(plot))
            {
                Plots.Add(plot);
                OwnerByObject[plot] = ownerKey ?? string.Empty;
            }
        }

        public static void Register(HostMapDestructible d)
            => Register(_currentOwner, d);

        public static void Register(string ownerKey, HostMapDestructible d)
        {
            if (d != null && !Destructibles.Contains(d))
            {
                Destructibles.Add(d);
                OwnerByObject[d] = ownerKey ?? string.Empty;
            }
        }

        public static void Unregister(HostMapDestructible d)
        {
            if (d != null)
            {
                Destructibles.Remove(d);
                OwnerByObject.Remove(d);
            }
        }

        public static bool TryPickPlot(Vector3 worldPoint, float radius, out HostMapPlotCell plot)
        {
            plot = null;
            var best = radius * radius;
            for (var i = Plots.Count - 1; i >= 0; i--)
            {
                var p = Plots[i];
                if (p == null)
                {
                    Plots.RemoveAt(i);
                    OwnerByObject.Remove(p);
                    continue;
                }

                var d2 = (p.transform.position - worldPoint).sqrMagnitude;
                if (d2 > best)
                    continue;
                best = d2;
                plot = p;
            }

            return plot != null;
        }

        public static bool TryPickDestructible(Vector3 worldPoint, float radius, out HostMapDestructible target)
        {
            return TryFindNearestDestructible(worldPoint, radius, out target);
        }

        /// <summary>Nearest destructible within radius; optional tree-only filter.</summary>
        public static bool TryFindNearestDestructible(
            Vector3 worldPoint,
            float maxRadius,
            out HostMapDestructible target,
            bool treesOnly = false,
            HostMapDestructible exclude = null)
        {
            target = null;
            var best = maxRadius * maxRadius;
            var excludeId = exclude != null ? exclude.GetInstanceID() : 0;
            for (var i = Destructibles.Count - 1; i >= 0; i--)
            {
                var d = Destructibles[i];
                if (d == null || d.IsDestroyed)
                {
                    Destructibles.RemoveAt(i);
                    OwnerByObject.Remove(d);
                    continue;
                }

                if (treesOnly && !d.IsTree)
                    continue;
                if (excludeId != 0 && d.GetInstanceID() == excludeId)
                    continue;

                var d2 = (d.transform.position - worldPoint).sqrMagnitude;
                if (d2 > best)
                    continue;
                best = d2;
                target = d;
            }

            return target != null;
        }

        static bool IsOwnedBy(Object item, string ownerKey) =>
            item != null && OwnerByObject.TryGetValue(item, out var owner) &&
            string.Equals(owner, ownerKey, System.StringComparison.Ordinal);
    }
}
