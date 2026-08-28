using System;
using System.Collections.Generic;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// PursuitOrder 移动目标真源：FormalArmy.CurrentHex；拓扑签名不含 StepProgress。
    /// </summary>
    public static class ArmyPursuitTargetService
    {
        public readonly struct PursuitMacroSignature : IEquatable<PursuitMacroSignature>
        {
            public readonly string TargetArmyId;
            public readonly FormalArmyState State;
            public readonly int HexQ;
            public readonly int HexR;

            public PursuitMacroSignature(
                string targetArmyId,
                FormalArmyState state,
                int hexQ,
                int hexR)
            {
                TargetArmyId = targetArmyId ?? string.Empty;
                State = state;
                HexQ = hexQ;
                HexR = hexR;
            }

            public bool Equals(PursuitMacroSignature other) =>
                string.Equals(TargetArmyId, other.TargetArmyId, StringComparison.Ordinal) &&
                State == other.State &&
                HexQ == other.HexQ &&
                HexR == other.HexR;

            public override bool Equals(object obj) => obj is PursuitMacroSignature other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = TargetArmyId.GetHashCode();
                    hash = (hash * 397) ^ (int)State;
                    hash = (hash * 397) ^ HexQ;
                    hash = (hash * 397) ^ HexR;
                    return hash;
                }
            }
        }

        static readonly Dictionary<string, PursuitMacroSignature> LastMacroSignatures =
            new Dictionary<string, PursuitMacroSignature>(StringComparer.Ordinal);

        public static void ClearTracking(string pursuerArmyId)
        {
            if (string.IsNullOrEmpty(pursuerArmyId))
                return;
            LastMacroSignatures.Remove(pursuerArmyId);
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
            var hex = army.UsesHexStrategicPosition ? army.CurrentHex : default;
            return new PursuitMacroSignature(
                army.ArmyId,
                army.State,
                hex.Q,
                hex.R);
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

            if (pursuer.State == FormalArmyState.Moving &&
                (!hasPrevious || !NeedsTopologyRetarget(previous, signature)) &&
                IsActivePursuitTravelValid(pursuer, target))
            {
                if (!hasPrevious)
                    LastMacroSignatures[pursuer.ArmyId] = signature;
                return true;
            }

            if (HexStrategicRuntime.IsActive(world))
            {
                var move = ArmyHexPursuitService.BeginAttackArmy(world, pursuer.ArmyId, target.ArmyId);
                if (move.IsSuccess)
                    LastMacroSignatures[pursuer.ArmyId] = signature;
                return move.IsSuccess;
            }

            return false;
        }

        public static bool IsActivePursuitTravelValid(FormalArmy pursuer, FormalArmy target)
        {
            if (pursuer == null || target == null || pursuer.State != FormalArmyState.Moving)
                return false;
            if (!pursuer.UsesHexStrategicPosition || !target.UsesHexStrategicPosition)
                return false;
            return pursuer.DestinationHex.Equals(target.CurrentHex) ||
                   pursuer.CurrentHex == target.CurrentHex;
        }

        public static void CaptureTickSnapshot(FormalArmy pursuer, FormalArmy target)
        {
            // Hex pursuit uses contact detection only; tick snapshots are obsolete.
        }

        public static bool TryDetectFormalArmyPursuitContact(
            SimulationWorld world,
            FormalArmy pursuer,
            FormalArmy target,
            string pursuerArmyId)
        {
            if (pursuer == null || target == null)
                return false;

            if (TryDetectSameSiteContact(pursuer, target))
                return true;

            return ArmyHexBattleAnchorService.TryDetectHexContact(world, pursuer, target);
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

        static bool TryDetectSameSiteContact(FormalArmy pursuer, FormalArmy target)
        {
            if (pursuer.State != FormalArmyState.Idle || target.State != FormalArmyState.Idle)
                return false;
            if (!pursuer.UsesHexStrategicPosition || !target.UsesHexStrategicPosition)
                return false;
            return pursuer.CurrentHex == target.CurrentHex;
        }
    }
}
