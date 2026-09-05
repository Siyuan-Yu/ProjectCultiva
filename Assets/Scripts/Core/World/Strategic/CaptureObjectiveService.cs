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

            // WorkArea 注册可能早于 WorldRegion；此时允许暂时无 Site，后续重绑与行动入口会懒解析。
            if (string.IsNullOrEmpty(siteId))
                TryResolveControlCoreSite(world, core, out siteId);

            var objectiveId = "capture:" + core.WorkAreaId;
            var hasRestoredObjective = world.Strategic.CaptureObjectives.TryGet(objectiveId, out var existingObjective) &&
                                       existingObjective != null;
            if (hasRestoredObjective)
            {
                existingObjective.SiteId = string.IsNullOrEmpty(siteId)
                    ? existingObjective.SiteId ?? string.Empty
                    : siteId;
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

            var siteOwner = ResolveSiteOwnerForCore(world, core);
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

            if (!TryResolveControlCoreSite(world, core, out var resolvedSiteId))
                return Result.Failure(ErrorCode.NotFound, "Control core WorldSite unresolved.", workAreaId);
            objective.SiteId = resolvedSiteId;

            var siteOwner = ResolveSiteOwnerForCore(world, core);
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
                world, objective.SiteId, attackerFactionId);
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

            world.Flags.Set("site_captured:" + objective.SiteId);
            ScenarioProgressionHooks.NotifyWorldSiteCaptured(
                world, objective.SiteId, oldOwnerFactionId, attackerFactionId, workAreaId);

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
        /// 将 ControlCore 的工作地点解析回正式 WorldSite。
        /// 优先保留已经验证存在的 Objective SiteId；否则经 WorldRegion Location 的 LocalMapId 匹配 Site。
        /// 成功时回填 CaptureObjective，避免 LocalMap session 壳丢失政治目标身份。
        /// </summary>
        public static bool TryResolveControlCoreSite(
            SimulationWorld world,
            ControlCoreState core,
            out string siteId)
        {
            siteId = string.Empty;
            if (world?.Strategic?.Sites == null || core == null)
                return false;

            if (world.Strategic.CaptureObjectives != null &&
                world.Strategic.CaptureObjectives.TryGet("capture:" + core.WorkAreaId, out var objective) &&
                objective != null &&
                !string.IsNullOrEmpty(objective.SiteId) &&
                world.Strategic.Sites.TryGet(objective.SiteId, out var objectiveSite) &&
                objectiveSite != null)
            {
                siteId = objectiveSite.SiteId;
                return true;
            }

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
                if (world.Strategic.CaptureObjectives.TryGet("capture:" + core.WorkAreaId, out objective) &&
                    objective != null)
                    objective.SiteId = siteId;
                return true;
            }

            return false;
        }

        /// <summary>WorldRegion 切换完成后重绑已注册 ControlCore；未命中的核心继续由行动入口懒解析。</summary>
        public static void RebindControlCoreSites(SimulationWorld world)
        {
            if (world?.ControlCores == null)
                return;

            foreach (var pair in world.ControlCores.All)
                TryResolveControlCoreSite(world, pair.Value, out _);
            SettlementAuthoritySync.Rebuild(world);
        }

        static string ResolveSiteOwnerForCore(SimulationWorld world, ControlCoreState core)
        {
            if (world == null || core == null)
                return string.Empty;

            if (TryResolveControlCoreSite(world, core, out var siteId))
                return WorldSiteOwnershipService.GetOwner(world, siteId);

            return string.Empty;
        }

        public static bool TryResolveCurrentOwner(
            SimulationWorld world,
            ControlCoreState core,
            out string siteId,
            out string ownerFactionId)
        {
            ownerFactionId = string.Empty;
            if (!TryResolveControlCoreSite(world, core, out siteId))
                return false;
            ownerFactionId = WorldSiteOwnershipService.GetOwner(world, siteId);
            return true;
        }
    }
}
