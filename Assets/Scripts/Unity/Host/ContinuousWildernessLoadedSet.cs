using System;
using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Navigation;
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
        string _key = string.Empty;
        string _sourceOwner = string.Empty;
        string _destinationOwner = string.Empty;
        float _seamPresentationX;

        public bool IsActive { get; private set; }
        public string Key => _key;
        public bool IsInternal(SurfaceExitConnection connection) =>
            IsActive && (SameEdge(_seam, connection) || IsReverseEdge(_seam, connection));

        public void Bind(PlayableHostBootstrap bootstrap)
        {
            _bootstrap = bootstrap;
            _tileMap = bootstrap != null ? bootstrap.GetComponent<HostDemoTileMap>() : null;
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
                _bootstrap.Session.World.Strategic.Sites.TryGetAtHex(seam.SourceHex, out var sourceSite) && sourceSite != null)
                return false;
            if (!WildernessLocalMapFallback.TryResolve(
                    _bootstrap.Session.World, seam.SourceHex, out var sourceMapId) ||
                !WildernessLocalMapFallback.TryResolve(
                    _bootstrap.Session.World, seam.DestinationHex, out var destinationMapId))
                return false;
            if (!TryResolveLayout(sourceMapId, out var source) || !TryResolveLayout(destinationMapId, out var destination) ||
                Math.Abs(source.CellSize - destination.CellSize) > 0.0001f ||
                source.Width != destination.Width || source.Height != destination.Height)
                return false;

            _tileMap = _tileMap != null ? _tileMap : _bootstrap.GetComponent<HostDemoTileMap>();
            if (_tileMap == null)
                return false;

            var cell = source.CellSize > 0f ? source.CellSize : 1f;
            var destinationOffset = new Vector2(
                Mathf.Sign(seam.LocalDirectionX) * source.Width * cell, 0f);
            try
            {
                _key = OwnerPrefix + seam.SourceHex + ">" + seam.DestinationHex;
                _sourceOwner = _key + ":A";
                _destinationOwner = _key + ":B";
                // Replace only the legacy owner; never call global Rebuild/Clear while a pair is active.
                _tileMap.RemoveLayoutInstance("legacy:active-localmap");
                _tileMap.BuildLayoutInstance(_sourceOwner, source, Vector2.zero);
                _tileMap.BuildLayoutInstance(_destinationOwner, destination, destinationOffset);
                var composite = WalkGridComposer.Compose(new List<WalkGridComposer.Input>
                {
                    new WalkGridComposer.Input(MapLayoutWalkGridBuilder.Create(source), 0f, 0f),
                    new WalkGridComposer.Input(MapLayoutWalkGridBuilder.Create(destination), destinationOffset.x, destinationOffset.y),
                });
                _bootstrap.MoveController.SetWalkGrid(composite);
                _bootstrap.MoveController.BindLocalMapContext(_key);
                _seamPresentationX = seam.LocalDirectionX > 0f
                    ? source.OriginX + source.Width * cell
                    : source.OriginX;
                _seam = seam;
                IsActive = true;
                return true;
            }
            catch (Exception)
            {
                CleanupOwners();
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
            // The logical primary map changes, but the shared movement context and EntityView stay put.
            _bootstrap.MoveController.BindLocalMapContext(_key);
            return true;
        }

        public bool TryActivateFirstUsableWildernessPair(IReadOnlyList<SurfaceExitVisibleZone> zones)
        {
            if (zones == null)
                return false;
            for (var i = 0; i < zones.Count; i++)
                if (TryActivate(zones[i].Connection))
                    return true;
            return false;
        }

        /// <summary>Normal-walk seam detector. It commits only after the active view crosses the
        /// presentation boundary; it neither moves nor recreates that view.</summary>
        public void TryCommitNormalWalk(Vector3 presentationPosition)
        {
            if (!IsActive || _bootstrap?.Session?.World?.PlayerPartyTravel == null)
                return;
            var motion = _bootstrap.Session.World.PlayerPartyTravel;
            var forward = _seam.LocalDirectionX > 0f;
            if (motion.CurrentHex.Equals(_seam.SourceHex) &&
                (forward ? presentationPosition.x >= _seamPresentationX : presentationPosition.x <= _seamPresentationX))
            {
                TryCommitInternalCrossing(_seam);
                return;
            }
            if (motion.CurrentHex.Equals(_seam.DestinationHex) &&
                (forward ? presentationPosition.x <= _seamPresentationX : presentationPosition.x >= _seamPresentationX))
            {
                var reverse = new SurfaceExitConnection(
                    _seam.DestinationHex, _seam.SourceHex,
                    WildernessLocalWorldProjection.OppositeDirection(_seam.DirectionIndex),
                    SurfaceExitDestinationKind.WildernessHex, string.Empty,
                    -_seam.LocalDirectionX, -_seam.LocalDirectionY,
                    0f, 0f, default,
                    _seam.BoundaryContactWorldX, _seam.BoundaryContactWorldY);
                TryCommitInternalCrossing(reverse);
            }
        }

        public void DeactivateToLegacy()
        {
            if (!IsActive)
                return;
            CleanupOwners();
            IsActive = false;
            _key = _sourceOwner = _destinationOwner = string.Empty;
            _seam = default;
            _seamPresentationX = 0f;
        }

        void CleanupOwners()
        {
            if (_tileMap == null)
                return;
            if (!string.IsNullOrEmpty(_sourceOwner)) _tileMap.RemoveLayoutInstance(_sourceOwner);
            if (!string.IsNullOrEmpty(_destinationOwner)) _tileMap.RemoveLayoutInstance(_destinationOwner);
        }

        bool TryResolveLayout(string mapId, out MapLayoutDefinition layout)
        {
            layout = null;
            var parsed = XianXia.Core.Domain.Ids.DefinitionId.Parse(mapId);
            return parsed.IsSuccess && _bootstrap.Session.Registry.TryGetMapLayout(parsed.Value, out layout) && layout != null;
        }

        static bool SameEdge(SurfaceExitConnection a, SurfaceExitConnection b) =>
            a.SourceHex.Equals(b.SourceHex) && a.DestinationHex.Equals(b.DestinationHex) &&
            a.DirectionIndex == b.DirectionIndex;

        static bool IsReverseEdge(SurfaceExitConnection a, SurfaceExitConnection b) =>
            a.SourceHex.Equals(b.DestinationHex) && a.DestinationHex.Equals(b.SourceHex);
    }
}
