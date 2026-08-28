using UnityEngine;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Level Tester 顶栏：当前包／地图／剧本与 Cheat Tools 入口。
    /// 作弊工具入口与 F10 Formal HUD 显隐解耦，HUD 显示时仍可打开。
    /// </summary>
    public sealed class LevelTesterHud : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] bool visible = true;
        [SerializeField] KeyCode toggleKey = KeyCode.F1;

        const float CheatButtonW = 88f;
        const float CheatButtonH = 24f;

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

            var worldMapOpen = bootstrap.WorldMapPanel != null && bootstrap.WorldMapPanel.IsOpen;
            var formalHud = bootstrap.GetComponent<HostFormalHud>();
            var formalHudVisible = formalHud != null && formalHud.IsHudVisible;
            var compactEntry = worldMapOpen || formalHudVisible;

            // 完整试玩台顶栏仅在正式 HUD 隐藏且非大地图时显示；入口按钮始终可点
            if (!compactEntry)
                DrawFullTesterBar();

            DrawCheatToolsEntry(compactEntry);
        }

        void DrawCheatToolsEntry(bool compactEntry)
        {
            Rect btn;
            if (compactEntry)
            {
                // Formal HUD / WorldMap 占用顶区时，入口沉底左侧，避免抢正式 HUD
                btn = new Rect(8f, Screen.height - CheatButtonH - 8f, CheatButtonW, CheatButtonH);
            }
            else
            {
                var w = Mathf.Min(760f, Screen.width - 16f);
                btn = new Rect(8f + w - 108f, 8f + 6f + 18f * 3f - 2f, 100f, 22f);
            }

            HostUiHitTest.Block(btn);
            var cheat = bootstrap.LevelTesterCheatPanel ??
                        bootstrap.GetComponent<HostLevelTesterCheatPanel>();
            var label = cheat != null && cheat.IsVisible ? "关闭作弊" : "作弊工具";
            if (GUI.Button(btn, label) && cheat != null)
                cheat.ToggleVisible();
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
            GUI.Label(new Rect(pad + 8f, y, w - 220f, 20f),
                "Space 暂停 ｜ FormalHud 顶栏变速 ｜ ` 打开作弊工具 ｜ F1 隐藏本栏 ｜ F10 显隐正式 HUD");
            // 同帧按钮已由 DrawCheatToolsEntry 绘制在栏内位置
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
