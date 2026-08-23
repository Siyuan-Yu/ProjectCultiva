using UnityEngine;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>WorldSite 地图图标层（Presentation only）；位置真源 = <see cref="WorldSite.AnchorHex"/>。</summary>
    public static class WorldSitePresentationLayer
    {
        public enum SitePresentationCategory
        {
            Village,
            Town,
            SectGate,
            Mine,
            Forest,
            Pass,
            RoadJunction,
            Ruins,
            Generic,
        }

        public static SitePresentationCategory ResolveCategory(WorldSite site)
        {
            if (site == null)
                return SitePresentationCategory.Generic;

            var kind = (site.SiteType ?? string.Empty).Trim();
            if (kind.Length == 0)
                return SitePresentationCategory.Generic;

            switch (kind)
            {
                case "Village":
                case "Settlement":
                    return SitePresentationCategory.Village;
                case "Town":
                case "City":
                    return SitePresentationCategory.Town;
                case "Sect":
                case "Fortress":
                    return SitePresentationCategory.SectGate;
                case "Mine":
                    return SitePresentationCategory.Mine;
                case "Forest":
                    return SitePresentationCategory.Forest;
                case "Pass":
                case "Checkpoint":
                    return SitePresentationCategory.Pass;
                case "Road":
                    return SitePresentationCategory.RoadJunction;
                case "Ruin":
                    return SitePresentationCategory.Ruins;
                default:
                    return SitePresentationCategory.Generic;
            }
        }

        public static string ResolveCategoryLabel(SitePresentationCategory category) =>
            category switch
            {
                SitePresentationCategory.Village => "村落",
                SitePresentationCategory.Town => "城镇",
                SitePresentationCategory.SectGate => "山门",
                SitePresentationCategory.Mine => "矿场",
                SitePresentationCategory.Forest => "林间",
                SitePresentationCategory.Pass => "关隘",
                SitePresentationCategory.RoadJunction => "路口",
                SitePresentationCategory.Ruins => "遗迹",
                _ => "地点",
            };

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
            if (hexScreenRadius < 1.5f)
                return;

            var iconSize = Mathf.Clamp(hexScreenRadius * 2.4f, 12f, 32f);
            const float minZoomForLabel = 4.5f;
            const float referenceIconSizePx = 18f;
            const int labelFontSize = 11;
            const float labelGapPx = 2f;

            foreach (var kv in world.Strategic.Sites.Sites)
            {
                var site = kv.Value;
                if (site == null)
                    continue;

                var center = projection.ProjectHexCenter(site.AnchorHex);
                var category = ResolveCategory(site);
                DrawIcon(center, iconSize, category, pixel);

                if (hexScreenRadius < minZoomForLabel)
                    continue;

                var label = string.IsNullOrEmpty(site.DisplayName) ? site.SiteId : site.DisplayName;
                var labelStyle = HostImguiStyles.InkLabel(labelFontSize, bold: true, ink: new Color(0.22f, 0.18f, 0.12f));
                labelStyle.alignment = TextAnchor.MiddleCenter;
                var content = new GUIContent(label);
                var textSize = labelStyle.CalcSize(content);
                var labelWidth = Mathf.Clamp(textSize.x + 8f, 48f, 220f);
                var labelHeight = Mathf.Max(textSize.y + 2f, labelFontSize + 4f);
                var iconTopY = MeasureIconTopY(center, referenceIconSizePx, category);
                var labelRect = new Rect(
                    center.x - labelWidth * 0.5f,
                    iconTopY - labelGapPx - labelHeight,
                    labelWidth,
                    labelHeight);
                GUI.Label(labelRect, content, labelStyle);
            }
        }

        /// <summary>固定屏幕像素下的图标顶边 Y，供地名锚点（不随地图缩放）。</summary>
        static float MeasureIconTopY(Vector2 center, float size, SitePresentationCategory category)
        {
            var half = size * 0.5f;
            var bodyHeight = size * 0.72f;
            var baseRect = new Rect(center.x - half, center.y - bodyHeight * 0.52f, size, bodyHeight);

            switch (category)
            {
                case SitePresentationCategory.Town:
                {
                    var townTop = baseRect.y - bodyHeight * 0.24f;
                    var roofH = size * 0.55f * 0.42f;
                    var smallRoofTop = baseRect.y - size * 0.08f - roofH * 0.55f;
                    return Mathf.Min(townTop, smallRoofTop);
                }
                case SitePresentationCategory.SectGate:
                case SitePresentationCategory.Pass:
                    return baseRect.y - bodyHeight * 0.12f - bodyHeight * 0.18f;
                case SitePresentationCategory.Mine:
                    return baseRect.y;
                case SitePresentationCategory.Forest:
                    return baseRect.y;
                case SitePresentationCategory.RoadJunction:
                    return baseRect.y + bodyHeight * 0.08f;
                case SitePresentationCategory.Ruins:
                    return baseRect.y + bodyHeight * 0.22f;
                case SitePresentationCategory.Village:
                case SitePresentationCategory.Generic:
                default:
                {
                    var roofH = bodyHeight * 0.42f;
                    return baseRect.y - roofH * 0.55f;
                }
            }
        }

        static void DrawIcon(Vector2 center, float size, SitePresentationCategory category, Texture2D pixel)
        {
            var tex = pixel != null ? pixel : Texture2D.whiteTexture;
            var prev = GUI.color;
            var half = size * 0.5f;
            var bodyHeight = size * 0.72f;
            var baseRect = new Rect(center.x - half, center.y - bodyHeight * 0.52f, size, bodyHeight);

            switch (category)
            {
                case SitePresentationCategory.Town:
                    DrawHouse(baseRect, tex, new Color(0.82f, 0.68f, 0.46f), new Color(0.58f, 0.34f, 0.22f), wide: true);
                    DrawHouse(
                        new Rect(baseRect.x + size * 0.28f, baseRect.y - size * 0.08f, size * 0.55f, size * 0.55f),
                        tex,
                        new Color(0.78f, 0.62f, 0.42f),
                        new Color(0.52f, 0.30f, 0.18f),
                        wide: false);
                    break;
                case SitePresentationCategory.SectGate:
                    DrawGate(baseRect, tex);
                    break;
                case SitePresentationCategory.Mine:
                    DrawMine(baseRect, tex);
                    break;
                case SitePresentationCategory.Forest:
                    DrawTree(baseRect, tex);
                    break;
                case SitePresentationCategory.Pass:
                    DrawPass(baseRect, tex);
                    break;
                case SitePresentationCategory.RoadJunction:
                    DrawRoadSign(baseRect, tex);
                    break;
                case SitePresentationCategory.Ruins:
                    DrawRuins(baseRect, tex);
                    break;
                case SitePresentationCategory.Village:
                case SitePresentationCategory.Generic:
                default:
                    DrawHouse(baseRect, tex, new Color(0.84f, 0.72f, 0.50f), new Color(0.56f, 0.32f, 0.20f), wide: false);
                    break;
            }

            GUI.color = prev;
        }

        static float DrawHouse(Rect body, Texture2D tex, Color wall, Color roof, bool wide)
        {
            var roofH = body.height * 0.42f;
            var roofRect = new Rect(body.x + body.width * 0.08f, body.y - roofH * 0.55f, body.width * 0.84f, roofH);
            var wallRect = new Rect(body.x + body.width * 0.18f, body.y, body.width * 0.64f, body.height * 0.82f);
            if (wide)
                wallRect = new Rect(body.x + body.width * 0.08f, body.y, body.width * 0.84f, body.height * 0.82f);

            FillRect(roofRect, roof, tex);
            FillRect(wallRect, wall, tex);
            FillRect(new Rect(wallRect.x + wallRect.width * 0.38f, wallRect.yMax - wallRect.height * 0.35f, wallRect.width * 0.24f, wallRect.height * 0.35f),
                new Color(0.34f, 0.24f, 0.16f, 1f), tex);
            return roofRect.y;
        }

        static float DrawGate(Rect body, Texture2D tex)
        {
            var pillarW = body.width * 0.16f;
            var pillarH = body.height * 0.95f;
            var left = new Rect(body.x + body.width * 0.14f, body.y, pillarW, pillarH);
            var right = new Rect(body.xMax - body.width * 0.14f - pillarW, body.y, pillarW, pillarH);
            var beam = new Rect(body.x + body.width * 0.08f, body.y - body.height * 0.12f, body.width * 0.84f, body.height * 0.18f);
            var ink = new Color(0.48f, 0.34f, 0.22f, 1f);
            FillRect(left, ink, tex);
            FillRect(right, ink, tex);
            FillRect(beam, new Color(0.62f, 0.42f, 0.24f, 1f), tex);
            return beam.y;
        }

        static float DrawMine(Rect body, Texture2D tex)
        {
            var ore = new Color(0.58f, 0.56f, 0.52f, 1f);
            var pick = new Color(0.42f, 0.30f, 0.20f, 1f);
            var pickTop = body.y;
            FillRect(new Rect(body.x + body.width * 0.18f, body.y + body.height * 0.35f, body.width * 0.64f, body.height * 0.45f), ore, tex);
            FillRect(new Rect(body.x + body.width * 0.46f, body.y, body.width * 0.08f, body.height * 0.55f), pick, tex);
            FillRect(new Rect(body.x + body.width * 0.30f, body.y + body.height * 0.18f, body.width * 0.40f, body.height * 0.08f), pick, tex);
            return pickTop;
        }

        static float DrawTree(Rect body, Texture2D tex)
        {
            var trunk = new Color(0.46f, 0.30f, 0.18f, 1f);
            var leaf = new Color(0.34f, 0.58f, 0.30f, 1f);
            var leafRect = new Rect(body.x + body.width * 0.22f, body.y, body.width * 0.56f, body.height * 0.48f);
            FillRect(new Rect(body.x + body.width * 0.44f, body.y + body.height * 0.35f, body.width * 0.12f, body.height * 0.45f), trunk, tex);
            FillRect(leafRect, leaf, tex);
            return leafRect.y;
        }

        static float DrawPass(Rect body, Texture2D tex)
        {
            var top = DrawGate(body, tex);
            FillRect(new Rect(body.x + body.width * 0.36f, body.y + body.height * 0.25f, body.width * 0.28f, body.height * 0.45f),
                new Color(0.22f, 0.18f, 0.14f, 0.85f), tex);
            return top;
        }

        static float DrawRoadSign(Rect body, Texture2D tex)
        {
            var pole = new Color(0.50f, 0.36f, 0.22f, 1f);
            var sign = new Color(0.88f, 0.78f, 0.52f, 1f);
            var signRect = new Rect(body.x + body.width * 0.18f, body.y + body.height * 0.08f, body.width * 0.64f, body.height * 0.22f);
            FillRect(new Rect(body.x + body.width * 0.46f, body.y, body.width * 0.08f, body.height * 0.9f), pole, tex);
            FillRect(signRect, sign, tex);
            return signRect.y;
        }

        static float DrawRuins(Rect body, Texture2D tex)
        {
            var stone = new Color(0.62f, 0.58f, 0.52f, 1f);
            var top = body.y + body.height * 0.22f;
            FillRect(new Rect(body.x + body.width * 0.10f, body.y + body.height * 0.35f, body.width * 0.28f, body.height * 0.45f), stone, tex);
            top = Mathf.Min(top, body.y + body.height * 0.22f);
            FillRect(new Rect(body.x + body.width * 0.42f, body.y + body.height * 0.22f, body.width * 0.22f, body.height * 0.58f), stone, tex);
            FillRect(new Rect(body.x + body.width * 0.62f, body.y + body.height * 0.42f, body.width * 0.24f, body.height * 0.28f), stone, tex);
            return top;
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
