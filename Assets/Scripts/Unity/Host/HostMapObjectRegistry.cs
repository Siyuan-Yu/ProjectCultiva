using System.Collections.Generic;
using UnityEngine;

namespace XianXia.Unity.Host
{
    /// <summary>地图刷出时可检视／可破坏物体的运行时登记（随 TileMap Rebuild 清空）。</summary>
    public static class HostMapObjectRegistry
    {
        static readonly List<HostMapPlotCell> Plots = new List<HostMapPlotCell>(256);
        static readonly List<HostMapDestructible> Destructibles = new List<HostMapDestructible>(128);

        public static IReadOnlyList<HostMapPlotCell> AllPlots => Plots;
        public static IReadOnlyList<HostMapDestructible> AllDestructibles => Destructibles;

        public static void BeginRebuild()
        {
            Plots.Clear();
            Destructibles.Clear();
        }

        public static void Register(HostMapPlotCell plot)
        {
            if (plot != null && !Plots.Contains(plot))
                Plots.Add(plot);
        }

        public static void Register(HostMapDestructible d)
        {
            if (d != null && !Destructibles.Contains(d))
                Destructibles.Add(d);
        }

        public static void Unregister(HostMapDestructible d)
        {
            if (d != null)
                Destructibles.Remove(d);
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
            target = null;
            var best = radius * radius;
            for (var i = Destructibles.Count - 1; i >= 0; i--)
            {
                var d = Destructibles[i];
                if (d == null || d.IsDestroyed)
                {
                    Destructibles.RemoveAt(i);
                    continue;
                }

                var d2 = (d.transform.position - worldPoint).sqrMagnitude;
                if (d2 > best)
                    continue;
                best = d2;
                target = d;
            }

            return target != null;
        }
    }
}
