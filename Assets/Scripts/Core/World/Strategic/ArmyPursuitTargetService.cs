using System;
using System.Collections.Generic;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// PursuitOrder 移动目标真源：FormalArmy.StrategicPosition；拓扑签名不含 Progress。
    /// </summary>
    public static class ArmyPursuitTargetService
    {
        public const string PursuitRouteLegPrefix = "__route_pursuit__:";

        public readonly struct PursuitMacroSignature : IEquatable<PursuitMacroSignature>
        {
            public readonly string TargetArmyId;
            public readonly FormalArmyState State;
            public readonly string NodeId;
            public readonly string RouteId;
            public readonly string DestNodeId;
            public readonly int TravelDirection;

            public PursuitMacroSignature(
                string targetArmyId,
                FormalArmyState state,
                string nodeId,
                string routeId,
                string destNodeId,
                int travelDirection)
            {
                TargetArmyId = targetArmyId ?? string.Empty;
                State = state;
                NodeId = nodeId ?? string.Empty;
                RouteId = routeId ?? string.Empty;
                DestNodeId = destNodeId ?? string.Empty;
                TravelDirection = travelDirection;
            }

            public bool Equals(PursuitMacroSignature other) =>
                string.Equals(TargetArmyId, other.TargetArmyId, StringComparison.Ordinal) &&
                State == other.State &&
                string.Equals(NodeId, other.NodeId, StringComparison.Ordinal) &&
                string.Equals(RouteId, other.RouteId, StringComparison.Ordinal) &&
                string.Equals(DestNodeId, other.DestNodeId, StringComparison.Ordinal) &&
                TravelDirection == other.TravelDirection;

            public override bool Equals(object obj) => obj is PursuitMacroSignature other && Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = TargetArmyId.GetHashCode();
                    hash = (hash * 397) ^ (int)State;
                    hash = (hash * 397) ^ NodeId.GetHashCode();
                    hash = (hash * 397) ^ RouteId.GetHashCode();
                    hash = (hash * 397) ^ DestNodeId.GetHashCode();
                    hash = (hash * 397) ^ TravelDirection;
                    return hash;
                }
            }
        }

        struct PursuitTickSnapshot
        {
            public float PursuerProgress;
            public float TargetProgress;
            public string RouteId;
            public bool Valid;
        }

        static readonly Dictionary<string, PursuitMacroSignature> LastMacroSignatures =
            new Dictionary<string, PursuitMacroSignature>(StringComparer.Ordinal);

        static readonly Dictionary<string, PursuitTickSnapshot> LastTickSnapshots =
            new Dictionary<string, PursuitTickSnapshot>(StringComparer.Ordinal);

        public static void ClearTracking(string pursuerArmyId)
        {
            if (string.IsNullOrEmpty(pursuerArmyId))
                return;
            LastMacroSignatures.Remove(pursuerArmyId);
            LastTickSnapshots.Remove(pursuerArmyId);
        }

        public static bool TryResolveTargetArmy(SimulationWorld world, out FormalArmy targetArmy)
        {
            targetArmy = null;
            var rt = world?.Strategic?.Encounter;
            if (rt == null || string.IsNullOrEmpty(rt.PursueDefenderArmyId))
                return false;
            return world.Strategic.FormalArmies.TryGet(rt.PursueDefenderArmyId, out targetArmy) &&
                   targetArmy != null;
        }

        public static PursuitMacroSignature BuildMacroSignature(FormalArmy army)
        {
            if (army == null)
                return default;
            return new PursuitMacroSignature(
                army.ArmyId,
                army.State,
                army.NodeId,
                army.RouteId,
                army.DestNodeId,
                ResolveTravelDirection(army));
        }

        public static bool NeedsTopologyRetarget(PursuitMacroSignature previous, PursuitMacroSignature current) =>
            !previous.Equals(current);

        public static bool TryEnsurePursuitTravel(
            SimulationWorld world,
            FormalArmy pursuer,
            FormalArmy target)
        {
            if (world == null || pursuer == null || target == null)
                return false;

            var signature = BuildMacroSignature(target);
            var hasPrevious = LastMacroSignatures.TryGetValue(pursuer.ArmyId, out var previous);

            if (pursuer.IsTraveling &&
                (!hasPrevious || !NeedsTopologyRetarget(previous, signature)) &&
                IsActivePursuitTravelValid(pursuer, target))
            {
                if (!hasPrevious)
                    LastMacroSignatures[pursuer.ArmyId] = signature;
                return true;
            }

            if (ArmyTravelCommandService.HasPendingLegs(pursuer.ArmyId))
                return ArmyTravelCommandService.TryContinueQueuedTravel(world, pursuer.ArmyId);

            var move = ArmyTravelCommandService.MoveArmyToTargetArmy(world, pursuer.ArmyId, target.ArmyId);
            if (move.IsSuccess)
                LastMacroSignatures[pursuer.ArmyId] = signature;
            return move.IsSuccess;
        }

        public static bool IsActivePursuitTravelValid(FormalArmy pursuer, FormalArmy target)
        {
            if (pursuer == null || target == null || !pursuer.IsTraveling)
                return false;

            if (IsStaticRouteTarget(target))
            {
                if (!string.Equals(pursuer.RouteId, target.RouteId, StringComparison.Ordinal))
                    return false;
                var targetProgress = target.GetRouteDisplayProgress();
                return pursuer.RouteSegmentEndProgress >= 0f &&
                       Math.Abs(pursuer.RouteSegmentEndProgress - targetProgress) <= 0.03f;
            }

            if (!string.Equals(pursuer.RouteId, target.RouteId, StringComparison.Ordinal))
                return false;

            var chaseEnd = ResolveChaseEndpoint(target);
            return pursuer.RouteSegmentEndProgress >= 0f &&
                   Math.Abs(pursuer.RouteSegmentEndProgress - chaseEnd) <= 0.03f;
        }

        public static bool IsStaticRouteTarget(FormalArmy target)
        {
            if (target == null)
                return false;
            if (target.IsRouteAnchored)
                return true;
            return !target.IsTraveling &&
                   !string.IsNullOrEmpty(target.RouteId) &&
                   target.RouteAnchorProgress >= 0f;
        }

        public static float ResolveChaseEndpoint(FormalArmy target)
        {
            if (target == null)
                return 1f;
            if (IsStaticRouteTarget(target))
                return target.GetRouteDisplayProgress();

            if (target.IsTraveling &&
                target.RouteSegmentOriginProgress >= 0f &&
                target.RouteSegmentEndProgress >= 0f)
            {
                return target.RouteSegmentEndProgress >= target.RouteSegmentOriginProgress ? 1f : 0f;
            }

            var display = target.GetRouteDisplayProgress();
            return display >= 0.5f ? 1f : 0f;
        }

        public static float ResolveTargetRouteProgressForLeg(SimulationWorld world, FormalArmy target, WorldRouteState route)
        {
            if (target == null || route == null)
                return 0f;
            if (IsStaticRouteTarget(target) &&
                string.Equals(target.RouteId, route.Id, StringComparison.Ordinal))
                return target.GetRouteDisplayProgress();

            return ResolveChaseEndpoint(target);
        }

        static int ResolveTravelDirection(FormalArmy army)
        {
            if (army == null)
                return 0;
            if (army.IsTraveling &&
                army.RouteSegmentOriginProgress >= 0f &&
                army.RouteSegmentEndProgress >= 0f)
            {
                if (army.RouteSegmentEndProgress > army.RouteSegmentOriginProgress + 0.001f)
                    return 1;
                if (army.RouteSegmentEndProgress < army.RouteSegmentOriginProgress - 0.001f)
                    return -1;
            }

            if (army.IsRouteAnchored || (!army.IsTraveling && army.RouteAnchorProgress >= 0f))
                return 0;
            return 0;
        }

        public static void CaptureTickSnapshot(FormalArmy pursuer, FormalArmy target)
        {
            if (pursuer == null || target == null || string.IsNullOrEmpty(pursuer.ArmyId))
                return;

            LastTickSnapshots[pursuer.ArmyId] = new PursuitTickSnapshot
            {
                Valid = !string.IsNullOrEmpty(pursuer.RouteId) &&
                        string.Equals(pursuer.RouteId, target.RouteId, StringComparison.Ordinal),
                RouteId = pursuer.RouteId ?? string.Empty,
                PursuerProgress = pursuer.GetRouteDisplayProgress(),
                TargetProgress = target.GetRouteDisplayProgress()
            };
        }

        public static bool TryDetectFormalArmyPursuitContact(
            FormalArmy pursuer,
            FormalArmy target,
            string pursuerArmyId)
        {
            if (pursuer == null || target == null)
                return false;

            if (TryDetectNodeContact(pursuer, target))
                return true;

            if (!TryGetSharedRouteId(pursuer, target, out _))
                return false;

            var pursuerProgress = pursuer.GetRouteDisplayProgress();
            var targetProgress = target.GetRouteDisplayProgress();
            if (Math.Abs(pursuerProgress - targetProgress) <= StrategicEngageRules.RouteProgressEpsilon)
                return true;

            if (string.IsNullOrEmpty(pursuerArmyId) ||
                !LastTickSnapshots.TryGetValue(pursuerArmyId, out var prev) ||
                !prev.Valid)
                return false;

            return DetectSweptRouteContact(prev.PursuerProgress, prev.TargetProgress, pursuerProgress, targetProgress);
        }

        public static bool DetectSweptRouteContact(
            float prevPursuer,
            float prevTarget,
            float currPursuer,
            float currTarget)
        {
            if ((prevPursuer <= prevTarget && currPursuer >= currTarget) ||
                (prevPursuer >= prevTarget && currPursuer <= currTarget))
                return true;
            return false;
        }

        static bool TryDetectNodeContact(FormalArmy pursuer, FormalArmy target)
        {
            if (pursuer.State == FormalArmyState.AtNode &&
                target.State == FormalArmyState.AtNode &&
                !string.IsNullOrEmpty(pursuer.NodeId) &&
                string.Equals(pursuer.NodeId, target.NodeId, StringComparison.Ordinal))
                return true;
            return false;
        }

        static bool TryGetSharedRouteId(FormalArmy pursuer, FormalArmy target, out string routeId)
        {
            routeId = string.Empty;
            if (pursuer == null || target == null)
                return false;
            if (string.IsNullOrEmpty(pursuer.RouteId) || string.IsNullOrEmpty(target.RouteId))
                return false;
            if (!string.Equals(pursuer.RouteId, target.RouteId, StringComparison.Ordinal))
                return false;
            routeId = pursuer.RouteId;
            return true;
        }

        public static string FormatPursuitRouteLeg(string routeId) => PursuitRouteLegPrefix + routeId;

        public static bool TryConsumePursuitRouteLeg(string legToken, out string routeId)
        {
            routeId = string.Empty;
            if (string.IsNullOrEmpty(legToken) ||
                !legToken.StartsWith(PursuitRouteLegPrefix, StringComparison.Ordinal))
                return false;
            routeId = legToken.Substring(PursuitRouteLegPrefix.Length);
            return !string.IsNullOrEmpty(routeId);
        }
    }
}
