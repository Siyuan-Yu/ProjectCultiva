using UnityEngine;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// LevelTester 统一 Development Cheat Tools UI。
    /// 仅服务于 LevelTester 人工验收；不占用 F-Key；不嵌入正式 Gameplay UI。
    /// </summary>
    public sealed class HostLevelTesterCheatPanel : MonoBehaviour
    {
        enum CheatTab
        {
            Time = 0,
            Background = 1,
            FormalArmy = 2,
            Content = 3,
            Diplomacy = 4,
            Snapshot = 5,
            Battle = 6,
            Diagnostics = 7,
        }

        static readonly string[] TabLabels =
        {
            "时间",
            "后台角色",
            "正规军",
            "内容",
            "外交",
            "存档",
            "战斗",
            "诊断",
        };

        const int WindowId = 0x1E7E573;
        const float PanelWidth = 620f;
        const float PanelMinHeight = 480f;
        const float TabRowHeight = 24f;
        const int TabsPerRow = 4;

        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] bool visible;
        [SerializeField] KeyCode toggleKey = KeyCode.BackQuote;

        readonly LevelTesterCheatTimeSection _time = new LevelTesterCheatTimeSection();
        readonly LevelTesterCheatBackgroundSection _background = new LevelTesterCheatBackgroundSection();
        readonly LevelTesterCheatFormalArmySection _formalArmy = new LevelTesterCheatFormalArmySection();
        readonly LevelTesterCheatContentSection _content = new LevelTesterCheatContentSection();
        readonly LevelTesterCheatDiplomacySection _diplomacy = new LevelTesterCheatDiplomacySection();

        CheatTab _activeTab = CheatTab.Time;
        bool _resetConfirmPending;
        string _sessionStatus = string.Empty;
        string _snapshotStatus = string.Empty;
        Vector2 _tabScroll;
        Rect _panelRect;
        bool _panelRectInitialized;
        GUIStyle _title;
        GUIStyle _body;

        public bool IsVisible => visible;

        public void Bind(PlayableHostBootstrap host, HostSelectionController selection)
        {
            bootstrap = host;
            selectionController = selection;
        }

        public void ToggleVisible() => visible = !visible;

        public void Show() => visible = true;

        public void Hide() => visible = false;

        void Update()
        {
            if (Input.GetKeyDown(toggleKey))
                visible = !visible;
        }

        void OnGUI()
        {
            if (!visible)
                return;

            EnsureStyles();
            EnsurePanelRect();
            _panelRect = GUI.Window(WindowId, _panelRect, DrawWindow, "LevelTester 开发工具");
            HostUiHitTest.Block(_panelRect);
        }

        void EnsurePanelRect()
        {
            if (_panelRectInitialized)
                return;
            _panelRect = new Rect(24f, 120f, PanelWidth, PanelMinHeight);
            _panelRectInitialized = true;
        }

        void EnsureStyles()
        {
            if (_body != null)
                return;
            _title = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            _body = new GUIStyle(GUI.skin.label) { wordWrap = true };
        }

        void DrawWindow(int id)
        {
            GUI.DragWindow(new Rect(0f, 0f, PanelWidth, 22f));

            if (GUI.Button(new Rect(PanelWidth - 72f, 4f, 64f, 20f), "关闭"))
                visible = false;

            const float pad = 8f;
            var innerW = PanelWidth - pad * 2f - 16f;
            var tabBarTop = 26f;
            var tabBarWidth = PanelWidth - pad * 2f;
            var tabW = tabBarWidth / TabsPerRow - 2f;

            for (var i = 0; i < TabLabels.Length; i++)
            {
                var row = i / TabsPerRow;
                var col = i % TabsPerRow;
                var rect = new Rect(
                    pad + col * (tabW + 2f),
                    tabBarTop + row * TabRowHeight,
                    tabW,
                    TabRowHeight - 2f);
                var selected = _activeTab == (CheatTab)i;
                if (GUI.Toggle(rect, selected, TabLabels[i], GUI.skin.button))
                    _activeTab = (CheatTab)i;
            }

            var tabRows = (TabLabels.Length + TabsPerRow - 1) / TabsPerRow;
            var contentTop = tabBarTop + tabRows * TabRowHeight + 4f;
            var viewH = Mathf.Max(120f, _panelRect.height - contentTop - pad);
            var contentH = EstimateActiveTabContentHeight();
            _tabScroll = GUI.BeginScrollView(
                new Rect(pad, contentTop, PanelWidth - pad * 2f, viewH),
                _tabScroll,
                new Rect(0f, 0f, innerW, contentH));

            DrawActiveTab(0f, innerW);
            GUI.EndScrollView();
        }

        float EstimateActiveTabContentHeight()
        {
            switch (_activeTab)
            {
                case CheatTab.Time:
                    return 280f;
                case CheatTab.Background:
                    return 520f;
                case CheatTab.FormalArmy:
                    return 960f;
                case CheatTab.Content:
                    return 480f;
                case CheatTab.Diplomacy:
                    return 420f;
                case CheatTab.Snapshot:
                    return 260f;
                case CheatTab.Battle:
                    return 80f;
                case CheatTab.Diagnostics:
                    return 80f;
                default:
                    return 400f;
            }
        }

        void DrawActiveTab(float x, float width)
        {
            switch (_activeTab)
            {
                case CheatTab.Time:
                    _time.Draw(bootstrap, x, 0f, width, _body);
                    break;
                case CheatTab.Background:
                    _background.Draw(bootstrap, x, 0f, width, _body);
                    break;
                case CheatTab.FormalArmy:
                    _formalArmy.Draw(bootstrap, x, 0f, width, _body);
                    break;
                case CheatTab.Content:
                    _content.Draw(bootstrap, selectionController, x, 0f, width, _body);
                    break;
                case CheatTab.Diplomacy:
                    _diplomacy.Draw(bootstrap, x, 0f, width, _body);
                    break;
                case CheatTab.Snapshot:
                    DrawSnapshotTab(x, 0f, width);
                    break;
                case CheatTab.Battle:
                    DrawBattleTab(x, 0f, width);
                    break;
                case CheatTab.Diagnostics:
                    DrawDiagnosticsTab(x, 0f, width);
                    break;
            }
        }

        void DrawSnapshotTab(float x, float y, float width)
        {
            var lineH = 18f;
            GUI.Label(new Rect(x, y, width, lineH),
                "存档 v" + HostLevelTesterSnapshotOps.SchemaVersion + "  " +
                HostLevelTesterSnapshotOps.SlotPath, _body);
            y += lineH + 4f;

            var saved = HostLevelTesterSnapshotSummary.LastSaved;
            var runtime = HostLevelTesterSnapshotSummary.LastRuntime;
            GUI.Label(new Rect(x, y, width, lineH),
                "上次保存: Ch=" + saved.CharacterCount +
                " Party=" + saved.PlayerPartyCount +
                " Army=" + saved.FormalArmyCount +
                " " + saved.WorldLocation, _body);
            y += lineH;
            GUI.Label(new Rect(x, y, width, lineH),
                "当前 Runtime: Ch=" + runtime.CharacterCount +
                " Party=" + runtime.PlayerPartyCount +
                " Army=" + runtime.FormalArmyCount +
                " " + runtime.WorldLocation, _body);
            y += lineH;
            if (!string.IsNullOrEmpty(saved.PlayerPartyDetail))
            {
                GUI.Label(new Rect(x, y, width, lineH), saved.PlayerPartyDetail, _body);
                y += lineH;
            }

            if (!string.IsNullOrEmpty(saved.LocalPlacementsDetail))
            {
                GUI.Label(new Rect(x, y, width, lineH), saved.LocalPlacementsDetail, _body);
                y += lineH;
            }

            if (!string.IsNullOrEmpty(runtime.PlayerPartyDetail))
            {
                GUI.Label(new Rect(x, y, width, lineH), runtime.PlayerPartyDetail, _body);
                y += lineH;
            }

            if (!string.IsNullOrEmpty(runtime.LocalPlacementsDetail))
            {
                GUI.Label(new Rect(x, y, width, lineH), runtime.LocalPlacementsDetail, _body);
                y += lineH;
            }

            if (bootstrap?.ViewSpawner != null)
            {
                GUI.Label(new Rect(x, y, width, lineH),
                    "Presented=" + bootstrap.ViewSpawner.SpawnedCount, _body);
                y += lineH;
            }

            if (GUI.Button(new Rect(x, y, width * 0.48f, 24f), "保存存档"))
            {
                var r = HostLevelTesterSnapshotOps.TrySave(bootstrap);
                _snapshotStatus = (r.Success ? "成功：" : "失败：") + r.Message;
            }

            if (GUI.Button(new Rect(x + width * 0.52f, y, width * 0.48f, 24f), "读取存档"))
            {
                var r = HostLevelTesterSnapshotOps.TryLoad(bootstrap);
                _snapshotStatus = (r.Success ? "成功：" : "失败：") + r.Message;
            }

            y += 28f;
            if (!_resetConfirmPending)
            {
                if (GUI.Button(new Rect(x, y, width, 24f), "重置 LevelTester 会话…"))
                    _resetConfirmPending = true;
            }
            else
            {
                GUI.Label(new Rect(x, y, width, lineH * 2f),
                    "将按当前 Inspector 配置重建整个会话。", _body);
                y += lineH * 2f;
                if (GUI.Button(new Rect(x, y, width * 0.48f, 24f), "确认重置"))
                {
                    _resetConfirmPending = false;
                    if (bootstrap != null)
                    {
                        var ok = bootstrap.TryInitialize();
                        _sessionStatus = ok ? "成功：会话已重置。" : "失败：会话重置失败。";
                    }
                    else
                    {
                        _sessionStatus = "失败：未找到 Bootstrap。";
                    }
                }

                if (GUI.Button(new Rect(x + width * 0.52f, y, width * 0.48f, 24f), "取消"))
                    _resetConfirmPending = false;
            }

            y += 28f;
            if (!string.IsNullOrEmpty(_snapshotStatus))
                GUI.Label(new Rect(x, y, width, lineH * 2f), _snapshotStatus, _body);
            if (!string.IsNullOrEmpty(_sessionStatus))
                GUI.Label(new Rect(x, y + lineH * 2f, width, lineH * 2f), _sessionStatus, _body);
        }

        void DrawBattleTab(float x, float y, float width)
        {
            var forceSolo = AutoBattleCasualtyService.DebugForceSoloAutoBattleIncapacitated;
            var next = GUI.Toggle(
                new Rect(x, y, width, 22f),
                forceSolo,
                "调试：下次单人自动战斗必定失能");
            if (next != forceSolo)
                AutoBattleCasualtyService.DebugForceSoloAutoBattleIncapacitated = next;
            y += 28f;

            var showBattleHexOverlay = BattleEngagementWorldMapDebug.ShowOverlay;
            var nextOverlay = GUI.Toggle(
                new Rect(x, y, width, 22f),
                showBattleHexOverlay,
                "调试：WorldMap 高亮 BattleArea(橙) / SupportArea(蓝)");
            if (nextOverlay != showBattleHexOverlay)
                BattleEngagementWorldMapDebug.ShowOverlay = nextOverlay;
            y += 28f;

            var world = bootstrap?.Session?.World;
            if (world != null)
            {
                var summary = BattleEngagementAuthorityDebug.BuildSummary(world);
                GUI.Label(new Rect(x, y, width, 360f), summary, _body);
            }
        }

        void DrawDiagnosticsTab(float x, float y, float width)
        {
            var strongSep = HostHexWorldRenderer.DebugStrongHexSeparation;
            var nextSep = GUI.Toggle(new Rect(x, y, width, 22f), strongSep,
                "调试：强化 Hex 分离（仅渲染）");
            if (nextSep != strongSep)
                HostHexWorldRenderer.DebugStrongHexSeparation = nextSep;
        }

        public const float TopBarEntryY = 8f;
        public const float TopBarEntryW = 72f;
        public const float TopBarEntryH = 32f;
        /// <summary>FormalHud 顶栏：紧挨 20x 右侧（pause@300 + 60 + 4×44 + 4）。</summary>
        public const float TopBarEntryX = 540f;

        public static void DrawTopBarEntryButton(PlayableHostBootstrap bootstrap)
        {
            DrawTopBarEntryButton(bootstrap, TopBarEntryX);
        }

        public static void DrawTopBarEntryButton(PlayableHostBootstrap bootstrap, float x)
        {
            if (bootstrap == null)
                return;

            var btn = new Rect(x, TopBarEntryY, TopBarEntryW, TopBarEntryH);
            HostUiHitTest.Block(btn);
            var cheat = bootstrap.LevelTesterCheatPanel ??
                        bootstrap.GetComponent<HostLevelTesterCheatPanel>();
            var label = cheat != null && cheat.IsVisible ? "关闭作弊" : "作弊工具";
            if (GUI.Button(btn, label) && cheat != null)
                cheat.ToggleVisible();
        }
    }
}
