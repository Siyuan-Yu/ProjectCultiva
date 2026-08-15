using UnityEngine;
using XianXia.Core.Domain.Ids;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// 保留组件以便场景／Bootstrap 绑定；人物／境界／关系入口已改到底栏状态板右侧（HostFormalHud）。
    /// </summary>
    public sealed class HostSelectedUnitChrome : MonoBehaviour
    {
        public void Bind(
            PlayableHostBootstrap host,
            HostSelectionController selection,
            HostCultivationPanel cultivation,
            HostCharacterSheetPanel characterSheet,
            HostRelationPanel relation,
            Camera camera)
        {
            // no-op：入口在 FormalHud 侧栏
        }
    }
}
