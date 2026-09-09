using System;
using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Navigation;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Surface;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;
using XianXia.Data.Content;

namespace XianXia.Unity.Host
{
    /// <summary>W1C presentation owner for the acceptance surface. Chunk ownership is transient only.</summary>
    public sealed class ContinuousOutdoorSurfaceRuntime : MonoBehaviour
    {
        string _surfaceId = string.Empty;
        readonly HashSet<SurfaceChunkCoord> _loaded = new HashSet<SurfaceChunkCoord>();
        readonly HashSet<SurfaceChunkCoord> _desired = new HashSet<SurfaceChunkCoord>();
        readonly HashSet<SurfaceChunkCoord> _add = new HashSet<SurfaceChunkCoord>();
        readonly HashSet<SurfaceChunkCoord> _remove = new HashSet<SurfaceChunkCoord>();
        readonly List<WalkGridComposer.Input> _grids = new List<WalkGridComposer.Input>(9);
        PlayableHostBootstrap _bootstrap;
        HostDemoTileMap _tileMap;
        OutdoorSurfaceCoordinateMapper _mapper;
        readonly Dictionary<EntityId, Vector3> _lastLegalMembers = new Dictionary<EntityId, Vector3>();
        HexCoord _diagnosticDerived, _diagnosticCommitted;
        bool _autoTravelPathBlocked;
        HexCoord _blockedNextHex, _blockedDestination;
        WorldVec2 _blockedFrom, _blockedCandidate;
        public string LastMovementDiagnostic { get; private set; } = string.Empty;
        public string SurfaceEgressStatus { get; private set; } = "None";
        public HexCoord NextOutsideHex { get; private set; }
        public string NextOutsideTerrain { get; private set; } = string.Empty;
        public bool NextOutsidePassable { get; private set; }
        public bool IsActive { get; private set; }
        public string ActiveSurfaceId => IsActive ? _surfaceId : string.Empty;
        public SurfaceChunkCoord CurrentChunk { get; private set; }
        public int LoadedChunkCount => _loaded.Count;
        public IEnumerable<SurfaceChunkCoord> LoadedChunks => _loaded;
        public OutdoorSurfaceCoordinateMapper Mapper => _mapper;
        public bool TryGetAcceptanceStartWorldPosition(out float x, out float y)
        {
            x = y = 0f;
            OutdoorWorldSurfaceDefinition surface = null;
            var registry = _bootstrap?.Session?.Registry;
            if (registry == null) return false;
            foreach (var entry in registry.OutdoorSurfaces)
            {
                for (var i = 0; i < entry.Value.Chunks.Count; i++)
                    if (entry.Value.Chunks[i].Coord == new SurfaceChunkCoord(0, 0)) { surface = entry.Value; break; }
                if (surface != null) break;
            }
            if (surface == null) return false;
            var mapper = new OutdoorSurfaceCoordinateMapper(surface.ChunkWidth, surface.ChunkHeight, surface.CellSize, originWorldX: surface.OriginWorldX, originWorldY: surface.OriginWorldY);
            mapper.ChunkLocalToWorld(new SurfaceChunkCoord(0, 0), surface.ChunkWidth * 0.5f, surface.ChunkHeight * 0.5f, out x, out y);
            return true;
        }
        public bool TryGetCompositeWalkGrid(out WalkGrid grid)
        {
            grid = null;
            if (!IsActive || _grids.Count == 0) return false;
            grid = WalkGridComposer.Compose(_grids);
            return grid != null;
        }

