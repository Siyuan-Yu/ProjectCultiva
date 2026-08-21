using System;
using System.Collections.Generic;
using UnityEngine;
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
        /// <summary>敌军栈默认吸附（屏幕像素，圆形半径外延）。</summary>
        const float ArmyStackHitPad = 16f;
        /// <summary>与我方头像／接战残留重叠时缩小，避免抢点。</summary>
        const float ArmyStackHitPadContested = 8f;
        /// <summary>判定「叠在一起」：头像与敌军视觉 rect 扩此值后相交。</summary>
        const float ArmyStackContestedOverlapPx = 4f;
        /// <summary>
        /// 最大放大：视口半宽（世界单位）。再放大一倍相对「邻站铺满」参考（半宽 1.5 ≈ 满屏跨度 3）。
        /// </summary>
        const float MinViewHalfExtent = 1.5f;
        const float MapPad = 48f;
        /// <summary>底部支援半径滑块条高度。</summary>
        const float BottomBarH = 36f;
        const float ReinforceRadiusMin = 0.25f;
        const float ReinforceRadiusMax = 4f;

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
        readonly List<EntityId> _attackPartyScratch = new List<EntityId>(8);

        // 弥留头像右键：查看再入战场
        ulong _avatarMenuEntityId;
        bool _avatarMenuOpen;
        Rect _avatarMenuRect;

        // 节点左键菜单
        string _nodeMenuNodeId = string.Empty;
        Rect _nodeMenuRect;
        bool _nodeMenuOpen;

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

        public void Open()
        {
            open = true;
            _requestClose = false;
            if (bootstrap?.Session != null && bootstrap.Session.IsInitialized)
            {
                _holdingPauseForMap = !bootstrap.Session.IsPaused;
                if (_holdingPauseForMap)
                    bootstrap.Pause();
            }
            else
            {
                _holdingPauseForMap = false;
            }
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

        public void Close()
        {
            open = false;
            _requestClose = false;
            _nodeMenuOpen = false;
            _nodeMenuNodeId = string.Empty;
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
        }

        bool _requestClose;

        void Update()
        {
            if (_requestClose)
                Close();

            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return;

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
            if (!open || bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return;
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
                "  （左键选部队｜右键节点/道路移动｜右键他方：攻击/详情｜M 关闭）",
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
                new Rect(pad, topBar, Screen.width - pad * 2f, 22f),
                "镜头：" + focusName +
                "　已选 " + _selected.Count +
                "　缩放 " + zoomPct + "%（最大：邻站铺满屏／最小：全图）" +
                (string.IsNullOrEmpty(_status) ? "" : "　｜　" + _status),
                _body);

            var mapTop = topBar + 56f;
            var mapRect = new Rect(
                pad,
                mapTop,
                Screen.width - pad * 2f,
                Screen.height - mapTop - pad - BottomBarH);
            GUI.color = new Color(0.12f, 0.14f, 0.16f, 1f);
            GUI.DrawTexture(mapRect, _px);
            GUI.color = Color.white;

            HandleCameraInput(mapRect);
            DrawGraph(mapRect, world, graph);
            DrawReinforcementRadiusOverlay(mapRect, world);
            DrawNodeContextMenu(mapRect, world, graph);
            DrawStackContextMenu(world, graph);
            DrawAvatarContextMenu(world);
            DrawReinforcementRadiusSlider(pad, world);
            TryDismissContextMenusOnOutsideClick();
            if (Event.current != null && Event.current.type == EventType.Used)
                return;
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
            GUI.Label(new Rect(x, y + 4f, 200f, 22f), "[ / ] 调倍速", _body);

            if (world.Strategic != null && world.Strategic.HasBlockingInterrupt)
            {
                x += 210f;
                GUI.Label(new Rect(x, y + 4f, 260f, 22f), "战略打断中…", _body);
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

                float wx;
                float wy;
                if (stack.IsRoutePositioned &&
                    graph.TryGetRoute(stack.RouteId, out var route) &&
                    graph.TryGetNode(stack.NodeId, out var from) &&
                    graph.TryGetNode(stack.DestNodeId, out var to))
                {
                    var t = stack.GetRouteDisplayProgress();
                    wx = from.WorldX + (to.WorldX - from.WorldX) * t;
                    wy = from.WorldY + (to.WorldY - from.WorldY) * t;
                }
                else if (!string.IsNullOrEmpty(stack.NodeId) &&
                         graph.TryGetNode(stack.NodeId, out var node))
                {
                    wx = node.WorldX;
                    wy = node.WorldY;
                }
                else
                {
                    continue;
                }

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
                var tag = stack.MemberCount + "人 · " +
                          (string.IsNullOrEmpty(stack.DisplayName) ? "敌军" : stack.DisplayName);
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
                    center = basePos + new Vector2((slot % 3) * 12f - 12f, -AvatarSize * 0.55f);
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
                var incap = IsIncapacitatedEntity(world, id);
                // 半透明，避免压住地名
                var fill = incap
                    ? new Color(0.75f, 0.28f, 0.22f, 0.72f)
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
                if (selected && !incap)
                {
                    GUI.color = new Color(1f, 0.95f, 0.55f, 0.85f);
                    GUI.DrawTexture(new Rect(rect.x - 2f, rect.y - 2f, rect.width + 4f, 3f), _px);
                    GUI.DrawTexture(new Rect(rect.x - 2f, rect.yMax - 1f, rect.width + 4f, 3f), _px);
                    GUI.DrawTexture(new Rect(rect.x - 2f, rect.y, 3f, rect.height), _px);
                    GUI.DrawTexture(new Rect(rect.xMax - 1f, rect.y, 3f, rect.height), _px);
                }

                GUI.color = new Color(0f, 0f, 0f, 0.7f);
                var shortName = EntityLabel(world, id);
                if (shortName.Length > 2)
                    shortName = shortName.Substring(0, 2);
                GUI.Label(rect, shortName, _avatarLabel);
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

            if (e.button == 0)
            {
                if (TryHitAvatar(mouse, out var hitAvatar))
                {
                    var id = new EntityId(hitAvatar);
                    if (IsIncapacitatedEntity(world, id))
                    {
                        _status = EntityLabel(world, id) +
                                  "（弥留）｜右键：查看；先左键选活人再右键：派人探望";
                        e.Use();
                        return;
                    }

                    var shift = e.shift;
                    if (!shift)
                    {
                        _selected.Clear();
                        _selectedStackId = string.Empty;
                    }

                    if (_selected.Contains(hitAvatar) && shift)
                        _selected.Remove(hitAvatar);
                    else
                        _selected.Add(hitAvatar);

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

                    _status = "已选 " + _selected.Count + " 人｜" + stateHint + "｜右键节点/道路移动";
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
                    _selectedStackId = hitStackId;
                    // 点敌军栈时保留已选己方，便于接着右键／菜单攻击
                    if (world.Strategic.Armies.TryGet(hitStackId, out var stack) && stack != null)
                    {
                        _status = DescribeStack(world, stack);
                        CollectSelectedParty(_scratchParty);
                        CollectOrderableParty(world, _scratchParty, _attackPartyScratch);
                        if (_attackPartyScratch.Count > 0)
                        {
                            _stackMenuStackId = hitStackId;
                            _stackMenuOpen = true;
                            _stackMenuRect = new Rect(mouse.x + 4f, mouse.y + 4f, 196f, 92f);
                        }
                        else
                        {
                            _stackMenuOpen = false;
                        }
                    }
                    else
                    {
                        _stackMenuOpen = false;
                    }

                    e.Use();
                    return;
                }

                for (var i = 0; i < _nodeRects.Count; i++)
                {
                    var (nodeId, rect) = _nodeRects[i];
                    if (!rect.Contains(mouse))
                        continue;

                    _nodeMenuNodeId = nodeId;
                    _nodeMenuOpen = true;
                    _nodeMenuRect = AnchorContextMenu(rect, 196f, 118f);
                    _stackMenuOpen = false;
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
                    _status = "已取消选择";
                }

                e.Use();
                return;
            }

            if (e.button != 1)
                return;

            if (TryHitAvatar(mouse, out var menuAvatarId))
            {
                var hitId = new EntityId(menuAvatarId);
                if (IsIncapacitatedEntity(world, hitId))
                {
                    if (TryDispatchSelectedLivingToIncap(world, graph, hitId, mouse))
                    {
                        e.Use();
                        return;
                    }

                    OpenIncapAvatarMenu(world, hitId, mouse);
                    e.Use();
                    return;
                }

                if (!_selected.Contains(menuAvatarId))
                {
                    _selected.Clear();
                    _selected.Add(menuAvatarId);
                }

                _status = "已选 " + EntityLabel(world, hitId) + "｜右键节点/道路移动";
                e.Use();
                return;
            }

            if (TryHitArmyStack(
                    world,
                    mouse,
                    out var menuStackId,
                    ArmyStackHitPad,
                    ArmyStackHitPadContested))
            {
                _stackMenuStackId = menuStackId;
                _stackMenuOpen = true;
                if (world.Strategic.Armies.TryGet(menuStackId, out var previewStack) && previewStack != null)
                {
                    _stackMenuRect = new Rect(mouse.x + 4f, mouse.y + 4f, 196f, 92f);
                    _status = DescribeStack(world, previewStack);
                }
                else
                {
                    _stackMenuRect = new Rect(mouse.x + 4f, mouse.y + 4f, 196f, 72f);
                }

                e.Use();
                return;
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
            else
            {
                ScreenToWorld(mapRect, mouse, out var wx, out var wy);
                var pickRadius = 14f / Mathf.Max(0.01f, MapScale(mapRect));
                if (WorldTravelPathService.TryPickRouteTarget(graph, wx, wy, pickRadius, out target))
                    hasTravelTarget = true;
            }

            if (hasTravelTarget)
            {
                CollectSelectedParty(_scratchParty);
                FilterOrderableParty(world, _scratchParty);
                if (_scratchParty.Count == 0)
                {
                    _status = "请先左键点选可下令的角色，再右键目标";
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

                var destLabel = target.Describe(graph);
                bootstrap.WorldTravelConfirm?.OpenTarget(_scratchParty, target, destLabel);
                _status = "等待确认移动到「" + destLabel + "」…";
                e.Use();
                return;
            }

            _status = "请点击节点或道路";
            e.Use();
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

        bool TryHitAvatar(Vector2 mouse, out ulong avatarId)
        {
            avatarId = 0;
            foreach (var kv in _avatarRects)
            {
                if (!kv.Value.Contains(mouse))
                    continue;
                avatarId = kv.Key;
                return true;
            }

            return false;
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
            HostUiHitTest.Block(_stackMenuRect);
            var prev = GUI.color;
            GUI.color = new Color(0.16f, 0.17f, 0.19f, 0.96f);
            GUI.DrawTexture(_stackMenuRect, _px);
            GUI.color = prev;

            var title = string.IsNullOrEmpty(stack.DisplayName) ? stack.Id : stack.DisplayName;
            GUI.Label(new Rect(_stackMenuRect.x + 8f, _stackMenuRect.y + 4f, _stackMenuRect.width - 16f, 18f), title, _body);
            var y = _stackMenuRect.y + 26f;
            var bw = _stackMenuRect.width - 16f;

            CollectSelectedParty(_scratchParty);
            CollectOrderableParty(world, _scratchParty, _attackPartyScratch);
            var hasParty = _attackPartyScratch.Count > 0;
            GUI.enabled = hasParty;

            var attackLabel = stack.IsBattlefieldRemnant ? "攻击（再入战场）" : "攻击";
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
            y += 26f;

            if (GUI.Button(new Rect(_stackMenuRect.x + 8f, y, bw, 22f), "查看详情"))
            {
                Event.current.Use();
                _status = DescribeStack(world, stack);
                _stackMenuOpen = false;
            }

            GUI.depth = prevDepth;
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
            ev.Use();
        }

        void DrawAvatarContextMenu(XianXia.Core.Simulation.SimulationWorld world)
        {
            if (!_avatarMenuOpen)
                return;
            var target = new EntityId(_avatarMenuEntityId);
            if (target.IsNone || !IsIncapacitatedEntity(world, target))
            {
                _avatarMenuOpen = false;
                return;
            }

            var prevDepth = GUI.depth;
            GUI.depth = -85;
            HostUiHitTest.Block(_avatarMenuRect);
            var prev = GUI.color;
            GUI.color = new Color(0.16f, 0.17f, 0.19f, 0.96f);
            GUI.DrawTexture(_avatarMenuRect, _px);
            GUI.color = prev;
            GUI.Label(
                new Rect(_avatarMenuRect.x + 8f, _avatarMenuRect.y + 4f, _avatarMenuRect.width - 16f, 18f),
                EntityLabel(world, target) + " · 弥留",
                _body);
            CollectLingeringViewParty(world, bootstrap.Session.CharacterIds, target, _attackPartyScratch);
            var hasLinger = BattleOfferService.HasLingeringBattlefield(world);
            var canView = hasLinger && _attackPartyScratch.Count > 0;
            if (!hasLinger)
            {
                GUI.Label(
                    new Rect(_avatarMenuRect.x + 8f, _avatarMenuRect.y + 24f, _avatarMenuRect.width - 16f, 16f),
                    "接战点已无残留战场",
                    _body);
            }

            GUI.enabled = canView;
            if (GUI.Button(
                    new Rect(_avatarMenuRect.x + 8f, _avatarMenuRect.y + 42f, _avatarMenuRect.width - 16f, 28f),
                    "查看（再入战场）") &&
                canView)
            {
                Event.current.Use();
                bootstrap.EnterLingeringBattlefield(_attackPartyScratch);
                _avatarMenuOpen = false;
                Close();
            }

            GUI.enabled = true;
            GUI.depth = prevDepth;
        }

        void DrawLingeringIncapAvatars(
            Rect mapRect,
            XianXia.Core.Simulation.SimulationWorld world)
        {
            var rt = world.Strategic?.Encounter;
            if (rt == null || (!rt.BattlefieldLingering && rt.SpawnedEntityIds.Count == 0))
                return;
            for (var i = 0; i < rt.SpawnedEntityIds.Count; i++)
            {
                var id = new EntityId(rt.SpawnedEntityIds[i]);
                if (!IsIncapacitatedEntity(world, id))
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
                var rect = new Rect(p.x - AvatarSize * 0.5f, p.y - AvatarSize * 0.5f, AvatarSize, AvatarSize);
                if (!rect.Overlaps(mapRect))
                    continue;
                _avatarRects[id.Value] = rect;
                var old = GUI.color;
                GUI.color = new Color(0.75f, 0.25f, 0.2f, 0.9f);
                GUI.DrawTexture(rect, _px);
                GUI.color = old;
                GUI.Label(rect, "弥", _avatarLabel);
            }
        }

        static bool IsIncapacitatedEntity(
            XianXia.Core.Simulation.SimulationWorld world,
            EntityId id)
        {
            if (world == null || id.IsNone)
                return false;
            if (!world.Entities.TryGet(id, out var ent) || ent == null)
                return false;
            return ent.TryGet<LifecycleComponent>(out var life) && life.IsIncapacitated;
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
            CollectOrderableParty(world, party, _scratchParty);
            if (_scratchParty.Count == 0)
            {
                _status = "所选角色正在途中或离场，无法再下令";
                return;
            }

            // BeginPursuitToStackAnchor 内部会再 BeginPursuit；这里先挂标记并尝试立即接战
            StrategicPursuitService.BeginPursuit(world, _scratchParty, stack);

            var ready = new List<EntityId>(_scratchParty.Count);
            StrategicEngageRules.CollectPartyReadyToEngageStack(world, _scratchParty, stack, ready);
            if (ready.Count > 0 &&
                BattleOfferService.TryBuildOfferForArmy(world, ready, stack, "主动接战"))
            {
                _status = "接战弹窗已打开（" + ready.Count + " 人先到）";
                return;
            }

            bootstrap.WorldTravelDeparture?.BeginPursuitToStackAnchor(_scratchParty, stack);
            var name = string.IsNullOrEmpty(stack.DisplayName) ? stack.Id : stack.DisplayName;
            _status = _scratchParty.Count + " 人出发攻击「" + name + "」（先到接战，后到可加入）";
        }

        static void FilterOrderableParty(
            XianXia.Core.Simulation.SimulationWorld world,
            List<EntityId> party)
        {
            for (var i = party.Count - 1; i >= 0; i--)
            {
                if (!WorldTravelService.CanReceiveTravelOrder(world, party[i]))
                    party.RemoveAt(i);
            }
        }

        void OpenIncapAvatarMenu(
            XianXia.Core.Simulation.SimulationWorld world,
            EntityId hitId,
            Vector2 mouse)
        {
            _avatarMenuEntityId = hitId.Value;
            _avatarMenuOpen = true;
            var hasLinger = BattleOfferService.HasLingeringBattlefield(world);
            _avatarMenuRect = new Rect(mouse.x + 4f, mouse.y + 4f, 196f, hasLinger ? 96f : 118f);
            CollectLingeringViewParty(world, bootstrap.Session.CharacterIds, hitId, _attackPartyScratch);
            _status = hasLinger && _attackPartyScratch.Count > 0
                ? EntityLabel(world, hitId) + "（弥留）｜可「查看」再入战场"
                : EntityLabel(world, hitId) + "（弥留）｜查看弥留者";
        }

        bool TryDispatchSelectedLivingToIncap(
            XianXia.Core.Simulation.SimulationWorld world,
            WorldGraphBoard graph,
            EntityId hitId,
            Vector2 mouse)
        {
            CollectSelectedLivingOrderableParty(world, _attackPartyScratch);
            if (_attackPartyScratch.Count == 0)
                return false;

            if (!TryBuildTravelTargetForIncap(world, graph, hitId, out var target, out var destLabel))
            {
                _status = "无法定位弥留者位置";
                return true;
            }

            var travelers = new List<EntityId>(_attackPartyScratch.Count);
            for (var i = 0; i < _attackPartyScratch.Count; i++)
            {
                var id = _attackPartyScratch[i];
                if (!world.WorldPresence.TryGet(id, out var wp) || wp == null)
                    continue;
                if (!WorldTravelPathService.CanAgentReachTarget(world, wp, target))
                    continue;
                travelers.Add(id);
            }

            if (travelers.Count == 0)
            {
                CollectLingeringViewParty(world, bootstrap.Session.CharacterIds, hitId, _scratchParty);
                if (BattleOfferService.HasLingeringBattlefield(world) && _scratchParty.Count > 0)
                {
                    OpenIncapAvatarMenu(world, hitId, mouse);
                    _status = EntityLabel(world, hitId) + "（弥留）｜队伍已在附近，可「查看」再入";
                    return true;
                }

                _status = "所选角色已在弥留者处或无法沿道路到达";
                return true;
            }

            bootstrap.WorldTravelConfirm?.OpenTarget(travelers, target, destLabel);
            _status = "派人探望「" + EntityLabel(world, hitId) + "」→ " + destLabel;
            return true;
        }

        void CollectSelectedLivingOrderableParty(
            XianXia.Core.Simulation.SimulationWorld world,
            List<EntityId> into)
        {
            into.Clear();
            CollectSelectedParty(_scratchParty);
            for (var i = 0; i < _scratchParty.Count; i++)
            {
                var id = _scratchParty[i];
                if (id.IsNone || !IsLivingForLingeringView(world, id))
                    continue;
                if (!WorldTravelService.CanReceiveTravelOrder(world, id))
                    continue;
                into.Add(id);
            }
        }

        static bool TryBuildTravelTargetForIncap(
            XianXia.Core.Simulation.SimulationWorld world,
            WorldGraphBoard graph,
            EntityId incapId,
            out WorldTravelTarget target,
            out string destLabel)
        {
            target = default;
            destLabel = string.Empty;
            if (world == null || incapId.IsNone)
                return false;

            var name = EntityLabel(world, incapId);
            if (world.WorldPresence.TryGet(incapId, out var wp) && wp != null)
            {
                if (wp.HasRoutePresentation && !string.IsNullOrEmpty(wp.RouteId))
                {
                    var progress = wp.Mode == PartyWorldPresenceMode.RouteAnchored
                        ? Mathf.Clamp01(wp.RouteAnchorProgress)
                        : Mathf.Clamp01(wp.TravelProgress);
                    target = WorldTravelTarget.OnRoute(
                        wp.RouteId,
                        wp.NodeId,
                        wp.DestNodeId,
                        progress);
                    destLabel = name + " 所在路段";
                    return true;
                }

                if (!string.IsNullOrEmpty(wp.NodeId))
                {
                    target = WorldTravelTarget.AtNode(wp.NodeId);
                    destLabel = target.Describe(graph);
                    return true;
                }
            }

            var snap = world.Strategic?.Participants;
            if (snap == null)
                return false;

            if (!string.IsNullOrEmpty(snap.BattleAnchorRouteId) &&
                snap.BattleAnchorProgress >= 0f &&
                graph != null &&
                graph.TryGetRoute(snap.BattleAnchorRouteId, out var route) &&
                route != null)
            {
                target = WorldTravelTarget.OnRoute(
                    snap.BattleAnchorRouteId,
                    route.FromNodeId,
                    route.ToNodeId,
                    snap.BattleAnchorProgress);
                destLabel = name + " 接战点";
                return true;
            }

            if (!string.IsNullOrEmpty(snap.BattleAnchorNodeId))
            {
                target = WorldTravelTarget.AtNode(snap.BattleAnchorNodeId);
                destLabel = target.Describe(graph);
                return true;
            }

            return false;
        }

        static void CollectOrderableParty(
            XianXia.Core.Simulation.SimulationWorld world,
            List<EntityId> from,
            List<EntityId> into)
        {
            into.Clear();
            for (var i = 0; i < from.Count; i++)
            {
                if (!WorldTravelService.CanReceiveTravelOrder(world, from[i]))
                    continue;
                into.Add(from[i]);
            }
        }

        /// <summary>
        /// 残留战场「查看」：优先接战锚点半径内存活者；若无则允许锚点上的弥留者单独再入（不要求另选人）。
        /// </summary>
        static bool CollectLingeringViewParty(
            XianXia.Core.Simulation.SimulationWorld world,
            IReadOnlyList<EntityId> roster,
            EntityId focusIncap,
            List<EntityId> into)
        {
            into.Clear();
            if (world?.Strategic == null || roster == null || into == null)
                return false;

            var snap = world.Strategic.Participants;
            if (snap == null ||
                (string.IsNullOrEmpty(snap.BattleAnchorNodeId) &&
                 string.IsNullOrEmpty(snap.BattleAnchorRouteId)))
                return false;

            var anchorNode = snap.BattleAnchorNodeId;
            var anchorRoute = snap.BattleAnchorRouteId;
            var anchorProgress = snap.BattleAnchorProgress;

            for (var i = 0; i < roster.Count; i++)
            {
                var id = roster[i];
                if (id.IsNone || !IsLivingForLingeringView(world, id))
                    continue;
                if (!world.WorldPresence.TryGet(id, out var wp) || wp == null)
                    continue;
                if (!WorldTravelService.CanReceiveTravelOrder(world, id))
                    continue;
                if (!ReinforcementRangeService.IsWithinReinforcementRange(
                        world, wp, anchorNode, anchorRoute, anchorProgress))
                    continue;
                into.Add(id);
            }

            if (into.Count > 0)
                return true;

            for (var i = 0; i < roster.Count; i++)
            {
                var id = roster[i];
                if (id.IsNone || !IsIncapacitatedEntity(world, id))
                    continue;
                if (!world.WorldPresence.TryGet(id, out var wp) || wp == null)
                    continue;
                if (!ReinforcementRangeService.IsWithinReinforcementRange(
                        world, wp, anchorNode, anchorRoute, anchorProgress))
                    continue;
                into.Add(id);
            }

            if (into.Count == 0 &&
                !focusIncap.IsNone &&
                IsIncapacitatedEntity(world, focusIncap) &&
                world.WorldPresence.TryGet(focusIncap, out var focusWp) &&
                focusWp != null &&
                ReinforcementRangeService.IsWithinReinforcementRange(
                    world, focusWp, anchorNode, anchorRoute, anchorProgress))
            {
                into.Add(focusIncap);
            }

            return into.Count > 0;
        }

        static bool IsLivingForLingeringView(
            XianXia.Core.Simulation.SimulationWorld world,
            EntityId id)
        {
            if (world == null || id.IsNone || !world.Entities.TryGet(id, out var ent) || ent == null)
                return false;
            if (!ent.TryGet<LifecycleComponent>(out var life) || life == null)
                return true;
            return !life.IsIncapacitated && !life.IsDead && !life.IsRemoved;
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
