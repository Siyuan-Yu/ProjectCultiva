using System.Diagnostics;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// BGTRAVEL 一次性完整 Trace（Development-only；不改变 Runtime 行为）。
    /// </summary>
    public static class BackgroundBgTravelFullTrace
    {
        static int _nextTraceId = 1;
        static int _activeTraceId;

        public static int BeginTrace() => _activeTraceId = _nextTraceId++;

        public static int ActiveTraceId => _activeTraceId;

        public static void Log(string section, string message)
        {
            var line = "BGTRAVEL TRACE #" + _activeTraceId + " [" + section + "] " + message;
            BackgroundTravelTraceSink.Emit(line);
        }

        public static void LogIntent(
            EntityId characterId,
            string sourceLocation,
            HexCoord targetHex,
            string destinationKind)
        {
            Log("Intent",
                "CharacterId=" + characterId.Value +
                " SourceLocation=" + sourceLocation +
                " TargetHex=" + targetHex +
                " DestinationKind=" + destinationKind);
        }

        public static void LogRoute(
            string routeHexes,
            int routeCount,
            HexCoord chosenExitHex,
            HexCoord chosenFootprintHex,
            int segmentCount,
            string segmentStart,
            string segmentEnd,
            bool isTravelingAfterBegin)
        {
            Log("Route",
                "RouteHexes=" + routeHexes +
                " RouteCount=" + routeCount +
                " BoundaryConnection=" + chosenFootprintHex + "->" + chosenExitHex +
                " SegmentCount=" + segmentCount +
                " SegmentStart=" + segmentStart +
                " SegmentEnd=" + segmentEnd +
                " IsTravelingAfterBegin=" + isTravelingAfterBegin);
        }

        public static void LogLocationCommit(
            string previousLocation,
            string newLocation,
            HexCoord previousHex,
            HexCoord newHex,
            string commitMethod)
        {
            Log("LocationCommit",
                "PreviousLocation=" + previousLocation +
                " NewLocation=" + newLocation +
                " PreviousHex=" + previousHex +
                " NewHex=" + newHex +
                " Method=" + commitMethod);
        }

        public static void LogRuntimeMap(
            SimulationWorld world,
            HexCoord enteredHex)
        {
            LoadedLocalMapBelongingExplain.ExplainTryResolveLoadedLocalMap(
                world,
                out var resolved,
                out var reason);
            Log("RuntimeMap",
                "EnteredHex=" + enteredHex +
                " " + reason +
                " Resolved=" + resolved);
        }

        public static void LogNotification(
            string eventName,
            bool handlerInvoked,
            string detail)
        {
            Log("Notification",
                "EventName=" + eventName +
                " HandlerInvoked=" + handlerInvoked +
                " Detail=" + detail);
        }

        public static void LogMaterializeGuard(
            string guardName,
            bool passed,
            string detail)
        {
            Log("MaterializeGuard",
                "Guard=" + guardName +
                " Passed=" + passed +
                " Detail=" + detail);
        }

        public static void LogMaterializeResult(
            EntityId targetCharacterId,
            bool requested,
            bool presentationExistsBefore,
            string guardResult,
            bool result)
        {
            Log("Materialize",
                "Requested=" + requested +
                " TargetCharacterId=" + targetCharacterId.Value +
                " PresentationExistsBefore=" + presentationExistsBefore +
                " GuardResult=" + guardResult +
                " Result=" + result);
        }

        public static void LogAuthority(
            CharacterWorldMovementAuthority before,
            CharacterWorldMovementAuthority after,
            bool isTraveling)
        {
            Log("Authority",
                "Before=" + before +
                " After=" + after +
                " IsTraveling=" + isTraveling);
        }

        public static void LogTravelComplete(bool triggered, string timing)
        {
            Log("TravelComplete", "Triggered=" + triggered + " Timing=" + timing);
        }

        public static void LogFlush(int pendingCount, int spawnedHint)
        {
            Log("HostFlush", "PendingPresentationCount=" + pendingCount + " SpawnHint=" + spawnedHint);
        }

        public static void LogActiveSideEffect(in BackgroundLoadedLocalMapArrivalDebug.ActiveSideEffectTrace trace)
        {
            Log("ActiveSideEffect",
                "ArrivingCharacterId=" + trace.ArrivingCharacterId.Value +
                " ActiveCharacterId=" + trace.ActiveCharacterId.Value +
                " ActiveWorldBefore=" + trace.ActiveWorldBefore +
                " ActiveLocalBefore=" + trace.ActiveLocalBefore +
                " ActiveWorldAfter=" + trace.ActiveWorldAfter +
                " ActiveLocalAfter=" + trace.ActiveLocalAfter +
                " ActivePositionChanged=" + trace.ActivePositionChanged);
        }
    }
}
