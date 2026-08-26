using System.Diagnostics;
using System.Text;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// WorldSite → Wilderness departure 一次性 Trace（非每帧）。
    /// </summary>
    public static class BackgroundSiteDepartureTravelTrace
    {
        public readonly struct Snapshot
        {
            public Snapshot(
                EntityId characterId,
                string sourceLocation,
                HexCoord targetHex,
                HexCoord chosenExitHex,
                HexCoord chosenFootprintHex,
                int chosenDirection,
                string routeHexes,
                int segmentCount,
                string segmentStart,
                string segmentEnd,
                bool worldLocationCommitted,
                bool enteredHexRaised,
                bool travelCompleteRaised,
                bool materializeRequested,
                bool isTravelingAfterBegin)
            {
                CharacterId = characterId;
                SourceLocation = sourceLocation ?? string.Empty;
                TargetHex = targetHex;
                ChosenExitHex = chosenExitHex;
                ChosenFootprintHex = chosenFootprintHex;
                ChosenDirection = chosenDirection;
                RouteHexes = routeHexes ?? string.Empty;
                SegmentCount = segmentCount;
                SegmentStart = segmentStart ?? string.Empty;
                SegmentEnd = segmentEnd ?? string.Empty;
                WorldLocationCommitted = worldLocationCommitted;
                EnteredHexRaised = enteredHexRaised;
                TravelCompleteRaised = travelCompleteRaised;
                MaterializeRequested = materializeRequested;
                IsTravelingAfterBegin = isTravelingAfterBegin;
            }

            public EntityId CharacterId { get; }
            public string SourceLocation { get; }
            public HexCoord TargetHex { get; }
            public HexCoord ChosenExitHex { get; }
            public HexCoord ChosenFootprintHex { get; }
            public int ChosenDirection { get; }
            public string RouteHexes { get; }
            public int SegmentCount { get; }
            public string SegmentStart { get; }
            public string SegmentEnd { get; }
            public bool WorldLocationCommitted { get; }
            public bool EnteredHexRaised { get; }
            public bool TravelCompleteRaised { get; }
            public bool MaterializeRequested { get; }
            public bool IsTravelingAfterBegin { get; }
        }

        public static void Log(in Snapshot snapshot)
        {
            var line =
                "BackgroundSiteDepartureTrace:" +
                " Character=" + snapshot.CharacterId.Value +
                " SourceLocation=" + snapshot.SourceLocation +
                " TargetHex=" + snapshot.TargetHex +
                " ChosenBoundaryConnection=" + snapshot.ChosenFootprintHex + "->" + snapshot.ChosenExitHex +
                " Direction=" + snapshot.ChosenDirection +
                " RouteHexes=" + snapshot.RouteHexes +
                " SegmentCount=" + snapshot.SegmentCount +
                " SegmentStart=" + snapshot.SegmentStart +
                " SegmentEnd=" + snapshot.SegmentEnd +
                " WorldLocationCommitted=" + snapshot.WorldLocationCommitted +
                " EnteredHexRaised=" + snapshot.EnteredHexRaised +
                " TravelCompleteRaised=" + snapshot.TravelCompleteRaised +
                " MaterializeRequested=" + snapshot.MaterializeRequested +
                " IsTravelingAfterBegin=" + snapshot.IsTravelingAfterBegin;
            BackgroundTravelTraceSink.Emit(line);
        }

        public static string FormatRoute(System.Collections.Generic.IReadOnlyList<HexCoord> path)
        {
            if (path == null || path.Count == 0)
                return "[]";
            var sb = new StringBuilder("[");
            for (var i = 0; i < path.Count; i++)
            {
                if (i > 0)
                    sb.Append(',');
                sb.Append(path[i]);
            }

            sb.Append(']');
            return sb.ToString();
        }

        public static string FormatWorldVec(WorldVec2 pos) =>
            pos.X.ToString("0.###") + "," + pos.Y.ToString("0.###");
    }
}
