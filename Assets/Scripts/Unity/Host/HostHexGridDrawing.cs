using System;
using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>Hex WorldMap 绘制入口（委托 <see cref="HostHexWorldRenderer"/> 批处理实现）。</summary>
    public static class HostHexGridDrawing
    {
        public static void Draw(
            HexMapViewportProjection projection,
            SimulationWorld world,
            Texture2D pixel,
            HexCoord? selectedHex,
            HexCoord? hoverHex,
            WorldSite selectedWorldSite,
            IReadOnlyList<HexCoord> pathPreview,
            bool[] pathMask,
            int pathMaskWidth,
            int pathMaskHeight)
        {
            HostHexWorldRenderer.Draw(
                projection,
                world,
                pixel,
                selectedHex,
                hoverHex,
                selectedWorldSite,
                pathMask,
                pathMaskWidth,
                pathMaskHeight);

            if (pathPreview != null && pathPreview.Count > 1)
                HostHexWorldRenderer.DrawPathPolyline(projection, world, pathPreview);
        }

        public static bool TryPickHex(
            HexMapViewportProjection projection,
            SimulationWorld world,
            Vector2 screenGui,
            out HexCoord coord) =>
            HexMapMousePick.TryResolveMouseHex(projection, world?.HexWorld, screenGui, out coord);

        public static void ComputeWorldBounds(HexWorld grid, out float minX, out float maxX, out float minY, out float maxY) =>
            HostHexWorldRenderer.ComputeWorldBounds(grid, out minX, out maxX, out minY, out maxY);

        public static void InvalidateCache() => HostHexWorldRenderer.InvalidateTerrainCache();
    }
}
