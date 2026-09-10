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
        WalkGrid _compositeWalkGrid;
        PlayableHostBootstrap _bootstrap;
        HostDemoTileMap _tileMap;
        OutdoorSurfaceCoordinateMapper _mapper;
        readonly Dictionary<EntityId, Vector3> _lastLegalMembers = new Dictionary<EntityId, Vector3>();
        readonly HashSet<EntityId> _continuousSitePopulation = new HashSet<EntityId>();
        readonly List<EntityId> _sitePopulationScratch = new List<EntityId>();
        ulong _lastPopulationRefreshTick = ulong.MaxValue;
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
        public float ActiveHexSize => _bootstrap?.Session?.World?.HexWorld?.HexSize > 0f
            ? _bootstrap.Session.World.HexWorld.HexSize : 1f;
        public bool TryGetBakedPlacementFootprint(
            string kind, string boundLocationId,
            out float minX, out float maxX, out float minY, out float maxY)
        {
            minX = maxX = minY = maxY = 0f;
            if (!IsActive || string.IsNullOrEmpty(boundLocationId) || !TryResolveSurface(out var surface))
                return false;
            for (var i = 0; i < surface.SitePlacements.Count; i++)
            {
                var p = surface.SitePlacements[i];
                if (p == null || !string.Equals(MapKindCatalog.NormalizeKind(p.Kind), kind, StringComparison.Ordinal) ||
                    !string.Equals(p.BoundLocationId, boundLocationId, StringComparison.Ordinal)) continue;
                _mapper.WorldToPresentation(p.WorldX, p.WorldY, out var x0, out var y0);
                _mapper.WorldToPresentation(p.WorldX + p.WorldWidth, p.WorldY + p.WorldHeight, out var x1, out var y1);
                minX = Mathf.Min(x0, x1); maxX = Mathf.Max(x0, x1);
                minY = Mathf.Min(y0, y1); maxY = Mathf.Max(y0, y1);
                return true;
            }
            return false;
        }
        public bool TryPickBakedPlacement(
            string kind, float presentationX, float presentationY, out string boundLocationId)
        {
            boundLocationId = string.Empty;
            if (!IsActive || !TryResolveSurface(out var surface)) return false;
            var bestArea = float.MaxValue;
            for (var i = 0; i < surface.SitePlacements.Count; i++)
            {
                var p = surface.SitePlacements[i];
                if (p == null || !string.Equals(MapKindCatalog.NormalizeKind(p.Kind), kind, StringComparison.Ordinal) ||
                    string.IsNullOrEmpty(p.BoundLocationId)) continue;
                _mapper.WorldToPresentation(p.WorldX, p.WorldY, out var x0, out var y0);
                _mapper.WorldToPresentation(p.WorldX + p.WorldWidth, p.WorldY + p.WorldHeight, out var x1, out var y1);
                var minX = Mathf.Min(x0, x1); var maxX = Mathf.Max(x0, x1);
                var minY = Mathf.Min(y0, y1); var maxY = Mathf.Max(y0, y1);
                if (presentationX < minX || presentationX > maxX || presentationY < minY || presentationY > maxY) continue;
                var area = (maxX - minX) * (maxY - minY);
                if (area >= bestArea) continue;
                bestArea = area; boundLocationId = p.BoundLocationId;
            }
            return !string.IsNullOrEmpty(boundLocationId);
        }
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
            grid = _compositeWalkGrid;
            return IsActive && grid != null;
        }

        public void RefreshCompositeWalkGrid()
        {
            if (IsActive)
                RecomposeWalkGrid();
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
            return "PresentationAuthority=" + (IsActive ? "MainContinuousSurface" : "LegacyLocalMap") +
                   " Surface=" + ActiveSurfaceId +
                   " Chunk=" + CurrentChunk +
                   " Loaded=" + _loaded.Count + "[" + string.Join(",", chunks) + "]" +
                   " LoadedNeighborhoodBoundary=radius1" +
                   " SurfaceCoverageBoundary=[" + minX + "," + minY + "]..[" + maxX + "," + maxY + "]" +
                   " Hex=" + (motion != null ? motion.CurrentHex.ToString() : string.Empty) +
                   " CurrentOutdoorWorldSiteId=" + (motion != null ? motion.CurrentOutdoorWorldSiteId : string.Empty) +
                   " Presentation=" + presentation + " CanonicalWorld=" + (motion != null ? motion.WorldPosition.ToString() : string.Empty) +
                   " DerivedHex=" + derived + " CommittedHex=" + (motion != null ? motion.CurrentHex.ToString() : string.Empty) +
                   "\nStrategicCellExists=" + exists + " StrategicTerrain=" + (tile != null ? tile.Terrain.ToString() : "Missing") +
                   " StrategicPassable=" + (tile != null && tile.IsPassable) + " StrategicIsRoad=" + (tile != null && tile.IsRoad) +
                   "\nSurfaceCoverageContainsWorldPosition=" + covered + " MovementContext=" + context +
                   "\nSurfaceEgressStatus=" + SurfaceEgressStatus +
                   " NextOutsideHex=" + NextOutsideHex +
                   " NextOutsideTerrain=" + NextOutsideTerrain +
                   " NextOutsidePassable=" + NextOutsidePassable +
                   "\nCurrentWorldSiteGateway=DisabledForOutdoorMigration" +
                   " WorldSiteIngressStatus=" + LastMovementDiagnostic +
                   " LocalPlacesContext=" + (string.IsNullOrEmpty(_bootstrap?.Session?.World?.WorldRegion?.ActiveMapLayoutId) ? "ContinuousEmpty" : _bootstrap.Session.World.WorldRegion.ActiveMapLayoutId) +
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
            TryMigrateLegacyOutdoorSiteRestore();
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
            if (result.IsSuccess)
                motion.SetCurrentOutdoorWorldSiteContext(
                    WorldSitePhysicalRegionQuery.ResolveSiteIdOrEmpty(world, motion.WorldPosition));
            if (_lastPopulationRefreshTick != world.Tick.Value)
            {
                _lastPopulationRefreshTick = world.Tick.Value;
                RefreshContinuousSitePopulation();
            }
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
            TryMigrateLegacyOutdoorSiteRestore();
            var motion = _bootstrap?.Session?.World?.PlayerPartyTravel;
            if (motion == null || !motion.HasPosition || motion.LocationKind != PlayerPartyLocationKind.AtWorldPosition ||
                _bootstrap.Session.World.LocalMap.IsInInterior ||
                BattleOfferService.HasActiveManualEncounter(_bootstrap.Session.World)) return false;
            // Explicit W1C diagnostic activation remains authoritative until its owner is
            // deactivated; normal resolver never selects acceptance-only content on its own.
            if (IsActive && TryResolveSurface(out var active) && active.AcceptanceOnly) return true;
            if (!OutdoorSurfaceCoverageResolver.TryResolveAtWorldPosition(
                    _bootstrap.Session.Registry, motion.WorldPosition.X, motion.WorldPosition.Y, out var surface))
                return false;
            if (IsActive && _surfaceId == surface.SurfaceId) return true;
            var mapper = new OutdoorSurfaceCoordinateMapper(surface.ChunkWidth, surface.ChunkHeight, surface.CellSize, presentationUnitsPerWorldUnit: 1f / surface.CellSize, originWorldX: surface.OriginWorldX, originWorldY: surface.OriginWorldY);
            ActivateSurface(surface, mapper.WorldToChunk(motion.WorldPosition.X, motion.WorldPosition.Y));
            return IsActive;
        }

        /// <summary>
        /// Compatibility restore only: an old save may still name an Outdoor WorldSite as its
        /// physical location.  Migrated sites restore at the saved canonical position when one
        /// exists, otherwise at their authored footprint anchor.  No Site LocalMap is selected.
        /// </summary>
        void TryMigrateLegacyOutdoorSiteRestore()
        {
            var world = _bootstrap?.Session?.World;
            var motion = world?.PlayerPartyTravel;
            if (world?.HexWorld == null || motion == null ||
                motion.LocationKind != PlayerPartyLocationKind.AtWorldSite ||
                string.IsNullOrEmpty(motion.SiteId) ||
                !world.Strategic.Sites.TryGet(motion.SiteId, out var site) ||
                !WorldSiteOutdoorMigrationPolicy.UsesContinuousOutdoorSurface(site))
                return;
            var size = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            var position = motion.WorldPosition;
            if (!motion.HasPosition)
            {
                HexMath.ToWorldPosition(site.PresenceHex, size, out var x, out var y);
                position = new WorldVec2(x, y);
            }
            var hex = HexMath.WorldToHex(position.X, position.Y, size);
            motion.SetAtWorldPosition(position, hex);
            motion.SetCurrentOutdoorWorldSiteContext(site.SiteId);
            var party = _bootstrap.Session.PlayerParty;
            if (party != null)
                for (var i = 0; i < party.Members.Count; i++)
                    world.WorldPresence.SetAtWorldPosition(party.Members[i], position, hex);
            world.PartyWorld.ClearSiteFocus();
            world.PartyWorld.SiteId = string.Empty;
            world.PartyWorld.LocalMapId = string.Empty;
            world.PartyWorld.Mode = PartyWorldPresenceMode.AtWorldPosition;
            world.LocalMap.ActiveMapLayoutId = string.Empty;
            world.LocalMap.OverworldMapLayoutId = string.Empty;
        }

        /// <summary>Diagnostic-only explicit W1C entry. Normal gameplay must use
        /// <see cref="TryActivateAtCurrentWorldPosition"/>, which excludes AcceptanceOnly surfaces.</summary>
        public bool TryActivateAcceptanceAtCurrentWorldPosition()
        {
            var motion = _bootstrap?.Session?.World?.PlayerPartyTravel;
            var registry = _bootstrap?.Session?.Registry;
            if (motion == null || registry == null || !motion.HasPosition ||
                motion.LocationKind != PlayerPartyLocationKind.AtWorldPosition)
                return false;
            OutdoorWorldSurfaceDefinition acceptance = null;
            foreach (var entry in registry.OutdoorSurfaces)
            {
                if (!entry.Value.AcceptanceOnly ||
                    !OutdoorSurfaceCoverageResolver.ContainsWorldPosition(
                        entry.Value, motion.WorldPosition.X, motion.WorldPosition.Y))
                    continue;
                if (acceptance != null) return false;
                acceptance = entry.Value;
            }
            if (acceptance == null) return false;
            var mapper = new OutdoorSurfaceCoordinateMapper(
                acceptance.ChunkWidth, acceptance.ChunkHeight, acceptance.CellSize,
                presentationUnitsPerWorldUnit: 1f / acceptance.CellSize,
                originWorldX: acceptance.OriginWorldX, originWorldY: acceptance.OriginWorldY);
            ActivateSurface(acceptance, mapper.WorldToChunk(motion.WorldPosition.X, motion.WorldPosition.Y));
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
            var activeMotion = _bootstrap.Session.World.PlayerPartyTravel;
            activeMotion.SetCurrentOutdoorWorldSiteContext(
                WorldSitePhysicalRegionQuery.ResolveSiteIdOrEmpty(
                    _bootstrap.Session.World, activeMotion.WorldPosition));
            _lastLegalMembers.Clear();
            LastMovementDiagnostic = string.Empty;
            _autoTravelPathBlocked = false;
            SurfaceEgressStatus = "None";
            NextOutsideTerrain = string.Empty;
            NextOutsidePassable = false;
            _bootstrap.MoveController.InvalidatePartyLocalMovement(_bootstrap.Session.PlayerParty.Members);
            // A continuous surface is not a LocalMap. Dispose the previous WorldSite-only
            // labels/interactions before ensuring the party presentation at global coordinates.
            _bootstrap.FinalizeContinuousWildernessPresentationHandoff();
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
            foreach (var coord in _remove)
            {
                _tileMap.RemoveLayoutInstance(SurfaceChunkNeighborhood.OwnerKey(_surfaceId, coord));
                RemoveSitePlacementInstances(coord);
            }
            foreach (var coord in _add) BuildChunk(coord);
            _loaded.ExceptWith(_remove); _loaded.UnionWith(_add); CurrentChunk = center;
            RefreshContinuousSitePopulation();
            RecomposeWalkGrid();
            _bootstrap.RefreshContinuousOutdoorOverlaysOnce();
            Debug.Log("[W1C] Neighborhood add=" + _add.Count + " remove=" + _remove.Count + " " + DescribeDiagnostics(), this);
        }

        void BuildChunk(SurfaceChunkCoord coord)
        {
            if (!TryResolveSource(coord, out var layout)) throw new InvalidOperationException("W1C source layout unavailable.");
            _mapper.ChunkLocalToWorld(coord, 0f, 0f, out var worldX, out var worldY);
            _mapper.WorldToPresentation(worldX, worldY, out var presentationX, out var presentationY);
            var placement = new Vector2(presentationX - layout.OriginX, presentationY - layout.OriginY);
            _tileMap.BuildLayoutInstance(SurfaceChunkNeighborhood.OwnerKey(_surfaceId, coord), layout, placement);
            BuildBakedOutdoorSitePlacements(coord);
        }

        void RemoveSitePlacementInstances(SurfaceChunkCoord coord)
        {
            var sites = _bootstrap?.Session?.World?.Strategic?.Sites?.Sites;
            if (sites == null) return;
            foreach (var entry in sites)
                _tileMap.RemoveLayoutInstance(SitePlacementOwnerKey(coord, entry.Key));
        }

        static string SitePlacementOwnerKey(SurfaceChunkCoord coord, string siteId) =>
            "surface:" + coord.X + ":" + coord.Y + ":site:" + (siteId ?? string.Empty);

        /// <summary>
        /// Renders checked-in baked placements directly into the active continuous chunk.
        /// Empty LocalMap ground is intentionally ignored. Source-local cells are retained only as
        /// authored stamping semantics; actor movement reads canonical WorldPosition exclusively.
        /// </summary>
        void BuildBakedOutdoorSitePlacements(SurfaceChunkCoord chunk)
        {
            if (!TryResolveSurface(out var surface) || surface.SitePlacements == null) return;
            var bySite = new Dictionary<string, List<OutdoorSurfacePlacementDefinition>>(StringComparer.Ordinal);
            for (var i = 0; i < surface.SitePlacements.Count; i++)
            {
                var source = surface.SitePlacements[i];
                if (source == null || !PlacementTouchesChunk(source, chunk))
                    continue;
                if (!bySite.TryGetValue(source.SiteId ?? string.Empty, out var converted))
                {
                    converted = new List<OutdoorSurfacePlacementDefinition>();
                    bySite[source.SiteId ?? string.Empty] = converted;
                }
                converted.Add(source);
            }
            foreach (var entry in bySite)
                _tileMap.BuildOutdoorPlacementInstance(
                    SitePlacementOwnerKey(chunk, entry.Key), entry.Value, _mapper, chunk);
        }

        bool PlacementTouchesChunk(OutdoorSurfacePlacementDefinition p, SurfaceChunkCoord chunk)
        {
            _mapper.ChunkLocalToWorld(chunk, 0f, 0f, out var left, out var bottom);
            return p.WorldX < left + _mapper.ChunkWidth && p.WorldX + p.WorldWidth > left &&
                   p.WorldY < bottom + _mapper.ChunkHeight && p.WorldY + p.WorldHeight > bottom;
        }

        void RecomposeWalkGrid()
        {
            _grids.Clear();
            _compositeWalkGrid = null;
            foreach (var coord in _loaded)
            {
                if (!TryResolveSource(coord, out var layout)) continue;
                _mapper.ChunkLocalToWorld(coord, 0f, 0f, out var wx, out var wy);
                _mapper.WorldToPresentation(wx, wy, out var px, out var py);
                _grids.Add(new WalkGridComposer.Input(MapLayoutWalkGridBuilder.Create(layout), px - layout.OriginX, py - layout.OriginY));
                var blockers = BuildSiteBlockerGrid(coord, px, py, layout);
                if (blockers != null) _grids.Add(new WalkGridComposer.Input(blockers, 0f, 0f));
            }
            if (_grids.Count > 0)
            {
                var composite = WalkGridComposer.Compose(_grids);
                var flags = _bootstrap.Session.World.Strategic.FactionFlags.Flags;
                foreach (var pair in flags)
                    HostFactionFlagQuery.ApplyWalkGridBlock(pair.Value, this, composite);
                _compositeWalkGrid = composite;
                _bootstrap.MoveController.SetWalkGrid(composite);
            }
        }

        WalkGrid BuildSiteBlockerGrid(SurfaceChunkCoord coord, float originX, float originY, MapLayoutDefinition sourceLayout)
        {
            if (!TryResolveSurface(out var surface) || surface.SitePlacements == null) return null;
            var cell = sourceLayout.CellSize > 0f ? sourceLayout.CellSize : 1f;
            var grid = new WalkGrid(originX, originY, cell, sourceLayout.Width, sourceLayout.Height);
            var any = false;
            for (var i = 0; i < surface.SitePlacements.Count; i++)
            {
                var p = surface.SitePlacements[i];
                if (p == null || !p.BlocksMovement || !PlacementTouchesChunk(p, coord)) continue;
                _mapper.WorldToPresentation(p.WorldX, p.WorldY, out var left, out var bottom);
                _mapper.WorldToPresentation(p.WorldX + p.WorldWidth, p.WorldY + p.WorldHeight, out var right, out var top);
                var minX = Mathf.FloorToInt((Mathf.Min(left, right) - originX) / cell);
                var minY = Mathf.FloorToInt((Mathf.Min(bottom, top) - originY) / cell);
                var maxX = Mathf.CeilToInt((Mathf.Max(left, right) - originX) / cell) - 1;
                var maxY = Mathf.CeilToInt((Mathf.Max(bottom, top) - originY) / cell) - 1;
                grid.SetBlockedRect(minX, minY, maxX, maxY, true); any = true;
            }
            return any ? grid : null;
        }

        /// <summary>Only the surface presentation owner may clear its chunk state. It never chooses a destination authority.</summary>
        public void DeactivatePresentationOnly()
        {
            foreach (var coord in _loaded)
            {
                _tileMap?.RemoveLayoutInstance(SurfaceChunkNeighborhood.OwnerKey(_surfaceId, coord));
                RemoveSitePlacementInstances(coord);
            }
            _loaded.Clear(); _desired.Clear(); _add.Clear(); _remove.Clear(); IsActive = false;
            _grids.Clear();
            _compositeWalkGrid = null;
            _lastLegalMembers.Clear();
            ReleaseContinuousSitePopulation();
            _bootstrap?.MoveController?.InvalidatePartyLocalMovement(_bootstrap.Session.PlayerParty.Members);
            _bootstrap?.MoveController?.SetWalkGrid(null);
            _bootstrap?.MoveController?.BindLocalMapContext(string.Empty);
            Debug.Log("[W1C] Deactivated surface=" + _surfaceId + " Loaded=0", this);
            _surfaceId = string.Empty;
        }

        void RefreshContinuousSitePopulation()
        {
            var world = _bootstrap?.Session?.World;
            var motion = world?.PlayerPartyTravel;
            ReleaseContinuousSitePopulation(pruneViews: false);
            if (world == null || motion == null || !TryResolveSurface(out var surface))
                return;
            _lastPopulationRefreshTick = world.Tick.Value;

            var party = _bootstrap.Session.PlayerParty;
            if (party != null)
            {
                _mapper.WorldToPresentation(motion.WorldPosition.X, motion.WorldPosition.Y, out var partyX, out var partyY);
                for (var i = 0; i < party.Members.Count; i++)
                {
                    world.ContinuousOutdoorMaterialization.Materialize(party.Members[i]);
                    if (world.Entities.TryGet(party.Members[i], out var member) &&
                        member.TryGet<XianXia.Core.Exploration.EntityLocationComponent>(out var memberLoc))
                        memberLoc.SetPresentationOverride(partyX + (i % 3) * .8f, partyY + (i / 3) * .8f);
                }
            }

            for (var regionIndex = 0; regionIndex < surface.SiteRegions.Count; regionIndex++)
            {
                var region = surface.SiteRegions[regionIndex];
                if (region == null || !IsSiteInLoadedNeighborhood(surface, region.SiteId) ||
                    !world.Strategic.Sites.TryGet(region.SiteId, out var site) ||
                    !WorldSiteOutdoorMigrationPolicy.UsesContinuousOutdoorSurface(site))
                    continue;
                world.ContinuousOutdoorMaterialization.RegisterLoadedSite(region.SiteId);
                for (var placeIndex = 0; placeIndex < surface.SitePlaces.Count; placeIndex++)
                {
                    var place = surface.SitePlaces[placeIndex];
                    if (place == null || !string.Equals(place.SiteId, region.SiteId, StringComparison.Ordinal)) continue;
                    _mapper.WorldToPresentation(place.WorldX, place.WorldY, out var placeX, out var placeY);
                    world.ContinuousOutdoorMaterialization.RegisterPlace(region.SiteId,
                        new XianXia.Core.Exploration.WorldLocationState
                        {
                            Id = place.LocationId, Name = place.Name, PresentationX = placeX, PresentationZ = placeY,
                            LocalMapId = place.LocalMapId, EnterLocalMapId = place.EnterLocalMapId,
                            EnterSpawnLocationId = place.EnterSpawnLocationId
                        });
                }

                StrategicWorldSitePopulationService.CollectCharacterIdsPresentAtWorldSite(
                    world, site, null, _sitePopulationScratch);
                for (var i = 0; i < _sitePopulationScratch.Count; i++)
                {
                    var id = _sitePopulationScratch[i];
                    if (_bootstrap.Session.PlayerParty.IsMember(id) || !world.Entities.TryGet(id, out var entity)) continue;
                    // 落点优先级（§9）：precise Continuous authored anchor
                    //　→ EntityLocation.LocationId 对应的 baked SitePlace
                    //　→ deterministic fallback。
                    var wx = region.ArrivalWorldX; var wy = region.ArrivalWorldY;
                    var hasPlacement = false;
                    if (world.WorldPresence.TryGet(id, out var sitePresence) &&
                        sitePresence != null &&
                        sitePresence.Mode == XianXia.Core.World.PartyWorldPresenceMode.AtSite &&
                        sitePresence.HasContinuousWorldPosition)
                    {
                        wx = sitePresence.WorldPosX;
                        wy = sitePresence.WorldPosY;
                        hasPlacement = true;
                    }

                    if (!entity.TryGet<XianXia.Core.Exploration.EntityLocationComponent>(out var loc))
                    {
                        loc = new XianXia.Core.Exploration.EntityLocationComponent();
                        entity.AddComponent(loc);
                    }

                    if (!hasPlacement)
                    {
                        var place = surface.SitePlaces.Find(p => string.Equals(p.SiteId, region.SiteId, StringComparison.Ordinal) &&
                                                                 string.Equals(p.LocationId, loc.LocationId, StringComparison.Ordinal));
                        if (place != null)
                        {
                            wx = place.WorldX; wy = place.WorldY;
                        }
                        else
                        {
                            AddDeterministicFallbackOffset(id, ref wx, ref wy, surface.CellSize);
                        }
                    }

                    _mapper.WorldToPresentation(wx, wy, out var px, out var py);
                    loc.SetPresentationOverride(px, py);
                    world.ContinuousOutdoorMaterialization.Materialize(id);
                    _continuousSitePopulation.Add(id);
                }
            }

            // Continuous positions and residual AtHex presences in loaded chunks are part of the
            // same transient presentation scope, including downed characters and visible corpses.
            foreach (var pair in world.WorldPresence.All)
            {
                var presence = pair.Value;
                var id = presence != null ? presence.EntityId : EntityId.None;
                if (id.IsNone || world.ContinuousOutdoorMaterialization.IsMaterialized(id) ||
                    !world.Entities.TryGet(id, out var entity) ||
                    XianXia.Core.Combat.CombatLifeStateService.ShouldHideFromSpawn(entity)) continue;
                WorldVec2 position;
                if (presence.Mode == PartyWorldPresenceMode.AtWorldPosition && presence.HasContinuousWorldPosition)
                    position = presence.ContinuousWorldPosition;
                else if (presence.Mode == PartyWorldPresenceMode.AtHex && presence.HasContinuousWorldPosition)
                    position = presence.ContinuousWorldPosition;
                else if (presence.Mode == PartyWorldPresenceMode.AtHex && presence.UsesHexPresence)
                {
                    HexMath.ToWorldPosition(presence.ResidualHex, world.HexWorld.HexSize, out var hx, out var hy);
                    position = new WorldVec2(hx, hy);
                }
                else continue;
                if (!_loaded.Contains(_mapper.WorldToChunk(position.X, position.Y))) continue;
                var wx = position.X; var wy = position.Y;
                AddDeterministicFallbackOffset(id, ref wx, ref wy, surface.CellSize);
                _mapper.WorldToPresentation(wx, wy, out var px, out var py);
                if (!entity.TryGet<XianXia.Core.Exploration.EntityLocationComponent>(out var loc))
                {
                    loc = new XianXia.Core.Exploration.EntityLocationComponent();
                    entity.AddComponent(loc);
                }
                loc.SetPresentationOverride(px, py);
                world.ContinuousOutdoorMaterialization.Materialize(id);
                _continuousSitePopulation.Add(id);
            }
            _bootstrap.Session.RefreshViewableEntityIds();
            _bootstrap.ViewSpawner.PruneHiddenViews(_bootstrap.Session);
            _bootstrap.ViewSpawner.SpawnMissingVisibleViews(_bootstrap.Session);
            _bootstrap.ViewSpawner.SyncLocations(_bootstrap.Session);
        }

        bool IsSiteInLoadedNeighborhood(OutdoorWorldSurfaceDefinition surface, string siteId)
        {
            for (var i = 0; i < surface.SitePlacements.Count; i++)
            {
                var p = surface.SitePlacements[i];
                if (p == null || !string.Equals(p.SiteId, siteId, StringComparison.Ordinal)) continue;
                foreach (var chunk in _loaded)
                    if (PlacementTouchesChunk(p, chunk)) return true;
            }
            var region = surface.SiteRegions.Find(r => r != null && string.Equals(r.SiteId, siteId, StringComparison.Ordinal));
            return region != null && _loaded.Contains(_mapper.WorldToChunk(region.ArrivalWorldX, region.ArrivalWorldY));
        }

        static void AddDeterministicFallbackOffset(EntityId id, ref float x, ref float y, float surfaceCellSize)
        {
            var ordinal = (int)(id.Value % 23UL) + 1;
            var angle = ordinal * 2.39996322972865332;
            var radius = Math.Max(surfaceCellSize * 2f, surfaceCellSize * (2f + ordinal / 6f));
            x += (float)Math.Cos(angle) * radius;
            y += (float)Math.Sin(angle) * radius;
        }

        void ReleaseContinuousSitePopulation(bool pruneViews = true)
        {
            var world = _bootstrap?.Session?.World;
            if (world != null)
            {
                foreach (var id in _continuousSitePopulation)
                {
                    if (world.Entities.TryGet(id, out var entity) &&
                        entity.TryGet<XianXia.Core.Exploration.EntityLocationComponent>(out var loc))
                        loc.HasPresentationOverride = false;
                }
                world.ContinuousOutdoorMaterialization.Clear();
            }
            _continuousSitePopulation.Clear();
            _sitePopulationScratch.Clear();
            if (pruneViews && _bootstrap?.Session?.IsInitialized == true)
            {
                _bootstrap.Session.RefreshViewableEntityIds();
                _bootstrap.ViewSpawner?.PruneHiddenViews(_bootstrap.Session);
            }
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
            if (party == null || motion == null || registry == null || party.ActiveCharacterId.IsNone)
                return;
            if (!registry.TryGet(party.ActiveCharacterId, out var active) || active == null)
            {
                _bootstrap.ViewSpawner?.SpawnMissingVisibleViews(_bootstrap.Session);
                if (!registry.TryGet(party.ActiveCharacterId, out active) || active == null)
                    return;
            }
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