        /// <summary>Lightweight W1C acceptance diagnostic; emitted only on streaming state changes.</summary>
        public string DescribeDiagnostics()
        {
            var chunks = new List<SurfaceChunkCoord>(_loaded);
            chunks.Sort();
            var minX = int.MaxValue; var minY = int.MaxValue; var maxX = int.MinValue; var maxY = int.MinValue;
            if (TryResolveSurface(out var definition))
                for (var i = 0; i < definition.Chunks.Count; i++)
                {
                    var c = definition.Chunks[i].Coord;
                    minX = Mathf.Min(minX, c.X); minY = Mathf.Min(minY, c.Y);
                    maxX = Mathf.Max(maxX, c.X); maxY = Mathf.Max(maxY, c.Y);
                }
            var motion = _bootstrap?.Session?.World?.PlayerPartyTravel;
            var context = _bootstrap?.MoveController?.BoundLocalMapId ?? string.Empty;
            var presentation = string.Empty;
            if (_bootstrap?.Session?.PlayerParty != null && _bootstrap.ViewSpawner?.Registry != null &&
                _bootstrap.ViewSpawner.Registry.TryGet(_bootstrap.Session.PlayerParty.ActiveCharacterId, out var view) && view != null)
                presentation = "(" + view.transform.position.x.ToString("0.###") + "," + view.transform.position.y.ToString("0.###") + ")";
            var hexWorld = _bootstrap?.Session?.World?.HexWorld;
            var derived = motion != null && hexWorld != null
                ? HexMath.WorldToHex(motion.WorldPosition.X, motion.WorldPosition.Y, hexWorld.HexSize) : default;
            HexCell tile = null;
            var exists = hexWorld != null && hexWorld.TryGetTile(derived, out tile);
            var covered = definition != null && motion != null &&
                OutdoorSurfaceCoverageResolver.ContainsWorldPosition(definition, motion.WorldPosition.X, motion.WorldPosition.Y);
            return "OutdoorPresentationAuthority=" + (IsActive ? "W1CContinuousSurface" : "LegacyLocalMap") +
                   " Surface=" + ActiveSurfaceId +
                   " Chunk=" + CurrentChunk +
                   " Loaded=" + _loaded.Count + "[" + string.Join(",", chunks) + "]" +
                   " LoadedNeighborhoodBoundary=radius1" +
                   " SurfaceCoverageBoundary=[" + minX + "," + minY + "]..[" + maxX + "," + maxY + "]" +
                   " Hex=" + (motion != null ? motion.CurrentHex.ToString() : string.Empty) +
                   " Presentation=" + presentation + " CanonicalWorld=" + (motion != null ? motion.WorldPosition.ToString() : string.Empty) +
                   " DerivedHex=" + derived + " CommittedHex=" + (motion != null ? motion.CurrentHex.ToString() : string.Empty) +
                   "\nStrategicCellExists=" + exists + " StrategicTerrain=" + (tile != null ? tile.Terrain.ToString() : "Missing") +
                   " StrategicPassable=" + (tile != null && tile.IsPassable) + " StrategicIsRoad=" + (tile != null && tile.IsRoad) +
                   "\nSurfaceCoverageContainsWorldPosition=" + covered + " MovementContext=" + context +
                   "\nSurfaceEgressStatus=" + SurfaceEgressStatus +
                   " NextOutsideHex=" + NextOutsideHex +
                   " NextOutsideTerrain=" + NextOutsideTerrain +
                   " NextOutsidePassable=" + NextOutsidePassable +
                   "\nLastMovementDiagnostic=" + LastMovementDiagnostic;
        }

        public void Bind(PlayableHostBootstrap bootstrap)
        {
            _bootstrap = bootstrap; _tileMap = bootstrap != null ? bootstrap.GetComponent<HostDemoTileMap>() : null;
            // The bridge source is the 50x50 fallback layout. 80x50 would create a 30-unit
            // physical navigation gap; this remains one uniform, provisional surface metric.
            if (TryResolveSurface(out var surface))
                _mapper = new OutdoorSurfaceCoordinateMapper(
                    surface.ChunkWidth,
                    surface.ChunkHeight,
                    surface.CellSize,
                    presentationUnitsPerWorldUnit: 1f / surface.CellSize,
                    originWorldX: surface.OriginWorldX,
                    originWorldY: surface.OriginWorldY);
            else
                _mapper = new OutdoorSurfaceCoordinateMapper(50f, 50f, 1f);
        }

