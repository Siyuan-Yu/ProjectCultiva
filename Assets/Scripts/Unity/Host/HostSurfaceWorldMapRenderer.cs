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
                var terrain = cache != null ? cache.BaseTerrainRows[y][x] : 'P';
                var finalWorld = nav.SurfaceId == "base:surface_main_wilderness_v1" &&
                                 width == 3800 && height == 1700;
                var forest = cache != null && (finalWorld
                    ? HasNearbyForest(cache, x, y, width, height)
                    : cache.ForestRows[y][x] != '0');
                Color32 color = finalWorld
                    ? FinalWorldColor(x, y, width, height, terrain, forest ? '5' : '0')
                    : terrain=='W' ? new Color32(51,118,181,255) : terrain=='M' ? new Color32(116,111,101,255) : new Color32(157,177,100,255);
                if (cache == null && nav.TryGetCell(nav.OriginX+(x+.5f)*nav.CellSize,nav.OriginY+(y+.5f)*nav.CellSize,out var kind))
                    color = (kind & SurfaceGroundCellKind.Water) != 0 ? new Color32(51,118,181,255) : (kind & SurfaceGroundCellKind.Solid) != 0 ? new Color32(82,78,73,255) : color;
                if(forest && !finalWorld)
                    color=new Color32(51,111,62,255);
                colors[pixelX+(textureHeight-1-pixelY)*textureWidth]=color;
            }
            if (_texture != null) Object.Destroy(_texture);
            _texture=new Texture2D(textureWidth,textureHeight,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp}; _texture.SetPixels32(colors); _texture.Apply(false,true);
            _worldBounds=new Rect(nav.OriginX,nav.OriginY,
                width*(cache?.CellSize ?? nav.CellSize),height*(cache?.CellSize ?? nav.CellSize));
        }

        static bool HasNearbyForest(ContinuousSurfaceWorldMapDefinition cache, int x, int y, int width, int height)
        {
            for (var dy=-1;dy<=1;dy++) for (var dx=-1;dx<=1;dx++)
            {
                var nx = x+dx; var ny = y+dy;
                if (nx >= 0 && ny >= 0 && nx < width && ny < height && cache.ForestRows[ny][nx] != '0')
                    return true;
            }
            return false;
        }

        static Color32 FinalWorldColor(int x, int y, int width, int height, char terrain, char forest)
        {
            if (terrain == 'W') return new Color32(76, 145, 183, 255);
            var mapX = x * 1705f / width;
            var mapY = (height - y) * 738f / height;
            if (terrain == 'M') return new Color32(139, 139, 121, 255);
            if (Ellipse(mapX,mapY,1475f,722f,390f,150f))
                return new Color32(230, 206, 157, 255); // Serpent desert floor.
            if (Ellipse(mapX,mapY,125f,700f,245f,175f))
                return new Color32(166, 153, 137, 255); // Fire plateau.
            if (Ellipse(mapX,mapY,1510f,35f,380f,145f))
                return new Color32(204, 222, 217, 255); // Northern snow plateau.
            if (Ellipse(mapX,mapY,1545f,462f,210f,125f) &&
                !Ellipse(mapX,mapY,1655f,430f,45f,39f) &&
                !Ellipse(mapX,mapY,1574f,348f,26f,25f))
                return new Color32(158, 179, 181, 255); // Thunder field, with Storm City on plain.
            if (Ellipse(mapX,mapY,1545f,278f,210f,87f) &&
                !Ellipse(mapX,mapY,1574f,348f,26f,25f))
                return new Color32(184, 208, 200, 255); // Gale field.
            if (Ellipse(mapX,mapY,115f,80f,245f,160f))
                return new Color32(179, 179, 165, 255); // Magnetic ravine.
            if (forest >= '1' && Ellipse(mapX,mapY,1000f,315f,180f,153f))
                return new Color32(119, 156, 125, 255); // Beast core.
            if (forest >= '1') return new Color32(160, 185, 149, 255); // Beast outskirts.
            if (mapX > 255f && mapX < 550f && mapY > 139f && mapY < 302f)
                return new Color32(193, 197, 157, 255); // Zhenyue highland.
            return new Color32(193, 207, 160, 255);
        }

        static bool Ellipse(float x, float y, float centerX, float centerY, float radiusX, float radiusY)
        {
            var dx = (x-centerX)/radiusX;
            var dy = (y-centerY)/radiusY;
            return dx*dx+dy*dy < 1f;
        }
    }
}
