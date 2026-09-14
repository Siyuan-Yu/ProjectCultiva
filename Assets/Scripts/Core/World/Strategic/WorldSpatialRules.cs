using System;
using System.Collections.Generic;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    public sealed class CoreLevelControlRange
    {
        public int Level { get; set; }
        public float WidthCells { get; set; }
        public float HeightCells { get; set; }
    }

    public readonly struct ResolvedWorldSpatialRange
    {
        public ResolvedWorldSpatialRange(float widthCells, float heightCells, float cellSize)
        {
            WidthCells = widthCells;
            HeightCells = heightCells;
            CellSize = cellSize;
            WidthWorld = widthCells * cellSize;
            HeightWorld = heightCells * cellSize;
        }

        public float WidthCells { get; }
        public float HeightCells { get; }
        public float CellSize { get; }
        public float WidthWorld { get; }
        public float HeightWorld { get; }
    }

    /// <summary>Content-owned metrics. Building collision/placement never consumes this catalog.</summary>
    public sealed class WorldSpatialRules
    {
        public string Id { get; set; } = string.Empty;
        public List<CoreLevelControlRange> CoreLevels { get; } = new List<CoreLevelControlRange>();
        public float WildernessEncounterWidthCells { get; set; }
        public float WildernessEncounterHeightCells { get; set; }
        public float InterventionDecisionSeconds { get; set; }
        public float InterventionArrivalSeconds { get; set; }
        public int InterventionRelationThreshold { get; set; }
        public int InterventionChanceBasisPoints { get; set; }

        public CoreLevelControlRange RequireLevel(int level)
        {
            for (var i = 0; i < CoreLevels.Count; i++)
                if (CoreLevels[i].Level == level) return CoreLevels[i];
            throw new InvalidOperationException("Missing core control range for level " + level);
        }

        public ResolvedWorldSpatialRange ResolveLevel(
            SimulationWorld world, int level, string surfaceId)
        {
            var range = RequireLevel(level);
            return Resolve(world, surfaceId, range.WidthCells, range.HeightCells);
        }

        public ResolvedWorldSpatialRange ResolveWildernessEncounter(
            SimulationWorld world, string surfaceId) =>
            Resolve(world, surfaceId, WildernessEncounterWidthCells, WildernessEncounterHeightCells);

        public void Bind(SimulationWorld world, WorldSite site)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            var range = ResolveLevel(world, site.CoreLevel, site.CoreSurfaceId);
            site.CoreRangeWidth = range.WidthWorld;
            site.CoreRangeHeight = range.HeightWorld;
        }

        static ResolvedWorldSpatialRange Resolve(
            SimulationWorld world, string surfaceId, float widthCells, float heightCells)
        {
            if (world?.SurfaceSpatial == null ||
                !world.SurfaceSpatial.TryGet(surfaceId, out var metric) ||
                metric == null || !(metric.CellSize > 0f) ||
                float.IsNaN(metric.CellSize) || float.IsInfinity(metric.CellSize))
                throw new InvalidOperationException(
                    "Surface metric unavailable for spatial range: " + (surfaceId ?? string.Empty));
            return new ResolvedWorldSpatialRange(widthCells, heightCells, metric.CellSize);
        }
    }
}
