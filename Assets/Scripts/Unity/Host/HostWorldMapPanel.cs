using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using XianXia.Core.Attributes;
using XianXia.Core.Combat;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// 宏观 WorldGraph 全屏页：RTS／文明式——头像标位、点选、右键下令、路上慢移；可缩放平移。
    /// </summary>
    public sealed class HostWorldMapPanel : MonoBehaviour
    {
        const float AvatarSize = 40f;
        const float NodeHitW = 128f;
        const float NodeHitH = 44f;
        /// <summary>敌军栈默认吸附（屏幕像素，圆形半径外延）。偏小以免抢道路右键移动。</summary>
        const float ArmyStackHitPad = 10f;
        /// <summary>与我方头像／接战残留重叠时再缩小。</summary>
        const float ArmyStackHitPadContested = 4f;
        /// <summary>判定「叠在一起」：头像与敌军视觉 rect 扩此值后相交。</summary>
        const float ArmyStackContestedOverlapPx = 4f;
        /// <summary>
        /// 最大放大：视口半宽（世界单位）。再放大一倍相对「邻站铺满」参考（半宽 1.5 ≈ 满屏跨度 3）。
        /// </summary>
        const float MinViewHalfExtent = 1.5f;
        const float MapPad = 48f;
        /// <summary>道路右键点选：屏幕像素容差（世界距离在放大后过严，几乎点不中）。</summary>
        const float RoutePickScreenPx = 28f;
        /// <summary>底部支援半径滑块条高度。</summary>
        const float BottomBarH = 36f;
        /// <summary>右侧选中信息面板宽度。</summary>
        const float InfoPanelW = 300f;
        const float ReinforceRadiusMin = 0.25f;
        const float ReinforceRadiusMax = 4f;
        /// <summary>Debug：大地图绘制支援半径圈。底栏滑块不受此开关影响。</summary>
        const bool ShowReinforcementRadiusDebug = false;

        enum MacroPartyKind
        {
            MoveOrAttack,
            LingeringView,
        }

        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] KeyCode toggleKey = KeyCode.M;
        [SerializeField] bool open;

        readonly HashSet<ulong> _selected = new HashSet<ulong>();
        readonly List<EntityId> _scratchParty = new List<EntityId>(8);
        readonly List<EntityId> _arrivedScratch = new List<EntityId>(8);
        readonly Dictionary<ulong, Rect> _avatarRects = new Dictionary<ulong, Rect>();
        readonly List<(string nodeId, Rect rect)> _nodeRects = new List<(string, Rect)>(64);
        readonly Dictionary<string, int> _slotAtNode = new Dictionary<string, int>();
        readonly Dictionary<string, int> _countAtNode = new Dictionary<string, int>();

        // 部队栈点选／右键菜单
        readonly Dictionary<string, Rect> _armyStackRects = new Dictionary<string, Rect>(16);
        string _selectedStackId = string.Empty;
        string _stackMenuStackId = string.Empty;
        Rect _stackMenuRect;
        bool _stackMenuOpen;
        /// <summary>攻击／移动／进入菜单的输出缓冲（可与选人缓冲分离）。</summary>
        readonly List<EntityId> _attackPartyScratch = new List<EntityId>(8);
        /// <summary>仅作「当前选中 → 过滤」中间表；禁止当作最终 into，避免 Clear 自清。</summary>
        readonly List<EntityId> _orderFilterScratch = new List<EntityId>(8);

        // 弥留头像右键：进入残留战场／有活人时「前往并进入」
        ulong _avatarMenuEntityId;
        bool _avatarMenuOpen;
        bool _avatarMenuVisitMode;
        Rect _avatarMenuRect;

        // 节点左键菜单
        string _nodeMenuNodeId = string.Empty;
        Rect _nodeMenuRect;
        bool _nodeMenuOpen;
        /// <summary>右侧信息面板聚焦的节点（左键点节点写入；与菜单开闭无关）。</summary>
        string _inspectNodeId = string.Empty;
        Vector2 _inspectScroll;

        string _status = string.Empty;
        bool _wasBlockingInput;
        int _travelingCountLast;

        // 地图镜头：世界坐标中心 + 半宽（世界单位）
        float _viewCx;
        float _viewCy;
        float _viewHalf;
        float _fullHalf = MinViewHalfExtent;
        bool _viewReady;
        bool _panning;
        Vector2 _panLastGui;
        bool _holdingPauseForMap;

        GUIStyle _title;
        GUIStyle _body;
        GUIStyle _nodeLabel;
        GUIStyle _avatarLabel;
        Texture2D _px;

        public bool IsOpen => open;

        public void Toggle()
        {
            if (open)
                Close();
            else
                Open();
        }

        /// <summary>
        /// 战场内（正常手动战／残留再进／手动战后未点结束）：禁止开大地图。
        /// 接战弹窗、自动战结算弹窗阶段仍可留在／打开大地图。
        /// </summary>
        public static bool IsBlockedByBattlefield(XianXia.Core.Simulation.SimulationWorld world)
        {
            if (world?.Strategic == null)
                return false;
            // 自动战结算：人还在战略层，结算 UI 应盖在大地图上，不要卸图跳到 LocalMap
            if (world.Strategic.Participants != null &&
                world.Strategic.Participants.IsAutoSettlement)
                return false;
            return StrategicClockFreezeService.IsModalEncounter(world) ||
                   BattleOfferService.HasActiveManualEncounter(world);
        }

        /// <summary>战后：清掉已腐烂选中；弥留／尸体仍可选中看情报（不可下令）；刷新战略层绘制状态。</summary>
        public void NotifyAfterBattleResolved(XianXia.Core.Simulation.SimulationWorld world)
        {
            if (world == null)
                return;
            StrategicEncounterResolveService.NormalizePresenceAfterEncounterExit(world);
            PruneRemovedFromSelection(world);
            // LocalMap 选中仍可能停在已倒下的人上 → FormalHud 左上角误显「弥留」
            bootstrap?.SelectionController?.ClearSelection();
            RefreshStrategicPresentation(world);
        }

        /// <summary>自动战／手动战后：重置大地图缓存与提示，确保残留弥留立刻可见。</summary>
        public void RefreshStrategicPresentation(XianXia.Core.Simulation.SimulationWorld world)
        {
            _avatarMenuOpen = false;
            _stackMenuOpen = false;
            _nodeMenuOpen = false;
            _inspectScroll = Vector2.zero;

            if (world?.Strategic?.Armies != null &&
                !string.IsNullOrEmpty(_selectedStackId) &&
                !world.Strategic.Armies.TryGet(_selectedStackId, out _))
                _selectedStackId = string.Empty;

            if (BattleOfferService.HasLingeringBattlefield(world))
                _status = "残留战场在接战点；敌军弥留／残留栈可左键选中";
            else if (_selected.Count == 0)
                _status = "战后请重新左键点选活人再移动";
        }

        public void Open()
        {
            if (bootstrap?.Session != null &&
                bootstrap.Session.IsInitialized &&
                IsBlockedByBattlefield(bootstrap.Session.World))
            {
                ShowBattlefieldMapBlockedToast();
                return;
            }

            open = true;
            _requestClose = false;
            if (bootstrap?.Session != null && bootstrap.Session.IsInitialized)
            {
                var world = bootstrap.Session.World;
                StrategicEncounterResolveService.NormalizePresenceAfterEncounterExit(world);
                PruneRemovedFromSelection(world);
            }

            // 开大地图不再强制暂停：战略时间由 Space／工具栏控制（RTS 开图仍可走时）
            _holdingPauseForMap = false;
        }

        /// <summary>大地图当前主选（活人优先）；供 FormalHud 在开图时不要误显 LocalMap 旧选中的弥留。</summary>
        public bool TryGetPrimarySelectedLiving(
            XianXia.Core.Simulation.SimulationWorld world,
            out EntityId id)
        {
            id = default;
            if (world == null || _selected.Count == 0)
                return false;
            foreach (var idVal in _selected)
            {
                var cand = new EntityId(idVal);
                if (LingeringBattlefieldPartyService.IsLivingForMacroOrder(world, cand))
                {
                    id = cand;
                    return true;
                }
            }

            return false;
        }

        /// <summary>到站弹窗「去查看」：打开后选中刚抵达的角色。</summary>
        public void SelectArrivedParty(IReadOnlyList<ulong> arrivedIds)
        {
            _selected.Clear();
            _selectedStackId = string.Empty;
            if (arrivedIds == null)
                return;
            for (var i = 0; i < arrivedIds.Count; i++)
            {
                if (arrivedIds[i] != 0)
                    _selected.Add(arrivedIds[i]);
            }

            if (_selected.Count > 0)
                _status = "已选到站 " + _selected.Count + " 人｜右键节点/道路移动";
        }

        /// <summary>探望弥留到站后：直接弹接战窗（半径内弥留强制纳入）。</summary>
        public void TryOpenPendingLingeringVisitAfterArrival()
        {
            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return;
            var world = bootstrap.Session.World;
            if (BattleOfferService.TryResolvePendingLingeringVisitOffer(
                    world, bootstrap.Session.CharacterIds))
            {
                if (!open)
                    Open();
                _status = "接战弹窗已打开";
            }
        }

        public void Close()
        {
            open = false;
            _requestClose = false;
            _nodeMenuOpen = false;
            _nodeMenuNodeId = string.Empty;
            _avatarMenuOpen = false;
            _avatarMenuVisitMode = false;
            _panning = false;
            ForceClearInputBlock();
            ReleaseMapPause();
        }

        void ReleaseMapPause()
        {
            if (!_holdingPauseForMap || bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
            {
                _holdingPauseForMap = false;
                return;
            }

            _holdingPauseForMap = false;
            bootstrap.Resume();
        }

        public void Bind(PlayableHostBootstrap host) => bootstrap = host;

        public void ClearSessionState()
        {
            _holdingPauseForMap = false;
            Close();
            _status = string.Empty;
            _selected.Clear();
            _travelingCountLast = 0;
            _viewReady = false;
            _selectedStackId = string.Empty;
            _stackMenuOpen = false;
            _nodeMenuOpen = false;
            _nodeMenuNodeId = string.Empty;
            _inspectNodeId = string.Empty;
            _inspectScroll = Vector2.zero;
        }

        bool _requestClose;
        string _blockToast = string.Empty;
        float _blockToastUntil;

        void ShowBattlefieldMapBlockedToast()
        {
            _blockToast = "战场进行中：请先结束战斗，再打开大地图";
            _blockToastUntil = Time.unscaledTime + 2.8f;
            _status = _blockToast;
        }

        void Update()
        {
            if (_requestClose)
                Close();

            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return;

            var world = bootstrap.Session.World;
            // 进战场后大地图一律关掉且不可再开（正常战／残留再进／未点结束）
            if (IsBlockedByBattlefield(world))
            {
                if (open)
                    Close();
                if (Input.GetKeyDown(toggleKey))
                    ShowBattlefieldMapBlockedToast();
                return;
            }

            if (OtherBlockingPanelOpen())
            {
                if (open)
                    Close();
                return;
            }

            if (Input.GetKeyDown(toggleKey))
            {
                if (open)
                    Close();
                else
                {
                    Open();
                    if (bootstrap.InventoryPanel != null && bootstrap.InventoryPanel.IsOpen)
                        bootstrap.InventoryPanel.Close();
                    if (bootstrap.QuestJournal != null && bootstrap.QuestJournal.IsOpen)
                        bootstrap.QuestJournal.Close();
                    _viewReady = false;
                }
            }

            if (open)
            {
                HostInputGate.BlockWorldCamera = true;
                HostInputGate.BlockWorldInteraction = true;
                _wasBlockingInput = true;
                WatchArrivals();
            }
            else
            {
                if (_wasBlockingInput)
                    ForceClearInputBlock();
                _panning = false;
            }
        }

        void ForceClearInputBlock()
        {
            _wasBlockingInput = false;
            HostInputGate.Clear();
        }

        void ClearInputBlock() => ForceClearInputBlock();

        void WatchArrivals()
        {
            var world = bootstrap.Session.World;
            var traveling = 0;
            foreach (var kv in world.WorldPresence.All)
            {
                if (kv.Value != null && kv.Value.Mode == PartyWorldPresenceMode.Traveling)
                    traveling++;
            }

            if (_travelingCountLast > 0 && traveling < _travelingCountLast)
            {
                WorldTravelService.SyncPartyFocus(world);
                _status = "有人抵达站点";
            }

            _travelingCountLast = traveling;
        }

        bool OtherBlockingPanelOpen()
        {
            var j = bootstrap.QuestJournal;
            var inv = bootstrap.InventoryPanel;
            return (j != null && j.IsOpen) || (inv != null && inv.IsOpen);
        }

        void EnsureView(WorldGraphBoard graph, XianXia.Core.Simulation.SimulationWorld world)
        {
            ComputeFullHalf(graph, out _fullHalf);
            if (_viewReady)
            {
                _viewHalf = Mathf.Clamp(_viewHalf, MinViewHalfExtent, _fullHalf);
                return;
            }

            _viewHalf = MinViewHalfExtent;
            _viewCx = 0f;
            _viewCy = 0f;
            var focusId = world.PartyWorld.NodeId;
            if (!string.IsNullOrEmpty(focusId) && graph.TryGetNode(focusId, out var focus))
            {
                _viewCx = focus.WorldX;
                _viewCy = focus.WorldY;
            }

            _viewReady = true;
        }

        static void ComputeFullHalf(WorldGraphBoard graph, out float fullHalf)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (var kv in graph.Nodes)
            {
                minX = Mathf.Min(minX, kv.Value.WorldX);
                maxX = Mathf.Max(maxX, kv.Value.WorldX);
                minY = Mathf.Min(minY, kv.Value.WorldY);
                maxY = Mathf.Max(maxY, kv.Value.WorldY);
            }

            if (maxX < minX)
            {
                fullHalf = MinViewHalfExtent;
                return;
            }

            var half = Mathf.Max((maxX - minX) * 0.5f, (maxY - minY) * 0.5f) + 1.5f;
            fullHalf = Mathf.Max(MinViewHalfExtent, half);
        }

        void OnGUI()
        {
            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return;

            if (!open)
            {
                DrawBattlefieldMapBlockedToastIfNeeded();
                return;
            }

            EnsureStyles();

            GUI.depth = -80;
            HostUiHitTest.BeginFrame();
            HostUiHitTest.Block(new Rect(0f, 0f, Screen.width, Screen.height));

            var prev = GUI.color;
            GUI.color = new Color(0.08f, 0.09f, 0.11f, 0.97f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _px);
            GUI.color = prev;

            const float topBar = 48f;
            const float pad = 16f;

            var world = bootstrap.Session.World;
            var graph = world.WorldGraph;

            GUI.Label(
                new Rect(pad, 12f, Screen.width - 220f, 28f),
                "大地图  " + (string.IsNullOrEmpty(graph.GraphName) ? graph.GraphId : graph.GraphName) +
                "  （左键选中｜右键下令：移动/攻击/进入残留｜M 关闭）",
                _title);

            if (GUI.Button(new Rect(Screen.width - 100f, 10f, 84f, 32f), "关闭"))
                Close();

            DrawMapToolbar(pad, topBar, world);

            if (!graph.HasGraph)
            {
                GUI.Label(new Rect(pad, topBar, Screen.width - pad * 2f, 40f), "未加载 WorldGraph。", _body);
                return;
            }

            EnsureView(graph, world);

            var focusName = world.PartyWorld.NodeId;
            if (graph.TryGetNode(world.PartyWorld.NodeId, out var focusNode))
                focusName = string.IsNullOrEmpty(focusNode.Name) ? focusNode.Id : focusNode.Name;

            var zoomPct = Mathf.Approximately(_fullHalf, MinViewHalfExtent)
                ? 100
                : Mathf.RoundToInt(100f * (1f - (_viewHalf - MinViewHalfExtent) / (_fullHalf - MinViewHalfExtent)));

            GUI.Label(
                new Rect(pad, topBar, Screen.width - pad * 2f - InfoPanelW - 8f, 22f),
                "镜头：" + focusName +
                "　已选 " + _selected.Count +
                "　缩放 " + zoomPct + "%（最大：邻站铺满屏／最小：全图）" +
                (string.IsNullOrEmpty(_status) ? "" : "　｜　" + _status),
                _body);

            var mapTop = topBar + 56f;
            var mapRect = new Rect(
                pad,
                mapTop,
                Screen.width - pad * 2f - InfoPanelW - 8f,
                Screen.height - mapTop - pad - BottomBarH);
            var infoRect = new Rect(
                mapRect.xMax + 8f,
                mapTop,
                InfoPanelW,
                mapRect.height);
            GUI.color = new Color(0.12f, 0.14f, 0.16f, 1f);
            GUI.DrawTexture(mapRect, _px);
            GUI.color = Color.white;

            HandleCameraInput(mapRect);
            DrawGraph(mapRect, world, graph);
            if (ShowReinforcementRadiusDebug)
                DrawReinforcementRadiusOverlay(mapRect, world);
            DrawNodeContextMenu(mapRect, world, graph);
            DrawStackContextMenu(world, graph);
            DrawAvatarContextMenu(world);
            DrawInspectPanel(infoRect, world, graph);
            DrawReinforcementRadiusSlider(pad, world);
            TryDismissContextMenusOnOutsideClick();
            if (Event.current != null && Event.current.type == EventType.Used)
                return;
            // 菜单仍开着（点在菜单内）时不处理地图下令；外侧点击已在上面关掉菜单且不吞事件
            if (_stackMenuOpen || _nodeMenuOpen || _avatarMenuOpen)
                return;
            HandleMapInput(mapRect, world, graph);
            HostUiHitTest.EndFrame();
            // 进入场景可能在本帧 OnGUI 中途关掉；立刻停画，避免同帧再盖一层
            if (!open)
                return;
        }

        void HandleCameraInput(Rect mapRect)
        {
            var e = Event.current;
            if (e == null || !mapRect.Contains(e.mousePosition) && e.type != EventType.MouseUp)
                return;

            if (e.type == EventType.ScrollWheel && mapRect.Contains(e.mousePosition))
            {
                // 滚轮：以鼠标下世界点为锚缩放
                ScreenToWorld(mapRect, e.mousePosition, out var wx, out var wy);
                var before = _viewHalf;
                var factor = e.delta.y > 0f ? 1.12f : 1f / 1.12f;
                _viewHalf = Mathf.Clamp(before * factor, MinViewHalfExtent, _fullHalf);
                // 保持锚点屏幕位置不变
                var t = 1f - _viewHalf / before;
                if (before > 0.01f)
                {
                    _viewCx += (wx - _viewCx) * t;
                    _viewCy += (wy - _viewCy) * t;
                }

                e.Use();
                return;
            }

            if (e.type == EventType.MouseDown && e.button == 2 && mapRect.Contains(e.mousePosition))
            {
                _panning = true;
                _panLastGui = e.mousePosition;
                e.Use();
                return;
            }

            if (e.type == EventType.MouseDrag && _panning && e.button == 2)
            {
                var delta = e.mousePosition - _panLastGui;
                _panLastGui = e.mousePosition;
                var scale = MapScale(mapRect);
                // GUI Y 向下，世界 Y 向上
                _viewCx -= delta.x / scale;
                _viewCy += delta.y / scale;
                e.Use();
                return;
            }

            if (e.type == EventType.MouseUp && e.button == 2)
            {
                _panning = false;
                e.Use();
            }
        }

        void DrawBattlefieldMapBlockedToastIfNeeded()
        {
            if (string.IsNullOrEmpty(_blockToast) || Time.unscaledTime > _blockToastUntil)
                return;
            EnsureStyles();
            var rect = new Rect(Screen.width * 0.5f - 240f, 72f, 480f, 32f);
            var prevDepth = GUI.depth;
            GUI.depth = -95;
            var prev = GUI.color;
            GUI.color = new Color(0.1f, 0.12f, 0.14f, 0.92f);
            GUI.DrawTexture(rect, _px != null ? _px : Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 12f, rect.y + 6f, rect.width - 24f, 22f), _blockToast, _body);
            GUI.color = prev;
            GUI.depth = prevDepth;
        }

        float MapScale(Rect mapRect)
        {
            var innerW = mapRect.width - MapPad * 2f;
            var innerH = mapRect.height - MapPad * 2f;
            return Mathf.Min(innerW, innerH) / (2f * Mathf.Max(0.01f, _viewHalf));
        }

        Vector2 Project(Rect mapRect, float wx, float wy)
        {
            var scale = MapScale(mapRect);
            var cx = mapRect.x + mapRect.width * 0.5f;
            var cy = mapRect.y + mapRect.height * 0.5f;
            return new Vector2(
                cx + (wx - _viewCx) * scale,
                cy - (wy - _viewCy) * scale);
        }

        void ScreenToWorld(Rect mapRect, Vector2 gui, out float wx, out float wy)
        {
            var scale = MapScale(mapRect);
            var cx = mapRect.x + mapRect.width * 0.5f;
            var cy = mapRect.y + mapRect.height * 0.5f;
            wx = _viewCx + (gui.x - cx) / scale;
            wy = _viewCy - (gui.y - cy) / scale;
        }

        void DrawMapToolbar(float pad, float topBar, XianXia.Core.Simulation.SimulationWorld world)
        {
            var y = topBar + 2f;
            var x = pad;
            var paused = bootstrap.Session.IsPaused;
            var pauseLabel = paused ? "继续 (Space)" : "暂停 (Space)";
            if (GUI.Button(new Rect(x, y, 120f, 26f), pauseLabel))
            {
                if (paused)
                    bootstrap.Resume();
                else
                    bootstrap.Pause();
            }

            x += 128f;
            var speed = bootstrap.EffectiveSpeedMultiplier();
            if (GUI.Button(new Rect(x, y, 72f, 26f), speed + "x"))
                bootstrap.SetSpeedMultiplier(CycleSpeedValue(speed));
            x += 80f;
            GUI.Label(new Rect(x, y + 4f, 88f, 22f), "[ / ] 调倍速", _body);
            x += 96f;

            // Debug：单人自动战强制弥留（测残留／弥留流程）
            var forceSolo = AutoBattleCasualtyService.DebugForceSoloAutoBattleIncapacitated;
            var nextForce = GUI.Toggle(
                new Rect(x, y + 2f, 220f, 24f),
                forceSolo,
                " Debug：单人自动战必弥留");
            if (nextForce != forceSolo)
                AutoBattleCasualtyService.DebugForceSoloAutoBattleIncapacitated = nextForce;

            if (world.Strategic != null &&
                (world.Strategic.HasBlockingInterrupt ||
                 StrategicClockFreezeService.IsWorldTickFrozen(world)))
            {
                x += 230f;
                var reason = world.Strategic.ClockFreeze != null
                    ? world.Strategic.ClockFreeze.Reason.ToString()
                    : "?";
                GUI.Label(
                    new Rect(x, y + 4f, 320f, 22f),
                    world.Strategic.HasBlockingInterrupt
                        ? "战略打断中…"
                        : "战略时间冻结：" + reason,
                    _body);
            }
        }

        void DrawReinforcementRadiusSlider(float pad, XianXia.Core.Simulation.SimulationWorld world)
        {
            if (world?.Strategic == null)
                return;

            var bar = new Rect(
                pad,
                Screen.height - BottomBarH - 8f,
                Screen.width - pad * 2f,
                BottomBarH);
            HostUiHitTest.Block(bar);

            var prev = GUI.color;
            GUI.color = new Color(0.14f, 0.15f, 0.17f, 0.96f);
            GUI.DrawTexture(bar, _px);
            GUI.color = prev;

            var radius = ReinforcementRangeService.GetWorldRadius(world);
            GUI.Label(
                new Rect(bar.x + 10f, bar.y + 8f, 118f, 22f),
                "支援半径 " + radius.ToString("0.00"),
                _body);

            var sliderRect = new Rect(bar.x + 128f, bar.y + 10f, Mathf.Max(120f, bar.width - 220f), 18f);
            var next = GUI.HorizontalSlider(sliderRect, radius, ReinforceRadiusMin, ReinforceRadiusMax);
            // 步进 0.05，避免浮点抖动
            next = Mathf.Round(next * 20f) / 20f;
            if (!Mathf.Approximately(next, radius))
                world.Strategic.ReinforcementWorldRadius = next;

            if (GUI.Button(new Rect(bar.xMax - 72f, bar.y + 6f, 62f, 24f), "默认"))
                world.Strategic.ReinforcementWorldRadius = ReinforcementRangeService.DefaultWorldRadius;
        }

        /// <summary>以当前选中单位／敌军栈为圆心画支援半径圈（世界坐标）。</summary>
        void DrawReinforcementRadiusOverlay(Rect mapRect, XianXia.Core.Simulation.SimulationWorld world)
        {
            if (world?.Strategic == null)
                return;
            if (!TryGetReinforceOverlayCenter(world, out var cx, out var cy))
                return;

            var radius = ReinforcementRangeService.GetWorldRadius(world);
            if (radius <= 0f)
                return;

            var center = Project(mapRect, cx, cy);
            var edge = Project(mapRect, cx + radius, cy);
            var pixelR = Mathf.Abs(edge.x - center.x);
            if (pixelR < 4f)
                return;

            var prev = GUI.color;
            GUI.color = new Color(0.35f, 0.72f, 0.55f, 0.22f);
            // 半透明填充（近似：中心小方块叠圆环感弱，用环线为主）
            DrawWireCircle(center, pixelR, mapRect, 48);
            GUI.color = new Color(0.35f, 0.72f, 0.55f, 0.85f);
            DrawWireCircle(center, pixelR, mapRect, 64);
            GUI.color = prev;
        }

        bool TryGetReinforceOverlayCenter(
            XianXia.Core.Simulation.SimulationWorld world,
            out float cx,
            out float cy)
        {
            cx = 0f;
            cy = 0f;

            // 优先：已选敌军栈（接战锚点）
            if (!string.IsNullOrEmpty(_selectedStackId) &&
                world.Strategic.Armies.TryGet(_selectedStackId, out var stack) &&
                stack != null)
            {
                if (TryResolveStackWorldXY(world, stack, out cx, out cy))
                    return true;
            }

            // 其次：已选己方头像
            if (_selected.Count > 0)
            {
                foreach (var idVal in _selected)
                {
                    var id = new EntityId(idVal);
                    if (!world.WorldPresence.TryGet(id, out var wp) || wp == null)
                        continue;
                    if (ReinforcementRangeService.TryGetPresenceWorldXY(world, wp, out cx, out cy))
                        return true;
                }
            }

            // 再次：接战 Offer 锚点
            var snap = world.Strategic.Participants;
            if (snap != null &&
                (!string.IsNullOrEmpty(snap.BattleAnchorNodeId) ||
                 !string.IsNullOrEmpty(snap.BattleAnchorRouteId)))
            {
                return ReinforcementRangeService.TryGetAnchorWorldXY(
                    world,
                    snap.BattleAnchorNodeId,
                    snap.BattleAnchorRouteId,
                    snap.BattleAnchorProgress,
                    out cx,
                    out cy);
            }

            return false;
        }

        static bool TryResolveStackWorldXY(
            XianXia.Core.Simulation.SimulationWorld world,
            ArmyStack stack,
            out float cx,
            out float cy)
        {
            cx = 0f;
            cy = 0f;
            if (stack == null || world?.WorldGraph == null)
                return false;
            if (stack.IsRoutePositioned)
            {
                return ReinforcementRangeService.TryGetAnchorWorldXY(
                    world,
                    stack.NodeId,
                    stack.RouteId,
                    stack.GetRouteDisplayProgress(),
                    out cx,
                    out cy);
            }

            if (string.IsNullOrEmpty(stack.NodeId) ||
                !world.WorldGraph.TryGetNode(stack.NodeId, out var node) ||
                node == null)
                return false;
            cx = node.WorldX;
            cy = node.WorldY;
            return true;
        }

        void DrawWireCircle(Vector2 center, float radiusPx, Rect clip, int segments)
        {
            if (radiusPx < 1f || segments < 8)
                return;
            var step = Mathf.PI * 2f / segments;
            Vector2 prev = default;
            for (var i = 0; i <= segments; i++)
            {
                var a = i * step;
                var p = new Vector2(
                    center.x + Mathf.Cos(a) * radiusPx,
                    center.y + Mathf.Sin(a) * radiusPx);
                if (i > 0)
                    DrawClippedSegment(prev, p, clip);
                prev = p;
            }
        }

        void DrawClippedSegment(Vector2 a, Vector2 b, Rect clip)
        {
            // 粗略：两端都在外则跳过；否则画细线
            if (!clip.Contains(a) && !clip.Contains(b))
            {
                var mid = (a + b) * 0.5f;
                if (!clip.Contains(mid))
                    return;
            }

            var dx = b.x - a.x;
            var dy = b.y - a.y;
            var len = Mathf.Sqrt(dx * dx + dy * dy);
            if (len < 0.5f)
                return;
            var angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, a);
            GUI.DrawTexture(new Rect(a.x, a.y - 1f, len, 2f), _px);
            GUI.matrix = matrix;
        }

        static int CycleSpeedValue(int current)
        {
            if (current <= 1)
                return 2;
            if (current <= 2)
                return 5;
            if (current <= 5)
                return 20;
            return 1;
        }

        void DrawGraph(Rect mapRect, XianXia.Core.Simulation.SimulationWorld world, WorldGraphBoard graph)
        {
            foreach (var kv in graph.Routes)
            {
                var r = kv.Value;
                if (!graph.TryGetNode(r.FromNodeId, out var a) || !graph.TryGetNode(r.ToNodeId, out var b))
                    continue;
                var pa = Project(mapRect, a.WorldX, a.WorldY);
                var pb = Project(mapRect, b.WorldX, b.WorldY);
                if (!SegmentNearMap(mapRect, pa, pb))
                    continue;
                DrawLine(pa, pb, new Color(0.55f, 0.5f, 0.4f, 0.85f));
            }

            _nodeRects.Clear();
            // 头像底层 → 节点地名 → 敌军栈置顶（避免被荒村大标签挡住）
            DrawAvatars(mapRect, world);

            foreach (var kv in graph.Nodes)
            {
                var n = kv.Value;
                var p = Project(mapRect, n.WorldX, n.WorldY);
                var rect = new Rect(p.x - NodeHitW * 0.5f, p.y - NodeHitH * 0.5f, NodeHitW, NodeHitH);
                if (!rect.Overlaps(mapRect))
                    continue;

                var isFocus = string.Equals(n.Id, world.PartyWorld.NodeId, System.StringComparison.Ordinal);
                var label = (isFocus ? "● " : "") + (string.IsNullOrEmpty(n.Name) ? n.Id : n.Name);
                // 暂不按势力 Owner 染色／标注（外交未启用）
                var boxC = isFocus
                    ? new Color(0.35f, 0.42f, 0.28f, 0.95f)
                    : new Color(0.22f, 0.24f, 0.27f, 0.92f);
                var old = GUI.color;
                GUI.color = boxC;
                GUI.DrawTexture(rect, _px);
                GUI.color = old;
                GUI.Label(rect, label, _nodeLabel);
                _nodeRects.Add((n.Id, rect));
            }

            DrawArmyStacks(mapRect, world, graph);
            DrawLingeringIncapAvatars(mapRect, world);
        }

        void DrawArmyStacks(
            Rect mapRect,
            XianXia.Core.Simulation.SimulationWorld world,
            WorldGraphBoard graph)
        {
            _armyStackRects.Clear();
            if (world.Strategic?.Armies == null)
                return;

            foreach (var kv in world.Strategic.Armies.Stacks)
            {
                var stack = kv.Value;
                if (stack == null)
                    continue;
                // 弥留／尸体已由个体头像绘制，不再叠聚合栈标记
                if (stack.HasDownedRemnant)
                    continue;

                float wx;
                float wy;
                if (!TryResolveArmyStackWorldPoint(world, graph, stack, out wx, out wy))
                    continue;

                var p = Project(mapRect, wx, wy);
                // 贴节点时挪到标签外侧，避免「躲在荒村后面」
                p = NudgeArmyMarkerAwayFromNodes(p);
                if (!mapRect.Contains(p))
                    continue;

                StrategicFactionCatalog.MapTint(stack.FactionId, out var r, out var g, out var b);
                var size = string.Equals(stack.Id, _selectedStackId, StringComparison.Ordinal) ? 26f : 22f;
                var rect = new Rect(p.x - size * 0.5f, p.y - size * 0.5f, size, size);
                var old = GUI.color;
                GUI.color = new Color(r, g, b, 0.95f);
                GUI.DrawTexture(rect, _px);
                if (string.Equals(stack.Id, _selectedStackId, StringComparison.Ordinal))
                {
                    GUI.color = Color.white;
                    GUI.DrawTexture(new Rect(rect.x - 2f, rect.y - 2f, rect.width + 4f, rect.height + 4f), _px);
                }

                GUI.color = old;
                var tag = stack.HasCorpseRemnant
                    ? stack.CorpseMemberCount + "人·尸体"
                    : stack.HasIncapacitatedRemnant
                        ? stack.IncapacitatedMemberCount + "人·弥留"
                        : stack.MemberCount + "人 · " +
                          (string.IsNullOrEmpty(stack.DisplayName) ? "敌军" : stack.DisplayName);
                if (stack.HasCorpseRemnant)
                {
                    GUI.color = new Color(0.42f, 0.36f, 0.30f, 0.88f);
                    GUI.DrawTexture(new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, rect.height - 8f), _px);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(rect.x, rect.y - 2f, rect.width, rect.height), "尸", _avatarLabel);
                    GUI.color = old;
                }
                else if (stack.HasIncapacitatedRemnant)
                {
                    GUI.color = new Color(r * 0.75f, g * 0.75f, b * 0.75f, 0.88f);
                    GUI.DrawTexture(new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, rect.height - 8f), _px);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(rect.x, rect.y - 2f, rect.width, rect.height), "弥", _avatarLabel);
                    GUI.color = old;
                }

                GUI.Label(new Rect(rect.x - 12f, rect.yMax + 2f, 120f, 18f), tag, _avatarLabel);
                _armyStackRects[stack.Id] = rect;
            }
        }

        Vector2 NudgeArmyMarkerAwayFromNodes(Vector2 screenPos)
        {
            for (var i = 0; i < _nodeRects.Count; i++)
            {
                var nr = _nodeRects[i].rect;
                var pad = new Rect(
                    nr.x - 10f,
                    nr.y - 10f,
                    nr.width + 20f,
                    nr.height + 28f);
                if (!pad.Contains(screenPos))
                    continue;
                // 挪到节点标签右侧偏上，与玩家头像（多在顶侧）错开
                return new Vector2(nr.xMax + 16f, nr.yMin - 8f);
            }

            return screenPos;
        }

        void DrawLingeringIncapAvatars(
            Rect mapRect,
            XianXia.Core.Simulation.SimulationWorld world)
        {
            var rt = world.Strategic?.Encounter;
            if (rt == null)
                return;

            var trackedDowned = CountTrackedEnemyDownedSpawns(world, rt);
            TryGetEncounterRemnantStack(world, out var remnantStack);
            if (!rt.BattlefieldLingering && trackedDowned <= 0 &&
                (remnantStack == null || !remnantStack.HasDownedRemnant))
                return;

            // 敌军弥留：跟对应势力栈同色
            var factionId = string.Empty;
            if (remnantStack != null && !string.IsNullOrEmpty(remnantStack.FactionId))
                factionId = remnantStack.FactionId;
            else if (!string.IsNullOrEmpty(rt.ArmyStackId) &&
                world.Strategic.Armies.TryGet(rt.ArmyStackId, out var stack) &&
                stack != null &&
                !string.IsNullOrEmpty(stack.FactionId))
                factionId = stack.FactionId;
            else if (world.Strategic.Participants != null &&
                     !string.IsNullOrEmpty(world.Strategic.Participants.PrimaryEnemyStackId) &&
                     world.Strategic.Armies.TryGet(
                         world.Strategic.Participants.PrimaryEnemyStackId, out var primary) &&
                     primary != null &&
                     !string.IsNullOrEmpty(primary.FactionId))
                factionId = primary.FactionId;

            StrategicFactionCatalog.MapTint(factionId, out var fr, out var fg, out var fb);
            var factionFill = new Color(fr, fg, fb, 0.9f);
            var factionSelected = new Color(
                Mathf.Min(1f, fr * 1.15f + 0.08f),
                Mathf.Min(1f, fg * 1.15f + 0.08f),
                Mathf.Min(1f, fb * 1.15f + 0.08f),
                0.95f);

            for (var i = 0; i < rt.SpawnedEntityIds.Count; i++)
            {
                var id = new EntityId(rt.SpawnedEntityIds[i]);
                if (!world.Entities.TryGet(id, out var spawnEnt) ||
                    !spawnEnt.TryGet<LifecycleComponent>(out var spawnLife) ||
                    spawnLife.IsRemoved)
                    continue;
                var isIncap = spawnLife.IsIncapacitated;
                var isDead = spawnLife.IsDead;
                if (!isIncap && !isDead)
                    continue;
                if (!world.WorldPresence.TryGet(id, out var presence) || presence == null)
                    continue;
                if (!WorldTravelService.TryResolveTravelWorldPoints(
                        world, presence, out var fx, out var fy, out var tx, out var ty))
                    continue;
                float wx = fx, wy = fy;
                if (presence.HasRoutePresentation)
                {
                    var t = presence.Mode == PartyWorldPresenceMode.RouteAnchored
                        ? Mathf.Clamp01(presence.RouteAnchorProgress)
                        : Mathf.Clamp01(presence.TravelProgress);
                    wx = Mathf.Lerp(fx, tx, t);
                    wy = Mathf.Lerp(fy, ty, t);
                }

                var p = Project(mapRect, wx, wy);
                // 敌军弥留／尸体「标」略下移，少挡我方活人
                var rect = new Rect(
                    p.x - AvatarSize * 0.5f + 8f,
                    p.y - AvatarSize * 0.5f + AvatarSize * 0.55f,
                    AvatarSize,
                    AvatarSize);
                if (!rect.Overlaps(mapRect))
                    continue;
                _avatarRects[id.Value] = rect;
                var selected = _selected.Contains(id.Value);
                var old = GUI.color;
                if (isDead)
                {
                    GUI.color = selected
                        ? new Color(0.55f, 0.48f, 0.40f, 0.92f)
                        : new Color(0.42f, 0.36f, 0.30f, 0.82f);
                }
                else
                    GUI.color = selected ? factionSelected : factionFill;
                GUI.DrawTexture(rect, _px);
                if (selected)
                {
                    GUI.color = new Color(1f, 0.95f, 0.55f, 0.85f);
                    GUI.DrawTexture(new Rect(rect.x - 2f, rect.y - 2f, rect.width + 4f, 3f), _px);
                    GUI.DrawTexture(new Rect(rect.x - 2f, rect.yMax - 1f, rect.width + 4f, 3f), _px);
                    GUI.DrawTexture(new Rect(rect.x - 2f, rect.y, 3f, rect.height), _px);
                    GUI.DrawTexture(new Rect(rect.xMax - 1f, rect.y, 3f, rect.height), _px);
                }

                GUI.color = Color.white;
                GUI.Label(rect, isDead ? "尸" : "弥", _avatarLabel);
                GUI.color = old;
            }

            // 自动战未进 LocalMap：尚无 Spawn 实体，用抽象栈位置画弥留标
            if (trackedDowned <= 0 &&
                remnantStack != null &&
                remnantStack.HasDownedRemnant &&
                bootstrap?.Session?.World?.WorldGraph != null &&
                TryResolveArmyStackWorldPoint(
                    world,
                    bootstrap.Session.World.WorldGraph,
                    remnantStack,
                    out var rwx,
                    out var rwy))
            {
                var count = remnantStack.HasCorpseRemnant
                    ? remnantStack.CorpseMemberCount
                    : remnantStack.IncapacitatedMemberCount;
                DrawAbstractRemnantEnemyMarkers(
                    mapRect,
                    rwx,
                    rwy,
                    count,
                    remnantStack.HasCorpseRemnant,
                    remnantStack.HasCorpseRemnant
                        ? new Color(0.42f, 0.36f, 0.30f, 0.82f)
                        : factionFill);
            }
        }

        static int CountTrackedEnemyDownedSpawns(
            XianXia.Core.Simulation.SimulationWorld world,
            StrategicEncounterRuntime rt)
        {
            if (world == null || rt == null)
                return 0;
            var n = 0;
            for (var i = 0; i < rt.SpawnedEntityIds.Count; i++)
            {
                var id = new EntityId(rt.SpawnedEntityIds[i]);
                if (LingeringBattlefieldPartyService.IsLingeringDowned(world, id))
                    n++;
            }

            return n;
        }

        static bool TryGetEncounterRemnantStack(
            XianXia.Core.Simulation.SimulationWorld world,
            out ArmyStack stack)
        {
            stack = null;
            if (world?.Strategic?.Armies == null)
                return false;
            var rt = world.Strategic.Encounter;
            if (rt != null && !string.IsNullOrEmpty(rt.ArmyStackId) &&
                world.Strategic.Armies.TryGet(rt.ArmyStackId, out stack) &&
                stack != null &&
                stack.HasDownedRemnant)
                return true;

            var primary = world.Strategic.Participants?.PrimaryEnemyStackId;
            if (!string.IsNullOrEmpty(primary) &&
                world.Strategic.Armies.TryGet(primary, out stack) &&
                stack != null &&
                stack.HasDownedRemnant)
                return true;

            stack = null;
            return false;
        }

        static bool TryResolveArmyStackWorldPoint(
            XianXia.Core.Simulation.SimulationWorld world,
            WorldGraphBoard graph,
            ArmyStack stack,
            out float wx,
            out float wy)
        {
            wx = 0f;
            wy = 0f;
            if (stack == null || graph == null)
                return false;

            if (stack.IsRoutePositioned &&
                graph.TryGetRoute(stack.RouteId, out var route) &&
                route != null &&
                graph.TryGetNode(stack.NodeId, out var from))
            {
                var toId = stack.DestNodeId;
                if (string.IsNullOrEmpty(toId))
                {
                    if (string.Equals(route.FromNodeId, stack.NodeId, StringComparison.Ordinal))
                        toId = route.ToNodeId ?? string.Empty;
                    else if (string.Equals(route.ToNodeId, stack.NodeId, StringComparison.Ordinal))
                        toId = route.FromNodeId ?? string.Empty;
                }

                if (!string.IsNullOrEmpty(toId) && graph.TryGetNode(toId, out var to))
                {
                    var t = Mathf.Clamp01(stack.GetRouteDisplayProgress());
                    wx = from.WorldX + (to.WorldX - from.WorldX) * t;
                    wy = from.WorldY + (to.WorldY - from.WorldY) * t;
                    return true;
                }
            }

            if (!string.IsNullOrEmpty(stack.NodeId) &&
                graph.TryGetNode(stack.NodeId, out var node))
            {
                wx = node.WorldX;
                wy = node.WorldY;
                return true;
            }

            return false;
        }

        void DrawAbstractRemnantEnemyMarkers(
            Rect mapRect,
            float wx,
            float wy,
            int memberCount,
            bool asCorpse,
            Color fill)
        {
            var total = Math.Max(1, memberCount);
            var p = Project(mapRect, wx, wy);
            const float spacing = AvatarSize + 3f;
            var x0 = -(total - 1) * 0.5f * spacing;
            var label = asCorpse ? "尸" : "弥";
            for (var i = 0; i < total; i++)
            {
                var center = p + new Vector2(x0 + i * spacing, AvatarSize * 0.55f);
                var rect = new Rect(
                    center.x - AvatarSize * 0.5f + 8f,
                    center.y - AvatarSize * 0.5f,
                    AvatarSize,
                    AvatarSize);
                if (!rect.Overlaps(mapRect))
                    continue;
                var old = GUI.color;
                GUI.color = fill;
                GUI.DrawTexture(rect, _px);
                GUI.color = Color.white;
                GUI.Label(rect, label, _avatarLabel);
                GUI.color = old;
            }
        }

        void DrawAvatars(Rect mapRect, XianXia.Core.Simulation.SimulationWorld world)
        {
            _avatarRects.Clear();
            var ids = bootstrap.Session.CharacterIds;
            if (ids == null)
                return;

            // 先统计同节点人数，便于紧贴居中排列
            _countAtNode.Clear();
            for (var i = 0; i < ids.Count; i++)
            {
                if (!world.WorldPresence.TryGet(ids[i], out var p) || p == null)
                    continue;
                if (p.Mode == PartyWorldPresenceMode.Traveling ||
                    p.Mode == PartyWorldPresenceMode.RouteAnchored ||
                    (p.Mode == PartyWorldPresenceMode.InEncounter && p.HasRoutePresentation))
                    continue;
                var key = p.NodeId ?? "";
                _countAtNode.TryGetValue(key, out var c);
                _countAtNode[key] = c + 1;
            }

            _slotAtNode.Clear();
            for (var i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                // 腐烂／Removed：大地图彻底不画、不占位
                if (world.Entities.TryGet(id, out var lifeEnt) &&
                    lifeEnt.TryGet<LifecycleComponent>(out var life) &&
                    life.IsRemoved)
                    continue;
                if (!world.WorldPresence.TryGet(id, out var presence) || presence == null)
                    continue;
                if (!WorldTravelService.TryResolveTravelWorldPoints(
                        world, presence, out var fx, out var fy, out var tx, out var ty))
                    continue;

                float wx = fx, wy = fy;
                if (presence.HasRoutePresentation)
                {
                    var t = presence.TravelProgress;
                    wx = Mathf.Lerp(fx, tx, t);
                    wy = Mathf.Lerp(fy, ty, t);
                }

                var basePos = Project(mapRect, wx, wy);
                Vector2 center;
                if (presence.HasRoutePresentation)
                {
                    _slotAtNode.TryGetValue("t:" + id.Value, out var slot);
                    _slotAtNode["t:" + id.Value] = slot + 1;
                    // 路锚叠位：拉开间距，弥留下移，避免点选总打到弥留
                    center = basePos + new Vector2((slot % 3) * (AvatarSize * 0.55f) - AvatarSize * 0.55f,
                        -AvatarSize * 0.55f);
                    if (LingeringBattlefieldPartyService.IsLingeringDowned(world, id))
                        center += new Vector2(AvatarSize * 0.35f, AvatarSize * 0.7f);
                }
                else
                {
                    var key = presence.NodeId ?? "";
                    _slotAtNode.TryGetValue(key, out var slot);
                    _slotAtNode[key] = slot + 1;
                    _countAtNode.TryGetValue(key, out var total);
                    if (total < 1)
                        total = 1;
                    // 紧贴节点「头顶」外侧，按人数水平居中
                    const float gap = 2f;
                    const float spacing = AvatarSize + 3f;
                    var rowY = -(NodeHitH * 0.5f + gap + AvatarSize * 0.5f);
                    var x0 = -(total - 1) * 0.5f * spacing;
                    center = basePos + new Vector2(x0 + slot * spacing, rowY);
                    if (LingeringBattlefieldPartyService.IsLingeringDowned(world, id))
                        center += new Vector2(0f, AvatarSize * 0.55f);
                }

                var rect = new Rect(
                    center.x - AvatarSize * 0.5f,
                    center.y - AvatarSize * 0.5f,
                    AvatarSize,
                    AvatarSize);
                if (!rect.Overlaps(mapRect))
                    continue;

                _avatarRects[id.Value] = rect;

                var selected = _selected.Contains(id.Value);
                var onRoute = presence.HasRoutePresentation;
                var inEncounter = presence.Mode == PartyWorldPresenceMode.InEncounter;
                var incap = LingeringBattlefieldPartyService.IsIncapacitated(world, id);
                var dead = world.Entities.TryGet(id, out var avatarEnt) &&
                           avatarEnt.TryGet<LifecycleComponent>(out var avatarLife) &&
                           avatarLife.IsDead;
                // 半透明，避免压住地名；我方弥留用蓝色，尸体用灰褐
                var fill = dead
                    ? (selected
                        ? new Color(0.55f, 0.48f, 0.40f, 0.88f)
                        : new Color(0.42f, 0.36f, 0.30f, 0.75f))
                    : incap
                        ? (selected
                            ? new Color(0.35f, 0.62f, 0.95f, 0.88f)
                            : new Color(0.28f, 0.48f, 0.88f, 0.78f))
                        : selected
                            ? new Color(0.92f, 0.72f, 0.22f, 0.55f)
                            : inEncounter
                                ? new Color(0.85f, 0.45f, 0.28f, 0.58f)
                                : onRoute
                                    ? new Color(0.35f, 0.55f, 0.85f, 0.50f)
                                    : new Color(0.62f, 0.64f, 0.58f, 0.45f);
                var old = GUI.color;
                GUI.color = fill;
                GUI.DrawTexture(rect, _px);
                if (selected)
                {
                    GUI.color = new Color(1f, 0.95f, 0.55f, 0.85f);
                    GUI.DrawTexture(new Rect(rect.x - 2f, rect.y - 2f, rect.width + 4f, 3f), _px);
                    GUI.DrawTexture(new Rect(rect.x - 2f, rect.yMax - 1f, rect.width + 4f, 3f), _px);
                    GUI.DrawTexture(new Rect(rect.x - 2f, rect.y, 3f, rect.height), _px);
                    GUI.DrawTexture(new Rect(rect.xMax - 1f, rect.y, 3f, rect.height), _px);
                }

                if (dead)
                {
                    GUI.color = Color.white;
                    GUI.Label(rect, "尸", _avatarLabel);
                }
                else if (incap)
                {
                    GUI.color = Color.white;
                    GUI.Label(rect, "弥", _avatarLabel);
                }
                else
                {
                    GUI.color = new Color(0f, 0f, 0f, 0.7f);
                    var shortName = EntityLabel(world, id);
                    if (shortName.Length > 2)
                        shortName = shortName.Substring(0, 2);
                    GUI.Label(rect, shortName, _avatarLabel);
                }

                GUI.color = old;
            }
        }

        static bool SegmentNearMap(Rect map, Vector2 a, Vector2 b)
        {
            var bounds = map;
            bounds.xMin -= 40f;
            bounds.xMax += 40f;
            bounds.yMin -= 40f;
            bounds.yMax += 40f;
            return bounds.Contains(a) || bounds.Contains(b) ||
                   (a.x >= bounds.xMin && a.x <= bounds.xMax) ||
                   (b.x >= bounds.xMin && b.x <= bounds.xMax);
        }

        void HandleMapInput(Rect mapRect, XianXia.Core.Simulation.SimulationWorld world, WorldGraphBoard graph)
        {
            if (bootstrap.WorldTravelConfirm != null && bootstrap.WorldTravelConfirm.IsOpen)
                return;

            var e = Event.current;
            if (e != null && e.type == EventType.Used)
                return;
            if (e == null || e.type != EventType.MouseDown)
                return;
            if (!mapRect.Contains(e.mousePosition))
                return;
            if (e.button == 2)
                return;

            var mouse = e.mousePosition;

            // —— 左键：只负责选中（永不弹攻击／进入指令菜单）——
            if (e.button == 0)
            {
                // 接战点活人／弥留叠在一起时，优先点中活人
                if (TryHitAvatar(mouse, world, out var hitAvatar, preferLiving: true))
                {
                    var id = new EntityId(hitAvatar);
                    var downed = LingeringBattlefieldPartyService.IsLingeringDowned(world, id);
                    var living = LingeringBattlefieldPartyService.IsLivingForMacroOrder(world, id);
                    if (!living && !downed)
                    {
                        _status = EntityLabel(world, id) + " 无法选中（已移除）";
                        e.Use();
                        return;
                    }

                    var shift = e.shift;
                    if (!shift)
                    {
                        _selected.Clear();
                        _selectedStackId = string.Empty;
                        _inspectNodeId = string.Empty;
                        _stackMenuOpen = false;
                    }

                    if (_selected.Contains(hitAvatar) && shift)
                        _selected.Remove(hitAvatar);
                    else
                        _selected.Add(hitAvatar);

                    if (downed)
                    {
                        world.Entities.TryGet(id, out var downEnt);
                        var cd = downEnt != null
                            ? CombatLifeStateService.FormatLifeStateWithCountdown(world, downEnt)
                            : "倒下";
                        var tag = LingeringBattlefieldPartyService.IsVisibleCorpse(world, id)
                            ? "尸体"
                            : "弥留";
                        _status = "已选" + tag + " " + EntityLabel(world, id) +
                                  (string.IsNullOrEmpty(cd) ? "" : "｜" + cd) +
                                  "｜右键该头像：进入残留战场";
                    }
                    else
                    {
                        var stateHint = "驻留";
                        if (world.WorldPresence.TryGet(id, out var selP) && selP != null)
                        {
                            if (selP.Mode == PartyWorldPresenceMode.InEncounter)
                                stateHint = "接战中（橘）";
                            else if (selP.IsCombatPursuing)
                                stateHint = "追击增援中（蓝）";
                            else if (selP.HasRoutePresentation)
                                stateHint = "路上移动（蓝）";
                            else if (selP.Mode == PartyWorldPresenceMode.Traveling)
                                stateHint = "行军中（蓝）";
                        }

                        _status = "已选 " + _selected.Count + " 人｜" + stateHint + "｜右键节点/道路移动；右键敌军攻击";
                    }

                    e.Use();
                    return;
                }

                if (TryHitArmyStack(
                        world,
                        mouse,
                        out var hitStackId,
                        ArmyStackHitPad,
                        ArmyStackHitPadContested))
                {
                    // 标准 RTS：点敌军改选敌军，清空我方选中；不弹指令菜单
                    _selected.Clear();
                    _selectedStackId = hitStackId;
                    _inspectNodeId = string.Empty;
                    _stackMenuOpen = false;
                    _avatarMenuOpen = false;
                    if (world.Strategic.Armies.TryGet(hitStackId, out var stack) && stack != null)
                        _status = "已选敌军｜" + DescribeStack(world, stack) + "｜先左键选我方，再右键攻击";
                    else
                        _status = "已选敌军栈";
                    e.Use();
                    return;
                }

                for (var i = 0; i < _nodeRects.Count; i++)
                {
                    var (nodeId, rect) = _nodeRects[i];
                    if (!rect.Contains(mouse))
                        continue;

                    _nodeMenuNodeId = nodeId;
                    _inspectNodeId = nodeId;
                    _nodeMenuOpen = true;
                    _nodeMenuRect = AnchorContextMenu(rect, 196f, 118f);
                    _stackMenuOpen = false;
                    _avatarMenuOpen = false;
                    if (!e.shift)
                    {
                        _selected.Clear();
                        _selectedStackId = string.Empty;
                    }

                    if (graph.TryGetNode(nodeId, out var node))
                        _status = StrategicNodeAccessService.DescribeNode(world, node);
                    e.Use();
                    return;
                }

                if (!e.shift)
                {
                    _selected.Clear();
                    _selectedStackId = string.Empty;
                    _inspectNodeId = string.Empty;
                    _stackMenuOpen = false;
                    _avatarMenuOpen = false;
                    _status = "已取消选择";
                }

                e.Use();
                return;
            }

            if (e.button != 1)
                return;

            // —— 右键：只负责下令（永不改选中集合）——
            // 弥留下：有活人选中 → 前往探望／进残留；无活人 → 原地进入菜单
            if (TryHitAvatar(mouse, world, out var menuAvatarId, preferLiving: false))
            {
                var hitId = new EntityId(menuAvatarId);
                if (LingeringBattlefieldPartyService.IsLingeringDowned(world, hitId))
                {
                    OpenIncapAvatarMenu(world, hitId, mouse);
                    e.Use();
                    return;
                }

                // 活人头像不拦截右键——继续落到节点／道路／敌军
            }

            if (TryHitArmyStack(
                    world,
                    mouse,
                    out var menuStackId,
                    ArmyStackHitPad,
                    ArmyStackHitPadContested))
            {
                // 不写入 _selectedStackId：右键不负责选中
                _stackMenuOpen = false;
                if (!world.Strategic.Armies.TryGet(menuStackId, out var previewStack) || previewStack == null)
                {
                    e.Use();
                    return;
                }

                CollectSelectedMacroParty(world, MacroPartyKind.MoveOrAttack, _attackPartyScratch);
                if (_attackPartyScratch.Count > 0)
                {
                    _stackMenuStackId = menuStackId;
                    _stackMenuOpen = true;
                    _stackMenuRect = new Rect(mouse.x + 4f, mouse.y + 4f, 196f, 56f);
                    _status = "下令攻击｜" + DescribeStack(world, previewStack);
                    e.Use();
                    return;
                }

                // 无我方下令人选时：不挡移动——落到下方节点／道路
            }

            string destId = null;
            for (var i = 0; i < _nodeRects.Count; i++)
            {
                if (!_nodeRects[i].rect.Contains(mouse))
                    continue;
                destId = _nodeRects[i].nodeId;
                break;
            }

            WorldTravelTarget target;
            var hasTravelTarget = false;
            if (!string.IsNullOrEmpty(destId))
            {
                target = WorldTravelTarget.AtNode(destId);
                hasTravelTarget = true;
            }
            else if (TryPickRouteTargetOnScreen(mapRect, graph, mouse, RoutePickScreenPx, out target))
            {
                hasTravelTarget = true;
            }

            if (hasTravelTarget)
            {
                var hadSelection = _selected.Count > 0;
                var onlyIncapSelected = false;
                if (hadSelection)
                {
                    onlyIncapSelected = true;
                    foreach (var idVal in _selected)
                    {
                        if (LingeringBattlefieldPartyService.IsLivingForMacroOrder(
                                world, new EntityId(idVal)))
                        {
                            onlyIncapSelected = false;
                            break;
                        }
                    }
                }

                PruneRemovedFromSelection(world);
                CollectSelectedMacroParty(world, MacroPartyKind.MoveOrAttack, _scratchParty);
                if (_scratchParty.Count == 0)
                {
                    var freeze = StrategicClockFreezeService.IsModalEncounter(world)
                        ? "（遭遇 Modal 未解冻，禁止战略令）"
                        : string.Empty;
                    _status = onlyIncapSelected
                        ? "选中的是弥留／尸体｜请左键点选活人再移动"
                        : hadSelection
                            ? "选中的活人当前无法上路" + freeze + "，请改选其他活人"
                            : "请先左键点选可下令的活人，再右键目标";
                    e.Use();
                    return;
                }

                for (var i = _scratchParty.Count - 1; i >= 0; i--)
                {
                    if (!world.WorldPresence.TryGet(_scratchParty[i], out var wp) ||
                        !WorldTravelPathService.CanAgentReachTarget(world, wp, target))
                        _scratchParty.RemoveAt(i);
                }

                if (_scratchParty.Count == 0)
                {
                    _status = "所选角色无法沿宏观道路到达该位置";
                    e.Use();
                    return;
                }

                if (bootstrap.WorldTravelConfirm == null)
                {
                    _status = "出行确认组件缺失，无法下达移动";
                    e.Use();
                    return;
                }

                var destLabel = target.Describe(graph);
                bootstrap.WorldTravelConfirm.OpenTarget(_scratchParty, target, destLabel);
                _status = "等待确认移动到「" + destLabel + "」…";
                e.Use();
                return;
            }

            _status = "请右键节点名牌或道路线条下达移动";
            e.Use();
        }

        bool TryPickRouteTargetOnScreen(
            Rect mapRect,
            WorldGraphBoard graph,
            Vector2 mouse,
            float maxScreenPx,
            out WorldTravelTarget target)
        {
            target = default;
            if (graph == null)
                return false;

            var bestDistSq = maxScreenPx * maxScreenPx;
            var found = false;
            foreach (var kv in graph.Routes)
            {
                var route = kv.Value;
                if (route == null ||
                    !graph.TryGetNode(route.FromNodeId, out var from) ||
                    !graph.TryGetNode(route.ToNodeId, out var to))
                    continue;

                var a = Project(mapRect, from.WorldX, from.WorldY);
                var b = Project(mapRect, to.WorldX, to.WorldY);
                var dx = b.x - a.x;
                var dy = b.y - a.y;
                var lenSq = dx * dx + dy * dy;
                if (lenSq <= 0.0001f)
                    continue;

                var t = ((mouse.x - a.x) * dx + (mouse.y - a.y) * dy) / lenSq;
                t = Mathf.Clamp01(t);
                var px = a.x + dx * t;
                var py = a.y + dy * t;
                var distSq = (mouse.x - px) * (mouse.x - px) + (mouse.y - py) * (mouse.y - py);
                if (distSq > bestDistSq)
                    continue;

                bestDistSq = distSq;
                target = WorldTravelTarget.OnRoute(route.Id, route.FromNodeId, route.ToNodeId, t);
                found = true;
            }

            return found;
        }

        static Rect AnchorContextMenu(Rect anchor, float width, float height)
        {
            var x = anchor.xMax + 4f;
            var y = anchor.yMin;
            if (x + width > Screen.width - 8f)
                x = anchor.xMin - width - 4f;
            if (x < 8f)
                x = Mathf.Clamp(anchor.xMax + 4f, 8f, Screen.width - width - 8f);
            if (y + height > Screen.height - 8f)
                y = Screen.height - height - 8f;
            if (y < 8f)
                y = 8f;
            return new Rect(x, y, width, height);
        }

        void DrawNodeContextMenu(Rect mapRect, XianXia.Core.Simulation.SimulationWorld world, WorldGraphBoard graph)
        {
            if (!_nodeMenuOpen || string.IsNullOrEmpty(_nodeMenuNodeId))
                return;
            if (!graph.TryGetNode(_nodeMenuNodeId, out var node) || node == null)
            {
                _nodeMenuOpen = false;
                return;
            }

            // 每帧按节点屏幕位置重算，避免镜头平移后菜单与节点脱节
            var p = Project(mapRect, node.WorldX, node.WorldY);
            var anchor = new Rect(p.x - NodeHitW * 0.5f, p.y - NodeHitH * 0.5f, NodeHitW, NodeHitH);
            _nodeMenuRect = AnchorContextMenu(anchor, 196f, 118f);

            var prevDepth = GUI.depth;
            GUI.depth = -85;
            HostUiHitTest.Block(_nodeMenuRect);
            var prev = GUI.color;
            GUI.color = new Color(0.16f, 0.17f, 0.19f, 0.96f);
            GUI.DrawTexture(_nodeMenuRect, _px);
            GUI.color = prev;

            var title = string.IsNullOrEmpty(node.Name) ? node.Id : node.Name;
            GUI.Label(new Rect(_nodeMenuRect.x + 8f, _nodeMenuRect.y + 4f, _nodeMenuRect.width - 16f, 18f), title, _body);
            var here = StrategicNodeAccessService.CountPartyMembersAtNode(world, node.Id);
            GUI.Label(
                new Rect(_nodeMenuRect.x + 8f, _nodeMenuRect.y + 22f, _nodeMenuRect.width - 16f, 16f),
                here > 0 ? "我方 " + here + " 人在此" : "无我方角色",
                _body);

            var y = _nodeMenuRect.y + 42f;
            var bw = _nodeMenuRect.width - 16f;
            var half = (bw - 4f) * 0.5f;
            if (GUI.Button(new Rect(_nodeMenuRect.x + 8f, y, half, 22f), "查看信息"))
            {
                _status = StrategicNodeAccessService.BuildNodeDetailText(world, node);
            }

            var canEnter = StrategicNodeAccessService.CanEnterNodeLocalMap(world, node.Id).IsSuccess;
            if (StrategicClockFreezeService.IsModalEncounter(world))
                canEnter = false;
            GUI.enabled = canEnter;
            var enterLabel = StrategicClockFreezeService.IsModalEncounter(world)
                ? "遭遇中锁定"
                : (canEnter ? "进入场景" : "无法进入");
            if (GUI.Button(new Rect(_nodeMenuRect.x + 12f + half, y, half, 22f), enterLabel) && canEnter)
            {
                var enter = WorldTravelService.EnterNodeScene(world, node.Id);
                if (enter.IsSuccess)
                {
                    CloseAllWorldMapPanels();
                    bootstrap.ApplyPartyWorldNodePresentation(closeWorldMap: true);
                    _status = "已进入 " + title;
                    _nodeMenuOpen = false;
                }
                else
                {
                    _status = FormatFail(enter);
                }
            }

            GUI.enabled = true;

            y += 26f;
            if (GUI.Button(new Rect(_nodeMenuRect.x + 8f, y, bw, 20f), "关闭"))
            {
                Event.current.Use();
                _nodeMenuOpen = false;
            }

            GUI.depth = prevDepth;
        }

        void CollectSelectedParty(List<EntityId> into)
        {
            into.Clear();
            var ids = bootstrap.Session.CharacterIds;
            if (ids == null)
                return;
            for (var i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                if (_selected.Contains(id.Value))
                    into.Add(id);
            }
        }

        bool TryHitAvatar(
            Vector2 mouse,
            XianXia.Core.Simulation.SimulationWorld world,
            out ulong avatarId,
            bool preferLiving)
        {
            avatarId = 0;
            ulong livingId = 0;
            ulong incapId = 0;
            ulong otherId = 0;
            foreach (var kv in _avatarRects)
            {
                if (!kv.Value.Contains(mouse))
                    continue;
                var id = new EntityId(kv.Key);
                if (LingeringBattlefieldPartyService.IsLivingForMacroOrder(world, id))
                {
                    livingId = kv.Key;
                }
                else if (LingeringBattlefieldPartyService.IsLingeringDowned(world, id))
                {
                    // 弥留／尸体同档；叠人时仍让位给活人
                    if (incapId == 0)
                        incapId = kv.Key;
                }
                else if (otherId == 0)
                {
                    otherId = kv.Key;
                }
            }

            if (preferLiving && livingId != 0)
            {
                avatarId = livingId;
                return true;
            }

            if (incapId != 0)
            {
                avatarId = incapId;
                return true;
            }

            if (livingId != 0)
            {
                avatarId = livingId;
                return true;
            }

            if (otherId != 0)
            {
                avatarId = otherId;
                return true;
            }

            return false;
        }

        void PruneRemovedFromSelection(XianXia.Core.Simulation.SimulationWorld world)
        {
            if (world == null || _selected.Count == 0)
                return;
            _orderFilterScratch.Clear();
            foreach (var idVal in _selected)
                _orderFilterScratch.Add(new EntityId(idVal));
            for (var i = 0; i < _orderFilterScratch.Count; i++)
            {
                var id = _orderFilterScratch[i];
                // 弥留／可见尸体保留可选中（看情报）；仅清掉已腐烂 Removed
                if (!world.Entities.TryGet(id, out var ent) || ent == null ||
                    CombatLifeStateService.ShouldHideFromSpawn(ent))
                    _selected.Remove(id.Value);
            }
        }

        bool TryHitArmyStack(
            XianXia.Core.Simulation.SimulationWorld world,
            Vector2 mouse,
            out string stackId,
            float normalPad,
            float contestedPad)
        {
            stackId = string.Empty;
            var bestDist = float.MaxValue;
            foreach (var kv in _armyStackRects)
            {
                ArmyStack stack = null;
                world?.Strategic?.Armies?.TryGet(kv.Key, out stack);
                var r = kv.Value;
                var hitPad = ResolveArmyStackHitPad(r, stack, normalPad, contestedPad);
                var cx = r.x + r.width * 0.5f;
                var cy = r.y + r.height * 0.5f;
                var radius = r.width * 0.5f + hitPad;
                var dx = mouse.x - cx;
                var dy = mouse.y - cy;
                var distSq = dx * dx + dy * dy;
                if (distSq > radius * radius)
                    continue;
                if (distSq >= bestDist)
                    continue;
                bestDist = distSq;
                stackId = kv.Key;
            }

            return !string.IsNullOrEmpty(stackId);
        }

        float ResolveArmyStackHitPad(
            Rect stackRect,
            ArmyStack stack,
            float normalPad,
            float contestedPad)
        {
            if (stack != null && stack.IsBattlefieldRemnant)
                return contestedPad;

            foreach (var kv in _avatarRects)
            {
                if (!RectsOverlap(
                        stackRect,
                        kv.Value,
                        ArmyStackContestedOverlapPx))
                    continue;
                return contestedPad;
            }

            return normalPad;
        }

        static bool RectsOverlap(Rect a, Rect b, float margin)
        {
            var axMin = a.xMin - margin;
            var axMax = a.xMax + margin;
            var ayMin = a.yMin - margin;
            var ayMax = a.yMax + margin;
            return axMax >= b.xMin && axMin <= b.xMax && ayMax >= b.yMin && ayMin <= b.yMax;
        }

        static string DescribeStack(XianXia.Core.Simulation.SimulationWorld world, ArmyStack stack)
        {
            if (stack == null)
                return string.Empty;
            var faction = StrategicFactionCatalog.DisplayName(stack.FactionId);
            var name = string.IsNullOrEmpty(stack.DisplayName) ? stack.Id : stack.DisplayName;
            var power = CombatPowerCalculator.ForArmyStack(stack);
            var where = stack.IsTraveling ? "途中" : stack.NodeId;
            return name + " · " + faction + " · " + stack.MemberCount + "人 · 战力" + power +
                   " · " + where;
        }

        void DrawStackContextMenu(XianXia.Core.Simulation.SimulationWorld world, WorldGraphBoard graph)
        {
            if (!_stackMenuOpen || string.IsNullOrEmpty(_stackMenuStackId))
                return;
            if (!world.Strategic.Armies.TryGet(_stackMenuStackId, out var stack) || stack == null)
            {
                _stackMenuOpen = false;
                return;
            }

            var prevDepth = GUI.depth;
            GUI.depth = -85;
            _stackMenuRect = new Rect(_stackMenuRect.x, _stackMenuRect.y, 196f, 56f);
            HostUiHitTest.Block(_stackMenuRect);
            var prev = GUI.color;
            GUI.color = new Color(0.16f, 0.17f, 0.19f, 0.96f);
            GUI.DrawTexture(_stackMenuRect, _px);
            GUI.color = prev;

            var title = string.IsNullOrEmpty(stack.DisplayName) ? stack.Id : stack.DisplayName;
            GUI.Label(new Rect(_stackMenuRect.x + 8f, _stackMenuRect.y + 4f, _stackMenuRect.width - 16f, 18f), title, _body);
            var y = _stackMenuRect.y + 26f;
            var bw = _stackMenuRect.width - 16f;

            CollectSelectedMacroParty(world, MacroPartyKind.MoveOrAttack, _attackPartyScratch);
            var hasParty = _attackPartyScratch.Count > 0;
            GUI.enabled = hasParty;

            var attackLabel = "攻击";
            if (GUI.Button(new Rect(_stackMenuRect.x + 8f, y, bw, 22f), attackLabel))
            {
                Event.current.Use();
                if (hasParty)
                    BeginAttackStack(world, _attackPartyScratch, stack);
                else
                    _status = "请先左键点选可下令的角色";
                _stackMenuOpen = false;
            }

            GUI.enabled = true;
            GUI.depth = prevDepth;
        }

        void BeginAttackStack(
            XianXia.Core.Simulation.SimulationWorld world,
            List<EntityId> party,
            ArmyStack stack)
        {
            // 暂不做敌对确认／宣战门槛，直接追击接战
            ExecuteAttackStack(world, party, stack);
        }

        void ExecuteAttackStack(
            XianXia.Core.Simulation.SimulationWorld world,
            List<EntityId> party,
            ArmyStack stack)
        {
            CollectMacroPartyFrom(world, party, _scratchParty, MacroPartyKind.MoveOrAttack);
            if (_scratchParty.Count == 0)
            {
                _status = "所选角色正在途中或离场，无法再下令";
                return;
            }

            // 远处可点「攻击」，但接战弹窗只由 Pursuit 到站触发（BeginPursuitToStackAnchor → AfterTravelTick）
            bootstrap.WorldTravelDeparture?.BeginPursuitToStackAnchor(_scratchParty, stack);
            var departureStatus = bootstrap.WorldTravelDeparture != null
                ? bootstrap.WorldTravelDeparture.LastStatus
                : string.Empty;
            if (!string.IsNullOrEmpty(departureStatus))
                _status = departureStatus;
            else if (world.Strategic.HasBattleOffer)
                _status = "接战弹窗已打开";
            else
            {
                var name = string.IsNullOrEmpty(stack.DisplayName) ? stack.Id : stack.DisplayName;
                _status = _scratchParty.Count + " 人出发攻击「" + name + "」（抵达后弹接战）";
            }
        }

        void TryDismissContextMenusOnOutsideClick()
        {
            var ev = Event.current;
            if (ev == null || ev.type != EventType.MouseDown)
                return;
            if (ev.button != 0 && ev.button != 1)
                return;

            if (_stackMenuOpen && _stackMenuRect.Contains(ev.mousePosition))
                return;
            if (_nodeMenuOpen && _nodeMenuRect.Contains(ev.mousePosition))
                return;
            if (_avatarMenuOpen && _avatarMenuRect.Contains(ev.mousePosition))
                return;

            if (!_stackMenuOpen && !_nodeMenuOpen && !_avatarMenuOpen)
                return;

            _stackMenuOpen = false;
            _nodeMenuOpen = false;
            _avatarMenuOpen = false;
            _avatarMenuVisitMode = false;
            // 不 Use：同一帧右键仍可落到移动／攻击下令
        }

        void DrawAvatarContextMenu(XianXia.Core.Simulation.SimulationWorld world)
        {
            if (!_avatarMenuOpen)
                return;
            var target = new EntityId(_avatarMenuEntityId);
            if (target.IsNone || !LingeringBattlefieldPartyService.IsLingeringDowned(world, target))
            {
                _avatarMenuOpen = false;
                _avatarMenuVisitMode = false;
                return;
            }

            var prevDepth = GUI.depth;
            GUI.depth = -85;
            HostUiHitTest.Block(_avatarMenuRect);
            var prev = GUI.color;
            GUI.color = new Color(0.16f, 0.17f, 0.19f, 0.96f);
            GUI.DrawTexture(_avatarMenuRect, _px);
            GUI.color = prev;
            var downTag = LingeringBattlefieldPartyService.IsVisibleCorpse(world, target)
                ? "尸体"
                : "弥留";
            if (world.Entities.TryGet(target, out var menuEnt) && menuEnt != null)
            {
                var stamped = CombatLifeStateService.FormatLifeStateWithCountdown(world, menuEnt);
                if (!string.IsNullOrEmpty(stamped))
                    downTag = stamped;
            }

            GUI.Label(
                new Rect(_avatarMenuRect.x + 8f, _avatarMenuRect.y + 4f, _avatarMenuRect.width - 16f, 18f),
                EntityLabel(world, target) + " · " + downTag,
                _body);
            CollectSelectedMacroParty(world, MacroPartyKind.MoveOrAttack, _scratchParty);
            LingeringBattlefieldPartyService.CollectViewParty(
                world, bootstrap.Session.CharacterIds, target, _attackPartyScratch, _scratchParty);
            var hasLinger = BattleOfferService.HasLingeringBattlefield(world);
            var canEnter = hasLinger && _attackPartyScratch.Count > 0;
            var hintY = _avatarMenuRect.y + 24f;
            if (!hasLinger)
            {
                GUI.Label(
                    new Rect(_avatarMenuRect.x + 8f, hintY, _avatarMenuRect.width - 16f, 16f),
                    "接战点已无残留战场",
                    _body);
                hintY += 18f;
            }
            else if (_avatarMenuVisitMode)
            {
                GUI.Label(
                    new Rect(_avatarMenuRect.x + 8f, hintY, _avatarMenuRect.width - 16f, 16f),
                    "已选活人将前往该处，抵达后弹接战窗",
                    _body);
                hintY += 18f;
            }
            else if (!canEnter)
            {
                GUI.Label(
                    new Rect(_avatarMenuRect.x + 8f, hintY, _avatarMenuRect.width - 16f, 16f),
                    "无法再入（接战锚点缺失或无人可进场）",
                    _body);
                hintY += 18f;
            }
            else
            {
                GUI.Label(
                    new Rect(_avatarMenuRect.x + 8f, hintY, _avatarMenuRect.width - 16f, 16f),
                    "将弹出接战窗，再选手动/自动进入遭遇地图",
                    _body);
                hintY += 18f;
            }

            GUI.enabled = hasLinger && (_avatarMenuVisitMode || canEnter);
            var btnLabel = _avatarMenuVisitMode ? "前往并进入残留战场" : "进入残留战场";
            if (GUI.Button(
                    new Rect(_avatarMenuRect.x + 8f, _avatarMenuRect.y + 58f, _avatarMenuRect.width - 16f, 28f),
                    btnLabel) &&
                hasLinger)
            {
                Event.current.Use();
                if (_avatarMenuVisitMode)
                {
                    if (TryBeginVisitIncapacitated(world, target))
                    {
                        _avatarMenuOpen = false;
                        _avatarMenuVisitMode = false;
                    }
                }
                else if (canEnter &&
                         BattleOfferService.TryBuildOfferForLingeringBattlefield(
                             world,
                             bootstrap.Session.CharacterIds,
                             target,
                             "残留战场",
                             _scratchParty))
                {
                    _avatarMenuOpen = false;
                    _avatarMenuVisitMode = false;
                    _status = "接战弹窗已打开";
                }
                else
                    _status = "无法打开接战弹窗";
            }

            GUI.enabled = true;
            GUI.depth = prevDepth;
        }

        /// <summary>已选活人前往弥留接战点；已在半径内则直接弹接战窗。</summary>
        bool TryBeginVisitIncapacitated(
            XianXia.Core.Simulation.SimulationWorld world,
            EntityId focusIncap)
        {
            CollectSelectedMacroParty(world, MacroPartyKind.MoveOrAttack, _scratchParty);
            if (_scratchParty.Count == 0)
            {
                _status = "请先左键点选可上路的活人";
                return false;
            }

            if (!BattleOfferService.HasLingeringBattlefield(world))
            {
                _status = "接战点已无残留战场";
                return false;
            }

            // 已在支援半径内：不必再走，直接接战窗（弥留强制纳入）
            if (LingeringBattlefieldPartyService.TryResolveBattleAnchor(
                    world, focusIncap, out var anchorNode, out var anchorRoute, out var anchorProgress))
            {
                var anyNear = false;
                for (var i = 0; i < _scratchParty.Count; i++)
                {
                    if (!world.WorldPresence.TryGet(_scratchParty[i], out var wp) || wp == null)
                        continue;
                    if (ReinforcementRangeService.IsWithinReinforcementRange(
                            world, wp, anchorNode, anchorRoute, anchorProgress))
                    {
                        anyNear = true;
                        break;
                    }
                }

                if (anyNear)
                {
                    if (BattleOfferService.TryBuildOfferForLingeringBattlefield(
                            world,
                            bootstrap.Session.CharacterIds,
                            focusIncap,
                            "残留战场",
                            _scratchParty))
                    {
                        _status = "接战弹窗已打开";
                        return true;
                    }

                    _status = "无法打开接战弹窗";
                    return false;
                }
            }

            if (!TryBuildTravelTargetToEntity(world, focusIncap, out var target))
            {
                _status = "无法解析倒下角色位置";
                return false;
            }

            if (bootstrap.WorldTravelConfirm == null)
            {
                _status = "出行确认组件缺失";
                return false;
            }

            world.Strategic.SetPendingLingeringVisit(focusIncap.Value, _scratchParty);
            var destLabel = "前往「" + EntityLabel(world, focusIncap) + "」残留点";
            bootstrap.WorldTravelConfirm.OpenTarget(_scratchParty, target, destLabel);
            _status = "等待确认前往倒下同伴…";
            return true;
        }

        static bool TryBuildTravelTargetToEntity(
            XianXia.Core.Simulation.SimulationWorld world,
            EntityId id,
            out WorldTravelTarget target)
        {
            target = default;
            if (world == null || id.IsNone ||
                !world.WorldPresence.TryGet(id, out var wp) || wp == null)
                return false;

            if (wp.HasRoutePresentation && !string.IsNullOrEmpty(wp.RouteId))
            {
                var progress = wp.Mode == PartyWorldPresenceMode.RouteAnchored
                    ? Mathf.Clamp01(wp.RouteAnchorProgress)
                    : Mathf.Clamp01(wp.TravelProgress);
                target = WorldTravelTarget.OnRoute(
                    wp.RouteId,
                    wp.NodeId ?? string.Empty,
                    wp.DestNodeId ?? string.Empty,
                    progress);
                return true;
            }

            if (!string.IsNullOrEmpty(wp.NodeId))
            {
                target = WorldTravelTarget.AtNode(wp.NodeId);
                return true;
            }

            return false;
        }

        void OpenIncapAvatarMenu(
            XianXia.Core.Simulation.SimulationWorld world,
            EntityId hitId,
            Vector2 mouse)
        {
            CollectSelectedMacroParty(world, MacroPartyKind.MoveOrAttack, _scratchParty);
            _avatarMenuVisitMode = _scratchParty.Count > 0;
            _avatarMenuEntityId = hitId.Value;
            _avatarMenuOpen = true;
            _avatarMenuRect = new Rect(mouse.x + 4f, mouse.y + 4f, 196f, 118f);
            LingeringBattlefieldPartyService.CollectViewParty(
                world, bootstrap.Session.CharacterIds, hitId, _attackPartyScratch, _scratchParty);
            var hasLinger = BattleOfferService.HasLingeringBattlefield(world);
            var tag = LingeringBattlefieldPartyService.IsVisibleCorpse(world, hitId)
                ? "尸体"
                : "弥留";
            if (_avatarMenuVisitMode)
            {
                _status = hasLinger
                    ? "已选活人｜右键" + tag + "：可前往并进入残留战场"
                    : EntityLabel(world, hitId) + "（" + tag + "）｜接战点已无残留战场";
                return;
            }

            var canEnter = hasLinger && _attackPartyScratch.Count > 0;
            _status = canEnter
                ? EntityLabel(world, hitId) + "（" + tag + "）｜可进入残留战场"
                : hasLinger
                    ? EntityLabel(world, hitId) + "（" + tag + "）｜无法再入（接战锚点缺失或无人可进场）"
                    : EntityLabel(world, hitId) + "（" + tag + "）｜接战点已无残留战场";
        }

        void CollectSelectedMacroParty(
            XianXia.Core.Simulation.SimulationWorld world,
            MacroPartyKind kind,
            List<EntityId> into,
            EntityId lingerFocusIncap = default)
        {
            if (into == null)
                return;
            into.Clear();
            if (kind == MacroPartyKind.LingeringView)
            {
                if (lingerFocusIncap.IsNone)
                    return;
                CollectSelectedParty(_orderFilterScratch);
                CollectMacroPartyFrom(world, _orderFilterScratch, into, MacroPartyKind.MoveOrAttack);
                return;
            }

            // 选中真源：_selected ∩ Session.CharacterIds
            // 弥留真源：LifecycleComponent（IsLivingForMacroOrder）
            // 能否上路：CanReceiveTravelOrder
            // 移动／攻击共用本函数；中间表必须独立，禁止 into 兼缓冲。
            CollectSelectedParty(_orderFilterScratch);
            CollectMacroPartyFrom(world, _orderFilterScratch, into, MacroPartyKind.MoveOrAttack);
        }

        static void CollectMacroPartyFrom(
            XianXia.Core.Simulation.SimulationWorld world,
            IReadOnlyList<EntityId> from,
            List<EntityId> into,
            MacroPartyKind kind)
        {
            if (world == null || from == null || into == null)
                return;

            if (ReferenceEquals(from, into))
            {
                for (var i = into.Count - 1; i >= 0; i--)
                {
                    var id = into[i];
                    if (id.IsNone ||
                        (kind == MacroPartyKind.MoveOrAttack &&
                         !LingeringBattlefieldPartyService.IsLivingForMacroOrder(world, id)) ||
                        !WorldTravelService.CanReceiveTravelOrder(world, id))
                        into.RemoveAt(i);
                }

                return;
            }

            into.Clear();
            for (var i = 0; i < from.Count; i++)
            {
                var id = from[i];
                if (id.IsNone)
                    continue;
                if (kind == MacroPartyKind.MoveOrAttack && !LingeringBattlefieldPartyService.IsLivingForMacroOrder(world, id))
                    continue;
                if (!WorldTravelService.CanReceiveTravelOrder(world, id))
                    continue;
                into.Add(id);
            }
        }

        static string EntityLabel(XianXia.Core.Simulation.SimulationWorld world, EntityId id)
        {
            if (!world.Entities.TryGet(id, out var e) || e == null)
                return id.Value.ToString();
            if (!string.IsNullOrWhiteSpace(e.DisplayName))
                return e.DisplayName;
            return e.DefinitionId.ToString();
        }

        static string FormatFail(XianXia.Core.Results.Result r) =>
            r.IsFailure ? (r.Error.Message + (string.IsNullOrEmpty(r.Error.Detail) ? "" : " · " + r.Error.Detail)) : "";

        /// <summary>进入场景时关掉大地图（本实例＋bootstrap 引用）。</summary>
        void CloseAllWorldMapPanels()
        {
            Close();
            if (bootstrap != null && bootstrap.WorldMapPanel != null && bootstrap.WorldMapPanel != this)
                bootstrap.WorldMapPanel.Close();
        }

        static void DrawLine(Vector2 a, Vector2 b, Color color)
        {
            var prev = GUI.color;
            GUI.color = color;
            var delta = b - a;
            var dist = delta.magnitude;
            if (dist < 1f)
            {
                GUI.color = prev;
                return;
            }

            var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            var center = (a + b) * 0.5f;
            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, center);
            GUI.DrawTexture(new Rect(center.x - dist * 0.5f, center.y - 1.5f, dist, 3f), Texture2D.whiteTexture);
            GUI.matrix = matrix;
            GUI.color = prev;
        }

        void DrawInspectPanel(
            Rect panelRect,
            XianXia.Core.Simulation.SimulationWorld world,
            WorldGraphBoard graph)
        {
            HostUiHitTest.Block(panelRect);
            var prev = GUI.color;
            GUI.color = new Color(0.14f, 0.15f, 0.17f, 0.97f);
            GUI.DrawTexture(panelRect, _px);
            GUI.color = new Color(0.32f, 0.36f, 0.40f, 1f);
            GUI.DrawTexture(new Rect(panelRect.x, panelRect.y, 2f, panelRect.height), _px);
            GUI.color = prev;

            GUI.Label(
                new Rect(panelRect.x + 12f, panelRect.y + 10f, panelRect.width - 24f, 22f),
                "情报",
                _title);

            var body = BuildInspectBody(world, graph);
            var textRect = new Rect(
                panelRect.x + 12f,
                panelRect.y + 38f,
                panelRect.width - 24f,
                panelRect.height - 50f);
            var contentH = Mathf.Max(
                textRect.height,
                _body.CalcHeight(new GUIContent(body), textRect.width - 18f) + 8f);
            _inspectScroll = GUI.BeginScrollView(
                textRect,
                _inspectScroll,
                new Rect(0f, 0f, textRect.width - 16f, contentH));
            GUI.Label(new Rect(0f, 0f, textRect.width - 18f, contentH), body, _body);
            GUI.EndScrollView();
        }

        string BuildInspectBody(
            XianXia.Core.Simulation.SimulationWorld world,
            WorldGraphBoard graph)
        {
            if (_selected.Count > 0)
                return BuildSelectedAgentsInspect(world, graph);
            if (!string.IsNullOrEmpty(_selectedStackId) &&
                world.Strategic?.Armies != null &&
                world.Strategic.Armies.TryGet(_selectedStackId, out var stack) &&
                stack != null)
                return BuildStackInspect(world, graph, stack);
            if (!string.IsNullOrEmpty(_inspectNodeId) &&
                graph != null &&
                graph.TryGetNode(_inspectNodeId, out var node) &&
                node != null)
                return BuildNodeInspect(world, node);

            return "左键点选我方角色、敌军部队或地图节点，在此查看详情。\n\n" +
                   "· 我方：境界／生命／宏观状态\n" +
                   "· 敌军：势力／人数／战力／位置\n" +
                   "· 节点：名称／在场人数／可否进入";
        }

        string BuildSelectedAgentsInspect(
            XianXia.Core.Simulation.SimulationWorld world,
            WorldGraphBoard graph)
        {
            var sb = new StringBuilder(256);
            sb.Append("已选 ").Append(_selected.Count).Append(" 人\n");
            var n = 0;
            foreach (var idVal in _selected)
            {
                if (n >= 8)
                {
                    sb.Append("…另有 ").Append(_selected.Count - n).Append(" 人\n");
                    break;
                }

                AppendAgentInspect(sb, world, graph, new EntityId(idVal));
                n++;
            }

            return sb.ToString();
        }

        void AppendAgentInspect(
            StringBuilder sb,
            XianXia.Core.Simulation.SimulationWorld world,
            WorldGraphBoard graph,
            EntityId id)
        {
            sb.Append('\n').Append("—— ").Append(EntityLabel(world, id)).Append(" ——\n");
            if (!world.Entities.TryGet(id, out var ent) || ent == null)
            {
                sb.Append("实体缺失\n");
                return;
            }

            var lifeLabel = CombatLifeStateService.FormatLifeStateWithCountdown(world, ent);
            if (string.IsNullOrEmpty(lifeLabel))
                lifeLabel = "存活";
            sb.Append("状态：").Append(lifeLabel).Append('\n');
            sb.Append("战力：").Append(CombatPowerCalculator.ForEntity(world, id)).Append('\n');

            if (!world.WorldPresence.TryGet(id, out var presence) || presence == null)
            {
                sb.Append("位置：无宏观坐标\n");
                return;
            }

            sb.Append("位置：").Append(FormatPresenceLocation(graph, presence)).Append('\n');
            sb.Append("行动：").Append(FormatPresenceAction(presence)).Append('\n');
        }

        static string FormatPresenceLocation(WorldGraphBoard graph, WorldAgentPresence p)
        {
            if (p.HasRoutePresentation)
            {
                var from = ResolveNodeName(graph, p.NodeId);
                var to = ResolveNodeName(graph, p.DestNodeId);
                var pct = Mathf.RoundToInt(Mathf.Clamp01(p.TravelProgress) * 100f);
                return from + " → " + to + "（" + pct + "%）";
            }

            if (!string.IsNullOrEmpty(p.NodeId))
                return ResolveNodeName(graph, p.NodeId);
            return "未知";
        }

        static string FormatPresenceAction(WorldAgentPresence p)
        {
            if (p == null)
                return "—";
            if (p.Mode == PartyWorldPresenceMode.InEncounter)
                return "接战中";
            if (p.IsCombatPursuing)
                return "追击增援";
            if (p.HasRoutePresentation || p.Mode == PartyWorldPresenceMode.Traveling)
                return "行军中";
            if (p.Mode == PartyWorldPresenceMode.RouteAnchored)
                return "路中驻留";
            return "驻留";
        }

        static string ResolveNodeName(WorldGraphBoard graph, string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
                return "？";
            if (graph != null &&
                graph.TryGetNode(nodeId, out var n) &&
                n != null &&
                !string.IsNullOrEmpty(n.Name))
                return n.Name;
            return nodeId;
        }

        string BuildStackInspect(
            XianXia.Core.Simulation.SimulationWorld world,
            WorldGraphBoard graph,
            ArmyStack stack)
        {
            var sb = new StringBuilder(192);
            sb.Append("敌军部队\n\n");
            sb.Append("名称：")
                .Append(string.IsNullOrEmpty(stack.DisplayName) ? stack.Id : stack.DisplayName)
                .Append('\n');
            sb.Append("势力：").Append(StrategicFactionCatalog.DisplayName(stack.FactionId)).Append('\n');
            sb.Append("人数：").Append(stack.MemberCount).Append('\n');
            if (stack.IncapacitatedMemberCount > 0)
                sb.Append("弥留残留：").Append(stack.IncapacitatedMemberCount).Append('\n');
            sb.Append("战力：").Append(CombatPowerCalculator.ForArmyStack(stack)).Append('\n');
            if (stack.IsBattlefieldRemnant)
                sb.Append("类型：残留战场\n");
            if (stack.IsTraveling)
            {
                sb.Append("状态：行军中\n");
                sb.Append("自 ").Append(ResolveNodeName(graph, stack.NodeId))
                    .Append(" → ").Append(ResolveNodeName(graph, stack.DestNodeId)).Append('\n');
            }
            else if (stack.IsRouteAnchored)
            {
                sb.Append("状态：路中驻留\n");
                sb.Append("道路：")
                    .Append(ResolveNodeName(graph, stack.NodeId))
                    .Append(" ↔ ")
                    .Append(ResolveNodeName(graph, stack.DestNodeId))
                    .Append("（")
                    .Append(Mathf.RoundToInt(Mathf.Clamp01(stack.RouteAnchorProgress) * 100f))
                    .Append("%）\n");
            }
            else
            {
                sb.Append("状态：驻留\n");
                sb.Append("节点：").Append(ResolveNodeName(graph, stack.NodeId)).Append('\n');
            }

            sb.Append("\n操作：先左键选我方，再右键该部队攻击");
            return sb.ToString();
        }

        static string BuildNodeInspect(
            XianXia.Core.Simulation.SimulationWorld world,
            WorldNodeState node)
        {
            var detail = StrategicNodeAccessService.BuildNodeDetailText(world, node);
            if (string.IsNullOrEmpty(detail))
                return "节点：" + (string.IsNullOrEmpty(node.Name) ? node.Id : node.Name);
            return "地图节点\n\n" + detail + "\n\n操作：右键可移动至此；有我方在场时可进入场景";
        }

        void EnsureStyles()
        {
            if (_title != null)
                return;
            _px = Texture2D.whiteTexture;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            _avatarLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _nodeLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
        }
    }
}
