using UnityEngine;
using XianXia.Core.World.Surface;
using XianXia.Data.Content;

namespace XianXia.Unity.Host
{
    /// <summary>Same ascending-Y raster as navigation; GUI projection alone converts screen Y.</summary>
    public sealed class HostSurfaceWorldMapRenderer
    {
        Texture2D _texture;
        string _identity = string.Empty;
        Rect _worldBounds;
        int _sampleStep;
        public void Clear()
        {
            if (_texture != null)
            {
                if (Application.isPlaying) Object.Destroy(_texture);
                else Object.DestroyImmediate(_texture);
            }
            _texture = null; _identity = string.Empty; _worldBounds = default; _sampleStep = 0;
        }
        public void Draw(Rect mapRect, SurfaceWorldMapViewportProjection projection,
            ContinuousSurfaceWorldMapDefinition cache, SurfaceGroundNavigation fallback)
        {
            if (Event.current.type != EventType.Repaint) return;
            Ensure(cache, fallback, mapRect);
            if (_texture != null) GUI.DrawTexture(projection.ProjectWorldRect(_worldBounds),
                _texture, ScaleMode.StretchToFill, false);
        }
        void Ensure(ContinuousSurfaceWorldMapDefinition cache, SurfaceGroundNavigation nav, Rect mapRect)
        {
            if (nav == null) return;
            var identity = cache != null ? cache.SurfaceId + "|" + cache.SourceHash : nav.SurfaceId + "|" + nav.SourceHash;
            var width = cache?.WidthCells ?? nav.Width;
            var height = cache?.HeightCells ?? nav.Height;
            var panelLimit = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(mapRect.width, mapRect.height) * 2f), 512, 4096);
            var textureLimit = SystemInfo.maxTextureSize > 0 ? Mathf.Min(panelLimit, SystemInfo.maxTextureSize) : panelLimit;
            var sampleStep = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(width, height) / (float)textureLimit));
            if (_texture != null && identity == _identity && sampleStep == _sampleStep) return;
            _identity = identity; _sampleStep = sampleStep;
            var textureWidth = (width + sampleStep - 1) / sampleStep;
            var textureHeight = (height + sampleStep - 1) / sampleStep;
            var colors = new Color32[textureWidth * textureHeight];
            var authoredColors = cache != null && cache.PresentationRows.Count == height;
            for (var pixelY = 0; pixelY < textureHeight; pixelY++)
            for (var pixelX = 0; pixelX < textureWidth; pixelX++)
            {
                // Uniform samples preserve the exact raster bounds even when dimensions are not divisible by step.
                var x = Mathf.Min(width - 1, Mathf.FloorToInt((pixelX + .5f) * width / textureWidth));
                var y = Mathf.Min(height - 1, Mathf.FloorToInt((pixelY + .5f) * height / textureHeight));
                var terrain = cache != null ? cache.BaseTerrainRows[y][x] : 'P';
                var color = new Color32(193, 207, 160, 255);
                if (authoredColors)
                {
                    var rgb = cache.PresentationPalette[cache.PresentationRows[y][x] - 'A'];
                    color = new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
                }
                else if (cache != null && cache.ForestRows[y][x] != '0')
                    color = new Color32(160, 185, 149, 255);
                if (terrain == 'W') color = new Color32(76, 145, 183, 255);
                else if (terrain == 'M') color = new Color32(139, 139, 121, 255);
                if (cache == null && nav.TryGetCell(nav.OriginX + (x + .5f) * nav.CellSize,
                    nav.OriginY + (y + .5f) * nav.CellSize, out var kind))
                    color = (kind & SurfaceGroundCellKind.Water) != 0 ? new Color32(76, 145, 183, 255) :
                        (kind & SurfaceGroundCellKind.Solid) != 0 ? new Color32(139, 139, 121, 255) : color;
                // Texture2D rows already grow upwards, just like Surface. Do not invert this row.
                colors[pixelX + pixelY * textureWidth] = color;
            }
            if (_texture != null) Object.Destroy(_texture);
            _texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            _texture.SetPixels32(colors); _texture.Apply(false, true);
            _worldBounds = new Rect(nav.OriginX, nav.OriginY, nav.Width * nav.CellSize, nav.Height * nav.CellSize);
        }
    }
}
