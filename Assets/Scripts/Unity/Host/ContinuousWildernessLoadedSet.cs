using System;
using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Navigation;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;
using XianXia.Data.Content;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// W1B transient owner for exactly one adjacent Wilderness pair. It has no Domain or Save
    /// identity: its key only keeps two Host presentation instances, their shared seam and the
    /// composite movement context alive together.
    /// </summary>
    public sealed class ContinuousWildernessLoadedSet : MonoBehaviour
    {
        const string OwnerPrefix = "continuous-wilderness:";
        PlayableHostBootstrap _bootstrap;
        HostDemoTileMap _tileMap;
        SurfaceExitConnection _seam;
        WalkGrid _compositeWalkGrid;
        string _key = string.Empty;
        string _sourceOwner = string.Empty;
        string _destinationOwner = string.Empty;
        string _mapLayoutA = string.Empty;
        string _mapLayoutB = string.Empty;
        HexCoord _hexA;
        HexCoord _hexB;
        Vector2 _offsetA;
        Vector2 _offsetB;
        float _seamPresentationX;
        string _activePairDiagnostic = "Inactive";

        public bool IsActive { get; private set; }
        public string Key => _key;
        public string ActivePairDiagnostic => _activePairDiagnostic;
        public HexCoord HexA => _hexA;
        public HexCoord HexB => _hexB;
        public string MapLayoutA => _mapLayoutA;
        public string MapLayoutB => _mapLayoutB;

        public bool IsInternal(SurfaceExitConnection connection) =>
            IsActive && (SameEdge(_seam, connection) || IsReverseEdge(_seam, connection));

        public bool ContainsHex(HexCoord hex) =>
            IsActive && (hex.Equals(_hexA) || hex.Equals(_hexB));

        public bool IsInternalNeighbour(HexCoord currentHex, HexCoord nextHex)
        {
            if (!IsActive)
                return false;
            return (currentHex.Equals(_hexA) && nextHex.Equals(_hexB)) ||
                   (currentHex.Equals(_hexB) && nextHex.Equals(_hexA));
        }

        public void Bind(PlayableHostBootstrap bootstrap)
        {
            _bootstrap = bootstrap;
            _tileMap = bootstrap != null ? bootstrap.GetComponent<HostDemoTileMap>() : null;
        }

        public bool TryGetCompositeWalkGrid(out WalkGrid grid)
        {
            grid = _compositeWalkGrid;
            return IsActive && grid != null;
        }

        public bool TryGetInstanceForHex(HexCoord hex, out SurfacePresentationInstance instance)
        {
            instance = null;
            if (!IsActive || _tileMap == null)
                return false;
            if (hex.Equals(_hexA))
                return _tileMap.LoadedInstances.TryGetValue(_sourceOwner, out instance);
            if (hex.Equals(_hexB))
                return _tileMap.LoadedInstances.TryGetValue(_destinationOwner, out instance);
            return false;
        }

        public bool TryGetPrimaryInstance(out SurfacePresentationInstance instance)
        {
            instance = null;
            var motion = _bootstrap?.Session?.World?.PlayerPartyTravel;
            if (!IsActive || motion == null || _tileMap == null)
                return false;
            if (motion.CurrentHex.Equals(_hexB))
                return _tileMap.LoadedInstances.TryGetValue(_destinationOwner, out instance);
            return _tileMap.LoadedInstances.TryGetValue(_sourceOwner, out instance);
        }

        public Vector2 GetPlacementOffset(HexCoord hex) =>
            ContinuousWildernessSurfaceCoordinates.OffsetForHex(hex, _hexA, _hexB, _offsetA, _offsetB);

        public bool PresentationToSurfaceLocal(
            HexCoord hex,
            float presentationX,
            float presentationY,
            out float surfaceLocalX,
            out float surfaceLocalY)
        {
            var offset = GetPlacementOffset(hex);
            return ContinuousWildernessSurfaceCoordinates.PresentationToSurfaceLocal(
                IsActive,
                offset,
                presentationX,
                presentationY,
                out surfaceLocalX,
                out surfaceLocalY);
        }

        public bool SurfaceLocalToPresentation(
            HexCoord hex,
            float surfaceLocalX,
            float surfaceLocalY,
            out float presentationX,
            out float presentationY)
        {
            var offset = GetPlacementOffset(hex);
            return ContinuousWildernessSurfaceCoordinates.SurfaceLocalToPresentation(
                IsActive,
                offset,
                surfaceLocalX,
                surfaceLocalY,
                out presentationX,
                out presentationY);
        }

        public bool TryGetCompositeBounds(
            out WildernessLocalWorldProjection.WildernessLocalMapBounds bounds)
        {
            bounds = default;
            if (!IsActive || !TryResolveLayout(_mapLayoutA, out var source))
                return false;
            var cell = source.CellSize > 0f ? source.CellSize : 1f;
            var minX = Math.Min(_offsetA.x + source.OriginX, _offsetB.x + source.OriginX);
            var minY = Math.Min(_offsetA.y + source.OriginY, _offsetB.y + source.OriginY);
            var maxX = Math.Max(
                _offsetA.x + source.OriginX + source.Width * cell,
                _offsetB.x + source.OriginX + source.Width * cell);
            var maxY = Math.Max(
                _offsetA.y + source.OriginY + source.Height * cell,
                _offsetB.y + source.OriginY + source.Height * cell);
            bounds = WildernessLocalWorldProjection.WildernessLocalMapBounds.FromOriginSize(
                minX, minY, cell, (int)Math.Ceiling((maxX - minX) / cell), (int)Math.Ceiling((maxY - minY) / cell));
            return true;
        }

        public SurfaceExitConnection ToPresentationConnection(SurfaceExitConnection connection)
        {
            if (!IsActive)
                return connection;
            var offset = GetPlacementOffset(connection.SourceHex);
            if (offset.sqrMagnitude <= 0.0001f)
                return connection;
            var slot = connection.SlotRect;
            var shifted = new SurfaceExitCoverageRect(
                slot.MinX + offset.x,
                slot.MaxX + offset.x,
                slot.MinY + offset.y,
                slot.MaxY + offset.y);
            return new SurfaceExitConnection(
                connection.SourceHex,
                connection.DestinationHex,
                connection.DirectionIndex,
                connection.DestinationKind,
                connection.DestinationSiteId,
                connection.LocalDirectionX,
                connection.LocalDirectionY,
                connection.ExitCenterLocalX + offset.x,
                connection.ExitCenterLocalY + offset.y,
                shifted,
                connection.BoundaryContactWorldX,
                connection.BoundaryContactWorldY);
        }

        /// <summary>
        /// Activates the deterministic acceptance pair when party is on either hex of the pair.
        /// </summary>
        public bool TryActivateAcceptancePair(
            SimulationWorld world,
            IReadOnlyList<SurfaceExitVisibleZone> zones,
            HexCoord partyHex,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (IsActive)
            {
                diagnostic = _activePairDiagnostic;
                return true;
            }

            if (!ContinuousWildernessPairSelector.TrySelectAcceptancePair(
                    world, zones, partyHex, out var candidate, out diagnostic))
            {
                _activePairDiagnostic = "NotActivated: " + diagnostic;
                Debug.LogWarning("[W1B] " + _activePairDiagnostic, this);
                return false;
            }

            if (!partyHex.Equals(candidate.HexA) && !partyHex.Equals(candidate.HexB))
            {
                _activePairDiagnostic =
                    "NotActivated: party hex " + partyHex + " is outside selected pair " +
                    candidate.HexA + "<->" + candidate.HexB;
                Debug.Log("[W1B] " + _activePairDiagnostic, this);
                return false;
            }

            if (!TryActivateInternal(candidate.ForwardSeam, candidate.MapLayoutA, candidate.MapLayoutB))
            {
                _activePairDiagnostic = "ActivationFailed for " + candidate.HexA + "<->" + candidate.HexB;
                Debug.LogWarning("[W1B] " + _activePairDiagnostic, this);
                return false;
            }

            _activePairDiagnostic = "Active " + diagnostic;
            Debug.Log("[W1B] " + _activePairDiagnostic, this);
            return true;
        }

        /// <summary>
        /// Activates only cardinal, lattice-compatible Wilderness edges. Other edges deliberately
        /// remain on the legacy SurfaceExit path in W1B.
        /// </summary>
        public bool TryActivate(SurfaceExitConnection seam)
        {
            if (IsInternal(seam))
                return true;
            if (IsActive || _bootstrap?.Session == null || !_bootstrap.Session.IsInitialized ||
                seam.DestinationKind != SurfaceExitDestinationKind.WildernessHex ||
                Math.Abs(seam.LocalDirectionY) > 0.0001f || Math.Abs(seam.LocalDirectionX) < 0.0001f)
                return false;
            if (_bootstrap.Session.World.Strategic?.Sites != null &&
                _bootstrap.Session.World.Strategic.Sites.TryGetAtHex(seam.SourceHex, out var sourceSite) &&
                sourceSite != null)
                return false;
            if (!WildernessLocalMapFallback.TryResolve(
                    _bootstrap.Session.World, seam.SourceHex, out var sourceMapId) ||
                !WildernessLocalMapFallback.TryResolve(
                    _bootstrap.Session.World, seam.DestinationHex, out var destinationMapId))
                return false;

            return TryActivateInternal(seam, sourceMapId, destinationMapId);
        }

        bool TryActivateInternal(SurfaceExitConnection seam, string sourceMapId, string destinationMapId)
        {
            if (!TryResolveLayout(sourceMapId, out var source) ||
                !TryResolveLayout(destinationMapId, out var destination))
                return false;
            if (Math.Abs(source.CellSize - destination.CellSize) > 0.0001f ||
                source.Width != destination.Width || source.Height != destination.Height)
                return false;

            _tileMap = _tileMap != null ? _tileMap : _bootstrap.GetComponent<HostDemoTileMap>();
            if (_tileMap == null)
                return false;

            var cell = source.CellSize > 0f ? source.CellSize : 1f;
            _offsetA = Vector2.zero;
            _offsetB = new Vector2(Mathf.Sign(seam.LocalDirectionX) * source.Width * cell, 0f);
            try
            {
                _key = OwnerPrefix + seam.SourceHex + ">" + seam.DestinationHex;
                _sourceOwner = _key + ":A";
                _destinationOwner = _key + ":B";
                _hexA = seam.SourceHex;
                _hexB = seam.DestinationHex;
                _mapLayoutA = sourceMapId;
                _mapLayoutB = destinationMapId;
                _tileMap.RemoveLayoutInstance("legacy:active-localmap");
                _tileMap.BuildLayoutInstance(_sourceOwner, source, _offsetA);
                _tileMap.BuildLayoutInstance(_destinationOwner, destination, _offsetB);
                _compositeWalkGrid = WalkGridComposer.Compose(new List<WalkGridComposer.Input>
                {
                    new WalkGridComposer.Input(MapLayoutWalkGridBuilder.Create(source), _offsetA.x, _offsetA.y),
                    new WalkGridComposer.Input(MapLayoutWalkGridBuilder.Create(destination), _offsetB.x, _offsetB.y),
                });
                _bootstrap.MoveController.SetWalkGrid(_compositeWalkGrid);
                _bootstrap.MoveController.BindLocalMapContext(_key);
                _seamPresentationX = seam.LocalDirectionX > 0f
                    ? _offsetA.x + source.OriginX + source.Width * cell
                    : _offsetA.x + source.OriginX;
                _seam = seam;
                IsActive = true;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[W1B] TryActivateInternal failed: " + ex.Message, this);
                CleanupOwners();
                ResetState();
                return false;
            }
        }

        public bool TryCommitInternalCrossing(SurfaceExitConnection connection)
        {
            if (!IsInternal(connection))
                return false;
            var result = PlayerPartyWildernessTransitionService.TryCommitSeamlessWildernessCrossing(
                _bootstrap.Session.World, _bootstrap.Session.PlayerParty, connection);
            if (result.IsFailure)
                return false;
            _bootstrap.MoveController.BindLocalMapContext(_key);
            return true;
        }

        public bool TryCommitInternalCrossingPreservingAutoTravel(SurfaceExitConnection connection)
        {
            if (!IsInternal(connection))
                return false;
            var result =
                PlayerPartyWildernessTransitionService.TryCommitSeamlessWildernessCrossingPreservingLocalVisibleAutoTravel(
                    _bootstrap.Session.World, _bootstrap.Session.PlayerParty, connection);
            if (result.IsFailure)
                return false;
            _bootstrap.MoveController.BindLocalMapContext(_key);
            return true;
        }

        public bool TryActivateFirstUsableWildernessPair(IReadOnlyList<SurfaceExitVisibleZone> zones)
        {
            var world = _bootstrap?.Session?.World;
            var partyHex = world?.PlayerPartyTravel?.CurrentHex ?? default;
            return TryActivateAcceptancePair(world, zones, partyHex, out _);
        }

        public bool TryResolveInternalSeamApproachPresentation(
            HexCoord currentHex,
            HexCoord nextHex,
            out Vector3 presentationTarget)
        {
            presentationTarget = default;
            if (!IsActive || !IsInternalNeighbour(currentHex, nextHex))
                return false;

            var forward = _seam.LocalDirectionX > 0f;
            var onASide = currentHex.Equals(_hexA);
            var inset = 1.5f;
            float localX;
            float localY;
            if (onASide)
            {
                localX = forward ? _seamPresentationX - inset : _seamPresentationX + inset;
                localY = 0f;
                if (TryResolveLayout(_mapLayoutA, out var layoutA))
                    localY = layoutA.OriginY + layoutA.Height * layoutA.CellSize * 0.5f;
                SurfaceLocalToPresentation(_hexA, localX, localY, out var px, out var py);
                presentationTarget = new Vector3(px, py, HostPresentationSpace.EntityZ);
                return true;
            }

            localX = forward ? _seamPresentationX + inset : _seamPresentationX - inset;
            localY = 0f;
            if (TryResolveLayout(_mapLayoutB, out var layoutB))
                localY = layoutB.OriginY + layoutB.Height * layoutB.CellSize * 0.5f;
            SurfaceLocalToPresentation(_hexB, localX, localY, out var bx, out var by);
            presentationTarget = new Vector3(bx, by, HostPresentationSpace.EntityZ);
            return true;
        }

        /// <summary>Normal-walk seam detector. It commits only after the active view crosses the
        /// presentation boundary; it neither moves nor recreates that view.</summary>
        public void TryCommitNormalWalk(Vector3 presentationPosition)
        {
            if (!IsActive || _bootstrap?.Session?.World?.PlayerPartyTravel == null)
                return;
            var motion = _bootstrap.Session.World.PlayerPartyTravel;
            var preservingAutoTravel =
                PlayerPartyLocalVisibleAutoTravelService.IsActiveLocalVisibleAutoTravel(motion);
            var forward = _seam.LocalDirectionX > 0f;
            if (motion.CurrentHex.Equals(_seam.SourceHex) &&
                (forward
                    ? presentationPosition.x >= _seamPresentationX
                    : presentationPosition.x <= _seamPresentationX))
            {
                if (preservingAutoTravel)
                    TryCommitInternalCrossingPreservingAutoTravel(_seam);
                else
                    TryCommitInternalCrossing(_seam);
                return;
            }

            if (motion.CurrentHex.Equals(_seam.DestinationHex) &&
                (forward
                    ? presentationPosition.x <= _seamPresentationX
                    : presentationPosition.x >= _seamPresentationX))
            {
                var reverse = new SurfaceExitConnection(
                    _seam.DestinationHex, _seam.SourceHex,
                    WildernessLocalWorldProjection.OppositeDirection(_seam.DirectionIndex),
                    SurfaceExitDestinationKind.WildernessHex, string.Empty,
                    -_seam.LocalDirectionX, -_seam.LocalDirectionY,
                    0f, 0f, default,
                    _seam.BoundaryContactWorldX, _seam.BoundaryContactWorldY);
                if (preservingAutoTravel)
                    TryCommitInternalCrossingPreservingAutoTravel(reverse);
                else
                    TryCommitInternalCrossing(reverse);
            }
        }

        public void DeactivateToLegacy()
        {
            if (!IsActive)
                return;
            CleanupOwners();
            ResetState();
            _activePairDiagnostic = "DeactivatedToLegacy";
            Debug.Log("[W1B] " + _activePairDiagnostic, this);
            if (_bootstrap?.MoveController != null)
                _bootstrap.MoveController.BindLocalMapContext(string.Empty);
        }

        void CleanupOwners()
        {
            if (_tileMap == null)
                return;
            if (!string.IsNullOrEmpty(_sourceOwner))
                _tileMap.RemoveLayoutInstance(_sourceOwner);
            if (!string.IsNullOrEmpty(_destinationOwner))
                _tileMap.RemoveLayoutInstance(_destinationOwner);
        }

        void ResetState()
        {
            IsActive = false;
            _compositeWalkGrid = null;
            _key = _sourceOwner = _destinationOwner = string.Empty;
            _mapLayoutA = _mapLayoutB = string.Empty;
            _hexA = _hexB = default;
            _offsetA = _offsetB = Vector2.zero;
            _seam = default;
            _seamPresentationX = 0f;
        }

        bool TryResolveLayout(string mapId, out MapLayoutDefinition layout)
        {
            layout = null;
            var parsed = XianXia.Core.Domain.Ids.DefinitionId.Parse(mapId);
            return parsed.IsSuccess &&
                   _bootstrap.Session.Registry.TryGetMapLayout(parsed.Value, out layout) &&
                   layout != null;
        }

        static bool SameEdge(SurfaceExitConnection a, SurfaceExitConnection b) =>
            a.SourceHex.Equals(b.SourceHex) && a.DestinationHex.Equals(b.DestinationHex) &&
            a.DirectionIndex == b.DirectionIndex;

        static bool IsReverseEdge(SurfaceExitConnection a, SurfaceExitConnection b) =>
            a.SourceHex.Equals(b.DestinationHex) && a.DestinationHex.Equals(b.SourceHex);
    }
}
