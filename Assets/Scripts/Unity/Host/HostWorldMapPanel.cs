using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;
using XianXia.Core.World.Surface;
using XianXia.Data.Content;

namespace XianXia.Unity.Host
{
    /// <summary>Current product WorldMap: one continuous Surface coordinate space.</summary>
    public sealed class HostWorldMapPanel : MonoBehaviour
    {
        const string InputOwner = "WorldMapPlanningOverlay";
        const float HeaderHeight = 78f;
        const float FooterHeight = 34f;
        const float MinimumViewHalf = 1.5f;
        const float CouncilHallMarkerSizeCells = 90f;
        const float FactionFlagMarkerSizeCells = 26f;
        const float PlayerMarkerScreenSize = 26f;
        const float SquadMarkerSizeCells = 14f;
        const float SiteLabelHeightCells = 24f;
        const float SiteLabelGapCells = 5f;
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] KeyCode toggleKey = KeyCode.M;
        [SerializeField] bool open;

        readonly HostSurfaceWorldMapRenderer _renderer = new HostSurfaceWorldMapRenderer();
        readonly HostGlobalStrategicToolbar _toolbar = new HostGlobalStrategicToolbar();
        readonly List<(string Id, Rect Rect)> _siteHits = new List<(string, Rect)>();
        readonly List<(string Id, Rect Rect)> _squadHits = new List<(string, Rect)>();
        readonly HashSet<ulong> _selected = new HashSet<ulong>();
        readonly GUIContent _siteLabelContent = new GUIContent();
        readonly Dictionary<int, GUIStyle> _siteLabelStyles = new Dictionary<int, GUIStyle>();
        readonly Dictionary<(string Text, int FontSize), Vector2> _siteLabelSizes =
            new Dictionary<(string, int), Vector2>();
        readonly HashSet<string> _missingSiteCoreReported = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> _reportedTravelFailures = new HashSet<string>(StringComparer.Ordinal);
        sealed class PendingRoute
        {
            public SimulationWorld World;
            public SurfaceGroundNavigation Navigation;
            public WorldVec2 Start;
            public WorldVec2 Destination;
            public EntityId Actor;
            public string SiteId;
            public CancellationTokenSource Cancellation;
            public Task<(SurfaceGroundRouteStatus Status, List<WorldVec2> Points)> Work;
        }

        PendingRoute _pendingRoute;
        bool _hasQueuedRoute;
        bool _discardPendingRoute;
        WorldVec2 _queuedDestination;
        string _queuedSiteId;
        bool _statusIsError;
        int _acceptedTravelPlanVersion = -1;
        EntityId _acceptedTravelActorId = EntityId.None;
        bool _acceptedTravelWasAirborne;
        HostStrategicCharacterListPanel _characterPanel;
        HostFactionDiplomacyOverviewPanel _factionPanel;
        GUIStyle _title;
        GUIStyle _body;
        GUIStyle _toggle;
        GUIStyle _axisCenter;
        GUIStyle _axisLeft;
        GUIStyle _playerLabel;
        bool _showGrid = true;
        bool _showGeography = true;
        bool _showTerritory = true;
        bool _showFactionLegend = true;
        string _hoverFaction;
        string _lastLegendClick;
        float _lastLegendClickTime = -1f;
        readonly Dictionary<string, int> _legendCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        readonly List<(string Id, int Count, string Label, Color Tint)> _legendRows =
            new List<(string, int, string, Color)>();
        SimulationWorld _legendWorld;
        string _legendSurface;
        int _legendFrame = -1;
        int _legendTotal;
        string _legendTitle = string.Empty;
        bool _viewReady;
        bool _panning;
        Vector2 _panLast;
        float _centerX;
        float _centerY;
        float _viewHalf = 20f;
        string _selectedSiteId = string.Empty;
        string _selectedSquadId = string.Empty;
        string _status = string.Empty;

        public bool IsOpen => open;
        public void Bind(PlayableHostBootstrap host) => bootstrap = host;

        public static bool IsBlockedByBattlefield(SimulationWorld world)
        {
            if (world?.Strategic == null) return false;
            return StrategicClockFreezeService.IsModalEncounter(world) ||
                   world.Strategic.CharacterEncounter != null;
        }

        public void Toggle()
        {
            if (open) CloseWithLocalMapTakeover();
            else Open();
        }

        public void Open()
        {
            if (bootstrap?.Session != null && bootstrap.Session.IsInitialized &&
                IsBlockedByBattlefield(bootstrap.Session.World)) return;
            // SPACE-01：Separate Space 内禁用 WorldMap，避免误显 Outdoor。
            if (bootstrap?.Session != null && bootstrap.Session.IsInitialized &&
                bootstrap.Session.World.LocalMap != null &&
                bootstrap.Session.World.LocalMap.IsActive)
            {
                _status = "当前位于独立空间，无法查看大世界地图。";
                return;
            }
            bootstrap?.InventoryPanel?.Close();
            bootstrap?.ConstructionPanel?.Close();
            bootstrap?.QuestJournal?.Close();
            open = true;
            var motion = bootstrap?.Session?.World?.PlayerPartyTravel;
            _acceptedTravelPlanVersion = motion != null && motion.IsMoving
                ? motion.TravelPlanVersion : -1;
            _acceptedTravelActorId = _acceptedTravelPlanVersion >= 0
                ? bootstrap.Session.PlayerParty.ActiveCharacterId : EntityId.None;
            _acceptedTravelWasAirborne = _acceptedTravelPlanVersion >= 0 &&
                XianXia.Core.Combat.FlightService.IsAirborne(
                    bootstrap.Session.World, _acceptedTravelActorId);
            _viewReady = false;
            _missingSiteCoreReported.Clear();
            HostInputGate.Acquire(InputOwner);
            bootstrap?.PlayerPartyController?.FreezeSurfaceTravelForPlanning();
        }

        public void Close() => CloseInternal(false);
        public void CloseWithLocalMapTakeover() => CloseInternal(true);
        void CloseInternal(bool resumeLocal)
        {
            _hoverFaction = null;
            _lastLegendClick = null;
            open = false;
            _acceptedTravelPlanVersion = -1;
            _acceptedTravelActorId = EntityId.None;
            _acceptedTravelWasAirborne = false;
            HostInputGate.Release(InputOwner);
            _panning = false;
            _characterPanel?.Close();
            _factionPanel?.Close();
            _toolbar.CloseAll();
            if (!resumeLocal || bootstrap?.Session == null || !bootstrap.Session.IsInitialized) return;
            var world = bootstrap.Session.World;
            if (bootstrap.ContinuousOutdoorSurfaceRuntime != null &&
                bootstrap.ContinuousOutdoorSurfaceRuntime.IsActive)
            {
                if (world?.PlayerPartyTravel != null && world.PlayerPartyTravel.IsMoving &&
                    world.PlayerPartyTravel.ExecutionMode == PlayerPartyTravelExecutionMode.SurfaceVisible)
                    bootstrap.PlayerPartyController?.ResumeSurfaceTravelAfterPlanning();
                return;
            }
            if (bootstrap.ContinuousOutdoorSurfaceRuntime != null &&
                bootstrap.ContinuousOutdoorSurfaceRuntime.TryActivateAtCurrentWorldPosition())
                bootstrap.RefreshContinuousOutdoorOverlaysOnce();
        }

        public void ClearSessionState()
        {
            Close();
            _pendingRoute?.Cancellation.Cancel();
            _pendingRoute = null;
            _hasQueuedRoute = false;
            _discardPendingRoute = false;
            _selected.Clear();
            _selectedSiteId = string.Empty;
            _selectedSquadId = string.Empty;
            _status = string.Empty;
            _statusIsError = false;
            _acceptedTravelPlanVersion = -1;
            _acceptedTravelActorId = EntityId.None;
            _acceptedTravelWasAirborne = false;
            _reportedTravelFailures.Clear();
            _viewReady = false;
        }

