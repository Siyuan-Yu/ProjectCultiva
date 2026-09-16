using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using XianXia.Core.Attributes;
using XianXia.Core.Combat;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;
using XianXia.Core.World.Surface;
using XianXia.Data.Content;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Hex 战略大地图全屏页：头像标位、点选、右Hex 下令；可缩放平移
    /// </summary>
    public sealed class HostWorldMapPanel : MonoBehaviour
    {
        const float AvatarSize = 40f;
        const float NodeHitW = 128f;
        const float NodeHitH = 44f;
        /// <summary>敌军栈默认吸附（屏幕像素，圆形半径外延）。偏小以免抢道路右键移动/summary>
        const float ArmyStackHitPad = 10f;
        /// <summary>与我方头像／接战残留重叠时再缩小/summary>
        const float ArmyStackHitPadContested = 4f;
        /// <summary>判定「叠在一起」：头像与敌军视rect 扩此值后相交/summary>
        const float ArmyStackContestedOverlapPx = 4f;
        /// <summary>
        /// 最大放大：视口半宽（世界单位）。再放大一倍相对「邻站铺满」参考（半宽 1.5 满屏跨度 3）
        /// </summary>
        const float MinViewHalfExtent = 1.5f;

        static float ResolveMinViewHalf(SimulationWorld world)
        {
            if (world?.SurfaceGround?.Active != null)
                return MinViewHalfExtent;
            if (world != null && ArmyHexCommandService.IsHexStrategicActive(world) && world.HexWorld.HasGrid)
            {
                return HexWorldScale.ViewHalfForHexesAcross(
                    HexWorldScale.CloseHexesAcross,
                    world.HexWorld.HexSize);
            }

            return MinViewHalfExtent;
        }
        const float MapPad = 48f;
        /// <summary>道路右键点选：屏幕像素容差（世界距离在放大后过严，几乎点不中）/summary>
        const float RoutePickScreenPx = 28f;
        /// <summary>底部支援半径滑块条高度/summary>
        const float BottomBarH = 36f;
        /// <summary>右侧选中信息面板宽度/summary>
        const float InfoPanelW = 300f;
        const float ReinforceRadiusMin = 0.25f;
        const float ReinforceRadiusMax = 4f;
        /// <summary>Debug：大地图绘制支援半径圈。底栏滑块不受此开关影响/summary>
        const bool ShowReinforcementRadiusDebug = false;
        /// <summary>大地图选中单位：头像外圈高亮（与填充色区分）/summary>
        static readonly Color MapSelectionRingColor = new(0.22f, 0.94f, 1f, 0.95f);
        const float MapSelectionRingWidth = 3f;

        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] KeyCode toggleKey = KeyCode.M;
        [SerializeField] bool open;

        readonly List<HexCoord> _hexPathPreview = new List<HexCoord>(32);
        enum RoutePreviewKind
        {
            None,
            SurfaceRoute,
            ContinuousGoalOnly,
            LegacyPlayerHexRoute
        }
        RoutePreviewKind _routePreviewKind;
        readonly List<Rect> _surfaceRoadWorldRects = new List<Rect>(256);
        string _surfaceRoadCacheIdentity = string.Empty;
        string _lastRoutePreviewDiagnostic = string.Empty;
        bool[] _pathMask;
        int _pathMaskW;
        int _pathMaskH;
        bool _terrainLegendExpanded;
        /// <summary>WorldMap 图层开关：显示势力范围（Territory overlay）。纯 UI preference，不写 SaveGame；panel hide/show 不重置。</summary>
        bool _showTerritoryOverlay = true;
        bool _showSurfaceGeography = true;
        /// <summary>WorldMap 军队表现层；默认 ON，不写入存档。</summary>
        bool _showArmyMarkers = true;
        float _lastMapViewportWidth = 800f;
        float _lastMapViewportHeight = 600f;
        HexCoord? _selectedHex;
        HexCoord? _hoverHex;
        HexCoord? _lastHoverHex;
        readonly HashSet<ulong> _selected = new HashSet<ulong>();
        readonly Dictionary<ulong, Rect> _avatarRects = new Dictionary<ulong, Rect>();
        readonly List<(ResidualMarkerGroupView group, Rect rect)> _residualMarkerRects =
            new List<(ResidualMarkerGroupView, Rect)>(16);
        ResidualMarkerGroupView _selectedResidualGroup;
        readonly List<(string nodeId, Rect rect)> _nodeRects = new List<(string, Rect)>(64);
        readonly Dictionary<string, int> _slotAtSiteKey = new Dictionary<string, int>();
        readonly Dictionary<string, int> _countAtSiteKey = new Dictionary<string, int>();

        // 部队栈点选／右键菜单
        readonly Dictionary<string, Rect> _armyStackRects = new Dictionary<string, Rect>(16);
        readonly Dictionary<string, Rect> _formalArmyRects = new Dictionary<string, Rect>(8);
        readonly HostWorldMapSelectionAuthority _worldMapSelection = new HostWorldMapSelectionAuthority();
        const float FormalArmyMarkerHitPad = 8f;
        // LegacyArmy -> NPC Squad read-only projection. It never participates in command selection.
        string _inspectedLegacySquadArmyId = string.Empty;

        string _lastMapFormalArmyClickId = string.Empty;
        double _lastMapFormalArmyClickTime;
        HostStrategicCharacterListPanel _characterListPanel;
        HostFactionDiplomacyOverviewPanel _factionDiplomacyPanel;
        readonly HostGlobalStrategicToolbar _globalStrategicToolbar = new HostGlobalStrategicToolbar();
        string _selectedStackId = string.Empty;
        /// <summary>只用于清理角色 inspect 选择中的已移除实体。</summary>
        readonly List<EntityId> _orderFilterScratch = new List<EntityId>(8);

        // 弥留头像菜单（Retired：残留战场不再是 gateway，仅 inspect 信息）
        ulong _avatarMenuEntityId;
        bool _avatarMenuOpen;
        Rect _avatarMenuRect;

        // 节点左键菜单
        string _nodeMenuNodeId = string.Empty;
        Rect _nodeMenuRect;
        bool _nodeMenuOpen;

        // Phase 5D-B1（降级）：Gateway 前方目标不可直达时的轻量确认框（仅 PlayerParty）
        bool _gatewayConfirmOpen;
        string _gatewayConfirmSiteId = string.Empty;
        string _gatewayConfirmDisplayName = string.Empty;
        HexCoord _gatewayConfirmApproachHex;
        Rect _gatewayConfirmRect;
        Vector2 _lastContextMousePos;
        // Phase 5D-B1（UI 生命周期）：打开确认框的帧 —— 该帧内的 outside-click 不得关闭刚打开的框（openedThisFrame）
        int _gatewayConfirmOpenFrame = -1;

        /// <summary>右侧信息面板聚焦的节点（左键点节点写入；与菜单开闭无关）/summary>
        string _inspectSiteId = string.Empty;
        string _selectedWorldSiteId = string.Empty;
        Vector2 _inspectScroll;

        string _status = string.Empty;
        bool _wasBlockingInput;
        int _travelingCountLast;
        readonly Dictionary<string, string> _lastSiteOwners = new Dictionary<string, string>(StringComparer.Ordinal);

        // 地图镜头：世界坐标中+ 半宽（世界单位）
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
        GUIStyle _layerToggle;
        Texture2D _px;
        readonly HostSurfaceWorldMapRenderer _surfaceRenderer = new HostSurfaceWorldMapRenderer();

        bool IsSurfaceMode(SimulationWorld world) => world?.SurfaceGround?.Active != null;
        SurfaceWorldMapViewportProjection BuildSurfaceProjection(Rect rect) => new SurfaceWorldMapViewportProjection(rect, _viewCx, _viewCy, _viewHalf);

        public bool IsOpen => open;

        public void Toggle()
        {
            if (open)
                CloseWithLocalMapTakeover();
            else
                Open();
        }

        /// <summary>
        /// 战场内（正常手动战／残留再进／手动战后未点结束）：禁止开大地图
        /// 接战弹窗、自动战结算弹窗阶段仍可留在／打开大地图
        /// </summary>
        public static bool IsBlockedByBattlefield(XianXia.Core.Simulation.SimulationWorld world)
        {
            if (world?.Strategic == null)
                return false;
            // 自动战结算：人还在战略层，结UI 应盖在大地图上，不要卸图跳到 LocalMap
            if (world.Strategic.Participants != null &&
                world.Strategic.Participants.IsAutoSettlement)
                return false;
            return StrategicClockFreezeService.IsModalEncounter(world) ||
                   BattleOfferService.HasActiveManualEncounter(world);
        }

        /// <summary>战后：清掉已腐烂选中；弥留／尸体仍可选中看情报（不可下令）；刷新战略层绘制状态/summary>
        public void NotifyAfterBattleResolved(XianXia.Core.Simulation.SimulationWorld world)
        {
            if (world == null)
                return;
            StrategicEncounterResolveService.NormalizePresenceAfterEncounterExit(world);
            PruneRemovedFromSelection(world);
            _hexPathPreview.Clear();
            // LocalMap 选中仍可能停在已倒下的人FormalHud 左上角误显「弥留
            bootstrap?.SelectionController?.ClearSelection();
            RefreshStrategicPresentation(world);
        }

        /// <summary>自动战／手动战后：重置大地图缓存与提示，确保残留弥留立刻可见/summary>
        public void RefreshStrategicPresentation(XianXia.Core.Simulation.SimulationWorld world)
        {
            _avatarMenuOpen = false;
            _nodeMenuOpen = false;
            _gatewayConfirmOpen = false;
            _inspectScroll = Vector2.zero;

            if (world?.Strategic?.Armies != null &&
                !string.IsNullOrEmpty(_selectedStackId) &&
                !world.Strategic.Armies.TryGet(_selectedStackId, out _))
                _selectedStackId = string.Empty;

            if (BattleOfferService.HasLingeringBattlefield(world))
                _status = "战后残留仍在｜弥留／尸体可左键查看，移动至该格可见现场";
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

            bootstrap?.InventoryPanel?.Close();
            bootstrap?.ConstructionPanel?.Close();
            bootstrap?.QuestJournal?.Close();
            open = true;
            _requestClose = false;
            if (bootstrap?.Session != null && bootstrap.Session.IsInitialized)
            {
                var world = bootstrap.Session.World;
                // WorldMap is a planning overlay for PlayerParty. Preserve the route and exact
                // canonical position, keep physical execution LocalVisible, and stop any active
                // presentation path while the input gate is held.
                PlayerPartyHexTravelService.HoldForLocalVisibleExecution(world);
                bootstrap.PlayerPartyController?.FreezeLocalVisibleTravelForPlanning();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                FormalArmyStrategicMutationDiagnostics.BindPresentationWorld(world);
#endif
                StrategicEncounterResolveService.NormalizePresenceAfterEncounterExit(world);
                PruneRemovedFromSelection(world);
            }

            // WorldMap is an input/planning overlay. Opening or closing it does not own the
            // player's ManualPaused intent and does not replace the movement authority.
        }

        /// <summary>大地图当前主选（活人优先）；FormalHud 在开图时不要误显 LocalMap 旧选中的弥留/summary>
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

        /// <summary>到站弹窗「去查看」：打开后选中刚抵达的角色/summary>
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

        /// <summary>到站「去查看」：只打开 WorldMap 定位/选中对象；
        /// 目标 Hex 有 residual 也不自动打开 Encounter/BattleOffer（残留战场不再是 gateway）。</summary>
        public void Close()
        {
            CloseInternal(takeoverLocalMap: false);
        }

        /// <summary>
        /// Phase 2C：关闭 WorldMap = 中断 AutoTravel（若有）+ 在当前真实位置展开／恢复 LocalMap。
        /// </summary>
        public void CloseWithLocalMapTakeover()
        {
            CloseInternal(takeoverLocalMap: true);
        }

        void CloseInternal(bool takeoverLocalMap)
        {
            var world = bootstrap?.Session != null && bootstrap.Session.IsInitialized
                ? bootstrap.Session.World
                : null;
            var party = bootstrap?.Session?.PlayerParty;
            var wasMoving = world?.PlayerPartyTravel != null && world.PlayerPartyTravel.IsMoving;

            // Peek：权威位置与当前 LocalMap 已一致 → 只关 Overlay，绝不改 LocationKind / SiteId。
            var matches = world != null &&
                          party != null &&
                          PlayerPartyHexTravelService.PartyLocalMapMatchesAuthoritativeLocation(world, party);
            var needExpand = takeoverLocalMap &&
                             party != null &&
                             party.HasActive &&
                             world != null &&
                             (wasMoving || !matches);

            open = false;
            _requestClose = false;
            _nodeMenuOpen = false;
            _nodeMenuNodeId = string.Empty;
            CloseGatewayConfirm();
            _avatarMenuOpen = false;
            _characterListPanel?.Close();
            _factionDiplomacyPanel?.Close();
            _globalStrategicToolbar.CloseAll();
            _worldMapSelection.SelectPlayerParty();
            _hexPathPreview.Clear();
            _panning = false;
            ForceClearInputBlock();

            // Continuous Outdoor already owns the live presentation and canonical sync. Closing
            // its planning overlay only rearms the existing LocalVisible route; it must not enter
            // the legacy World -> Local takeover/materialize path.
            if (bootstrap?.ContinuousOutdoorSurfaceRuntime != null &&
                bootstrap.ContinuousOutdoorSurfaceRuntime.IsActive)
            {
                if (world?.PlayerPartyTravel != null &&
                    world.PlayerPartyTravel.IsMoving &&
                    world.PlayerPartyTravel.ExecutionMode == PlayerPartyTravelExecutionMode.LocalVisible)
                    bootstrap.PlayerPartyController?.ResumeLocalVisibleTravelAfterPlanning();
                return;
            }

            if (!needExpand)
                return;

            // Phase 5B: CloseWorldMapTakeover preserves AutoTravel (LocalVisible); Idle EnterLocal.
            // ExpandLocalMap stays Host-side.
            var enter = PlayerPartyHexTravelService.CloseWorldMapTakeover(world, party);
            if (enter.IsSuccess && bootstrap != null)
            {
                bootstrap.PlayerPartyController?.OnLegacyLocalVisibleTravelTakeover();
                // W1D: WorldMap close is a normal Wilderness entry. Once Core has restored
                // the canonical outdoor position, coverage owns presentation before legacy
                // LocalMap materialization can run.
                if (world.PlayerPartyTravel != null &&
                    world.PlayerPartyTravel.LocationKind == PlayerPartyLocationKind.AtWorldPosition &&
                    bootstrap.ContinuousOutdoorSurfaceRuntime != null &&
                    bootstrap.ContinuousOutdoorSurfaceRuntime.TryActivateAtCurrentWorldPosition())
                {
                    bootstrap.SurfaceExitZonePresenter?.Clear();
                    return;
                }
                bootstrap.ExpandLocalMapForCurrentPartyWorld(closeWorldMap: false);
            }
        }

        void SetArmyLayerVisible(bool visible)
        {
            _showArmyMarkers = visible;
            if (visible)
                return;

            // 关闭图层必须同步清除所有不可见目标的命中／选择／菜单／路线表现；不改 simulation。
            _formalArmyRects.Clear();
            _inspectedLegacySquadArmyId = string.Empty;
            _armyStackRects.Clear();
            _residualMarkerRects.Clear();
            _avatarRects.Clear();
            _selected.Clear();
            _selectedStackId = string.Empty;
            _selectedResidualGroup = null;
            _avatarMenuOpen = false;
            _lastMapFormalArmyClickId = string.Empty;
            _worldMapSelection.SelectPlayerParty();
            _hexPathPreview.Clear();
            var world = bootstrap?.Session?.World;
            if (world != null)
                RefreshPlayerPartyPathPreview(world);
        }

        public void Bind(PlayableHostBootstrap host) => bootstrap = host;

        public void ClearSessionState()
        {
            Close();
            _status = string.Empty;
            _selected.Clear();
            _travelingCountLast = 0;
            _viewReady = false;
            _selectedStackId = string.Empty;
            _worldMapSelection.SelectPlayerParty();
            _nodeMenuOpen = false;
            _nodeMenuNodeId = string.Empty;
            CloseGatewayConfirm();
            _inspectSiteId = string.Empty;
            _selectedWorldSiteId = string.Empty;
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
                CloseWithLocalMapTakeover();

            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return;

            var world = bootstrap.Session.World;
            // 进战场后大地图一律关掉且不可再开（正常战／残留再进／未点结束
            if (IsBlockedByBattlefield(world))
            {
                if (open)
                    Close();
                if (Input.GetKeyDown(toggleKey))
                    ShowBattlefieldMapBlockedToast();
                return;
            }

            if (Input.GetKeyDown(toggleKey))
            {
                if (open)
                    CloseWithLocalMapTakeover();
                else
                {
                    Open();
                    _viewReady = false;
                }
                return;
            }

            if (OtherBlockingPanelOpen())
            {
                if (open)
                    Close();
                return;
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
                if (kv.Value != null && kv.Value.Mode == PartyWorldPresenceMode.AtSite)
                    traveling++;
            }

            if (_travelingCountLast > 0 && traveling < _travelingCountLast)
            {
                // 仅当 PlayerParty 仍属 Site 聚合时才同步焦点；开世界旅行中禁止回写。
                var travel = world.PlayerPartyTravel;
                var allowSync = travel == null ||
                                !travel.HasPosition ||
                                (!travel.IsMoving &&
                                 travel.LocationKind == PlayerPartyLocationKind.AtWorldSite);
                if (allowSync)
                {
                    WorldTravelService.SyncPartyFocus(world);
                    _status = "有人抵达站点";
                }
            }

            _travelingCountLast = traveling;
        }

        bool OtherBlockingPanelOpen()
        {
            var j = bootstrap.QuestJournal;
            var inv = bootstrap.InventoryPanel;
            var construction = bootstrap.ConstructionPanel;
            return (j != null && j.IsOpen) ||
                   (inv != null && inv.IsOpen) ||
                   (construction != null && construction.IsOpen);
        }

        void EnsureView(
            XianXia.Core.Simulation.SimulationWorld world,
            float mapViewportWidth,
            float mapViewportHeight)
        {
            ComputeFullHalf(world, mapViewportWidth, mapViewportHeight, out _fullHalf);
            if (_viewReady)
            {
                _viewHalf = Mathf.Clamp(_viewHalf, ResolveMinViewHalf(world), _fullHalf);
                ClampSurfaceCamera(mapViewportWidth, mapViewportHeight, world);
                return;
            }

            var surfaceMode = IsSurfaceMode(world);
            var hexMode = !surfaceMode && ArmyHexCommandService.IsHexStrategicActive(world) && world.HexWorld.HasGrid;
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
                var focusId = world.PartyWorld.SiteId;
                if (ArmyHexBattleAnchorService.TryResolveHexForSite(world, focusId, out var focusHex))
                    HexMath.ToWorldPosition(focusHex, world.HexWorld.HexSize, out _viewCx, out _viewCy);
            }
            else if (surfaceMode)
            {
                var nav = world.SurfaceGround.Active;
                _viewCx = nav.OriginX + (nav.MaxX-nav.OriginX)*.5f; _viewCy = nav.OriginY + (nav.MaxY-nav.OriginY)*.5f;
                if (bootstrap?.Session?.PlayerParty != null && PlayerPartyWorldLocationQuery.TryResolve(world, bootstrap.Session.PlayerParty, out var party)) { _viewCx=party.WorldPosition.X; _viewCy=party.WorldPosition.Y; }
            }

            ClampSurfaceCamera(mapViewportWidth, mapViewportHeight, world);
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
        void ClampSurfaceCamera(float w,float h,SimulationWorld world)
        {
            if (IsSurfaceMode(world)) { var nav=world.SurfaceGround.Active; var scale=Mathf.Min(w,h)/(2f*Mathf.Max(.001f,_viewHalf)); var halfX=w/(2f*scale); var halfY=h/(2f*scale); _viewCx=Mathf.Clamp(_viewCx,nav.OriginX+halfX,nav.MaxX-halfX); _viewCy=Mathf.Clamp(_viewCy,nav.OriginY+halfY,nav.MaxY-halfY); return; }
            ClampHexCamera(w,h,world);
        }

        static void ComputeFullHalf(
            SimulationWorld world,
            float mapViewportWidth,
            float mapViewportHeight,
            out float fullHalf)
        {
            if (world?.SurfaceGround?.Active != null) { var nav=world.SurfaceGround.Active; fullHalf=Mathf.Max((nav.MaxX-nav.OriginX)*.5f,(nav.MaxY-nav.OriginY)*.5f); return; }
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

            fullHalf = MinViewHalfExtent;
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
            HostUiHitTest.BlockSelectionWholeScreen();

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

            var title = "世界地图（左键查看｜右键规划玩家小队前往｜M关闭）";
            GUI.Label(
                new Rect(pad, titleY, Screen.width - 380f, 28f),
                title, _title);

            // 产品默认显示 actual-control world geometry；关闭仅影响 presentation。
            var showTerritory = GUI.Toggle(
                new Rect(Screen.width - 350f, titleY + 4f, 116f, 26f),
                _showTerritoryOverlay,
                "显示势力范围",
                _layerToggle);
            if (showTerritory != _showTerritoryOverlay)
            {
                _showTerritoryOverlay = showTerritory;
                HostHexWorldRenderer.SetTerritoryOverlayVisible(showTerritory);
            }

            _showSurfaceGeography = GUI.Toggle(
                new Rect(Screen.width - 480f, titleY + 4f, 122f, 26f),
                _showSurfaceGeography,
                "显示地理层",
                _layerToggle);

            var showArmies = GUI.Toggle(
                new Rect(Screen.width - 226f, titleY + 4f, 108f, 26f),
                _showArmyMarkers,
                "显示 NPC 小队",
                _layerToggle);
            if (showArmies != _showArmyMarkers)
                SetArmyLayerVisible(showArmies);

            if (GUI.Button(new Rect(Screen.width - 100f, titleY, 84f, 32f), "关闭"))
                CloseWithLocalMapTakeover();

            DrawMapToolbar(pad, toolbarY, world);

            if (!IsSurfaceMode(world) && (!ArmyHexCommandService.IsHexStrategicActive(world) ||
                world?.HexWorld == null ||
                !world.HexWorld.HasGrid))
            {
                GUI.Label(new Rect(pad, toolbarY, Screen.width - pad * 2f, 40f), "Hex 战略地图未加载。", _body);
                return;
            }

            _lastMapViewportWidth = Screen.width - pad * 2f - InfoPanelW - 8f;
            _lastMapViewportHeight = Screen.height - mapTop - pad - BottomBarH;
            EnsureView(world, _lastMapViewportWidth, _lastMapViewportHeight);

            var focusName = world.PartyWorld.SiteId;
            if (world.Strategic.Sites.TryGet(world.PartyWorld.SiteId, out var focusSite) &&
                focusSite != null &&
                !string.IsNullOrEmpty(focusSite.DisplayName))
                focusName = focusSite.DisplayName;

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
            var hexGutterActive = !IsSurfaceMode(world) && ArmyHexCommandService.IsHexStrategicActive(world) &&
                                  world?.HexWorld != null &&
                                  world.HexWorld.HasGrid;
            GUI.color = hexGutterActive
                ? HostHexWorldRenderer.ResolveGutterColor()
                : new Color(0.93f, 0.89f, 0.78f, 1f);
            GUI.DrawTexture(mapRect, _px);
            GUI.color = Color.white;

            HexMapViewportProjection hexProjection = default;
            if (ArmyHexCommandService.IsHexStrategicActive(world) &&
                world?.HexWorld != null &&
                world.HexWorld.HasGrid)
            {
                hexProjection = BuildHexProjection(mapRect, world);
            }

            if (!IsSurfaceMode(world)) RefreshHexPresentation(hexProjection, world); else RefreshRoutePreview(world);
            // 与 panel toggle 保持一致的 overlay 图层状态（唯一入口；防御性同步）。
            HostHexWorldRenderer.SetTerritoryOverlayVisible(_showTerritoryOverlay);
            DrawGraph(mapRect, hexProjection, world);
            DrawMapUnitOverlays(mapRect, hexProjection, world);
            if (ShowReinforcementRadiusDebug)
                DrawReinforcementRadiusOverlay(mapRect, world);

            DrawGatewayConfirm(world);
            DrawAvatarContextMenu(world);
            DrawInspectPanel(infoRect, world);
            DrawReinforcementRadiusSlider(pad, world);
            DrawStrategicRosterPanels(world);
            TryDismissContextMenusOnOutsideClick();
            if (Event.current != null && Event.current.type == EventType.Used)
                return;
            // 菜单仍开着（点在菜单内）时不处理地图下令；外侧点击已在上面关掉菜单且不吞事
            if (_nodeMenuOpen || _avatarMenuOpen || _gatewayConfirmOpen)
                return;
            HandleMapInput(mapRect, hexProjection, world);
            HandleCameraInput(mapRect, world);
            HostUiHitTest.EndFrame();
            // 进入场景可能在本OnGUI 中途关掉；立刻停画，避免同帧再盖一
            if (!open)
                return;
        }

        void HandleCameraInput(Rect mapRect, XianXia.Core.Simulation.SimulationWorld world)
        {
            var e = Event.current;
            if (e == null)
                return;

            if (HostUiHitTest.ContainsCurrentGuiPoint(e.mousePosition))
            {
                // 中键拖拽可越过 UI 继续平移；新开拖拽／滚轮在 UI 上时不交给地图
                if (e.type != EventType.MouseDrag || !_panning)
                    return;
            }

            if (!mapRect.Contains(e.mousePosition) && e.type != EventType.MouseUp)
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

                ClampSurfaceCamera(mapRect.width, mapRect.height, world);

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
                ClampSurfaceCamera(mapRect.width, mapRect.height, world);
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
            if (IsSurfaceMode(world)) return BuildSurfaceProjection(mapRect).ProjectWorld(wx, wy);
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
            if (world?.Strategic?.Sites?.Sites == null)
                return;

            foreach (var kv in world.Strategic.Sites.Sites)
            {
                var site = kv.Value;
                if (site == null)
                    continue;

                var owner = site.OwnerFactionId ?? string.Empty;
                if (_lastSiteOwners.TryGetValue(site.SiteId, out var prev) &&
                    !string.Equals(prev, owner, StringComparison.Ordinal))
                {
                    var siteName = string.IsNullOrEmpty(site.DisplayName) ? site.SiteId : site.DisplayName;
                    _status = "据点易主：" + siteName + "  " +
                              StrategicAcceptanceInspector.ResolveOwnerDisplay(prev) + " → " +
                              StrategicAcceptanceInspector.ResolveOwnerDisplay(owner);
                }

                _lastSiteOwners[site.SiteId] = owner;
            }
        }

        void DrawMapToolbar(float pad, float toolbarY, XianXia.Core.Simulation.SimulationWorld world)
        {
            HostUiHitTest.Block(new Rect(pad, toolbarY, Screen.width - pad * 2f, 26f));
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

            EnsureStrategicProductPanels();
            var clickedModule = _globalStrategicToolbar.Draw(x, y, _body);
            if (clickedModule != HostGlobalStrategicToolbar.ModuleId.None)
                HandleGlobalStrategicToolbarClick(clickedModule);
            x += _globalStrategicToolbar.LastDrawnWidth;

            // Phase 2B：PlayerParty Travel（不创建 FormalArmy）
            x += 12f;
            var party = bootstrap?.Session?.PlayerParty;
            var partyMoving = world.PlayerPartyTravel != null && world.PlayerPartyTravel.IsMoving;
            GUI.enabled = partyMoving;
            if (GUI.Button(new Rect(x, y, 120f, 26f), "停止 Party 旅行") && partyMoving)
            {
                var cancel = PlayerPartyHexTravelService.CancelTravel(world, party);
                _status = cancel.IsSuccess
                    ? "已停止 Party 旅行 @" + world.PlayerPartyTravel.CurrentHex
                    : cancel.Error.Message;
            }

            GUI.enabled = true;

            if (world.Strategic != null &&
                (world.Strategic.HasBlockingInterrupt ||
                 StrategicClockFreezeService.IsWorldTickFrozen(world)))
            {
                x += 12f;
                var reason = world.Strategic.ClockFreeze != null
                    ? world.Strategic.ClockFreeze.Reason.ToString()
                    : "?";
                GUI.Label(
                    new Rect(x, y + 4f, 320f, 22f),
                    world.Strategic.HasBlockingInterrupt
                        ? "战略打断中" : "战略时间冻结\uff1a" + reason,
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
            // 步进 0.05，避免浮点抖
            next = Mathf.Round(next * 20f) / 20f;
            if (!Mathf.Approximately(next, radius))
                world.Strategic.ReinforcementWorldRadius = next;

            if (GUI.Button(new Rect(bar.xMax - 72f, bar.y + 6f, 62f, 24f), "默认"))
                world.Strategic.ReinforcementWorldRadius = ReinforcementRangeService.DefaultWorldRadius;
        }

        /// <summary>以当前选中单位／敌军栈为圆心画支援半径圈（世界坐标）/summary>
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
            // 半透明填充（近似：中心小方块叠圆环感弱，用环线为主
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

            // 其次：已选己方头
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

            // 再次：接Offer 锚点
            var snap = world.Strategic.Participants;
            if (snap != null &&
                ArmyHexBattleAnchorService.TryGetBattleAnchorHex(snap, out var offerAnchorHex))
            {
                HexMath.ToWorldPosition(offerAnchorHex, world.HexWorld.HexSize, out cx, out cy);
                return true;
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
            if (stack == null || world?.HexWorld == null || !world.HexWorld.HasGrid)
                return false;

            if (!string.IsNullOrEmpty(stack.FormalArmyId) &&
                world.Strategic.FormalArmies.TryGet(stack.FormalArmyId, out var formal) &&
                formal != null &&
                formal.UsesHexStrategicPosition &&
                FormalArmyHexWorldPositionResolver.TryResolve(world, formal, out cx, out cy))
                return true;

            if (ArmyHexBattleAnchorService.TryResolveHexForSite(world, stack.SiteId, out var hex))
            {
                HexMath.ToWorldPosition(hex, world.HexWorld.HexSize, out cx, out cy);
                return true;
            }

            return false;
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
            // 粗略：两端都在外则跳过；否则画细
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

            RefreshRoutePreview(world);
        }

        void RefreshPlayerPartyPathPreview(SimulationWorld world)
        {
            RefreshRoutePreview(world);
        }

        void RefreshRoutePreview(SimulationWorld world)
        {
            _hexPathPreview.Clear();
            _routePreviewKind = RoutePreviewKind.None;
            if (world == null)
            {
                ReportRoutePreviewSource(world);
                return;
            }

            var motion = world.PlayerPartyTravel;
            if (motion == null || !motion.IsMoving)
            {
                ReportRoutePreviewSource(world);
                return;
            }

            if (motion.HasContinuousPhysicalDestination)
            {
                _routePreviewKind = motion.HasContinuousSurfaceRoute &&
                                    motion.ContinuousSurfaceRouteIndex < motion.ContinuousSurfaceRoute.Count
                    ? RoutePreviewKind.SurfaceRoute
                    : RoutePreviewKind.ContinuousGoalOnly;
                // Continuous plans retain a coarse HexPath for strategic context. Neither an
                // incomplete cross-coverage plan nor an old restored plan may present that path
                // as verified physical navigation.
                ReportRoutePreviewSource(world);
                return;
            }

            _routePreviewKind = RoutePreviewKind.LegacyPlayerHexRoute;
            var path = motion.HexPath;

            // Phase 5R-B6.3A：route 起点 authority 统一走 Query。AtWorldSite + departure 时
            // 起点 = Canonical 派生 hex（不再是冻结的 CurrentHex=presence），消除
            // “presence→真实位置”伪前缀（人工看到的先绕行再转向目标）。
            PlayerPartyWorldLocationQuery.TryResolveRouteStartHex(
                world, motion, out var startHex, out var pathIndex);

            _hexPathPreview.Add(startHex);
            for (var i = pathIndex; i < motion.HexPathCount; i++)
                _hexPathPreview.Add(path[i]);
            if (_hexPathPreview.Count == 1 && motion.DestinationHex != startHex)
                _hexPathPreview.Add(motion.DestinationHex);
            ReportRoutePreviewSource(world);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"),
         System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        void ReportRoutePreviewSource(SimulationWorld world)
        {
            var motion = world?.PlayerPartyTravel;
            var geography = bootstrap?.ContinuousOutdoorSurfaceRuntime?.ActiveGeography;
            var nav = geography?.Navigation;
            var owner = _routePreviewKind == RoutePreviewKind.SurfaceRoute ||
                        _routePreviewKind == RoutePreviewKind.ContinuousGoalOnly ||
                        _routePreviewKind == RoutePreviewKind.LegacyPlayerHexRoute
                ? "PlayerParty"
                : "None";
            var startCovered = motion != null && nav != null && motion.HasPosition &&
                               nav.Contains(motion.WorldPosition.X, motion.WorldPosition.Y);
            var goalCovered = motion != null && nav != null && motion.HasContinuousPhysicalDestination &&
                              nav.Contains(motion.ContinuousPhysicalDestination.X,
                                  motion.ContinuousPhysicalDestination.Y);
            var text = "Owner=" + owner +
                       " Kind=" + _routePreviewKind +
                       " TravelPlanVersion=" + (motion?.TravelPlanVersion ?? -1) +
                       " SurfaceId=" + (geography?.SurfaceId ?? string.Empty) +
                       " StartInCoverage=" + startCovered +
                       " GoalInCoverage=" + goalCovered +
                       " RoutePointCount=" + (motion?.ContinuousSurfaceRoute.Count ?? 0) +
                       " RouteIndex=" + (motion?.ContinuousSurfaceRouteIndex ?? 0) +
                       " SourceRevision=" + (geography?.SourceRevision ?? string.Empty) +
                       " SourceHash=" + (geography?.SourceHash ?? string.Empty);
            if (string.Equals(text, _lastRoutePreviewDiagnostic, StringComparison.Ordinal)) return;
            _lastRoutePreviewDiagnostic = text;
            Debug.Log("[WorldMapRoutePreview] " + text, this);
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
            XianXia.Core.Simulation.SimulationWorld world)
        {
            if (IsSurfaceMode(world))
            {
                _nodeRects.Clear();
                var nav = world.SurfaceGround.Active;
                ContinuousSurfaceWorldMapDefinition cache = null;
                if (bootstrap?.Session?.Registry != null)
                    bootstrap.Session.Registry.TryGetContinuousSurfaceWorldMap(nav.SurfaceId, out cache);
                _surfaceRenderer.Draw(mapRect, BuildSurfaceProjection(mapRect), cache, nav);
                DrawSurfaceGeography(mapRect, world); DrawSurfaceSiteMarkers(mapRect, world); DrawContinuousSurfaceRoutePreview(mapRect, world); return;
            }
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
                    ResolveSelectedWorldSite(world),
                    _hexPathPreview,
                    _pathMask,
                    _pathMaskW,
                    _pathMaskH);
                if (_showSurfaceGeography)
                    DrawSurfaceGeography(mapRect, world);
                DrawContinuousSurfaceRoutePreview(mapRect, world);
                return;
            }
        }

        void DrawSurfaceSiteMarkers(Rect mapRect, SimulationWorld world)
        {
            var projection=BuildSurfaceProjection(mapRect);
            foreach (var pair in world.Strategic.Sites.Sites)
            {
                var site=pair.Value; if(site==null || !world.SurfaceGround.TryResolveSiteArrival(site.SiteId,out _,out var point)) continue;
                var screen=projection.ProjectWorld(point.X,point.Y); if(!mapRect.Contains(screen)) continue;
                var rect=new Rect(screen.x-6f,screen.y-6f,12f,12f); var old=GUI.color; GUI.color=Color.white; GUI.DrawTexture(rect,_px); GUI.color=old;
                GUI.Label(new Rect(screen.x+8f,screen.y-11f,140f,20f),string.IsNullOrEmpty(site.DisplayName)?site.SiteId:site.DisplayName,_avatarLabel);
                _nodeRects.Add((site.SiteId,rect));
            }
        }

        void DrawSurfaceGeography(Rect mapRect, SimulationWorld world)
        {
            var geography = bootstrap?.ContinuousOutdoorSurfaceRuntime?.ActiveGeography;
            if (geography == null) return;
            EnsureSurfaceRoadDisplayCache(geography);
            var old = GUI.color;
            GUI.color = new Color(.78f, .59f, .30f, .88f);
            for (var i = 0; i < _surfaceRoadWorldRects.Count; i++)
            {
                var road = _surfaceRoadWorldRects[i];
                var a = ProjectHex(mapRect, world, road.xMin, road.yMin);
                var b = ProjectHex(mapRect, world, road.xMax, road.yMax);
                var rect = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y),
                    Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
                if (rect.Overlaps(mapRect)) GUI.DrawTexture(rect, _px);
            }
            for (var i = 0; i < geography.MapPrimitives.Count; i++)
            {
                var p = geography.MapPrimitives[i];
                if (p.Kind == "roadPolyline")
                    continue;
                var a = ProjectHex(mapRect, world, p.WorldX, p.WorldY);
                var b = ProjectHex(mapRect, world, p.WorldX + p.WorldWidth, p.WorldY + p.WorldHeight);
                var rect = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
                if (!rect.Overlaps(mapRect)) continue;
                GUI.color = p.Kind == "waterRegion" ? new Color(.10f, .38f, .72f, .76f) :
                    p.Kind == "bridge" ? new Color(.64f, .40f, .16f, .95f) : new Color(.25f, .27f, .29f, .92f);
                GUI.DrawTexture(rect, _px);
            }
            GUI.color = Color.white;
            for (var i = 0; i < geography.Landmarks.Count; i++)
            {
                var l = geography.Landmarks[i];
                var point = ProjectHex(mapRect, world, l.WorldX, l.WorldY);
                if (mapRect.Contains(point))
                    GUI.Label(new Rect(point.x - 70f, point.y - 18f, 140f, 20f), l.Label, _avatarLabel);
            }
            GUI.color = old;
        }

        void EnsureSurfaceRoadDisplayCache(OutdoorSurfaceGeographyDefinition geography)
        {
            var nav = geography?.Navigation;
            var identity = nav == null
                ? string.Empty
                : geography.SurfaceId + "|" + geography.SourceRevision + "|" + geography.SourceHash +
                  "|" + nav.OriginX + "|" + nav.OriginY + "|" + nav.Width + "|" + nav.Height;
            if (string.Equals(identity, _surfaceRoadCacheIdentity, StringComparison.Ordinal)) return;
            _surfaceRoadCacheIdentity = identity;
            _surfaceRoadWorldRects.Clear();
            if (nav == null) return;

            // Build once per checked-in bake identity. Only final dry Road cells become road
            // display; water/solid cells crossed by the authoring polyline are deliberately absent.
            // Bridge has its own distinct primitive and remains visibly above the river.
            for (var y = 0; y < nav.Height; y++)
            {
                var runStart = -1;
                for (var x = 0; x <= nav.Width; x++)
                {
                    var road = false;
                    if (x < nav.Width)
                    {
                        nav.WalkGrid.CellToWorldCenter(x, y, out var wx, out var wy);
                        if (nav.TryGetCell(wx, wy, out var kind))
                            road = (kind & SurfaceGroundCellKind.Road) != 0 &&
                                   (kind & (SurfaceGroundCellKind.Water |
                                            SurfaceGroundCellKind.Bridge |
                                            SurfaceGroundCellKind.Solid)) == 0;
                    }
                    if (road && runStart < 0) runStart = x;
                    if (road || runStart < 0) continue;
                    _surfaceRoadWorldRects.Add(new Rect(
                        nav.OriginX + runStart * nav.CellSize,
                        nav.OriginY + y * nav.CellSize,
                        (x - runStart) * nav.CellSize,
                        nav.CellSize));
                    runStart = -1;
                }
            }
        }

        void DrawContinuousSurfaceRoutePreview(Rect mapRect, SimulationWorld world)
        {
            var motion = world?.PlayerPartyTravel;
            if (_worldMapSelection.Kind != HostWorldMapSelectionKind.PlayerParty ||
                motion == null || !motion.IsMoving || !motion.HasContinuousPhysicalDestination)
                return;
            if (_routePreviewKind == RoutePreviewKind.ContinuousGoalOnly)
            {
                DrawContinuousGoalMarker(mapRect, world, motion, showUnavailableLabel: true);
                return;
            }
            if (_routePreviewKind != RoutePreviewKind.SurfaceRoute || !motion.HasContinuousSurfaceRoute)
                return;
            var old = GUI.color;
            GUI.color = new Color(.25f, .95f, .82f, .95f);
            var previous = ProjectHex(mapRect, world, motion.WorldPosition.X, motion.WorldPosition.Y);
            for (var i = motion.ContinuousSurfaceRouteIndex; i < motion.ContinuousSurfaceRoute.Count; i++)
            {
                var point = motion.ContinuousSurfaceRoute[i];
                var next = ProjectHex(mapRect, world, point.X, point.Y);
                DrawClippedSegment(previous, next, mapRect);
                previous = next;
            }
            GUI.color = old;
            DrawContinuousGoalMarker(mapRect, world, motion, showUnavailableLabel: false);
        }

        void DrawContinuousGoalMarker(
            Rect mapRect, SimulationWorld world, PlayerPartyWorldMotion motion, bool showUnavailableLabel)
        {
            var goal = motion.ContinuousPhysicalDestination;
            var screen = ProjectHex(mapRect, world, goal.X, goal.Y);
            if (!mapRect.Contains(screen)) return;
            var old = GUI.color;
            GUI.color = new Color(.98f, .76f, .20f, .96f);
            GUI.DrawTexture(new Rect(screen.x - 5f, screen.y - 5f, 10f, 10f), _px);
            GUI.color = Color.white;
            if (showUnavailableLabel)
                GUI.Label(new Rect(screen.x + 8f, screen.y - 11f, 230f, 22f),
                    "目的地已设定｜暂无完整地形路线预览", _body);
            GUI.color = old;
        }

        void DrawMapUnitOverlays(
            Rect mapRect,
            HexMapViewportProjection projection,
            XianXia.Core.Simulation.SimulationWorld world)
        {
            var prevDepth = GUI.depth;
            GUI.depth = -100;
            var hexMode = ArmyHexCommandService.IsHexStrategicActive(world) &&
                          world?.HexWorld != null &&
                          world.HexWorld.HasGrid;
            if (_showArmyMarkers)
            {
                DrawResidualMarkers(mapRect, world,
                    hexMode: !IsSurfaceMode(world), hexProjection: projection);
                DrawFormalArmyAvatars(mapRect, world);
                DrawArmyStacks(mapRect, world);
                DrawAvatars(mapRect, world, hexMode: true, hexProjection: projection);
            }
            else
            {
                _residualMarkerRects.Clear();
                _formalArmyRects.Clear();
                _armyStackRects.Clear();
                _avatarRects.Clear();
            }
            DrawPlayerPartyMarker(mapRect, world, projection);
            BattleEngagementWorldMapDebug.Draw(projection, world);

            GUI.depth = prevDepth;
        }

        void DrawArmyStacks(
            Rect mapRect,
            XianXia.Core.Simulation.SimulationWorld world)
        {
            _armyStackRects.Clear();
            if (world.Strategic?.Armies == null)
                return;

            foreach (var kv in world.Strategic.Armies.Stacks)
            {
                var stack = kv.Value;
                if (stack == null)
                    continue;
                if (!ArmyWorldMapPresentation.ShouldDrawArmyStackMarker(
                        world, stack, ResolvePlayerFactionId(world)))
                    continue;
                // 弥留／尸体已由个体头像绘制，不再叠聚合栈标记
                if (stack.HasDownedRemnant)
                    continue;

                float wx;
                float wy;
                if (!TryResolveArmyStackWorldPoint(world, stack, out wx, out wy))
                    continue;

                var p = Project(mapRect, wx, wy);
                // 贴节点时挪到标签外侧，避免「躲在荒村后面
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
                    ? stack.CorpseMemberCount + "人·尸体" : stack.HasIncapacitatedRemnant
                        ? stack.IncapacitatedMemberCount + "人·弥留" : stack.MemberCount + "人 · " +
                          (string.IsNullOrEmpty(stack.DisplayName) ? "NPC小队" : stack.DisplayName);
                if (stack.HasCorpseRemnant)
                {
                    GUI.color = new Color(0.42f, 0.36f, 0.30f, 0.88f);
                    GUI.DrawTexture(new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, rect.height - 8f), _px);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(rect.x, rect.y - 2f, rect.width, rect.height), "弥", _avatarLabel);
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


        void DrawResidualMarkers(
            Rect mapRect,
            XianXia.Core.Simulation.SimulationWorld world,
            bool hexMode = false,
            HexMapViewportProjection hexProjection = default)
        {
            _residualMarkerRects.Clear();
            if (world == null || (hexMode && (world.HexWorld == null || !world.HexWorld.HasGrid)))
                return;

            var groups = hexMode
                ? StrategicResidualPresentationQuery.Query(world)
                : StrategicResidualPresentationQuery.QueryContinuous(world);
            if (groups.Count == 0)
                return;

            // Low priority first; high priority draws last (IMGUI later = on top).
            groups.Sort((a, b) => a.VisualPriority.CompareTo(b.VisualPriority));

            var markerSize = 20f;
            var hexSize = world.HexWorld?.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            var edgeAnchorX = hexSize * 0.42f;
            var edgeAnchorY = hexSize * 0.48f;
            const float stackStep = 7f;

            var slotByHex = new Dictionary<string, int>(8);
            for (var i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                if (group == null || group.Count <= 0)
                    continue;
                if (hexMode && !world.HexWorld.Contains(group.Hex))
                    continue;

                float wx, wy;
                if (hexMode)
                {
                    HexMath.ToWorldPosition(group.Hex, hexSize, out wx, out wy);
                    wx += edgeAnchorX;
                    wy += edgeAnchorY;
                    var hexKey = group.Hex.Q + ":" + group.Hex.R;
                    slotByHex.TryGetValue(hexKey, out var slot);
                    slotByHex[hexKey] = slot + 1;
                    wx += slot * stackStep * 0.55f;
                    wy += slot * stackStep;
                }
                else
                {
                    if (!group.HasWorldPosition ||
                        !world.SurfaceGround.TryGet(group.SurfaceId, out _)) continue;
                    wx = group.WorldX;
                    wy = group.WorldY;
                }

                var p = hexMode ? hexProjection.ProjectWorld(wx, wy) : Project(mapRect, wx, wy);
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

                if (ArmyHexBattleAnchorService.TryResolveHexForSite(world, stack.SiteId, out var hex))
                {
                    HexMath.ToWorldPosition(hex, world.HexWorld.HexSize, out wx, out wy);
                    return true;
                }
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

            _countAtSiteKey.Clear();
            _slotAtSiteKey.Clear();
            foreach (var kv in world.Strategic.FormalArmies.Armies)
            {
                var army = kv.Value;
                if (army == null || !string.Equals(army.FactionId, playerFaction, StringComparison.Ordinal))
                    continue;
                if (!ArmyWorldMapPresentation.ShouldDrawFormalArmyPortrait(world, army))
                    continue;
                var armySiteKey = army.UsesHexStrategicPosition
                    ? army.CurrentHex.ToString()
                    : (ArmyService.TryResolveArmySiteId(world, army, out var faSite) ? faSite : string.Empty);
                var key = hexMode && army.UsesHexStrategicPosition
                    ? army.CurrentHex.ToString()
                    : armySiteKey;
                _countAtSiteKey.TryGetValue(key, out var c);
                _countAtSiteKey[key] = c + 1;
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

                if (string.Equals(_inspectedLegacySquadArmyId, army.ArmyId, StringComparison.Ordinal))
                {
                    FormalArmyStrategicMutationDiagnostics.RecordPresentation(army, wx, wy, true, true);
                }

                var leaderId = ArmyWorldMapPresentation.ResolvePortraitLeader(army);
                var basePos = hexMode && army.UsesHexStrategicPosition
                    ? ProjectHex(mapRect, world, wx, wy)
                    : Project(mapRect, wx, wy);
                var armySiteKey = army.UsesHexStrategicPosition
                    ? army.CurrentHex.ToString()
                    : (ArmyService.TryResolveArmySiteId(world, army, out var faSite) ? faSite : string.Empty);
                var key = hexMode && army.UsesHexStrategicPosition
                    ? army.CurrentHex.ToString()
                    : armySiteKey;
                _slotAtSiteKey.TryGetValue("fa:" + key, out var slot);
                _slotAtSiteKey["fa:" + key] = slot + 1;
                _countAtSiteKey.TryGetValue(key, out var total);
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
                var selected = string.Equals(_inspectedLegacySquadArmyId, army.ArmyId, StringComparison.Ordinal);
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

        bool TryHitFormalArmy(Vector2 mouse, out string armyId, float pad = 0f)
        {
            armyId = string.Empty;
            foreach (var kv in _formalArmyRects)
            {
                var rect = pad > 0f ? InflateRect(kv.Value, pad) : kv.Value;
                if (rect.Contains(mouse))
                {
                    armyId = kv.Key;
                    return true;
                }
            }

            return false;
        }

        static Rect InflateRect(Rect rect, float pad) =>
            new Rect(rect.x - pad, rect.y - pad, rect.width + pad * 2f, rect.height + pad * 2f);

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
                        _factionDiplomacyPanel.Close();
                        _characterListPanel.Open();
                        _globalStrategicToolbar.SetActive(moduleId);
                    }

                    break;
                case HostGlobalStrategicToolbar.ModuleId.Army:
                    // Legacy enum compatibility only; Army is not a product toolbar module.
                    _globalStrategicToolbar.CloseAll();
                    break;
                case HostGlobalStrategicToolbar.ModuleId.FactionDiplomacy:
                    if (_factionDiplomacyPanel.IsOpen)
                    {
                        _factionDiplomacyPanel.Close();
                        _globalStrategicToolbar.CloseAll();
                    }
                    else
                    {
                        _characterListPanel.Close();
                        _factionDiplomacyPanel.Open();
                        _globalStrategicToolbar.SetActive(moduleId);
                    }

                    break;
            }
        }

        void SyncFormalArmySelection(string armyId)
        {
            if (string.IsNullOrEmpty(armyId))
                return;
            _worldMapSelection.SelectPlayerParty();
            _inspectedLegacySquadArmyId = armyId;
            _selected.Clear();
            _selectedStackId = string.Empty;
            ClearResidualSelection();
            WorldMapArmyMarkerDiagnostics.LogMarkerSelectionVisual(
                armyId,
                _worldMapSelection.Kind,
                string.Empty,
                true);
        }

        void ClearFormalArmySelection()
        {
            _worldMapSelection.SelectPlayerParty();
            _inspectedLegacySquadArmyId = string.Empty;
            _hexPathPreview.Clear();
        }

        void ClearResidualSelection() => _selectedResidualGroup = null;

        string ResolvePlayerFactionId(XianXia.Core.Simulation.SimulationWorld world) =>
            HostStrategicRosterQueries.ResolvePlayerFactionId(world, bootstrap?.Session?.CharacterIds);

        void EnsureStrategicProductPanels()
        {
            if (_characterListPanel == null)
                _characterListPanel = new HostStrategicCharacterListPanel(_body, _title);
            if (_factionDiplomacyPanel == null)
                _factionDiplomacyPanel = new HostFactionDiplomacyOverviewPanel(_body, _title);
        }

        void DrawStrategicRosterPanels(XianXia.Core.Simulation.SimulationWorld world)
        {
            EnsureStrategicProductPanels();
            var partyCharacterIds = bootstrap.Session.CharacterIds;
            var partyRuntime = bootstrap.Session.PlayerParty;
            if (_characterListPanel.IsOpen)
            {
                var rect = HostStrategicRosterPanelLayout.Compute(Screen.width, Screen.height);
                _characterListPanel.Draw(
                    rect,
                    world,
                    partyCharacterIds,
                    partyRuntime,
                    EntityLabel,
                    FocusCameraOnArmy,
                    FocusCameraOnNode);
            }

            if (_factionDiplomacyPanel.IsOpen)
            {
                var rect = HostStrategicRosterPanelLayout.Compute(Screen.width, Screen.height);
                _factionDiplomacyPanel.Draw(rect, world);
            }

            _globalStrategicToolbar.SyncFromPanels(
                _characterListPanel != null && _characterListPanel.IsOpen,
                false,
                _factionDiplomacyPanel != null && _factionDiplomacyPanel.IsOpen);
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
            _worldMapSelection.SelectPlayerParty();
            _inspectedLegacySquadArmyId = armyId;
            ArmyService.TryResolveArmySiteId(world, army, out var inspectSiteId);
            _inspectSiteId = inspectSiteId ?? string.Empty;
            _status = "已定位 NPC 小队｜队长 " + EntityLabel(world, army.LeaderCharacterId) +
                      "｜" + army.MemberCharacterIds.Count + "人";
        }

        public void FocusCameraOnNode(string nodeId)
        {
            if (bootstrap?.Session?.World == null || string.IsNullOrEmpty(nodeId))
                return;
            var world = bootstrap.Session.World;
            if (!world.Strategic.Sites.TryGet(nodeId, out var site) || site == null)
                return;
            HexMath.ToWorldPosition(site.AnchorHex, world.HexWorld.HexSize, out var wx, out var wy);
            FocusCameraOnWorldPoint(wx, wy);
            _inspectSiteId = nodeId;
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

        string BuildFormalArmyInspect(
            XianXia.Core.Simulation.SimulationWorld world,
            FormalArmy army)
        {
            var sb = new StringBuilder(512);
            sb.Append("NPC 小队\n\n");
            sb.Append("队长：").Append(EntityLabel(world, army.LeaderCharacterId)).Append('\n');
            sb.Append("人数：").Append(army.MemberCharacterIds.Count).Append('\n');
            sb.Append("状态：").Append(army.State == FormalArmyState.Moving ? "途中" : "驻留").Append('\n');
            ArmyService.TryResolveArmySiteId(world, army, out var tooltipSiteId);
            sb.Append("Site?").Append(StrategicSiteAccessService.DescribeSite(world, tooltipSiteId)).Append('\n');

            var motion = army.WorldMotion;
            sb.Append("移动：").Append(motion.IsMoving ? "正在前往目的地" : "无").Append('\n');
            sb.Append("成员：\n");
            for (var i = 0; i < army.MemberCharacterIds.Count; i++)
            {
                var memberId = new EntityId(army.MemberCharacterIds[i]);
                sb.Append(" · ").Append(EntityLabel(world, memberId)).Append('\n');
            }

            if (army.State == FormalArmyState.Garrisoned)
                sb.Append("\n成员状态：\n");
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

            // 先统计同节点人数，便于紧贴居中排
            _countAtSiteKey.Clear();
            for (var i = 0; i < ids.Count; i++)
            {
                if (!world.WorldPresence.TryGet(ids[i], out var p) || p == null)
                    continue;
                if (p.Mode == PartyWorldPresenceMode.InEncounter)
                    continue;
                var key = hexMode ? ids[i].Value.ToString() : p.SiteId ?? "";
                _countAtSiteKey.TryGetValue(key, out var c);
                _countAtSiteKey[key] = c + 1;
            }

            _slotAtSiteKey.Clear();
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
                if (presence.Mode == PartyWorldPresenceMode.AtHex)
                {
                    _slotAtSiteKey.TryGetValue("t:" + id.Value, out var slot);
                    _slotAtSiteKey["t:" + id.Value] = slot + 1;
                    center = basePos + new Vector2((slot % 3) * (avatarSize * 0.55f) - avatarSize * 0.55f,
                        -avatarSize * 0.55f);
                    if (LingeringBattlefieldPartyService.IsLingeringDowned(world, id))
                        center += new Vector2(avatarSize * 0.35f, avatarSize * 0.7f);
                }
                else if (hexMode)
                {
                    _slotAtSiteKey.TryGetValue("h:" + id.Value, out var slot);
                    _slotAtSiteKey["h:" + id.Value] = slot + 1;
                    center = basePos + new Vector2((slot % 3) * (avatarSize * 0.45f), -avatarSize * 0.35f);
                }
                else
                {
                    var key = presence.SiteId ?? "";
                    _slotAtSiteKey.TryGetValue(key, out var slot);
                    _slotAtSiteKey[key] = slot + 1;
                    _countAtSiteKey.TryGetValue(key, out var total);
                    if (total < 1)
                        total = 1;
                    // 紧贴节点「头顶」外侧，按人数水平居
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
                var onRoute = presence.Mode == PartyWorldPresenceMode.AtHex;
                var inEncounter = presence.Mode == PartyWorldPresenceMode.InEncounter;
                var incap = LingeringBattlefieldPartyService.IsIncapacitated(world, id);
                var dead = world.Entities.TryGet(id, out var avatarEnt) &&
                           avatarEnt.TryGet<LifecycleComponent>(out var avatarLife) &&
                           avatarLife.IsDead;
                // 半透明，避免压住地名；我方弥留用蓝色，尸体用灰
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
                    GUI.Label(rect, "弥", _avatarLabel);
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
            XianXia.Core.Simulation.SimulationWorld world)
        {
            var e = Event.current;
            if (e != null && e.type == EventType.Used)
                return;
            if (e == null || e.type != EventType.MouseDown)
                return;

            var mouse = e.mousePosition;
            if (HostUiHitTest.ContainsCurrentGuiPoint(mouse))
            {
                WorldMapArmyMarkerDiagnostics.LogWorldMapPointerDispatch(
                    mouse,
                    overStrategicUi: true,
                    overArmyMarker: false,
                    overPlayerMarker: false,
                    handledBy: "UI",
                    mapInputExecuted: false);
                return;
            }

            if (!mapRect.Contains(mouse))
                return;
            if (e.button == 2)
                return;

            // —左键：只负责选中（永不弹攻击／进入指令菜单）—
            if (e.button == 0)
            {
                if (IsSurfaceMode(world))
                {
                    var point = BuildSurfaceProjection(mapRect).ScreenToWorld(mouse);
                    for (var i=0;i<_nodeRects.Count;i++) if (_nodeRects[i].rect.Contains(mouse)) { _selectedWorldSiteId=_nodeRects[i].nodeId; _inspectSiteId=_selectedWorldSiteId; _status="已选择地点："+_selectedWorldSiteId; e.Use(); return; }
                    var nav = world.SurfaceGround.Active;
                    _selectedHex = null; _hoverHex = null; _selectedWorldSiteId = string.Empty;
                    _status = nav.TryGetCell(point.x, point.y, out var kind)
                        ? "世界坐标：(" + point.x.ToString("0.000") + "," + point.y.ToString("0.000") + ")｜" + ((kind & SurfaceGroundCellKind.Water) != 0 ? "水域" : (kind & SurfaceGroundCellKind.Road) != 0 ? "道路" : "平原")
                        : "世界坐标：(" + point.x.ToString("0.000") + "," + point.y.ToString("0.000") + ")";
                    e.Use(); return;
                }
                if (TryHitFormalArmy(mouse, out var hitArmyId, FormalArmyMarkerHitPad))
                {
                    WorldMapArmyMarkerDiagnostics.LogWorldMapPointerDispatch(
                        mouse,
                        overStrategicUi: false,
                        overArmyMarker: true,
                        overPlayerMarker: false,
                        handledBy: "FormalArmyMarker",
                        mapInputExecuted: false);
                    var beforeKind = _worldMapSelection.DescribeKind();
                    var beforeId = bootstrap?.Session?.PlayerParty?.ActiveCharacterId.ToString() ?? string.Empty;
                    var now = Time.realtimeSinceStartupAsDouble;
                    if (string.Equals(_lastMapFormalArmyClickId, hitArmyId, StringComparison.Ordinal) &&
                        now - _lastMapFormalArmyClickTime <= 0.35)
                    {
                        _lastMapFormalArmyClickId = string.Empty;
                        FocusCameraOnArmy(hitArmyId);
                    }
                    else
                    {
                        _lastMapFormalArmyClickId = hitArmyId;
                        _lastMapFormalArmyClickTime = now;
                        SyncFormalArmySelection(hitArmyId);
                        if (world.Strategic.FormalArmies.TryGet(hitArmyId, out var viewedSquad) && viewedSquad != null)
                            _status = "已选中 NPC 小队｜队长 " + EntityLabel(world, viewedSquad.LeaderCharacterId) +
                                      "｜" + viewedSquad.MemberCharacterIds.Count + "人｜只读";
                    }

                    WorldMapArmyMarkerDiagnostics.LogWorldMapSelectionClick(
                        hitArmyId,
                        beforeKind,
                        beforeId,
                        _worldMapSelection.DescribeKind(),
                        string.Empty,
                        _worldMapSelection.DescribeKind(),
                        string.Empty,
                        Event.current?.GetHashCode() ?? 0);

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
                    _inspectSiteId = string.Empty;
                    // 纯 Inspect：marker 只是信息展示，不是 Encounter gateway。
                    _status = FormatResidualGroupTitle(residualGroup) + " ×" + residualGroup.Count +
                              "｜移动至该格后可在 LocalMap 查看现场";
                    e.Use();
                    return;
                }

                // 接战点活人／弥留叠在一起时，优先点中活
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
                            armyHint = "｜移动至该格后可在 LocalMap 查看现场";
                        else
                            armyHint = "｜敌方战场残留｜移动至该格可查看现场";

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
                        _status = EntityLabel(world, id) + "｜NPC 小队成员｜只读";
                    }
                    else
                    {
                        ClearFormalArmySelection();
                        _status = "已选中角色 " + EntityLabel(world, id);
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
                    // 标准 RTS：点敌军改选敌军，清空我方选中；不弹指令菜
                    _selected.Clear();
                    ClearFormalArmySelection();
                    _selectedStackId = hitStackId;
                    _inspectSiteId = string.Empty;
                    ClearResidualSelection();
                    _avatarMenuOpen = false;
                    if (world.Strategic.Armies.TryGet(hitStackId, out var stack) && stack != null)
                    {
                        _status = "已选中 NPC 小队｜" + DescribeStack(world, stack) + "｜只读";
                    }
                    else
                        _status = "已选中 NPC 小队｜只读";
                    e.Use();
                    return;
                }

                if (ArmyHexCommandService.IsHexStrategicActive(world) &&
                    TryHandleHexLeftClick(projection, world, mouse, e))
                {
                    WorldMapArmyMarkerDiagnostics.LogWorldMapPointerDispatch(
                        mouse,
                        overStrategicUi: false,
                        overArmyMarker: false,
                        overPlayerMarker: false,
                        handledBy: "Hex",
                        mapInputExecuted: true);
                    return;
                }

                if (!e.shift)
                {
                    _selected.Clear();
                    _selectedStackId = string.Empty;
                    ClearFormalArmySelection();
                    _inspectSiteId = string.Empty;
                    _avatarMenuOpen = false;
                    _status = "已取消选择";
                }

                e.Use();
                return;
            }

            if (e.button != 1)
                return;

            if (IsSurfaceMode(world))
            {
                var point=BuildSurfaceProjection(mapRect).ScreenToWorld(mouse);
                TryHandleSurfaceGroundCommand(point, world, mouse, e);
                return;
            }

            var hexStrategicActive = ArmyHexCommandService.IsHexStrategicActive(world);
            if (hexStrategicActive)
            {
                if (TryHandleSurfaceGroundCommand(projection.ScreenToWorld(mouse), world, mouse, e))
                    return;
                if (TryHandleHexMapCommand(projection, world, mouse, e))
                    return;
            }

            // —右键：只规划 PlayerParty 旅行（永不改选中集合）—
            // 弥留／尸体 avatar 已退役为纯 inspect（左键）；右键不再弹残留战场菜单。
            if (TryHitArmyStack(
                    world,
                    mouse,
                    out var menuStackId,
                    ArmyStackHitPad,
                    ArmyStackHitPadContested))
            {
                _status = world.Strategic.Armies.TryGet(menuStackId, out var viewedStack) && viewedStack != null
                    ? "NPC 小队｜" + DescribeStack(world, viewedStack) + "｜只读；请右键地面普通前往"
                    : "NPC 小队｜只读";
                e.Use();
                return;
            }

            _status = "请在地图上选择可通行目标";
            e.Use();
        }

        bool TryHandleSurfaceGroundCommand(
            Vector2 point,
            SimulationWorld world,
            Vector2 mouse,
            Event e)
        {
            if (_worldMapSelection.Kind != HostWorldMapSelectionKind.PlayerParty) return false;
            var nav = world?.SurfaceGround?.Active;
            if (nav == null) return false;
            if (!nav.Contains(point.x, point.y)) return false;
            var goal = new WorldVec2(point.x, point.y);
            if (!nav.IsWalkable(goal.X, goal.Y))
            {
                _status = "不可步行到达的地面目标";
                e.Use();
                return true;
            }
            var party = bootstrap?.Session?.PlayerParty;
            if (party == null || !party.HasActive) return false;
            PlayerPartyHexPursuitService.CancelPursuit(world, party);
            var move = PlayerPartySurfaceTravelService.BeginTravel(
                world, party, goal, string.Empty, nav.CellSize * .75f);
            if (move.IsFailure)
            {
                _status = FormatFail(move);
                e.Use();
                return true;
            }
            PlayerPartyHexTravelService.HoldForLocalVisibleExecution(world);
            RefreshPlayerPartyPathPreview(world);
            _status = "已规划前往精确地面点 (" + goal.X.ToString("0.00") + "," +
                      goal.Y.ToString("0.00") + ")｜关闭大地图后出发";
            e.Use();
            return true;
        }

        bool TryHandleHexLeftClick(
            HexMapViewportProjection projection,
            XianXia.Core.Simulation.SimulationWorld world,
            Vector2 mouse,
            Event e)
        {
            if (!HexMapMousePick.TryResolveMouseHex(projection, world.HexWorld, mouse, out var pickedHex))
                return false;

            if (TryHitFormalArmy(mouse, out _, FormalArmyMarkerHitPad))
            {
                e.Use();
                return true;
            }

            _selectedHex = pickedHex;
            _hoverHex = pickedHex;
            ClearResidualSelection();
            var previousSelectedSiteId = _selectedWorldSiteId;

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
                ClearFormalArmySelection();
                _inspectSiteId = string.Empty;
                _selectedWorldSiteId = string.Empty;
            }

            var siteIdsAtHex = world.Strategic.Sites.GetSiteIdsAtHex(pickedHex);
            if (siteIdsAtHex.Count > 0)
            {
                var selectedIndex = 0;
                for (var i = 0; i < siteIdsAtHex.Count; i++)
                    if (string.Equals(siteIdsAtHex[i], previousSelectedSiteId, StringComparison.Ordinal))
                    {
                        selectedIndex = (i + 1) % siteIdsAtHex.Count;
                        break;
                    }
                _selectedWorldSiteId = siteIdsAtHex[selectedIndex];
            }
            else _selectedWorldSiteId = string.Empty;

            if (!world.HexWorld.TryGetTile(pickedHex, out var inspectTile) || inspectTile == null)
            {
                _status = "Hex " + pickedHex + "（无地块数据）";
                e.Use();
                return true;
            }

            var label = pickedHex.ToString();
            if (!string.IsNullOrEmpty(_selectedWorldSiteId) &&
                world.Strategic.Sites.TryGet(_selectedWorldSiteId, out var site) && site != null)
                label = string.IsNullOrEmpty(site.DisplayName) ? site.SiteId : site.DisplayName;

            _status = "Hex " + pickedHex + "｜" + label + "｜" + HexTerrainPresentation.GetDisplayName(inspectTile) +
                      (inspectTile.IsRoad ? "｜道路" : string.Empty) +
                      (inspectTile.IsPassable ? "｜可通行" : "｜不可通行");
            if (BattleOfferService.HasLingeringBattlefield(world) &&
                LingeringBattlefieldQueryService.TryGetLingeringBattlefieldAtHex(world, pickedHex, out _))
            {
                _status += "｜有战场残留｜移动至本格可在 LocalMap 查看";
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
            _lastContextMousePos = mouse;
            if (!HexMapMousePick.TryResolveMouseHex(projection, world.HexWorld, mouse, out var pickedHex))
            {
                _status = "无法解析倒下角色位置";
                e.Use();
                return true;
            }

            _selectedHex = pickedHex;
            if (!world.HexWorld.TryGetTile(pickedHex, out var tile) || tile == null || !tile.IsPassable)
            {
                _status = "无法解析倒下角色位置";
                e.Use();
                return true;
            }

            _worldMapSelection.SelectPlayerParty();
            var resolution = HexRightClickResolver.ResolvePlayerTravel(
                world,
                pickedHex,
                ResolvePlayerFactionId(world),
                true);

            _avatarMenuOpen = false;
            _nodeMenuOpen = false;

            // Product input has a single command outcome: ordinary PlayerParty travel.
            // Legacy action enum values cannot reach this dispatcher.
            DispatchHexRightClickTravel(world, pickedHex, resolution.StatusHint);

            e.Use();
            return true;
        }

        void DispatchHexRightClickTravel(
            XianXia.Core.Simulation.SimulationWorld world,
            HexCoord hex,
            string statusHint)
        {
            if (TryExecutePlayerPartyTravel(world, hex, out var partyStatus))
            {
                _status = partyStatus;
                return;
            }

            _status = string.IsNullOrEmpty(statusHint)
                ? "右键规划玩家小队前往"
                : statusHint;
        }

        bool TryExecutePlayerPartyTravel(
            XianXia.Core.Simulation.SimulationWorld world,
            HexCoord hex,
            out string status)
        {
            status = string.Empty;
            if (_worldMapSelection.Kind != HostWorldMapSelectionKind.PlayerParty)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                UnityEngine.Debug.LogWarning(
                    "[WorldMapSelection] Blocked PlayerParty travel: authority Kind=" +
                    _worldMapSelection.Kind +
                    " InspectedLegacySquadId=" + (_inspectedLegacySquadArmyId ?? string.Empty));
#endif
                return false;
            }

            var party = bootstrap?.Session?.PlayerParty;
            if (party == null || !party.HasActive)
            {
                return false;
            }

            if (!world.HexWorld.TryGetTile(hex, out var tile) || tile == null || !tile.IsPassable)
            {
                status = "目标 Hex 不可通行";
                return true;
            }

            if (!WorldMapPartyTravelCommand.TryResolve(world, hex, out var cmd))
            {
                status = "无法解析旅行目标";
                return true;
            }

            // Phase 5S-B2-3.5：新的普通 Move order 代表玩家明确意图 → 取消既有 Attack pursuit，
            // 否则 pursuit tick 下一帧会把路线抢回 Enemy。
            PlayerPartyHexPursuitService.CancelPursuit(world, party);

            Result move;
            var continuous = bootstrap?.ContinuousOutdoorSurfaceRuntime;
            if (IsSurfaceMode(world) &&
                world.Strategic.Sites.TryGet(cmd.TargetSiteId, out var surfaceSite) &&
                WorldSiteOutdoorMigrationPolicy.UsesContinuousOutdoorSurface(surfaceSite))
            {
                if (string.IsNullOrEmpty(cmd.TargetSiteId) ||
                    !world.SurfaceGround.TryResolveSiteArrival(cmd.TargetSiteId, out _, out var surfaceGoal))
                {
                    status = "目标缺少正式 Surface 到达点";
                    return true;
                }
                move = PlayerPartySurfaceTravelService.BeginTravel(
                    world, party, surfaceGoal, cmd.TargetSiteId,
                    world.SurfaceGround.Active.CellSize * .75f);
            }
            else if (continuous != null && continuous.IsActive)
            {
                if (!continuous.TryResolveContinuousAutoTravelGoal(
                        cmd.DestinationHex, cmd.TargetSiteId ?? string.Empty,
                        out var physicalGoal, out var arrivalRadius, out var goalFailure))
                {
                    status = goalFailure;
                    return true;
                }
                move = PlayerPartyHexTravelService.BeginContinuousSurfaceTravel(
                    world, party, cmd.DestinationHex, cmd.TargetSiteId ?? string.Empty,
                    physicalGoal, arrivalRadius);
            }
            else
            {
                move = PlayerPartyHexTravelService.BeginTravel(
                    world, party, cmd.DestinationHex, cmd.TargetSiteId ?? string.Empty);
            }
            if (move.IsFailure)
            {
                status = FormatFail(move);
                return true;
            }

            PlayerPartyHexTravelService.HoldForLocalVisibleExecution(world);
            _worldMapSelection.SelectPlayerParty();
            RefreshPlayerPartyPathPreview(world);
            var destLabel = cmd.TargetHex.ToString();
            if (!string.IsNullOrEmpty(cmd.TargetSiteId) &&
                world.Strategic.Sites.TryGet(cmd.TargetSiteId, out var site) &&
                site != null)
                destLabel = string.IsNullOrEmpty(site.DisplayName) ? site.SiteId : site.DisplayName;
            status = "已规划前往 " + destLabel + "｜关闭大地图后出发";

            return true;
        }

        void DrawPlayerPartyMarker(
            Rect mapRect,
            XianXia.Core.Simulation.SimulationWorld world,
            HexMapViewportProjection projection)
        {
            var party = bootstrap?.Session?.PlayerParty;
            if (party == null || !party.HasActive)
                return;
            if (!PlayerPartyWorldLocationQuery.TryResolve(world, party, out var resolved))
                return;

            var wx = resolved.WorldPosition.X;
            var wy = resolved.WorldPosition.Y;
            var screen = ProjectHex(mapRect, world, wx, wy);
            const float size = 24f;
            var rect = new Rect(screen.x - size * 0.5f, screen.y - size * 0.5f, size, size);
            if (!rect.Overlaps(mapRect))
                return;

            var old = GUI.color;
            GUI.color = new Color(0.95f, 0.72f, 0.22f, 0.92f);
            GUI.DrawTexture(rect, _px);
            GUI.color = Color.white;
            var shortName = EntityLabel(world, party.ActiveCharacterId);
            if (shortName.Length > 2)
                shortName = shortName.Substring(0, 2);
            GUI.Label(rect, shortName, _avatarLabel);
            GUI.color = old;
        }

        void CloseGatewayConfirm(string reason = null)
        {
            if (!_gatewayConfirmOpen)
                return; // 未打开不记录：只在状态改变时打印
            _gatewayConfirmOpen = false;
            _gatewayConfirmSiteId = string.Empty;
            _gatewayConfirmDisplayName = string.Empty;
            _gatewayConfirmApproachHex = default;
        }

        void DrawGatewayConfirm(XianXia.Core.Simulation.SimulationWorld world)
        {
            if (!_gatewayConfirmOpen || string.IsNullOrEmpty(_gatewayConfirmSiteId))
                return;
            if (bootstrap?.Session?.PlayerParty == null || !bootstrap.Session.PlayerParty.HasActive)
            {
                CloseGatewayConfirm();
                return;
            }

            var prevDepth = GUI.depth;
            GUI.depth = -86;
            HostUiHitTest.Block(_gatewayConfirmRect);
            var prevColor = GUI.color;
            GUI.color = new Color(0.16f, 0.17f, 0.19f, 0.97f);
            GUI.DrawTexture(_gatewayConfirmRect, _px);
            GUI.color = prevColor;

            GUI.Label(
                new Rect(_gatewayConfirmRect.x + 10f, _gatewayConfirmRect.y + 6f, _gatewayConfirmRect.width - 20f, 18f),
                "无法直接前往目标地点",
                _body);
            GUI.Label(
                new Rect(_gatewayConfirmRect.x + 10f, _gatewayConfirmRect.y + 26f, _gatewayConfirmRect.width - 20f, 18f),
                "需要先经过【" + _gatewayConfirmDisplayName + "】",
                _body);

            var y = _gatewayConfirmRect.y + 50f;
            var bw = (_gatewayConfirmRect.width - 24f) * 0.5f;
            if (GUI.Button(new Rect(_gatewayConfirmRect.x + 10f, y, bw, 24f), "前往 " + _gatewayConfirmDisplayName))
            {
                Event.current.Use();
                ExecuteGatewayConfirmTravel(world);
                CloseGatewayConfirm("confirmClicked");
            }

            if (GUI.Button(new Rect(_gatewayConfirmRect.x + 14f + bw, y, bw, 24f), "取消"))
            {
                Event.current.Use();
                CloseGatewayConfirm("cancelClicked");
            }

            GUI.depth = prevDepth;
        }

        void ExecuteGatewayConfirmTravel(XianXia.Core.Simulation.SimulationWorld world)
        {
            var party = bootstrap?.Session?.PlayerParty;
            if (party == null || !party.HasActive)
                return;

            // 复用稳定 Direct Target WorldSite Ingress：普通 PlayerParty → Gateway 旅行
            //（到达 Gateway = AtSite + Travel Complete；不保留 B continuation，不自动出关）。
            // Phase 5S-B2-3.5：新的 Gateway 旅行也是新 Move order → 取消既有 Attack pursuit。
            PlayerPartyHexPursuitService.CancelPursuit(world, party);
            Result move;
            var continuous = bootstrap?.ContinuousOutdoorSurfaceRuntime;
            if (IsSurfaceMode(world) &&
                world.Strategic.Sites.TryGet(_gatewayConfirmSiteId, out var gatewaySite) &&
                WorldSiteOutdoorMigrationPolicy.UsesContinuousOutdoorSurface(gatewaySite))
            {
                if (!world.SurfaceGround.TryResolveSiteArrival(
                        _gatewayConfirmSiteId, out _, out var surfaceGoal))
                {
                    _status = "Gateway 缺少正式 Surface 到达点";
                    return;
                }
                move = PlayerPartySurfaceTravelService.BeginTravel(
                    world, party, surfaceGoal, _gatewayConfirmSiteId,
                    world.SurfaceGround.Active.CellSize * .75f);
            }
            else if (continuous != null && continuous.IsActive)
            {
                if (!continuous.TryResolveContinuousAutoTravelGoal(
                        _gatewayConfirmApproachHex, _gatewayConfirmSiteId,
                        out var physicalGoal, out var arrivalRadius, out var goalFailure))
                {
                    _status = goalFailure;
                    return;
                }
                move = PlayerPartyHexTravelService.BeginContinuousSurfaceTravel(
                    world, party, _gatewayConfirmApproachHex, _gatewayConfirmSiteId,
                    physicalGoal, arrivalRadius);
            }
            else
            {
                move = PlayerPartyHexTravelService.BeginTravel(
                    world, party, _gatewayConfirmApproachHex, _gatewayConfirmSiteId);
            }
            if (move.IsSuccess)
            {
                PlayerPartyHexTravelService.HoldForLocalVisibleExecution(world);
                _worldMapSelection.SelectPlayerParty();
                RefreshPlayerPartyPathPreview(world);
                _status = "已规划前往 " + _gatewayConfirmDisplayName + "｜关闭大地图后出发";
            }
            else
            {
                _status = FormatFail(move);
            }
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
                    // 弥留／尸体同档；叠人时仍让位给活
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

        string FormatSelectionSummary()
        {
            if (!string.IsNullOrEmpty(_inspectedLegacySquadArmyId))
                return "NPC 小队（只读）";
            return _selected.Count.ToString();
        }

        static string DescribeStack(XianXia.Core.Simulation.SimulationWorld world, ArmyStack stack)
        {
            if (stack == null)
                return string.Empty;
            var faction = StrategicFactionCatalog.DisplayName(stack.FactionId);
            var name = string.IsNullOrEmpty(stack.DisplayName) ? stack.Id : stack.DisplayName;
            var power = CombatPowerCalculator.ForArmyStack(world, stack);
            ArmyStackAdapter.TryGetFormalArmy(world, stack, out var labelArmy);
            var where = labelArmy != null && labelArmy.State == FormalArmyState.Moving ? "途中" : stack.SiteId;
            return name + " · " + faction + " · " + stack.MemberCount + "人 · 战力" + power +
                   " · " + where;
        }

        void TryDismissContextMenusOnOutsideClick()
        {
            var ev = Event.current;
            if (ev == null || ev.type != EventType.MouseDown)
                return;
            if (ev.button != 0 && ev.button != 1)
                return;

            // Phase 5D-B1（UI 生命周期）：触发打开确认框的那次右键事件，
            // 不得在同一帧被 outside-click 判断关闭刚打开的确认框（openedThisFrame 模式，
            // 非 magic delay）。打开帧内跳过全部菜单关闭；下一帧起恢复正常 dismiss。
            if (_gatewayConfirmOpen && Time.frameCount == _gatewayConfirmOpenFrame)
            {
                return;
            }

            if (_nodeMenuOpen && _nodeMenuRect.Contains(ev.mousePosition))
                return;
            if (_avatarMenuOpen && _avatarMenuRect.Contains(ev.mousePosition))
                return;
            if (_gatewayConfirmOpen && _gatewayConfirmRect.Contains(ev.mousePosition))
                return;

            if (!_nodeMenuOpen && !_avatarMenuOpen && !_gatewayConfirmOpen)
                return;

            _nodeMenuOpen = false;
            CloseGatewayConfirm("outsideClick");
            _avatarMenuOpen = false;
            // Use：同一帧右键仍可落到普通 PlayerParty 旅行。
        }

        /// <summary>
        /// Retired：弥留／尸体 avatar 菜单已退役 —— 残留战场不再是 Encounter gateway。
        /// 现在只保留 inspect 信息（姓名 / life state / countdown / 提示移动至该 Hex 查看）。
        /// 绝不 Build BattleOffer、SetPendingLingeringVisit、自动派军或进入 Encounter。
        /// </summary>
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
                return;
            }

            var downTag = LingeringBattlefieldPartyService.IsVisibleCorpse(world, target)
                ? "尸体"
                : "弥留";
            if (world.Entities.TryGet(target, out var menuEnt) && menuEnt != null)
            {
                var stamped = CombatLifeStateService.FormatLifeStateWithCountdown(world, menuEnt);
                if (!string.IsNullOrEmpty(stamped))
                    downTag = stamped;
            }

            _status = EntityLabel(world, target) + " · " + downTag +
                      "｜移动至该格后可在 LocalMap 查看现场";
            _avatarMenuOpen = false;
        }

        void OpenIncapAvatarMenu(
            XianXia.Core.Simulation.SimulationWorld world,
            EntityId hitId,
            Vector2 mouse)
        {
            if (!LingeringBattlefieldPartyService.IsFriendlyLingeringDowned(world, hitId))
                return;

            _avatarMenuEntityId = hitId.Value;
            CloseGatewayConfirm();
            _avatarMenuOpen = true;
            _avatarMenuRect = new Rect(mouse.x + 4f, mouse.y + 4f, 196f, 118f);

            var tag = LingeringBattlefieldPartyService.IsVisibleCorpse(world, hitId)
                ? "尸体"
                : "弥留";
            _status = EntityLabel(world, hitId) + "（" + tag + "）｜移动至该格后可查看现场";
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

        /// <summary>进入场景时关掉大地图（本实例＋bootstrap 引用）/summary>
        void CloseAllWorldMapPanels()
        {
            Close();
            if (bootstrap != null && bootstrap.WorldMapPanel != null && bootstrap.WorldMapPanel != this)
                bootstrap.WorldMapPanel.Close();
        }

        void DrawInspectPanel(
            Rect panelRect,
            XianXia.Core.Simulation.SimulationWorld world)
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

            var body = BuildInspectBody(world);
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
            XianXia.Core.Simulation.SimulationWorld world)
        {
            if (_selectedResidualGroup != null)
                return BuildResidualInspect(world, _selectedResidualGroup);
            if (!string.IsNullOrEmpty(_inspectedLegacySquadArmyId) &&
                world.Strategic?.FormalArmies != null &&
                world.Strategic.FormalArmies.TryGet(_inspectedLegacySquadArmyId, out var formalArmy) &&
                formalArmy != null)
                return BuildFormalArmyInspect(world, formalArmy);
            if (_selectedHex.HasValue && ArmyHexCommandService.IsHexStrategicActive(world))
            {
                if (!string.IsNullOrEmpty(_selectedWorldSiteId) &&
                    world.Strategic.Sites.TryGet(_selectedWorldSiteId, out var selectedSite) &&
                    selectedSite != null)
                    return BuildWorldSiteSelectionInspect(world, selectedSite, _selectedHex.Value);
                return BuildHexInspect(world, _selectedHex.Value);
            }
            if (_selected.Count > 0)
                return BuildSelectedAgentsInspect(world);
            if (!string.IsNullOrEmpty(_selectedStackId) &&
                world.Strategic?.Armies != null &&
                world.Strategic.Armies.TryGet(_selectedStackId, out var stack) &&
                stack != null)
                return BuildStackInspect(world, stack);
            if (!string.IsNullOrEmpty(_inspectSiteId) &&
                world.Strategic.Sites.TryGet(_inspectSiteId, out var inspectSite) &&
                inspectSite != null)
                return BuildSiteInspect(world, inspectSite);

            return "左键点选 Hex、角色、NPC 小队或残留标记，在此查看详情。\n\n" +
                   "· Hex：地形／道路／地点\n" +
                   "· 我方：境界／生命／弥留·尸体倒计时\n" +
                   "· 残留：弥留／阵亡聚合名单（含倒计时）\n" +
                   "· NPC 小队：势力／人数／战力／成员倒计时（只读）\n" +
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
            sb.Append('\u3010').Append(FormatResidualGroupTitle(group)).Append("\u3011\n");
            sb.Append("地图格：").Append(group.Hex).Append('\n');
            var siteName = ResolveHexSiteName(world, group.Hex);
            if (!string.IsNullOrEmpty(siteName))
                sb.Append("地点：").Append(siteName).Append('\n');
            sb.Append("数量：").Append(group.Count).Append("\n\n");
            sb.Append("角色：\n");
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

        WorldSite ResolveSelectedWorldSite(XianXia.Core.Simulation.SimulationWorld world)
        {
            if (string.IsNullOrEmpty(_selectedWorldSiteId) || world?.Strategic?.Sites == null)
                return null;
            return world.Strategic.Sites.TryGet(_selectedWorldSiteId, out var site) ? site : null;
        }

        string BuildWorldSiteSelectionInspect(
            XianXia.Core.Simulation.SimulationWorld world,
            WorldSite site,
            HexCoord clickedHex)
        {
            var sb = new StringBuilder(640);
            var siteName = string.IsNullOrEmpty(site.DisplayName) ? site.SiteId : site.DisplayName;
            var footprintCount = WorldSiteFootprintValidator.CountFootprintHexes(site);
            sb.Append("地点：").Append(siteName).Append('\n');
            sb.Append("地点 ID：").Append(FormatOptional(site.SiteId)).Append("\n\n");
            sb.Append("锚点格：").Append(site.AnchorHex).Append('\n');
            sb.Append("地点占地：").Append(footprintCount).Append(" 格\n");
            var outsideCount = WorldSiteFootprintExitConnectionResolver.CountUniqueTraversableOutsideNeighbors(
                world, site);
            sb.Append("可用出口：").Append(outsideCount).Append("\n\n");
            sb.Append("占地范围：\n");
            foreach (var hex in site.EnumerateFootprintHexes())
                sb.Append(hex).Append('\n');
            sb.Append('\n');
            sb.Append("本地地图 ID：").Append(FormatOptional(site.LocalMapId)).Append('\n');
            sb.Append("领地区域 ID：").Append(FormatOptional(site.TerritoryRegionId)).Append('\n');
            if (!string.IsNullOrEmpty(site.TerritoryRegionId) &&
                world?.Strategic?.TerritoryRegions != null &&
                world.Strategic.TerritoryRegions.TryGet(site.TerritoryRegionId, out var region) &&
                region != null)
            {
                sb.Append("控制势力：").Append(FormatFactionDisplay(region.ControlFactionId)).Append('\n');
                sb.Append("领地范围：").Append(region.HexCount).Append(" 格\n");
            }
            else
            {
                sb.Append("控制势力：无\n");
                sb.Append("领地范围：无\n");
            }

            sb.Append("所属势力 ID：").Append(FormatOptional(site.OwnerFactionId)).Append('\n');
            if (!string.IsNullOrEmpty(site.CoreAssetId))
            {
                sb.Append("核心 ID：").Append(site.CoreAssetId).Append('\n');
                sb.Append("核心状态：").Append(site.IsCoreActive ? "有效" : "失效").Append('\n');
                sb.Append("等级：").Append(site.CoreLevel).Append('\n');
                sb.Append("Surface：").Append(FormatOptional(site.CoreSurfaceId)).Append('\n');
                if (site.HasCoreWorldPosition)
                    sb.Append("核心世界坐标：(").Append(site.CoreWorldX.ToString("0.###"))
                        .Append(", ").Append(site.CoreWorldY.ToString("0.###")).Append(")\n");
                CoreLevelControlRange configuredRange = null;
                try { configuredRange = world.Strategic.SpatialRules?.RequireLevel(site.CoreLevel); }
                catch (InvalidOperationException) { }
                if (configuredRange != null)
                    sb.Append("理论范围：").Append(configuredRange.WidthCells.ToString("0.#"))
                        .Append(" × ").Append(configuredRange.HeightCells.ToString("0.#")).Append(" cells\n");
                sb.Append("理论范围 Surface 解析：").Append(site.CoreRangeWidth.ToString("0.###"))
                    .Append(" × ").Append(site.CoreRangeHeight.ToString("0.###")).Append(" world");
                var surfaces = bootstrap?.Session?.Registry?.OutdoorSurfaces;
                if (surfaces != null)
                    foreach (var pair in surfaces)
                    {
                        var surface = pair.Value;
                        if (surface == null || !string.Equals(surface.SurfaceId, site.CoreSurfaceId, StringComparison.Ordinal) ||
                            !(surface.ChunkWidth > 0f) || !(surface.ChunkHeight > 0f)) continue;
                        sb.Append(" ≈ ").Append((site.CoreRangeWidth / surface.ChunkWidth).ToString("0.#"))
                            .Append(" × ").Append((site.CoreRangeHeight / surface.ChunkHeight).ToString("0.#"))
                            .Append(" chunks");
                        break;
                    }
                sb.Append('\n');
                var actualOverlays = WorldSiteActualControlOverlayBuilder.Build(world);
                WorldSiteActualControlOverlay actualOverlay = null;
                for (var i = 0; i < actualOverlays.Count; i++)
                    if (string.Equals(actualOverlays[i].SiteId, site.SiteId, StringComparison.Ordinal) &&
                        string.Equals(actualOverlays[i].SurfaceId, site.CoreSurfaceId, StringComparison.Ordinal))
                    {
                        actualOverlay = actualOverlays[i];
                        break;
                    }
                if (actualOverlay == null)
                    sb.Append("实际控制几何：0 pieces\n");
                else
                    sb.Append("实际控制几何：").Append(actualOverlay.Pieces.Count).Append(" pieces，bounds=(")
                        .Append(actualOverlay.MinX.ToString("0.###")).Append(", ")
                        .Append(actualOverlay.MinY.ToString("0.###")).Append(")..(")
                        .Append(actualOverlay.MaxX.ToString("0.###")).Append(", ")
                        .Append(actualOverlay.MaxY.ToString("0.###")).Append(") world\n");
                var claimCount = 0;
                foreach (var claim in world.Strategic.TerritoryClaims.EnumerateForSite(site.SiteId))
                {
                    if (claimCount++ == 0) sb.Append("取得历史：\n");
                    sb.Append("  #").Append(claim.AcquiredOrder).Append(' ')
                        .Append(claim.ClaimId).Append(" @(")
                        .Append(claim.CenterX.ToString("0.###")).Append(", ")
                        .Append(claim.CenterY.ToString("0.###")).Append(") ")
                        .Append(claim.Width.ToString("0.#")).Append(" × ")
                        .Append(claim.Height.ToString("0.#")).Append(" world\n");
                }
                sb.Append("Claim 数：").Append(claimCount).Append('\n');
                var effectiveHexCount = 0;
                foreach (var source in world.Strategic.TerritoryRegions.Regions)
                    if (source.Value != null &&
                        string.Equals(source.Value.PrimaryWorldSiteId, site.SiteId, StringComparison.Ordinal))
                        effectiveHexCount += source.Value.HexCount;
                sb.Append("有效战略投影：").Append(effectiveHexCount).Append(" 格\n");
            }
            sb.Append("当前格：").Append(clickedHex).Append('\n');
            HexMath.ToWorldPosition(clickedHex, world.HexWorld.HexSize, out var inspectX, out var inspectY);
            sb.Append("检查位置：Surface=").Append(FormatOptional(site.CoreSurfaceId))
                .Append(" World=(").Append(inspectX.ToString("0.###")).Append(", ")
                .Append(inspectY.ToString("0.###")).Append(")\n");
            sb.Append("理论覆盖 Site：");
            var theoreticalCount = 0;
            foreach (var pair in world.Strategic.Sites.Sites)
                if (WorldSiteCoreCoverageResolver.Contains(
                        pair.Value, site.CoreSurfaceId, inspectX, inspectY))
                {
                    if (theoreticalCount++ > 0) sb.Append(", ");
                    sb.Append(pair.Value.SiteId);
                }
            if (theoreticalCount == 0) sb.Append("无");
            sb.Append('\n');
            if (WorldSiteAdministrativeControlResolver.TryResolve(
                    world, site.CoreSurfaceId, inspectX, inspectY,
                    out var actualSite, out var actualClaim))
                sb.Append("实际管理：Site=").Append(actualSite.SiteId)
                    .Append(" Owner=").Append(FormatOptional(actualSite.OwnerFactionId))
                    .Append(" Claim=").Append(actualClaim.ClaimId)
                    .Append(" Order=").Append(actualClaim.AcquiredOrder)
                    .Append(" Core=").Append(actualSite.IsCoreActive ? "Active" : "Inactive")
                    .Append(" L").Append(actualSite.CoreLevel).Append('\n');
            else sb.Append("实际管理：无\n");
            return sb.ToString();
        }

        static string FormatOptional(string value)
        {
            return string.IsNullOrEmpty(value) ? "无" : value;
        }

        static string FormatFactionDisplay(string factionId)
        {
            if (string.IsNullOrEmpty(factionId))
                return "无";

            var displayName = StrategicFactionCatalog.DisplayName(factionId);
            var separator = factionId.LastIndexOf(':');
            var fallbackName = separator >= 0 ? factionId.Substring(separator + 1) : factionId;
            return string.IsNullOrEmpty(displayName) ||
                   string.Equals(displayName, factionId, StringComparison.Ordinal) ||
                   string.Equals(displayName, fallbackName, StringComparison.Ordinal)
                ? factionId
                : displayName + "（" + factionId + "）";
        }

        static string FormatWorldSiteType(string siteType)
        {
            switch (siteType)
            {
                case "Village": return "村落";
                case "Town": return "城镇";
                case "Mine": return "矿区";
                case "Forest": return "林地";
                case "Pass": return "关隘";
                case "Sect": return "宗门";
                case "Ferry": return "渡口";
                default: return FormatOptional(siteType);
            }
        }

        void AppendTerritoryInspect(
            System.Text.StringBuilder sb,
            XianXia.Core.Simulation.SimulationWorld world,
            HexCoord hex,
            XianXia.Core.World.Hex.HexCell tile)
        {
            var controller = tile?.ControlFactionId ?? string.Empty;
            sb.Append("控制势力：").Append(FormatFactionDisplay(controller)).Append('\n');
            if (world?.Strategic?.TerritoryRegions == null)
                return;

            // O(1) hex → Region（Board 索引）；不扫全表。
            if (world.Strategic.TerritoryRegions.TryGetAtHex(hex, out var region) && region != null)
            {
                sb.Append("领地区域：").Append(FormatOptional(region.RegionId)).Append('\n');
                if (!string.IsNullOrEmpty(region.PrimaryWorldSiteId) &&
                    world.Strategic.Sites.TryGet(region.PrimaryWorldSiteId, out var site) &&
                    site != null)
                    sb.Append("领地中心地点：").Append(site.SiteId).Append('\n');
            }
        }

        string BuildHexInspect(XianXia.Core.Simulation.SimulationWorld world, HexCoord hex)
        {
            var sb = new StringBuilder(320);
            sb.Append("地图格：").Append(hex).Append('\n');
            if (!world.HexWorld.TryGetTile(hex, out var tile) || tile == null)
            {
                    sb.Append("\n成员状态：\n");
                return sb.ToString();
            }

            sb.Append("地形：").Append(HexTerrainPresentation.GetDisplayName(tile)).Append('\n');
            sb.Append("移动代价：").Append(tile.ResolveMovementCost().ToString("0.##")).Append('\n');
            sb.Append("道路：").Append(tile.IsRoad ? "是" : "否").Append('\n');
            sb.Append("可通行：").Append(tile.IsPassable ? "是" : "否").Append('\n');
            AppendTerritoryInspect(sb, world, hex, tile);

            if (world.Strategic.Sites.TryGetAtHex(hex, out var site) && site != null)
            {
                sb.Append('\n');
                sb.Append("地点：").Append(string.IsNullOrEmpty(site.DisplayName) ? site.SiteId : site.DisplayName).Append('\n');
                if (!string.IsNullOrEmpty(site.SiteType))
                    sb.Append("类型：").Append(FormatWorldSiteType(site.SiteType)).Append('\n');
                sb.Append("所属势力：").Append(FormatFactionDisplay(site.OwnerFactionId)).Append('\n');
                sb.Append("本地地图 ID：").Append(FormatOptional(site.LocalMapId)).Append('\n');
            }

            return sb.ToString();
        }

        string BuildSelectedAgentsInspect(
            XianXia.Core.Simulation.SimulationWorld world)
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

                AppendAgentInspect(sb, world, new EntityId(idVal));
                n++;
            }

            return sb.ToString();
        }

        void AppendAgentInspect(
            StringBuilder sb,
            XianXia.Core.Simulation.SimulationWorld world,
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

            sb.Append("位置：").Append(FormatPresenceLocation(world, presence)).Append('\n');
            sb.Append("行动：").Append(FormatPresenceAction(presence)).Append('\n');
        }

        static string FormatPresenceLocation(SimulationWorld world, WorldAgentPresence p)
        {
            if (p.UsesHexPresence)
                return p.ResidualHex.ToString();
            if (p.Mode == PartyWorldPresenceMode.AtSite && !string.IsNullOrEmpty(p.SiteId))
            {
                var siteName = StrategicSiteAccessService.DescribeSite(world, p.SiteId);
                if (world.Strategic.Sites.TryResolveSitePresenceHex(p.SiteId, out var presenceHex))
                    return siteName + " · WorldHex " + presenceHex;
                return siteName;
            }
            if (!string.IsNullOrEmpty(p.SiteId))
                return StrategicSiteAccessService.DescribeSite(world, p.SiteId);
            return "unknown";
        }

        static string FormatPresenceAction(WorldAgentPresence p)
        {
            if (p == null)
            return "驻留";
            if (p.Mode == PartyWorldPresenceMode.InEncounter)
            return "驻留";
            if (p.IsCombatPursuing)
                return "追击增援";
            if (p.Mode == PartyWorldPresenceMode.AtSite)
            return "驻留";
            if (p.Mode == PartyWorldPresenceMode.AtSite)
                return "路中驻留";
            return "驻留";
        }

        static string ResolveNodeName(SimulationWorld world, string nodeId) =>
            StrategicSiteAccessService.DescribeSite(world, nodeId);

        string BuildStackInspect(
            XianXia.Core.Simulation.SimulationWorld world,
            ArmyStack stack)
        {
            var sb = new StringBuilder(320);
            sb.Append("NPC 小队\n\n");
                sb.Append("名称：")
                .Append(string.IsNullOrEmpty(stack.DisplayName) ? "未命名小队" : stack.DisplayName)
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
            ArmyStackAdapter.TryGetFormalArmy(world, stack, out var tooltipArmy);
            if (tooltipArmy != null && tooltipArmy.State == FormalArmyState.Moving)
            {
                sb.Append("??????\n");
                sb.Append("? ").Append(ResolveNodeName(world, stack.SiteId))
                    .Append(" ? ").Append(HostStrategicRosterQueries.DescribeHexLabel(world, tooltipArmy.DestinationHex)).Append('\n');
            }
            else
            {
                sb.Append("?????\n");
                sb.Append("???").Append(ResolveNodeName(world, stack.SiteId)).Append('\n');
            }

            AppendArmyMemberLifeStates(sb, world, stack);
            sb.Append("\n操作：只读查看；实际人物冲突请在地面接触后发起");
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

        static string BuildSiteInspect(
            XianXia.Core.Simulation.SimulationWorld world,
            WorldSite site)
        {
            var detail = StrategicSiteAccessService.BuildSiteDetailText(world, site);
            if (string.IsNullOrEmpty(detail))
                return "地点\uff1a" + StrategicSiteAccessService.DescribeSite(site);
            return "地图地点\n\n" + detail + "\n\n操作：右键 Hex 移动；有我方在场时可进入场景";
        }

        void EnsureStyles()
        {
            if (_title != null)
                return;
            _px = Texture2D.whiteTexture;
            _title = HostImguiStyles.InkLabel(17, bold: true, ink: new Color(0.94f, 0.95f, 0.97f));
            _body = HostImguiStyles.InkLabel(13, wordWrap: true, ink: new Color(0.86f, 0.88f, 0.91f));
            _layerToggle = new GUIStyle(GUI.skin.toggle)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(18, 0, 0, 0),
                wordWrap = false
            };
            var layerInk = new Color(0.90f, 0.92f, 0.95f);
            _layerToggle.normal.textColor = layerInk;
            _layerToggle.onNormal.textColor = layerInk;
            _layerToggle.hover.textColor = Color.white;
            _layerToggle.onHover.textColor = Color.white;
            _layerToggle.active.textColor = Color.white;
            _layerToggle.onActive.textColor = Color.white;
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
            _characterListPanel = new HostStrategicCharacterListPanel(_body, _title);
            _factionDiplomacyPanel = new HostFactionDiplomacyOverviewPanel(_body, _title);
        }
    }
}
