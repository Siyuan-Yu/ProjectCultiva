using UnityEngine;
using XianXia.Core.Domain.Ids;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// 旧关系面板兼容入口。正式 Gameplay 已统一转入 HostCharacterSheetPanel.Social，
    /// 本组件不再持有窗口、暂停或绘制逻辑。
    /// </summary>
    public sealed class HostRelationPanel : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;

        public bool IsOpen => false;

        public void Bind(PlayableHostBootstrap host) => bootstrap = host;

        public void ClearSessionState() { }

        public void OpenFor(EntityId id)
        {
            if (!id.IsNone)
                bootstrap?.CharacterSheetPanel?.OpenFor(id, CharacterProfilePage.Social);
        }

        public void Close() { }
    }
}
