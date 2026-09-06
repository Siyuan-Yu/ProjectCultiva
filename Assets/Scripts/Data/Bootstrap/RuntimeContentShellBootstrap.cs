using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Data.Content;

namespace XianXia.Data.Bootstrap
{
    /// <summary>
    /// Snapshot Restore 后恢复静态内容定义壳。
    /// 只说明物品、功法、战技、境界、工作区与职业“是什么”；绝不重放开局、角色进度或世界状态。
    /// </summary>
    public static class RuntimeContentShellBootstrap
    {
        public static Result Rehydrate(SimulationWorld world, DefinitionRegistry registry)
        {
            if (world == null || registry == null)
                return Result.Failure(ErrorCode.InvalidArgument, "RuntimeContentShellBootstrap args null.");

            ContentRuntimeBootstrap.RehydrateInventoryCatalog(world, registry);
            ContentRuntimeBootstrap.RehydrateConstructionCatalog(world, registry);

            // Snapshot 内的 ManualSnapshotDto 只是旧兼容的最小定义；内容定义必须覆盖同 ID。
            world.ClearManuals();
            var manuals = PlayableDayBootstrap.RegisterManuals(world, registry);
            if (manuals.IsFailure)
                return manuals;

            world.ClearCombatArts();
            var arts = PlayableDayBootstrap.RegisterCombatArts(world, registry);
            if (arts.IsFailure)
                return arts;

            var ladder = PlayableDayBootstrap.RegisterRealmLadder(world, registry);
            if (ladder.IsFailure)
                return ladder;

            // Register 使用现有内容 mapper，并刷新 ControlCore / WorkArea 的 session 定义。
            var jobs = JobRuntimeBootstrap.Register(world, registry);
            if (jobs.IsFailure)
                return jobs;

            // Content 的 stack/tag 生效后仅在不会溢出时重排，数量绝不因恢复被截断。
            world.Inventory.TryOrganizeWithoutLoss();
            return Result.Success();
        }
    }
}
