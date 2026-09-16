using UnityEngine;
using XianXia.Core.World.Surface;
using XianXia.Data.Content;

namespace XianXia.Unity.Host
{
    /// <summary>Cached Surface WorldMap terrain. One texture rebuild per content identity, never per IMGUI repaint.</summary>
    public sealed class HostSurfaceWorldMapRenderer
    {
        Texture2D _texture; string _identity = string.Empty; Rect _worldBounds;
        public void Draw(Rect mapRect, SurfaceWorldMapViewportProjection projection, ContinuousSurfaceWorldMapDefinition cache, SurfaceGroundNavigation fallback)
        {
            Ensure(cache, fallback); if (_texture == null) return;
            var screen = projection.ProjectWorldRect(_worldBounds); GUI.DrawTexture(screen, _texture, ScaleMode.StretchToFill, false);
        }
        void Ensure(ContinuousSurfaceWorldMapDefinition cache, SurfaceGroundNavigation nav)
        {
            var identity = cache != null ? cache.SurfaceId + "|" + cache.SourceHash : nav?.SurfaceId + "|" + nav?.SourceHash;
            if (_texture != null && identity == _identity) return;
            _identity = identity; if (nav == null) return;
            var width = cache?.WidthCells ?? nav.Width; var height = cache?.HeightCells ?? nav.Height;
            var colors = new Color32[width * height];
            for (var y=0;y<height;y++) for(var x=0;x<width;x++)
            {
                var terrain = cache != null ? cache.BaseTerrainRows[y][x] : 'P'; var forest = cache != null && cache.ForestRows[y][x] != '0';
                Color32 color = terrain=='W' ? new Color32(51,118,181,255) : terrain=='M' ? new Color32(116,111,101,255) : new Color32(157,177,100,255);
                if (cache == null && nav.TryGetCell(nav.OriginX+(x+.5f)*nav.CellSize,nav.OriginY+(y+.5f)*nav.CellSize,out var kind))
                    color = (kind & SurfaceGroundCellKind.Water) != 0 ? new Color32(51,118,181,255) : (kind & SurfaceGroundCellKind.Solid) != 0 ? new Color32(82,78,73,255) : color;
                if(forest) color=new Color32(51,111,62,255); colors[x+(height-1-y)*width]=color;
            }
            if (_texture != null) Object.Destroy(_texture);
            _texture=new Texture2D(width,height,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp}; _texture.SetPixels32(colors); _texture.Apply(false,true);
            _worldBounds=new Rect(nav.OriginX,nav.OriginY,width*nav.CellSize,height*nav.CellSize);
        }
    }
}
