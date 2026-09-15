using UnityEngine;
using XianXia.Core.Results;

namespace XianXia.Unity.Host
{
    /// <summary>恢复处的正式建造入口；预览几何由统一室外 footprint presenter 执行。</summary>
    public sealed class HostRecoverySpotConstructionPresenter : MonoBehaviour
    {
        public Result BeginConstructionPlacement(string buildingId)
        {
            var shared = GetComponent<HostFarmFieldConstructionPresenter>();
            return shared != null
                ? shared.BeginRecoveryPlacement(buildingId)
                : Result.Failure(ErrorCode.InvalidOperation, "室外建造预览器未就绪。");
        }

        public void CancelPlacement() => GetComponent<HostFarmFieldConstructionPresenter>()?.CancelPlacement();
    }
}
