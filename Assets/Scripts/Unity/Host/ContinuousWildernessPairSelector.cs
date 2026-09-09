using System;
using System.Collections.Generic;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;
using XianXia.Data.Content;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Content-based selector for one deterministic horizontal Wilderness A↔B pair (W1B).
    /// Does not hardcode gameplay branches on specific hex ids; ranks candidates from formal exits.
    /// </summary>
    public static class ContinuousWildernessPairSelector
    {
        public readonly struct Candidate
        {
            public Candidate(
                SurfaceExitConnection forwardSeam,
                HexCoord hexA,
                HexCoord hexB,
                string mapLayoutA,
                string mapLayoutB)
            {
                ForwardSeam = forwardSeam;
                HexA = hexA;
                HexB = hexB;
                MapLayoutA = mapLayoutA ?? string.Empty;
                MapLayoutB = mapLayoutB ?? string.Empty;
            }

            public SurfaceExitConnection ForwardSeam { get; }
            public HexCoord HexA { get; }
            public HexCoord HexB { get; }
            public string MapLayoutA { get; }
            public string MapLayoutB { get; }
        }

        public static bool TrySelectAcceptancePair(
            SimulationWorld world,
            IReadOnlyList<SurfaceExitVisibleZone> zones,
            HexCoord partyHex,
            out Candidate candidate,
            out string diagnostic)
        {
            candidate = default;
            diagnostic = string.Empty;
            if (world == null)
            {
                diagnostic = "No SimulationWorld.";
                return false;
            }

            if (zones == null || zones.Count == 0)
            {
                diagnostic = "No usable SurfaceExit zones to evaluate.";
                return false;
            }

            Candidate? best = null;
            var bestScore = int.MinValue;
            for (var i = 0; i < zones.Count; i++)
            {
                var seam = zones[i].Connection;
                if (!IsHorizontalWildernessPair(world, seam, out var mapA, out var mapB))
                    continue;

                var score = ScoreCandidate(seam, partyHex, mapA, mapB);
                if (score <= bestScore)
                    continue;
                bestScore = score;
                best = new Candidate(
                    seam,
                    seam.SourceHex,
                    seam.DestinationHex,
                    mapA,
                    mapB);
            }

            if (!best.HasValue)
            {
                diagnostic =
                    "No lattice-compatible horizontal Wilderness pair found in current SurfaceExit zones.";
                return false;
            }

            candidate = best.Value;
            diagnostic =
                "ActivePair A=" + candidate.HexA +
                " B=" + candidate.HexB +
                " direction=" + candidate.ForwardSeam.DirectionIndex +
                " mapA=" + candidate.MapLayoutA +
                " mapB=" + candidate.MapLayoutB;
            return true;
        }

        static int ScoreCandidate(
            SurfaceExitConnection seam,
            HexCoord partyHex,
            string mapA,
            string mapB)
        {
            var score = 0;
            if (partyHex.Equals(seam.SourceHex) || partyHex.Equals(seam.DestinationHex))
                score += 1000;
            if (string.Equals(mapA, mapB, StringComparison.Ordinal))
                score += 100;
            // Deterministic tie-break: prefer lexicographically smaller source hex, then destination.
            score -= seam.SourceHex.Q * 10 + seam.SourceHex.R;
            score -= seam.DestinationHex.Q * 5 + seam.DestinationHex.R;
            return score;
        }

        static bool IsHorizontalWildernessPair(
            SimulationWorld world,
            SurfaceExitConnection seam,
            out string sourceMapId,
            out string destinationMapId)
        {
            sourceMapId = string.Empty;
            destinationMapId = string.Empty;
            if (seam.DestinationKind != SurfaceExitDestinationKind.WildernessHex)
                return false;
            if (Math.Abs(seam.LocalDirectionY) > 0.0001f || Math.Abs(seam.LocalDirectionX) < 0.0001f)
                return false;
            if (world.Strategic?.Sites != null &&
                world.Strategic.Sites.TryGetAtHex(seam.SourceHex, out var sourceSite) && sourceSite != null)
                return false;
            if (world.Strategic?.Sites != null &&
                world.Strategic.Sites.TryGetAtHex(seam.DestinationHex, out var destSite) && destSite != null)
                return false;
            if (!WildernessLocalMapFallback.TryResolve(world, seam.SourceHex, out sourceMapId) ||
                !WildernessLocalMapFallback.TryResolve(world, seam.DestinationHex, out destinationMapId))
                return false;
            return true;
        }
    }
}