        void Update()
        {
            var motion = _bootstrap?.Session?.World?.PlayerPartyTravel;
            if (motion == null || !motion.HasPosition || motion.LocationKind != PlayerPartyLocationKind.AtWorldPosition ||
                _bootstrap.Session.World.LocalMap.IsInInterior || BattleOfferService.HasActiveManualEncounter(_bootstrap.Session.World))
            {
                if (IsActive) DeactivatePresentationOnly();
                return;
            }
            if (!OutdoorSurfaceCoverageResolver.TryResolveAtWorldPosition(_bootstrap.Session.Registry, motion.WorldPosition.X, motion.WorldPosition.Y, out var surface))
            { if (IsActive) HandoffToLegacy(); return; }
            var mapper = new OutdoorSurfaceCoordinateMapper(surface.ChunkWidth, surface.ChunkHeight, surface.CellSize, presentationUnitsPerWorldUnit: 1f / surface.CellSize, originWorldX: surface.OriginWorldX, originWorldY: surface.OriginWorldY);
            var chunk = mapper.WorldToChunk(motion.WorldPosition.X, motion.WorldPosition.Y);
            if (!IsActive || !string.Equals(_surfaceId, surface.SurfaceId, StringComparison.Ordinal)) ActivateSurface(surface, chunk);
            else if (chunk != CurrentChunk) UpdateNeighborhood(chunk);
        }

        public bool PresentationToWorld(float x, float y, out float worldX, out float worldY)
        { _mapper.PresentationToWorld(x, y, out worldX, out worldY); return IsActive; }

        /// <summary>Called after all realtime writers. Canonical is the last accepted safe point;
        /// rejected transforms never become position authority. No Stop command / CancelTravel.</summary>
        public void SyncPartyPresentation()
        {
            if (!IsActive) return;
            var session = _bootstrap.Session;
            var world = session.World;
            var motion = world.PlayerPartyTravel;
            var party = session.PlayerParty;
            if (motion == null || party == null || motion.LocationKind != PlayerPartyLocationKind.AtWorldPosition ||
                world.LocalMap.IsInInterior || BattleOfferService.HasActiveManualEncounter(world)) return;
            var views = _bootstrap.ViewSpawner.Registry;
            if (!views.TryGet(party.ActiveCharacterId, out var active) || active == null) return;
            var safe = motion.WorldPosition;
            PlayerPartyTransitionMembership.CaptureTravelingMembersForPartyTransition(world, party);
            _mapper.PresentationToWorld(active.transform.position.x, active.transform.position.y, out var wx, out var wy);
            var result = PlayerPartyWildernessTransitionService.TrySyncContinuousSurfaceWorldPosition(world, wx, wy);
            if (result.IsFailure)
            {
                ReportLegalityBlocked();
                if (PlayerPartyLocalVisibleAutoTravelService.IsActiveLocalVisibleAutoTravel(motion))
                {
                    _autoTravelPathBlocked = true;
                    _blockedFrom = safe;
                    _blockedCandidate = new WorldVec2(wx, wy);
                    _blockedDestination = motion.DestinationHex;
                    _blockedNextHex = PlayerPartyLocalVisibleAutoTravelService.TryResolveActiveLeg(
                        motion, out _, out var next, out _) ? next : motion.DestinationHex;
                }
                _bootstrap.MoveController.InvalidatePartyLocalMovement(party.Members);
                _mapper.WorldToPresentation(safe.X, safe.Y, out var px, out var py);
                RestoreMember(party.ActiveCharacterId, new Vector3(px, py, HostPresentationSpace.EntityZ));
            }
            foreach (var id in motion.TravelingMembers)
            {
                if (!views.TryGet(id, out var view) || view == null) continue;
                var p = view.transform.position;
                _mapper.PresentationToWorld(p.x, p.y, out wx, out wy);
                var previous = _lastLegalMembers.TryGetValue(id, out var last) ? last : active.transform.position;
                _mapper.PresentationToWorld(previous.x, previous.y, out var oldX, out var oldY);
                var oldHex = HexMath.WorldToHex(oldX, oldY, world.HexWorld.HexSize);
                if (!id.Equals(party.ActiveCharacterId) &&
                    !ContinuousSurfacePrototypeGroundLegality.CanMoveTo(world.HexWorld, oldHex,
                        new WorldVec2(wx, wy), world.HexWorld.HexSize))
                {
                    _bootstrap.MoveController.CancelPresentationMovementPublic(id);
                    RestoreMember(id, previous);
                }
                _lastLegalMembers[id] = view.transform.position;
            }
            var derived = HexMath.WorldToHex(motion.WorldPosition.X, motion.WorldPosition.Y, world.HexWorld.HexSize);
            if (!derived.Equals(_diagnosticDerived) || !motion.CurrentHex.Equals(_diagnosticCommitted))
            {
                _diagnosticDerived = derived; _diagnosticCommitted = motion.CurrentHex;
                Debug.Log("[W1C] Hex change " + DescribeDiagnostics(), this);
            }
        }

