using System;
using XianXia.Core.World.Surface;

namespace XianXia.Data.Content
{
    /// <summary>Pure authored-coverage egress query. It is independent of loaded chunks and WalkGrid.</summary>
    public static class OutdoorSurfaceBoundaryEgressResolver
    {
        public enum State { Inside, CrossingOuterBoundary, Outside }

        public struct Result
        {
            public State EgressState;
            public float BoundaryWorldX, BoundaryWorldY;
            public float JustOutsideWorldX, JustOutsideWorldY;
        }

        // One centralized, metric-relative offset makes the half-open chunk query unambiguously outside.
        public const float JustOutsideEpsilonFraction = 0.0001f;

        public static bool TryResolve(
            OutdoorWorldSurfaceDefinition surface,
            float currentWorldX, float currentWorldY,
            float desiredWorldX, float desiredWorldY,
            out Result result)
        {
            result = default;
            if (surface == null) return false;
            var currentInside = OutdoorSurfaceCoverageResolver.ContainsWorldPosition(surface, currentWorldX, currentWorldY);
            var desiredInside = OutdoorSurfaceCoverageResolver.ContainsWorldPosition(surface, desiredWorldX, desiredWorldY);
            if (!currentInside)
            {
                result.EgressState = State.Outside;
                return true;
            }
            if (desiredInside)
            {
                result.EgressState = State.Inside;
                return true;
            }

            // First union exit along the movement ray. The current point is known inside and the
            // desired point outside, so bisection stays correct for rectangular chunk unions too.
            var lo = 0f;
            var hi = 1f;
            var dx = desiredWorldX - currentWorldX;
            var dy = desiredWorldY - currentWorldY;
            if (dx * dx + dy * dy <= 0.0000000001f) return false;
            for (var i = 0; i < 28; i++)
            {
                var t = (lo + hi) * 0.5f;
                if (OutdoorSurfaceCoverageResolver.ContainsWorldPosition(surface, currentWorldX + dx * t, currentWorldY + dy * t)) lo = t;
                else hi = t;
            }

            var length = (float)Math.Sqrt(dx * dx + dy * dy);
            var epsilon = Math.Max(0.000001f, Math.Min(surface.ChunkWidth, surface.ChunkHeight) * JustOutsideEpsilonFraction);
            result.EgressState = State.CrossingOuterBoundary;
            result.BoundaryWorldX = currentWorldX + dx * lo;
            result.BoundaryWorldY = currentWorldY + dy * lo;
            result.JustOutsideWorldX = result.BoundaryWorldX + dx / length * epsilon;
            result.JustOutsideWorldY = result.BoundaryWorldY + dy / length * epsilon;
            return true;
        }
    }
}
