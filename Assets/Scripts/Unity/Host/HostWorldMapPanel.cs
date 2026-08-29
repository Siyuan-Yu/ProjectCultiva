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
        readonly Dictionary<string, int> _slotAtSiteKey = new Dictionary<string, int>();
        readonly Dictionary<string, int> _countAtSiteKey = new Dictionary<string, int>();

        // 部队栈点选／右键菜单
        readonly Dictionary<string, Rect> _armyStackRects = new Dictionary<string, Rect>(16);
        readonly Dictionary<string, Rect> _formalArmyRects = new Dictionary<string, Rect>(8);
        readonly HostWorldMapSelectionAuthority _worldMapSelection = new HostWorldMapSelectionAuthority();
        const float FormalArmyMarkerHitPad = 8f;

        string SelectedFormalArmyId => _worldMapSelection.FormalArmyId;
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
        /// <summary>攻击／移动／进入菜单的输出缓冲（可与选人缓冲分离）/summary>
        readonly List<EntityId> _attackPartyScratch = new List<EntityId>(8);
        /// <summary>仅作「当前选中 过滤」中间表；禁止当作最into，避Clear 自清/summary>
        readonly List<EntityId> _orderFilterScratch = new List<EntityId>(8);
        readonly List<string> _previewPathScratch = new List<string>(16);
        string _orderPreviewArmyId = string.Empty;
        WorldTravelTarget _orderPreviewTarget;
        bool _orderPreviewActive;

        // 弥留头像右键：进入残留战场／有活人时「前往并进入
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

        // Hex 右键：WorldSite 进入菜单（须点击后才LocalMap
        bool _hexSiteEnterMenuOpen;
        string _hexSiteEnterMenuSiteId = string.Empty;
        HexCoord _hexSiteEnterMenuHex;
        Rect _hexSiteEnterMenuRect;
        HexRightClickResolution _hexSiteEnterMenuResolution;
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
            ClearArmyOrderPreview();
            // LocalMap 选中仍可能停在已倒下的人FormalHud 左上角误显「弥留
            bootstrap?.SelectionController?.ClearSelection();
            RefreshStrategicPresentation(world);
        }

        /// <summary>自动战／手动战后：重置大地图缓存与提示，确保残留弥留立刻可见/summary>
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
                // Phase 5B: LocalVisible -> World; Path/Progress/WorldPosition unchanged.
                PlayerPartyHexTravelService.ResumeWorldTravelExecutionIfNeeded(world);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                FormalArmyStrategicMutationDiagnostics.BindPresentationWorld(world);