        public void ReportLegalityBlocked()
        {
            if (LastMovementDiagnostic != ContinuousSurfacePrototypeGroundLegality.BlockedDiagnostic)
                Debug.LogWarning("[W1C] " + ContinuousSurfacePrototypeGroundLegality.BlockedDiagnostic, this);
            LastMovementDiagnostic = ContinuousSurfacePrototypeGroundLegality.BlockedDiagnostic;
        }

        public bool IsAutoTravelPathBlocked(HexCoord nextHex)
        {
            if (!_autoTravelPathBlocked) return false;
            var world = _bootstrap.Session.World;
            var motion = world.PlayerPartyTravel;
            // Retry only after manual reposition, route change or ground legality change.
            // Do not continually reissue the same failed realtime path, or cancel its TravelPlan.
            if (!motion.WorldPosition.Equals(_blockedFrom) || !nextHex.Equals(_blockedNextHex) ||
                !motion.DestinationHex.Equals(_blockedDestination) ||
                ContinuousSurfacePrototypeGroundLegality.CanMoveTo(world.HexWorld, motion.CurrentHex,
                    _blockedCandidate, world.HexWorld.HexSize))
                _autoTravelPathBlocked = false;
            return _autoTravelPathBlocked;
        }

        void RestoreMember(EntityId id, Vector3 position)
        {
            if (_bootstrap.ViewSpawner.Registry.TryGet(id, out var view) && view != null)
                view.transform.position = position;
            if (_bootstrap.Session.World.Entities.TryGet(id, out var entity) &&
                entity.TryGet<XianXia.Core.Exploration.EntityLocationComponent>(out var location))
                location.SetPresentationOverride(position.x, position.y);
        }

        /// <summary>Pure authored coverage is queried before any WalkGrid collision. This method
        /// owns presentation handoff, while Core owns the canonical/presence commit.</summary>
        public bool TryHandoffContinuousSurfaceToLegacy(Vector3 desiredPresentation)
        {
            if (!IsActive || !TryResolveSurface(out var surface)) return false;
            var world = _bootstrap.Session.World;
            var motion = world.PlayerPartyTravel;
            _mapper.PresentationToWorld(desiredPresentation.x, desiredPresentation.y, out var desiredX, out var desiredY);
            if (!OutdoorSurfaceBoundaryEgressResolver.TryResolve(surface,
                    motion.WorldPosition.X, motion.WorldPosition.Y, desiredX, desiredY, out var egress) ||
                egress.EgressState != OutdoorSurfaceBoundaryEgressResolver.State.CrossingOuterBoundary)
                return false;

            var size = world.HexWorld != null && world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            var outside = new WorldVec2(egress.JustOutsideWorldX, egress.JustOutsideWorldY);
            var destination = HexMath.WorldToHex(outside.X, outside.Y, size);
            CacheEgressDestination(world.HexWorld, destination);
            var party = _bootstrap.Session.PlayerParty;
            if (!_bootstrap.ViewSpawner.Registry.TryGet(party.ActiveCharacterId, out var active) || active == null) return false;
            PlayerPartyTransitionMembership.CaptureTravelingMembersForPartyTransition(world, party);
            var commit = PlayerPartyWildernessTransitionService.TryCommitContinuousSurfaceBoundaryEgress(
                world, outside, destination);
            if (commit.IsFailure)
            {
                SurfaceEgressStatus = commit.Error.Message == "BoundaryBlockedByStrategicGround"
                    ? "BlockedByStrategicGround" : "None";
                LastMovementDiagnostic = commit.Error.Message;
                return false;
            }

            _mapper.WorldToPresentation(outside.X, outside.Y, out var px, out var py);
            RestoreMember(party.ActiveCharacterId, new Vector3(px, py, HostPresentationSpace.EntityZ));
            SurfaceEgressStatus = "HandoffToLegacy";
            LastMovementDiagnostic = "SurfaceCoverageBoundary -> LegacyWilderness";
            HandoffToLegacy(destination);
            return true;
        }