        public void RefreshStrategicPresentation(SimulationWorld world)
        {
            if (world == null) return;
            _selected.RemoveWhere(id => !world.Entities.TryGet(new EntityId(id), out _));
            _status = "战略地图已刷新";
        }

        public bool TryGetPrimarySelectedLiving(SimulationWorld world, out EntityId id)
        {
            id = default;
            if (world == null) return false;
            foreach (var value in _selected)
            {
                var candidate = new EntityId(value);
                if (!CharacterLifeStateQuery.IsLivingForMacroOrder(world, candidate)) continue;
                id = candidate;
                return true;
            }
            return false;
        }

        public void FocusCameraOnSquad(string squadId)
        {
            var world = bootstrap?.Session?.World;
            if (world == null || string.IsNullOrEmpty(squadId) ||
                !SquadWorldMotionService.TryGetActiveNpcSquadAuthority(
                    world, squadId, out _, out var motion)) return;
            Focus(motion.WorldPosition.X, motion.WorldPosition.Y);
            _selectedSquadId = squadId;
            _selectedSiteId = string.Empty;
            _status = "已定位 NPC 小队";
        }

        public void FocusCameraOnNode(string nodeId)
        {
            var world = bootstrap?.Session?.World;
            if (world == null || string.IsNullOrEmpty(nodeId) ||
                !world.Strategic.Sites.TryGet(nodeId, out var site) || site == null ||
                !TrySitePoint(world, site, out var x, out var y)) return;
            Focus(x, y);
            _selectedSiteId = nodeId;
            _selectedSquadId = string.Empty;
            _status = "已定位 " + site.DisplayName;
        }

        public void FocusCameraOnWorldPosition(WorldVec2 position)
        {
            Focus(position.X, position.Y);
            _selectedSiteId = string.Empty;
            _selectedSquadId = string.Empty;
            _status = "已定位角色的个人位置";
        }

        void Focus(float x, float y)
        {
            _centerX = x;
            _centerY = y;
            _viewHalf = Mathf.Min(_viewHalf, MinimumViewHalf * 1.25f);
            _viewReady = true;
        }

        static float FullSurfaceViewHalf(Rect viewport, SurfaceGroundNavigation nav)
        {
            const float margin = 1.08f;
            var worldWidth = nav.Width * nav.CellSize;
            var worldHeight = nav.Height * nav.CellSize;
            var scale = Mathf.Min(viewport.width / (worldWidth * margin),
                viewport.height / (worldHeight * margin));
            return Mathf.Max(MinimumViewHalf,
                Mathf.Min(viewport.width, viewport.height) / (2f * scale));
        }

        void Update()
        {
            using var timing = HostDebugHud.WorldMapUpdate.Auto();
            FinishPendingRoute();
            ReportInterruptedRoute();
            if (open) HostInputGate.Acquire(InputOwner);
            if (open && bootstrap?.Session?.IsInitialized == true &&
                IsBlockedByBattlefield(bootstrap.Session.World))
                Close();
            if (!HostInputGate.TextInputFocused && Input.GetKeyDown(toggleKey)) Toggle();
        }

        void OnDisable()
        {
            open = false;
            _acceptedTravelPlanVersion = -1;
            _acceptedTravelActorId = EntityId.None;
            _acceptedTravelWasAirborne = false;
            _pendingRoute?.Cancellation.Cancel();
            _pendingRoute = null;
            _hasQueuedRoute = false;
            _discardPendingRoute = false;
            HostInputGate.Release(InputOwner);
        }

        void OnDestroy() => _renderer.Clear();