#endif
                StrategicEncounterResolveService.NormalizePresenceAfterEncounterExit(world);
                PruneRemovedFromSelection(world);
            }

            // 开大地图不再强制暂停：战略时间Space／工具栏控制（RTS 开图仍可走时）
            _holdingPauseForMap = false;
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

        /// <summary>探望弥留到站后：直接弹接战窗（半径内弥留强制纳入）/summary>
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
            _hexMenuOpen = false;
            CloseHexSiteEnterMenu();
            _avatarMenuOpen = false;
            _avatarMenuVisitMode = false;
            _armyFormPanel?.Close();
            _armyListPanel?.Close();
            _characterListPanel?.Close();
            _globalStrategicToolbar.CloseAll();
            _worldMapSelection.SelectPlayerParty();
            ClearArmyOrderPreview();
            _panning = false;
            ForceClearInputBlock();
            ReleaseMapPause();

            if (!needExpand)
                return;

            // Phase 5B: CloseWorldMapTakeover preserves AutoTravel (LocalVisible); Idle EnterLocal.
            // ExpandLocalMap stays Host-side.
            var enter = PlayerPartyHexTravelService.CloseWorldMapTakeover(world, party);
            if (enter.IsSuccess && bootstrap != null)
                bootstrap.ExpandLocalMapForCurrentPartyWorld(closeWorldMap: false);
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
        /// 兼容旧调用点：Hex 路线 overlay 仅由 RefreshSelectedArmyPathPreview 驱动
        /// </summary>
        public void SetArmyHexPathPreview(string armyId, HexCoord destination)
        {
            _ = destination;
            if (!string.IsNullOrEmpty(armyId) &&
                !string.Equals(armyId, SelectedFormalArmyId, StringComparison.Ordinal))
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
            _worldMapSelection.SelectPlayerParty();
            _stackMenuOpen = false;
            _nodeMenuOpen = false;
            _nodeMenuNodeId = string.Empty;
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

            if (OtherBlockingPanelOpen())
            {
                if (open)
                    Close();
                return;
            }

            if (Input.GetKeyDown(toggleKey))
            {
                if (open)
                    CloseWithLocalMapTakeover();
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
            return (j != null && j.IsOpen) || (inv != null && inv.IsOpen);
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
                if (!string.IsNullOrEmpty(SelectedFormalArmyId) &&
                    world.Strategic.FormalArmies.TryGet(SelectedFormalArmyId, out var army) &&
                    army != null &&
                    army.UsesHexStrategicPosition)
                {
                    HexMath.ToWorldPosition(army.CurrentHex, world.HexWorld.HexSize, out _viewCx, out _viewCy);
                }
                else
                {
                    var focusId = world.PartyWorld.SiteId;
                    if (ArmyHexBattleAnchorService.TryResolveHexForSite(world, focusId, out var focusHex))
                        HexMath.ToWorldPosition(focusHex, world.HexWorld.HexSize, out _viewCx, out _viewCy);
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

            GUI.Label(
                new Rect(pad, titleY, Screen.width - 220f, 28f),
                "大地 Hex 战略 （左键：选格/军团｜右键：军团移动或 Party Travel｜停止 Party 旅行｜M 关闭回近景", _title);

            if (GUI.Button(new Rect(Screen.width - 100f, titleY, 84f, 32f), "关闭"))
                CloseWithLocalMapTakeover();

            DrawMapToolbar(pad, toolbarY, world);

            if (!ArmyHexCommandService.IsHexStrategicActive(world) ||
                world?.HexWorld == null ||
                !world.HexWorld.HasGrid)
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
            var hexGutterActive = ArmyHexCommandService.IsHexStrategicActive(world) &&
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

            RefreshHexPresentation(hexProjection, world);
            DrawGraph(mapRect, hexProjection, world);
            DrawMapUnitOverlays(mapRect, hexProjection, world);
            if (ShowReinforcementRadiusDebug)
                DrawReinforcementRadiusOverlay(mapRect, world);

            DrawHexContextMenu(world);
            DrawHexWorldSiteEnterMenu(world);
            DrawStackContextMenu(world);
            DrawAvatarContextMenu(world);
            DrawInspectPanel(infoRect, world);
            DrawReinforcementRadiusSlider(pad, world);
            DrawStrategicRosterPanels(world);
            TryDismissContextMenusOnOutsideClick();
            if (Event.current != null && Event.current.type == EventType.Used)
                return;
            // 菜单仍开着（点在菜单内）时不处理地图下令；外侧点击已在上面关掉菜单且不吞事
            if (_stackMenuOpen || _nodeMenuOpen || _avatarMenuOpen || _hexMenuOpen || _hexSiteEnterMenuOpen)
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
            if (world?.Strategic?.Sites?.Sites == null)
                return;

            foreach (var kv in world.Strategic.Sites.Sites)
            {
                var site = kv.Value;
                if (site == null)
                    continue;

                var owner = site.OwnerFactionId ?? string.Empty;
                if (_lastSiteOwners.TryGetValue(site.SiteId, out var prev) &&
                    !string.Equals(prev, owner, StringComparison.Ordinal) &&
                    world.Strategic?.CaptureObjectives != null &&
                    world.Strategic.CaptureObjectives.AllCompletedForSite(site.SiteId))
                {
                    var siteName = string.IsNullOrEmpty(site.DisplayName) ? site.SiteId : site.DisplayName;
                    _status = "Site captured: " + siteName + "  New Owner: " +
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

            EnsureStrategicRosterPanels();
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

            RefreshSelectedArmyPathPreview(world);
            RefreshPlayerPartyPathPreview(world);
        }

        void RefreshPlayerPartyPathPreview(SimulationWorld world)
        {
            if (!string.IsNullOrEmpty(SelectedFormalArmyId))
                return;
            if (world?.PlayerPartyTravel == null || !world.PlayerPartyTravel.IsMoving)
                return;

            _hexPathPreview.Clear();
            var motion = world.PlayerPartyTravel;
            _hexPathPreview.Add(motion.CurrentHex);
            var path = motion.HexPath;
            for (var i = motion.CurrentPathIndex; i < motion.HexPathCount; i++)
                _hexPathPreview.Add(path[i]);
            if (_hexPathPreview.Count == 1 && motion.DestinationHex != motion.CurrentHex)
                _hexPathPreview.Add(motion.DestinationHex);
        }

        /// <summary>
        /// 仅当前选中的我方军团且有有效移动计划时，填Hex 路线 overlay（线 + 格高亮共用）
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

        /// <summary>路线预览：SELF + 有效 Hex TravelPlan（剩余路径）才显示/summary>
        bool TryGetSelectedPlayerArmyForPathPreview(
            SimulationWorld world,
            out FormalArmy army)
        {
            army = null;
            if (world?.Strategic?.FormalArmies == null ||
                string.IsNullOrEmpty(SelectedFormalArmyId))
                return false;

            if (!world.Strategic.FormalArmies.TryGet(SelectedFormalArmyId, out army) || army == null)
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
                string.IsNullOrEmpty(SelectedFormalArmyId))
                return false;

            if (!world.Strategic.FormalArmies.TryGet(SelectedFormalArmyId, out army) || army == null)
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
            XianXia.Core.Simulation.SimulationWorld world)
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
                    ResolveSelectedWorldSite(world),
                    _hexPathPreview,
                    _pathMask,
                    _pathMaskW,
                    _pathMaskH);
                return;
            }
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
            DrawResidualMarkers(mapRect, world, hexMode: true, hexProjection: projection);
            DrawFormalArmyAvatars(mapRect, world);
            DrawPlayerPartyMarker(mapRect, world, projection);
            BattleEngagementWorldMapDebug.Draw(projection, world);
            DrawArmyStacks(mapRect, world);
            DrawAvatars(mapRect, world, hexMode: true, hexProjection: projection);

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
                          (string.IsNullOrEmpty(stack.DisplayName) ? "敌军" : stack.DisplayName);
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

            RegisterRemnantStackHitRects(mapRect, world);
        }

        /// <summary>弥留／尸体栈不画聚合标记，但仍需可右键攻击（个体头像会挡住栈心）/summary>
        void RegisterRemnantStackHitRects(
            Rect mapRect,
            XianXia.Core.Simulation.SimulationWorld world)
        {
            foreach (var kv in world.Strategic.Armies.Stacks)
            {
                var stack = kv.Value;
                if (stack == null || !stack.HasDownedRemnant)
                    continue;
                if (!TryResolveArmyStackWorldPoint(world, stack, out var wx, out var wy))
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

                if (_worldMapSelection.IsFormalArmySelected(army.ArmyId))
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
                var selected = _worldMapSelection.IsFormalArmySelected(army.ArmyId);
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
            _worldMapSelection.SelectFormalArmy(armyId);
            _selected.Clear();
            _selectedStackId = string.Empty;
            ClearResidualSelection();
            WorldMapArmyMarkerDiagnostics.LogMarkerSelectionVisual(
                armyId,
                _worldMapSelection.Kind,
                SelectedFormalArmyId,
                true);
            EnsureStrategicRosterPanels();
            _characterListPanel?.Close();
            _armyListPanel?.Open();
            _armyListPanel?.SelectArmy(armyId);
            _globalStrategicToolbar.SetActive(HostGlobalStrategicToolbar.ModuleId.Army);
            if (bootstrap?.Session?.World != null &&
                bootstrap.Session.World.Strategic.FormalArmies.TryGet(armyId, out var army) &&
                army != null)
            {
            }
        }

        void ClearFormalArmySelection()
        {
            if (!_worldMapSelection.IsFormalArmy)
                return;
            _worldMapSelection.SelectPlayerParty();
            ClearArmyOrderPreview();
            _armyListPanel?.SelectArmy(string.Empty);
        }

        void ClearResidualSelection() => _selectedResidualGroup = null;

        string ResolvePlayerFactionId(XianXia.Core.Simulation.SimulationWorld world) =>
            HostStrategicRosterQueries.ResolvePlayerFactionId(world, bootstrap?.Session?.CharacterIds);

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
            var partyCharacterIds = bootstrap.Session.CharacterIds;
            var partyRuntime = bootstrap.Session.PlayerParty;
            if (_armyListPanel.IsOpen)
            {
                var rect = HostStrategicRosterPanelLayout.Compute(Screen.width, Screen.height);
                if (_armyListPanel.Draw(
                        rect,
                        world,
                        partyCharacterIds,
                        EntityLabel,
                        partyRuntime,
                        FocusCameraOnArmy,
                        () => RefreshStrategicPresentation(world),
                        armyId => SyncFormalArmySelection(armyId)))
                {
                    RefreshStrategicPresentation(world);
                }
            }

            if (_characterListPanel.IsOpen)
            {
                var rect = HostStrategicRosterPanelLayout.Compute(Screen.width, Screen.height);
                if (_characterListPanel.Draw(
                        rect,
                        world,
                        partyCharacterIds,
                        partyRuntime,
                        EntityLabel,
                        FocusCameraOnArmy,
                        FocusCameraOnNode,
                        armyId =>
                        {
                            _characterListPanel.Close();
                            _armyListPanel.Open();
                            _armyListPanel.SelectArmy(armyId);
                            _worldMapSelection.SelectFormalArmy(armyId);
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
            _worldMapSelection.SelectFormalArmy(armyId);
            ArmyService.TryResolveArmySiteId(world, army, out var inspectSiteId);
            _inspectSiteId = inspectSiteId ?? string.Empty;
            _status = "已定位军队 " + armyId;
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

        void EnsureArmyFormPanel()
        {
            if (_armyFormPanel == null)
                _armyFormPanel = new HostArmyFormPanel(_body, _title);
        }

        string BuildFormalArmyInspect(
            XianXia.Core.Simulation.SimulationWorld world,
            FormalArmy army)
        {
            var sb = new StringBuilder(512);
            sb.Append("我方军团\n\n");
            sb.Append("Id：").Append(army.ArmyId).Append('\n');
            sb.Append("Leader：").Append(EntityLabel(world, army.LeaderCharacterId)).Append('\n');
            sb.Append("State：").Append(army.State).Append('\n');
            ArmyService.TryResolveArmySiteId(world, army, out var tooltipSiteId);
            sb.Append("Site?").Append(StrategicSiteAccessService.DescribeSite(world, tooltipSiteId)).Append('\n');

            var motion = army.WorldMotion;
            sb.Append("LocationKind：").Append(motion.LocationKind).Append('\n');
            sb.Append("SiteId：").Append(motion.SiteId ?? "—").Append('\n');
            sb.Append("WorldPosition：(")
                .Append(motion.WorldPosition.X.ToString("0.##")).Append(',')
                .Append(motion.WorldPosition.Y.ToString("0.##")).Append(")\n");
            sb.Append("CurrentHex：").Append(motion.CurrentHex).Append('\n');
            sb.Append("InsideWorldSite：").Append(
                motion.LocationKind == FormalArmyLocationKind.AtWorldSite).Append('\n');
            sb.Append("CurrentOrder：").Append(motion.CurrentOrderKind).Append('\n');
            sb.Append("Destination：").Append(motion.DestinationHex);
            if (!string.IsNullOrEmpty(motion.DestinationSiteId))
                sb.Append(" (Site ").Append(motion.DestinationSiteId).Append(')');
            sb.Append('\n');
            sb.Append("TravelState：Moving=").Append(motion.IsMoving)
                .Append(" Seg=").Append(motion.SegmentIndex)
                .Append('/').Append(Math.Max(0, motion.HexPathCount - 1))
                .Append(" Prog=").Append(motion.SegmentProgress.ToString("0.##")).Append('\n');
            sb.Append("Members：").Append(army.MemberCharacterIds.Count).Append('\n');
            for (var i = 0; i < army.MemberCharacterIds.Count; i++)
            {
                var memberId = new EntityId(army.MemberCharacterIds[i]);
                var party = bootstrap?.Session?.PlayerParty;
                CharacterWorldMovementAuthorityQuery.TryGetAuthority(world, memberId, party, out var auth);
                ArmyService.TryGetArmyForCharacter(world, memberId, out var memberArmy);
                var inParty = party != null && party.IsMember(memberId);
                sb.Append(" · ").Append(EntityLabel(world, memberId))
                    .Append(" Auth=").Append(auth)
                    .Append(" Party=").Append(inParty ? "Yes" : "No")
                    .Append(" Army=").Append(memberArmy != null ? memberArmy.ArmyId : "—")
                    .Append('\n');
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
                    var beforeId = _worldMapSelection.Kind == HostWorldMapSelectionKind.FormalArmy
                        ? SelectedFormalArmyId
                        : (bootstrap?.Session?.PlayerParty?.ActiveCharacterId.ToString() ?? string.Empty);
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

                    WorldMapArmyMarkerDiagnostics.LogWorldMapSelectionClick(
                        hitArmyId,
                        beforeKind,
                        beforeId,
                        _worldMapSelection.DescribeKind(),
                        SelectedFormalArmyId,
                        _worldMapSelection.DescribeKind(),
                        SelectedFormalArmyId,
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
                    // 保留已选军团：便于立刻右键同格进入残留
                    _inspectSiteId = string.Empty;
                    var enterHint = BattleOfferService.HasLingeringBattlefield(world)
                        ? (string.IsNullOrEmpty(SelectedFormalArmyId)
                            ? "｜先选我方军团，再右键本格／标记进入残留"
                            : "｜右键本格或残留标记进入残留战场")
                        : string.Empty;
                    _status = FormatResidualGroupTitle(residualGroup) + " ×" + residualGroup.Count + enterHint;
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
                        {
                            armyHint = string.IsNullOrEmpty(SelectedFormalArmyId)
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
                    // 标准 RTS：点敌军改选敌军，清空我方选中；不弹指令菜
                    _selected.Clear();
                    ClearFormalArmySelection();
                    _selectedStackId = hitStackId;
                    _inspectSiteId = string.Empty;
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

            // —右键：只负责下令（永不改选中集合）—
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
                   "· Ctrl+左键：切换道路（编辑）";
                    e.Use();
                    return;
                }
            }

                _status = "无法解析倒下角色位置";
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

            if (TryHitFormalArmy(mouse, out _, FormalArmyMarkerHitPad))
            {
                e.Use();
                return true;
            }

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
                _inspectSiteId = string.Empty;
                _selectedWorldSiteId = string.Empty;
            }

            if (world.Strategic.Sites.TryGetAtHex(pickedHex, out var selectedSite) && selectedSite != null)
                _selectedWorldSiteId = selectedSite.SiteId;
            else
                _selectedWorldSiteId = string.Empty;

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
                true,
                selectedArmy);

            LogHexRightClickTrace(resolution, selectedArmy, pickedHex);

            _hexMenuOpen = false;
            CloseHexSiteEnterMenu();
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
                case HexRightClickResolvedAction.ShowWorldSiteEnterMenu:
                    OpenHexWorldSiteEnterMenu(resolution, pickedHex, mouse);
                    break;
                default:
                    DispatchHexRightClickTravel(world, pickedHex, resolution.StatusHint);
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
                case HexRightClickResolvedAction.ShowWorldSiteEnterMenu:
                    return "ENTER_WORLD_SITE_MENU";
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
            if (!string.IsNullOrEmpty(SelectedFormalArmyId) &&
                world?.Strategic?.FormalArmies != null &&
                world.Strategic.FormalArmies.TryGet(SelectedFormalArmyId, out var army) &&
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
            if (_worldMapSelection.Kind != HostWorldMapSelectionKind.FormalArmy)
            {
                _status = "当前选中 PlayerParty，不能直接对军团下 Hex 移动命令";
                return;
            }

            if (string.IsNullOrEmpty(SelectedFormalArmyId) ||
                !world.Strategic.FormalArmies.TryGet(SelectedFormalArmyId, out var army) ||
                army == null)
            {
                WarnBrokenFormalArmySelection("ExecuteDirectMoveArmyToHex");
                _status = "FormalArmy 选中态损坏，已阻止移动（未 fallback PlayerParty）";
                return;
            }

            if (army.State == FormalArmyState.Garrisoned)
            {
                _status = "无法解析倒下角色位置";
                return;
            }

            var destLabel = hex.ToString();
            if (world.Strategic.Sites.TryGetAtHex(hex, out var site) && site != null)
                destLabel = string.IsNullOrEmpty(site.DisplayName) ? site.SiteId : site.DisplayName;

            SetArmyHexPathPreview(SelectedFormalArmyId, hex);
            var move = ArmyHexCommandService.MoveArmy(world, SelectedFormalArmyId, hex);
            _status = move.IsSuccess ? "军团已出发前往 " + destLabel : FormatFail(move);
        }

        void DispatchHexRightClickTravel(
            XianXia.Core.Simulation.SimulationWorld world,
            HexCoord hex,
            string statusHint)
        {
            if (_worldMapSelection.Kind == HostWorldMapSelectionKind.FormalArmy)
            {
                if (string.IsNullOrEmpty(SelectedFormalArmyId) ||
                    !world.Strategic.FormalArmies.TryGet(SelectedFormalArmyId, out _))
                {
                    WarnBrokenFormalArmySelection("DispatchHexRightClickTravel");
                    _status = "FormalArmy 选中态损坏，已阻止移动（未 fallback PlayerParty）";
                    return;
                }

                _status = string.IsNullOrEmpty(statusHint)
                    ? "当前选中军团：请右键有效目标或使用攻击菜单"
                    : statusHint;
                return;
            }

            if (TryExecutePlayerPartyTravel(world, hex, out var partyStatus))
            {
                _status = partyStatus;
                return;
            }

            _status = string.IsNullOrEmpty(statusHint)
                ? "右键：PlayerParty Travel；或先左键选军团"
                : statusHint;
        }

        void WarnBrokenFormalArmySelection(string context)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UnityEngine.Debug.LogWarning(
                "[WorldMapSelection] Broken FormalArmy authority: Kind=FormalArmy" +
                " FormalArmyId=" + (SelectedFormalArmyId ?? string.Empty) +
                " Context=" + (context ?? string.Empty) +
                " → blocked travel (no PlayerParty fallback)");
#endif
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
                    " FormalArmyId=" + (SelectedFormalArmyId ?? string.Empty));
#endif
                return false;
            }

            var party = bootstrap?.Session?.PlayerParty;
            if (party == null || !party.HasActive)
                return false;

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

            var move = PlayerPartyHexTravelService.BeginTravel(
                world, party, cmd.DestinationHex, cmd.TargetSiteId ?? string.Empty);
            if (move.IsFailure)
            {
                status = FormatFail(move);
                return true;
            }

            _worldMapSelection.SelectPlayerParty();
            RefreshPlayerPartyPathPreview(world);
            var destLabel = cmd.TargetHex.ToString();
            if (!string.IsNullOrEmpty(cmd.TargetSiteId) &&
                world.Strategic.Sites.TryGet(cmd.TargetSiteId, out var site) &&
                site != null)
                destLabel = string.IsNullOrEmpty(site.DisplayName) ? site.SiteId : site.DisplayName;
            status = "PlayerParty Travel → " + destLabel;
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
            if (ArmyService.TryGetArmyForCharacter(world, party.ActiveCharacterId, out _))
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

                _status = "无法解析倒下角色位置";
        }

        void CloseHexSiteEnterMenu()
        {
            _hexSiteEnterMenuOpen = false;
            _hexSiteEnterMenuSiteId = string.Empty;
            _hexSiteEnterMenuResolution = null;
        }

        void OpenHexWorldSiteEnterMenu(
            HexRightClickResolution resolution,
            HexCoord hex,
            Vector2 mouse)
        {
            if (resolution == null || string.IsNullOrEmpty(resolution.SiteId))
                return;

            _hexSiteEnterMenuResolution = resolution;
            _hexSiteEnterMenuSiteId = resolution.SiteId;
            _hexSiteEnterMenuHex = hex;
            _hexSiteEnterMenuRect = AnchorContextMenu(new Rect(mouse.x, mouse.y, 1f, 1f), 196f, 72f);
            _hexSiteEnterMenuOpen = true;
            _hexMenuOpen = false;
            _stackMenuOpen = false;
            _avatarMenuOpen = false;
            _nodeMenuOpen = false;
        }

        void DrawHexWorldSiteEnterMenu(XianXia.Core.Simulation.SimulationWorld world)
        {
            if (!_hexSiteEnterMenuOpen || string.IsNullOrEmpty(_hexSiteEnterMenuSiteId))
                return;
            if (world?.Strategic?.Sites == null ||
                !world.Strategic.Sites.TryGet(_hexSiteEnterMenuSiteId, out var site) ||
                site == null)
            {
                CloseHexSiteEnterMenu();
                return;
            }

            var prevDepth = GUI.depth;
            GUI.depth = -85;
            HostUiHitTest.Block(_hexSiteEnterMenuRect);
            var prev = GUI.color;
            GUI.color = new Color(0.16f, 0.17f, 0.19f, 0.96f);
            GUI.DrawTexture(_hexSiteEnterMenuRect, _px);
            GUI.color = prev;

            var title = string.IsNullOrEmpty(site.DisplayName) ? site.SiteId : site.DisplayName;
            GUI.Label(
                new Rect(_hexSiteEnterMenuRect.x + 8f, _hexSiteEnterMenuRect.y + 4f, _hexSiteEnterMenuRect.width - 16f, 18f),
                title,
                _body);

            var enterLabel = StrategicWorldSiteAccessService.BuildEnterSiteMenuLabel(site);
            var canEnter = TryGetSelectedLivingPlayerArmy(world, out var army, out _) &&
                             army != null &&
                             StrategicWorldSiteAccessService.CanEnterWorldSiteLocalMap(
                                 world, site.SiteId, army.ArmyId).IsSuccess;
            if (StrategicClockFreezeService.IsModalEncounter(world))
                canEnter = false;

            GUI.enabled = canEnter;
            var y = _hexSiteEnterMenuRect.y + 28f;
            var bw = _hexSiteEnterMenuRect.width - 16f;
            if (GUI.Button(new Rect(_hexSiteEnterMenuRect.x + 8f, y, bw, 22f), enterLabel) && canEnter)
            {
                Event.current.Use();
                ExecuteEnterWorldSiteAtHex(world, _hexSiteEnterMenuResolution);
                CloseHexSiteEnterMenu();
            }

            GUI.enabled = true;
            GUI.depth = prevDepth;
        }

        void ExecuteEnterWorldSiteAtHex(
            XianXia.Core.Simulation.SimulationWorld world,
            HexRightClickResolution resolution)
        {
            if (resolution == null || string.IsNullOrEmpty(resolution.SiteId))
            {
                _status = "无法解析倒下角色位置";
                return;
            }

            if (!TryGetSelectedLivingPlayerArmy(world, out var army, out var err))
            {
                _status = string.IsNullOrEmpty(err) ? "请先左键选中我方军团" : err;
                return;
            }

            if (!world.Strategic.Sites.TryGet(resolution.SiteId, out var site) || site == null)
            {
                _status = "无法解析倒下角色位置";
                return;
            }

            var enter = WorldTravelService.EnterWorldSiteScene(world, site.SiteId, army.ArmyId);
            if (enter.IsSuccess)
            {
                CloseAllWorldMapPanels();
                bootstrap.ApplyPartyWorldSitePresentation(closeWorldMap: true);
                var title = string.IsNullOrEmpty(site.DisplayName) ? site.SiteId : site.DisplayName;
                _status = "已进入 " + title;
                return;
            }

            _status = FormatFail(enter);
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
                _status = "无法解析倒下角色位置";
                return;
            }

            if (!world.Strategic.Armies.TryGet(target.StackId, out var stack) || stack == null)
            {
                _status = "无法解析倒下角色位置";
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
        /// 右键「有残留Hex」：按原先逻辑进入残留战场（我方弥留头像菜单／敌方残留栈菜单）
        /// 无残留或接战点未激活时返回 false，交还给普Hex 移动
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

            // 我方弥留所在格 与右键弥留头像同一套菜
            if (!friendlyFocus.IsNone)
            {
                OpenIncapAvatarMenu(world, friendlyFocus, mouse);
                return true;
            }

            // 敌方残留所在格 与右键残留栈同一套菜单（进入残留／追击再攻）
            if (!string.IsNullOrEmpty(enemyStackId) &&
                TryOpenStackAttackMenu(world, enemyStackId, mouse))
                return true;

            _status = "该格有残留，但无法打开进入菜单";
            return true;
        }

        /// <summary>Hex 上是否有与当前残留战场相关的我方弥留／敌方残留/summary>
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

            if (string.IsNullOrEmpty(SelectedFormalArmyId))
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
                _status = "无法解析倒下角色位置";
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

        /// <summary>须左键选中我方存活军团；不自动从散装队伍推断攻击方/summary>
        bool TryGetSelectedLivingPlayerArmy(
            XianXia.Core.Simulation.SimulationWorld world,
            out FormalArmy army,
            out string error)
        {
            army = null;
            error = null;
            if (world?.Strategic?.FormalArmies == null)
                return false;

            if (string.IsNullOrEmpty(SelectedFormalArmyId))
                return false;

            if (!world.Strategic.FormalArmies.TryGet(SelectedFormalArmyId, out army) || army == null)
            {
                error = "该军团已无可用成员，无法发起攻击。";
                return false;
            }

            var playerFaction = ResolvePlayerFactionId(world);
            if (!string.IsNullOrEmpty(playerFaction) &&
                !string.Equals(army.FactionId, playerFaction, StringComparison.Ordinal))
            {
                error = "该军团已无可用成员，无法发起攻击。";
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
            if (!string.IsNullOrEmpty(SelectedFormalArmyId))
            {
                if (_selected.Count > 0)
                    return "军团 " + SelectedFormalArmyId + " +" + _selected.Count;
                return "军团 " + SelectedFormalArmyId;
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
            ArmyStackAdapter.TryGetFormalArmy(world, stack, out var labelArmy);
            var where = labelArmy != null && labelArmy.State == FormalArmyState.Moving ? "途中" : stack.SiteId;
            return name + " · " + faction + " · " + stack.MemberCount + "人 · 战力" + power +
                   " · " + where;
        }

        void DrawStackContextMenu(XianXia.Core.Simulation.SimulationWorld world)
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
            var menuH = isRemnant && hasLinger ? 86f : 56f;

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

            if (isRemnant && hasLinger)
            {
                CollectActingArmyLivingParty(world, _scratchParty);
                var needTravel = false;
                if (_scratchParty.Count > 0 &&
                    TryResolveRemnantBattleAnchorHex(world, stack, out var remnantAnchorHex))
                {
                    needTravel = !IsAnyPartyMemberInReinforcementRange(
                        world, _scratchParty, remnantAnchorHex);
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
                _status = "无法解析倒下角色位置";
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
                _status = "无法解析倒下角色位置";
                _stackMenuOpen = false;
            }

            GUI.enabled = true;
            GUI.depth = prevDepth;
        }

        static bool TryResolveRemnantBattleAnchorHex(
            XianXia.Core.Simulation.SimulationWorld world,
            ArmyStack stack,
            out HexCoord anchorHex)
        {
            anchorHex = default;
            if (world?.Strategic?.Participants != null &&
                ArmyHexBattleAnchorService.TryGetBattleAnchorHex(world.Strategic.Participants, out anchorHex))
                return true;

            if (stack != null &&
                ArmyStackAdapter.TryGetFormalArmy(world, stack, out var army) &&
                army != null &&
                army.UsesHexStrategicPosition)
            {
                anchorHex = army.CurrentHex;
                return true;
            }

            if (stack != null &&
                ArmyHexBattleAnchorService.TryResolveHexForSite(world, stack.SiteId, out anchorHex))
                return true;

            return false;
        }

        void ExecuteAttackStack(
            XianXia.Core.Simulation.SimulationWorld world,
            List<EntityId> party,
            ArmyStack stack)
        {
            var attackerArmyId = SelectedFormalArmyId;
            if (string.IsNullOrEmpty(attackerArmyId) ||
                !world.Strategic.FormalArmies.TryGet(attackerArmyId, out var attackerArmy) ||
                attackerArmy == null ||
                !ArmyPostBattleSyncService.HasMacroOrderLivingMember(world, attackerArmy))
            {
                _status = "无法解析倒下角色位置";
                return;
            }

            var attackerFaction = attackerArmy.FactionId;

            if (!string.IsNullOrEmpty(attackerFaction) &&
                !string.IsNullOrEmpty(stack.FactionId) &&
                !string.Equals(attackerFaction, stack.FactionId, System.StringComparison.Ordinal) &&
                !WarGateService.CanAttack(world, attackerFaction, stack.FactionId))
            {
                _status = "无法解析倒下角色位置";
                return;
            }

            var attack = ArmyHexCommandService.AttackStack(world, attackerArmyId, stack);
            if (!attack.IsSuccess)
                _status = FormatFail(attack);
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
            if (_hexSiteEnterMenuOpen && _hexSiteEnterMenuRect.Contains(ev.mousePosition))
                return;
            if (_avatarMenuOpen && _avatarMenuRect.Contains(ev.mousePosition))
                return;

            if (!_stackMenuOpen && !_nodeMenuOpen && !_hexMenuOpen && !_hexSiteEnterMenuOpen &&
                !_avatarMenuOpen)
                return;

            _stackMenuOpen = false;
            _nodeMenuOpen = false;
            _hexMenuOpen = false;
            CloseHexSiteEnterMenu();
            _avatarMenuOpen = false;
            _avatarMenuVisitMode = false;
            // Use：同一帧右键仍可落到移动／攻击下令
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
                LingeringBattlefieldPartyService.TryResolveBattleAnchorHex(
                    world, target, out var anchorHex))
            {
                _avatarMenuVisitMode = !IsAnyPartyMemberInReinforcementRange(
                    world, _scratchParty, anchorHex);
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
                    "驻扎中不可追击，请先解除驻扎", _body);
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
                    "驻扎中不可追击，请先解除驻扎", _body);
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

        /// <summary>已选活人前往弥留接战点；已在半径内则直接弹接战窗/summary>
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
            if (_scratchParty.Count == 0 || string.IsNullOrEmpty(SelectedFormalArmyId))
            {
                _status = "请先左键选中军团";
                return false;
            }

            if (!BattleOfferService.HasLingeringBattlefield(world))
            {
                _status = "无法解析倒下角色位置";
                return false;
            }

            if (LingeringBattlefieldPartyService.TryResolveBattleAnchorHex(
                    world, focusIncap, out var anchorHex) &&
                IsAnyPartyMemberInReinforcementRange(
                    world, _scratchParty, anchorHex))
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

            if (!ArmyHexCommandService.TryResolveDestinationHex(world, target, out var destHex, out _))
            {
                _status = "无法解析倒下角色位置";
                return false;
            }

            world.Strategic.SetPendingLingeringVisit(focusIncap.Value, _scratchParty);
            var move = ArmyHexCommandService.MoveArmy(world, SelectedFormalArmyId, destHex);
            if (!move.IsSuccess)
            {
                world.Strategic.ClearPendingLingeringVisit();
                _status = FormatFail(move);
                return false;
            }

            _status = "军团前往「" + EntityLabel(world, focusIncap) + "」残留点…";
            return true;
        }

        static bool IsAnyPartyMemberInReinforcementRange(
            XianXia.Core.Simulation.SimulationWorld world,
            IReadOnlyList<EntityId> party,
            HexCoord anchorHex)
        {
            if (world == null || party == null)
                return false;
            for (var i = 0; i < party.Count; i++)
            {
                if (!world.WorldPresence.TryGet(party[i], out var wp) || wp == null)
                    continue;
                if (ReinforcementRangeService.IsWithinReinforcementRange(
                        world, wp, anchorHex))
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

            if (wp.UsesHexPresence)
            {
                target = WorldTravelTarget.AtHex(wp.ResidualHex);
                return true;
            }

            if (wp.Mode == PartyWorldPresenceMode.AtSite &&
                !string.IsNullOrEmpty(wp.SiteId) &&
                world.Strategic.Sites.TryResolveSiteHex(wp.SiteId, out var siteHex))
            {
                target = WorldTravelTarget.AtHex(siteHex);
                return true;
            }

            if (ArmyService.TryGetArmyForCharacter(world, id, out var army) &&
                army != null &&
                army.UsesHexStrategicPosition)
            {
                target = WorldTravelTarget.AtHex(army.CurrentHex);
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
            _status = EntityLabel(world, hitId) + "（" + tag + "）｜可进入残留战场";
                return;
            }

            if (!hasArmy)
            {
            _status = EntityLabel(world, hitId) + "（" + tag + "）｜可进入残留战场";
                return;
            }

            if (LingeringBattlefieldPartyService.TryResolveBattleAnchorHex(
                    world, hitId, out var anchorHex) &&
                !IsAnyPartyMemberInReinforcementRange(
                    world, _scratchParty, anchorHex))
            {
            _status = EntityLabel(world, hitId) + "（" + tag + "）｜可进入残留战场";
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
                    SelectedFormalArmyId,
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

            // 选中真源：_selected Session.CharacterIds
            // 弥留真源：LifecycleComponent（IsLivingForMacroOrder
            // 能否上路：CanReceiveTravelOrder
            // 移动／攻击共用本函数；中间表必须独立，禁into 兼缓冲
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

        /// <summary>进入场景时关掉大地图（本实例＋bootstrap 引用）/summary>
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
            if (!string.IsNullOrEmpty(SelectedFormalArmyId) &&
                world.Strategic?.FormalArmies != null &&
                world.Strategic.FormalArmies.TryGet(SelectedFormalArmyId, out var formalArmy) &&
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
            sb.Append('\u3010').Append(FormatResidualGroupTitle(group)).Append("\u3011\n");
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
            sb.Append("WorldSite：").Append(siteName).Append('\n');
            sb.Append("WorldSiteId：").Append(site.SiteId).Append("\n\n");
            sb.Append("AnchorHex：").Append(site.AnchorHex).Append('\n');
            sb.Append("Footprint Count：").Append(footprintCount).Append("\n");
            var outsideCount = WorldSiteFootprintExitConnectionResolver.CountUniqueTraversableOutsideNeighbors(
                world, site);
            sb.Append("Surface Exit Connections：").Append(outsideCount).Append("\n\n");
            sb.Append("Footprint Hexes：\n");
            foreach (var hex in site.EnumerateFootprintHexes())
                sb.Append(hex).Append('\n');
            sb.Append('\n');
            if (!string.IsNullOrEmpty(site.LocalMapId))
                sb.Append("LocalMapId：").Append(site.LocalMapId).Append('\n');
            if (!string.IsNullOrEmpty(site.OwnerFactionId))
                sb.Append("OwnerFactionId：").Append(site.OwnerFactionId).Append('\n');
            sb.Append("Clicked Hex：").Append(clickedHex).Append('\n');
            return sb.ToString();
        }

        string BuildHexInspect(XianXia.Core.Simulation.SimulationWorld world, HexCoord hex)
        {
            var sb = new StringBuilder(320);
            sb.Append("Hex ").Append(hex).Append('\n');
            if (!world.HexWorld.TryGetTile(hex, out var tile) || tile == null)
            {
                    sb.Append("\n成员状态：\n");
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
                if (!string.IsNullOrEmpty(site.SiteType))
                    sb.Append("类型：").Append(site.SiteType).Append('\n');
                if (!string.IsNullOrEmpty(site.OwnerFactionId))
                    sb.Append("归属：").Append(StrategicFactionCatalog.DisplayName(site.OwnerFactionId)).Append('\n');
                if (!string.IsNullOrEmpty(site.LocalMapId))
                    sb.Append("LocalMap：").Append(site.LocalMapId).Append('\n');
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
            sb.Append("敌军部队\n\n");
                sb.Append("道路：")
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