        /// <summary>For LocalVisible AutoTravel: distinguishes authored outer egress from merely
        /// being outside the currently loaded radius-1 neighborhood.</summary>
        public bool TryGetOuterBoundaryApproach(Vector3 desiredPresentation, out Vector3 boundaryPresentation)
        {
            boundaryPresentation = default;
            if (!IsActive || !TryResolveSurface(out var surface)) return false;
            var motion = _bootstrap.Session.World.PlayerPartyTravel;
            _mapper.PresentationToWorld(desiredPresentation.x, desiredPresentation.y, out var desiredX, out var desiredY);
            if (!OutdoorSurfaceBoundaryEgressResolver.TryResolve(surface, motion.WorldPosition.X, motion.WorldPosition.Y,
                    desiredX, desiredY, out var egress) ||
                egress.EgressState != OutdoorSurfaceBoundaryEgressResolver.State.CrossingOuterBoundary)
                return false;
            var world = _bootstrap.Session.World;
            var size = world.HexWorld != null && world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            CacheEgressDestination(world.HexWorld,
                HexMath.WorldToHex(egress.JustOutsideWorldX, egress.JustOutsideWorldY, size));
            SurfaceEgressStatus = "ApproachingOuterBoundary";
            _mapper.WorldToPresentation(egress.BoundaryWorldX, egress.BoundaryWorldY, out var px, out var py);
            boundaryPresentation = new Vector3(px, py, HostPresentationSpace.EntityZ);
            return true;
        }

        // Compatibility name retained for existing direct-input call sites.
        public bool TryStepAcrossCoverageBoundary(Vector3 proposed) => TryHandoffContinuousSurfaceToLegacy(proposed);

        void CacheEgressDestination(HexWorld hexWorld, HexCoord hex)
        {
            NextOutsideHex = hex;
            if (hexWorld != null && hexWorld.TryGetTile(hex, out var tile) && tile != null)
            {
                NextOutsideTerrain = tile.Terrain.ToString();
                NextOutsidePassable = tile.Terrain != HexTerrainType.Water && tile.IsPassable;
            }
            else
            {
                NextOutsideTerrain = "Missing";
                NextOutsidePassable = false;
            }
        }

        void HandoffToLegacy(HexCoord? committedDestination = null)
        {
            var world = _bootstrap.Session.World;
            var motion = world.PlayerPartyTravel;
            var destination = committedDestination ?? motion.CurrentHex;
            if (!WildernessLocalMapFallback.TryResolve(world, destination, out var mapId))
            {
                SurfaceEgressStatus = "BlockedByStrategicGround";
                Debug.LogError("[W1C] SurfaceCoverageBoundary: legacy Wilderness unavailable at " + destination, this);
                return;
            }
            var prepared = WorldTravelService.EnterWildernessLocalMap(world, destination, mapId);
            if (prepared.IsFailure) { Debug.LogError(prepared.Error, this); return; }
            DeactivatePresentationOnly();
            _bootstrap.ExpandLocalMapForCurrentPartyWorld(closeWorldMap: false);
        }

        public bool TryActivateAtCurrentWorldPosition()
        {
            var motion = _bootstrap?.Session?.World?.PlayerPartyTravel;
            if (motion == null || !motion.HasPosition || motion.LocationKind != PlayerPartyLocationKind.AtWorldPosition ||
                _bootstrap.Session.World.LocalMap.IsInInterior ||
                BattleOfferService.HasActiveManualEncounter(_bootstrap.Session.World) ||
                !OutdoorSurfaceCoverageResolver.TryResolveAtWorldPosition(_bootstrap.Session.Registry, motion.WorldPosition.X, motion.WorldPosition.Y, out var surface)) return false;
            if (IsActive && _surfaceId == surface.SurfaceId) return true;
            var mapper = new OutdoorSurfaceCoordinateMapper(surface.ChunkWidth, surface.ChunkHeight, surface.CellSize, presentationUnitsPerWorldUnit: 1f / surface.CellSize, originWorldX: surface.OriginWorldX, originWorldY: surface.OriginWorldY);
            ActivateSurface(surface, mapper.WorldToChunk(motion.WorldPosition.X, motion.WorldPosition.Y));
            return IsActive;
        }

