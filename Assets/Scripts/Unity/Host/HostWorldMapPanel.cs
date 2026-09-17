using System;
using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
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
        const float CouncilHallMarkerSizeCells = 30f;
        const float FactionFlagMarkerSizeCells = 26f;
        const float SiteLabelHeightCells = 24f;
        const float SiteLabelGapCells = 5f;
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] KeyCode toggleKey = KeyCode.M;
        [SerializeField] bool open;

        readonly HostSurfaceWorldMapRenderer _renderer = new HostSurfaceWorldMapRenderer();
        readonly HostGlobalStrategicToolbar _toolbar = new HostGlobalStrategicToolbar();
        readonly List<(string Id, Rect Rect)> _siteHits = new List<(string, Rect)>();
        readonly List<(string Id, Rect Rect)> _armyHits = new List<(string, Rect)>();
        readonly HashSet<ulong> _selected = new HashSet<ulong>();
        readonly List<Rect> _roadRects = new List<Rect>(256);
        readonly Dictionary<int, GUIStyle> _siteLabelStyles = new Dictionary<int, GUIStyle>();
        readonly HashSet<string> _missingSiteCoreReported = new HashSet<string>(StringComparer.Ordinal);
        string _roadCacheIdentity = string.Empty;
        HostStrategicCharacterListPanel _characterPanel;
        HostFactionDiplomacyOverviewPanel _factionPanel;
        GUIStyle _title;
        GUIStyle _body;
        GUIStyle _toggle;
        bool _showGrid = true;
        bool _showGeography = true;
        bool _showTerritory = true;
        bool _viewReady;
        bool _panning;
        Vector2 _panLast;
        float _centerX;
        float _centerY;
        float _viewHalf = 20f;
        string _selectedSiteId = string.Empty;
        string _selectedArmyId = string.Empty;
        string _status = string.Empty;

        public bool IsOpen => open;
        public void Bind(PlayableHostBootstrap host) => bootstrap = host;

        public static bool IsBlockedByBattlefield(SimulationWorld world)
        {
            if (world?.Strategic == null) return false;
            if (world.Strategic.Participants != null && world.Strategic.Participants.IsAutoSettlement)
                return false;
            return StrategicClockFreezeService.IsModalEncounter(world) ||
                   BattleOfferService.HasActiveManualEncounter(world);
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
            bootstrap?.InventoryPanel?.Close();
            bootstrap?.ConstructionPanel?.Close();
            bootstrap?.QuestJournal?.Close();
            open = true;
            _viewReady = false;
            _missingSiteCoreReported.Clear();
            HostInputGate.Acquire(InputOwner);
            bootstrap?.PlayerPartyController?.FreezeLocalVisibleTravelForPlanning();
        }

        public void Close() => CloseInternal(false);
        public void CloseWithLocalMapTakeover() => CloseInternal(true);
        void CloseInternal(bool resumeLocal)
        {
            open = false;
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
                    world.PlayerPartyTravel.ExecutionMode == PlayerPartyTravelExecutionMode.LocalVisible)
                    bootstrap.PlayerPartyController?.ResumeLocalVisibleTravelAfterPlanning();
                return;
            }
            if (bootstrap.ContinuousOutdoorSurfaceRuntime != null &&
                bootstrap.ContinuousOutdoorSurfaceRuntime.TryActivateAtCurrentWorldPosition())
                bootstrap.SurfaceExitZonePresenter?.Clear();
        }

        public void ClearSessionState()
        {
            Close();
            _selected.Clear();
            _selectedSiteId = string.Empty;
            _selectedArmyId = string.Empty;
            _status = string.Empty;
            _viewReady = false;
        }

        public void NotifyAfterBattleResolved(SimulationWorld world)
        {
            if (world == null) return;
            StrategicEncounterResolveService.NormalizePresenceAfterEncounterExit(world);
            RefreshStrategicPresentation(world);
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
                if (!LingeringBattlefieldPartyService.IsLivingForMacroOrder(world, candidate)) continue;
                id = candidate;
                return true;
            }
            return false;
        }

        public void SelectArrivedParty(IReadOnlyList<ulong> arrivedIds)
        {
            _selected.Clear();
            if (arrivedIds != null)
                foreach (var id in arrivedIds)
                    if (id != 0) _selected.Add(id);
            _selectedSiteId = string.Empty;
            _selectedArmyId = string.Empty;
            _status = "已选到站角色 " + _selected.Count + " 人";
        }

        public void FocusCameraOnArmy(string armyId)
        {
            var world = bootstrap?.Session?.World;
            if (world == null || string.IsNullOrEmpty(armyId) ||
                !world.Strategic.FormalArmies.TryGet(armyId, out var army) || army == null) return;
            if (ArmyWorldMapPresentation.TryResolveArmyWorldPoint(world, army, out var x, out var y))
                Focus(x, y);
            _selectedArmyId = armyId;
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
            _selectedArmyId = string.Empty;
            _status = "已定位 " + site.DisplayName;
        }

        void Focus(float x, float y)
        {
            _centerX = x;
            _centerY = y;
            _viewHalf = Mathf.Min(_viewHalf, MinimumViewHalf * 1.25f);
            _viewReady = true;
        }

        void Update()
        {
            if (open) HostInputGate.Acquire(InputOwner);
            if (open && bootstrap?.Session?.IsInitialized == true &&
                IsBlockedByBattlefield(bootstrap.Session.World))
                Close();
            if (Input.GetKeyDown(toggleKey)) Toggle();
        }

        void OnDisable()
        {
            open = false;
            HostInputGate.Release(InputOwner);
        }

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
            var mapRect = new Rect(12f, HeaderHeight, Screen.width - 24f,
                Mathf.Max(64f, Screen.height - HeaderHeight - FooterHeight));
            if (!_viewReady)
            {
                _centerX = nav.OriginX + nav.Width * nav.CellSize * .5f;
                _centerY = nav.OriginY + nav.Height * nav.CellSize * .5f;
                _viewHalf = Mathf.Max(MinimumViewHalf,
                    Mathf.Max(nav.Width * nav.CellSize * .55f,
                              nav.Height * nav.CellSize * .55f));
                _viewReady = true;
            }
            DrawHeader(world);
            RegisterOverlayInputRects();
            Fill(mapRect, new Color(.93f, .89f, .78f, 1f));
            GUI.BeginGroup(mapRect);
            var local = new Rect(0, 0, mapRect.width, mapRect.height);
            var projection = new SurfaceWorldMapViewportProjection(local, _centerX, _centerY, _viewHalf);
            DrawSurface(local, projection, world, nav);
            HandleMapInput(local, mapRect, projection, world, nav);
            GUI.EndGroup();
            GUI.Label(new Rect(16, Screen.height - FooterHeight + 3, Screen.width - 32, 26), _status, _body);
            DrawRosterPanels(world);
            if (!string.IsNullOrEmpty(_selectedSiteId) || !string.IsNullOrEmpty(_selectedArmyId))
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
            var clicked = _toolbar.Draw(570, 12, _body);
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
            _toolbar.SyncFromPanels(_characterPanel?.IsOpen == true, false,
                _factionPanel?.IsOpen == true);
            if (world.PlayerPartyTravel.IsMoving &&
                GUI.Button(new Rect(16, 44, 95, 27), "停止旅行"))
            {
                PlayerPartySurfaceTravelService.Cancel(world);
                _status = "已停止旅行";
            }
        }

        void DrawSurface(Rect local, SurfaceWorldMapViewportProjection projection,
            SimulationWorld world, SurfaceGroundNavigation nav)
        {
            ContinuousSurfaceWorldMapDefinition cache = null;
            bootstrap?.Session?.Registry?.TryGetContinuousSurfaceWorldMap(nav.SurfaceId, out cache);
            _renderer.Draw(local, projection, cache, nav);
            if (_showGrid) HostSurfaceWorldMapGridRenderer.Draw(local, projection, nav, Texture2D.whiteTexture);
            if (_showTerritory)
                HostSurfaceActualControlRenderer.Draw(world, nav.SurfaceId, projection, Texture2D.whiteTexture);
            if (_showGeography) DrawGeography(local, projection, nav);
            DrawRoute(projection, world);
            DrawSites(projection, world, nav);
            DrawArmies(projection, world);
            DrawPlayer(projection, world);
        }

        void DrawGeography(Rect local, SurfaceWorldMapViewportProjection projection,
            SurfaceGroundNavigation nav)
        {
            var identity = nav.SurfaceId + "|" + nav.SourceRevision + "|" + nav.SourceHash;
            if (!string.Equals(identity, _roadCacheIdentity, StringComparison.Ordinal))
            {
                _roadCacheIdentity = identity;
                _roadRects.Clear();
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
                        _roadRects.Add(new Rect(nav.OriginX + runStart * nav.CellSize,
                            nav.OriginY + y * nav.CellSize, (x - runStart) * nav.CellSize,
                            nav.CellSize));
                        runStart = -1;
                    }
                }
            }
            foreach (var road in _roadRects)
            {
                var screen = projection.ProjectWorldRect(road);
                if (screen.Overlaps(local)) Fill(screen, new Color(.78f, .59f, .30f, .88f));
            }
            var geography = bootstrap?.ContinuousOutdoorSurfaceRuntime?.ActiveGeography;
            if (geography == null) return;
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
                var selected = string.Equals(_selectedSiteId, site.SiteId, StringComparison.Ordinal);
                if (site.CoreIsRemovable) DrawSiteFlag(marker, selected);
                else DrawCouncilHall(marker, selected);

                var fontWorld = nav.CellSize * SiteLabelHeightCells;
                var labelWorld = new Rect(
                    site.CoreWorldX + sizeWorld * .5f + nav.CellSize * SiteLabelGapCells,
                    site.CoreWorldY - fontWorld * .5f,
                    fontWorld * Mathf.Max(1, site.DisplayName.Length + 1), fontWorld);
                var label = projection.ProjectWorldRect(labelWorld);
                var fontSize = Mathf.Clamp(Mathf.RoundToInt(fontWorld * projection.Scale), 1, 128);
                GUI.Label(label, site.DisplayName, SiteLabelStyle(fontSize));
                _siteHits.Add((site.SiteId, Rect.MinMaxRect(
                    Mathf.Min(marker.xMin, label.xMin), Mathf.Min(marker.yMin, label.yMin),
                    Mathf.Max(marker.xMax, label.xMax), Mathf.Max(marker.yMax, label.yMax))));
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

        void DrawArmies(SurfaceWorldMapViewportProjection projection, SimulationWorld world)
        {
            _armyHits.Clear();
            foreach (var pair in world.Strategic.FormalArmies.Armies)
            {
                var army = pair.Value;
                if (army == null || !ArmyWorldMapPresentation.TryResolveArmyWorldPoint(
                        world, army, out var x, out var y)) continue;
                var p = projection.ProjectWorld(x, y);
                var rect = new Rect(p.x - 7, p.y - 7, 14, 14);
                Fill(rect, string.Equals(_selectedArmyId, pair.Key, StringComparison.Ordinal)
                    ? Color.cyan : new Color(.95f, .32f, .25f));
                _armyHits.Add((pair.Key, new Rect(p.x - 11, p.y - 11, 22, 22)));
            }
        }

        void DrawPlayer(SurfaceWorldMapViewportProjection projection, SimulationWorld world)
        {
            if (world.PlayerPartyTravel == null || !world.PlayerPartyTravel.HasPosition) return;
            var pos = world.PlayerPartyTravel.WorldPosition;
            var p = projection.ProjectWorld(pos.X, pos.Y);
            Fill(new Rect(p.x - 10, p.y - 10, 20, 20), new Color(.25f, 1f, .36f));
            GUI.Label(new Rect(p.x + 12, p.y - 9, 120, 24), "玩家小队", _body);
        }

        void DrawRoute(SurfaceWorldMapViewportProjection projection, SimulationWorld world)
        {
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
            HostUiHitTest.Block(new Rect(0f, 0f, Screen.width, HeaderHeight));
            HostUiHitTest.Block(new Rect(0f, Screen.height - FooterHeight,
                Screen.width, FooterHeight));
            if (_characterPanel?.IsOpen == true || _factionPanel?.IsOpen == true)
                HostUiHitTest.Block(HostStrategicRosterPanelLayout.Compute(Screen.width, Screen.height));
            if (!string.IsNullOrEmpty(_selectedSiteId) || !string.IsNullOrEmpty(_selectedArmyId))
                HostUiHitTest.Block(InspectRect());
        }

        static Rect InspectRect() => new Rect(Screen.width - 310, HeaderHeight + 8, 292, 102);

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
                if (e.type == EventType.MouseUp && e.button == 2) _panning = false;
                return;
            }
            if (e.type == EventType.ScrollWheel)
            {
                _viewHalf = Mathf.Clamp(_viewHalf * (e.delta.y > 0 ? 1.15f : .87f),
                    MinimumViewHalf, Math.Max(nav.Width, nav.Height) * nav.CellSize);
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
                foreach (var hit in _armyHits)
                    if (hit.Rect.Contains(e.mousePosition))
                    {
                        _selectedArmyId = hit.Id;
                        _selectedSiteId = string.Empty;
                        _status = "已选中 NPC 小队（只读）";
                        e.Use(); return;
                    }
                foreach (var hit in _siteHits)
                    if (hit.Rect.Contains(e.mousePosition))
                    {
                        _selectedSiteId = hit.Id;
                        _selectedArmyId = string.Empty;
                        _status = "已选中地点 " + hit.Id;
                        e.Use(); return;
                    }
                _selectedArmyId = string.Empty;
                _selectedSiteId = string.Empty;
                _status = "已选中玩家小队";
                e.Use();
            }
            else if (e.button == 1)
            {
                if (!string.IsNullOrEmpty(_selectedArmyId))
                {
                    _status = "NPC 小队仅供查看";
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
                Result result = PlayerPartySurfaceTravelService.BeginTravel(
                    world, party, new(worldPoint.x, worldPoint.y), siteId,
                    nav.CellSize * .75f);
                if (result.IsFailure)
                    _status = result.Error.Message;
                else
                {
                    world.PlayerPartyTravel.SetExecutionMode(PlayerPartyTravelExecutionMode.LocalVisible);
                    _status = string.IsNullOrEmpty(siteId)
                        ? "已规划前往地面位置，关闭地图后出发"
                        : "已规划前往 " + siteId + "，关闭地图后出发";
                }
                e.Use();
            }
        }

        void DrawInspect(SimulationWorld world)
        {
            var rect = InspectRect();
            Fill(rect, new Color(.08f, .11f, .13f, .93f));
            if (!string.IsNullOrEmpty(_selectedSiteId) &&
                world.Strategic.Sites.TryGet(_selectedSiteId, out var site) && site != null)
                GUI.Label(new Rect(rect.x + 10, rect.y + 8, rect.width - 20, 84),
                    site.DisplayName + "\n" + site.SiteType + "  ·  " + site.OwnerFactionId, _body);
            else if (!string.IsNullOrEmpty(_selectedArmyId) &&
                     world.Strategic.FormalArmies.TryGet(_selectedArmyId, out var army) && army != null)
                GUI.Label(new Rect(rect.x + 10, rect.y + 8, rect.width - 20, 84),
                    "NPC 小队\n队长 " + EntityLabel(world, army.LeaderCharacterId) +
                    " · " + army.MemberCharacterIds.Count + " 人", _body);
        }

        void DrawRosterPanels(SimulationWorld world)
        {
            EnsurePanels();
            var rect = HostStrategicRosterPanelLayout.Compute(Screen.width, Screen.height);
            if (_characterPanel.IsOpen)
                _characterPanel.Draw(rect, world, bootstrap.Session.CharacterIds,
                    bootstrap.Session.PlayerParty, EntityLabel, FocusCameraOnArmy, FocusCameraOnNode);
            if (_factionPanel.IsOpen) _factionPanel.Draw(rect, world);
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
        }
    }
}
