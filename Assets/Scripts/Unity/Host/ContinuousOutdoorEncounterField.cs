using System;
using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Data.Content;
using XianXia.Core.Exploration;
using XianXia.Core.Navigation;
using XianXia.Core.Results;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Surface;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    public sealed partial class ContinuousOutdoorSurfaceRuntime
    {
        string _independentFieldId = string.Empty;
        public string IndependentFieldId => _independentFieldId;
        string SurfaceOwnerKey(SurfaceChunkCoord coord) =>
            (string.IsNullOrEmpty(_independentFieldId) ? "" : _independentFieldId + ":") +
            SurfaceChunkNeighborhood.OwnerKey(_surfaceId, coord);

        public Result PreflightIndependentField(CharacterEncounterState state)
        {
            if (state == null || !IsActive || state.SourceSurfaceId != _surfaceId ||
                !TryResolveSurface(out var surface) || !ReferenceEquals(_navigationStateWorld, _bootstrap.Session.World))
                return Result.Failure(ErrorCode.InvalidOperation, "Independent field source is unavailable.");
            var inputs = new List<WalkGridComposer.Input>();
            foreach (var chunk in surface.Chunks)
            {
                if (!EncounterTouchesChunk(state, chunk.Coord)) continue;
                if (!TryResolveSource(chunk.Coord, out var layout))
                    return Result.Failure(ErrorCode.ContentLoadFailed, "Missing real source chunk: " + chunk.StableChunkId);
                _mapper.ChunkLocalToWorld(chunk.Coord, 0f, 0f, out var wx, out var wy);
                _mapper.WorldToPresentation(wx, wy, out var px, out var py);
                inputs.Add(new WalkGridComposer.Input(MapLayoutWalkGridBuilder.Create(layout), px - layout.OriginX, py - layout.OriginY));
                var blockers = BuildSiteBlockerGrid(chunk.Coord, px, py, layout);
                if (blockers != null) inputs.Add(new WalkGridComposer.Input(blockers, 0f, 0f));
                var geography = BuildGeographyBlockerGrid(chunk.Coord, px, py, layout);
                if (geography != null) inputs.Add(new WalkGridComposer.Input(geography, 0f, 0f));
            }
            if (inputs.Count == 0) return Result.Failure(ErrorCode.ContentLoadFailed, "No authored surface in frozen field.");
            var grid = WalkGridComposer.Compose(inputs);
            foreach (var p in state.Participants)
            {
                _mapper.WorldToPresentation(p.TacticalX, p.TacticalY, out var px, out var py);
                if (!grid.TryWorldToCell(px, py, out var x, out var y) || !grid.IsWalkable(x, y))
                    return Result.Failure(ErrorCode.InvalidOperation, "Personal field position is not walkable: CharacterId=" + p.CharacterId);
            }
            return Result.Success();
        }

        bool EncounterTouchesChunk(CharacterEncounterState state, SurfaceChunkCoord chunk)
        {
            _mapper.ChunkLocalToWorld(chunk, 0f, 0f, out var x, out var y);
            return x < state.CenterX + state.Width * .5f && x + _mapper.ChunkWidth > state.CenterX - state.Width * .5f &&
                   y < state.CenterY + state.Height * .5f && y + _mapper.ChunkHeight > state.CenterY - state.Height * .5f;
        }

        void RemovePresentedField()
        {
            CancelNeighborhoodTransition();
            foreach (var coord in _presentedChunks)
            {
                _tileMap.RemoveLayoutInstance(SurfaceOwnerKey(coord));
                _tileMap.RemoveLayoutInstance(GeographyOwnerKey(coord));
                RemoveSitePlacementInstances(coord);
            }
            _presentedChunks.Clear(); _loaded.Clear(); _materializedSitePlacementOwners.Clear();
        }

        public Result EnterIndependentField(CharacterEncounterState state)
        {
            var valid = PreflightIndependentField(state);
            if (valid.IsFailure) return valid;
            RemovePresentedField();
            _independentFieldId = state.EncounterId;
            TryResolveSurface(out var surface);
            // Build dedicated owner roots from formal sources, including chunks outside streaming.
            // The frozen rectangle remains full-sized; areas outside the defined continent are void.
            foreach (var chunk in surface.Chunks)
                if (EncounterTouchesChunk(state, chunk.Coord))
                {
                    BuildChunk(chunk.Coord);
                    _loaded.Add(chunk.Coord); _presentedChunks.Add(chunk.Coord);
                }
            RecomposeWalkGrid();
            for (var y = 0; y < _compositeWalkGrid.Height; y++)
            for (var x = 0; x < _compositeWalkGrid.Width; x++)
            {
                _compositeWalkGrid.CellToWorldCenter(x, y, out var px, out var py);
                _mapper.PresentationToWorld(px, py, out var wx, out var wy);
                if (!state.Contains(wx, wy)) _compositeWalkGrid.SetBlocked(x, y, true);
            }
            var world = _bootstrap.Session.World;
            _bootstrap.MoveController.InvalidatePartyLocalMovement(_bootstrap.Session.PlayerParty.Members);
            foreach (var p in state.Participants)
            {
                var id = new EntityId(p.CharacterId);
                world.Entities.TryGet(id, out var entity);
                if (!entity.TryGet<EntityLocationComponent>(out var location))
                { location = new EntityLocationComponent(); entity.AddComponent(location); }
                _mapper.WorldToPresentation(p.TacticalX, p.TacticalY, out var px, out var py);
                location.SetPresentationOverride(px, py);
                _bootstrap.MoveController.CancelPresentationMovementPublic(id);
                if (_bootstrap.ViewSpawner.Registry.TryGet(id, out var view) && view != null)
                    view.transform.position = HostPresentationSpace.FromPresentation(px, py);
            }
            ReconcileOutdoorEntityMaterialization();
            Debug.Log("[IndependentEncounter] Id=" + state.EncounterId + " Source=" + state.SourceSurfaceId +
                " Center=" + state.CenterX + "," + state.CenterY + " Size=" + state.Width + "x" + state.Height +
                " SourceChunks=" + _loaded.Count + " Participants=" + state.Participants.Count);
            return Result.Success();
        }

        public bool PrepareInterventionPlacement(EncounterCandidate candidate)
        {
            var state = _bootstrap?.Session?.World?.Strategic?.CharacterEncounter;
            if (state == null || state.EncounterId != _independentFieldId || _compositeWalkGrid == null) return false;
            foreach (var member in candidate.Members)
            {
                _mapper.WorldToPresentation(member.OriginX, member.OriginY, out var px, out var py);
                if (!_compositeWalkGrid.TryWorldToCell(px, py, out var x, out var y)) return false;
                if (!_compositeWalkGrid.IsWalkable(x, y))
                {
                    if (!_compositeWalkGrid.TryFindNearestWalkable(x, y, 1, out x, out y)) return false;
                    _compositeWalkGrid.CellToWorldCenter(x, y, out px, out py);
                }
                _mapper.PresentationToWorld(px, py, out member.TacticalX, out member.TacticalY);
                if (!state.Contains(member.TacticalX, member.TacticalY)) return false;
            }
            return true;
        }

        public void PresentJoinedParticipants(int previousCount)
        {
            var world = _bootstrap.Session.World;
            var state = world.Strategic.CharacterEncounter;
            for (var i = previousCount; i < state.Participants.Count; i++)
            {
                var p = state.Participants[i];
                world.Entities.TryGet(new EntityId(p.CharacterId), out var entity);
                if (!entity.TryGet<EntityLocationComponent>(out var location))
                { location = new EntityLocationComponent(); entity.AddComponent(location); }
                _mapper.WorldToPresentation(p.TacticalX, p.TacticalY, out var x, out var y);
                location.SetPresentationOverride(x, y);
                _bootstrap.MoveController.CancelPresentationMovementPublic(new EntityId(p.CharacterId));
            }
            ReconcileOutdoorEntityMaterialization();
        }

        public void CaptureIndependentField()
        {
            var state = _bootstrap?.Session?.World?.Strategic?.CharacterEncounter;
            if (state == null || state.EncounterId != _independentFieldId || state.Phase == CharacterEncounterPhase.Committed ||
                !ReferenceEquals(_navigationStateWorld, _bootstrap.Session.World)) return;
            foreach (var p in state.Participants)
                if (_bootstrap.ViewSpawner.Registry.TryGet(new EntityId(p.CharacterId), out var view) && view != null)
                {
                    _mapper.PresentationToWorld(view.transform.position.x, view.transform.position.y, out var x, out var y);
                    if (state.Contains(x, y)) { p.TacticalX = x; p.TacticalY = y; }
                }
        }

        public void LeaveIndependentField(CharacterEncounterState completed)
        {
            if (completed == null || completed.EncounterId != _independentFieldId) return;
            var world = _bootstrap.Session.World;
            foreach (var p in completed.Participants)
            {
                var id = new EntityId(p.CharacterId);
                _bootstrap.MoveController.CancelPresentationMovementPublic(id);
                if (world.Entities.TryGet(id, out var entity) && entity.TryGet<EntityLocationComponent>(out var location))
                {
                    _mapper.WorldToPresentation(p.OriginX, p.OriginY, out var x, out var y);
                    location.SetPresentationOverride(x, y);
                    if (_bootstrap.ViewSpawner.Registry.TryGet(id, out var view) && view != null)
                        view.transform.position = HostPresentationSpace.FromPresentation(x, y);
                }
            }
            RemovePresentedField(); _independentFieldId = string.Empty;
            // Ordinary activation reads the unchanged original party motion and individual presence.
            IsActive = false;
            TryActivateAtCurrentWorldPosition();
        }
    }
}