        void ActivateSurface(OutdoorWorldSurfaceDefinition surface, SurfaceChunkCoord center)
        {
            if (_bootstrap?.ContinuousWildernessLoadedSet?.IsActive == true)
                _bootstrap.DeactivateContinuousWildernessIfActive();
            _surfaceId = surface.SurfaceId;
            _mapper = new OutdoorSurfaceCoordinateMapper(surface.ChunkWidth, surface.ChunkHeight, surface.CellSize, presentationUnitsPerWorldUnit: 1f / surface.CellSize, originWorldX: surface.OriginWorldX, originWorldY: surface.OriginWorldY);
            if (!TryResolveSource(center, out var layout)) { _surfaceId = string.Empty; return; }
            _tileMap.RemoveLayoutInstance("legacy:active-localmap");
            IsActive = true;
            _lastLegalMembers.Clear();
            LastMovementDiagnostic = string.Empty;
            _autoTravelPathBlocked = false;
            SurfaceEgressStatus = "None";
            NextOutsideTerrain = string.Empty;
            NextOutsidePassable = false;
            _bootstrap.MoveController.InvalidatePartyLocalMovement(_bootstrap.Session.PlayerParty.Members);
            AlignPartyPresentationToWorld();
            UpdateNeighborhood(center);
            _bootstrap.SurfaceExitZonePresenter?.Clear();
            _bootstrap.MoveController.BindLocalMapContext("ContinuousSurface:" + _surfaceId);
            Debug.Log("[W1C] Activated " + DescribeDiagnostics(), this);
        }

        void UpdateNeighborhood(SurfaceChunkCoord center)
        {
            SurfaceChunkNeighborhood.CollectSquare(center, 1, _desired);
            _desired.RemoveWhere(coord => !HasChunk(coord));
            SurfaceChunkNeighborhood.Diff(_loaded, _desired, _add, _remove);
            foreach (var coord in _remove) _tileMap.RemoveLayoutInstance(SurfaceChunkNeighborhood.OwnerKey(_surfaceId, coord));
            foreach (var coord in _add) BuildChunk(coord);
            _loaded.ExceptWith(_remove); _loaded.UnionWith(_add); CurrentChunk = center;
            RecomposeWalkGrid();
            Debug.Log("[W1C] Neighborhood add=" + _add.Count + " remove=" + _remove.Count + " " + DescribeDiagnostics(), this);
        }

        void BuildChunk(SurfaceChunkCoord coord)
        {
            if (!TryResolveSource(coord, out var layout)) throw new InvalidOperationException("W1C source layout unavailable.");
            _mapper.ChunkLocalToWorld(coord, 0f, 0f, out var worldX, out var worldY);
            _mapper.WorldToPresentation(worldX, worldY, out var presentationX, out var presentationY);
            var placement = new Vector2(presentationX - layout.OriginX, presentationY - layout.OriginY);
            _tileMap.BuildLayoutInstance(SurfaceChunkNeighborhood.OwnerKey(_surfaceId, coord), layout, placement);
        }

        void RecomposeWalkGrid()
        {
            _grids.Clear();
            foreach (var coord in _loaded)
            {
                if (!TryResolveSource(coord, out var layout)) continue;
                _mapper.ChunkLocalToWorld(coord, 0f, 0f, out var wx, out var wy);
                _mapper.WorldToPresentation(wx, wy, out var px, out var py);
                _grids.Add(new WalkGridComposer.Input(MapLayoutWalkGridBuilder.Create(layout), px - layout.OriginX, py - layout.OriginY));
            }
            if (_grids.Count > 0) _bootstrap.MoveController.SetWalkGrid(WalkGridComposer.Compose(_grids));
        }

