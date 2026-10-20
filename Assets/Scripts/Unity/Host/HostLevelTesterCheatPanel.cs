using System.Text;
using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Combat;
using XianXia.Core.Attributes;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Npc;
using XianXia.Core.World.Strategic;
using UnityEngine.SceneManagement;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// LevelTester 统一 Development Cheat Tools UI。
    /// 仅服务于 LevelTester 人工验收；不占用 F-Key；不嵌入正式 Gameplay UI。
    /// </summary>
    public sealed class HostLevelTesterCheatPanel : MonoBehaviour
    {
        const int WindowId = 0x1E7E573;
        const float PanelWidth = 860f;
        const float PanelMinHeight = 680f;
        const float DirectoryWidth = 228f;

        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] bool visible;
        [SerializeField] KeyCode toggleKey = KeyCode.BackQuote;

        readonly LevelTesterCheatTimeSection _time = new LevelTesterCheatTimeSection();
        readonly LevelTesterCheatBackgroundSection _background = new LevelTesterCheatBackgroundSection();
        readonly LevelTesterCheatContentSection _content = new LevelTesterCheatContentSection();
        readonly LevelTesterCheatDiplomacySection _diplomacy = new LevelTesterCheatDiplomacySection();
        readonly Dictionary<string, Vector2> _toolScroll = new Dictionary<string, Vector2>();
        readonly Dictionary<string, string> _panelToolResults = new Dictionary<string, string>();
        readonly HashSet<string> _expandedGroups = new HashSet<string>();

        string _activeToolId = "civilian.satiety";
        Vector2 _directoryScroll;
        bool _resetConfirmPending;
        string _panelStatus = string.Empty;
        string _search = string.Empty;
        string _lastOperation = "尚无操作";
        string _lastTarget = "—";
        string _lastOutcome = "—";
        string _observedContentStatus = string.Empty;
        string _observedBackgroundStatus = string.Empty;
        string _observedDiplomacyStatus = string.Empty;
        string _observedTimeStatus = string.Empty;
        string _observedPanelStatus = string.Empty;
        object _observedWorld;
        int _observedSceneHandle = int.MinValue;
        string _systemDiagnosticsCache = string.Empty;
        float _nextSystemDiagnosticsRefreshAt;
        bool _textInputFocused;
        bool _groupExpansionInitialized;
        bool _clearOwnedGuiStateRequested;
        bool _ownsWindowDrag;
        int _windowDragHotControl;
        Rect _panelRect;
        bool _panelRectInitialized;
        GUIStyle _title;
        GUIStyle _body;
        string PersistentOwner => "LevelTesterCheatPanel:" + GetInstanceID();
        string TextInputOwner => PersistentOwner + ":TextInput";

        public bool IsVisible => visible;

        public void Bind(PlayableHostBootstrap host, HostSelectionController selection)
        {
            bootstrap = host;
            selectionController = selection;
        }

        public void ToggleVisible()
        {
            if (visible) Hide(); else Show();
        }

        public void Show()
        {
            if (!HostPlayerPaths.DevelopmentToolsAllowed) return;
            visible = true;
            EnsurePanelRect();
            ClampPanelRect();
            HostUiHitTest.RegisterPersistent(PersistentOwner, _panelRect);
        }

        public void Hide()
        {
            visible = false;
            _textInputFocused = false;
            _resetConfirmPending = false;
            _content.CancelPendingConfirmation();
            _clearOwnedGuiStateRequested = true;
            HostUiHitTest.UnregisterPersistent(PersistentOwner, retireForCurrentFrame: true);
            HostInputGate.ReleaseTextInput(TextInputOwner);
        }

        void OnEnable()
        {
            if (visible) Show();
        }

        void OnDisable()
        {
            EndOwnInteraction();
        }

        void OnDestroy()
        {
            EndOwnInteraction();
        }

        void EndOwnInteraction()
        {
            visible = false;
            _textInputFocused = false;
            _resetConfirmPending = false;
            _content.CancelPendingConfirmation();
            _clearOwnedGuiStateRequested = true;
            HostUiHitTest.UnregisterPersistent(PersistentOwner);
            HostInputGate.ReleaseTextInput(TextInputOwner);
        }

        void Update()
        {
            if (!HostPlayerPaths.DevelopmentToolsAllowed) { if (visible) Hide(); return; }
            if (Input.GetKeyDown(toggleKey) && !_textInputFocused)
                ToggleVisible();
            if (!visible)
                return;

            EnsurePanelRect();
            ClampPanelRect();
            HostUiHitTest.RegisterPersistent(PersistentOwner, _panelRect);
            var world = bootstrap?.Session?.World;
            var sceneHandle = SceneManager.GetActiveScene().handle;
            if (!ReferenceEquals(_observedWorld, world) || _observedSceneHandle != sceneHandle)
            {
                _observedWorld = world;
                _observedSceneHandle = sceneHandle;
                ClearSessionContext();
            }
        }

        void OnGUI()
        {
            if (!HostPlayerPaths.DevelopmentToolsAllowed) return;
            if (!visible)
                return;

            EnsureStyles();
            ClearOwnedGuiStateIfRequested();
            EnsurePanelRect();
            ClampPanelRect();
            // Register before GUI.Window so input consumers do not depend on this window's draw order.
            HostUiHitTest.RegisterPersistent(PersistentOwner, _panelRect);
            _panelRect = GUI.Window(WindowId, _panelRect, DrawWindow, "LevelTester 开发工具");
            ClampPanelRect();
            if (!visible)
            {
                ClearOwnedGuiStateIfRequested();
                return;
            }
            HostUiHitTest.RegisterPersistent(PersistentOwner, _panelRect);

            var focused = GUI.GetNameOfFocusedControl();
            _textInputFocused = !string.IsNullOrEmpty(focused) &&
                                focused.StartsWith("LevelTester.", System.StringComparison.Ordinal);
            if (_textInputFocused)
                HostInputGate.AcquireTextInput(TextInputOwner);
            else
                HostInputGate.ReleaseTextInput(TextInputOwner);
        }

        void ClearOwnedGuiStateIfRequested()
        {
            if (!_clearOwnedGuiStateRequested)
                return;
            var focused = GUI.GetNameOfFocusedControl();
            if (!string.IsNullOrEmpty(focused) &&
                focused.StartsWith("LevelTester.", System.StringComparison.Ordinal))
                GUI.FocusControl(null);
            if (_ownsWindowDrag && _windowDragHotControl != 0 &&
                GUIUtility.hotControl == _windowDragHotControl)
                GUIUtility.hotControl = 0;
            _ownsWindowDrag = false;
            _windowDragHotControl = 0;
            _clearOwnedGuiStateRequested = false;
        }

        void EnsurePanelRect()
        {
            if (_panelRectInitialized)
                return;
            _panelRect = new Rect(24f, 120f, PanelWidth, PanelMinHeight);
            _panelRectInitialized = true;
        }

        void ClampPanelRect()
        {
            var width = Mathf.Min(PanelWidth, Mathf.Max(320f, Screen.width - 16f));
            var height = Mathf.Min(PanelMinHeight, Mathf.Max(360f, Screen.height - 16f));
            _panelRect.width = width;
            _panelRect.height = height;
            _panelRect.x = Mathf.Clamp(_panelRect.x, 0f, Mathf.Max(0f, Screen.width - width));
            _panelRect.y = Mathf.Clamp(_panelRect.y, 0f, Mathf.Max(0f, Screen.height - height));
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
            var windowW = _panelRect.width;
            var closeRect = new Rect(windowW - 72f, 4f, 64f, 20f);
            if (GUI.Button(closeRect, "关闭"))
            {
                Hide();
                return;
            }

            const float pad = 8f;
            var usableW = windowW - pad * 2f;
            DrawFixedContext(pad, 26f, usableW);

            const float searchTop = 138f;
            GUI.Label(new Rect(pad, searchTop, 42f, 22f), "搜索");
            GUI.SetNextControlName("LevelTester.Search");
            _search = GUI.TextField(new Rect(pad + 44f, searchTop, usableW - 44f, 22f), _search ?? string.Empty);

            var bodyTop = searchTop + 28f;
            var bodyH = Mathf.Max(160f, _panelRect.height - bodyTop - pad);
            var directoryRect = new Rect(pad, bodyTop, DirectoryWidth, bodyH);
            var toolRect = new Rect(pad + DirectoryWidth + 8f, bodyTop, usableW - DirectoryWidth - 8f, bodyH);
            DrawToolDirectory(directoryRect);
            DrawSelectedToolPage(toolRect);

            var current = Event.current;
            if (current != null && current.type == EventType.ScrollWheel &&
                (directoryRect.Contains(current.mousePosition) || toolRect.Contains(current.mousePosition)))
                current.Use();

            CaptureSectionFeedback();

            var dragRect = new Rect(0f, 0f, Mathf.Max(0f, closeRect.xMin - 8f), 22f);
            var currentEvent = Event.current;
            if (currentEvent != null && currentEvent.type == EventType.MouseDown &&
                dragRect.Contains(currentEvent.mousePosition))
                _ownsWindowDrag = true;
            GUI.DragWindow(dragRect);
            if (_ownsWindowDrag && GUIUtility.hotControl != 0)
                _windowDragHotControl = GUIUtility.hotControl;
            if (currentEvent != null && currentEvent.rawType == EventType.MouseUp)
            {
                _ownsWindowDrag = false;
                _windowDragHotControl = 0;
            }
        }

        void DrawToolDirectory(Rect rect)
        {
            GUI.Box(rect, GUIContent.none);
            EnsureInitialGroupExpansion();
            var searching = !string.IsNullOrWhiteSpace(_search);
            var visibleGroupCount = 0;
            var visibleToolCount = 0;
            var previousGroupKey = string.Empty;
            for (var i = 0; i < LevelTesterToolCatalog.All.Count; i++)
            {
                var entry = LevelTesterToolCatalog.All[i];
                if (!entry.Matches(_search)) continue;
                if (!string.Equals(previousGroupKey, entry.GroupKey, System.StringComparison.Ordinal))
                {
                    visibleGroupCount++;
                    previousGroupKey = entry.GroupKey;
                }
                if (searching || _expandedGroups.Contains(entry.GroupKey)) visibleToolCount++;
            }
            var contentHeight = Mathf.Max(rect.height - 4f,
                visibleGroupCount * 26f + visibleToolCount * 27f + 12f);
            var view = new Rect(rect.x + 3f, rect.y + 3f, rect.width - 6f, rect.height - 6f);
            _directoryScroll = GUI.BeginScrollView(view, _directoryScroll,
                new Rect(0f, 0f, rect.width - 24f, contentHeight));

            var y = 0f;
            previousGroupKey = string.Empty;
            var found = 0;
            for (var i = 0; i < LevelTesterToolCatalog.All.Count; i++)
            {
                var entry = LevelTesterToolCatalog.All[i];
                if (!entry.Matches(_search)) continue;
                if (!string.Equals(previousGroupKey, entry.GroupKey, System.StringComparison.Ordinal))
                {
                    var expanded = searching || _expandedGroups.Contains(entry.GroupKey);
                    var groupLabel = (expanded ? "▾ " : "▸ ") + entry.Group;
                    var groupRect = new Rect(2f, y, rect.width - 28f, 24f);
                    if (searching)
                        GUI.Label(groupRect, groupLabel, GUI.skin.button);
                    else if (GUI.Button(groupRect, groupLabel))
                    {
                        if (expanded) _expandedGroups.Remove(entry.GroupKey);
                        else _expandedGroups.Add(entry.GroupKey);
                    }
                    y += 26f;
                    previousGroupKey = entry.GroupKey;
                }

                if (!searching && !_expandedGroups.Contains(entry.GroupKey))
                    continue;

                var selected = string.Equals(_activeToolId, entry.Id, System.StringComparison.Ordinal);
                if (GUI.Toggle(new Rect(16f, y, rect.width - 42f, 24f), selected, entry.Title, GUI.skin.button) &&
                    !selected)
                    SelectTool(entry.Id);
                y += 27f;
                found++;
            }

            if (found == 0)
                GUI.Label(new Rect(4f, y, rect.width - 30f, 40f), "没有匹配的具体功能。", _body);
            GUI.EndScrollView();
        }

        void EnsureInitialGroupExpansion()
        {
            if (_groupExpansionInitialized)
                return;
            var active = LevelTesterToolCatalog.Find(_activeToolId);
            if (active != null)
                _expandedGroups.Add(active.GroupKey);
            _groupExpansionInitialized = true;
        }

        void DrawSelectedToolPage(Rect rect)
        {
            GUI.Box(rect, GUIContent.none);
            var entry = LevelTesterToolCatalog.Find(_activeToolId) ?? LevelTesterToolCatalog.All[0];
            if (!_toolScroll.TryGetValue(entry.Id, out var scroll)) scroll = Vector2.zero;
            var view = new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, rect.height - 8f);
            var innerWidth = rect.width - 28f;
            scroll = GUI.BeginScrollView(view, scroll, new Rect(0f, 0f, innerWidth, EstimateToolHeight(entry.Id)));
            _toolScroll[entry.Id] = scroll;

            GUI.Label(new Rect(0f, 0f, innerWidth, 24f), entry.Title, _title);
            GUI.Label(new Rect(0f, 26f, innerWidth, 38f), entry.Description, _body);
            DrawSelectedTool(entry.Id, 0f, 68f, innerWidth);
            GUI.EndScrollView();
        }

        void SelectTool(string toolId)
        {
            if (string.IsNullOrEmpty(toolId) || string.Equals(_activeToolId, toolId, System.StringComparison.Ordinal))
                return;
            if (IsPanelTool(_activeToolId)) _panelToolResults[_activeToolId] = _panelStatus ?? string.Empty;
            _activeToolId = toolId;
            _panelStatus = IsPanelTool(toolId) && _panelToolResults.TryGetValue(toolId, out var savedResult)
                ? savedResult : string.Empty;
            _resetConfirmPending = false;
            _content.CancelPendingConfirmation();
        }

        static bool IsPanelTool(string toolId) =>
            !string.IsNullOrEmpty(toolId) &&
            !toolId.StartsWith("civilian.", System.StringComparison.Ordinal) &&
            !toolId.StartsWith("focus.", System.StringComparison.Ordinal) &&
            !toolId.StartsWith("commerce.", System.StringComparison.Ordinal) &&
            !toolId.StartsWith("quest.", System.StringComparison.Ordinal) &&
            !toolId.StartsWith("social.", System.StringComparison.Ordinal) &&
            !toolId.StartsWith("content.", System.StringComparison.Ordinal) &&
            !toolId.StartsWith("world.background-", System.StringComparison.Ordinal) &&
            !toolId.StartsWith("diplomacy.", System.StringComparison.Ordinal) &&
            !toolId.StartsWith("time.", System.StringComparison.Ordinal) &&
            (!toolId.StartsWith("diagnostic.", System.StringComparison.Ordinal) || toolId == "diagnostic.system");

        static float EstimateToolHeight(string toolId)
        {
            if (toolId != null && toolId.StartsWith("diagnostic.", System.StringComparison.Ordinal)) return 1800f;
            if (toolId == "encounter.intervene") return 900f;
            return 720f;
        }

        void DrawSelectedTool(string toolId, float x, float y, float width)
        {
            if (toolId.StartsWith("civilian.", System.StringComparison.Ordinal) ||
                toolId.StartsWith("focus.", System.StringComparison.Ordinal) ||
                toolId.StartsWith("commerce.", System.StringComparison.Ordinal) ||
                toolId.StartsWith("quest.", System.StringComparison.Ordinal) ||
                toolId.StartsWith("social.", System.StringComparison.Ordinal) ||
                toolId.StartsWith("content.", System.StringComparison.Ordinal) ||
                (toolId.StartsWith("diagnostic.", System.StringComparison.Ordinal) && toolId != "diagnostic.system"))
            {
                _content.DrawTool(toolId, bootstrap, selectionController, x, y, width, _body);
                return;
            }
            if (toolId.StartsWith("world.background-", System.StringComparison.Ordinal))
            {
                _background.DrawTool(toolId, bootstrap, x, y, width, _body);
                return;
            }
            if (toolId.StartsWith("diplomacy.", System.StringComparison.Ordinal))
            {
                _diplomacy.DrawTool(toolId, bootstrap, x, y, width, _body);
                return;
            }
            if (toolId.StartsWith("time.", System.StringComparison.Ordinal))
            {
                _time.DrawTool(toolId, bootstrap, x, y, width, _body);
                return;
            }

            switch (toolId)
            {
                case "session.save": DrawSnapshotSaveTool(x, y, width); break;
                case "session.load": DrawSnapshotLoadTool(x, y, width); break;
                case "session.reset": DrawSessionResetTool(x, y, width); break;
                case "combat.party-attack": DrawPartyAttackTool(x, y, width); break;
                case "combat.party-max-hp": DrawPartyMaxHpTool(x, y, width); break;
                case "combat.party-refill": DrawPartyRefillTool(x, y, width); break;
                case "combat.incapacitate-selected": DrawSelectedIncapacitateTool(x, y, width); break;
                case "world.npc-squad-move": DrawNpcSquadMoveTool(x, y, width); break;
                case "succession.prepare": DrawSuccessionPrepareTool(x, y, width); break;
                case "succession.kill-party": DrawSuccessionKillTool(x, y, width); break;
                case "control.incapacitate-party": DrawControlHandoffTool(x, y, width); break;
                case "encounter.intervene": DrawEncounterInterventionTool(x, y, width); break;
                case "diagnostic.system": DrawDiagnosticsTab(x, y, width); break;
                default: GUI.Label(new Rect(x, y, width, 24f), "此功能入口尚不可用。", _body); break;
            }
        }
        void DrawFixedContext(float x, float y, float width)
        {
            var world = bootstrap?.Session?.World;
            var selectedText = "（无；选中人物操作禁用，不回退到主控）";
            var selectedIds = selectionController?.State?.SelectedIds;
            if (selectedIds != null && selectedIds.Count == 1)
            {
                var id = selectedIds[0];
                Entity entity = null;
                if (world != null) world.Entities.TryGet(id, out entity);
                selectedText = (entity?.DisplayName ?? "Missing") + "〔EntityId=" + id.Value + "〕";
            }
            else if (selectedIds != null && selectedIds.Count > 1)
            {
                selectedText = "多选 " + selectedIds.Count + " 个 Entity；须缩减为单一明确目标";
            }

            var activeText = "—";
            var siteText = "—";
            if (bootstrap?.Session?.PlayerParty?.HasActive == true)
            {
                var activeId = bootstrap.Session.PlayerParty.ActiveCharacterId;
                Entity active = null;
                if (world != null) world.Entities.TryGet(activeId, out active);
                activeText = (active?.DisplayName ?? "Missing") + "〔EntityId=" + activeId.Value + "〕";
                if (world != null)
                {
                    var siteId = MortalCivilianQuery.ResolveCurrentSiteId(world, activeId);
                    if (!string.IsNullOrEmpty(siteId) && world.Strategic.Sites.TryGet(siteId, out var site))
                        siteText = (site.DisplayName ?? siteId) + "〔" + siteId + "〕 owner=" + site.OwnerFactionId;
                }
            }

            var scene = SceneManager.GetActiveScene();
            var package = world == null ? "—" : world.EnabledPackageId + "@" + world.EnabledPackageVersion;
            GUI.Box(new Rect(x, y, width, 108f), GUIContent.none);
            GUI.Label(new Rect(x + 6f, y + 3f, width - 12f, 18f), "选中人物：" + selectedText, _body);
            GUI.Label(new Rect(x + 6f, y + 21f, width - 12f, 18f), "当前 ActiveCharacter：" + activeText, _body);
            GUI.Label(new Rect(x + 6f, y + 39f, width - 12f, 18f), "当前 Site：" + siteText, _body);
            GUI.Label(new Rect(x + 6f, y + 57f, width - 12f, 18f),
                "场景：" + scene.name + "　数据包：" + package + "　Snapshot schema=" + HostLevelTesterSnapshotOps.SchemaVersion,
                _body);
            GUI.Label(new Rect(x + 6f, y + 77f, width - 12f, 28f),
                "最近操作：" + _lastOperation + "　实际目标：" + _lastTarget + "　结果：" + _lastOutcome, _body);
        }

        void CaptureSectionFeedback()
        {
            var active = LevelTesterToolCatalog.Find(_activeToolId);
            var operation = active?.Title ?? "LevelTester 操作";
            CaptureStatus(operation, _content.SectionStatus, ref _observedContentStatus);
            CaptureStatus(operation, _background.SectionStatus, ref _observedBackgroundStatus);
            CaptureStatus(operation, _diplomacy.SectionStatus, ref _observedDiplomacyStatus);
            CaptureStatus(operation, _time.SectionStatus, ref _observedTimeStatus);
            CaptureStatus(operation, _panelStatus, ref _observedPanelStatus);
        }

        void CaptureStatus(string operation, string status, ref string observed)
        {
            if (string.IsNullOrEmpty(status) || string.Equals(status, observed, System.StringComparison.Ordinal))
                return;
            observed = status;
            _lastOperation = operation;
            _lastTarget = DescribeCurrentTarget();
            _lastOutcome = status;
        }

        string DescribeCurrentTarget()
        {
            var ids = selectionController?.State?.SelectedIds;
            if (ids == null || ids.Count == 0) return "无选中人物（未回退到主控）";
            if (ids.Count > 1) return "多选 " + ids.Count + " 个 Entity";
            return "选中人物 EntityId=" + ids[0].Value;
        }

        static string FirstNonEmpty(params string[] values)
        {
            for (var i = 0; i < values.Length; i++)
                if (!string.IsNullOrEmpty(values[i])) return values[i];
            return string.Empty;
        }

        void ClearSessionContext()
        {
            _lastOperation = "会话/场景已变化";
            _lastTarget = "—";
            _lastOutcome = "旧目标与结果上下文已清除";
            _observedContentStatus = _observedBackgroundStatus = _observedDiplomacyStatus =
                _observedTimeStatus = _observedPanelStatus = string.Empty;
            _resetConfirmPending = false;
            _panelStatus = string.Empty;
            _panelToolResults.Clear();
            _systemDiagnosticsCache = string.Empty;
            _nextSystemDiagnosticsRefreshAt = 0f;
            _background.ResetSessionState();
            _content.ResetSessionState();
            _diplomacy.ResetSessionState();
            _time.ResetSessionState();
        }

        void DrawSnapshotSaveTool(float x, float y, float width)
        {
            DrawSnapshotSummary(x, ref y, width);
            if (GUI.Button(new Rect(x, y, width, 26f), "保存测试存档"))
            {
                var result = HostLevelTesterSnapshotOps.TrySave(bootstrap);
                _panelStatus = (result.Success ? "成功：" : "失败：") + result.Message;
            }
            DrawPanelResult(x, y + 32f, width, _panelStatus);
        }

        void DrawSnapshotLoadTool(float x, float y, float width)
        {
            DrawSnapshotSummary(x, ref y, width);
            GUI.Label(new Rect(x, y, width, 38f),
                "读取会重建运行态；完成后清除旧 Entity、确认和结果上下文，不自动重跑任何工具。", _body);
            y += 42f;
            if (GUI.Button(new Rect(x, y, width, 26f), "读取测试存档"))
            {
                var result = HostLevelTesterSnapshotOps.TryLoad(bootstrap);
                var status = (result.Success ? "成功：" : "失败：") + result.Message;
                if (result.Success) ClearSessionContext();
                _panelStatus = status;
            }
            DrawPanelResult(x, y + 32f, width, _panelStatus);
        }

        void DrawSessionResetTool(float x, float y, float width)
        {
            GUI.Label(new Rect(x, y, width, 42f),
                "影响范围：按当前 Inspector 配置重建整个 LevelTester 会话。切换功能会取消待确认状态。", _body);
            y += 48f;
            if (!_resetConfirmPending)
            {
                if (GUI.Button(new Rect(x, y, width, 26f), "重置整个会话…"))
                    _resetConfirmPending = true;
            }
            else
            {
                if (GUI.Button(new Rect(x, y, width * .48f, 26f), "确认重置"))
                {
                    _resetConfirmPending = false;
                    var ok = bootstrap != null && bootstrap.TryInitialize();
                    var status = ok ? "成功：会话已重置。" : "失败：会话重置失败。";
                    if (ok) ClearSessionContext();
                    _panelStatus = status;
                }
                if (GUI.Button(new Rect(x + width * .52f, y, width * .48f, 26f), "取消"))
                {
                    _resetConfirmPending = false;
                    _panelStatus = "已取消：未修改会话。";
                }
            }
            DrawPanelResult(x, y + 34f, width, _panelStatus);
        }

        void DrawSnapshotSummary(float x, ref float y, float width)
        {
            var saved = HostLevelTesterSnapshotSummary.LastSaved;
            var runtime = HostLevelTesterSnapshotSummary.LastRuntime;
            GUI.Label(new Rect(x, y, width, 20f),
                "Snapshot v" + HostLevelTesterSnapshotOps.SchemaVersion + " · " + HostLevelTesterSnapshotOps.SlotPath, _body);
            y += 22f;
            GUI.Label(new Rect(x, y, width, 38f),
                "上次保存：Ch=" + saved.CharacterCount + " Party=" + saved.PlayerPartyCount + " Squad=" + saved.SquadCount +
                "\n当前运行：Ch=" + runtime.CharacterCount + " Party=" + runtime.PlayerPartyCount + " Squad=" + runtime.SquadCount, _body);
            y += 44f;
            DrawSnapshotDetail(x, ref y, width, saved.PlayerPartyDetail);
            DrawSnapshotDetail(x, ref y, width, saved.LocalPlacementsDetail);
            DrawSnapshotDetail(x, ref y, width, runtime.PlayerPartyDetail);
            DrawSnapshotDetail(x, ref y, width, runtime.LocalPlacementsDetail);
            if (bootstrap?.ViewSpawner != null)
            {
                GUI.Label(new Rect(x, y, width, 20f), "Presented=" + bootstrap.ViewSpawner.SpawnedCount, _body);
                y += 24f;
            }
        }

        void DrawSnapshotDetail(float x, ref float y, float width, string detail)
        {
            if (string.IsNullOrEmpty(detail)) return;
            GUI.Label(new Rect(x, y, width, 38f), detail, _body);
            y += 42f;
        }

        void DrawPartyAttackTool(float x, float y, float width)
        {
            if (GUI.Button(new Rect(x, y, width, 26f), "我方全员攻击 +10"))
                ApplyPartyAttributeCheat(AttributeId.Attack, 10, false);
            DrawPanelResult(x, y + 32f, width, _panelStatus);
        }

        void DrawPartyMaxHpTool(float x, float y, float width)
        {
            if (GUI.Button(new Rect(x, y, width, 26f), "最大生命 +50 并回满"))
                ApplyPartyAttributeCheat(AttributeId.MaxHp, 50, true);
            DrawPanelResult(x, y + 32f, width, _panelStatus);
        }

        void DrawPartyRefillTool(float x, float y, float width)
        {
            if (GUI.Button(new Rect(x, y, width, 26f), "生命／灵力回满")) RefillPartyCombatPools();
            DrawPanelResult(x, y + 32f, width, _panelStatus);
        }

        void DrawSelectedIncapacitateTool(float x, float y, float width)
        {
            var valid = selectionController?.State != null && selectionController.State.Count == 1;
            GUI.enabled = valid;
            if (GUI.Button(new Rect(x, y, width, 26f), "让唯一选中人物进入弥留")) ForceSelectedCharacterIncapacitated();
            GUI.enabled = true;
            if (!valid) GUI.Label(new Rect(x, y + 32f, width, 36f), "请只选中一个明确人物；不会回退到主控。", _body);
            DrawPanelResult(x, y + 72f, width, _panelStatus);
        }

        void DrawNpcSquadMoveTool(float x, float y, float width)
        {
            var valid = selectionController?.State != null && selectionController.State.Count == 1;
            GUI.enabled = valid;
            if (GUI.Button(new Rect(x, y, width, 26f), "让所选 NPC 小队移动到主控附近测试点")) MoveSelectedNpcSquadNearPlayer();
            GUI.enabled = true;
            DrawPanelResult(x, y + 32f, width, _panelStatus);
        }

        void DrawSuccessionPrepareTool(float x, float y, float width)
        {
            if (GUI.Button(new Rect(x, y, width, 26f), "准备 A/B 继承候选"))
                _panelStatus = LevelTesterSuccessionCheats.TryPrepare(bootstrap).Message;
            DrawPanelResult(x, y + 32f, width, _panelStatus);
        }

        void DrawSuccessionKillTool(float x, float y, float width)
        {
            if (GUI.Button(new Rect(x, y, width, 26f), "使当前 Party 正式全灭"))
                _panelStatus = LevelTesterSuccessionCheats.TryKillCurrentParty(bootstrap).Message;
            DrawPanelResult(x, y + 32f, width, _panelStatus);
        }

        void DrawControlHandoffTool(float x, float y, float width)
        {
            if (GUI.Button(new Rect(x, y, width, 26f), "使当前 Party 全员弥留"))
                _panelStatus = LevelTesterSuccessionCheats.TryIncapacitateCurrentParty(bootstrap).Message;
            DrawPanelResult(x, y + 32f, width, _panelStatus);
        }

        void DrawEncounterInterventionTool(float x, float y, float width)
        {
            var encounter = bootstrap?.Session?.World?.Strategic?.CharacterEncounter;
            if (encounter == null)
            {
                GUI.Label(new Rect(x, y, width, 24f), "当前没有 CharacterEncounter。", _body);
                return;
            }
            GUI.Label(new Rect(x, y, width, 22f), "Encounter=" + encounter.EncounterId + " Roster=" + encounter.RosterVersion, _body);
            y += 28f;
            foreach (var candidate in encounter.Candidates)
            {
                GUI.Label(new Rect(x, y, width - 132f, 22f), candidate.SquadId + " " + candidate.Phase + " roll=" + candidate.Roll, _body);
                GUI.enabled = candidate.Phase == EncounterCandidatePhase.Undecided;
                if (GUI.Button(new Rect(x + width - 126f, y, 126f, 22f), "关系合格则介入"))
                    CharacterEncounterService.DecideCandidate(bootstrap.Session.World, candidate, true);
                GUI.enabled = true;
                y += 26f;
            }
        }

        void DrawPanelResult(float x, float y, float width, string result)
        {
            GUI.Label(new Rect(x, y, width, 20f), "最近结果", _title);
            GUI.Label(new Rect(x, y + 22f, width, 72f), string.IsNullOrEmpty(result) ? "尚未执行。" : result, _body);
        }

        void ApplyPartyAttributeCheat(AttributeId attribute, int delta, bool refill)
        {
            var world = bootstrap?.Session?.World;
            var members = bootstrap?.Session?.PlayerParty?.Members;
            var result = attribute == AttributeId.Attack
                ? LevelTesterPartyCombatCheats.AddAttack(world, members)
                : LevelTesterPartyCombatCheats.AddMaxHpAndRefill(world, members);
            _panelStatus = "上次数值作弊：" +
                (attribute == AttributeId.Attack ? "攻击 +10" : "最大生命 +50 并回满") +
                "　成功 " + result.Succeeded + " / 跳过 " + result.Skipped;
        }

        void RefillPartyCombatPools()
        {
            var world = bootstrap?.Session?.World;
            var members = bootstrap?.Session?.PlayerParty?.Members;
            var result = LevelTesterPartyCombatCheats.Refill(world, members);
            _panelStatus = "上次数值作弊：生命／灵力回满　成功 " +
                                      result.Succeeded + " / 跳过 " + result.Skipped;
        }

        void ForceSelectedCharacterIncapacitated()
        {
            var selected = selectionController?.State?.SelectedIds;
            var result = LevelTesterCharacterCombatCheats.TryForceSelectedCharacterIncapacitated(
                bootstrap?.Session?.World,
                selected);
            if (result.Success && selected != null && selected.Count == 1)
                HostSnapshotLocalPlacementCaptureSync.TryCaptureCharacterPlacementFromView(
                    bootstrap?.Session?.World, bootstrap?.ViewSpawner, selected[0]);
            _panelStatus = result.Message;
        }

        void MoveSelectedNpcSquadNearPlayer()
        {
            _panelStatus = LevelTesterNpcSquadMotionCheats.TryMoveSelectedNpcSquadNearPlayer(
                bootstrap?.Session?.World, selectionController?.State?.SelectedIds).Message;
        }

        void DrawDiagnosticsTab(float x, float y, float width)
        {
            var surface = bootstrap?.ContinuousOutdoorSurfaceRuntime;
            GUI.Label(new Rect(x, y, width, 24f), "系统 / 性能", _title);
            y += 30f;
            GUI.Label(new Rect(x, y, width, 22f),
                bootstrap != null ? bootstrap.OutdoorAuthorityDiagnostic : "Authority=Uninitialized", _body);
            y += 24f;
            GUI.Label(new Rect(x, y, width, 56f),
                bootstrap != null ? bootstrap.OpeningPopulationDiagnostic : string.Empty, _body);
            y += 60f;

            var world = bootstrap?.Session?.World;
            var strategic = world?.Strategic;
            GUI.Label(new Rect(x, y, width, 40f),
                "NPC Squad Runtime: Squads=" + (strategic?.Squads?.Squads?.Count ?? 0) +
                "  ActiveNpcSquadWorldMotions=" + CountActiveNpcSquadWorldMotions() + "\n" +
                "Playable Space: " +
                (world?.LocalMap?.IsActive == true ? "SeparateSpace" : "ContinuousSurface"), _body);
            y += 44f;

            var mover = bootstrap != null ? bootstrap.NpcScheduleMover : null;
            if (GUI.Button(new Rect(x, y, 120f, 22f), "刷新系统诊断"))
                _nextSystemDiagnosticsRefreshAt = 0f;
            y += 26f;
            if (Time.unscaledTime >= _nextSystemDiagnosticsRefreshAt || string.IsNullOrEmpty(_systemDiagnosticsCache))
            {
                _systemDiagnosticsCache =
                // 性能诊断（判断卡顿是否 A* storm／registry rebuild spike）：
                "[Perf] VisibleEntity=" + (bootstrap?.ViewSpawner != null ? bootstrap.ViewSpawner.SpawnedCount : 0) +
                " OutdoorMaterialized=" + (surface != null ? surface.MaterializedOutdoorEntityCount : 0) +
                " MovingNpc=" + (mover != null ? mover.MovingNpcCount : 0) + "\n" +
                "[Perf] NpcPathReq frame=" + (mover != null ? mover.NpcPathRequestsThisFrame : 0) +
                " /sec=" + (mover != null ? mover.NpcPathRequestsLastSecond : 0) +
                " repath/sec=" + (mover != null ? mover.NpcRepathRequestsLastSecond : 0) + "\n" +
                "[Perf] PathBuildMs last=" + (mover != null ? mover.LastNpcPathBuildMs.ToString("0.0") : "0") +
                " max/sec=" + (mover != null ? mover.MaxNpcPathBuildMsLastSecond.ToString("0.0") : "0") + "\n" +
                "[Perf] InteractSpots=" + HostInteractSpots.LoadedSpotCount +
                " Plots=" + HostMapObjectRegistry.AllPlots.Count +
                " PlaceGen=" + (surface != null ? surface.PlaceRefreshGeneration : 0) +
                " EntityGen=" + (surface != null ? surface.EntityReconcileGeneration : 0) +
                " LoadedChunks=" + (surface != null ? surface.LoadedChunkCount : 0) + "\n" +
                "[Startup] Postcondition=" +
                (bootstrap != null && !string.IsNullOrEmpty(bootstrap.ContinuousStartupPostconditionDiagnostic)
                    ? bootstrap.ContinuousStartupPostconditionDiagnostic
                    : "ok") + "\n" +
                (surface != null ? surface.DescribeDiagnostics() : string.Empty);
                _nextSystemDiagnosticsRefreshAt = Time.unscaledTime + .5f;
            }
            GUI.Label(new Rect(x, y, width, 350f), _systemDiagnosticsCache, _body);
        }

        int CountActiveNpcSquadWorldMotions()
        {
            var world = bootstrap?.Session?.World;
            if (world?.Strategic?.SquadWorldMotions == null) return 0;
            var count = 0;
            foreach (var pair in world.Strategic.SquadWorldMotions.Motions)
                if (world.Strategic.Squads.TryGet(pair.Key, out var squad) &&
                    SquadWorldMotionService.IsActiveNpcSquadAuthority(world, squad, pair.Value)) count++;
            return count;
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
            if (!HostPlayerPaths.DevelopmentToolsAllowed) return;
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
