using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.Combat;
using XianXia.Core.Social;
using XianXia.Core.World;

using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Phase J：Captured / Escaped / RetreatingArmy 最小衔接（概率占位）。</summary>
    public static class BattleAftermathService
    {
        /// <summary>测试／调试：强制 Captured 概率（0..1）；生产公式 DEFER。</summary>
        public static double TestCaptureChancePlaceholder { get; set; } = 1.0;

        public static Result TryAssignCaptured(
            SimulationWorld world,
            EntityId characterId,
            string captorFactionId)
        {
            if (world == null || characterId.IsNone)
                return Result.Failure(ErrorCode.InvalidArgument, "Invalid capture target.");
            if (!world.Entities.TryGet(characterId, out var entity) || entity == null)
                return Result.Failure(ErrorCode.EntityNotFound, "Character missing.");

            if (!CombatLifeStateService.TryEnterCaptured(world, entity, captorFactionId))
                return Result.Failure(ErrorCode.InvalidOperation, "Cannot enter Captured state.");

            return Result.Success();
        }

        public static Result TryAssignEscapedAndRetreat(
            SimulationWorld world,
            string sourceArmyId,
            IReadOnlyList<EntityId> escapedMembers,
            string nodeId)
        {
            HexCoord? hex = null;
            if (!string.IsNullOrEmpty(sourceArmyId) &&
                world?.Strategic?.FormalArmies != null &&
                world.Strategic.FormalArmies.TryGet(sourceArmyId, out var sourceArmy) &&
                sourceArmy != null &&
                sourceArmy.UsesHexStrategicPosition)
                hex = sourceArmy.CurrentHex;
            else if (ArmyHexBattleAnchorService.IsHexAnchorMode(world) &&
                     ArmyHexBattleAnchorService.TryResolveHexForNode(world, nodeId, out var nodeHex))
                hex = nodeHex;

            return TryAssignEscapedAndRetreat(world, sourceArmyId, escapedMembers, nodeId, hex);
        }

        public static Result TryAssignEscapedAndRetreat(
            SimulationWorld world,
            string sourceArmyId,
            IReadOnlyList<EntityId> escapedMembers,
            string nodeId,
            HexCoord? hex)
        {
            if (world?.Strategic?.RetreatingArmies == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld incomplete.");
            if (escapedMembers == null || escapedMembers.Count == 0)
                return Result.Failure(ErrorCode.InvalidArgument, "No escaped members.");

            FormalArmy sourceArmy = null;
            if (!string.IsNullOrEmpty(sourceArmyId))
                world.Strategic.FormalArmies.TryGet(sourceArmyId, out sourceArmy);

            var retreat = new RetreatingArmy
            {
                RetreatingArmyId = world.Strategic.RetreatingArmies.AllocateId(),
                SourceArmyId = sourceArmyId ?? string.Empty,
                FactionId = sourceArmy?.FactionId ?? string.Empty,
                NodeId = nodeId ?? string.Empty
            };
            if (hex.HasValue)
            {
                retreat.HexQ = hex.Value.Q;
                retreat.HexR = hex.Value.R;
            }
            retreat.SetMembers(escapedMembers);
            world.Strategic.RetreatingArmies.Register(retreat);
            return Result.Success();
        }

        public static bool ShouldCaptureByPlaceholderPolicy(SimulationWorld world, EntityId id)
        {
            if (world == null || id.IsNone)
                return false;
            return world.Random.NextDouble() <= TestCaptureChancePlaceholder;
        }
    }

    /// <summary>Phase J：Landless Faction 最小 hook（失土仍存活）。</summary>
    public static class LandlessFactionService
    {
        public static bool IsLandless(SimulationWorld world, string factionId)
        {
            if (world?.WorldGraph == null || string.IsNullOrEmpty(factionId))
                return false;
            foreach (var kv in world.WorldGraph.Nodes)
            {
                var node = kv.Value;
                if (node == null || string.IsNullOrEmpty(node.OwnerId))
                    continue;
                if (string.Equals(node.OwnerId, factionId, StringComparison.Ordinal))
                    return false;
            }

            return HasLivingFactionCharacter(world, factionId) ||
                   HasFormalArmy(world, factionId);
        }

        static bool HasLivingFactionCharacter(SimulationWorld world, string factionId)
        {
            foreach (var entity in world.Entities.All)
            {
                if (entity == null || !entity.TryGet<FactionMembershipComponent>(out var mem))
                    continue;
                if (!string.Equals(mem.FactionId, factionId, StringComparison.Ordinal))
                    continue;
                if (entity.TryGet<LifecycleComponent>(out var life) &&
                    (life.IsDead || life.IsRemoved))
                    continue;
                return true;
            }

            return false;
        }

        static bool HasFormalArmy(SimulationWorld world, string factionId)
        {
            foreach (var kv in world.Strategic.FormalArmies.Armies)
            {
                if (kv.Value != null &&
                    string.Equals(kv.Value.FactionId, factionId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}
