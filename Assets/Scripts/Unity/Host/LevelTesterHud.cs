using UnityEngine;
using XianXia.Core.Domain.Ids;

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
        bool _hasMapLineInput;
        bool _mapLineLoaded;
        DefinitionId _mapLineId;
        int _mapLineWidth;
        int _mapLineHeight;
        int _mapLinePlacementCount;
        string _mapLinePreferredId;

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
            if (!HostPlayerPaths.DevelopmentToolsAllowed) return;
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
                var preferred = bootstrap?.PreferredMapLayoutId;
                if (_hasMapLineInput && !_mapLineLoaded && _mapLinePreferredId == preferred)
                    return;
                _hasMapLineInput = true;
                _mapLineLoaded = false;
                _mapLinePreferredId = preferred;
                _mapLine = string.IsNullOrEmpty(preferred)
                    ? "(未选到 mapLayout)"
                    : preferred + " (未加载)";
                return;
            }

            var placementCount = layout.Placements?.Count ?? 0;
            if (_hasMapLineInput && _mapLineLoaded && _mapLineId == layout.Id &&
                _mapLineWidth == layout.Width && _mapLineHeight == layout.Height &&
                _mapLinePlacementCount == placementCount)
                return;
            _hasMapLineInput = true;
            _mapLineLoaded = true;
            _mapLineId = layout.Id;
            _mapLineWidth = layout.Width;
            _mapLineHeight = layout.Height;
            _mapLinePlacementCount = placementCount;
            _mapLine = _mapLineId + " · " + _mapLineWidth + "×" + _mapLineHeight +
                       " · placements=" + _mapLinePlacementCount;
        }

        static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max)
                return s ?? "";
            return "…" + s.Substring(s.Length - max + 1);
        }
    }
}
