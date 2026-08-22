using System;
using XianXia.Core.Npc;
using XianXia.Core.Results;
using XianXia.Core.Settlement;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Phase H：CaptureObjective 泛化 + Node Owner 易主 + SettlementAuthority 同步。</summary>
    public static class CaptureObjectiveService
    {
        public static void RegisterControlCore(SimulationWorld world, ControlCoreState core, string nodeId)
        {
            if (world?.Strategic?.CaptureObjectives == null || core == null)
                return;

            var objective = new CaptureObjectiveState
            {
                ObjectiveId = "capture:" + core.WorkAreaId,
                WorkAreaId = core.WorkAreaId ?? string.Empty,
                NodeId = nodeId ?? string.Empty,
                CurrentHp = Math.Max(0, core.CurrentDurability),
                MaxHp = Math.Max(1, core.MaxDurability),
                OccupyHoldSeconds = Math.Max(0.1f, core.OccupyHoldSeconds),
                Completed = core.PlayerControlled
            };
            world.Strategic.CaptureObjectives.Register(objective);
        }

        public static Result TryBeginMilitaryAssault(
            SimulationWorld world,
            string attackerFactionId,
            string workAreaId)
        {
            if (world == null || string.IsNullOrEmpty(workAreaId))
                return Result.Failure(ErrorCode.InvalidArgument, "Invalid assault request.");
            if (!world.ControlCores.TryGet(workAreaId, out var core))
                return Result.Failure(ErrorCode.NotFound, "Control core not found.", workAreaId);

            var nodeOwner = ResolveNodeOwnerForCore(world, core);
            if (!string.IsNullOrEmpty(nodeOwner) &&
                !WarGateService.CanMilitaryCapture(world, attackerFactionId, nodeOwner))
            {
                return Result.Failure(
                    ErrorCode.InvalidOperation,
                    "Military capture requires active war.",
                    attackerFactionId + "->" + nodeOwner);
            }

            return Result.Success();
        }

        public static Result TryCompleteNodeCapture(
            SimulationWorld world,
            string attackerFactionId,
            string workAreaId)
        {
            if (world == null || string.IsNullOrEmpty(workAreaId))
                return Result.Failure(ErrorCode.InvalidArgument, "Invalid capture request.");
            if (!world.ControlCores.TryGet(workAreaId, out var core))
                return Result.Failure(ErrorCode.NotFound, "Control core not found.", workAreaId);
            if (!world.Strategic.CaptureObjectives.TryGet("capture:" + workAreaId, out var objective) ||
                objective == null)
                return Result.Failure(ErrorCode.NotFound, "Capture objective missing.", workAreaId);

            var nodeOwner = ResolveNodeOwnerForCore(world, core);
            if (!string.IsNullOrEmpty(nodeOwner) &&
                !WarGateService.CanMilitaryCapture(world, attackerFactionId, nodeOwner))
            {
                return Result.Failure(
                    ErrorCode.InvalidOperation,
                    "Military capture requires active war.",
                    attackerFactionId + "->" + nodeOwner);
            }

            objective.Completed = true;
            objective.CurrentHp = 0;
            core.PlayerControlled = true;

            if (!string.IsNullOrEmpty(objective.NodeId) &&
                world.WorldGraph.TryGetNode(objective.NodeId, out var node) &&
                node != null)
            {
                node.OwnerId = attackerFactionId;
            }

            world.SettlementAuthority.GrantAll(core.GrantsPrivileges);
            world.Flags.Set("settlement_player_controlled");
            world.Flags.Set("control_core_owned:" + workAreaId);
            world.Flags.Clear("control_core_capture_available");

            if (!string.IsNullOrEmpty(objective.NodeId) &&
                world.Strategic.CaptureObjectives.AllCompletedForNode(objective.NodeId))
            {
                world.Flags.Set("node_captured:" + objective.NodeId);
                ScenarioProgressionHooks.NotifyAllCaptureObjectivesCompletedForNode(
                    world,
                    objective.NodeId);
            }

            return Result.Success();
        }

        public static void SyncObjectiveFromControlCore(SimulationWorld world, ControlCoreState core)
        {
            if (world?.Strategic?.CaptureObjectives == null || core == null)
                return;
            if (!world.Strategic.CaptureObjectives.TryGet("capture:" + core.WorkAreaId, out var objective) ||
                objective == null)
                return;

            objective.CurrentHp = Math.Max(0, core.CurrentDurability);
            objective.MaxHp = Math.Max(1, core.MaxDurability);
            objective.OccupyProgressSeconds = core.OccupyProgressSeconds;
            objective.OccupyHoldSeconds = Math.Max(0.1f, core.OccupyHoldSeconds);
            if (core.PlayerControlled)
                objective.Completed = true;
        }

        static string ResolveNodeOwnerForCore(SimulationWorld world, ControlCoreState core)
        {
            if (world == null || core == null)
                return string.Empty;

            if (world.Strategic?.CaptureObjectives != null &&
                world.Strategic.CaptureObjectives.TryGet("capture:" + core.WorkAreaId, out var objective) &&
                objective != null &&
                !string.IsNullOrEmpty(objective.NodeId) &&
                world.WorldGraph.TryGetNode(objective.NodeId, out var ownedNode) &&
                ownedNode != null &&
                !string.IsNullOrEmpty(ownedNode.OwnerId))
                return ownedNode.OwnerId;

            foreach (var kv in world.WorldGraph.Nodes)
            {
                var nodeState = kv.Value;
                if (nodeState == null || string.IsNullOrEmpty(nodeState.LocalMapId))
                    continue;
                if (!string.Equals(nodeState.LocalMapId, core.LocationId, StringComparison.Ordinal))
                    continue;
                return nodeState.OwnerId ?? string.Empty;
            }

            return string.Empty;
        }
    }
}
