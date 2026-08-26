using UnityEngine;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Level Tester 顶栏：当前包／地图／剧本与快捷键提示。
    /// </summary>
    public sealed class LevelTesterHud : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] bool visible = true;
        [SerializeField] KeyCode toggleKey = KeyCode.F1;

        string _mapLine = "-";

        void Awake()
        {
            if (bootstrap == null)
                bootstrap = GetComponent<PlayableHostBootstrap>() ??
                            FindObjectOfType<PlayableHostBootstrap>();
        }

        void Update()
        {
            if (Input.GetKeyDown(toggleKey))
                visible = !visible;
            RefreshMapLine();
        }

        void OnGUI()
        {
            if (!visible || bootstrap == null)
                return;

            if (bootstrap.WorldMapPanel != null && bootstrap.WorldMapPanel.IsOpen)
                return;

            var formalHud = bootstrap.GetComponent<HostFormalHud>();
            if (formalHud != null && formalHud.IsHudVisible)
                return;

            const float pad = 8f;
            var w = Mathf.Min(720f, Screen.width - 16f);
            GUI.Box(new Rect(pad, pad, w, 96f), GUIContent.none);
            var y = pad + 6f;
            GUI.Label(new Rect(pad + 8f, y, w - 16f, 20f),
                "Level Tester · 逻辑关卡试玩台（非美术场景）");
            y += 18f;
            GUI.Label(new Rect(pad + 8f, y, w - 16f, 20f),
                "包: " + Truncate(bootstrap.ResolvedContentPath, 90));
            y += 18f;
            GUI.Label(new Rect(pad + 8f, y, w - 16f, 20f),
                "地图: " + _mapLine + " ｜ 剧本: " +
                (string.IsNullOrEmpty(bootstrap.OpeningScenarioId) ? "(default)" : bootstrap.OpeningScenarioId) +
                " ｜ 名册: " +
                (string.IsNullOrEmpty(bootstrap.CharacterRosterId) ? "(用剧本spawns)" : bootstrap.CharacterRosterId));
            y += 18f;
            GUI.Label(new Rect(pad + 8f, y, w - 16f, 20f),
                "Space 暂停 · .／N 步进 · [ ] 变速 · R 重载 · F12 Background Travel DEBUG · F1 隐藏本栏 · Inspector 换地图／剧本／名册");
        }

        void RefreshMapLine()
        {
            var session = bootstrap != null ? bootstrap.Session : null;
            if (session == null || !MapLayoutPick.TryGet(session, out var layout) || layout == null)
            {
                _mapLine = string.IsNullOrEmpty(bootstrap?.PreferredMapLayoutId)
                    ? "(未选到 mapLayout)"
                    : bootstrap.PreferredMapLayoutId + " (未加载)";
                return;
            }

            _mapLine = layout.Id + " · " + layout.Width + "×" + layout.Height +
                       " · placements=" + (layout.Placements?.Count ?? 0);
        }

        static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max)
                return s ?? "";
            return "…" + s.Substring(s.Length - max + 1);
        }
    }
}
