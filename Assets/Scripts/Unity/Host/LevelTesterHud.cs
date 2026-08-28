using UnityEngine;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Level Tester 顶栏：当前包／地图／剧本。作弊入口在 FormalHud 暂停/变速旁；本组件仅作 F10 隐藏 HUD 时的兜底。
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
            if (bootstrap == null)
                return;

            var worldMapOpen = bootstrap.WorldMapPanel != null && bootstrap.WorldMapPanel.IsOpen;
            var formalHud = bootstrap.GetComponent<HostFormalHud>();
            var formalTopBarActive = formalHud != null && formalHud.IsHudVisible && !worldMapOpen;

            if (!formalTopBarActive && !worldMapOpen)
                HostLevelTesterCheatPanel.DrawTopBarEntryButton(bootstrap);

            if (!visible)
                return;

            if (worldMapOpen)
                return;

            if (formalHud != null && formalHud.IsHudVisible)
                return;

            DrawFullTesterBar();
        }

        void DrawFullTesterBar()
        {
            const float pad = 8f;
            var w = Mathf.Min(760f, Screen.width - 16f);
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
            GUI.Label(new Rect(pad + 8f, y, w - 120f, 20f),
                "Space 暂停 ｜ FormalHud 顶栏变速旁「作弊工具」 ｜ ` 快捷键 ｜ F1 隐藏本栏 ｜ F10 显隐正式 HUD");
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
