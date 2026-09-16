using System;
using UnityEngine;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>Read-only presentation of the current Site claim authority on the Surface map.</summary>
    public static class HostSurfaceActualControlRenderer
    {
        public static void Draw(SimulationWorld world, string surfaceId,
            SurfaceWorldMapViewportProjection projection, Texture2D pixel)
        {
            if (Event.current.type != EventType.Repaint || pixel == null) return;
            var overlays = WorldSiteActualControlOverlayBuilder.BuildFactionUnion(world);
            foreach (var overlay in overlays)
            {
                if (!string.Equals(overlay.SurfaceId, surfaceId, StringComparison.Ordinal)) continue;
                StrategicFactionCatalog.MapTint(overlay.FactionId, out var r, out var g, out var b);
                foreach (var piece in overlay.Pieces)
                {
                    var a = projection.ProjectWorld(piece.MinX, piece.MinY);
                    var c = projection.ProjectWorld(piece.MaxX, piece.MaxY);
                    Fill(Rect.MinMaxRect(Mathf.Min(a.x, c.x), Mathf.Min(a.y, c.y),
                        Mathf.Max(a.x, c.x), Mathf.Max(a.y, c.y)), new Color(r, g, b, .16f), pixel);
                }
                foreach (var edge in overlay.BoundarySegments)
                {
                    var a = projection.ProjectWorld(edge.X0, edge.Y0);
                    var b0 = projection.ProjectWorld(edge.X1, edge.Y1);
                    var rect = Mathf.Abs(a.x - b0.x) < 1f
                        ? new Rect(a.x - 1f, Mathf.Min(a.y, b0.y), 2f, Mathf.Abs(a.y - b0.y))
                        : new Rect(Mathf.Min(a.x, b0.x), a.y - 1f, Mathf.Abs(a.x - b0.x), 2f);
                    Fill(rect, new Color(r, g, b, .95f), pixel);
                }
            }
        }

        static void Fill(Rect rect, Color color, Texture2D pixel)
        {
            var old = GUI.color; GUI.color = color; GUI.DrawTexture(rect, pixel); GUI.color = old;
        }
    }
}
