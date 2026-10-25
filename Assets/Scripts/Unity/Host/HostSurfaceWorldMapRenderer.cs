using UnityEngine;
using XianXia.Core.World.Surface;
using XianXia.Data.Content;

namespace XianXia.Unity.Host
{
    /// <summary>Cached Surface WorldMap terrain. One texture rebuild per content identity, never per IMGUI repaint.</summary>
    public sealed class HostSurfaceWorldMapRenderer
    {
        Texture2D _texture; string _identity = string.Empty; Rect _worldBounds; int _sampleStep;
        public void Clear()
        {
            if (_texture != null)
            {
                if (Application.isPlaying) Object.Destroy(_texture);
                else Object.DestroyImmediate(_texture);
            }
            _texture = null;
            _identity = string.Empty;
            _worldBounds = default;
            _sampleStep = 0;
        }
        public void Draw(Rect mapRect, SurfaceWorldMapViewportProjection projection, ContinuousSurfaceWorldMapDefinition cache, SurfaceGroundNavigation fallback)
        {
            if (Event.current.type != EventType.Repaint) return;
            Ensure(cache, fallback, mapRect); if (_texture == null) return;
            var screen = projection.ProjectWorldRect(_worldBounds); GUI.DrawTexture(screen, _texture, ScaleMode.StretchToFill, false);
        }
        void Ensure(ContinuousSurfaceWorldMapDefinition cache, SurfaceGroundNavigation nav, Rect mapRect)
        {
            var identity = cache != null ? cache.SurfaceId + "|" + cache.SourceHash : nav?.SurfaceId + "|" + nav?.SourceHash;
            if (nav == null) return;
            var width = cache?.WidthCells ?? nav.Width; var height = cache?.HeightCells ?? nav.Height;
            // A global 4096-wide texture exceeds most Game panels. Sample only enough pixels
            // for the actual map panel, while preserving the full world-space bounds below.
            var panelLimit = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(mapRect.width, mapRect.height)), 512, 4096);
            var textureLimit = SystemInfo.maxTextureSize > 0
                ? Mathf.Min(panelLimit, SystemInfo.maxTextureSize) : panelLimit;
            var sampleStep = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(width, height) / (float)textureLimit));
            if (_texture != null && identity == _identity && sampleStep == _sampleStep) return;
            _identity = identity;
            _sampleStep = sampleStep;
            var textureWidth = (width + sampleStep - 1) / sampleStep;
            var textureHeight = (height + sampleStep - 1) / sampleStep;
            var colors = new Color32[textureWidth * textureHeight];
            for (var pixelY=0;pixelY<textureHeight;pixelY++) for(var pixelX=0;pixelX<textureWidth;pixelX++)
            {
                var x = Mathf.Min(width - 1, pixelX * sampleStep + sampleStep / 2);
                var y = Mathf.Min(height - 1, pixelY * sampleStep + sampleStep / 2);
                var terrain = cache != null ? cache.BaseTerrainRows[y][x] : 'P'; var forest = cache != null && cache.ForestRows[y][x] != '0';
                Color32 color = terrain=='W' ? new Color32(51,118,181,255) : terrain=='M' ? new Color32(116,111,101,255) : new Color32(157,177,100,255);
                if (cache == null && nav.TryGetCell(nav.OriginX+(x+.5f)*nav.CellSize,nav.OriginY+(y+.5f)*nav.CellSize,out var kind))
                    color = (kind & SurfaceGroundCellKind.Water) != 0 ? new Color32(51,118,181,255) : (kind & SurfaceGroundCellKind.Solid) != 0 ? new Color32(82,78,73,255) : color;
                if(forest) color=new Color32(51,111,62,255); colors[pixelX+(textureHeight-1-pixelY)*textureWidth]=color;
            }
            if (_texture != null) Object.Destroy(_texture);
            _texture=new Texture2D(textureWidth,textureHeight,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp}; _texture.SetPixels32(colors); _texture.Apply(false,true);
            _worldBounds=new Rect(nav.OriginX,nav.OriginY,
                width*(cache?.CellSize ?? nav.CellSize),height*(cache?.CellSize ?? nav.CellSize));
        }
    }
}
