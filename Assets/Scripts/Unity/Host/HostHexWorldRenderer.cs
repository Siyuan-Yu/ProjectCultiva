using System;
using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// RimWorld 式 Hex WorldMap 批处理渲染：Chunk 缓存世界几何 + 每帧单次 GL 批绘制。
    /// 0 GameObject / 0 Collider / 0 per-Hex MonoBehaviour。
    /// </summary>
    public static class HostHexWorldRenderer
    {
        const int MaxVerts = 280_000;
        /// <summary>Unity GL 单次 Begin/End 顶点上限约 65535；分批提交避免整批地形静默失败。</summary>
        const int MaxGlVertsPerBatch = 60_000;
        const float HoverOutlineHalfWidthPx = 1.6f;
        const float SelectOutlineHalfWidthPx = 2.1f;

        static readonly Color PlainColor = ToColor(HexTerrainPresentation.ResolveRgb(new HexCell { Terrain = HexTerrainType.Plain }));
        static readonly Color ForestColor = ToColor(HexTerrainPresentation.ResolveRgb(new HexCell { Terrain = HexTerrainType.Forest }));
        static readonly Color MountainColor = ToColor(HexTerrainPresentation.ResolveRgb(new HexCell { Terrain = HexTerrainType.Mountain, IsPassable = false }));
        static readonly Color WaterColor = ToColor(HexTerrainPresentation.ResolveRgb(new HexCell { Terrain = HexTerrainType.Water, IsPassable = false }));
        static readonly Color RoadTint = ToColor(HexTerrainPresentation.ResolveRgb(new HexCell { Terrain = HexTerrainType.Road, IsRoad = true }));
        static readonly Color PathPreviewFill = new Color(0.40f, 0.82f, 0.96f, 0.48f);
        static readonly Color PathPreviewBorder = new Color(0.18f, 0.58f, 0.78f, 0.92f);
        static readonly Color HoverBorder = new Color(0.95f, 0.78f, 0.10f, 1f);
        static readonly Color SelectBorder = new Color(0.92f, 0.48f, 0.06f, 1f);
        static readonly Color SiteFootprintSelectFill = new Color(1f, 0.92f, 0.20f, 0.28f);
        static readonly Color SiteFootprintSelectBorder = new Color(1f, 0.55f, 0.05f, 1f);

        static readonly float[] CornerWx = new float[6];
        static readonly float[] CornerWy = new float[6];
        static readonly Vector2[] CornerScreen = new Vector2[6];

        static readonly float[] TerrainVx = new float[MaxVerts];
        static readonly float[] TerrainVy = new float[MaxVerts];
        static readonly float[] TerrainCr = new float[MaxVerts];
        static readonly float[] TerrainCg = new float[MaxVerts];
        static readonly float[] TerrainCb = new float[MaxVerts];
        static readonly float[] TerrainCa = new float[MaxVerts];

        static readonly float[] BorderVx = new float[MaxVerts];
        static readonly float[] BorderVy = new float[MaxVerts];

        static Material _glMaterial;
        static HexWorld _cachedWorld;
        static HexTerrainChunkCache _terrainCache;

        /// <summary>开发验证：CellFillScale=0.80 + 高对比 Gutter。默认 OFF。</summary>
        public static bool DebugStrongHexSeparation
        {
            get => _debugStrongHexSeparation;
            set => _debugStrongHexSeparation = value;
        }

        static bool _debugStrongHexSeparation;

        /// <summary>兼容旧名。</summary>
        public static bool DebugStrongHexGrid
        {
            get => DebugStrongHexSeparation;
            set => DebugStrongHexSeparation = value;
        }

        public static Color ResolveGutterColor()
        {
            var rgb = HexTerrainVisualInset.ResolveGutterColor(DebugStrongHexSeparation);
            return ToColor(rgb);
        }

        public static void InvalidateTerrainCache() => _terrainCache = null;

        public static void Draw(
            HexMapViewportProjection projection,
            SimulationWorld world,
            Texture2D pixel,
            HexCoord? selectedHex,
            HexCoord? hoverHex,
            WorldSite selectedWorldSite,
            bool[] pathMask,
            int pathMaskWidth,
            int pathMaskHeight)
        {
            if (world?.HexWorld == null || !world.HexWorld.HasGrid || projection.Viewport.width <= 1f)
                return;

            if (Event.current != null && Event.current.type != EventType.Repaint)
                return;

            var grid = world.HexWorld;
            EnsureTerrainCache(grid);

            ComputeViewBounds(projection, out var minWx, out var maxWx, out var minWy, out var maxWy);
            var hexScreenRadius = grid.HexSize * projection.Scale;
            var terrainInsetScale = HexTerrainVisualInset.ResolveInsetScale(DebugStrongHexSeparation);

            var terrainCount = 0;
            if (grid.UsesCompactStorage)
            {
                BatchDrawTerrainCompact(
                    grid,
                    projection,
                    minWx,
                    maxWx,
                    minWy,
                    maxWy,
                    pathMask,
                    pathMaskWidth,
                    pathMaskHeight,
                    terrainInsetScale,
                    ref terrainCount);
                FlushTriangles(TerrainVx, TerrainVy, TerrainCr, TerrainCg, TerrainCb, TerrainCa, terrainCount);
            }
            else
            {
                BatchDrawTerrainSparse(
                    grid,
                    projection,
                    minWx,
                    maxWx,
                    minWy,
                    maxWy,
                    terrainInsetScale,
                    ref terrainCount);
                FlushTriangles(TerrainVx, TerrainVy, TerrainCr, TerrainCg, TerrainCb, TerrainCa, terrainCount);
            }

            if (selectedWorldSite != null)
                DrawWorldSiteFootprintSelection(projection, grid, selectedWorldSite, ref terrainCount);

            var suppressSingleHexSelect = selectedWorldSite != null &&
                                          selectedHex.HasValue &&
                                          selectedWorldSite.OccupiesHex(selectedHex.Value);
            DrawOverlayOutline(projection, grid, hoverHex, selectedHex, HoverBorder, HoverOutlineHalfWidthPx);
            if (!suppressSingleHexSelect)
                DrawOverlayOutline(projection, grid, selectedHex, null, SelectBorder, SelectOutlineHalfWidthPx);

            WorldSitePresentationLayer.Draw(projection, world, pixel, hexScreenRadius);
        }

        static void DrawWorldSiteFootprintSelection(
            HexMapViewportProjection projection,
            HexWorld grid,
            WorldSite site,
            ref int vertCount)
        {
            if (site == null)
                return;

            const float fillInset = 0.94f;
            foreach (var hex in site.EnumerateFootprintHexes())
            {
                if (!grid.Contains(hex))
                    continue;
                if (vertCount + 18 >= MaxVerts)
                {
                    FlushTriangles(TerrainVx, TerrainVy, TerrainCr, TerrainCg, TerrainCb, TerrainCa, vertCount);
                    vertCount = 0;
                }

                EmitTerrainFill(projection, hex, grid.HexSize, SiteFootprintSelectFill, fillInset, ref vertCount);
            }

            FlushTriangles(TerrainVx, TerrainVy, TerrainCr, TerrainCg, TerrainCb, TerrainCa, vertCount);
            vertCount = 0;

            foreach (var hex in site.EnumerateFootprintHexes())
            {
                if (!grid.Contains(hex))
                    continue;

                ProjectLogicalHexCorners(projection, hex, grid.HexSize, CornerScreen);
                for (var i = 0; i < 6; i++)
                {
                    var next = (i + 1) % 6;
                    if (vertCount + 18 >= MaxVerts)
                    {
                        FlushTriangles(TerrainVx, TerrainVy, TerrainCr, TerrainCg, TerrainCb, TerrainCa, vertCount);
                        vertCount = 0;
                    }

                    AppendLineQuad(
                        CornerScreen[i],
                        CornerScreen[next],
                        3.4f,
                        SiteFootprintSelectBorder.r,
                        SiteFootprintSelectBorder.g,
                        SiteFootprintSelectBorder.b,
                        SiteFootprintSelectBorder.a,
                        ref vertCount);
                }
            }

            FlushTriangles(TerrainVx, TerrainVy, TerrainCr, TerrainCg, TerrainCb, TerrainCa, vertCount);
            vertCount = 0;
        }

        public static void DrawPathPolyline(
            HexMapViewportProjection projection,
            SimulationWorld world,
            IReadOnlyList<HexCoord> pathPreview)
        {
            if (Event.current != null && Event.current.type != EventType.Repaint)
                return;
            if (world?.HexWorld == null || pathPreview == null || pathPreview.Count < 2)
                return;
            for (var i = 0; i < pathPreview.Count - 1; i++)
            {
                var a = projection.ProjectHexCenter(pathPreview[i]);
                var b = projection.ProjectHexCenter(pathPreview[i + 1]);
                DrawScreenLine(a, b, PathPreviewBorder, 2.5f);
            }
        }

        [Obsolete("Use HexMapViewportProjection.TryPickHex.")]
        public static bool TryPickHex(SimulationWorld world, float worldX, float worldY, out HexCoord coord)
        {
            coord = default;
            if (world?.HexWorld == null || !world.HexWorld.HasGrid)
                return false;

            coord = world.HexWorld.WorldToHex(worldX, worldY);
            return world.HexWorld.Contains(coord);
        }

        public static void ComputeWorldBounds(HexWorld grid, out float minX, out float maxX, out float minY, out float maxY) =>
            HexWorldLayout.ComputeWorldBounds(grid, out minX, out maxX, out minY, out maxY);

        static void EnsureTerrainCache(HexWorld grid)
        {
            if (_terrainCache != null && ReferenceEquals(_cachedWorld, grid) && _terrainCache.Matches(grid))
                return;
            _cachedWorld = grid;
            _terrainCache = HexTerrainChunkCache.Build(grid);
        }

        static void BatchDrawTerrainCompact(
            HexWorld grid,
            HexMapViewportProjection projection,
            float minWx,
            float maxWx,
            float minWy,
            float maxWy,
            bool[] pathMask,
            int maskW,
            int maskH,
            float terrainInsetScale,
            ref int vertCount)
        {
            var pad = grid.HexSize * 1.2f;
            HexWorldMapRenderBounds.ComputeVisibleCompactRange(
                grid,
                minWx,
                maxWx,
                minWy,
                maxWy,
                pad,
                out var qMin,
                out var qMax,
                out var rMin,
                out var rMax);

            for (var r = rMin; r <= rMax; r++)
            {
                for (var q = qMin; q <= qMax; q++)
                {
                    if (!grid.TryGetCell(new HexCoord(q, r), out var cell) || cell == null)
                        continue;

                    HexMath.ToWorldPosition(cell.Coord, grid.HexSize, out var cx, out var cy);
                    if (cx < minWx - pad || cx > maxWx + pad || cy < minWy - pad || cy > maxWy + pad)
                        continue;

                    var fill = ResolveTerrainColor(cell);
                    if (pathMask != null && q >= 0 && r >= 0 && q < maskW && r < maskH && pathMask[q + r * maskW])
                        fill = Color.Lerp(fill, PathPreviewFill, 0.72f);

                    if (vertCount + 18 >= MaxVerts)
                    {
                        FlushTriangles(TerrainVx, TerrainVy, TerrainCr, TerrainCg, TerrainCb, TerrainCa, vertCount);
                        vertCount = 0;
                    }

                    EmitTerrainFill(projection, cell.Coord, grid.HexSize, fill, terrainInsetScale, ref vertCount);
                }
            }
        }

        static void BatchDrawTerrainSparse(
            HexWorld grid,
            HexMapViewportProjection projection,
            float minWx,
            float maxWx,
            float minWy,
            float maxWy,
            float terrainInsetScale,
            ref int vertCount)
        {
            var pad = grid.HexSize * 1.2f;
            foreach (var kv in grid.Tiles)
            {
                var cell = kv.Value;
                if (cell == null)
                    continue;
                HexMath.ToWorldPosition(cell.Coord, grid.HexSize, out var cx, out var cy);
                if (cx < minWx - pad || cx > maxWx + pad || cy < minWy - pad || cy > maxWy + pad)
                    continue;
                EmitTerrainFill(
                    projection,
                    cell.Coord,
                    grid.HexSize,
                    ResolveTerrainColor(cell),
                    terrainInsetScale,
                    ref vertCount);
            }
        }

        static void EmitTerrainFill(
            HexMapViewportProjection projection,
            HexCoord coord,
            float hexSize,
            Color fill,
            float insetScale,
            ref int vertCount)
        {
            if (vertCount + 18 >= MaxVerts)
                return;

            ProjectInsetHexCorners(projection, coord, hexSize, insetScale, CornerScreen);
            var center = projection.ProjectHexCenter(coord);
            var fr = fill.r;
            var fg = fill.g;
            var fb = fill.b;
            var fa = fill.a;

            for (var i = 0; i < 6; i++)
            {
                var next = (i + 1) % 6;
                AppendTriangle(center, CornerScreen[i], CornerScreen[next], fr, fg, fb, fa, ref vertCount);
            }
        }

        static void DrawOverlayOutline(
            HexMapViewportProjection projection,
            HexWorld grid,
            HexCoord? coord,
            HexCoord? skipIfEquals,
            Color border,
            float halfWidthPx)
        {
            if (!coord.HasValue || !grid.Contains(coord.Value))
                return;
            if (skipIfEquals.HasValue && skipIfEquals.Value.Equals(coord.Value))
                return;

            ProjectLogicalHexCorners(projection, coord.Value, grid.HexSize, CornerScreen);
            var count = 0;
            var r = border.r;
            var g = border.g;
            var b = border.b;
            var a = border.a;
            for (var i = 0; i < 6; i++)
            {
                var next = (i + 1) % 6;
                AppendLineQuad(CornerScreen[i], CornerScreen[next], halfWidthPx, r, g, b, a, ref count);
            }

            FlushTriangles(TerrainVx, TerrainVy, TerrainCr, TerrainCg, TerrainCb, TerrainCa, count);
        }

        static void ProjectLogicalHexCorners(
            HexMapViewportProjection projection,
            HexCoord coord,
            float hexSize,
            Vector2[] cornerScreenOut)
        {
            HexMath.CollectCornerWorldPositions(coord, hexSize, CornerWx, CornerWy);
            for (var i = 0; i < 6; i++)
                cornerScreenOut[i] = projection.ProjectWorld(CornerWx[i], CornerWy[i]);
        }

        static void ProjectInsetHexCorners(
            HexMapViewportProjection projection,
            HexCoord coord,
            float hexSize,
            float insetScale,
            Vector2[] cornerScreenOut)
        {
            HexTerrainVisualInset.CollectInsetCornerWorldPositions(coord, hexSize, insetScale, CornerWx, CornerWy);
            for (var i = 0; i < 6; i++)
                cornerScreenOut[i] = projection.ProjectWorld(CornerWx[i], CornerWy[i]);
        }

        static void AppendTriangle(
            Vector2 a,
            Vector2 b,
            Vector2 c,
            float r,
            float g,
            float bcol,
            float aCol,
            ref int count)
        {
            TerrainVx[count] = a.x;
            TerrainVy[count] = a.y;
            TerrainCr[count] = r;
            TerrainCg[count] = g;
            TerrainCb[count] = bcol;
            TerrainCa[count] = aCol;
            count++;

            TerrainVx[count] = b.x;
            TerrainVy[count] = b.y;
            TerrainCr[count] = r;
            TerrainCg[count] = g;
            TerrainCb[count] = bcol;
            TerrainCa[count] = aCol;
            count++;

            TerrainVx[count] = c.x;
            TerrainVy[count] = c.y;
            TerrainCr[count] = r;
            TerrainCg[count] = g;
            TerrainCb[count] = bcol;
            TerrainCa[count] = aCol;
            count++;
        }

        static void AppendLineQuad(
            Vector2 a,
            Vector2 b,
            float halfWidthPx,
            float r,
            float g,
            float bcol,
            float aCol,
            ref int count)
        {
            var delta = b - a;
            var len = delta.magnitude;
            if (len < 0.001f)
                return;

            var nx = -delta.y / len * halfWidthPx;
            var ny = delta.x / len * halfWidthPx;
            var v0 = new Vector2(a.x + nx, a.y + ny);
            var v1 = new Vector2(a.x - nx, a.y - ny);
            var v2 = new Vector2(b.x - nx, b.y - ny);
            var v3 = new Vector2(b.x + nx, b.y + ny);
            AppendTriangle(v0, v1, v2, r, g, bcol, aCol, ref count);
            AppendTriangle(v0, v2, v3, r, g, bcol, aCol, ref count);
        }

        static void FlushTriangles(
            float[] vx,
            float[] vy,
            float[] cr,
            float[] cg,
            float[] cb,
            float[] ca,
            int count)
        {
            if (count < 3)
                return;
            EnsureGlMaterial();
            _glMaterial.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, Screen.width, Screen.height, 0);
            var offset = 0;
            while (offset < count)
            {
                var batchCount = Mathf.Min(MaxGlVertsPerBatch, count - offset);
                batchCount -= batchCount % 3;
                if (batchCount < 3)
                    break;
                GL.Begin(GL.TRIANGLES);
                for (var i = 0; i < batchCount; i++)
                {
                    var idx = offset + i;
                    GL.Color(new Color(cr[idx], cg[idx], cb[idx], ca[idx]));
                    GL.Vertex3(vx[idx], vy[idx], 0f);
                }

                GL.End();
                offset += batchCount;
            }

            GL.PopMatrix();
        }

        static void FlushLines(float[] vx, float[] vy, int count, Color color, float alpha)
        {
            if (count < 2)
                return;
            EnsureGlMaterial();
            _glMaterial.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, Screen.width, Screen.height, 0);
            var lineColor = new Color(color.r, color.g, color.b, color.a * alpha);
            var offset = 0;
            while (offset < count)
            {
                var batchCount = Mathf.Min(MaxGlVertsPerBatch, count - offset);
                batchCount -= batchCount % 2;
                if (batchCount < 2)
                    break;
                GL.Begin(GL.LINES);
                GL.Color(lineColor);
                for (var i = 0; i < batchCount; i++)
                    GL.Vertex3(vx[offset + i], vy[offset + i], 0f);
                GL.End();
                offset += batchCount;
            }

            GL.PopMatrix();
        }

        static void DrawScreenLine(Vector2 a, Vector2 b, Color color, float width)
        {
            var delta = b - a;
            var len = delta.magnitude;
            if (len < 0.5f)
                return;

            var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            var prev = GUI.color;
            GUI.color = color;
            var matrix = GUI.matrix;
            var center = (a + b) * 0.5f;
            GUIUtility.RotateAroundPivot(angle, center);
            GUI.DrawTexture(
                new Rect(center.x - len * 0.5f, center.y - width * 0.5f, len, width),
                Texture2D.whiteTexture);
            GUI.matrix = matrix;
            GUI.color = prev;
        }

        static void ComputeViewBounds(
            HexMapViewportProjection projection,
            out float minWx,
            out float maxWx,
            out float minWy,
            out float maxWy)
        {
            var viewport = projection.Viewport;
            var corners = new[]
            {
                new Vector2(viewport.xMin, viewport.yMin),
                new Vector2(viewport.xMax, viewport.yMin),
                new Vector2(viewport.xMin, viewport.yMax),
                new Vector2(viewport.xMax, viewport.yMax),
            };
            minWx = float.MaxValue;
            maxWx = float.MinValue;
            minWy = float.MaxValue;
            maxWy = float.MinValue;
            for (var i = 0; i < corners.Length; i++)
            {
                var world = projection.ScreenToWorld(corners[i]);
                minWx = Mathf.Min(minWx, world.x);
                maxWx = Mathf.Max(maxWx, world.x);
                minWy = Mathf.Min(minWy, world.y);
                maxWy = Mathf.Max(maxWy, world.y);
            }
        }

        static Color ToColor(HexRgb rgb) => new Color(rgb.R, rgb.G, rgb.B, 1f);

        static Color ResolveTerrainColor(HexCell tile)
        {
            var rgb = HexTerrainPresentation.ResolveRgb(tile);
            return ToColor(rgb);
        }

        static void EnsureGlMaterial()
        {
            if (_glMaterial != null)
                return;
            var shader = Shader.Find("Hidden/Internal-Colored");
            _glMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        /// <summary>地形 Chunk 元数据缓存（世界空间）；地形变更时 Invalidate。</summary>
        sealed class HexTerrainChunkCache
        {
            public int Width;
            public int Height;
            public float HexSize;

            public static HexTerrainChunkCache Build(HexWorld grid)
            {
                return new HexTerrainChunkCache
                {
                    Width = grid.Width,
                    Height = grid.Height,
                    HexSize = grid.HexSize,
                };
            }

            public bool Matches(HexWorld grid) =>
                grid != null &&
                Width == grid.Width &&
                Height == grid.Height &&
                Math.Abs(HexSize - grid.HexSize) < 0.0001f;
        }
    }
}
