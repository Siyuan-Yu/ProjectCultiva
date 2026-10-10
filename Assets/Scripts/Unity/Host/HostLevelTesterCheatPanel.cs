using System.Text;
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
        enum CheatTab
        {
            CurrentAcceptance = 0,
            CharacterResources = 1,
            WorldContent = 2,
            SaveSession = 3,
            Diagnostics = 4,
        }

        static readonly string[] TabLabels =
        {
            "当前验收",
            "人物与资源",
            "世界与内容",
            "存档与会话",
            "诊断",
        };

        static readonly string[] SearchCatalog =
        {
            "当前验收 > 食宿｜有粮取食 回住所睡眠 紧急就地睡眠 无粮低精力｜CIVILIAN-LIFE-01",
            "当前验收 > 工作与建设｜职业 工作面板 建设 UI 材料｜CIVILIAN-LIFE-01",
            "当前验收 > 占领与追击｜主管驻军 巡卫小队 0.2逃亡 追击 Displaced｜CIVILIAN-LIFE-01",
            "当前验收 > 医疗搬运｜救援 伤员 恢复点 Medic｜CIVILIAN-LIFE-01",
            "当前验收 > 囚室交接｜CapturedEscorted Carrier PrisonDropOff 押送｜CIVILIAN-LIFE-01",
            "当前验收 > 囚犯照料与招降｜Detained 忠诚 招降｜CIVILIAN-LIFE-01",
            "当前验收 > 存读档｜工作 搬运 押送 关押 逃亡 Displaced｜Snapshot v14",
            "人物与资源 > 战斗数值 弥留 继承 CONTROL-HANDOFF SUCCESSION",
            "人物与资源 > 我方全员攻击 最大生命 回满 选中角色弥留 NPC小队移动",
            "世界与内容 > 后台角色旅行 前往世界地点 取消 Travel",
            "世界与内容 > 外交 宣战 结盟 建立附庸 Diplomacy Vassalage",
            "世界与内容 > 内容标记 Set Flag Clear Flag 强制事件 Force Event 延迟事件 Delayed Event",
            "世界与内容 > 商店钱包 Reset Acceptance Shop｜SHOP-TRADE-01",
            "世界与内容 > 拍卖寄拍物品 Reset Acceptance Auction｜AUCTION-01",
            "世界与内容 > 社交好感 秘境同行｜SOCIAL-QUEST-01",
            "世界与内容 > Hidden石碑 PublicNotice包裹 WorldVisible灵草 清理｜DYNAMIC-DISCOVERY-01",
            "存档与会话 > 保存存档 读取存档 全会话 Reset Snapshot v14",
            "诊断 > Core 时间 Tick 次日零点 1x 2x 5x 20x 性能 authority",
        };

        const int WindowId = 0x1E7E573;
        const float PanelWidth = 720f;
        const float PanelMinHeight = 680f;
        const float TabRowHeight = 24f;
        const int TabsPerRow = 5;

        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] bool visible;
        [SerializeField] KeyCode toggleKey = KeyCode.BackQuote;

        readonly LevelTesterCheatTimeSection _time = new LevelTesterCheatTimeSection();
        readonly LevelTesterCheatBackgroundSection _background = new LevelTesterCheatBackgroundSection();
        readonly LevelTesterCheatContentSection _content = new LevelTesterCheatContentSection();
        readonly LevelTesterCheatDiplomacySection _diplomacy = new LevelTesterCheatDiplomacySection();

        CheatTab _activeTab = CheatTab.CurrentAcceptance;
        bool _resetConfirmPending;
        string _sessionStatus = string.Empty;
        string _snapshotStatus = string.Empty;
        string _partyCombatCheatStatus = string.Empty;
        readonly Vector2[] _tabScroll = new Vector2[TabLabels.Length];
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
        readonly bool[] _acceptanceExpanded = new bool[7];
        string _systemDiagnosticsCache = string.Empty;
        float _nextSystemDiagnosticsRefreshAt;
        bool _textInputFocused;
        float _contentHeight = 480f;
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
            visible = true;
            EnsurePanelRect();
            ClampPanelRect();
            HostUiHitTest.RegisterPersistent(PersistentOwner, _panelRect);
        }

        public void Hide()
        {
            visible = false;
            _textInputFocused = false;
            HostUiHitTest.UnregisterPersistent(PersistentOwner, retireForCurrentFrame: true);
            HostInputGate.Release(TextInputOwner);
        }

        void OnEnable()
        {
            if (visible) Show();
        }

        void OnDisable()
        {
            HostUiHitTest.UnregisterPersistent(PersistentOwner);
            HostInputGate.Release(TextInputOwner);
        }

        void OnDestroy()
        {
            HostUiHitTest.UnregisterPersistent(PersistentOwner);
            HostInputGate.Release(TextInputOwner);
        }

        void Update()
        {
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
            if (!visible)
                return;

            EnsureStyles();
            EnsurePanelRect();
            ClampPanelRect();
            // Register before GUI.Window so input consumers do not depend on this window's draw order.
            HostUiHitTest.RegisterPersistent(PersistentOwner, _panelRect);
            _panelRect = GUI.Window(WindowId, _panelRect, DrawWindow, "LevelTester 开发工具");
            ClampPanelRect();
            if (visible)
                HostUiHitTest.RegisterPersistent(PersistentOwner, _panelRect);

            var focused = GUI.GetNameOfFocusedControl();
            _textInputFocused = !string.IsNullOrEmpty(focused) &&
                                focused.StartsWith("LevelTester.", System.StringComparison.Ordinal);
            if (_textInputFocused)
                HostInputGate.Acquire(TextInputOwner);
            else
                HostInputGate.Release(TextInputOwner);
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
            GUI.DragWindow(new Rect(0f, 0f, windowW, 22f));

            if (GUI.Button(new Rect(windowW - 72f, 4f, 64f, 20f), "关闭"))
            {
                Hide();
                return;
            }

            const float pad = 8f;
            var innerW = windowW - pad * 2f - 16f;
            var tabBarTop = 26f;
            var tabBarWidth = windowW - pad * 2f;
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

            var contextTop = tabBarTop + TabRowHeight + 4f;
            DrawFixedContext(pad, contextTop, tabBarWidth);
            var searchTop = contextTop + 112f;
            GUI.Label(new Rect(pad, searchTop, 44f, 22f), "搜索");
            GUI.SetNextControlName("LevelTester.Search");
            _search = GUI.TextField(new Rect(pad + 46f, searchTop, tabBarWidth - 46f, 22f), _search ?? string.Empty);
            var contentTop = searchTop + 27f;
            var viewH = Mathf.Max(120f, _panelRect.height - contentTop - pad);
            var contentH = EstimateActiveTabContentHeight();
            var viewRect = new Rect(pad, contentTop, windowW - pad * 2f, viewH);
            var tabIndex = (int)_activeTab;
            _tabScroll[tabIndex] = GUI.BeginScrollView(
                viewRect,
                _tabScroll[tabIndex],
                new Rect(0f, 0f, innerW, contentH));

            var y = DrawSearchResults(0f, 0f, innerW);
            DrawActiveTab(0f, y, innerW);
            GUI.EndScrollView();
            // Let child scroll views consume first. Even at their bounds, prevent this wheel
            // event from reaching the world camera/map.
            var current = Event.current;
            if (current != null && current.type == EventType.ScrollWheel && viewRect.Contains(current.mousePosition))
                current.Use();

            CaptureSectionFeedback();
        }

        float EstimateActiveTabContentHeight()
        {
            switch (_activeTab)
            {
                case CheatTab.CurrentAcceptance:
                    return 1420f;
                case CheatTab.CharacterResources:
                    return 760f;
                case CheatTab.WorldContent:
                    return Mathf.Max(1600f, _contentHeight + 980f);
                case CheatTab.SaveSession:
                    return 360f;
                case CheatTab.Diagnostics:
                    return 1180f;
                default:
                    return 400f;
            }
        }

        void DrawActiveTab(float x, float y, float width)
        {
            switch (_activeTab)
            {
                case CheatTab.CurrentAcceptance:
                    DrawAcceptanceCards(x, y, width);
                    break;
                case CheatTab.CharacterResources:
                    DrawBattleTab(x, y, width);
                    break;
                case CheatTab.WorldContent:
                    y = _background.Draw(bootstrap, x, y, width, _body) + 12f;
                    y = _diplomacy.Draw(bootstrap, x, y, width, _body) + 12f;
                    _contentHeight = _content.Draw(bootstrap, selectionController, x, y, width, _body);
                    break;
                case CheatTab.SaveSession:
                    DrawSnapshotTab(x, y, width);
                    break;
                case CheatTab.Diagnostics:
                    y = _time.Draw(bootstrap, x, y, width, _body) + 12f;
                    DrawDiagnosticsTab(x, y, width);
                    break;
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

        float DrawSearchResults(float x, float y, float width)
        {
            var query = (_search ?? string.Empty).Trim();
            if (query.Length == 0) return y;
            GUI.Label(new Rect(x, y, width, 20f), "搜索结果（名称／别名／说明／里程碑）：", _title);
            y += 22f;
            var found = 0;
            for (var i = 0; i < SearchCatalog.Length; i++)
            {
                if (SearchCatalog[i].IndexOf(query, System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                GUI.Label(new Rect(x, y, width, 36f), "• " + SearchCatalog[i], _body);
                y += 38f;
                found++;
            }
            if (found == 0)
            {
                GUI.Label(new Rect(x, y, width, 22f), "未找到匹配操作。", _body);
                y += 24f;
            }
            return y + 6f;
        }

        void DrawAcceptanceCards(float x, float y, float width)
        {
            GUI.Label(new Rect(x, y, width, 22f),
                "CIVILIAN-LIFE-01：Implementation Complete / Producer Acceptance Pending", _title);
            y += 26f;
            GUI.Label(new Rect(x, y, width, 38f),
                "卡片只导航到既有准备工具；制作人必须通过正式 UI/世界交互完成行为。准备成功、编译通过均不等于人工验收通过。",
                _body);
            y += 42f;

            y = DrawAcceptanceCard(0, "A. 食宿", "验收准备",
                "前置：选中一个受管凡人；按场景设置需求与 Site 粗粮。",
                "正式操作：以 1x/2x/5x/20x 观察取粮、回住所或就地睡眠。",
                "预期：真实移动完成后才结算；Core 跳时不能证明该路径。",
                "阻塞：囚犯食宿生命周期早退另列缺口，不在本轮修。", x, y, width, CheatTab.WorldContent);
            y = DrawAcceptanceCard(1, "B. 工作与建设", "验收准备",
                "前置：工具只准备人物、公共木材和合法 Site。",
                "正式操作：使用正式“工作”面板分配职业，并从正式建设 UI 发起建造。",
                "预期：人物真实取材、搬运与施工；工具不直接完成工单。",
                "阻塞：ScheduleComponent 与 FactionMortalSchedule 显示/执行不一致另列缺口。", x, y, width, CheatTab.WorldContent);
            y = DrawAcceptanceCard(2, "C. 占领与追击", "观察/定位 + 验收准备",
                "前置：主管显式 DefendedSiteId 驻军；巡卫为独立三人 patrol squad。",
                "正式操作：完成占领后观察 0.2 倍逃亡、真实接近/追击直至 Displaced。",
                "预期：不传送、不凭空换身份；离屏后进入既有 Displaced 后台处理。",
                "阻塞：无本轮工具阻塞；仍待制作人人工验收。", x, y, width, CheatTab.WorldContent);
            y = DrawAcceptanceCard(3, "D. 医疗搬运", "强制状态/诊断准备（需二次确认）",
                "前置：清 RescueTarget，由可工作同势力凡人自行发现弥留者。",
                "正式操作：等待 AI 真实搬到具体恢复点并放下。",
                "预期：不预填目标、不直接送达、不恢复 HP；治疗/入院/出院明确延期。",
                "阻塞：医疗本轮只验搬运，不扩医院功能。", x, y, width, CheatTab.WorldContent);
            y = DrawAcceptanceCard(4, "E. 囚室交接", "强制状态准备（需二次确认）",
                "前置：制造 CapturedEscorted + 真实 Carrier；不会直接 Detain。",
                "正式操作：点击显式囚犯交接点，走到后执行正式安置。",
                "预期：容量与到达条件合法后 Detained；该准备跳过俘虏战。",
                "阻塞：最新 Content 已有 PrisonDropOff，旧缺口 C 已不再存在。", x, y, width, CheatTab.WorldContent);
            y = DrawAcceptanceCard(5, "F. 囚犯照料与招降", "观察/强制诊断",
                "前置：保持真实 Captured/Detained，不伪造 Alive。",
                "正式操作：正常帧循环验证食宿/照料日、忠诚下降，再从正式人物交互招降。",
                "预期：招降后获得对应人员权限并可按正式规则入队。",
                "阻塞：Captured 早退当前会挡 Detained 食宿；本轮只记录。", x, y, width, CheatTab.WorldContent);
            DrawAcceptanceCard(6, "G. 存读档", "观察/会话操作",
                "前置：分别建立正常工作、搬运/送达、押送、关押、逃亡/Displaced 状态。",
                "正式操作：保存测试槽、读取，再核 EntityId、目标和 owner。",
                "预期：不自动重跑准备；旧结果/缓存清空，状态按 Snapshot v14 恢复。",
                "阻塞：各业务分支仍待制作人人工验收。", x, y, width, CheatTab.SaveSession);
        }

        float DrawAcceptanceCard(int index, string title, string kind, string prerequisite,
            string formalAction, string expected, string blocker, float x, float y, float width,
            CheatTab destination)
        {
            var h = _acceptanceExpanded[index] ? 174f : 136f;
            GUI.Box(new Rect(x, y, width, h), GUIContent.none);
            GUI.Label(new Rect(x + 6f, y + 4f, width - 12f, 20f), title + "　〔" + kind + "〕", _title);
            GUI.Label(new Rect(x + 6f, y + 26f, width - 12f, 34f), prerequisite, _body);
            GUI.Label(new Rect(x + 6f, y + 58f, width - 12f, 34f), formalAction, _body);
            GUI.Label(new Rect(x + 6f, y + 90f, width - 12f, 34f), expected, _body);
            if (GUI.Button(new Rect(x + 6f, y + h - 28f, 170f, 22f), "打开对应准备/操作入口"))
                _activeTab = destination;
            if (GUI.Button(new Rect(x + 182f, y + h - 28f, 130f, 22f),
                    _acceptanceExpanded[index] ? "收起相关诊断" : "展开相关诊断"))
                _acceptanceExpanded[index] = !_acceptanceExpanded[index];
            if (_acceptanceExpanded[index])
                GUI.Label(new Rect(x + 6f, y + 124f, width - 12f, 40f), blocker, _body);
            return y + h + 8f;
        }

        void CaptureSectionFeedback()
        {
            CaptureStatus("内容/CIVILIAN 操作", _content.SectionStatus, ref _observedContentStatus);
            CaptureStatus("后台角色旅行", _background.SectionStatus, ref _observedBackgroundStatus);
            CaptureStatus("外交操作", _diplomacy.SectionStatus, ref _observedDiplomacyStatus);
            CaptureStatus("Core 时间/速度", _time.SectionStatus, ref _observedTimeStatus);
            var panelStatus = FirstNonEmpty(_snapshotStatus, _sessionStatus, _partyCombatCheatStatus);
            CaptureStatus(_activeTab == CheatTab.SaveSession ? "存档/会话" : "人物与资源",
                panelStatus, ref _observedPanelStatus);
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
            _snapshotStatus = _sessionStatus = _partyCombatCheatStatus = string.Empty;
            _systemDiagnosticsCache = string.Empty;
            _nextSystemDiagnosticsRefreshAt = 0f;
            _background.ResetSessionState();
            _content.ResetSessionState();
            _diplomacy.ResetSessionState();
            _time.ResetSessionState();
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
                " Squad=" + saved.SquadCount +
                " " + saved.WorldLocation, _body);
            y += lineH;
            GUI.Label(new Rect(x, y, width, lineH),
                "当前 Runtime: Ch=" + runtime.CharacterCount +
                " Party=" + runtime.PlayerPartyCount +
                " Squad=" + runtime.SquadCount +
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
            GUI.Label(new Rect(x, y, width, 22f), "我方角色数值作弊", _title);
            y += 26f;
            var buttonWidth = (width - 12f) / 3f;
            if (GUI.Button(new Rect(x, y, buttonWidth, 24f), "我方全员攻击 +10"))
                ApplyPartyAttributeCheat(AttributeId.Attack, 10, refill: false);
            if (GUI.Button(new Rect(x + buttonWidth + 6f, y, buttonWidth, 24f), "最大生命 +50 并回满"))
                ApplyPartyAttributeCheat(AttributeId.MaxHp, 50, refill: true);
            if (GUI.Button(new Rect(x + (buttonWidth + 6f) * 2f, y, buttonWidth, 24f), "生命／灵力回满"))
                RefillPartyCombatPools();
            y += 28f;
            GUI.enabled = selectionController?.State != null && selectionController.State.Count == 1;
            if (GUI.Button(new Rect(x, y, width, 24f), "选中角色进入弥留"))
                ForceSelectedCharacterIncapacitated();
            y += 28f;
            if (GUI.Button(new Rect(x, y, width, 24f), "让所选 NPC 小队移动到主控附近测试点"))
                MoveSelectedNpcSquadNearPlayer();
            y += 28f;
            GUI.enabled = true;
            if (GUI.Button(new Rect(x, y, width, 24f), "SUCCESSION-01：准备 A/B 继承候选"))
                _partyCombatCheatStatus = LevelTesterSuccessionCheats.TryPrepare(bootstrap).Message;
            y += 28f;
            if (GUI.Button(new Rect(x, y, width, 24f), "SUCCESSION-01：使当前 Party 正式全灭"))
                _partyCombatCheatStatus = LevelTesterSuccessionCheats.TryKillCurrentParty(bootstrap).Message;
            y += 28f;
            if (GUI.Button(new Rect(x, y, width, 24f), "CONTROL-HANDOFF-01：使当前 Party 全员弥留"))
                _partyCombatCheatStatus = LevelTesterSuccessionCheats.TryIncapacitateCurrentParty(bootstrap).Message;
            y += 28f;
            if (!string.IsNullOrEmpty(_partyCombatCheatStatus))
            {
                GUI.Label(new Rect(x, y, width, 100f), _partyCombatCheatStatus, _body);
                y += 104f;
            }

            var world = bootstrap?.Session?.World;
            if (world != null)
            {
                var encounter = world.Strategic.CharacterEncounter;
                if (encounter != null)
                {
                    GUI.Label(new Rect(x, y, width, 22), "遭遇 " + encounter.EncounterId + " 名单版本 " + encounter.RosterVersion);
                    y += 26;
                    foreach (var candidate in encounter.Candidates)
                    {
                        GUI.Label(new Rect(x, y, width - 130, 22), candidate.SquadId + " " + candidate.Phase + " roll=" + candidate.Roll);
                        if (candidate.Phase == EncounterCandidatePhase.Undecided &&
                            GUI.Button(new Rect(x + width - 125, y, 125, 22), "关系合格则介入"))
                            CharacterEncounterService.DecideCandidate(world, candidate, manualAccept: true);
                        y += 26;
                    }
                }
            }
        }

        void ApplyPartyAttributeCheat(AttributeId attribute, int delta, bool refill)
        {
            var world = bootstrap?.Session?.World;
            var members = bootstrap?.Session?.PlayerParty?.Members;
            var result = attribute == AttributeId.Attack
                ? LevelTesterPartyCombatCheats.AddAttack(world, members)
                : LevelTesterPartyCombatCheats.AddMaxHpAndRefill(world, members);
            _partyCombatCheatStatus = "上次数值作弊：" +
                (attribute == AttributeId.Attack ? "攻击 +10" : "最大生命 +50 并回满") +
                "　成功 " + result.Succeeded + " / 跳过 " + result.Skipped;
        }

        void RefillPartyCombatPools()
        {
            var world = bootstrap?.Session?.World;
            var members = bootstrap?.Session?.PlayerParty?.Members;
            var result = LevelTesterPartyCombatCheats.Refill(world, members);
            _partyCombatCheatStatus = "上次数值作弊：生命／灵力回满　成功 " +
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
            _partyCombatCheatStatus = result.Message;
        }

        void MoveSelectedNpcSquadNearPlayer()
        {
            _partyCombatCheatStatus = LevelTesterNpcSquadMotionCheats.TryMoveSelectedNpcSquadNearPlayer(
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
