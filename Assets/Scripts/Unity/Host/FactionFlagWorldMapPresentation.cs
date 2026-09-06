using UnityEngine;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    public static class FactionFlagWorldMapPresentation
    {
        public static void Draw(HexMapViewportProjection projection, SimulationWorld world, Texture2D pixel, float hexScreenRadius)
        {
            if (Event.current != null && Event.current.type != EventType.Repaint)
                return;
            if (world?.Strategic?.FactionFlags == null || pixel == null || hexScreenRadius < 1.2f)
                return;
            var size = Mathf.Clamp(hexScreenRadius * 1.1f, 7f, 15f);
            foreach (var pair in world.Strategic.FactionFlags.Flags)
            {
                var flag = pair.Value;
                if (flag == null) continue;
                var center = projection.ProjectHexCenter(flag.AnchorHex);
                StrategicFactionCatalog.MapTint(flag.FactionId, out var r, out var g, out var b);
                Fill(new Rect(center.x - 1f, center.y - size * .6f, 2f, size * 1.4f), new Color(.25f,.2f,.12f,1f), pixel);
                Fill(new Rect(center.x, center.y - size * .6f, size * .85f, size * .5f), new Color(r,g,b,1f), pixel);
            }
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
