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

            var objectiveId = "capture:" + core.WorkAreaId;
            var hasRestoredObjective = world.Strategic.CaptureObjectives.TryGet(objectiveId, out var existingObjective) &&
                                       existingObjective != null;
            if (hasRestoredObjective)
            {
                if (!string.IsNullOrEmpty(siteId))
                    world.Strategic.CaptureObjectives.BindSite(objectiveId, siteId);
                // 旧 Completed=true 表示一次性模型曾占领过；Owner 只以 Strategic Site Snapshot 为准。
                // 迁移后立即成为完好的可重复争夺建筑，且本次运行不再传播 Completed。
                if (existingObjective.Completed)
                {
                    core.CurrentDurability = Math.Max(1, core.MaxDurability);
                    core.CaptureAvailable = false;
                    core.OccupyProgressSeconds = 0f;
                    existingObjective.Completed = false;
                }
                else
                {
                    core.CurrentDurability = Math.Min(
                        Math.Max(1, core.MaxDurability),
                        Math.Max(0, existingObjective.CurrentHp));
                    core.CaptureAvailable = core.CurrentDurability <= 0;
                    core.OccupyProgressSeconds = Math.Min(
                        Math.Max(0f, existingObjective.OccupyProgressSeconds),
                        Math.Max(0.1f, core.OccupyHoldSeconds));
                }
                existingObjective.CurrentHp = Math.Max(0, core.CurrentDurability);
                existingObjective.MaxHp = Math.Max(1, core.MaxDurability);
                existingObjective.OccupyHoldSeconds = Math.Max(0.1f, core.OccupyHoldSeconds);
                existingObjective.OccupyProgressSeconds = core.OccupyProgressSeconds;
                SettlementAuthoritySync.Rebuild(world);
                return;
            }

            var objective = new CaptureObjectiveState
            {
                ObjectiveId = objectiveId,
                WorkAreaId = core.WorkAreaId ?? string.Empty,
                SiteId = siteId ?? string.Empty,
                CurrentHp = Math.Max(0, core.CurrentDurability),
                MaxHp = Math.Max(1, core.MaxDurability),
                OccupyHoldSeconds = Math.Max(0.1f, core.OccupyHoldSeconds),
                Completed = false
            };
            world.Strategic.CaptureObjectives.Register(objective);
            SettlementAuthoritySync.Rebuild(world);
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

            if (!TryGetBoundSiteForControlCore(world, workAreaId, out var boundSite))
                return Result.Failure(ErrorCode.NotFound, "Control core canonical WorldSite binding missing.", workAreaId);
            var siteOwner = boundSite.OwnerFactionId ?? string.Empty;
            if (!string.IsNullOrEmpty(siteOwner) &&
                string.Equals(attackerFactionId, siteOwner, StringComparison.Ordinal))
                return Result.Failure(ErrorCode.InvalidOperation, "Already controlled by attacker faction.");
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

            if (!TryGetBoundSiteForControlCore(world, workAreaId, out var targetSite))
                return Result.Failure(ErrorCode.NotFound, "Control core canonical WorldSite binding missing.", workAreaId);
            var resolvedSiteId = targetSite.SiteId;

            if (targetSite.CoreIsRemovable)
                return Result.Failure(ErrorCode.InvalidOperation, "可拆核心只能摧毁，不能占领。");

            var siteOwner = targetSite.OwnerFactionId ?? string.Empty;
            if (!string.IsNullOrEmpty(siteOwner) &&
                string.Equals(attackerFactionId, siteOwner, StringComparison.Ordinal))
                return Result.Failure(ErrorCode.InvalidOperation, "Already controlled by attacker faction.");
            if (!string.IsNullOrEmpty(siteOwner) &&
                !WarGateService.CanMilitaryCapture(world, attackerFactionId, siteOwner))
            {
                return Result.Failure(
                    ErrorCode.InvalidOperation,
                    "Military capture requires active war.",
                    attackerFactionId + "->" + siteOwner);
            }

            if (!world.ControlCores.TryCapture(workAreaId, out _))
                return Result.Failure(ErrorCode.InvalidOperation, "Occupy hold not finished.");

            // Transfer 是唯一政治写入；它失败前绝不改变 Core／Objective 的物理状态。
            var oldOwnerFactionId = siteOwner;
            var transfer = WorldSiteTerritoryTransferService.Transfer(
                world, resolvedSiteId, attackerFactionId);
            if (transfer.IsFailure)
                return transfer;

            world.ControlCores.ResetAfterCapture(
                workAreaId,
                string.Equals(attackerFactionId, world.Strategic.PlayerFactionId, StringComparison.Ordinal),
                out core);
            objective.CurrentHp = core.CurrentDurability;
            objective.MaxHp = core.MaxDurability;
            objective.OccupyProgressSeconds = 0f;
            objective.OccupyHoldSeconds = core.OccupyHoldSeconds;
            objective.Completed = false;
            SettlementAuthoritySync.Rebuild(world);
            world.Flags.Clear("control_core_capture_available");

            world.Flags.Set("site_captured:" + resolvedSiteId);
            ScenarioProgressionHooks.NotifyWorldSiteCaptured(
                world, resolvedSiteId, oldOwnerFactionId, attackerFactionId, workAreaId);
            CharacterEncounterService.NotifyStrategicObjectiveResolved(world, targetSite.SiteId, targetSite.CoreAssetId);

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
            objective.Completed = false;
        }

        /// <summary>
        /// 仅供旧存档／旧地图迁移使用的 LocalMap 猜测。正常运行时不得调用。
        /// </summary>
        public static bool TryResolveControlCoreSiteLegacyCompatibilityFallback(
            SimulationWorld world,
            ControlCoreState core,
            out string siteId)
        {
            siteId = string.Empty;
            if (world?.Strategic?.Sites == null || core == null)
                return false;

            if (string.IsNullOrEmpty(core.LocationId) ||
                world.WorldRegion == null ||
                !world.WorldRegion.TryGet(core.LocationId, out var location) ||
                location == null)
                return false;

            var mapLayoutId = !string.IsNullOrEmpty(location.LocalMapId)
                ? location.LocalMapId
                : world.WorldRegion.ActiveMapLayoutId;
            if (string.IsNullOrEmpty(mapLayoutId))
                return false;

            foreach (var pair in world.Strategic.Sites.Sites)
            {
                var site = pair.Value;
                if (site == null ||
                    !string.Equals(site.LocalMapId, mapLayoutId, StringComparison.Ordinal))
                    continue;

                siteId = site.SiteId;
                return true;
            }

            return false;
        }

        public static Result ValidateControlCoreWorldSiteBinding(
            SimulationWorld world, string workAreaId, string siteId)
        {
            if (world?.Strategic?.CaptureObjectives == null || string.IsNullOrWhiteSpace(workAreaId) ||
                string.IsNullOrWhiteSpace(siteId))
                return Result.Failure(ErrorCode.InvalidArgument, "ControlCore binding requires WorkAreaId and SiteId.");
            if (!world.ControlCores.TryGet(workAreaId, out var core) || core == null)
                return Result.Failure(ErrorCode.NotFound, "ControlCore binding target is missing.", workAreaId);
            if (!world.Strategic.Sites.TryGet(siteId, out var site) || site == null)
                return Result.Failure(ErrorCode.NotFound, "ControlCore binding WorldSite is missing.", siteId);
            if (site.CoreIsRemovable)
                return Result.Failure(ErrorCode.InvalidOperation, "Removable WorldSite cannot bind a fixed ControlCore.", siteId);
            var objectiveId = "capture:" + workAreaId;
            if (!world.Strategic.CaptureObjectives.TryGet(objectiveId, out var objective) || objective == null ||
                !string.Equals(objective.WorkAreaId, workAreaId, StringComparison.Ordinal))
                return Result.Failure(ErrorCode.NotFound, "ControlCore capture objective is missing.", workAreaId);
            if (!string.IsNullOrEmpty(objective.SiteId) &&
                !string.Equals(objective.SiteId, siteId, StringComparison.Ordinal))
                return Result.Failure(ErrorCode.InvalidOperation, "ControlCore is already bound to another WorldSite.",
                    workAreaId + " -> " + objective.SiteId);
            var ids = world.Strategic.CaptureObjectives.GetObjectiveIdsForSite(siteId);
            for (var i = 0; i < ids.Count; i++)
                if (!string.Equals(ids[i], objectiveId, StringComparison.Ordinal))
                    return Result.Failure(ErrorCode.InvalidOperation, "Fixed WorldSite already has another ControlCore.",
                        siteId + " -> " + ids[i]);
            return Result.Success();
        }

        public static Result BindControlCoreToWorldSite(
            SimulationWorld world, string workAreaId, string siteId)
        {
            var validation = ValidateControlCoreWorldSiteBinding(world, workAreaId, siteId);
            if (validation.IsFailure) return validation;
            if (!world.Strategic.CaptureObjectives.BindSite("capture:" + workAreaId, siteId))
                return Result.Failure(ErrorCode.InvalidOperation, "ControlCore binding transaction failed.", workAreaId);
            SettlementAuthoritySync.Rebuild(world);
            return Result.Success();
        }

        public static bool TryGetBoundSiteForControlCore(
            SimulationWorld world, string workAreaId, out WorldSite site)
        {
            site = null;
            if (world?.Strategic?.CaptureObjectives == null || string.IsNullOrEmpty(workAreaId) ||
                !world.ControlCores.TryGet(workAreaId, out _) ||
                !world.Strategic.CaptureObjectives.TryGet("capture:" + workAreaId, out var objective) || objective == null ||
                !string.Equals(objective.WorkAreaId, workAreaId, StringComparison.Ordinal) ||
                string.IsNullOrEmpty(objective.SiteId) ||
                !world.Strategic.Sites.TryGet(objective.SiteId, out site) || site == null || site.CoreIsRemovable)
            { site = null; return false; }
            return true;
        }

        public static bool TryGetBoundControlCoreForSite(
            SimulationWorld world, string siteId, out ControlCoreState core)
        {
            core = null;
            if (world?.Strategic?.CaptureObjectives == null || string.IsNullOrEmpty(siteId) ||
                !world.Strategic.Sites.TryGet(siteId, out var site) || site == null || site.CoreIsRemovable)
                return false;
            var ids = world.Strategic.CaptureObjectives.GetObjectiveIdsForSite(siteId);
            if (ids.Count != 1 || !world.Strategic.CaptureObjectives.TryGet(ids[0], out var objective) ||
                objective == null || !string.Equals(objective.SiteId, siteId, StringComparison.Ordinal) ||
                !world.ControlCores.TryGet(objective.WorkAreaId, out core) || core == null)
            { core = null; return false; }
            return true;
        }

        /// <summary>静态壳或政治 overlay 后刷新派生权限；不再从当前 WorldRegion 猜 Site。</summary>
        public static void RebindControlCoreSites(SimulationWorld world)
        {
            if (world?.ControlCores == null)
                return;

            SettlementAuthoritySync.Rebuild(world);
        }

        public static bool TryResolveCurrentOwner(
            SimulationWorld world,
            ControlCoreState core,
            out string siteId,
            out string ownerFactionId)
        {
            ownerFactionId = string.Empty;
            if (!TryGetBoundSiteForControlCore(world, core.WorkAreaId, out var site))
            { siteId = string.Empty;
                return false;
            }
            siteId = site.SiteId;
            ownerFactionId = WorldSiteOwnershipService.GetOwner(world, siteId);
            return true;
        }
    }
}