        void OnGUI()
        {
            if (!open) return;
            EnsureStyles();
            var world = bootstrap?.Session != null && bootstrap.Session.IsInitialized
                ? bootstrap.Session.World : null;
            var nav = world?.SurfaceGround?.Active;
            GUI.depth = -80;
            HostUiHitTest.BeginFrame();
            HostUiHitTest.BlockSelectionWholeScreen();
            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(.08f, .09f, .11f, 1f));
            if (nav == null)
            {
                GUI.Label(new Rect(28, 28, Screen.width - 56, 36),
                    "当前世界没有可用的连续世界地图。", _title);
                if (GUI.Button(new Rect(28, 70, 80, 30), "关闭")) Close();
                HostUiHitTest.EndFrame();
                return;
            }
            RefreshFactionLegend(world, nav.SurfaceId);
            var legendRect = FactionLegendRect();
            HandleLegendInput(legendRect, world, nav);
            var mapRect = new Rect(12f, HeaderHeight, Screen.width - 24f,
                Mathf.Max(64f, legendRect.y - HeaderHeight - 4f));
            var fullViewHalf = FullSurfaceViewHalf(mapRect, nav);
            if (!_viewReady)
            {
                _centerX = nav.OriginX + nav.Width * nav.CellSize * .5f;
                _centerY = nav.OriginY + nav.Height * nav.CellSize * .5f;
                _viewHalf = fullViewHalf;
                _viewReady = true;
            }
            _viewHalf = Mathf.Min(_viewHalf, fullViewHalf);
            if (_viewHalf >= fullViewHalf - .001f)
            {
                _centerX = nav.OriginX + nav.Width * nav.CellSize * .5f;
                _centerY = nav.OriginY + nav.Height * nav.CellSize * .5f;
            }
            DrawHeader(world);
            RegisterOverlayInputRects();
            Fill(mapRect, new Color(.93f, .89f, .78f, 1f));
            GUI.BeginGroup(mapRect);
            var local = new Rect(0, 0, mapRect.width, mapRect.height);
            var projection = new SurfaceWorldMapViewportProjection(local, _centerX, _centerY, _viewHalf);
            DrawSurface(local, projection, world, nav);
            var pointerAvailable = TryResolvePointerWorld(local, mapRect, projection, out var pointerWorld);
            HandleMapInput(local, mapRect, projection, world, nav);
            DrawCoordinateReference(local, projection, world, nav, pointerAvailable, pointerWorld);
            GUI.EndGroup();
            DrawFactionLegend(legendRect);
            GUI.Label(new Rect(16, Screen.height - FooterHeight + 3, Screen.width - 32, 26), _status, _body);
            if (_statusIsError || _pendingRoute != null)
            {
                var notice = new Rect(16f, HeaderHeight + 8f,
                    Mathf.Min(740f, Screen.width - 32f), 42f);
                Fill(notice, _statusIsError
                    ? new Color(.27f, .07f, .06f, .96f)
                    : new Color(.06f, .17f, .23f, .96f));
                GUI.Label(new Rect(notice.x + 10f, notice.y + 3f,
                    notice.width - 20f, notice.height - 6f), _status, _body);
            }
            DrawRosterPanels(world);
            if (!string.IsNullOrEmpty(_selectedSiteId) || !string.IsNullOrEmpty(_selectedSquadId))
                DrawInspect(world);
            HostUiHitTest.EndFrame();
        }

        void DrawHeader(SimulationWorld world)
        {
            GUI.Label(new Rect(16, 8, 210, 34), "连续世界地图", _title);
            if (GUI.Button(new Rect(Screen.width - 88, 12, 72, 30), "关闭")) CloseWithLocalMapTakeover();
            _showTerritory = GUI.Toggle(new Rect(230, 14, 105, 24), _showTerritory, "势力范围", _toggle);
            _showGeography = GUI.Toggle(new Rect(340, 14, 105, 24), _showGeography, "地理层", _toggle);
            _showGrid = GUI.Toggle(new Rect(450, 14, 105, 24), _showGrid, "坐标网格", _toggle);
            var openingLocked = HuangcunOpeningAccess.IsLocked(world);
            if (openingLocked)
            {
                _factionPanel?.Close();
                GUI.Label(new Rect(570, 44, 480, 24), "取得荒村后，解锁势力与外界。", _body);
            }
            var clicked = _toolbar.Draw(570, 12, _body, !openingLocked);
            if (clicked != HostGlobalStrategicToolbar.ModuleId.None)
            {
                EnsurePanels();
                if (clicked == HostGlobalStrategicToolbar.ModuleId.Character)
                {
                    var wasOpen = _characterPanel.IsOpen;
                    _factionPanel.Close();
                    if (wasOpen) _characterPanel.Close(); else _characterPanel.Open();
                }
                else if (clicked == HostGlobalStrategicToolbar.ModuleId.FactionDiplomacy)
                {
                    var wasOpen = _factionPanel.IsOpen;
                    _characterPanel.Close();
                    if (wasOpen) _factionPanel.Close(); else _factionPanel.Open();
                }
            }
            _toolbar.SyncFromPanels(
                _characterPanel?.IsOpen == true,
                _factionPanel?.IsOpen == true);
            if ((world.PlayerPartyTravel.IsMoving || _pendingRoute != null) &&
                GUI.Button(new Rect(16, 44, 95, 27),
                    _pendingRoute != null ? "取消规划" : "停止旅行"))
            {
                if (_pendingRoute != null)
                {
                    _pendingRoute.Cancellation.Cancel();
                    _discardPendingRoute = true;
                    _hasQueuedRoute = false;
                }
                if (world.PlayerPartyTravel.IsMoving)
                    PlayerPartySurfaceTravelService.Cancel(world);
                _acceptedTravelPlanVersion = -1;
                _acceptedTravelActorId = EntityId.None;
                _acceptedTravelWasAirborne = false;
                _status = _pendingRoute != null ? "已取消路线规划" : "已停止旅行";
                _statusIsError = false;
            }
        }

        void DrawSurface(Rect local, SurfaceWorldMapViewportProjection projection,
            SimulationWorld world, SurfaceGroundNavigation nav)
        {
            ContinuousSurfaceWorldMapDefinition cache = null;
            bootstrap?.Session?.Registry?.TryGetContinuousSurfaceWorldMap(nav.SurfaceId, out cache);
            _renderer.Draw(local, projection, cache, nav);
            if (_showGrid) HostSurfaceWorldMapGridRenderer.Draw(local, projection, nav, Texture2D.whiteTexture);
            if (_showGeography) DrawGeography(local, projection, nav);
            if (_showTerritory)
                HostSurfaceActualControlRenderer.Draw(world, nav.SurfaceId, projection, Texture2D.whiteTexture);
            DrawRoute(projection, world);
            DrawSites(projection, world, nav);
            DrawHuangcunSpiritGate(projection, world, nav);
            DrawSquads(projection, world, nav);
            DrawPlayer(projection, world, nav);
        }

        void DrawGeography(Rect local, SurfaceWorldMapViewportProjection projection,
            SurfaceGroundNavigation nav)
        {
            if (Event.current.type != EventType.Repaint) return;
            var geography = bootstrap?.ContinuousOutdoorSurfaceRuntime?.ActiveGeography;
            if (geography == null && bootstrap?.Session?.Registry != null)
                bootstrap.Session.Registry.TryGetOutdoorSurfaceGeography(nav.SurfaceId, out geography);
            if (geography == null) return;
            // The authored centerlines are the map's display geometry. The exact road grid
            // remains in SurfaceGroundNavigation for movement, but has tens of thousands of
            // horizontal runs in this world and must not be sent through IMGUI each frame.
            foreach (var primitive in geography.MapPrimitives)
            {
                if (primitive.Kind != "roadPolyline") continue;
                var points = primitive.Points;
                var thickness = Mathf.Clamp(primitive.StrokeWidth * projection.Scale, 1f, 20f);
                for (var i = 0; i + 3 < points.Count; i += 2)
                {
                    var a = projection.ProjectWorld(points[i], points[i + 1]);
                    var b = projection.ProjectWorld(points[i + 2], points[i + 3]);
                    if (!Rect.MinMaxRect(Mathf.Min(a.x, b.x) - thickness,
                            Mathf.Min(a.y, b.y) - thickness,
                            Mathf.Max(a.x, b.x) + thickness,
                            Mathf.Max(a.y, b.y) + thickness).Overlaps(local)) continue;
                    DrawScaledSegment(a, b, thickness, new Color(.78f, .59f, .30f, .88f));
                }
            }
            foreach (var primitive in geography.MapPrimitives)
            {
                if (primitive.Kind == "roadPolyline") continue;
                var screen = projection.ProjectWorldRect(new Rect(primitive.WorldX,
                    primitive.WorldY, primitive.WorldWidth, primitive.WorldHeight));
                if (!screen.Overlaps(local)) continue;
                Fill(screen, primitive.Kind == "waterRegion" ? new Color(.10f, .38f, .72f, .76f) :
                    primitive.Kind == "bridge" ? new Color(.64f, .40f, .16f, .95f) :
                    new Color(.25f, .27f, .29f, .92f));
            }
            foreach (var landmark in geography.Landmarks)
            {
                var point = projection.ProjectWorld(landmark.WorldX, landmark.WorldY);
                if (local.Contains(point))
                    GUI.Label(new Rect(point.x - 70f, point.y - 18f, 140f, 20f),
                        landmark.Label, _body);
            }
        }

        static bool TryResolvePointerWorld(Rect local, Rect mapRect,
            SurfaceWorldMapViewportProjection projection, out Vector2 worldPosition)
        {
            worldPosition = default;
            var current = Event.current;
            if (current == null || !local.Contains(current.mousePosition) ||
                HostUiHitTest.ContainsCurrentGuiPoint(current.mousePosition + mapRect.position)) return false;
            return projection.TryScreenToWorld(current.mousePosition, out worldPosition);
        }

        void DrawCoordinateReference(Rect local, SurfaceWorldMapViewportProjection projection,
            SimulationWorld world, SurfaceGroundNavigation nav, bool pointerAvailable, Vector2 pointerWorld)
        {
            if (Event.current.type != EventType.Repaint) return;
            var bounds = projection.VisibleWorldBounds;
            DrawAxisTicks(local, projection, bounds);

            var pointerText = pointerAvailable
                ? "坐标  X " + FormatCoordinate(pointerWorld.x) + "  Y " + FormatCoordinate(pointerWorld.y)
                : "坐标  —";
            var playerText = "玩家坐标：不可用";
            var party = bootstrap?.Session?.PlayerParty;
            if (PlayerPartyWorldLocationQuery.TryResolve(world, party, out var resolved) &&
                string.Equals(resolved.SurfaceId, nav.SurfaceId, StringComparison.Ordinal))
                playerText = "玩家  X " + FormatCoordinate(resolved.WorldPosition.X) +
                             "  Y " + FormatCoordinate(resolved.WorldPosition.Y);

            var panel = new Rect(local.xMax - 278f, local.yMax - 54f, 270f, 46f);
            Fill(panel, new Color(.06f, .07f, .08f, .78f));
            GUI.Label(new Rect(panel.x + 8f, panel.y + 3f, panel.width - 16f, 20f), pointerText, _body);
            GUI.Label(new Rect(panel.x + 8f, panel.y + 22f, panel.width - 16f, 20f), playerText, _body);
        }

        void DrawAxisTicks(Rect local, SurfaceWorldMapViewportProjection projection, Rect bounds)
        {
            var color = new Color(.12f, .10f, .08f, .58f);
            var xInterval = SurfaceWorldMapViewportProjection.ChooseMajorInterval(bounds.width);
            var yInterval = SurfaceWorldMapViewportProjection.ChooseMajorInterval(bounds.height);
            var firstX = Mathf.Ceil(bounds.xMin / xInterval) * xInterval;
            var firstY = Mathf.Ceil(bounds.yMin / yInterval) * yInterval;
            for (var value = firstX; value <= bounds.xMax + xInterval * .001f; value += xInterval)
            {
                var point = projection.ProjectWorld(value, bounds.yMin);
                Fill(new Rect(point.x, local.yMax - 7f, 1f, 7f), color);
                var label = FormatTick(value);
                GUI.Label(new Rect(point.x - 28f, local.yMax - 26f, 56f, 18f), label,
                    _axisCenter);
            }
            for (var value = firstY; value <= bounds.yMax + yInterval * .001f; value += yInterval)
            {
                var point = projection.ProjectWorld(bounds.xMin, value);
                Fill(new Rect(local.xMin, point.y, 7f, 1f), color);
                GUI.Label(new Rect(local.xMin + 9f, point.y - 9f, 54f, 18f), FormatTick(value),
                    _axisLeft);
            }
        }

        static string FormatCoordinate(float value) => value.ToString("0.0", CultureInfo.InvariantCulture);
        static string FormatTick(float value)
        {
            if (Mathf.Abs(value) < .0001f) value = 0f;
            return value.ToString(Mathf.Abs(value) < 10f && value != Mathf.Round(value) ? "0.#" : "0",
                CultureInfo.InvariantCulture);
        }

        void DrawSites(SurfaceWorldMapViewportProjection projection, SimulationWorld world,
            SurfaceGroundNavigation nav)
        {
            _siteHits.Clear();
            foreach (var pair in world.Strategic.Sites.Sites)
            {
                var site = pair.Value;
                if (site == null) continue;
                if (!string.IsNullOrEmpty(site.CoreSurfaceId) &&
                    !string.Equals(site.CoreSurfaceId, nav.SurfaceId, StringComparison.Ordinal))
                    continue;
                if (!site.IsCoreActive || !site.HasContinuousCore ||
                    !string.Equals(site.CoreSurfaceId, nav.SurfaceId, StringComparison.Ordinal))
                {
                    if (!site.IsRuntimeCreated && _missingSiteCoreReported.Add(site.SiteId))
                        Debug.LogError("[WorldMapSiteCoreMissing] SiteId=" + site.SiteId);
                    continue;
                }

                var sizeCells = site.CoreIsRemovable
                    ? FactionFlagMarkerSizeCells : CouncilHallMarkerSizeCells;
                var sizeWorld = nav.CellSize * sizeCells;
                var markerWorld = new Rect(site.CoreWorldX - sizeWorld * .5f,
                    site.CoreWorldY - sizeWorld * .5f, sizeWorld, sizeWorld);
                var marker = projection.ProjectWorldRect(markerWorld);
                var highlighted = _hoverFaction != null && !site.CoreIsRemovable &&
                    string.Equals(_hoverFaction, site.OwnerFactionId ?? string.Empty, StringComparison.Ordinal);
                if (highlighted)
                {
                    var enlarged = Mathf.Max(marker.width * 1.65f, 18f);
                    marker = new Rect(marker.center.x - enlarged * .5f,
                        marker.center.y - enlarged * .5f, enlarged, enlarged);
                }
                var fontWorld = nav.CellSize * SiteLabelHeightCells;
                var fontSize = Mathf.Clamp(Mathf.RoundToInt(fontWorld * projection.Scale), 11, 48);
                if (highlighted) fontSize = Mathf.Min(48, fontSize + 2);
                var style = SiteLabelStyle(fontSize);
                _siteLabelContent.text = site.DisplayName;
                var measured = SiteLabelSize(site.DisplayName, fontSize, style);
                var labelHeight = Mathf.Ceil(Mathf.Max(measured.y + 4f, fontSize * 1.5f + 4f));
                var anchor = projection.ProjectWorld(
                    site.CoreWorldX + sizeWorld * .5f + nav.CellSize * SiteLabelGapCells,
                    site.CoreWorldY);
                var label = new Rect(anchor.x, anchor.y - labelHeight * .5f,
                    Mathf.Ceil(measured.x + 8f), labelHeight);
                if (highlighted) label.x = marker.xMax + 4f;
                if (!marker.Overlaps(projection.MapRect) && !label.Overlaps(projection.MapRect))
                    continue;
                if (Event.current.type == EventType.Repaint)
                {
                    var selected = string.Equals(_selectedSiteId, site.SiteId, StringComparison.Ordinal);
                    if (highlighted)
                    {
                        StrategicFactionCatalog.MapTint(_hoverFaction, out var r, out var g, out var b);
                        Fill(new Rect(marker.x - 3f, marker.y - 3f, marker.width + 6f, marker.height + 6f),
                            new Color(r, g, b, .85f));
                        Fill(marker, new Color(.96f, .93f, .8f, 1f));
                    }
                    if (site.CoreIsRemovable) DrawSiteFlag(marker, selected);
                    else DrawCouncilHall(marker, selected || highlighted);
                    GUI.Label(label, _siteLabelContent, style);
                }
                _siteHits.Add((site.SiteId, Rect.MinMaxRect(
                    Mathf.Min(marker.xMin, label.xMin), Mathf.Min(marker.yMin, label.yMin),
                    Mathf.Max(marker.xMax, label.xMax), Mathf.Max(marker.yMax, label.yMax))));
            }
        }

        void DrawHuangcunSpiritGate(SurfaceWorldMapViewportProjection projection,
            SimulationWorld world, SurfaceGroundNavigation nav)
        {
            var area = world?.OpeningArea;
            if (Event.current.type != EventType.Repaint || area == null || !area.IsConfigured ||
                !string.Equals(area.SurfaceId, nav.SurfaceId, StringComparison.Ordinal) ||
                nav.CellSize * projection.Scale < 2.5f) return;
            var at = projection.ProjectWorld(area.MaxX - area.CellSize * 30f,
                (area.MinY + area.MaxY) * .5f);
            if (projection.MapRect.Contains(at))
            {
                var marker = new Rect(at.x - 5f, at.y - 5f, 10f, 10f);
                Fill(marker, HuangcunOpeningAccess.IsLocked(world)
                    ? new Color(.28f, .49f, .52f, .95f)
                    : new Color(.19f, .88f, .94f, .98f));
                GUI.Label(new Rect(at.x + 8f, at.y - 11f, 116f, 22f), "古传送灵阵", _body);
            }
            var outside = projection.ProjectWorld(area.MaxX + area.CellSize * 125f,
                (area.MinY + area.MaxY) * .5f);
            if (projection.MapRect.Contains(outside))
            {
                Fill(new Rect(outside.x - 5f, outside.y - 5f, 10f, 10f),
                    new Color(.19f, .88f, .94f, .98f));
                GUI.Label(new Rect(outside.x + 8f, outside.y - 11f, 116f, 22f),
                    "荒村返程灵阵", _body);
            }
        }

        GUIStyle SiteLabelStyle(int fontSize)
        {
            if (_siteLabelStyles.TryGetValue(fontSize, out var style)) return style;
            style = new GUIStyle(_body) { fontSize = fontSize, wordWrap = false,
                alignment = TextAnchor.MiddleLeft };
            style.normal.textColor = new Color(.12f, .10f, .08f);
            _siteLabelStyles.Add(fontSize, style);
            return style;
        }

        Vector2 SiteLabelSize(string text, int fontSize, GUIStyle style)
        {
            var key = (text, fontSize);
            if (_siteLabelSizes.TryGetValue(key, out var size)) return size;
            _siteLabelContent.text = text;
            size = style.CalcSize(_siteLabelContent);
            _siteLabelSizes.Add(key, size);
            return size;
        }

        static void DrawCouncilHall(Rect r, bool selected)
        {
            var wall = selected ? new Color(.18f, .85f, .92f) : new Color(.92f, .73f, .40f);
            var roof = selected ? new Color(.07f, .43f, .52f) : new Color(.42f, .19f, .12f);
            Fill(new Rect(r.x + r.width * .17f, r.y + r.height * .43f,
                r.width * .66f, r.height * .50f), wall);
            DrawScaledSegment(new Vector2(r.x + r.width * .08f, r.y + r.height * .48f),
                new Vector2(r.center.x, r.y + r.height * .10f), r.width * .16f, roof);
            DrawScaledSegment(new Vector2(r.center.x, r.y + r.height * .10f),
                new Vector2(r.xMax - r.width * .08f, r.y + r.height * .48f), r.width * .16f, roof);
            Fill(new Rect(r.center.x - r.width * .09f, r.y + r.height * .65f,
                r.width * .18f, r.height * .28f), new Color(.26f, .16f, .10f));
        }

        static void DrawSiteFlag(Rect r, bool selected)
        {
            Fill(new Rect(r.x + r.width * .28f, r.y + r.height * .10f,
                r.width * .09f, r.height * .84f), new Color(.26f, .20f, .14f));
            Fill(new Rect(r.x + r.width * .37f, r.y + r.height * .14f,
                r.width * .52f, r.height * .34f),
                selected ? new Color(.18f, .85f, .92f) : new Color(.80f, .20f, .18f));
        }

        static void DrawScaledSegment(Vector2 from, Vector2 to, float thickness, Color color)
        {
            var delta = to - from;
            if (delta.sqrMagnitude < .0001f) return;
            var matrix = GUI.matrix;
            var previous = GUI.color;
            GUI.color = color;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, from);
            GUI.DrawTexture(new Rect(from.x, from.y - thickness * .5f,
                delta.magnitude, thickness), Texture2D.whiteTexture);
            GUI.matrix = matrix;
            GUI.color = previous;
        }

        void DrawSquads(SurfaceWorldMapViewportProjection projection, SimulationWorld world,
            SurfaceGroundNavigation nav)
        {
            _squadHits.Clear();
            var sizeWorld = nav.CellSize * SquadMarkerSizeCells;
            foreach (var pair in world.Strategic.SquadWorldMotions.Motions)
            {
                if (!SquadWorldMotionService.TryGetActiveNpcSquadAuthority(
                        world, pair.Key, out _, out var motion)) continue;
                var rect = projection.ProjectWorldRect(new Rect(
                    motion.WorldPosition.X - sizeWorld * .5f,
                    motion.WorldPosition.Y - sizeWorld * .5f, sizeWorld, sizeWorld));
                if (Event.current.type == EventType.Repaint && rect.Overlaps(projection.MapRect))
                    Fill(rect, string.Equals(_selectedSquadId, pair.Key, StringComparison.Ordinal)
                        ? Color.cyan : new Color(.95f, .32f, .25f));
                _squadHits.Add((pair.Key, rect));
            }
        }

        void DrawPlayer(SurfaceWorldMapViewportProjection projection, SimulationWorld world,
            SurfaceGroundNavigation nav)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (world.PlayerPartyTravel == null || !world.PlayerPartyTravel.HasPosition) return;
            var pos = world.PlayerPartyTravel.WorldPosition;
            var center = projection.ProjectWorld(pos.X, pos.Y);
            if (!projection.MapRect.Contains(center)) return;
            var half = PlayerMarkerScreenSize * .5f;
            // This is deliberately the only map marker sized in screen pixels. The dark halo
            // separates the active party from small Site and NPC marks at every zoom level.
            Fill(new Rect(center.x - half - 3f, center.y - half - 3f,
                PlayerMarkerScreenSize + 6f, PlayerMarkerScreenSize + 6f),
                new Color(.06f, .14f, .16f, .96f));
            Fill(new Rect(center.x - half, center.y - half,
                PlayerMarkerScreenSize, PlayerMarkerScreenSize),
                new Color(1f, .86f, .28f));
            Fill(new Rect(center.x - 7f, center.y - 7f, 14f, 14f),
                new Color(.08f, .56f, .48f));
            Fill(new Rect(center.x - 2f, center.y - 2f, 4f, 4f), Color.white);
            var labelX = center.x + half + 7f;
            if (labelX + 84f > projection.MapRect.xMax)
                labelX = center.x - half - 91f;
            var label = new Rect(labelX, center.y - 13f, 84f, 26f);
            Fill(label, new Color(.06f, .14f, .16f, .94f));
            GUI.Label(label, "玩家小队", _playerLabel);
        }

        void DrawRoute(SurfaceWorldMapViewportProjection projection, SimulationWorld world)
        {
            if (Event.current.type != EventType.Repaint) return;
            var motion = world.PlayerPartyTravel;
            if (motion == null || !motion.IsMoving || motion.ContinuousSurfaceRoute == null) return;
            var route = motion.ContinuousSurfaceRoute;
            for (var i = Math.Max(1, motion.ContinuousSurfaceRouteIndex); i < route.Count; i++)
            {
                var a = route[i - 1];
                var b = route[i];
                DrawLine(projection.ProjectWorld(a.X, a.Y), projection.ProjectWorld(b.X, b.Y),
                    new Color(.2f, .9f, 1f));
            }
        }

        void RegisterOverlayInputRects()
        {
            HostUiHitTest.Block(FactionLegendRect());
            HostUiHitTest.Block(new Rect(0f, 0f, Screen.width, HeaderHeight));
            HostUiHitTest.Block(new Rect(0f, Screen.height - FooterHeight,
                Screen.width, FooterHeight));
            if (_characterPanel?.IsOpen == true || _factionPanel?.IsOpen == true)
                HostUiHitTest.Block(HostStrategicRosterPanelLayout.Compute(Screen.width, Screen.height));
            if (!string.IsNullOrEmpty(_selectedSiteId) || !string.IsNullOrEmpty(_selectedSquadId))
                HostUiHitTest.Block(InspectRect());
            if (_statusIsError || _pendingRoute != null)
                HostUiHitTest.Block(new Rect(16f, HeaderHeight + 8f,
                    Mathf.Min(740f, Screen.width - 32f), 42f));
        }

        static Rect InspectRect() => new Rect(Screen.width - 330, HeaderHeight + 8, 312, 132);

        int LegendColumns => Mathf.Max(1, Mathf.FloorToInt((Screen.width - 48f) / 190f));

        Rect FactionLegendRect()
        {
            var rows = (_legendRows.Count + LegendColumns - 1) / LegendColumns;
            var height = 28f + (_showFactionLegend ? rows * 27f + 8f : 0f);
            return new Rect(12f, Screen.height - FooterHeight - height,
                Screen.width - 24f, height);
        }

        void RefreshFactionLegend(SimulationWorld world, string surfaceId)
        {
            var sameWorld = ReferenceEquals(world, _legendWorld) && _legendSurface == surfaceId;
            if (sameWorld && _legendFrame == Time.frameCount) return;
            _legendWorld = world;
            _legendSurface = surfaceId;
            _legendFrame = Time.frameCount;
            _legendCounts.Clear();
            var player = world.Strategic.PlayerFactionId;
            if (!string.IsNullOrEmpty(player)) _legendCounts[player] = 0;
            _legendCounts[string.Empty] = 0;
            var total = 0;
            foreach (var pair in world.Strategic.Sites.Sites)
            {
                var site = pair.Value;
                if (site == null || !site.IsCoreActive || site.CoreIsRemovable || !site.HasContinuousCore ||
                    !string.Equals(site.CoreSurfaceId, surfaceId, StringComparison.Ordinal)) continue;
                var owner = site.OwnerFactionId ?? string.Empty;
                _legendCounts.TryGetValue(owner, out var count);
                _legendCounts[owner] = count + 1;
                total++;
            }
            var changed = !sameWorld || total != _legendTotal || _legendRows.Count != _legendCounts.Count;
            if (!changed)
                foreach (var row in _legendRows)
                    if (!_legendCounts.TryGetValue(row.Id, out var count) || count != row.Count)
                    { changed = true; break; }
            if (!changed) return;
            _legendTotal = total;
            _legendTitle = "势力图例 · 当前地图 " + total + " 城镇 · 悬停高亮 / 双击主城";
            _legendRows.Clear();
            foreach (var pair in _legendCounts)
            {
                StrategicFactionCatalog.MapTint(pair.Key, out var r, out var g, out var b);
                var name = pair.Key.Length == 0 ? "无归属" : StrategicFactionCatalog.DisplayName(pair.Key);
                if (pair.Key == player) name += "（玩家）";
                _legendRows.Add((pair.Key, pair.Value, name + "  " + pair.Value + " 城", new Color(r, g, b)));
            }
            _legendRows.Sort((a, b) =>
            {
                if (a.Id == b.Id) return 0;
                if (a.Id == player) return -1;
                if (b.Id == player) return 1;
                if (a.Id.Length == 0) return 1;
                if (b.Id.Length == 0) return -1;
                var sa = StrategicFactionCatalog.TryGetInstalled(a.Id, out var fa) ? fa.SortOrder : int.MaxValue;
                var sb = StrategicFactionCatalog.TryGetInstalled(b.Id, out var fb) ? fb.SortOrder : int.MaxValue;
                return sa != sb ? sa.CompareTo(sb) : string.CompareOrdinal(a.Id, b.Id);
            });
        }

        Rect LegendItemRect(Rect rect, int index)
        {
            var width = (rect.width - 24f) / LegendColumns;
            return new Rect(rect.x + 12f + index % LegendColumns * width,
                rect.y + 30f + index / LegendColumns * 27f, width, 24f);
        }

        void HandleLegendInput(Rect rect, SimulationWorld world, SurfaceGroundNavigation nav)
        {
            _hoverFaction = null;
            if (!_showFactionLegend || _characterPanel?.IsOpen == true || _factionPanel?.IsOpen == true)
            { _lastLegendClick = null; return; }
            var e = Event.current;
            for (var i = 0; i < _legendRows.Count; i++)
            {
                if (!LegendItemRect(rect, i).Contains(e.mousePosition)) continue;
                var id = _legendRows[i].Id;
                _hoverFaction = id;
                if (e.type != EventType.MouseDown || e.button != 0) return;
                var twice = _lastLegendClick == id && Time.realtimeSinceStartup - _lastLegendClickTime < .4f;
                _lastLegendClick = twice ? null : id;
                _lastLegendClickTime = Time.realtimeSinceStartup;
                e.Use();
                if (twice) FocusFactionCapital(id, world, nav);
                return;
            }
            if (e.type == EventType.MouseDown) _lastLegendClick = null;
        }

        void FocusFactionCapital(string factionId, SimulationWorld world, SurfaceGroundNavigation nav)
        {
            _statusIsError = false;
            if (string.IsNullOrEmpty(factionId)) { _status = "无归属城镇没有统一主城。"; return; }
            var preferred = StrategicFactionCatalog.TryGetInstalled(factionId, out var faction)
                ? faction.MapCapitalSiteId : string.Empty;
            WorldSite target = null;
            foreach (var pair in world.Strategic.Sites.Sites)
            {
                var site = pair.Value;
                if (site == null || !site.IsCoreActive || site.CoreIsRemovable || !site.HasContinuousCore ||
                    site.CoreSurfaceId != nav.SurfaceId || site.OwnerFactionId != factionId) continue;
                if (site.SiteId == preferred) { target = site; break; }
                if (target == null || site.CoreLevel > target.CoreLevel ||
                    (site.CoreLevel == target.CoreLevel && string.CompareOrdinal(site.SiteId, target.SiteId) < 0)) target = site;
            }
            if (target == null) { _status = "该势力在当前地图没有城镇。"; return; }
            _centerX = target.CoreWorldX;
            _centerY = target.CoreWorldY;
            _viewHalf = 12f;
            _viewReady = true;
            _selectedSiteId = target.SiteId;
            _selectedSquadId = string.Empty;
            _statusIsError = false;
            _status = target.SiteId == preferred ? "已聚焦主城：" + target.DisplayName :
                "原主城不属于该势力，已聚焦现有城镇：" + target.DisplayName;
        }

        void DrawFactionLegend(Rect rect)
        {
            Fill(rect, new Color(.055f, .105f, .12f, 1f));
            if (GUI.Button(new Rect(rect.x + 6f, rect.y + 2f, 28f, 24f),
                _showFactionLegend ? "▼" : "▶"))
            {
                var oldHeight = Mathf.Max(64f, rect.y - HeaderHeight - 4f);
                _showFactionLegend = !_showFactionLegend;
                var newHeight = Mathf.Max(64f, FactionLegendRect().y - HeaderHeight - 4f);
                _viewHalf *= newHeight / oldHeight;
            }
            GUI.Label(new Rect(rect.x + 40f, rect.y + 3f, rect.width - 48f, 23f), _legendTitle, _body);
            if (!_showFactionLegend) return;
            var columns = LegendColumns;
            var width = (rect.width - 24f) / columns;
            for (var i = 0; i < _legendRows.Count; i++)
            {
                var row = _legendRows[i];
                if (_hoverFaction == row.Id)
                    Fill(LegendItemRect(rect, i), new Color(row.Tint.r, row.Tint.g, row.Tint.b, .25f));
                var x = rect.x + 12f + (i % columns) * width;
                var y = rect.y + 30f + (i / columns) * 27f;
                Fill(new Rect(x, y + 3f, 16f, 16f), row.Tint);
                GUI.Label(new Rect(x + 23f, y, width - 27f, 24f), row.Label, _body);
            }
        }

        void HandleMapInput(Rect local, Rect mapRect, SurfaceWorldMapViewportProjection projection,
            SimulationWorld world, SurfaceGroundNavigation nav)
        {
            var e = Event.current;
            if (e == null || !local.Contains(e.mousePosition)) return;
            // BeginGroup makes mousePosition local. Convert once to the GUI screen space used
            // by every overlay rect; map input must never consume an overlay's mouse/scroll event.
            var guiPoint = e.mousePosition + mapRect.position;
            if (HostUiHitTest.ContainsCurrentGuiPoint(guiPoint))
            {
                if (_panning && (e.type == EventType.MouseDrag ||
                                 (e.type == EventType.MouseUp && e.button == 2)))
                    _panning = false;
                return;
            }
            if (e.type == EventType.ScrollWheel)
            {
                var fullViewHalf = FullSurfaceViewHalf(local, nav);
                _viewHalf = Mathf.Clamp(_viewHalf * (e.delta.y > 0 ? 1.15f : .87f),
                    MinimumViewHalf, fullViewHalf);
                if (_viewHalf >= fullViewHalf - .001f)
                {
                    _centerX = nav.OriginX + nav.Width * nav.CellSize * .5f;
                    _centerY = nav.OriginY + nav.Height * nav.CellSize * .5f;
                }
                e.Use();
                return;
            }
            if (e.type == EventType.MouseDown && e.button == 2)
            {
                _panning = true;
                _panLast = e.mousePosition;
                e.Use();
                return;
            }
            if (_panning && e.type == EventType.MouseDrag)
            {
                _centerX -= (e.mousePosition.x - _panLast.x) / projection.Scale;
                _centerY += (e.mousePosition.y - _panLast.y) / projection.Scale;
                _panLast = e.mousePosition;
                e.Use();
                return;
            }
            if (_panning && e.type == EventType.MouseUp && e.button == 2)
            {
                _panning = false;
                e.Use();
                return;
            }
            if (e.type != EventType.MouseDown) return;
            if (e.button == 0)
            {
                foreach (var hit in _squadHits)
                    if (hit.Rect.Contains(e.mousePosition))
                    {
                        _selectedSquadId = hit.Id;
                        _selectedSiteId = string.Empty;
                        _status = "已选中 NPC 小队（只读）";
                        _statusIsError = false;
                        e.Use(); return;
                    }
                foreach (var hit in _siteHits)
                    if (hit.Rect.Contains(e.mousePosition))
                    {
                        _selectedSiteId = hit.Id;
            _selectedSquadId = string.Empty;
                        _status = "已选中地点：" + SiteDisplayName(world, hit.Id);
                        _statusIsError = false;
                        e.Use(); return;
                    }
            _selectedSquadId = string.Empty;
                _selectedSiteId = string.Empty;
                _status = "已选中玩家小队";
                _statusIsError = false;
                e.Use();
            }
            else if (e.button == 1)
            {
                if (!string.IsNullOrEmpty(_selectedSquadId))
                {
                    ShowRouteFailure(world, world.PlayerPartyTravel.WorldPosition,
                        "NPC 小队仅供查看；请先单击空白处取消选中，再右键规划玩家路线。");
                    e.Use(); return;
                }
                var party = bootstrap?.Session?.PlayerParty;
                if (party == null || !party.HasActive) return;
                var siteId = string.Empty;
                var worldPoint = projection.ScreenToWorld(e.mousePosition);
                foreach (var hit in _siteHits)
                    if (hit.Rect.Contains(e.mousePosition) &&
                        world.SurfaceGround.TryResolveSiteArrival(hit.Id, out _, out var arrival))
                    {
                        siteId = hit.Id;
                        worldPoint = new Vector2(arrival.X, arrival.Y);
                        break;
                    }
                PlanSurfaceRoute(world, nav,
                    new WorldVec2(worldPoint.x, worldPoint.y), siteId);
                e.Use();
            }
        }

        void PlanSurfaceRoute(SimulationWorld world, SurfaceGroundNavigation nav,
            WorldVec2 destination, string siteId)
        {
            _statusIsError = false;
            var party = bootstrap?.Session?.PlayerParty;
            var motion = world?.PlayerPartyTravel;
            if (party == null || !party.HasActive || motion == null || !motion.HasPosition ||
                nav == null || !nav.Contains(destination.X, destination.Y))
            {
                ShowRouteFailure(world, destination, "目标不在当前连续世界地面范围内。");
                return;
            }
            if (!HuangcunOpeningAccess.AllowsPosition(
                    world, motion.SurfaceId, destination.X, destination.Y))
            {
                ShowRouteFailure(world, destination, HuangcunOpeningAccess.BlockedMessage);
                return;
            }

            if (_pendingRoute != null)
            {
                _pendingRoute.Cancellation.Cancel();
                _discardPendingRoute = false;
                _hasQueuedRoute = true;
                _queuedDestination = destination;
                _queuedSiteId = siteId;
                _status = "正在规划新目标，请稍候…";
                return;
            }

            var actor = party.ActiveCharacterId;
            var airborne = XianXia.Core.Combat.FlightService.IsAirborne(world, actor);
            if (airborne || !nav.IsWalkable(destination.X, destination.Y) ||
                nav.IsSegmentWalkable(motion.WorldPosition.X, motion.WorldPosition.Y,
                    destination.X, destination.Y))
            {
                FinishSynchronousRoute(world, party, nav, destination, siteId);
                return;
            }

            // Only immutable Surface cells are read on the worker. World/party/story state stays
            // on the main thread and is rechecked when the route is committed.
            var start = motion.WorldPosition;
            var cancellation = new CancellationTokenSource();
            _pendingRoute = new PendingRoute
            {
                World = world,
                Navigation = nav,
                Start = start,
                Destination = destination,
                Actor = actor,
                SiteId = siteId,
                Cancellation = cancellation,
                Work = Task.Run(() =>
                {
                    var points = new List<WorldVec2>(128);
                    var status = nav.TryFindRouteIsolated(start, destination, points,
                        cancellation.Token);
                    return (status, points);
                })
            };
            _status = "正在规划路线，地图可以继续拖动或缩放…";
        }

        void FinishPendingRoute()
        {
            var pending = _pendingRoute;
            if (pending == null || !pending.Work.IsCompleted) return;
            _pendingRoute = null;
            if (pending.Work.IsFaulted)
            {
                var ignoredException = pending.Work.Exception;
            }
            pending.Cancellation.Dispose();
            if (_discardPendingRoute)
            {
                _discardPendingRoute = false;
                return;
            }
            if (_hasQueuedRoute)
            {
                var destination = _queuedDestination;
                var siteId = _queuedSiteId;
                _hasQueuedRoute = false;
                if (bootstrap?.Session?.IsInitialized == true)
                    PlanSurfaceRoute(bootstrap.Session.World,
                        bootstrap.Session.World.SurfaceGround.Active, destination, siteId);
                return;
            }
            if (bootstrap?.Session?.IsInitialized != true ||
                !ReferenceEquals(bootstrap.Session.World, pending.World) ||
                !ReferenceEquals(pending.World.SurfaceGround.Active, pending.Navigation))
                return;
            if (pending.Work.IsFaulted || pending.Work.IsCanceled)
            {
                ShowRouteFailure(pending.World, pending.Destination,
                    "路线规划失败，请重新选择目标。");
                return;
            }
            var planned = pending.Work.Result;
            if (planned.Status != SurfaceGroundRouteStatus.Found)
            {
                ShowRouteFailure(pending.World, pending.Destination,
                    planned.Status == SurfaceGroundRouteStatus.BlockedGoal
                        ? "目标地面不可通行。"
                        : "当前地面无法找到通往目标的路线。");
                return;
            }
            var result = PlayerPartySurfaceTravelService.BeginTravelWithPreparedRoute(
                pending.World, bootstrap.Session.PlayerParty, pending.Destination,
                pending.SiteId, pending.Navigation.CellSize * .75f,
                planned.Points, pending.Start, pending.Navigation.SurfaceId,
                pending.Actor, expectedAirborne: false);
            if (result.IsFailure)
                ShowRouteFailure(pending.World, pending.Destination, result.Error.Message);
            else
                ShowRouteSuccess(pending.SiteId);
        }

        void FinishSynchronousRoute(SimulationWorld world, PlayerPartyRuntime party,
            SurfaceGroundNavigation nav, WorldVec2 destination, string siteId)
        {
            var result = PlayerPartySurfaceTravelService.BeginTravel(
                world, party, destination, siteId, nav.CellSize * .75f);
            if (result.IsFailure)
                ShowRouteFailure(world, destination, result.Error.Message);
            else
                ShowRouteSuccess(siteId);
        }

        void ShowRouteSuccess(string siteId)
        {
            _statusIsError = false;
            var motion = bootstrap?.Session?.World?.PlayerPartyTravel;
            _acceptedTravelPlanVersion = open && motion != null && motion.IsMoving
                ? motion.TravelPlanVersion : -1;
            _acceptedTravelActorId = _acceptedTravelPlanVersion >= 0
                ? bootstrap.Session.PlayerParty.ActiveCharacterId : EntityId.None;
            _acceptedTravelWasAirborne = _acceptedTravelPlanVersion >= 0 &&
                XianXia.Core.Combat.FlightService.IsAirborne(
                    bootstrap.Session.World, _acceptedTravelActorId);
            _status = string.IsNullOrEmpty(siteId)
                ? "已规划前往地面位置，关闭地图后出发"
                : "已规划前往 " + SiteDisplayName(bootstrap?.Session?.World, siteId) + "，关闭地图后出发";
            if (!open)
                bootstrap?.PlayerPartyController?.ResumeSurfaceTravelAfterPlanning();
        }

        void ReportInterruptedRoute()
        {
            if (!open || _acceptedTravelPlanVersion < 0 ||
                bootstrap?.Session?.IsInitialized != true)
                return;
            var motion = bootstrap.Session.World.PlayerPartyTravel;
            if (motion != null && motion.IsMoving)
                return;
            _acceptedTravelPlanVersion = -1;
            if (_statusIsError) return;
            _statusIsError = true;
            var actorChanged = bootstrap.Session.PlayerParty.ActiveCharacterId != _acceptedTravelActorId;
            var actorExists = bootstrap.Session.World.Entities.TryGet(
                _acceptedTravelActorId, out var actor);
            var exhausted = actorExists &&
                (XianXia.Core.Npc.PartyRestService.BlocksMovement(actor) ||
                 actor.TryGet<XianXia.Core.Npc.PersonalLifeComponent>(out var personal) &&
                 personal.Energy <= 0);
            var flightEnded = _acceptedTravelWasAirborne && actorExists &&
                !XianXia.Core.Combat.FlightService.IsAirborne(actor);
            _status = actorChanged ? "主控已切换，原旅行路线已取消。" :
                exhausted ? "主控精力耗尽，旅行已中断；休息后重新选择目标。" :
                flightEnded ? "斗气化翼已结束，飞行路线已取消；请落地后重新规划。" :
                "旅行已被中断，路线已取消。请确认主控状态后重新选择目标。";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[WorldMapTravelInterrupted] PlanVersion=" +
                (motion != null ? motion.TravelPlanVersion.ToString() : "missing") +
                " Position=" + (motion != null ? motion.WorldPosition.ToString() : "missing") +
                " Active=" + bootstrap.Session.PlayerParty.ActiveCharacterId +
                " AcceptedActor=" + _acceptedTravelActorId +
                " WasAirborne=" + _acceptedTravelWasAirborne +
                " Exhausted=" + exhausted +
                " FlightEnded=" + flightEnded +
                " EndedBy=" + (motion != null ? motion.LastTravelEndTrace : "missing"), this);
