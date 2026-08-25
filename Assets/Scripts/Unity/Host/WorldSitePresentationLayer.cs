using UnityEngine;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>WorldSite Footprint 地图标记：每格相同小房子；名称仅 AnchorHex 显示一次。</summary>
    public static class WorldSitePresentationLayer
    {
        public static void Draw(
            HexMapViewportProjection projection,
            SimulationWorld world,
            Texture2D pixel,
            float hexScreenRadius)
        {
            if (Event.current != null && Event.current.type != EventType.Repaint)
                return;
            if (world?.Strategic?.Sites == null || pixel == null)
                return;
            if (hexScreenRadius < 1.2f)
                return;

            var houseSize = Mathf.Clamp(hexScreenRadius * 1.55f, 10f, 20f);
            const float minZoomForLabel = 4.5f;
            const int labelFontSize = 11;
            const float labelGapPx = 2f;

            foreach (var kv in world.Strategic.Sites.Sites)
            {
                var site = kv.Value;
                if (site == null)
                    continue;

                foreach (var hex in site.EnumerateFootprintHexes())
                {
                    var center = projection.ProjectHexCenter(hex);
                    DrawFootprintHouse(center, houseSize, pixel);
                }

                if (hexScreenRadius < minZoomForLabel)
                    continue;

                var anchorCenter = projection.ProjectHexCenter(site.AnchorHex);
                var label = string.IsNullOrEmpty(site.DisplayName) ? site.SiteId : site.DisplayName;
                var labelStyle = HostImguiStyles.InkLabel(labelFontSize, bold: true, ink: new Color(0.22f, 0.18f, 0.12f));
                labelStyle.alignment = TextAnchor.MiddleCenter;
                var content = new GUIContent(label);
                var textSize = labelStyle.CalcSize(content);
                var labelWidth = Mathf.Clamp(textSize.x + 8f, 48f, 220f);
                var labelHeight = Mathf.Max(textSize.y + 2f, labelFontSize + 4f);
                var houseTopY = anchorCenter.y - houseSize * 0.55f;
                var labelRect = new Rect(
                    anchorCenter.x - labelWidth * 0.5f,
                    houseTopY - labelGapPx - labelHeight,
                    labelWidth,
                    labelHeight);
                GUI.Label(labelRect, content, labelStyle);
            }
        }

        static void DrawFootprintHouse(Vector2 center, float size, Texture2D pixel)
        {
            var tex = pixel != null ? pixel : Texture2D.whiteTexture;
            var prev = GUI.color;
            var half = size * 0.5f;
            var bodyHeight = size * 0.72f;
            var baseRect = new Rect(center.x - half, center.y - bodyHeight * 0.52f, size, bodyHeight);
            var wall = new Color(0.84f, 0.72f, 0.50f, 0.98f);
            var roof = new Color(0.56f, 0.32f, 0.20f, 0.98f);
            var roofH = bodyHeight * 0.42f;
            var roofRect = new Rect(baseRect.x + baseRect.width * 0.08f, baseRect.y - roofH * 0.55f, baseRect.width * 0.84f, roofH);
            var wallRect = new Rect(baseRect.x + baseRect.width * 0.18f, baseRect.y, baseRect.width * 0.64f, baseRect.height * 0.82f);
            FillRect(roofRect, roof, tex);
            FillRect(wallRect, wall, tex);
            FillRect(
                new Rect(wallRect.x + wallRect.width * 0.38f, wallRect.yMax - wallRect.height * 0.35f, wallRect.width * 0.24f, wallRect.height * 0.35f),
                new Color(0.34f, 0.24f, 0.16f, 1f),
                tex);
            GUI.color = prev;
        }

        static void FillRect(Rect rect, Color color, Texture2D tex)
        {
            var prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, tex);
            GUI.color = prev;
        }
    }
}
