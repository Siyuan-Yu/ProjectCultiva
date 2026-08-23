using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using XianXia.Core.Attributes;
using XianXia.Core.Combat;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
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

        static float ResolveMinViewHalf(SimulationWorld world)
        {
            if (world != null && ArmyHexCommandService.IsHexStrategicActive(world) && world.HexWorld.HasGrid)
            {
                return HexWorldScale.ViewHalfForHexesAcross(
                    HexWorldScale.CloseHexesAcross,
                    world.HexWorld.HexSize);
            }

            return MinViewHalfExtent;
        }
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
        /// <summary>大地图选中单位：头像外圈高亮（与填充色区分）。</summary>
        static readonly Color MapSelectionRingColor = new(0.22f, 0.94f, 1f, 0.95f);
        const float MapSelectionRingWidth = 3f;

        enum MacroPartyKind
        {
            MoveOrAttack,
            LingeringView,
        }

        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] KeyCode toggleKey = KeyCode.M;
        [SerializeField] bool open;

        readonly List<HexCoord> _hexPathPreview = new List<HexCoord>(32);
        bool[] _pathMask;
        int _pathMaskW;
        int _pathMaskH;
        bool _terrainLegendExpanded;
        float _lastMapViewportWidth = 800f;
        float _lastMapViewportHeight = 600f;
        HexCoord? _selectedHex;
        HexCoord? _hoverHex;
        HexCoord? _lastHoverHex;
        readonly HashSet<ulong> _selected = new HashSet<ulong>();
        readonly List<EntityId> _scratchParty = new List<EntityId>(8);
        readonly List<EntityId> _arrivedScratch = new List<EntityId>(8);
        readonly Dictionary<ulong, Rect> _avatarRects = new Dictionary<ulong, Rect>();
        readonly List<(ResidualMarkerGroupView group, Rect rect)> _residualMarkerRects =
            new List<(ResidualMarkerGroupView, Rect)>(16);
        ResidualMarkerGroupView _selectedResidualGroup;
        readonly List<(string nodeId, Rect rect)> _nodeRects = new List<(string, Rect)>(64);
        readonly Dictionary<string, int> _slotAtNode = new Dictionary<string, int>();
        readonly Dictionary<string, int> _countAtNode = new Dictionary<string, int>();

        // 部队栈点选／右键菜单
        readonly Dictionary<string, Rect> _armyStackRects = new Dictionary<string, Rect>(16);
        readonly Dictionary<string, Rect> _formalArmyRects = new Dictionary<string, Rect>(8);
        string _selectedFormalArmyId = string.Empty;
        string _lastMapFormalArmyClickId = string.Empty;
        double _lastMapFormalArmyClickTime;
        HostArmyFormPanel _armyFormPanel;
        HostStrategicArmyListPanel _armyListPanel;
        HostStrategicCharacterListPanel _characterListPanel;
        readonly HostGlobalStrategicToolbar _globalStrategicToolbar = new HostGlobalStrategicToolbar();
        string _selectedStackId = string.Empty;
        string _stackMenuStackId = string.Empty;
        Rect _stackMenuRect;
        bool _stackMenuOpen;
        /// <summary>攻击／移动／进入菜单的输出缓冲（可与选人缓冲分离）。</summary>
        readonly List<EntityId> _attackPartyScratch = new List<EntityId>(8);
        /// <summary>仅作「当前选中 → 过滤」中间表；禁止当作最终 into，避免 Clear 自清。</summary>
        readonly List<EntityId> _orderFilterScratch = new List<EntityId>(8);
        readonly List<string> _previewPathScratch = new List<string>(16);
        string _orderPreviewArmyId = string.Empty;
        WorldTravelTarget _orderPreviewTarget;
        bool _orderPreviewActive;

        // 弥留头像右键：进入残留战场／有活人时「前往并进入」
        ulong _avatarMenuEntityId;
        bool _avatarMenuOpen;
        bool _avatarMenuVisitMode;
        Rect _avatarMenuRect;

        // 节点左键菜单
        string _nodeMenuNodeId = string.Empty;
        Rect _nodeMenuRect;
        bool _nodeMenuOpen;

        // Hex 右键 Context（残留战场正式入口）
        bool _hexMenuOpen;
        HexCoord _hexMenuHex;
        Rect _hexMenuRect;
        HexResidualContextView _hexMenuContext;
        HexRightClickResolution _hexMenuResolution;
        /// <summary>右侧信息面板聚焦的节点（左键点节点写入；与菜单开闭无关）。</summary>
        string _inspectNodeId = string.Empty;
        Vector2 _inspectScroll;

        string _status = string.Empty;
        bool _wasBlockingInput;
        int _travelingCountLast;
        readonly Dictionary<string, string> _lastNodeOwners = new Dictionary<string, string>(StringComparer.Ordinal);

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
            ClearArmyOrderPreview();
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                FormalArmyStrategicMutationDiagnostics.BindWorldGraphForPresentationCheck(world);
#endif
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
            _hexMenuOpen = false;
            _avatarMenuOpen = false;
            _avatarMenuVisitMode = false;
            _armyFormPanel?.Close();
            _armyListPanel?.Close();
            _characterListPanel?.Close();
            _globalStrategicToolbar.CloseAll();
            _selectedFormalArmyId = string.Empty;
            ClearArmyOrderPreview();
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

        public void SetArmyOrderPreview(string armyId, WorldTravelTarget target)
        {
            _orderPreviewArmyId = armyId ?? string.Empty;
            _orderPreviewTarget = target;
            _orderPreviewActive = !string.IsNullOrEmpty(_orderPreviewArmyId);
            _hexPathPreview.Clear();
        }

        /// <summary>
        /// 兼容旧调用点：Hex 路线 overlay 仅由 RefreshSelectedArmyPathPreview 驱动。
        /// </summary>
        public void SetArmyHexPathPreview(string armyId, HexCoord destination)
        {
            _ = destination;
            if (!string.IsNullOrEmpty(armyId) &&
                !string.Equals(armyId, _selectedFormalArmyId, StringComparison.Ordinal))
                _hexPathPreview.Clear();
        }

        void ClearArmyOrderPreview()
        {
            _orderPreviewArmyId = string.Empty;
            _orderPreviewActive = false;
            _hexPathPreview.Clear();
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
            _selectedFormalArmyId = string.Empty;
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
                WatchNodeOwnerChanges(world);
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

        void EnsureView(
            WorldGraphBoard graph,
            XianXia.Core.Simulation.SimulationWorld world,
            float mapViewportWidth,
            float mapViewportHeight)
        {
            ComputeFullHalf(graph, world, mapViewportWidth, mapViewportHeight, out _fullHalf);
            if (_viewReady)
            {
                _viewHalf = Mathf.Clamp(_viewHalf, ResolveMinViewHalf(world), _fullHalf);
                ClampHexCamera(mapViewportWidth, mapViewportHeight, world);
                return;
            }

            var hexMode = ArmyHexCommandService.IsHexStrategicActive(world) && world.HexWorld.HasGrid;
            _viewHalf = hexMode
                ? HexWorldScale.ViewHalfForHexesAcross(
                    HexWorldScale.DefaultHexesAcross,
                    world.HexWorld.HexSize)
                : ResolveMinViewHalf(world);
            _viewCx = 0f;
            _viewCy = 0f;
            if (hexMode)
            {
                HexWorldLayout.ComputeWorldCenter(world.HexWorld, out _viewCx, out _viewCy);
                if (!string.IsNullOrEmpty(_selectedFormalArmyId) &&
                    world.Strategic.FormalArmies.TryGet(_selectedFormalArmyId, out var army) &&
                    army != null &&
                    army.UsesHexStrategicPosition)
                {
                    HexMath.ToWorldPosition(army.CurrentHex, world.HexWorld.HexSize, out _viewCx, out _viewCy);
                }
                else
                {
                    var focusId = world.PartyWorld.NodeId;
                    if (ArmyHexBattleAnchorService.TryResolveHexForNode(world, focusId, out var focusHex))
                        HexMath.ToWorldPosition(focusHex, world.HexWorld.HexSize, out _viewCx, out _viewCy);
                }
            }
            else
            {
                var focusId = world.PartyWorld.NodeId;
                if (!string.IsNullOrEmpty(focusId) && graph.TryGetNode(focusId, out var focus))
                {
                    _viewCx = focus.WorldX;
                    _viewCy = focus.WorldY;
                }
            }

            ClampHexCamera(mapViewportWidth, mapViewportHeight, world);
            _viewReady = true;
        }

        void ClampHexCamera(float mapViewportWidth, float mapViewportHeight, SimulationWorld world)
        {
            if (world?.HexWorld == null || !world.HexWorld.HasGrid ||
                !ArmyHexCommandService.IsHexStrategicActive(world))
                return;

            HexWorldLayout.ClampViewCenter(
                world.HexWorld,
                mapViewportWidth,
                mapViewportHeight,
                _viewHalf,
                HexWorldLayout.DefaultCameraMargin,
                ref _viewCx,
                ref _viewCy);
        }

        static void ComputeFullHalf(
            WorldGraphBoard graph,
            SimulationWorld world,
            float mapViewportWidth,
            float mapViewportHeight,
            out float fullHalf)
        {
            if (ArmyHexCommandService.IsHexStrategicActive(world) && world?.HexWorld != null && world.HexWorld.HasGrid)
            {
                var fitHalf = HexWorldLayout.ComputeFitViewHalf(
                    mapViewportWidth,
                    mapViewportHeight,
                    world.HexWorld);
                var closeHalf = HexWorldScale.ViewHalfForHexesAcross(
                    HexWorldScale.CloseHexesAcross,
                    world.HexWorld.HexSize);
                fullHalf = Mathf.Max(fitHalf, closeHalf);
                return;
            }

            ComputeFullHalfFromGraph(graph, out fullHalf);
        }

        static void ComputeFullHalfFromGraph(WorldGraphBoard graph, out float fullHalf)
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

            const float titleY = 10f;
            const float toolbarY = 42f;
            const float statusY = 74f;
            const float mapTop = 104f;
            const float pad = 16f;

            var world = bootstrap.Session.World;
            var graph = world.WorldGraph;

            GUI.Label(
                new Rect(pad, titleY, Screen.width - 220f, 28f),
                ArmyHexCommandService.IsHexStrategicActive(world)
                    ? "大地图  Hex 战略格  （左键：选格/军团｜右键：移动/攻击/残留格进入｜Ctrl+左键：道路｜M 关闭）"
                    : "大地图  " + (string.IsNullOrEmpty(graph.GraphName) ? graph.GraphId : graph.GraphName) +
                      "  （左键：选军团／选弥留｜右键：移动/攻击/我方弥留进入｜M 关闭）",
                _title);

            if (GUI.Button(new Rect(Screen.width - 100f, titleY, 84f, 32f), "关闭"))
                Close();

            DrawMapToolbar(pad, toolbarY, world);

            if (!graph.HasGraph)
            {
                GUI.Label(new Rect(pad, toolbarY, Screen.width - pad * 2f, 40f), "未加载 WorldGraph。", _body);
                return;
            }

            _lastMapViewportWidth = Screen.width - pad * 2f - InfoPanelW - 8f;
            _lastMapViewportHeight = Screen.height - mapTop - pad - BottomBarH;
            EnsureView(graph, world, _lastMapViewportWidth, _lastMapViewportHeight);

            var focusName = world.PartyWorld.NodeId;
            if (graph.TryGetNode(world.PartyWorld.NodeId, out var focusNode))
                focusName = string.IsNullOrEmpty(focusNode.Name) ? focusNode.Id : focusNode.Name;

            var zoomPct = Mathf.Approximately(_fullHalf, MinViewHalfExtent)
                ? 100
                : Mathf.RoundToInt(100f * (1f - (_viewHalf - MinViewHalfExtent) / (_fullHalf - MinViewHalfExtent)));

            GUI.Label(
                new Rect(pad, statusY, Screen.width - pad * 2f - InfoPanelW - 8f, 22f),
                "镜头：" + focusName +
                "　已选 " + FormatSelectionSummary() +
                "　缩放 " + zoomPct + "%（最大：邻站铺满屏／最小：全图）" +
                (string.IsNullOrEmpty(_status) ? "" : "　｜　" + _status),
                _body);

            var mapRect = new Rect(
                pad,
                mapTop,
                _lastMapViewportWidth,
                _lastMapViewportHeight);
            var infoRect = new Rect(
                mapRect.xMax + 8f,
                mapTop,
                InfoPanelW,
                mapRect.height);
            var hexGutterActive = ArmyHexCommandService.IsHexStrategicActive(world) &&
                                  world?.HexWorld != null &&
                                  world.HexWorld.HasGrid;
            GUI.color = hexGutterActive
                ? HostHexWorldRenderer.ResolveGutterColor()
                : new Color(0.93f, 0.89f, 0.78f, 1f);
            GUI.DrawTexture(mapRect, _px);
            GUI.color = Color.white;

            HandleCameraInput(mapRect, world);
            HexMapViewportProjection hexProjection = default;
            if (ArmyHexCommandService.IsHexStrategicActive(world) &&
                world?.HexWorld != null &&
                world.HexWorld.HasGrid)
            {
                hexProjection = BuildHexProjection(mapRect, world);
            }

            RefreshHexPresentation(hexProjection, world);
            DrawGraph(mapRect, hexProjection, world, graph);
            DrawMapUnitOverlays(mapRect, hexProjection, world, graph);
            if (ShowReinforcementRadiusDebug)
                DrawReinforcementRadiusOverlay(mapRect, world);
            DrawNodeContextMenu(mapRect, world, graph);
            DrawHexContextMenu(world);
            DrawStackContextMenu(world, graph);
            DrawAvatarContextMenu(world);
            DrawInspectPanel(infoRect, world, graph);
            DrawReinforcementRadiusSlider(pad, world);
            DrawStrategicRosterPanels(world);
            TryDismissContextMenusOnOutsideClick();
            if (Event.current != null && Event.current.type == EventType.Used)
                return;
            // 菜单仍开着（点在菜单内）时不处理地图下令；外侧点击已在上面关掉菜单且不吞事件
            if (_stackMenuOpen || _nodeMenuOpen || _avatarMenuOpen || _hexMenuOpen)
                return;
            HandleMapInput(mapRect, hexProjection, world, graph);
            HostUiHitTest.EndFrame();
            // 进入场景可能在本帧 OnGUI 中途关掉；立刻停画，避免同帧再盖一层
            if (!open)
                return;
        }

        void HandleCameraInput(Rect mapRect, XianXia.Core.Simulation.SimulationWorld world)
        {
            var e = Event.current;
            if (e == null || !mapRect.Contains(e.mousePosition) && e.type != EventType.MouseUp)
                return;

            var minHalf = ResolveMinViewHalf(world);
            var hexMode = ArmyHexCommandService.IsHexStrategicActive(world) && world?.HexWorld != null && world.HexWorld.HasGrid;
            var projection = hexMode ? BuildHexProjection(mapRect, world) : default;

            if (e.type == EventType.ScrollWheel && mapRect.Contains(e.mousePosition))
            {
                Vector2 worldPoint;
                if (hexMode)
                    worldPoint = projection.ScreenToWorld(e.mousePosition);
                else
                {
                    ScreenToWorld(mapRect, e.mousePosition, out var wx, out var wy);
                    worldPoint = new Vector2(wx, wy);
                }

                var before = _viewHalf;
                var factor = e.delta.y > 0f ? 1.12f : 1f / 1.12f;
                _viewHalf = Mathf.Clamp(before * factor, minHalf, _fullHalf);
                var t = 1f - _viewHalf / before;
                if (before > 0.01f)
                {
                    _viewCx += (worldPoint.x - _viewCx) * t;
                    _viewCy += (worldPoint.y - _viewCy) * t;
                }

                if (hexMode)
                    ClampHexCamera(mapRect.width, mapRect.height, world);

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
                var scale = hexMode ? projection.Scale : MapScale(mapRect);
                _viewCx -= delta.x / scale;
                _viewCy += delta.y / scale;
                if (hexMode)
                    ClampHexCamera(mapRect.width, mapRect.height, world);
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

        HexMapViewportProjection BuildHexProjection(Rect mapRect, XianXia.Core.Simulation.SimulationWorld world) =>
            new HexMapViewportProjection(
                mapRect,
                _viewCx,
                _viewCy,
                _viewHalf,
                world.HexWorld.HexSize);

        float MapScale(Rect mapRect) =>
            Mathf.Min(mapRect.width, mapRect.height) / (2f * Mathf.Max(0.01f, _viewHalf));

        Vector2 Project(Rect mapRect, float wx, float wy)
        {
            var scale = MapScale(mapRect);
            var cx = mapRect.x + mapRect.width * 0.5f;
            var cy = mapRect.y + mapRect.height * 0.5f;
            return new Vector2(
                cx + (wx - _viewCx) * scale,
                cy - (wy - _viewCy) * scale);
        }

        Vector2 ProjectHex(Rect mapRect, XianXia.Core.Simulation.SimulationWorld world, float wx, float wy)
        {
            if (ArmyHexCommandService.IsHexStrategicActive(world) && world?.HexWorld != null && world.HexWorld.HasGrid)
                return BuildHexProjection(mapRect, world).ProjectWorld(wx, wy);
            return Project(mapRect, wx, wy);
        }

        void ScreenToWorld(Rect mapRect, Vector2 gui, out float wx, out float wy)
        {
            var scale = MapScale(mapRect);
            var cx = mapRect.x + mapRect.width * 0.5f;
            var cy = mapRect.y + mapRect.height * 0.5f;
            wx = _viewCx + (gui.x - cx) / scale;
            wy = _viewCy - (gui.y - cy) / scale;
        }

        void WatchNodeOwnerChanges(XianXia.Core.Simulation.SimulationWorld world)
        {
            if (world?.WorldGraph?.Nodes == null)
                return;

            foreach (var kv in world.WorldGraph.Nodes)
            {
                var node = kv.Value;
                if (node == null)
                    continue;

                var owner = node.OwnerId ?? string.Empty;
                if (_lastNodeOwners.TryGetValue(node.Id, out var prev) &&
                    !string.Equals(prev, owner, StringComparison.Ordinal) &&
                    world.Strategic?.CaptureObjectives != null &&
                    world.Strategic.CaptureObjectives.AllCompletedForNode(node.Id))
                {
                    var nodeName = string.IsNullOrEmpty(node.Name) ? node.Id : node.Name;
                    _status = "Node captured: " + nodeName + "  New Owner: " +
                              StrategicAcceptanceInspector.ResolveOwnerDisplay(owner);
                }

                _lastNodeOwners[node.Id] = owner;
            }
        }

        void DrawMapToolbar(float pad, float toolbarY, XianXia.Core.Simulation.SimulationWorld world)
        {
            var y = toolbarY;
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
            x += 230f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (ArmyHexCommandService.IsHexStrategicActive(world))
            {
                var strongSep = HostHexWorldRenderer.DebugStrongHexSeparation;
                var nextStrongSep = GUI.Toggle(
                    new Rect(x, y + 2f, 260f, 24f),
                    strongSep,
                    " Debug：强 Hex Separation");
                if (nextStrongSep != strongSep)
                    HostHexWorldRenderer.DebugStrongHexSeparation = nextStrongSep;
                x += 268f;
            }
#endif

            if (bootstrap.StrategicAcceptancePanel != null &&
                GUI.Button(new Rect(x, y, 120f, 26f), "战略验收 F8"))
            {
                bootstrap.StrategicAcceptancePanel.Toggle();
            }

            x += 128f;
            EnsureStrategicRosterPanels();
            var clickedModule = _globalStrategicToolbar.Draw(x, y, _body);
            if (clickedModule != HostGlobalStrategicToolbar.ModuleId.None)
                HandleGlobalStrategicToolbarClick(clickedModule);
            x += _globalStrategicToolbar.LastDrawnWidth;

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

        void RefreshHexPresentation(HexMapViewportProjection projection, XianXia.Core.Simulation.SimulationWorld world)
        {
            if (!ArmyHexCommandService.IsHexStrategicActive(world) || world?.HexWorld == null || !world.HexWorld.HasGrid)
                return;

            var ev = Event.current;
            if (ev != null && ev.type != EventType.Repaint && ev.type != EventType.MouseMove)
                return;

            var mouse = ev != null ? ev.mousePosition : Vector2.zero;
            if (HexMapMousePick.TryResolveMouseHex(projection, world.HexWorld, mouse, out var hover))
                _hoverHex = hover;
            else
                _hoverHex = null;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_hoverHex.HasValue &&
                !projection.ValidateProjectionRoundTrip(_hoverHex.Value, out var roundTripped))
            {
                Debug.LogWarning(
                    "HexMap projection round-trip failed at hover " + _hoverHex.Value +
                    " → " + roundTripped);
            }
#endif

            if (_hoverHex != _lastHoverHex)
                _lastHoverHex = _hoverHex;

            RefreshSelectedArmyPathPreview(world);
        }

        /// <summary>
        /// 仅当前选中的我方军团且有有效移动计划时，填充 Hex 路线 overlay（线 + 格高亮共用）。
        /// </summary>
        void RefreshSelectedArmyPathPreview(SimulationWorld world)
        {
            _hexPathPreview.Clear();
            if (!TryGetSelectedPlayerArmyForPathPreview(world, out var army))
                return;

            _hexPathPreview.Add(army.CurrentHex);
            var path = army.HexPath;
            for (var i = army.CurrentPathIndex; i < army.HexPathCount; i++)
                _hexPathPreview.Add(path[i]);
            if (_hexPathPreview.Count == 1 && army.DestinationHex != army.CurrentHex)
                _hexPathPreview.Add(army.DestinationHex);
        }

        /// <summary>路线预览：SELF + 有效 Hex TravelPlan（剩余路径）才显示。</summary>
        bool TryGetSelectedPlayerArmyForPathPreview(
            SimulationWorld world,
            out FormalArmy army)
        {
            army = null;
            if (world?.Strategic?.FormalArmies == null ||
                string.IsNullOrEmpty(_selectedFormalArmyId))
                return false;

            if (!world.Strategic.FormalArmies.TryGet(_selectedFormalArmyId, out army) || army == null)
                return false;

            var playerFaction = ResolvePlayerFactionId(world);
            if (string.IsNullOrEmpty(playerFaction) ||
                !string.Equals(army.FactionId, playerFaction, StringComparison.Ordinal))
            {
                army = null;
                return false;
            }

            if (!army.UsesHexStrategicPosition || army.State != FormalArmyState.Moving)
            {
                army = null;
                return false;
            }

            if (army.CurrentPathIndex >= army.HexPathCount &&
                army.DestinationHex == army.CurrentHex)
            {
                army = null;
                return false;
            }

            return true;
        }

        bool TryGetSelectedPlayerArmy(
            SimulationWorld world,
            out FormalArmy army)
        {
            army = null;
            if (world?.Strategic?.FormalArmies == null ||
                string.IsNullOrEmpty(_selectedFormalArmyId))
                return false;

            if (!world.Strategic.FormalArmies.TryGet(_selectedFormalArmyId, out army) || army == null)
                return false;

            var playerFaction = ResolvePlayerFactionId(world);
            if (string.IsNullOrEmpty(playerFaction) ||
                !string.Equals(army.FactionId, playerFaction, StringComparison.Ordinal))
            {
                army = null;
                return false;
            }

            return true;
        }

        void FillPathMask(SimulationWorld world)
        {
            if (world?.HexWorld == null || !world.HexWorld.HasGrid)
                return;

            _pathMaskW = world.HexWorld.Width;
            _pathMaskH = world.HexWorld.Height;
            var need = _pathMaskW * _pathMaskH;
            if (_pathMask == null || _pathMask.Length < need)
                _pathMask = new bool[need];

            Array.Clear(_pathMask, 0, need);
            for (var i = 0; i < _hexPathPreview.Count; i++)
            {
                var c = _hexPathPreview[i];
                if (c.Q < 0 || c.R < 0 || c.Q >= _pathMaskW || c.R >= _pathMaskH)
                    continue;
                _pathMask[c.Q + c.R * _pathMaskW] = true;
            }
        }

        void DrawGraph(
            Rect mapRect,
            HexMapViewportProjection projection,
            XianXia.Core.Simulation.SimulationWorld world,
            WorldGraphBoard graph)
        {
            if (world?.HexWorld != null && world.HexWorld.HasGrid)
            {
                _nodeRects.Clear();
                FillPathMask(world);
                HostHexGridDrawing.Draw(
                    projection,
                    world,
                    _px,
                    _selectedHex,
                    _hoverHex,
                    _hexPathPreview,
                    _pathMask,
                    _pathMaskW,
                    _pathMaskH);
                return;
            }

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
                if (!string.IsNullOrEmpty(n.OwnerId))
                {
                    StrategicFactionCatalog.MapTint(n.OwnerId, out var tr, out var tg, out var tb);
                    var blend = isFocus ? 0.55f : 0.42f;
                    boxC = new Color(
                        tr * blend + 0.12f,
                        tg * blend + 0.12f,
                        tb * blend + 0.12f,
                        0.92f);
                }
                var old = GUI.color;
                GUI.color = boxC;
                GUI.DrawTexture(rect, _px);
                GUI.color = old;
                GUI.Label(rect, label, _nodeLabel);
                _nodeRects.Add((n.Id, rect));
            }

            DrawSelectedArmyRoutePreview(mapRect, world, graph);
        }

        void DrawMapUnitOverlays(
            Rect mapRect,
            HexMapViewportProjection projection,
            XianXia.Core.Simulation.SimulationWorld world,
            WorldGraphBoard graph)
        {
            var prevDepth = GUI.depth;
            GUI.depth = -100;
            var hexMode = ArmyHexCommandService.IsHexStrategicActive(world) &&
                          world?.HexWorld != null &&
                          world.HexWorld.HasGrid;
            if (hexMode)
            {
                DrawResidualMarkers(mapRect, world, hexMode: true, hexProjection: projection);
                DrawFormalArmyAvatars(mapRect, world);
                DrawArmyStacks(mapRect, world, graph);
                DrawAvatars(mapRect, world, hexMode: true, hexProjection: projection);
            }
            else
            {
                DrawAvatars(mapRect, world);
                DrawFormalArmyAvatars(mapRect, world);
                DrawArmyStacks(mapRect, world, graph);
            }

            GUI.depth = prevDepth;
        }

        void DrawSelectedArmyRoutePreview(
            Rect mapRect,
            XianXia.Core.Simulation.SimulationWorld world,
            WorldGraphBoard graph)
        {
            if (!TryGetSelectedPlayerArmy(world, out var army))
                return;

            if (army.IsTraveling &&
                !string.IsNullOrEmpty(army.RouteId) &&
                graph.TryGetRoute(army.RouteId, out var activeRoute))
            {
                if (graph.TryGetNode(activeRoute.FromNodeId, out var routeFrom) &&
                    routeFrom != null &&
                    graph.TryGetNode(activeRoute.ToNodeId, out var routeTo) &&
                    routeTo != null)
                {
                    var pa = Project(mapRect, routeFrom.WorldX, routeFrom.WorldY);
                    var pb = Project(mapRect, routeTo.WorldX, routeTo.WorldY);
                    var startT = army.RouteSegmentOriginProgress >= 0f
                        ? army.RouteSegmentOriginProgress
                        : 0f;
                    var endT = army.RouteSegmentEndProgress >= 0f
                        ? army.RouteSegmentEndProgress
                        : army.GetRouteDisplayProgress();
                    DrawLine(Vector2.Lerp(pa, pb, startT), Vector2.Lerp(pa, pb, endT),
                        new Color(0.95f, 0.82f, 0.18f, 0.9f));
                }
            }

            if (!_orderPreviewActive ||
                !string.Equals(_orderPreviewArmyId, _selectedFormalArmyId, StringComparison.Ordinal))
                return;

            DrawCommittedArmyOrderPreview(mapRect, world, graph, army, _orderPreviewTarget);
        }

        void DrawCommittedArmyOrderPreview(
            Rect mapRect,
            XianXia.Core.Simulation.SimulationWorld world,
            WorldGraphBoard graph,
            FormalArmy army,
            WorldTravelTarget target)
        {
            _previewPathScratch.Clear();
            var targetRouteId = string.Empty;
            var targetRouteProgress = -1f;
            var hasPreview = false;

            if (target.IsRouteProgress)
            {
                targetRouteId = target.RouteId;
                targetRouteProgress = target.RouteProgress;
                var entryNode = target.RouteFromNodeId;
                if (string.IsNullOrEmpty(entryNode))
                    entryNode = target.RouteToNodeId;
                if (!string.IsNullOrEmpty(entryNode))
                {
                    hasPreview = ArmyTravelCommandService.TryBuildPathPreviewToNode(
                        world,
                        army,
                        entryNode,
                        _previewPathScratch);
                }
            }
            else
            {
                hasPreview = ArmyTravelCommandService.TryBuildPathPreviewToNode(
                    world,
                    army,
                    target.NodeId,
                    _previewPathScratch);
            }

            if (!hasPreview || _previewPathScratch.Count < 2)
                return;

            var previewColor = new Color(0.22f, 0.94f, 1f, 0.78f);
            for (var i = 0; i < _previewPathScratch.Count - 1; i++)
            {
                if (!graph.TryGetNode(_previewPathScratch[i], out var aNode) ||
                    !graph.TryGetNode(_previewPathScratch[i + 1], out var bNode))
                    continue;
                var pa = Project(mapRect, aNode.WorldX, aNode.WorldY);
                var pb = Project(mapRect, bNode.WorldX, bNode.WorldY);
                DrawLine(pa, pb, previewColor);
            }

            if (string.IsNullOrEmpty(targetRouteId) ||
                targetRouteProgress < 0f ||
                !graph.TryGetRoute(targetRouteId, out var previewRoute) ||
                !graph.TryGetNode(previewRoute.FromNodeId, out var previewFrom) ||
                !graph.TryGetNode(previewRoute.ToNodeId, out var previewTo))
                return;

            if (!graph.TryGetNode(_previewPathScratch[_previewPathScratch.Count - 1], out var lastNode))
                return;

            var routeStart = Project(mapRect, previewFrom.WorldX, previewFrom.WorldY);
            var routeEnd = Project(mapRect, previewTo.WorldX, previewTo.WorldY);
            var targetPoint = Vector2.Lerp(routeStart, routeEnd, targetRouteProgress);
            var lastPoint = Project(mapRect, lastNode.WorldX, lastNode.WorldY);
            DrawLine(lastPoint, targetPoint, previewColor);
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
                    DrawMapSelectionRing(rect);

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

            RegisterRemnantStackHitRects(mapRect, world, graph);
        }

        /// <summary>弥留／尸体栈不画聚合标记，但仍需可右键攻击（个体头像会挡住栈心）。</summary>
        void RegisterRemnantStackHitRects(
            Rect mapRect,
            XianXia.Core.Simulation.SimulationWorld world,
            WorldGraphBoard graph)
        {
            foreach (var kv in world.Strategic.Armies.Stacks)
            {
                var stack = kv.Value;
                if (stack == null || !stack.HasDownedRemnant)
                    continue;
                if (!TryResolveArmyStackWorldPoint(world, graph, stack, out var wx, out var wy))
                    continue;

                var p = Project(mapRect, wx, wy);
                p = NudgeArmyMarkerAwayFromNodes(p);
                if (!mapRect.Contains(p))
                    continue;

                var size = 30f;
                var rect = new Rect(p.x - size * 0.5f, p.y - size * 0.5f, size, size);
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


        void DrawResidualMarkers(
            Rect mapRect,
            XianXia.Core.Simulation.SimulationWorld world,
            bool hexMode = false,
            HexMapViewportProjection hexProjection = default)
        {
            _residualMarkerRects.Clear();
            if (world == null || !hexMode || world.HexWorld == null || !world.HexWorld.HasGrid)
                return;

            var groups = StrategicResidualPresentationQuery.Query(world);
            if (groups.Count == 0)
                return;

            // Low priority first; high priority draws last (IMGUI later = on top).
            groups.Sort((a, b) => a.VisualPriority.CompareTo(b.VisualPriority));

            var markerSize = 20f;
            var hexSize = world.HexWorld.HexSize;
            var edgeAnchorX = hexSize * 0.42f;
            var edgeAnchorY = hexSize * 0.48f;
            const float stackStep = 7f;

            var slotByHex = new Dictionary<string, int>(8);
            for (var i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                if (group == null || group.Count <= 0)
                    continue;
                if (!world.HexWorld.Contains(group.Hex))
                    continue;

                HexMath.ToWorldPosition(group.Hex, hexSize, out var wx, out var wy);
                wx += edgeAnchorX;
                wy += edgeAnchorY;

                var hexKey = group.Hex.Q + ":" + group.Hex.R;
                slotByHex.TryGetValue(hexKey, out var slot);
                slotByHex[hexKey] = slot + 1;
                wx += slot * stackStep * 0.55f;
                wy += slot * stackStep;

                var p = hexProjection.ProjectWorld(wx, wy);
                var rect = new Rect(
                    p.x - markerSize * 0.5f,
                    p.y - markerSize * 0.5f,
                    markerSize,
                    markerSize);
                if (!rect.Overlaps(mapRect))
                    continue;

                _residualMarkerRects.Add((group, rect));

                var fill = ResidualStateFill(group.State);
                var border = ResidualRelationBorder(group.Relation);
                var selected = IsSelectedResidualGroup(group);
                var old = GUI.color;
                GUI.color = fill;
                GUI.DrawTexture(rect, _px);
                GUI.color = border;
                GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 2f), _px);
                GUI.DrawTexture(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), _px);
                GUI.DrawTexture(new Rect(rect.x, rect.y, 2f, rect.height), _px);
                GUI.DrawTexture(new Rect(rect.xMax - 2f, rect.y, 2f, rect.height), _px);
                if (selected)
                    DrawMapSelectionRing(rect);

                GUI.color = Color.white;
                var glyph = group.State == ResidualStateBucket.Dead ? "X" : "D";
                GUI.Label(rect, glyph, _avatarLabel);
                var badge = new Rect(rect.xMax - 12f, rect.yMax - 12f, 14f, 12f);
                GUI.color = new Color(0.08f, 0.08f, 0.08f, 0.85f);
                GUI.DrawTexture(badge, _px);
                GUI.color = Color.white;
                GUI.Label(badge, group.Count.ToString(), _avatarLabel);
                GUI.color = old;
            }
        }

        static Color ResidualStateFill(ResidualStateBucket state) =>
            state == ResidualStateBucket.Dead
                ? new Color(0.42f, 0.36f, 0.30f, 0.92f)
                : new Color(0.55f, 0.28f, 0.28f, 0.90f);

        static Color ResidualRelationBorder(StrategicRelationBucket relation)
        {
            switch (relation)
            {
                case StrategicRelationBucket.Self:
                    return new Color(0.35f, 0.85f, 0.45f, 1f);
                case StrategicRelationBucket.Ally:
                    return new Color(0.35f, 0.65f, 0.95f, 1f);
                case StrategicRelationBucket.Enemy:
                    return new Color(0.92f, 0.28f, 0.28f, 1f);
                default:
                    return new Color(0.75f, 0.72f, 0.55f, 1f);
            }
        }

        bool IsSelectedResidualGroup(ResidualMarkerGroupView group)
        {
            if (_selectedResidualGroup == null || group == null)
                return false;
            return _selectedResidualGroup.Hex == group.Hex &&
                   _selectedResidualGroup.Relation == group.Relation &&
                   _selectedResidualGroup.State == group.State;
        }

        bool TryHitResidualMarker(Vector2 mouse, out ResidualMarkerGroupView group)
        {
            group = null;
            var bestPri = int.MinValue;
            for (var i = 0; i < _residualMarkerRects.Count; i++)
            {
                var (g, rect) = _residualMarkerRects[i];
                if (g == null || !rect.Contains(mouse))
                    continue;
                if (g.VisualPriority < bestPri)
                    continue;
                bestPri = g.VisualPriority;
                group = g;
            }

            return group != null;
        }

        void DrawLingeringIncapAvatars(
            Rect mapRect,
            XianXia.Core.Simulation.SimulationWorld world,
            bool hexMode = false,
            HexMapViewportProjection hexProjection = default)
        {
            // Retired: aggregated Residual markers replaced per-character / abstract remnant drawing.
        }

        void DrawAbstractRemnantEnemyMarkers(
            Rect mapRect,
            float wx,
            float wy,
            int memberCount,
            bool asCorpse,
            Color fill)
        {
            // Retired from production WorldMap path.
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
            if (stack == null)
                return false;

            if (world != null && ArmyHexCommandService.IsHexStrategicActive(world) && world.HexWorld.HasGrid)
            {
                if (!string.IsNullOrEmpty(stack.FormalArmyId) &&
                    world.Strategic.FormalArmies.TryGet(stack.FormalArmyId, out var formal) &&
                    formal != null &&
                    formal.UsesHexStrategicPosition &&
                    FormalArmyHexWorldPositionResolver.TryResolve(world, formal, out wx, out wy))
                    return true;

                if (ArmyHexBattleAnchorService.TryResolveHexForNode(world, stack.NodeId, out var hex))
                {
                    HexMath.ToWorldPosition(hex, world.HexWorld.HexSize, out wx, out wy);
                    return true;
                }
            }

            if (graph == null)
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

        void DrawFormalArmyAvatars(Rect mapRect, XianXia.Core.Simulation.SimulationWorld world)
        {
            _formalArmyRects.Clear();
            if (world?.Strategic?.FormalArmies == null)
                return;

            var playerFaction = ResolvePlayerFactionId(world);
            if (string.IsNullOrEmpty(playerFaction))
                return;

            var hexMode = ArmyHexCommandService.IsHexStrategicActive(world);
            var avatarSize = hexMode ? 22f : AvatarSize;

            _countAtNode.Clear();
            _slotAtNode.Clear();
            foreach (var kv in world.Strategic.FormalArmies.Armies)
            {
                var army = kv.Value;
                if (army == null || !string.Equals(army.FactionId, playerFaction, StringComparison.Ordinal))
                    continue;
                if (!ArmyWorldMapPresentation.ShouldDrawFormalArmyPortrait(world, army))
                    continue;
                var key = hexMode && army.UsesHexStrategicPosition
                    ? army.CurrentHex.ToString()
                    : army.NodeId ?? string.Empty;
                _countAtNode.TryGetValue(key, out var c);
                _countAtNode[key] = c + 1;
            }

            foreach (var kv in world.Strategic.FormalArmies.Armies)
            {
                var army = kv.Value;
                if (army == null || !string.Equals(army.FactionId, playerFaction, StringComparison.Ordinal))
                    continue;
                if (!ArmyWorldMapPresentation.ShouldDrawFormalArmyPortrait(world, army))
                    continue;
                if (!ArmyWorldMapPresentation.TryResolveArmyWorldPoint(world, army, out var wx, out var wy))
                    continue;

                if (string.Equals(army.ArmyId, _selectedFormalArmyId, StringComparison.Ordinal))
                {
                    FormalArmyStrategicMutationDiagnostics.RecordPresentation(army, wx, wy, true, true);
                }

                var leaderId = ArmyWorldMapPresentation.ResolvePortraitLeader(army);
                var basePos = hexMode && army.UsesHexStrategicPosition
                    ? ProjectHex(mapRect, world, wx, wy)
                    : Project(mapRect, wx, wy);
                var key = hexMode && army.UsesHexStrategicPosition
                    ? army.CurrentHex.ToString()
                    : army.NodeId ?? string.Empty;
                _slotAtNode.TryGetValue("fa:" + key, out var slot);
                _slotAtNode["fa:" + key] = slot + 1;
                _countAtNode.TryGetValue(key, out var total);
                if (total < 1)
                    total = 1;
                const float gap = 2f;
                var spacing = avatarSize + 3f;
                Vector2 center;
                if (hexMode && army.UsesHexStrategicPosition)
                {
                    if (total <= 1)
                        center = basePos;
                    else
                    {
                        var rowY = -(avatarSize + 4f);
                        var x0 = -(total - 1) * 0.5f * spacing;
                        center = basePos + new Vector2(x0 + slot * spacing, rowY);
                    }
                }
                else if (total <= 1)
                {
                    center = basePos + new Vector2(0f, -(NodeHitH * 0.5f + gap + avatarSize * 0.25f));
                }
                else
                {
                    var rowY = -(NodeHitH * 0.5f + gap + avatarSize * 0.5f) - (avatarSize + 6f);
                    var x0 = -(total - 1) * 0.5f * spacing;
                    center = basePos + new Vector2(x0 + slot * spacing, rowY);
                }

                var rect = new Rect(
                    center.x - avatarSize * 0.5f,
                    center.y - avatarSize * 0.5f,
                    avatarSize,
                    avatarSize);
                if (!rect.Overlaps(mapRect))
                    continue;

                _formalArmyRects[army.ArmyId] = rect;
                var selected = string.Equals(army.ArmyId, _selectedFormalArmyId, StringComparison.Ordinal);
                var garrisoned = army.State == FormalArmyState.Garrisoned;
                var fill = selected
                    ? new Color(0.35f, 0.68f, 0.98f, 0.88f)
                    : garrisoned
                        ? new Color(0.22f, 0.45f, 0.78f, 0.78f)
                        : new Color(0.28f, 0.52f, 0.90f, 0.82f);
                var old = GUI.color;
                GUI.color = fill;
                GUI.DrawTexture(rect, _px);
                if (selected)
                    DrawMapSelectionRing(rect);

                GUI.color = Color.white;
                var shortName = EntityLabel(world, leaderId);
                if (shortName.Length > 2)
                    shortName = shortName.Substring(0, 2);
                GUI.Label(rect, shortName, _avatarLabel);
                GUI.color = old;
            }
        }

        bool TryHitFormalArmy(Vector2 mouse, out string armyId)
        {
            armyId = string.Empty;
            foreach (var kv in _formalArmyRects)
            {
                if (kv.Value.Contains(mouse))
                {
                    armyId = kv.Key;
                    return true;
                }
            }

            return false;
        }

        void HandleGlobalStrategicToolbarClick(HostGlobalStrategicToolbar.ModuleId moduleId)
        {
            switch (moduleId)
            {
                case HostGlobalStrategicToolbar.ModuleId.Character:
                    if (_characterListPanel.IsOpen)
                    {
                        _characterListPanel.Close();
                        _globalStrategicToolbar.CloseAll();
                    }
                    else
                    {
                        _armyListPanel.Close();
                        _characterListPanel.Open();
                        _globalStrategicToolbar.SetActive(moduleId);
                    }

                    break;
                case HostGlobalStrategicToolbar.ModuleId.Army:
                    if (_armyListPanel.IsOpen)
                    {
                        _armyListPanel.Close();
                        _armyFormPanel?.Close();
                        _globalStrategicToolbar.CloseAll();
                    }
                    else
                    {
                        _characterListPanel.Close();
                        _armyListPanel.Open();
                        _globalStrategicToolbar.SetActive(moduleId);
                    }

                    break;
            }
        }

        void SyncFormalArmySelection(string armyId)
        {
            if (string.IsNullOrEmpty(armyId))
                return;
            _selectedFormalArmyId = armyId;
            _selected.Clear();
            _selectedStackId = string.Empty;
            ClearResidualSelection();
            EnsureStrategicRosterPanels();
            _characterListPanel?.Close();
            _armyListPanel?.Open();
            _armyListPanel?.SelectArmy(armyId);
            _globalStrategicToolbar.SetActive(HostGlobalStrategicToolbar.ModuleId.Army);
            if (bootstrap?.Session?.World != null &&
                bootstrap.Session.World.Strategic.FormalArmies.TryGet(armyId, out var army) &&
                army != null)
            {
                ArmyTravelCommandService.ReconcileArmyWithLivingMembers(bootstrap.Session.World, army);
            }
        }

        void ClearFormalArmySelection()
        {
            if (string.IsNullOrEmpty(_selectedFormalArmyId))
                return;
            _selectedFormalArmyId = string.Empty;
            ClearArmyOrderPreview();
            _armyListPanel?.SelectArmy(string.Empty);
        }

        void ClearResidualSelection() => _selectedResidualGroup = null;

        string ResolvePlayerFactionId(XianXia.Core.Simulation.SimulationWorld world)
        {
            if (!string.IsNullOrEmpty(world?.Strategic?.PlayerFactionId))
                return world.Strategic.PlayerFactionId;
            var party = bootstrap?.Session?.CharacterIds;
            var fromParty = XianXia.Core.Npc.HousingAssignmentService.ResolvePlayerFactionId(world, party);
            if (!string.IsNullOrEmpty(fromParty))
                return fromParty;
            return world?.Strategic?.PlayerFactionId ?? string.Empty;
        }

        void EnsureStrategicRosterPanels()
        {
            EnsureArmyFormPanel();
            if (_armyListPanel == null)
                _armyListPanel = new HostStrategicArmyListPanel(_body, _title, _armyFormPanel);
            if (_characterListPanel == null)
                _characterListPanel = new HostStrategicCharacterListPanel(_body, _title);
        }

        void DrawStrategicRosterPanels(XianXia.Core.Simulation.SimulationWorld world)
        {
            EnsureStrategicRosterPanels();
            var party = bootstrap.Session.CharacterIds;
            if (_armyListPanel.IsOpen)
            {
                var rect = new Rect(12f, 104f, Mathf.Min(720f, Screen.width - 24f), Screen.height - 140f);
                if (_armyListPanel.Draw(
                        rect,
                        world,
                        party,
                        EntityLabel,
                        FocusCameraOnArmy,
                        () => RefreshStrategicPresentation(world)))
                {
                    _selectedFormalArmyId = _armyListPanel.SelectedArmyId;
                    RefreshStrategicPresentation(world);
                }
            }

            if (_characterListPanel.IsOpen)
            {
                if (_characterListPanel.Draw(
                        world,
                        party,
                        EntityLabel,
                        FocusCameraOnArmy,
                        FocusCameraOnNode,
                        armyId =>
                        {
                            _characterListPanel.Close();
                            _armyListPanel.Open();
                            _armyListPanel.SelectArmy(armyId);
                            _selectedFormalArmyId = armyId;
                            _globalStrategicToolbar.SetActive(HostGlobalStrategicToolbar.ModuleId.Army);
                        },
                        () => RefreshStrategicPresentation(world)))
                {
                    RefreshStrategicPresentation(world);
                }
            }

            _globalStrategicToolbar.SyncFromPanels(
                _characterListPanel != null && _characterListPanel.IsOpen,
                _armyListPanel != null && _armyListPanel.IsOpen);
        }

        public void FocusCameraOnArmy(string armyId)
        {
            if (bootstrap?.Session?.World == null || string.IsNullOrEmpty(armyId))
                return;
            var world = bootstrap.Session.World;
            if (!world.Strategic.FormalArmies.TryGet(armyId, out var army) || army == null)
                return;
            if (ArmyWorldMapPresentation.TryResolveArmyWorldPoint(world, army, out var wx, out var wy))
                FocusCameraOnWorldPoint(wx, wy);
            _selectedFormalArmyId = armyId;
            _inspectNodeId = army.NodeId ?? string.Empty;
            _status = "已定位军队 " + armyId;
        }

        public void FocusCameraOnNode(string nodeId)
        {
            if (bootstrap?.Session?.World == null || string.IsNullOrEmpty(nodeId))
                return;
            var world = bootstrap.Session.World;
            if (!world.WorldGraph.TryGetNode(nodeId, out var node) || node == null)
                return;
            FocusCameraOnWorldPoint(node.WorldX, node.WorldY);
            _inspectNodeId = nodeId;
            _status = "已定位节点 " + HostStrategicRosterQueries.ResolveNodeLabel(world, nodeId);
        }

        void FocusCameraOnWorldPoint(float wx, float wy)
        {
            _viewCx = wx;
            _viewCy = wy;
            var minHalf = ResolveMinViewHalf(bootstrap?.Session?.World);
            _viewHalf = Mathf.Min(_viewHalf, minHalf * 1.25f);
            var world = bootstrap?.Session?.World;
            if (world != null)
                ClampHexCamera(_lastMapViewportWidth, _lastMapViewportHeight, world);
            _viewReady = true;
        }

        void EnsureArmyFormPanel()
        {
            if (_armyFormPanel == null)
                _armyFormPanel = new HostArmyFormPanel(_body, _title);
        }

        string BuildFormalArmyInspect(
            XianXia.Core.Simulation.SimulationWorld world,
            WorldGraphBoard graph,
            FormalArmy army)
        {
            var sb = new StringBuilder(256);
            sb.Append("我方军团\n\n");
            sb.Append("Id：").Append(army.ArmyId).Append('\n');
            sb.Append("Leader：").Append(EntityLabel(world, army.LeaderCharacterId)).Append('\n');
            sb.Append("State：").Append(army.State).Append('\n');
            sb.Append("Node：").Append(ResolveNodeName(graph, army.NodeId)).Append('\n');
            sb.Append("Members：").Append(army.MemberCharacterIds.Count).Append('\n');
            if (army.State == FormalArmyState.Garrisoned)
                sb.Append("\n驻扎中：无法移动／追击，请先在军队详情「解除驻扎 Mobilize」");
            sb.Append("\n操作：Global Strategic Toolbar「军队」；右键节点移动（需 AtNode）");
            return sb.ToString();
        }

        void DrawAvatars(
            Rect mapRect,
            XianXia.Core.Simulation.SimulationWorld world,
            bool hexMode = false,
            HexMapViewportProjection hexProjection = default)
        {
            _avatarRects.Clear();
            var ids = bootstrap.Session.CharacterIds;
            if (ids == null)
                return;

            var avatarSize = hexMode ? 22f : AvatarSize;
            Vector2 ProjectAvatar(float wx, float wy) =>
                hexMode ? hexProjection.ProjectWorld(wx, wy) : Project(mapRect, wx, wy);

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
                var key = hexMode ? ids[i].Value.ToString() : p.NodeId ?? "";
                _countAtNode.TryGetValue(key, out var c);
                _countAtNode[key] = c + 1;
            }

            _slotAtNode.Clear();
            for (var i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                if (!ArmyWorldMapPresentation.ShouldDrawIndependentCharacterPortrait(world, id))
                    continue;
                // 腐烂／Removed：大地图彻底不画、不占位
                if (world.Entities.TryGet(id, out var lifeEnt) &&
                    lifeEnt.TryGet<LifecycleComponent>(out var life) &&
                    life.IsRemoved)
                    continue;
                if (!world.WorldPresence.TryGet(id, out var presence) || presence == null)
                    continue;
                if (!WorldAgentMapPositionResolver.TryResolve(world, id, presence, out var wx, out var wy))
                    continue;

                var basePos = ProjectAvatar(wx, wy);
                Vector2 center;
                if (presence.HasRoutePresentation)
                {
                    _slotAtNode.TryGetValue("t:" + id.Value, out var slot);
                    _slotAtNode["t:" + id.Value] = slot + 1;
                    // 路锚叠位：拉开间距，弥留下移，避免点选总打到弥留
                    center = basePos + new Vector2((slot % 3) * (avatarSize * 0.55f) - avatarSize * 0.55f,
                        -avatarSize * 0.55f);
                    if (LingeringBattlefieldPartyService.IsLingeringDowned(world, id))
                        center += new Vector2(avatarSize * 0.35f, avatarSize * 0.7f);
                }
                else if (hexMode)
                {
                    _slotAtNode.TryGetValue("h:" + id.Value, out var slot);
                    _slotAtNode["h:" + id.Value] = slot + 1;
                    center = basePos + new Vector2((slot % 3) * (avatarSize * 0.45f), -avatarSize * 0.35f);
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
                    var spacing = avatarSize + 3f;
                    var rowY = -(NodeHitH * 0.5f + gap + avatarSize * 0.5f);
                    var x0 = -(total - 1) * 0.5f * spacing;
                    center = basePos + new Vector2(x0 + slot * spacing, rowY);
                    if (LingeringBattlefieldPartyService.IsLingeringDowned(world, id))
                        center += new Vector2(0f, avatarSize * 0.55f);
                }

                var rect = new Rect(
                    center.x - avatarSize * 0.5f,
                    center.y - avatarSize * 0.5f,
                    avatarSize,
                    avatarSize);
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
                    DrawMapSelectionRing(rect);

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

        void HandleMapInput(
            Rect mapRect,
            HexMapViewportProjection projection,
            XianXia.Core.Simulation.SimulationWorld world,
            WorldGraphBoard graph)
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
                if (TryHitFormalArmy(mouse, out var hitArmyId))
                {
                    var now = Time.realtimeSinceStartupAsDouble;
                    if (string.Equals(_lastMapFormalArmyClickId, hitArmyId, StringComparison.Ordinal) &&
                        now - _lastMapFormalArmyClickTime <= 0.35)
                    {
                        _lastMapFormalArmyClickId = string.Empty;
                        SyncFormalArmySelection(hitArmyId);
                        FocusCameraOnArmy(hitArmyId);
                    }
                    else
                    {
                        _lastMapFormalArmyClickId = hitArmyId;
                        _lastMapFormalArmyClickTime = now;
                        SyncFormalArmySelection(hitArmyId);
                        _status = "已选军团 " + hitArmyId;
                    }

                    _selectedResidualGroup = null;
                    e.Use();
                    return;
                }

                if (TryHitResidualMarker(mouse, out var residualGroup) && residualGroup != null)
                {
                    _selectedResidualGroup = residualGroup;
                    _selectedHex = residualGroup.Hex;
                    _selected.Clear();
                    _selectedStackId = string.Empty;
                    // 保留已选军团：便于立刻右键同格进入残留
                    _inspectNodeId = string.Empty;
                    var enterHint = BattleOfferService.HasLingeringBattlefield(world)
                        ? (string.IsNullOrEmpty(_selectedFormalArmyId)
                            ? "｜先选我方军团，再右键本格／标记进入残留"
                            : "｜右键本格或残留标记进入残留战场")
                        : string.Empty;
                    _status = FormatResidualGroupTitle(residualGroup) + " ×" + residualGroup.Count + enterHint;
                    e.Use();
                    return;
                }

                // 接战点活人／弥留叠在一起时，优先点中活人
                if (TryHitAvatar(mouse, world, out var hitAvatar, preferLiving: true))
                {
                    var id = new EntityId(hitAvatar);
                    var downed = LingeringBattlefieldPartyService.IsLingeringDowned(world, id);
                    if (downed)
                    {
                        if (!e.shift)
                        {
                            _selected.Clear();
                            _selectedStackId = string.Empty;
                        }

                        if (_selected.Contains(hitAvatar) && e.shift)
                            _selected.Remove(hitAvatar);
                        else
                            _selected.Add(hitAvatar);

                        var tag = LingeringBattlefieldPartyService.IsVisibleCorpse(world, id)
                            ? "尸体"
                            : "弥留";
                        var cd = world.Entities.TryGet(id, out var downEnt) && downEnt != null
                            ? CombatLifeStateService.FormatLifeStateWithCountdown(world, downEnt)
                            : string.Empty;
                        string armyHint;
                        if (LingeringBattlefieldPartyService.IsFriendlyLingeringDowned(world, id))
                        {
                            armyHint = string.IsNullOrEmpty(_selectedFormalArmyId)
                                ? "｜请先左键选军团，再右键该头像进入残留战场"
                                : "｜右键该头像进入残留战场";
                        }
                        else
                        {
                            armyHint = "｜敌方残留：请左键选我方军团，再右键进入残留战场";
                        }

                        _status = "已选" + tag + " " + EntityLabel(world, id) +
                                  (string.IsNullOrEmpty(cd) ? "" : "｜" + cd) + armyHint;
                        e.Use();
                        return;
                    }

                    _selected.Clear();
                    if (ArmyService.TryGetArmyForCharacter(world, id, out var memberArmy) &&
                        memberArmy != null)
                    {
                        SyncFormalArmySelection(memberArmy.ArmyId);
                        _status = "已选军团 " + memberArmy.ArmyId + "｜右键节点移动或右键敌军攻击/进入残留";
                    }
                    else
                    {
                        ClearFormalArmySelection();
                        _status = EntityLabel(world, id) + " 未编入军团｜请通过 Global Strategic Toolbar 组军";
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
                    ClearFormalArmySelection();
                    _selectedStackId = hitStackId;
                    _inspectNodeId = string.Empty;
                    ClearResidualSelection();
                    _stackMenuOpen = false;
                    _avatarMenuOpen = false;
                    if (world.Strategic.Armies.TryGet(hitStackId, out var stack) && stack != null)
                    {
                        var actionHint = "｜左键选我方军团，再右键攻击";
                        _status = "已选敌军｜" + DescribeStack(world, stack) + actionHint;
                    }
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
                ClearFormalArmySelection();
                ClearResidualSelection();
            }

                    if (graph.TryGetNode(nodeId, out var node))
                        _status = StrategicNodeAccessService.DescribeNode(world, node);
                    e.Use();
                    return;
                }

                if (ArmyHexCommandService.IsHexStrategicActive(world) &&
                    TryHandleHexLeftClick(projection, world, mouse, e))
                {
                    return;
                }

                if (!e.shift)
                {
                    _selected.Clear();
                    _selectedStackId = string.Empty;
                    ClearFormalArmySelection();
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

            if (ArmyHexCommandService.IsHexStrategicActive(world))
            {
                if (TryHandleHexMapCommand(projection, world, mouse, e))
                    return;
            }

            // —— 右键：只负责下令（永不改选中集合）——
            // 我方弥留／尸体：可右键弹「进入残留战场」（进场仍用已选军团）
            if (TryHitAvatar(mouse, world, out var menuAvatarId, preferLiving: false))
            {
                var hitId = new EntityId(menuAvatarId);
                if (LingeringBattlefieldPartyService.IsFriendlyLingeringDowned(world, hitId))
                {
                    OpenIncapAvatarMenu(world, hitId, mouse);
                    e.Use();
                    return;
                }
            }

            if (TryHitArmyStack(
                    world,
                    mouse,
                    out var menuStackId,
                    ArmyStackHitPad,
                    ArmyStackHitPadContested))
            {
                if (TryOpenStackAttackMenu(world, menuStackId, mouse))
                {
                    e.Use();
                    return;
                }
            }

            if (TryHitAvatar(mouse, world, out menuAvatarId, preferLiving: false))
            {
                var hitId = new EntityId(menuAvatarId);
                if (LingeringBattlefieldPartyService.IsLingeringDowned(world, hitId) &&
                    !LingeringBattlefieldPartyService.IsFriendlyLingeringDowned(world, hitId))
                {
                    // 敌方弥留／尸体：有选中军团时走残留进入／攻击菜单（与我方弥留对称）
                    if (TryResolveEnemyRemnantStackId(world, out var remnantStackId) &&
                        TryOpenStackAttackMenu(world, remnantStackId, mouse))
                    {
                        e.Use();
                        return;
                    }

                    var enemyTag = LingeringBattlefieldPartyService.IsVisibleCorpse(world, hitId)
                        ? "敌方尸体"
                        : "敌方弥留";
                    _status = enemyTag + " " + EntityLabel(world, hitId) +
                              "：请左键选我方军团，再右键进入残留战场";
                    e.Use();
                    return;
                }
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
                if (!string.IsNullOrEmpty(_selectedFormalArmyId) &&
                    world.Strategic.FormalArmies.TryGet(_selectedFormalArmyId, out var selectedArmy) &&
                    selectedArmy != null)
                {
                    if (selectedArmy.State == FormalArmyState.Garrisoned)
                    {
                        _status = "驻扎中的军团无法移动，请先在军队详情点击「解除驻扎 Mobilize」";
                        e.Use();
                        return;
                    }

                    if (bootstrap.WorldTravelConfirm == null)
                    {
                        _status = "出行确认组件缺失，无法下达军团移动";
                        e.Use();
                        return;
                    }

                    var armyDestLabel = target.Describe(graph);
                    bootstrap.WorldTravelConfirm.OpenArmyTarget(
                        _selectedFormalArmyId,
                        target,
                        armyDestLabel);
                    _status = "等待确认军团移动到「" + armyDestLabel + "」…";
                    e.Use();
                    return;
                }

                PruneRemovedFromSelection(world);
                CollectSelectedMacroParty(world, MacroPartyKind.MoveOrAttack, _scratchParty);
                for (var i = _scratchParty.Count - 1; i >= 0; i--)
                {
                    if (ArmyService.TryGetArmyForCharacter(world, _scratchParty[i], out var memberArmy) &&
                        memberArmy != null)
                    {
                        _scratchParty.RemoveAt(i);
                        continue;
                    }

                    if (!WorldTravelService.CanReceivePlayerMacroTravelOrder(world, _scratchParty[i]))
                        _scratchParty.RemoveAt(i);
                }

                if (_scratchParty.Count == 0)
                {
                    var freeze = StrategicClockFreezeService.IsModalEncounter(world)
                        ? "（遭遇 Modal 未解冻，禁止战略令）"
                        : string.Empty;
                    _status = "请左键选中军团再右键目标移动（角色须编入军团，不可散装跨节点）" + freeze;
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

        bool TryHandleHexLeftClick(
            HexMapViewportProjection projection,
            XianXia.Core.Simulation.SimulationWorld world,
            Vector2 mouse,
            Event e)
        {
            if (!HexMapMousePick.TryResolveMouseHex(projection, world.HexWorld, mouse, out var pickedHex))
                return false;

            _selectedHex = pickedHex;
            _hoverHex = pickedHex;
            ClearResidualSelection();

            if (e.control)
            {
                if (world.HexWorld.TryGetTile(pickedHex, out var tile) && tile != null)
                {
                    var willRoad = !tile.IsRoad;
                    HexMapEditorService.SetRoad(world, pickedHex, willRoad);
                    _status = "Hex " + pickedHex + (willRoad ? "：已设道路" : "：已取消道路");
                }

                e.Use();
                return true;
            }

            if (!e.shift)
            {
                _selected.Clear();
                _selectedStackId = string.Empty;
                // 残留接战格：保留已选军团，便于立刻右键进入
                if (!(BattleOfferService.HasLingeringBattlefield(world) &&
                      LingeringBattlefieldQueryService.TryGetLingeringBattlefieldAtHex(world, pickedHex, out _)))
                    ClearFormalArmySelection();
                _inspectNodeId = string.Empty;
            }

            if (!world.HexWorld.TryGetTile(pickedHex, out var inspectTile) || inspectTile == null)
            {
                _status = "Hex " + pickedHex + "（无地块数据）";
                e.Use();
                return true;
            }

            var label = pickedHex.ToString();
            if (world.Strategic.Sites.TryGetAtHex(pickedHex, out var site) && site != null)
                label = string.IsNullOrEmpty(site.DisplayName) ? site.SiteId : site.DisplayName;

            _status = "Hex " + pickedHex + "｜" + label + "｜" + HexTerrainPresentation.GetDisplayName(inspectTile) +
                      (inspectTile.IsRoad ? "｜道路" : string.Empty) +
                      (inspectTile.IsPassable ? "｜可通行" : "｜不可通行");
            if (BattleOfferService.HasLingeringBattlefield(world) &&
                LingeringBattlefieldQueryService.TryGetLingeringBattlefieldAtHex(world, pickedHex, out _))
            {
                _status += "｜有残留：右键本格可进入／攻击残留战场";
            }
            e.Use();
            return true;
        }

        bool TryHandleHexMapCommand(
            HexMapViewportProjection projection,
            XianXia.Core.Simulation.SimulationWorld world,
            Vector2 mouse,
            Event e)
        {
            if (!HexMapMousePick.TryResolveMouseHex(projection, world.HexWorld, mouse, out var pickedHex))
            {
                _status = "请右键可通行的 Hex";
                e.Use();
                return true;
            }

            _selectedHex = pickedHex;
            if (!world.HexWorld.TryGetTile(pickedHex, out var tile) || tile == null || !tile.IsPassable)
            {
                _status = "该 Hex 不可通行";
                e.Use();
                return true;
            }

            var hasSelectedArmy = TryGetSelectedLivingPlayerArmy(world, out var selectedArmy, out _);
            var hasMovableArmy = hasSelectedArmy &&
                                 selectedArmy != null &&
                                 selectedArmy.State != FormalArmyState.Garrisoned;
            var attackerFaction = ResolveAttackerFactionForHexContext(world);
            var resolution = HexRightClickResolver.Resolve(
                world,
                pickedHex,
                attackerFaction,
                hasSelectedArmy,
                hasMovableArmy,
                true);

            LogHexRightClickTrace(resolution, selectedArmy, pickedHex);

            _hexMenuOpen = false;
            _stackMenuOpen = false;
            _avatarMenuOpen = false;
            _nodeMenuOpen = false;

            switch (resolution.Action)
            {
                case HexRightClickResolvedAction.DirectMove:
                    ExecuteDirectMoveArmyToHex(world, pickedHex);
                    if (!string.IsNullOrEmpty(resolution.StatusHint))
                        _status = resolution.StatusHint + " " + _status;
                    break;
                case HexRightClickResolvedAction.ShowAttackTargetMenu:
                    OpenHexAttackTargetMenu(resolution, pickedHex, mouse);
                    break;
                case HexRightClickResolvedAction.DirectEnterFriendlyLingering:
                    ExecuteEnterFriendlyLingeringAtHex(world, pickedHex);
                    break;
                default:
                    _status = string.IsNullOrEmpty(resolution.StatusHint)
                        ? "请左键选中军团，再右键 Hex 移动"
                        : resolution.StatusHint;
                    break;
            }

            e.Use();
            return true;
        }

        void OpenHexAttackTargetMenu(HexRightClickResolution resolution, HexCoord hex, Vector2 mouse)
        {
            _hexMenuResolution = resolution;
            _hexMenuContext = resolution.Context;
            _hexMenuHex = hex;
            var rows = 1f + (resolution.MenuActions?.Count ?? 0);
            if (_hexMenuContext != null && _hexMenuContext.HasActiveLingering)
                rows += 1f;
            _hexMenuRect = AnchorContextMenu(new Rect(mouse.x, mouse.y, 1f, 1f), 220f, 28f + rows * 24f);
            _hexMenuOpen = true;
        }

        void LogHexRightClickTrace(
            HexRightClickResolution resolution,
            FormalArmy selectedArmy,
            HexCoord hex)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var ctx = resolution?.Context;
            var activeEnemyCount = ctx?.ActiveEnemyArmies?.Count ?? 0;
            var residualCounts = StrategicResidualPresentationQuery.CountAtHex(
                bootstrap?.Session?.World, hex);
            var enemyResidualCount = residualCounts.EnemyTotal;
            if (enemyResidualCount <= 0 && ctx?.Lingering != null)
                enemyResidualCount = ctx.Lingering.EnemyDownedCount + ctx.Lingering.EnemyDeadCount;

            var rt = bootstrap?.Session?.World?.Strategic?.Encounter;
            var hasLingerLookup = LingeringBattlefieldQueryService.TryGetLingeringBattlefieldAtHex(
                bootstrap?.Session?.World, hex, out _);
            var anchor = "?";
            if (StrategicEncounterResolveService.TryGetLingeringBattleAnchorHex(
                    bootstrap?.Session?.World, out var anchorHex))
                anchor = anchorHex.ToString();

            Debug.Log(
                "[HEX-RIGHTCLICK]\n" +
                "SelectedArmy: " + (selectedArmy?.ArmyId ?? string.Empty) + "\n" +
                "TargetHex: " + hex + "\n" +
                "ActiveEnemyArmiesAtHex: " + activeEnemyCount + "\n" +
                "EnemyLingeringBattlefieldsAtHex: " + (ctx != null && ctx.CanAttackEnemyLingering ? 1 : 0) + "\n" +
                "FriendlyLingeringBattlefieldsAtHex: " + (ctx != null && ctx.CanEnterFriendlyLingering ? 1 : 0) + "\n" +
                "ResolvedCommand: " + MapResolvedCommand(resolution));

            if (enemyResidualCount > 0 || activeEnemyCount == 0)
            {
                Debug.Log(
                    "[ENEMY-RESIDUAL-RIGHTCLICK]\n" +
                    "TargetHex = " + hex + "\n" +
                    "SelectedArmyId = " + (selectedArmy?.ArmyId ?? string.Empty) + "\n" +
                    "ActiveEnemyArmyCount = " + activeEnemyCount + "\n" +
                    "EnemyResidualCharacters = " + enemyResidualCount + "\n" +
                    "EnemyDownedCount = " + residualCounts.EnemyDowned + "\n" +
                    "EnemyDeadCount = " + residualCounts.EnemyDead + "\n" +
                    "LingeringBattlefieldAtHex = " + (hasLingerLookup ? 1 : 0) + "\n" +
                    "BattlefieldLingering = " + (rt != null && rt.BattlefieldLingering) + "\n" +
                    "BattleAnchorHex = " + anchor + "\n" +
                    "EnemyLingeringBattlefield = " + (ctx != null && ctx.CanAttackEnemyLingering) + "\n" +
                    "ResolvedRightClickAction = " + MapResolvedCommand(resolution));
            }
#endif
        }

        static string MapResolvedCommand(HexRightClickResolution resolution)
        {
            if (resolution == null)
                return "NONE";
            if (resolution.Action == HexRightClickResolvedAction.ShowAttackTargetMenu &&
                resolution.MenuActions != null)
            {
                var hasArmy = resolution.MenuActions.Contains(HexStrategicContextActionKind.AttackArmy);
                var hasLinger = resolution.MenuActions.Contains(
                    HexStrategicContextActionKind.AttackLingeringBattlefield);
                if (hasArmy && hasLinger)
                    return "ATTACK_MENU";
                if (hasArmy)
                    return "ATTACK_ARMY";
                if (hasLinger)
                    return "ATTACK_LINGERING";
                if (resolution.MenuActions.Contains(HexStrategicContextActionKind.EnterLingeringBattlefield))
                    return "ENTER_LINGERING";
                return "ATTACK_MENU";
            }

            switch (resolution.Action)
            {
                case HexRightClickResolvedAction.DirectMove:
                    return "MOVE";
                case HexRightClickResolvedAction.DirectEnterFriendlyLingering:
                    return "ENTER_LINGERING";
                case HexRightClickResolvedAction.DirectAttackLingeringBattlefield:
                    return "ATTACK_LINGERING";
                case HexRightClickResolvedAction.DirectAttackArmy:
                    return "ATTACK_ARMY";
                default:
                    return resolution.Action.ToString().ToUpperInvariant();
            }
        }

        string ResolveAttackerFactionForHexContext(XianXia.Core.Simulation.SimulationWorld world)
        {
            if (!string.IsNullOrEmpty(_selectedFormalArmyId) &&
                world?.Strategic?.FormalArmies != null &&
                world.Strategic.FormalArmies.TryGet(_selectedFormalArmyId, out var army) &&
                army != null &&
                !string.IsNullOrEmpty(army.FactionId))
            {
                return army.FactionId;
            }

            return ResolvePlayerFactionId(world);
        }

        void DrawHexContextMenu(XianXia.Core.Simulation.SimulationWorld world)
        {
            if (!_hexMenuOpen || _hexMenuResolution == null)
                return;

            var prevDepth = GUI.depth;
            GUI.depth = -85;
            HostUiHitTest.Block(_hexMenuRect);
            var prev = GUI.color;
            GUI.color = new Color(0.16f, 0.17f, 0.19f, 0.96f);
            GUI.DrawTexture(_hexMenuRect, _px);
            GUI.color = prev;

            var title = "选择攻击目标";
            if (world?.Strategic?.Sites != null &&
                world.Strategic.Sites.TryGetAtHex(_hexMenuHex, out var site) &&
                site != null &&
                !string.IsNullOrEmpty(site.DisplayName))
            {
                title = site.DisplayName + " · 选择目标";
            }

            GUI.Label(new Rect(_hexMenuRect.x + 8f, _hexMenuRect.y + 4f, _hexMenuRect.width - 16f, 18f), title, _body);
            var y = _hexMenuRect.y + 26f;
            var bw = _hexMenuRect.width - 16f;

            if (_hexMenuContext != null && _hexMenuContext.HasActiveLingering)
            {
                if (!string.IsNullOrEmpty(_hexMenuContext.FriendlyResidualSummary))
                {
                    GUI.Label(new Rect(_hexMenuRect.x + 8f, y, bw, 16f),
                        "我方：" + _hexMenuContext.FriendlyResidualSummary, _body);
                    y += 18f;
                }

                if (!string.IsNullOrEmpty(_hexMenuContext.EnemyResidualSummary))
                {
                    GUI.Label(new Rect(_hexMenuRect.x + 8f, y, bw, 16f),
                        "敌方：" + _hexMenuContext.EnemyResidualSummary, _body);
                    y += 18f;
                }
            }

            var menuActions = _hexMenuResolution.MenuActions;
            for (var i = 0; i < menuActions.Count; i++)
            {
                var action = menuActions[i];
                var label = DescribeHexAttackTargetMenuLabel(world, action, out var enabled);
                GUI.enabled = enabled;
                if (GUI.Button(new Rect(_hexMenuRect.x + 8f, y, bw, 22f), label))
                {
                    Event.current.Use();
                    ExecuteHexAttackTargetMenuAction(world, action);
                    _hexMenuOpen = false;
                }

                GUI.enabled = true;
                y += 26f;
            }

            GUI.depth = prevDepth;
        }

        string DescribeHexAttackTargetMenuLabel(
            XianXia.Core.Simulation.SimulationWorld world,
            HexStrategicContextActionKind action,
            out bool enabled)
        {
            enabled = true;
            switch (action)
            {
                case HexStrategicContextActionKind.AttackArmy:
                {
                    var target = _hexMenuContext?.PrimaryActiveEnemyArmy;
                    var canArmy = TryGetSelectedLivingPlayerArmy(world, out var attackerArmy, out _);
                    enabled = canArmy &&
                                attackerArmy != null &&
                                attackerArmy.State != FormalArmyState.Garrisoned &&
                                target != null &&
                                target.CanAttack;
                    if (target != null && !string.IsNullOrEmpty(target.DisplayName))
                        return "攻击军队·" + target.DisplayName;
                    if (target != null && !target.CanAttack && !string.IsNullOrEmpty(target.BlockReason))
                        return target.BlockReason;
                    return "攻击军队";
                }
                case HexStrategicContextActionKind.AttackLingeringBattlefield:
                    enabled = TryGetSelectedLivingPlayerArmy(world, out _, out _);
                    return enabled ? "攻击残留战场" : "攻击残留战场（请先选军团）";
                case HexStrategicContextActionKind.EnterLingeringBattlefield:
                    return "进入残留战场";
                default:
                    enabled = false;
                    return action.ToString();
            }
        }

        void ExecuteHexAttackTargetMenuAction(
            XianXia.Core.Simulation.SimulationWorld world,
            HexStrategicContextActionKind action)
        {
            switch (action)
            {
                case HexStrategicContextActionKind.AttackArmy:
                    ExecuteAttackEnemyArmyFromHex(world, _hexMenuContext?.PrimaryActiveEnemyArmy);
                    break;
                case HexStrategicContextActionKind.AttackLingeringBattlefield:
                    ExecuteAttackEnemyLingeringAtHex(world, _hexMenuHex);
                    break;
                case HexStrategicContextActionKind.EnterLingeringBattlefield:
                    ExecuteEnterFriendlyLingeringAtHex(world, _hexMenuHex);
                    break;
            }
        }

        void ExecuteDirectMoveArmyToHex(
            XianXia.Core.Simulation.SimulationWorld world,
            HexCoord hex)
        {
            if (string.IsNullOrEmpty(_selectedFormalArmyId) ||
                !world.Strategic.FormalArmies.TryGet(_selectedFormalArmyId, out var army) ||
                army == null)
            {
                _status = "请左键选中军团，再右键 Hex 移动";
                return;
            }

            if (army.State == FormalArmyState.Garrisoned)
            {
                _status = "驻扎中的军团无法移动，请先在军队详情点击「解除驻扎 Mobilize」";
                return;
            }

            var destLabel = hex.ToString();
            if (world.Strategic.Sites.TryGetAtHex(hex, out var site) && site != null)
                destLabel = string.IsNullOrEmpty(site.DisplayName) ? site.SiteId : site.DisplayName;

            SetArmyHexPathPreview(_selectedFormalArmyId, hex);
            if (bootstrap.WorldTravelDeparture == null)
            {
                _status = "出行组件缺失，无法下达军团移动";
                return;
            }

            bootstrap.WorldTravelDeparture.BeginArmyMacroOrder(
                _selectedFormalArmyId,
                WorldTravelTarget.AtHex(hex));
            if (!string.IsNullOrEmpty(bootstrap.WorldTravelDeparture.LastStatus))
                _status = bootstrap.WorldTravelDeparture.LastStatus;
            else
                _status = "军团已出发前往 " + destLabel;
        }

        void ExecuteEnterFriendlyLingeringAtHex(
            XianXia.Core.Simulation.SimulationWorld world,
            HexCoord hex)
        {
            if (BattleOfferService.TryEnterFriendlyLingeringAtHex(
                    world, hex, bootstrap.Session.CharacterIds))
            {
                _status = "接战弹窗已打开";
                return;
            }

            _status = "无法进入残留战场（接战点已失效或无可进入对象）";
        }

        void ExecuteAttackEnemyLingeringAtHex(
            XianXia.Core.Simulation.SimulationWorld world,
            HexCoord hex)
        {
            if (!TryGetSelectedLivingPlayerArmy(world, out var army, out var err))
            {
                _status = string.IsNullOrEmpty(err) ? "请先左键选中我方军团" : err;
                return;
            }

            if (BattleOfferService.TryAttackEnemyLingeringAtHex(world, army.ArmyId, hex, out var hint))
            {
                if (world.Strategic.HasBattleOffer)
                {
                    _status = "接战弹窗已打开";
                    return;
                }

                SetArmyHexPathPreview(army.ArmyId, hex);
                bootstrap.WorldTravelDeparture?.HidePartyFromLocalMapForArmy(army.ArmyId);
                _status = hint;
                return;
            }

            _status = string.IsNullOrEmpty(hint) ? "无法攻击残留战场" : hint;
        }

        void ExecuteAttackEnemyArmyFromHex(
            XianXia.Core.Simulation.SimulationWorld world,
            HexActiveEnemyArmyTarget target)
        {
            if (target == null || string.IsNullOrEmpty(target.StackId))
            {
                _status = "目标军团不存在";
                return;
            }

            if (!world.Strategic.Armies.TryGet(target.StackId, out var stack) || stack == null)
            {
                _status = "目标军团不存在";
                return;
            }

            if (!TryGetSelectedLivingPlayerArmy(world, out _, out var err))
            {
                _status = string.IsNullOrEmpty(err) ? "请先左键选中我方军团" : err;
                return;
            }

            ExecuteAttackStack(world, _attackPartyScratch, stack);
        }

        /// <summary>
        /// 右键「有残留的 Hex」：按原先逻辑进入残留战场（我方弥留头像菜单／敌方残留栈菜单）。
        /// 无残留或接战点未激活时返回 false，交还给普通 Hex 移动。
        /// </summary>
        [System.Obsolete("Hex Context Menu 已取代；保留供 legacy Node 路径参考。")]
        bool TryOpenResidualHexEnter(
            XianXia.Core.Simulation.SimulationWorld world,
            HexCoord hex,
            Vector2 mouse)
        {
            if (world?.Strategic == null || !BattleOfferService.HasLingeringBattlefield(world))
                return false;
            if (!TryResolveResidualEnterTargetsOnHex(
                    world, hex, out var friendlyFocus, out var enemyStackId))
                return false;

            if (!TryGetSelectedLivingPlayerArmy(world, out _, out var selectionError))
            {
                _status = string.IsNullOrEmpty(selectionError)
                    ? "请先左键选中我方军团，再右键该格进入残留战场"
                    : selectionError;
                return true;
            }

            // 我方弥留所在格 → 与右键弥留头像同一套菜单
            if (!friendlyFocus.IsNone)
            {
                OpenIncapAvatarMenu(world, friendlyFocus, mouse);
                return true;
            }

            // 敌方残留所在格 → 与右键残留栈同一套菜单（进入残留／追击再攻）
            if (!string.IsNullOrEmpty(enemyStackId) &&
                TryOpenStackAttackMenu(world, enemyStackId, mouse))
                return true;

            _status = "该格有残留，但无法打开进入菜单";
            return true;
        }

        /// <summary>该 Hex 上是否有与当前残留战场相关的我方弥留／敌方残留。</summary>
        static bool TryResolveResidualEnterTargetsOnHex(
            XianXia.Core.Simulation.SimulationWorld world,
            HexCoord hex,
            out EntityId friendlyFocus,
            out string enemyStackId)
        {
            friendlyFocus = EntityId.None;
            enemyStackId = string.Empty;
            if (world == null)
                return false;

            var onBattleAnchor = false;
            if (ArmyHexBattleAnchorService.TryGetBattleAnchorHex(
                    world.Strategic.Participants, out var anchorHex) &&
                anchorHex.Equals(hex))
                onBattleAnchor = true;

            var groups = StrategicResidualPresentationQuery.Query(world);
            ResidualMarkerGroupView selfGroup = null;
            ResidualMarkerGroupView enemyGroup = null;
            for (var i = 0; i < groups.Count; i++)
            {
                var g = groups[i];
                if (g == null || !g.Hex.Equals(hex) || g.Count <= 0)
                    continue;
                if (g.Relation == StrategicRelationBucket.Self ||
                    g.Relation == StrategicRelationBucket.Ally)
                {
                    if (selfGroup == null || g.VisualPriority >= selfGroup.VisualPriority)
                        selfGroup = g;
                }
                else if (g.Relation == StrategicRelationBucket.Enemy)
                {
                    if (enemyGroup == null || g.VisualPriority >= enemyGroup.VisualPriority)
                        enemyGroup = g;
                }
            }

            if (selfGroup == null && enemyGroup == null && !onBattleAnchor)
                return false;

            if (selfGroup != null)
            {
                for (var i = 0; i < selfGroup.Characters.Count; i++)
                {
                    var row = selfGroup.Characters[i];
                    if (row == null || row.CharacterId.IsNone)
                        continue;
                    if (LingeringBattlefieldPartyService.IsFriendlyLingeringDowned(world, row.CharacterId))
                    {
                        friendlyFocus = row.CharacterId;
                        break;
                    }
                }
            }

            if (TryGetEncounterRemnantStack(world, out var remnant) && remnant != null)
            {
                if (ArmyStackAdapter.TryGetFormalArmy(world, remnant, out var army) &&
                    army != null &&
                    army.UsesHexStrategicPosition &&
                    army.CurrentHex.Equals(hex))
                {
                    enemyStackId = remnant.Id;
                }
                else if (onBattleAnchor || enemyGroup != null)
                {
                    enemyStackId = remnant.Id;
                }
            }

            return !friendlyFocus.IsNone || !string.IsNullOrEmpty(enemyStackId);
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
            _nodeMenuRect = AnchorContextMenu(anchor, 196f, 146f);

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

        bool TryOpenStackAttackMenu(
            XianXia.Core.Simulation.SimulationWorld world,
            string stackId,
            Vector2 mouse)
        {
            if (string.IsNullOrEmpty(stackId))
                return false;
            if (!world.Strategic.Armies.TryGet(stackId, out var stack) || stack == null)
                return false;

            var playerFaction = ResolvePlayerFactionId(world);
            if (!string.IsNullOrEmpty(playerFaction) &&
                string.Equals(stack.FactionId, playerFaction, StringComparison.Ordinal))
                return false;

            if (string.IsNullOrEmpty(_selectedFormalArmyId))
                return false;

            if (!TryGetSelectedLivingPlayerArmy(world, out var attackerArmy, out var selectionError))
            {
                if (!string.IsNullOrEmpty(selectionError))
                {
                    _status = selectionError;
                    return true;
                }

                return false;
            }

            if (attackerArmy.State == FormalArmyState.Garrisoned)
            {
                _status = "驻扎中的军团无法追击，请先在军队详情点击「解除驻扎 Mobilize」";
                return true;
            }

            _stackMenuStackId = stackId;
            _stackMenuOpen = true;
            var isRemnant = stack.HasDownedRemnant || stack.IsBattlefieldRemnant;
            _stackMenuRect = new Rect(mouse.x + 4f, mouse.y + 4f, 196f, isRemnant ? 86f : 56f);
            _status = isRemnant
                ? "残留战场｜" + DescribeStack(world, stack)
                : "下令攻击｜" + DescribeStack(world, stack);
            return true;
        }

        /// <summary>须左键选中我方存活军团；不自动从散装队伍推断攻击方。</summary>
        bool TryGetSelectedLivingPlayerArmy(
            XianXia.Core.Simulation.SimulationWorld world,
            out FormalArmy army,
            out string error)
        {
            army = null;
            error = null;
            if (world?.Strategic?.FormalArmies == null)
                return false;

            if (string.IsNullOrEmpty(_selectedFormalArmyId))
                return false;

            if (!world.Strategic.FormalArmies.TryGet(_selectedFormalArmyId, out army) || army == null)
            {
                error = "所选军团无效。";
                return false;
            }

            var playerFaction = ResolvePlayerFactionId(world);
            if (!string.IsNullOrEmpty(playerFaction) &&
                !string.Equals(army.FactionId, playerFaction, StringComparison.Ordinal))
            {
                error = "只能命令我方军团发起攻击。";
                army = null;
                return false;
            }

            if (!ArmyPostBattleSyncService.HasMacroOrderLivingMember(world, army))
            {
                error = "该军团已无可用成员，无法发起攻击。";
                army = null;
                return false;
            }

            return true;
        }

        void DrawMapSelectionRing(Rect rect)
        {
            var old = GUI.color;
            GUI.color = MapSelectionRingColor;
            var w = MapSelectionRingWidth;
            var outer = new Rect(rect.x - w, rect.y - w, rect.width + w * 2f, rect.height + w * 2f);
            GUI.DrawTexture(new Rect(outer.x, outer.y, outer.width, w), _px);
            GUI.DrawTexture(new Rect(outer.x, outer.yMax - w, outer.width, w), _px);
            GUI.DrawTexture(new Rect(outer.x, outer.y, w, outer.height), _px);
            GUI.DrawTexture(new Rect(outer.xMax - w, outer.y, w, outer.height), _px);
            GUI.color = old;
        }

        static bool TryResolveEnemyRemnantStackId(
            XianXia.Core.Simulation.SimulationWorld world,
            out string stackId)
        {
            stackId = string.Empty;
            if (!TryGetEncounterRemnantStack(world, out var stack) || stack == null)
                return false;
            stackId = stack.Id;
            return !string.IsNullOrEmpty(stackId);
        }

        string FormatSelectionSummary()
        {
            if (!string.IsNullOrEmpty(_selectedFormalArmyId))
            {
                if (_selected.Count > 0)
                    return "军团 " + _selectedFormalArmyId + " +" + _selected.Count;
                return "军团 " + _selectedFormalArmyId;
            }

            return _selected.Count.ToString();
        }

        static string DescribeStack(XianXia.Core.Simulation.SimulationWorld world, ArmyStack stack)
        {
            if (stack == null)
                return string.Empty;
            var faction = StrategicFactionCatalog.DisplayName(stack.FactionId);
            var name = string.IsNullOrEmpty(stack.DisplayName) ? stack.Id : stack.DisplayName;
            var power = CombatPowerCalculator.ForArmyStack(world, stack);
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

            var hexMode = ArmyHexCommandService.IsHexStrategicActive(world);
            var isRemnant = stack.HasDownedRemnant || stack.IsBattlefieldRemnant;
            var hasLinger = BattleOfferService.HasLingeringBattlefield(world);
            var menuH = !hexMode && isRemnant && hasLinger ? 86f : 56f;

            var prevDepth = GUI.depth;
            GUI.depth = -85;
            _stackMenuRect = new Rect(_stackMenuRect.x, _stackMenuRect.y, 196f, menuH);
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
            var canAttack = TryGetSelectedLivingPlayerArmy(world, out var atkArmyRef, out _);
            if (canAttack && atkArmyRef.State == FormalArmyState.Garrisoned)
            {
                GUI.Label(new Rect(_stackMenuRect.x + 8f, y, bw, 18f),
                    "驻扎中不可追击，请先解除驻扎", _body);
                y += 20f;
                canAttack = false;
            }

            if (!hexMode && isRemnant && hasLinger)
            {
                CollectActingArmyLivingParty(world, _scratchParty);
                var needTravel = false;
                if (_scratchParty.Count > 0 &&
                    TryResolveRemnantBattleAnchor(world, stack, out var aNode, out var aRoute, out var aProg))
                {
                    needTravel = !IsAnyPartyMemberInReinforcementRange(
                        world, _scratchParty, aNode, aRoute, aProg);
                }

                GUI.enabled = canAttack && _scratchParty.Count > 0;
                var enterLabel = needTravel ? "前往并进入残留战场" : "进入残留战场";
                if (GUI.Button(new Rect(_stackMenuRect.x + 8f, y, bw, 22f), enterLabel))
                {
                    Event.current.Use();
                    if (canAttack && _scratchParty.Count > 0)
                    {
                        if (needTravel)
                            ExecuteAttackStack(world, _scratchParty, stack);
                        else if (!BattleOfferService.TryBuildOfferForEnemyRemnantReentry(
                                     world, _scratchParty, stack.Id, "残留战场"))
                            _status = "无法进入残留战场（接战点已失效或不在范围内）";
                    }
                    else
                        _status = "请先左键选中我方军团再进入残留";
                    _stackMenuOpen = false;
                }

                y += 26f;
                GUI.enabled = true;
            }

            GUI.enabled = canAttack;

            var attackLabel = isRemnant ? "追击／再攻" : "攻击";
            if (GUI.Button(new Rect(_stackMenuRect.x + 8f, y, bw, 22f), attackLabel))
            {
                Event.current.Use();
                if (canAttack)
                    ExecuteAttackStack(world, _attackPartyScratch, stack);
                else
                    _status = "请先左键选中我方军团再攻击";
                _stackMenuOpen = false;
            }

            GUI.enabled = true;
            GUI.depth = prevDepth;
        }

        static bool TryResolveRemnantBattleAnchor(
            XianXia.Core.Simulation.SimulationWorld world,
            ArmyStack stack,
            out string anchorNode,
            out string anchorRoute,
            out float anchorProgress)
        {
            anchorNode = string.Empty;
            anchorRoute = string.Empty;
            anchorProgress = -1f;
            if (world?.Strategic?.Participants != null)
            {
                var snap = world.Strategic.Participants;
                if (!string.IsNullOrEmpty(snap.BattleAnchorNodeId) ||
                    !string.IsNullOrEmpty(snap.BattleAnchorRouteId) ||
                    ArmyHexBattleAnchorService.HasBattleAnchorHex(snap))
                {
                    anchorNode = snap.BattleAnchorNodeId ?? string.Empty;
                    anchorRoute = snap.BattleAnchorRouteId ?? string.Empty;
                    anchorProgress = snap.BattleAnchorProgress;
                    return true;
                }
            }

            if (stack == null)
                return false;
            anchorNode = stack.NodeId ?? string.Empty;
            anchorRoute = stack.RouteId ?? string.Empty;
            anchorProgress = stack.IsRouteAnchored ? stack.RouteAnchorProgress : -1f;
            return !string.IsNullOrEmpty(anchorNode) || !string.IsNullOrEmpty(anchorRoute);
        }

        void ExecuteAttackStack(
            XianXia.Core.Simulation.SimulationWorld world,
            List<EntityId> party,
            ArmyStack stack)
        {
            var attackerArmyId = _selectedFormalArmyId;
            if (string.IsNullOrEmpty(attackerArmyId) ||
                !world.Strategic.FormalArmies.TryGet(attackerArmyId, out var attackerArmy) ||
                attackerArmy == null ||
                !ArmyPostBattleSyncService.HasMacroOrderLivingMember(world, attackerArmy))
            {
                _status = "请左键选中我方存活军团再攻击";
                return;
            }

            var attackerFaction = attackerArmy.FactionId;

            if (!string.IsNullOrEmpty(attackerFaction) &&
                !string.IsNullOrEmpty(stack.FactionId) &&
                !string.Equals(attackerFaction, stack.FactionId, System.StringComparison.Ordinal) &&
                !WarGateService.CanAttack(world, attackerFaction, stack.FactionId))
            {
                _status = "未宣战：无法军事攻击该势力军队";
                return;
            }

            bootstrap.WorldTravelDeparture?.BeginFormalArmyPursuit(attackerArmyId, stack);
            var departure = bootstrap.WorldTravelDeparture;
            if (departure != null && !string.IsNullOrEmpty(departure.LastStatus))
                _status = departure.LastStatus;
            else if (world.Strategic.HasBattleOffer)
                _status = "接战弹窗已打开";
            else
            {
                var name = string.IsNullOrEmpty(stack.DisplayName) ? stack.Id : stack.DisplayName;
                _status = "军团出发攻击「" + name + "」（抵达后弹接战）";
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
            if (_hexMenuOpen && _hexMenuRect.Contains(ev.mousePosition))
                return;
            if (_avatarMenuOpen && _avatarMenuRect.Contains(ev.mousePosition))
                return;

            if (!_stackMenuOpen && !_nodeMenuOpen && !_hexMenuOpen && !_avatarMenuOpen)
                return;

            _stackMenuOpen = false;
            _nodeMenuOpen = false;
            _hexMenuOpen = false;
            _avatarMenuOpen = false;
            _avatarMenuVisitMode = false;
            // 不 Use：同一帧右键仍可落到移动／攻击下令
        }

        void DrawAvatarContextMenu(XianXia.Core.Simulation.SimulationWorld world)
        {
            if (!_avatarMenuOpen)
                return;
            if (ArmyHexCommandService.IsHexStrategicActive(world))
            {
                _avatarMenuOpen = false;
                return;
            }
            var target = new EntityId(_avatarMenuEntityId);
            if (target.IsNone ||
                !LingeringBattlefieldPartyService.IsFriendlyLingeringDowned(world, target))
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

            CollectActingArmyLivingParty(world, _scratchParty);
            var hasArmy = _scratchParty.Count > 0;
            _avatarMenuVisitMode = false;
            if (hasArmy &&
                LingeringBattlefieldPartyService.TryResolveBattleAnchor(
                    world, target, out var anchorNode, out var anchorRoute, out var anchorProgress))
            {
                _avatarMenuVisitMode = !IsAnyPartyMemberInReinforcementRange(
                    world, _scratchParty, anchorNode, anchorRoute, anchorProgress);
            }

            LingeringBattlefieldPartyService.CollectViewParty(
                world, bootstrap.Session.CharacterIds, target, _attackPartyScratch, _scratchParty);
            var hasLinger = BattleOfferService.HasLingeringBattlefield(world);
            var canEnter = hasLinger && hasArmy && !_avatarMenuVisitMode && _attackPartyScratch.Count > 0;
            var hintY = _avatarMenuRect.y + 24f;
            if (!hasLinger)
            {
                GUI.Label(
                    new Rect(_avatarMenuRect.x + 8f, hintY, _avatarMenuRect.width - 16f, 16f),
                    "接战点已无残留战场",
                    _body);
                hintY += 18f;
            }
            else if (!hasArmy)
            {
                GUI.Label(
                    new Rect(_avatarMenuRect.x + 8f, hintY, _avatarMenuRect.width - 16f, 16f),
                    "请先左键选中我方军团",
                    _body);
                hintY += 18f;
            }
            else if (_avatarMenuVisitMode)
            {
                GUI.Label(
                    new Rect(_avatarMenuRect.x + 8f, hintY, _avatarMenuRect.width - 16f, 16f),
                    "军团将前往该处，抵达后弹接战窗",
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
                    "将弹出接战窗（当前军团强制参战），选手动战斗进入",
                    _body);
                hintY += 18f;
            }

            GUI.enabled = hasLinger && hasArmy && (_avatarMenuVisitMode || canEnter);
            var btnLabel = _avatarMenuVisitMode ? "前往并进入残留战场" : "进入残留战场";
            if (GUI.Button(
                    new Rect(_avatarMenuRect.x + 8f, _avatarMenuRect.y + 58f, _avatarMenuRect.width - 16f, 28f),
                    btnLabel) &&
                hasLinger &&
                hasArmy)
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
            if (!LingeringBattlefieldPartyService.IsFriendlyLingeringDowned(world, focusIncap))
            {
                _status = "敌方弥留／尸体不可直接进入，请派军团进攻";
                return false;
            }

            CollectActingArmyLivingParty(world, _scratchParty);
            if (_scratchParty.Count == 0 || string.IsNullOrEmpty(_selectedFormalArmyId))
            {
                _status = "请先左键选中军团";
                return false;
            }

            if (!BattleOfferService.HasLingeringBattlefield(world))
            {
                _status = "接战点已无残留战场";
                return false;
            }

            if (LingeringBattlefieldPartyService.TryResolveBattleAnchor(
                    world, focusIncap, out var anchorNode, out var anchorRoute, out var anchorProgress) &&
                IsAnyPartyMemberInReinforcementRange(
                    world, _scratchParty, anchorNode, anchorRoute, anchorProgress))
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
            var destLabel = "军团前往「" + EntityLabel(world, focusIncap) + "」残留点";
            bootstrap.WorldTravelConfirm.OpenArmyTarget(_selectedFormalArmyId, target, destLabel);
            _status = "等待确认军团前往残留点…";
            return true;
        }

        static bool IsAnyPartyMemberInReinforcementRange(
            XianXia.Core.Simulation.SimulationWorld world,
            IReadOnlyList<EntityId> party,
            string anchorNode,
            string anchorRoute,
            float anchorProgress)
        {
            if (world == null || party == null)
                return false;
            for (var i = 0; i < party.Count; i++)
            {
                if (!world.WorldPresence.TryGet(party[i], out var wp) || wp == null)
                    continue;
                if (ReinforcementRangeService.IsWithinReinforcementRange(
                        world, wp, anchorNode, anchorRoute, anchorProgress))
                    return true;
            }

            return false;
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
            if (!LingeringBattlefieldPartyService.IsFriendlyLingeringDowned(world, hitId))
                return;

            _avatarMenuEntityId = hitId.Value;
            _avatarMenuOpen = true;
            _avatarMenuRect = new Rect(mouse.x + 4f, mouse.y + 4f, 196f, 118f);

            CollectActingArmyLivingParty(world, _scratchParty);
            var hasArmy = _scratchParty.Count > 0;
            var tag = LingeringBattlefieldPartyService.IsVisibleCorpse(world, hitId)
                ? "尸体"
                : "弥留";
            var hasLinger = BattleOfferService.HasLingeringBattlefield(world);
            if (!hasLinger)
            {
                _status = EntityLabel(world, hitId) + "（" + tag + "）｜接战点已无残留战场";
                return;
            }

            if (!hasArmy)
            {
                _status = EntityLabel(world, hitId) + "（" + tag + "）｜请先左键选军团，再右键进入残留";
                return;
            }

            if (LingeringBattlefieldPartyService.TryResolveBattleAnchor(
                    world, hitId, out var anchorNode, out var anchorRoute, out var anchorProgress) &&
                !IsAnyPartyMemberInReinforcementRange(
                    world, _scratchParty, anchorNode, anchorRoute, anchorProgress))
            {
                _status = EntityLabel(world, hitId) + "（" + tag + "）｜军团可前往并进入残留战场";
                return;
            }

            _status = EntityLabel(world, hitId) + "（" + tag + "）｜可进入残留战场";
        }

        void CollectActingArmyLivingParty(
            XianXia.Core.Simulation.SimulationWorld world,
            List<EntityId> into)
        {
            if (into == null)
                return;
            into.Clear();
            CollectSelectedMacroParty(world, MacroPartyKind.MoveOrAttack, _orderFilterScratch);
            if (!ArmyMacroPartyQueries.TryResolvePlayerArmyId(
                    world,
                    _selectedFormalArmyId,
                    _orderFilterScratch,
                    out var armyId))
                return;
            ArmyMacroPartyQueries.CollectLivingMembers(world, armyId, into);
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
            GUI.color = new Color(0.96f, 0.93f, 0.86f, 0.98f);
            GUI.DrawTexture(panelRect, _px);
            GUI.color = new Color(0.62f, 0.54f, 0.42f, 1f);
            GUI.DrawTexture(new Rect(panelRect.x, panelRect.y, 2f, panelRect.height), _px);
            GUI.color = prev;

            var inspectTitle = HostImguiStyles.InkLabel(17, bold: true, ink: new Color(0.22f, 0.18f, 0.12f));
            var inspectBody = HostImguiStyles.InkLabel(13, wordWrap: true, ink: new Color(0.28f, 0.24f, 0.18f));

            GUI.Label(
                new Rect(panelRect.x + 12f, panelRect.y + 10f, panelRect.width - 24f, 22f),
                "情报",
                inspectTitle);

            var body = BuildInspectBody(world, graph);
            var legendReserve = ArmyHexCommandService.IsHexStrategicActive(world) && world?.HexWorld != null && world.HexWorld.HasGrid
                ? 92f
                : 0f;
            var textRect = new Rect(
                panelRect.x + 12f,
                panelRect.y + 38f,
                panelRect.width - 24f,
                panelRect.height - 50f - legendReserve);
            var contentH = Mathf.Max(
                textRect.height,
                inspectBody.CalcHeight(new GUIContent(body), textRect.width - 18f) + 8f);
            _inspectScroll = GUI.BeginScrollView(
                textRect,
                _inspectScroll,
                new Rect(0f, 0f, textRect.width - 16f, contentH));
            GUI.Label(new Rect(0f, 0f, textRect.width - 18f, contentH), body, inspectBody);
            GUI.EndScrollView();

            if (legendReserve > 0f)
                DrawTerrainLegend(new Rect(panelRect.x + 8f, panelRect.yMax - legendReserve + 4f, panelRect.width - 16f, legendReserve - 8f));
        }

        void DrawTerrainLegend(Rect rect)
        {
            var headerStyle = HostImguiStyles.InkLabel(12, bold: true, ink: new Color(0.24f, 0.20f, 0.14f));
            var entryStyle = HostImguiStyles.InkLabel(11, ink: new Color(0.30f, 0.26f, 0.18f));
            _terrainLegendExpanded = GUI.Toggle(
                new Rect(rect.x, rect.y, rect.width, 18f),
                _terrainLegendExpanded,
                "地形图例",
                headerStyle);
            if (!_terrainLegendExpanded)
                return;

            var y = rect.y + 22f;
            var swatch = 12f;
            var gap = 4f;
            foreach (var entry in HexTerrainPresentation.LegendEntries)
            {
                var color = new Color(entry.Color.R, entry.Color.G, entry.Color.B, 1f);
                var prev = GUI.color;
                GUI.color = color;
                GUI.DrawTexture(new Rect(rect.x + 4f, y + 2f, swatch, swatch), _px);
                GUI.color = prev;
                GUI.Label(new Rect(rect.x + swatch + gap + 6f, y, rect.width - swatch - 10f, 16f), entry.Label, entryStyle);
                y += 18f;
            }
        }

        string BuildInspectBody(
            XianXia.Core.Simulation.SimulationWorld world,
            WorldGraphBoard graph)
        {
            if (_selectedResidualGroup != null)
                return BuildResidualInspect(world, _selectedResidualGroup);
            if (!string.IsNullOrEmpty(_selectedFormalArmyId) &&
                world.Strategic?.FormalArmies != null &&
                world.Strategic.FormalArmies.TryGet(_selectedFormalArmyId, out var formalArmy) &&
                formalArmy != null)
                return BuildFormalArmyInspect(world, graph, formalArmy);
            if (_selectedHex.HasValue && ArmyHexCommandService.IsHexStrategicActive(world))
                return BuildHexInspect(world, _selectedHex.Value);
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

            return "左键点选 Hex、我方军团、残留标记或敌军，在此查看详情。\n\n" +
                   "· Hex：地形／道路／地点\n" +
                   "· 我方：境界／生命／弥留·尸体倒计时\n" +
                   "· 残留：弥留／阵亡聚合名单（含倒计时）\n" +
                   "· 敌军：势力／人数／战力／成员倒计时\n" +
                   "· Ctrl+左键：切换道路（编辑）";
        }

        static string FormatResidualGroupTitle(ResidualMarkerGroupView group)
        {
            if (group == null)
                return "残留";
            string rel;
            switch (group.Relation)
            {
                case StrategicRelationBucket.Self:
                    rel = "我方";
                    break;
                case StrategicRelationBucket.Ally:
                    rel = "盟友";
                    break;
                case StrategicRelationBucket.Enemy:
                    rel = "敌方";
                    break;
                default:
                    rel = "其他";
                    break;
            }

            var state = group.State == ResidualStateBucket.Dead ? "阵亡" : "弥留";
            return rel + state;
        }

        string BuildResidualInspect(
            XianXia.Core.Simulation.SimulationWorld world,
            ResidualMarkerGroupView group)
        {
            var sb = new StringBuilder(480);
            sb.Append('【').Append(FormatResidualGroupTitle(group)).Append("】\n");
            sb.Append("Hex: ").Append(group.Hex).Append('\n');
            var siteName = ResolveHexSiteName(world, group.Hex);
            if (!string.IsNullOrEmpty(siteName))
                sb.Append("Location: ").Append(siteName).Append('\n');
            sb.Append("Count: ").Append(group.Count).Append("\n\n");
            sb.Append("Characters:\n");
            for (var i = 0; i < group.Characters.Count; i++)
            {
                var row = group.Characters[i];
                if (row == null)
                    continue;
                sb.Append(row.DisplayName).Append('\n');
                sb.Append("  Faction：").Append(row.FactionDisplayName).Append('\n');
                sb.Append("  State：").Append(row.LifeStateLabel).Append('\n');
                if (world != null &&
                    !row.CharacterId.IsNone &&
                    world.Entities.TryGet(row.CharacterId, out var ent) &&
                    ent != null &&
                    CombatLifeStateService.TryGetLifeStateCountdown(world, ent, out var cdLabel, out var cdSec))
                {
                    if (cdLabel == "弥留")
                        sb.Append("  倒计时：").Append(cdSec).Append("s 后转阵亡\n");
                    else if (cdLabel == "尸体")
                        sb.Append("  倒计时：").Append(cdSec).Append("s 后腐烂消失\n");
                }
            }

            return sb.ToString();
        }

        static string ResolveHexSiteName(XianXia.Core.Simulation.SimulationWorld world, HexCoord hex)
        {
            if (world?.Strategic?.Sites == null)
                return string.Empty;
            if (world.Strategic.Sites.TryGetAtHex(hex, out var site) && site != null)
                return string.IsNullOrEmpty(site.DisplayName) ? site.SiteId : site.DisplayName;
            return string.Empty;
        }

        string BuildHexInspect(XianXia.Core.Simulation.SimulationWorld world, HexCoord hex)
        {
            var sb = new StringBuilder(320);
            sb.Append("Hex ").Append(hex).Append('\n');
            if (!world.HexWorld.TryGetTile(hex, out var tile) || tile == null)
            {
                sb.Append("（无地块数据）");
                return sb.ToString();
            }

            sb.Append("地形：").Append(HexTerrainPresentation.GetDisplayName(tile)).Append('\n');
            sb.Append("移动代价：").Append(tile.ResolveMovementCost().ToString("0.##")).Append('\n');
            if (tile.IsRoad)
                sb.Append("道路：是\n");
            sb.Append(tile.IsPassable ? "可通行\n" : "不可通行\n");

            if (world.Strategic.Sites.TryGetAtHex(hex, out var site) && site != null)
            {
                sb.Append('\n');
                sb.Append("地点：").Append(string.IsNullOrEmpty(site.DisplayName) ? site.SiteId : site.DisplayName).Append('\n');
                var category = WorldSitePresentationLayer.ResolveCategory(site);
                sb.Append("类型：").Append(WorldSitePresentationLayer.ResolveCategoryLabel(category)).Append('\n');
                if (!string.IsNullOrEmpty(site.SiteType))
                    sb.Append("SiteType：").Append(site.SiteType).Append('\n');
                if (!string.IsNullOrEmpty(site.OwnerFactionId))
                    sb.Append("归属：").Append(StrategicFactionCatalog.DisplayName(site.OwnerFactionId)).Append('\n');
                if (!string.IsNullOrEmpty(site.LocalMapId))
                    sb.Append("LocalMap：").Append(site.LocalMapId).Append('\n');
            }

            return sb.ToString();
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
            if (CombatLifeStateService.TryGetLifeStateCountdown(world, ent, out var cdLabel, out var cdSec))
            {
                if (cdLabel == "弥留")
                    sb.Append("倒计时：").Append(cdSec).Append("s 后转阵亡\n");
                else if (cdLabel == "尸体")
                    sb.Append("倒计时：").Append(cdSec).Append("s 后腐烂消失\n");
            }
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
            var sb = new StringBuilder(320);
            sb.Append("敌军部队\n\n");
            sb.Append("名称：")
                .Append(string.IsNullOrEmpty(stack.DisplayName) ? stack.Id : stack.DisplayName)
                .Append('\n');
            sb.Append("势力：").Append(StrategicFactionCatalog.DisplayName(stack.FactionId)).Append('\n');
            sb.Append("人数：").Append(stack.MemberCount).Append('\n');
            if (stack.IncapacitatedMemberCount > 0)
                sb.Append("弥留残留：").Append(stack.IncapacitatedMemberCount).Append('\n');
            if (stack.CorpseMemberCount > 0)
                sb.Append("尸体残留：").Append(stack.CorpseMemberCount).Append('\n');
            sb.Append("战力：").Append(CombatPowerCalculator.ForArmyStack(world, stack)).Append('\n');
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

            AppendArmyMemberLifeStates(sb, world, stack);
            sb.Append("\n操作：先左键选我方，再右键该部队攻击");
            return sb.ToString();
        }

        static void AppendArmyMemberLifeStates(
            StringBuilder sb,
            XianXia.Core.Simulation.SimulationWorld world,
            ArmyStack stack)
        {
            if (sb == null || world == null || stack == null)
                return;
            if (!ArmyStackAdapter.TryGetFormalArmy(world, stack, out var army) || army == null)
                return;

            var any = false;
            for (var i = 0; i < army.MemberCharacterIds.Count; i++)
            {
                var id = new EntityId(army.MemberCharacterIds[i]);
                if (id.IsNone || !world.Entities.TryGet(id, out var ent) || ent == null)
                    continue;
                var stamped = CombatLifeStateService.FormatLifeStateWithCountdown(world, ent);
                if (string.IsNullOrEmpty(stamped) || stamped == "存活")
                    continue;
                if (!any)
                {
                    sb.Append("\n成员状态：\n");
                    any = true;
                }

                var name = string.IsNullOrEmpty(ent.DisplayName) ? id.ToString() : ent.DisplayName;
                sb.Append("  ").Append(name).Append(" · ").Append(stamped).Append('\n');
            }
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
            _title = HostImguiStyles.InkLabel(17, bold: true, ink: new Color(0.94f, 0.95f, 0.97f));
            _body = HostImguiStyles.InkLabel(13, wordWrap: true, ink: new Color(0.86f, 0.88f, 0.91f));
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
            _armyFormPanel = new HostArmyFormPanel(_body, _title);
            _armyListPanel = new HostStrategicArmyListPanel(_body, _title, _armyFormPanel);
            _characterListPanel = new HostStrategicCharacterListPanel(_body, _title);
        }
    }
}
