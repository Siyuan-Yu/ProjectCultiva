using UnityEngine;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    public static class FactionFlagWorldMapPresentation
    {
        public static void Draw(
            HexMapViewportProjection projection,
            SimulationWorld world,
            Texture2D pixel,
            float hexScreenRadius,
            bool includeDebugOnly = false)
        {
            if (Event.current != null && Event.current.type != EventType.Repaint)
                return;
            if (world?.Strategic?.FactionFlags == null || pixel == null || hexScreenRadius < 1.2f)
                return;
            var size = Mathf.Clamp(hexScreenRadius * 1.1f, 7f, 15f);
            foreach (var pair in world.Strategic.FactionFlags.Flags)
            {
                var flag = pair.Value;
                // Normal product rendering owns Site-Core flags through WorldSitePresentationLayer.
                // The legacy layer is opt-in diagnostics only and never presents an isolated flag.
                if (flag == null || flag.IsSiteCore || !includeDebugOnly || !flag.IsWorldMapDebugOnly)
                    continue;
                var center = projection.ProjectHexCenter(flag.AnchorHex);
                DrawFlagMarker(center, size, flag.FactionId, pixel);
            }
        }

        public static void DrawFlagMarker(
            Vector2 center, float size, string factionId, Texture2D pixel)
        {
            StrategicFactionCatalog.MapTint(factionId, out var r, out var g, out var b);
            // The caller projects a stable world-space marker size around the exact anchor.
            var half = size * .5f;
            var poleX = center.x - half * .8f;
            var poleWidth = size * .1f;
            Fill(new Rect(poleX, center.y - half, poleWidth, size),
                new Color(.25f, .2f, .12f, 1f), pixel);
            Fill(new Rect(poleX + poleWidth, center.y - half, size * .8f - poleWidth, size * .44f),
                new Color(r, g, b, 1f), pixel);
        }

        static void Fill(Rect rect, Color color, Texture2D texture)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, texture);
            GUI.color = previous;
        }
    }
}