        /// <summary>Only the surface presentation owner may clear its chunk state. It never chooses a destination authority.</summary>
        public void DeactivatePresentationOnly()
        {
            foreach (var coord in _loaded) _tileMap?.RemoveLayoutInstance(SurfaceChunkNeighborhood.OwnerKey(_surfaceId, coord));
            _loaded.Clear(); _desired.Clear(); _add.Clear(); _remove.Clear(); IsActive = false;
            _grids.Clear();
            _lastLegalMembers.Clear();
            _bootstrap?.MoveController?.InvalidatePartyLocalMovement(_bootstrap.Session.PlayerParty.Members);
            _bootstrap?.MoveController?.SetWalkGrid(null);
            _bootstrap?.MoveController?.BindLocalMapContext(string.Empty);
            Debug.Log("[W1C] Deactivated surface=" + _surfaceId + " Loaded=0", this);
            _surfaceId = string.Empty;
        }

        /// <summary>Legacy compatibility entry. Normal authority handoff should call DeactivatePresentationOnly first.</summary>
        public void DeactivateSurface()
        {
            HandoffToLegacy();
        }

        bool TryResolveSource(SurfaceChunkCoord coord, out MapLayoutDefinition layout)
        {
            layout = null;
            if (!TryResolveSurface(out var surface)) return false;
            OutdoorSurfaceChunkDefinition chunk = null;
            for (var i = 0; i < surface.Chunks.Count; i++)
                if (surface.Chunks[i].Coord == coord) { chunk = surface.Chunks[i]; break; }
            if (chunk == null || string.IsNullOrWhiteSpace(chunk.SourceMapLayoutId)) return false;
            var parsed = DefinitionId.Parse(chunk.SourceMapLayoutId);
            if (!parsed.IsSuccess || _bootstrap?.Session?.Registry == null ||
                !_bootstrap.Session.Registry.TryGetMapLayout(parsed.Value, out layout) || layout == null)
                return false;
            // A legacy source bridge must exactly fill its uniform chunk metric. Otherwise a
            // source-map scale or gap would quietly turn a seam into a gameplay boundary.
            var sourceWidth = layout.Width * surface.CellSize;
            var sourceHeight = layout.Height * surface.CellSize;
            if (Mathf.Abs(sourceWidth - surface.ChunkWidth) > 0.0001f ||
                Mathf.Abs(sourceHeight - surface.ChunkHeight) > 0.0001f)
            {
                Debug.LogError("[W1C] Source layout metric does not fill chunk " + coord, this);
                layout = null;
                return false;
            }
            return true;
        }

        void AlignPartyPresentationToWorld()
        {
            var party = _bootstrap?.Session?.PlayerParty;
            var motion = _bootstrap?.Session?.World?.PlayerPartyTravel;
            var registry = _bootstrap?.ViewSpawner?.Registry;
            if (party == null || motion == null || registry == null || party.ActiveCharacterId.IsNone ||
                !registry.TryGet(party.ActiveCharacterId, out var active) || active == null)
                return;
            _mapper.WorldToPresentation(motion.WorldPosition.X, motion.WorldPosition.Y, out var x, out var y);
            var delta = new Vector3(x - active.transform.position.x, y - active.transform.position.y, 0f);
            foreach (var member in party.Members)
                if (registry.TryGet(member, out var view) && view != null)
                    view.transform.position += delta;
        }
        bool TryResolveSurface(out OutdoorWorldSurfaceDefinition surface)
        {
            surface = null;
            var parsed = DefinitionId.Parse(_surfaceId);
            return parsed.IsSuccess && _bootstrap?.Session?.Registry != null &&
                   _bootstrap.Session.Registry.TryGetOutdoorSurface(parsed.Value, out surface) && surface != null;
        }
        bool HasChunk(SurfaceChunkCoord coord)
        {
            if (!TryResolveSurface(out var surface)) return false;
            for (var i = 0; i < surface.Chunks.Count; i++) if (surface.Chunks[i].Coord == coord) return true;
            return false;
        }
    }
}
