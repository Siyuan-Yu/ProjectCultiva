using System;
using XianXia.Core.Npc;
using XianXia.Core.Results;
using XianXia.Core.Settlement;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Phase H：CaptureObjective 泛化 + WorldSite Owner 易主 + SettlementAuthority 同步。</summary>
    public static class CaptureObjectiveService
    {
        public static void RegisterControlCore(SimulationWorld world, ControlCoreState core, string siteId)
        {
            if (world?.Strategic?.CaptureObjectives == null || core == null)
                return;

            var objective = new CaptureObjectiveState
            {
                ObjectiveId = "capture:" + core.WorkAreaId,
                WorkAreaId = core.WorkAreaId ?? string.Empty,
                SiteId = siteId ?? string.Empty,
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

            var siteOwner = ResolveSiteOwnerForCore(world, core);
            if (!string.IsNullOrEmpty(siteOwner) &&
                !WarGateService.CanMilitaryCapture(world, attackerFactionId, siteOwner))
            {
                return Result.Failure(
                    ErrorCode.InvalidOperation,
                    "Military capture requires active war.",
                    attackerFactionId + "->" + siteOwner);
            }

            return Result.Success();
        }

        public static Result TryCompleteWorldSiteCapture(
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

            var siteOwner = ResolveSiteOwnerForCore(world, core);
            if (!string.IsNullOrEmpty(siteOwner) &&
                !WarGateService.CanMilitaryCapture(world, attackerFactionId, siteOwner))
            {
                return Result.Failure(
                    ErrorCode.InvalidOperation,
                    "Military capture requires active war.",
                    attackerFactionId + "->" + siteOwner);
            }

            objective.Completed = true;
            objective.CurrentHp = 0;
            core.PlayerControlled = true;

            if (!string.IsNullOrEmpty(objective.SiteId))
                WorldSiteOwnershipService.SetOwner(world, objective.SiteId, attackerFactionId);

            world.SettlementAuthority.GrantAll(core.GrantsPrivileges);
            world.Flags.Set("settlement_player_controlled");
            world.Flags.Set("control_core_owned:" + workAreaId);
            world.Flags.Clear("control_core_capture_available");

            if (!string.IsNullOrEmpty(objective.SiteId) &&
                world.Strategic.CaptureObjectives.AllCompletedForSite(objective.SiteId))
            {
                world.Flags.Set("site_captured:" + objective.SiteId);
                ScenarioProgressionHooks.NotifyAllCaptureObjectivesCompletedForSite(
                    world,
                    objective.SiteId);
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

        static string ResolveSiteOwnerForCore(SimulationWorld world, ControlCoreState core)
        {
            if (world == null || core == null)
                return string.Empty;

            if (world.Strategic?.CaptureObjectives != null &&
                world.Strategic.CaptureObjectives.TryGet("capture:" + core.WorkAreaId, out var objective) &&
                objective != null &&
                !string.IsNullOrEmpty(objective.SiteId))
                return WorldSiteOwnershipService.GetOwner(world, objective.SiteId);

            var partySiteId = world.PartyWorld?.SiteId;
            if (!string.IsNullOrEmpty(partySiteId) &&
                world.Strategic?.Sites != null &&
                world.Strategic.Sites.TryGet(partySiteId, out var partySite) &&
                partySite != null &&
                string.Equals(partySite.LocalMapId, core.LocationId, StringComparison.Ordinal))
                return partySite.OwnerFactionId ?? string.Empty;

            return string.Empty;
        }
    }
}
