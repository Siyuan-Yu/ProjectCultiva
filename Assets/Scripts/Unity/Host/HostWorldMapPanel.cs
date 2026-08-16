using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.World;

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
        /// <summary>
        /// 最大放大：视口半宽（世界单位）。再放大一倍相对「邻站铺满」参考（半宽 1.5 ≈ 满屏跨度 3）。
        /// </summary>
        const float MinViewHalfExtent = 1.5f;
        const float MapPad = 48f;

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

        // 头像右键菜单
        ulong _menuAvatar;
        Rect _menuRect;
        bool _menuOpen;

        string _status = string.Empty;
        bool _wasBlockingInput;
        int _travelingCountLast;
        float _travelAccum;

        // 地图镜头：世界坐标中心 + 半宽（世界单位）
        float _viewCx;
        float _viewCy;
        float _viewHalf;
        float _fullHalf = MinViewHalfExtent;
        bool _viewReady;
        bool _panning;
        Vector2 _panLastGui;

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
        }

        public void Close()
        {
            open = false;
            _requestClose = false;
            _menuOpen = false;
            _menuAvatar = 0;
            _panning = false;
            ForceClearInputBlock();
        }

        public void Bind(PlayableHostBootstrap host) => bootstrap = host;

        public void ClearSessionState()
        {
            Close();
            _status = string.Empty;
            _selected.Clear();
            _travelingCountLast = 0;
            _viewReady = false;
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
                if (bootstrap.Session.IsPaused)
                    DriveTravelWhilePaused();
                else
                    WatchArrivals();
            }
            else
            {
                if (_wasBlockingInput)
                    ForceClearInputBlock();
                _travelAccum = 0f;
                _panning = false;
            }
        }

        void DriveTravelWhilePaused()
        {
            var world = bootstrap.Session.World;
            var speed = bootstrap.EffectiveSpeedMultiplier();
            var interval = bootstrap.SecondsPerAutoTickAt1x;
            _travelAccum += Time.unscaledDeltaTime * Mathf.Max(0.1f, speed);
            while (_travelAccum >= interval)
            {
                _travelAccum -= interval;
                _arrivedScratch.Clear();
                WorldTravelService.AdvanceTravel(world, 1, _arrivedScratch);
                if (_arrivedScratch.Count > 0)
                {
                    WorldTravelService.SyncPartyFocus(world);
                    _status = "有人抵达站点";
                }
            }

            WatchArrivals();
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
                "  （灰=停留　棕=未出行可打断　蓝=大地图途中｜右键节点确认出行｜M 关闭）",
                _title);

            if (GUI.Button(new Rect(Screen.width - 100f, 10f, 84f, 32f), "关闭"))
                Close();

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

            var mapTop = topBar + 28f;
            var mapRect = new Rect(pad, mapTop, Screen.width - pad * 2f, Screen.height - mapTop - pad);
            GUI.color = new Color(0.12f, 0.14f, 0.16f, 1f);
            GUI.DrawTexture(mapRect, _px);
            GUI.color = Color.white;

            HandleCameraInput(mapRect);
            DrawGraph(mapRect, world, graph);
            HandleMapInput(mapRect, world, graph);
            DrawAvatarContextMenu(world, graph);
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
            // 先画头像（底层半透明），再画节点，保证地名不被挡住
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
                if (p.Mode == PartyWorldPresenceMode.Traveling)
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
                if (presence.Mode == PartyWorldPresenceMode.Traveling)
                {
                    var t = presence.TravelProgress;
                    wx = Mathf.Lerp(fx, tx, t);
                    wy = Mathf.Lerp(fy, ty, t);
                }

                var basePos = Project(mapRect, wx, wy);
                Vector2 center;
                if (presence.Mode == PartyWorldPresenceMode.Traveling)
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
                var traveling = presence.Mode == PartyWorldPresenceMode.Traveling;
                // 半透明，避免压住地名
                var fill = selected
                    ? new Color(0.92f, 0.72f, 0.22f, 0.55f)
                    : traveling
                        ? new Color(0.35f, 0.55f, 0.85f, 0.50f)
                        : presence.Mode == PartyWorldPresenceMode.DepartingLocalMap
                            ? new Color(0.75f, 0.55f, 0.35f, 0.50f)
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
            if (e == null || e.type != EventType.MouseDown)
                return;
            if (!mapRect.Contains(e.mousePosition))
                return;
            if (e.button == 2)
                return;

            var mouse = e.mousePosition;

            if (e.button == 0)
            {
                ulong hitAvatar = 0;
                var found = false;
                foreach (var kv in _avatarRects)
                {
                    if (!kv.Value.Contains(mouse))
                        continue;
                    hitAvatar = kv.Key;
                    found = true;
                    break;
                }

                if (found)
                {
                    var shift = e.shift;
                    if (!shift)
                        _selected.Clear();
                    if (_selected.Contains(hitAvatar) && shift)
                        _selected.Remove(hitAvatar);
                    else
                        _selected.Add(hitAvatar);
                    _status = "已选 " + _selected.Count + " 人｜右键相邻节点出发";
                    e.Use();
                    return;
                }

                for (var i = 0; i < _nodeRects.Count; i++)
                {
                    var (nodeId, rect) = _nodeRects[i];
                    if (!rect.Contains(mouse))
                        continue;
                    var focus = WorldTravelService.FocusNode(world, nodeId);
                    if (focus.IsFailure)
                    {
                        _status = FormatFail(focus);
                    }
                    else
                    {
                        if (graph.TryGetNode(nodeId, out var n))
                        {
                            _viewCx = n.WorldX;
                            _viewCy = n.WorldY;
                        }

                        bootstrap.ApplyPartyWorldNodePresentation();
                        var name = nodeId;
                        if (graph.TryGetNode(nodeId, out var nn))
                            name = string.IsNullOrEmpty(nn.Name) ? nn.Id : nn.Name;
                        _status = "焦点 → " + name;
                    }

                    if (!e.shift)
                        _selected.Clear();
                    e.Use();
                    return;
                }

                if (!e.shift)
                {
                    _selected.Clear();
                    _status = "已取消选择";
                }

                e.Use();
                return;
            }

            if (e.button != 1)
                return;

            // 右键头像：打开菜单（查看／进入场景）
            foreach (var kv in _avatarRects)
            {
                if (!kv.Value.Contains(mouse))
                    continue;
                _menuAvatar = kv.Key;
                _menuOpen = true;
                _menuRect = new Rect(mouse.x + 4f, mouse.y + 4f, 168f, 54f);
                if (!_selected.Contains(kv.Key))
                {
                    _selected.Clear();
                    _selected.Add(kv.Key);
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

            if (string.IsNullOrEmpty(destId))
            {
                _menuOpen = false;
                e.Use();
                return;
            }

            CollectSelectedParty(_scratchParty);
            if (_scratchParty.Count == 0)
            {
                _status = "请先左键点选头像，再右键目标节点";
                e.Use();
                return;
            }

            // 仅 AtNode 可下令（途中／离场中不可）
            for (var i = _scratchParty.Count - 1; i >= 0; i--)
            {
                if (!world.WorldPresence.TryGet(_scratchParty[i], out var p) ||
                    p.Mode != PartyWorldPresenceMode.AtNode)
                    _scratchParty.RemoveAt(i);
            }

            if (_scratchParty.Count == 0)
            {
                _status = "所选角色正在途中或离场，无法再下令";
                e.Use();
                return;
            }

            var destName = destId;
            if (graph.TryGetNode(destId, out var destNode))
                destName = string.IsNullOrEmpty(destNode.Name) ? destNode.Id : destNode.Name;

            // 先校验至少一人有直达路
            var anyRoute = false;
            for (var i = 0; i < _scratchParty.Count; i++)
            {
                if (!world.WorldPresence.TryGet(_scratchParty[i], out var p))
                    continue;
                if (world.WorldGraph.TryFindRoute(p.NodeId, destId, out _))
                {
                    anyRoute = true;
                    break;
                }
            }

            if (!anyRoute)
            {
                _status = "所选角色与「" + destName + "」无相邻道路";
                e.Use();
                return;
            }

            _menuOpen = false;
            bootstrap.WorldTravelConfirm?.Open(_scratchParty, destId, destName);
            _status = "等待确认出行…";
            e.Use();
        }

        void DrawAvatarContextMenu(XianXia.Core.Simulation.SimulationWorld world, WorldGraphBoard graph)
        {
            if (!_menuOpen || _menuAvatar == 0)
                return;

            var id = new EntityId(_menuAvatar);
            if (!world.WorldPresence.TryGet(id, out var presence) || presence == null)
            {
                _menuOpen = false;
                return;
            }

            HostUiHitTest.Block(_menuRect);
            var prev = GUI.color;
            GUI.color = new Color(0.16f, 0.17f, 0.19f, 0.96f);
            GUI.DrawTexture(_menuRect, _px);
            GUI.color = prev;

            var name = EntityLabel(world, id);
            GUI.Label(new Rect(_menuRect.x + 8f, _menuRect.y + 4f, _menuRect.width - 16f, 18f), name, _body);

            var y = _menuRect.y + 26f;
            WorldNodeState node = null;
            var canEnter = presence.Mode == PartyWorldPresenceMode.AtNode &&
                           !string.IsNullOrEmpty(presence.NodeId) &&
                           graph.TryGetNode(presence.NodeId, out node) &&
                           node != null;
            var placeName = node == null
                ? ""
                : (string.IsNullOrEmpty(node.Name) ? node.Id : node.Name);
            var enterLabel = canEnter
                ? "进入 " + placeName
                : presence.Mode == PartyWorldPresenceMode.Traveling
                    ? "途中，无法进入场景"
                    : presence.Mode == PartyWorldPresenceMode.DepartingLocalMap
                        ? "正在离场…"
                        : "无法进入";

            GUI.enabled = canEnter;
            if (GUI.Button(new Rect(_menuRect.x + 8f, y, _menuRect.width - 16f, 22f), enterLabel) && canEnter)
            {
                var enter = WorldTravelService.EnterNodeScene(world, presence.NodeId);
                if (enter.IsSuccess)
                {
                    // 必须立刻关大地图（含 bootstrap 引用的实例），再刷 LocalMap
                    CloseAllWorldMapPanels();
                    bootstrap.ApplyPartyWorldNodePresentation(closeWorldMap: true);
                    _status = "已进入 " + placeName;
                }
                else
                {
                    _status = FormatFail(enter);
                    _menuOpen = false;
                }
            }

            GUI.enabled = true;

            var e = Event.current;
            if (e != null && e.type == EventType.MouseDown && !_menuRect.Contains(e.mousePosition))
                _menuOpen = false;
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
