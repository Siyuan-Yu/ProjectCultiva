using XianXia.Core.Construction;
using XianXia.Core.Results;

namespace XianXia.Unity.Host
{
    /// <summary>Thin Host dispatcher from construction UI to placement presenters.</summary>
    public sealed class HostConstructionController : UnityEngine.MonoBehaviour
    {
        PlayableHostBootstrap _bootstrap;

        public void Bind(PlayableHostBootstrap host) => _bootstrap = host;

        public Result BeginPlacement(string buildingId)
        {
            var world = _bootstrap?.Session?.World;
            if (world == null || !world.ConstructionCatalog.TryGet(buildingId, out var spec) || spec == null)
                return Result.Failure(ErrorCode.NotFound, "建筑定义不存在。", buildingId);
            if (!spec.UnlockedByDefault)
                return Result.Failure(ErrorCode.InvalidOperation, "此建筑尚未解锁。");
            if (!ConstructionService.HasRequiredMaterials(world, spec, out _))
                return Result.Failure(ErrorCode.InvalidOperation, "建造材料不足。");

            switch (spec.PlacementKind)
            {
                case ConstructionPlacementKind.FactionFlag:
                    var presenter = _bootstrap.GetComponent<HostFactionFlagPresenter>();
                    if (presenter == null)
                        return Result.Failure(ErrorCode.InvalidOperation, "势力控制建筑放置器未就绪。");
                    return presenter.BeginConstructionPlacement(buildingId);
                default:
                    return Result.Failure(ErrorCode.InvalidOperation, "未知建筑放置类型。", buildingId);
            }
        }
    }
}