#endif
            _acceptedTravelActorId = EntityId.None;
            _acceptedTravelWasAirborne = false;
        }

        void ShowRouteFailure(SimulationWorld world, WorldVec2 destination, string message)
        {
            if (_pendingRoute != null)
            {
                _pendingRoute.Cancellation.Cancel();
                _discardPendingRoute = true;
                _hasQueuedRoute = false;
            }
            _statusIsError = true;
            _status = "无法出发：" + message;
            ReportSurfaceTravelPlanRejected(world,
                new Vector2(destination.X, destination.Y), message);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"),
         System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        void ReportSurfaceTravelPlanRejected(
            SimulationWorld world, Vector2 destination, string reason)
        {
            // Expected interaction rejection is already visible on the map. Logging a stack
            // for every exploratory far click can itself stall the Editor Console.
            if (string.Equals(reason, HuangcunOpeningAccess.BlockedMessage,
                    StringComparison.Ordinal) ||
                string.Equals(reason, HuangcunOpeningAccess.PatrolBlockedMessage,
                    StringComparison.Ordinal) ||
                reason != null && (reason.StartsWith("目标地面", StringComparison.Ordinal) ||
                    reason.StartsWith("目标不在", StringComparison.Ordinal) ||
                    reason.StartsWith("NPC 小队", StringComparison.Ordinal)) ||
                !_reportedTravelFailures.Add(reason ?? string.Empty))
                return;
            var motion = world?.PlayerPartyTravel;
            Debug.LogWarning(
                "[SurfaceTravelPlanRejected] reason=" + (reason ?? string.Empty) +
                " LocationKind=" + (motion != null ? motion.LocationKind.ToString() : "missing") +
                " SurfaceId=" + (motion?.SurfaceId ?? string.Empty) +
                " WorldPosition=" + (motion != null ? motion.WorldPosition.ToString() : "missing") +
                " Destination=(" + destination.x.ToString("0.###") + "," +
                destination.y.ToString("0.###") + ")");
        }

        static string SiteDisplayName(SimulationWorld world, string siteId)
        {
            return world != null && world.Strategic.Sites.TryGet(siteId, out var site) &&
                site != null && !string.IsNullOrEmpty(site.DisplayName) ? site.DisplayName : "未知地点";
        }

        void DrawInspect(SimulationWorld world)
        {
            var rect = InspectRect();
            Fill(rect, new Color(.08f, .11f, .13f, .93f));
            if (!string.IsNullOrEmpty(_selectedSiteId) &&
                world.Strategic.Sites.TryGet(_selectedSiteId, out var site) && site != null)
            {
                var owner = site.OwnerFactionId ?? string.Empty;
                var owned = owner.Length > 0;
                var ownerName = owned ? StrategicFactionCatalog.DisplayName(owner) : "无主城镇";
                var capital = owned && StrategicFactionCatalog.TryGetInstalled(owner, out var faction) &&
                    string.Equals(faction.MapCapitalSiteId, site.SiteId, StringComparison.Ordinal);
                StrategicFactionCatalog.MapTint(owner, out var r, out var g, out var b);
                GUI.Label(new Rect(rect.x + 10, rect.y + 8, rect.width - 20, 24),
                    site.DisplayName + "  ·  " + site.CoreLevel + " 级", _body);
                Fill(new Rect(rect.x + 10, rect.y + 38, 12, 12), new Color(r, g, b));
                GUI.Label(new Rect(rect.x + 29, rect.y + 32, rect.width - 39, 24),
                    "所属势力：" + ownerName, _body);
                GUI.Label(new Rect(rect.x + 10, rect.y + 58, rect.width - 20, 24),
                    capital ? "势力主城" : site.CoreIsRemovable ? "临时据点" : "固定城镇", _body);
                GUI.Label(new Rect(rect.x + 10, rect.y + 84, rect.width - 20, 24),
                    string.Format(CultureInfo.InvariantCulture, "坐标：X {0:0.0}  Y {1:0.0}",
                        site.CoreWorldX, site.CoreWorldY), _body);
            }
            else if (!string.IsNullOrEmpty(_selectedSquadId) &&
                     SquadWorldMotionService.TryGetActiveNpcSquadAuthority(
                         world, _selectedSquadId, out var squad, out _))
                GUI.Label(new Rect(rect.x + 10, rect.y + 8, rect.width - 20, 84),
                    "NPC 小队\n队长 " + EntityLabel(world, squad.LeaderCharacterId) +
                    " · " + squad.MemberCharacterIds.Count + " 人", _body);
        }

        void DrawRosterPanels(SimulationWorld world)
        {
            EnsurePanels();
            var rect = HostStrategicRosterPanelLayout.Compute(Screen.width, Screen.height);
            if (_characterPanel.IsOpen)
                _characterPanel.Draw(rect, world, bootstrap.Session.CharacterIds,
                    bootstrap.Session.PlayerParty, EntityLabel, FocusCameraOnSquad, FocusCameraOnNode,
                    FocusCameraOnWorldPosition);
            if (_factionPanel.IsOpen) _factionPanel.Draw(rect, world, bootstrap.PlayerPartyController);
        }

        void EnsurePanels()
        {
            if (_characterPanel == null) _characterPanel = new HostStrategicCharacterListPanel(_body, _title);
            if (_factionPanel == null) _factionPanel = new HostFactionDiplomacyOverviewPanel(_body, _title);
        }

        static bool TrySitePoint(SimulationWorld world, WorldSite site, out float x, out float y)
        {
            x = y = 0;
            if (site == null || !site.IsCoreActive || !site.HasContinuousCore ||
                !string.Equals(site.CoreSurfaceId, world.SurfaceGround.Active?.SurfaceId,
                    StringComparison.Ordinal)) return false;
            x = site.CoreWorldX;
            y = site.CoreWorldY;
            return true;
        }

        static string EntityLabel(SimulationWorld world, EntityId id)
        {
            if (!world.Entities.TryGet(id, out var entity) || entity == null) return id.Value.ToString();
            return string.IsNullOrWhiteSpace(entity.DisplayName)
                ? entity.DefinitionId.ToString() : entity.DisplayName;
        }

        static void Fill(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        static void DrawLine(Vector2 a, Vector2 b, Color color)
        {
            var delta = b - a;
            if (delta.sqrMagnitude < .01f) return;
            var matrix = GUI.matrix;
            var previous = GUI.color;
            GUI.color = color;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, a);
            GUI.DrawTexture(new Rect(a.x, a.y, delta.magnitude, 2f), Texture2D.whiteTexture);
            GUI.matrix = matrix;
            GUI.color = previous;
        }

        void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
            _title.normal.textColor = Color.white;
            _body = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            _body.normal.textColor = Color.white;
            _toggle = new GUIStyle(GUI.skin.toggle) { fontSize = 14 };
            _toggle.normal.textColor = Color.white;
            _toggle.onNormal.textColor = Color.white;
            _toggle.hover.textColor = Color.white;
            _toggle.onHover.textColor = Color.white;
            _toggle.active.textColor = Color.white;
            _toggle.onActive.textColor = Color.white;
            _axisCenter = new GUIStyle(_body) { fontSize = 11, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            _axisCenter.normal.textColor = new Color(.12f, .10f, .08f, .70f);
            _axisLeft = new GUIStyle(_axisCenter) { alignment = TextAnchor.MiddleLeft };
            _playerLabel = new GUIStyle(_body)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false
            };
        }
    }
}
