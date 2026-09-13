using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Data.Content;
using XianXia.Core.Exploration;
using XianXia.Core.Navigation;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Surface;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    public sealed partial class ContinuousOutdoorSurfaceRuntime
    {
        string _independentFieldId = string.Empty;
        string _stagingIndependentFieldId = string.Empty;
        readonly List<SurfaceChunkCoord> _stagingIndependentChunks = new List<SurfaceChunkCoord>();
        Coroutine _independentNavigationRefresh;
        public string IndependentPreparationProgress { get; private set; } = string.Empty;
        public string IndependentFieldId => _independentFieldId;
        public bool IsIndependentFieldPreparing => !string.IsNullOrEmpty(_stagingIndependentFieldId);

        public sealed class PreparedIndependentField
        {
            internal SimulationWorld World;
            internal CharacterEncounterState State;
            internal readonly List<SurfaceChunkCoord> Chunks = new List<SurfaceChunkCoord>();
            internal WalkGrid Grid;
            internal int InputGridCount;
            internal int OutputGridCells;
            internal ulong TopologyRevision;
            internal string SurfaceId = string.Empty;
        }

        string SurfaceOwnerKey(SurfaceChunkCoord coord) =>
            ((string.IsNullOrEmpty(_independentFieldId) ? _stagingIndependentFieldId : _independentFieldId) +
             (string.IsNullOrEmpty(_independentFieldId) && string.IsNullOrEmpty(_stagingIndependentFieldId) ? "" : ":")) +
            SurfaceChunkNeighborhood.OwnerKey(_surfaceId, coord);

        /// <summary>Builds only pure source/navigation data in slices; it does not mutate encounter state.</summary>
        public IEnumerator PrepareIndependentField(
            CharacterEncounterState state, Action<Result, PreparedIndependentField> completed)
        {
            var world = _bootstrap?.Session?.World;
            if (state == null || !IsActive || state.SourceSurfaceId != _surfaceId ||
                !TryResolveSurface(out var surface) || !ReferenceEquals(_navigationStateWorld, world))
            {
                completed?.Invoke(Result.Failure(ErrorCode.InvalidOperation, "Independent field source is unavailable."), null);
                yield break;
            }
            var plan = new PreparedIndependentField
            {
                World = world, State = state, SurfaceId = _surfaceId,
                TopologyRevision = world.OutdoorStatefulObjects?.DestructibleTopologyRevision ?? 0
            };
            var inputs = new List<WalkGridComposer.Input>();
            var started = Time.realtimeSinceStartup;
            for (var i = 0; i < surface.Chunks.Count; i++)
            {
                var chunk = surface.Chunks[i];
                if (!EncounterTouchesChunk(state, chunk.Coord)) continue;
                if (!TryResolveSource(chunk.Coord, out var layout))
                {
                    completed?.Invoke(Result.Failure(ErrorCode.ContentLoadFailed, "Missing real source chunk: " + chunk.StableChunkId), null);
                    yield break;
                }
                _mapper.ChunkLocalToWorld(chunk.Coord, 0f, 0f, out var wx, out var wy);
                _mapper.WorldToPresentation(wx, wy, out var px, out var py);
                inputs.Add(new WalkGridComposer.Input(MapLayoutWalkGridBuilder.Create(layout), px - layout.OriginX, py - layout.OriginY));
                var blockers = BuildSiteBlockerGrid(chunk.Coord, px, py, layout);
                if (blockers != null) inputs.Add(new WalkGridComposer.Input(blockers, 0f, 0f));
                var geography = BuildGeographyBlockerGrid(chunk.Coord, px, py, layout);
                if (geography != null) inputs.Add(new WalkGridComposer.Input(geography, 0f, 0f));
                plan.Chunks.Add(chunk.Coord);
                if (Time.realtimeSinceStartup - started >= .004f) { started = Time.realtimeSinceStartup; yield return null; }
            }
            if (inputs.Count == 0)
            {
                completed?.Invoke(Result.Failure(ErrorCode.ContentLoadFailed, "No authored surface in frozen field."), null);
                yield break;
            }
            WalkGridComposer.Job job;
            try { job = new WalkGridComposer.Job(inputs); }
            catch (Exception ex)
            {
                completed?.Invoke(Result.Failure(ErrorCode.InvalidOperation, "Navigation preparation failed: " + ex.Message), null);
                yield break;
            }
            while (!job.IsComplete)
            {
                var slice = Time.realtimeSinceStartup;
                do { job.Step(8192); }
                while (!job.IsComplete && Time.realtimeSinceStartup - slice < .004f);
                yield return null;
            }
            plan.Grid = job.Result;
            plan.InputGridCount = job.InputCount;
            plan.OutputGridCells = job.Width * job.Height;
            foreach (var pair in world.Strategic.FactionFlags.Flags)
                if (pair.Value != null && pair.Value.SurfaceId == state.SourceSurfaceId)
                    HostFactionFlagQuery.ApplyWalkGridBlock(pair.Value, this, plan.Grid);
            yield return ClipPreparedEncounterGrid(plan.Grid, state);
            foreach (var p in state.Participants)
            {
                _mapper.WorldToPresentation(p.TacticalX, p.TacticalY, out var px, out var py);
                if (!plan.Grid.TryWorldToCell(px, py, out var x, out var y) || !plan.Grid.IsWalkable(x, y))
                {
                    completed?.Invoke(Result.Failure(ErrorCode.InvalidOperation, "Personal field position is not walkable: CharacterId=" + p.CharacterId), null);
                    yield break;
                }
            }
            completed?.Invoke(Result.Success(), plan);
        }

        /// <summary>Stages source presentation under a private owner, then performs one bounded takeover.</summary>
        public IEnumerator CommitPreparedIndependentField(
            PreparedIndependentField plan, Action<Result> completed, bool domainAlreadyBound = false)
        {
            if (plan == null || plan.World == null || plan.State == null ||
                !ReferenceEquals(plan.World, _bootstrap?.Session?.World) || plan.SurfaceId != _surfaceId ||
                plan.TopologyRevision != (plan.World.OutdoorStatefulObjects?.DestructibleTopologyRevision ?? 0))
            {
                completed?.Invoke(Result.Failure(ErrorCode.InvalidOperation, "Prepared independent field expired."));
                yield break;
            }
            _stagingIndependentFieldId = plan.State.EncounterId;
            _stagingIndependentChunks.Clear();
            IndependentPreparationProgress = "构建独立战场 0/" + plan.Chunks.Count;
            var started = Time.realtimeSinceStartup;
            var sliceStarted = started;
            Debug.Log("[IndependentEncounter] build start Id=" + plan.State.EncounterId +
                " chunks=" + plan.Chunks.Count + " cells=" + plan.OutputGridCells);
            for (var i = 0; i < plan.Chunks.Count; i++)
            {
                if (!ReferenceEquals(plan.World, _bootstrap?.Session?.World) ||
                    plan.TopologyRevision != (plan.World.OutdoorStatefulObjects?.DestructibleTopologyRevision ?? 0))
                {
                    CancelPreparedIndependentField();
                    completed?.Invoke(Result.Failure(ErrorCode.InvalidOperation, "Prepared field changed during build."));
                    yield break;
                }
                Exception failure = null;
                // Include a partially built chunk in rollback, even if BuildChunk throws.
                _stagingIndependentChunks.Add(plan.Chunks[i]);
                try
                {
                    BuildChunk(plan.Chunks[i]);
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
                if (failure != null)
                {
                    CancelPreparedIndependentField();
                    completed?.Invoke(Result.Failure(ErrorCode.InvalidOperation, "Independent field build failed: " + failure));
                    yield break;
                }
                IndependentPreparationProgress = "构建独立战场 " + (i + 1) + "/" + plan.Chunks.Count;
                if (Time.realtimeSinceStartup - sliceStarted >= .004f)
                {
                    yield return null;
                    sliceStarted = Time.realtimeSinceStartup;
                }
            }
            Debug.Log("[IndependentEncounter] build complete Id=" + plan.State.EncounterId +
                " elapsedSeconds=" + (Time.realtimeSinceStartup - started));
            completed?.Invoke(CompletePreparedTakeover(plan, domainAlreadyBound));
        }

        Result CompletePreparedTakeover(PreparedIndependentField plan, bool domainAlreadyBound)
        {
            var beganHere = false;
            var normalRemoved = false;
            try
            {
                // Nothing in the domain changes until every staged chunk and the prepared grid exist.
                if (!domainAlreadyBound)
                {
                    var begun = CharacterEncounterService.Begin(plan.World, plan.State);
                    if (begun.IsFailure) { CancelPreparedIndependentField(); return begun; }
                    beganHere = true;
                }
                RemoveNormalPresentedField();
                normalRemoved = true;
                _independentFieldId = _stagingIndependentFieldId;
                _loaded.Clear(); _presentedChunks.Clear();
                for (var i = 0; i < plan.Chunks.Count; i++) { _loaded.Add(plan.Chunks[i]); _presentedChunks.Add(plan.Chunks[i]); }
                _compositeWalkGrid = plan.Grid;
                _bootstrap.MoveController.SetWalkGrid(_compositeWalkGrid);
                NavigationGeneration++;
                _bootstrap.MoveController.InvalidatePartyLocalMovement(_bootstrap.Session.PlayerParty.Members);
                foreach (var p in plan.State.Participants)
                {
                    var id = new EntityId(p.CharacterId);
                    plan.World.Entities.TryGet(id, out var entity);
                    if (!entity.TryGet<EntityLocationComponent>(out var location)) { location = new EntityLocationComponent(); entity.AddComponent(location); }
                    _mapper.WorldToPresentation(p.TacticalX, p.TacticalY, out var px, out var py);
                    location.SetPresentationOverride(px, py);
                    _bootstrap.MoveController.CancelPresentationMovementPublic(id);
                }
                ReconcileOutdoorEntityMaterialization();
                // Keep the staging owner until every takeover step has completed, so a later
                // exception can still remove the exact roots it created.
                _stagingIndependentFieldId = string.Empty;
                _stagingIndependentChunks.Clear();
                Debug.Log("[IndependentEncounter] commit Id=" + plan.State.EncounterId + " chunks=" + plan.Chunks.Count +
                    " inputs=" + plan.InputGridCount + " cells=" + plan.OutputGridCells);
                return Result.Success();
            }
            catch (Exception ex)
            {
                if (beganHere) CharacterEncounterService.AbortEntry(plan.World);
                CancelPreparedIndependentField();
                _independentFieldId = string.Empty;
                if (normalRemoved) { IsActive = false; TryActivateAtCurrentWorldPosition(); }
                return Result.Failure(ErrorCode.InvalidOperation, "Independent field takeover failed: " + ex.Message);
            }
        }

        public bool BeginIndependentFieldRestore(CharacterEncounterState state)
        {
            if (state == null || IsIndependentFieldPreparing || !string.IsNullOrEmpty(_independentFieldId)) return false;
            StartCoroutine(RestorePreparedIndependentField(state));
            return true;
        }

        IEnumerator RestorePreparedIndependentField(CharacterEncounterState state)
        {
            const string restoreOwner = "CharacterEncounterRestore";
            _bootstrap.Session.AcquireModalPause(restoreOwner);
            Result prepared = default;
            PreparedIndependentField field = null;
            yield return StartCoroutine(PrepareIndependentField(state, (result, plan) => { prepared = result; field = plan; }));
            if (prepared.IsSuccess && field != null)
            {
                Result committed = default;
                yield return StartCoroutine(CommitPreparedIndependentField(field, result => committed = result, domainAlreadyBound: true));
                if (committed.IsFailure) Debug.LogError("[EncounterRestore] " + committed.Error.Message);
            }
            else Debug.LogError("[EncounterRestore] " + prepared.Error.Message);
            _bootstrap.Session.ReleaseModalPause(restoreOwner);
        }

        public void CancelPreparedIndependentField()
        {
            if (string.IsNullOrEmpty(_stagingIndependentFieldId)) return;
            var staged = _stagingIndependentFieldId;
            foreach (var coord in _stagingIndependentChunks)
            {
                _tileMap.RemoveLayoutInstance(staged + ":" + SurfaceChunkNeighborhood.OwnerKey(_surfaceId, coord));
                _tileMap.RemoveLayoutInstance(staged + ":" + SurfaceChunkNeighborhood.OwnerKey(_surfaceId, coord) + ":geography");
                RemoveSitePlacementInstances(coord, staged);
            }
            _stagingIndependentChunks.Clear();
            _stagingIndependentFieldId = string.Empty;
        }

        public bool BeginIndependentNavigationRefresh(ulong requestedTopologyRevision)
        {
            if (_independentNavigationRefresh != null || string.IsNullOrEmpty(_independentFieldId)) return false;
            var state = _bootstrap?.Session?.World?.Strategic?.CharacterEncounter;
            if (state == null || state.EncounterId != _independentFieldId) return false;
            _independentNavigationRefresh = StartCoroutine(RefreshIndependentNavigation(state, requestedTopologyRevision));
            return true;
        }

        IEnumerator RefreshIndependentNavigation(CharacterEncounterState state, ulong requestedTopologyRevision)
        {
            Result prepared = default;
            PreparedIndependentField field = null;
            yield return StartCoroutine(PrepareIndependentField(state, (result, plan) => { prepared = result; field = plan; }));
            var board = _bootstrap?.Session?.World?.OutdoorStatefulObjects;
            if (prepared.IsSuccess && field != null && board != null &&
                requestedTopologyRevision == board.DestructibleTopologyRevision &&
                state.EncounterId == _independentFieldId)
            {
                _compositeWalkGrid = field.Grid;
                _bootstrap.MoveController.SetWalkGrid(_compositeWalkGrid);
                NavigationGeneration++;
                _navigationStateWorld = field.World;
                _observedDestructibleTopologyRevision = requestedTopologyRevision;
                _dynamicNavigationDirty = false;
            }
            else if (prepared.IsFailure)
                Debug.LogError("[IndependentEncounter] dynamic navigation refresh failed: " + prepared.Error.Message);
            _independentNavigationRefresh = null;
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

        void RemoveNormalPresentedField()
        {
            foreach (var coord in _presentedChunks)
            {
                _tileMap.RemoveLayoutInstance(SurfaceChunkNeighborhood.OwnerKey(_surfaceId, coord));
                _tileMap.RemoveLayoutInstance(SurfaceChunkNeighborhood.OwnerKey(_surfaceId, coord) + ":geography");
                RemoveSitePlacementInstances(coord, string.Empty);
            }
            _presentedChunks.Clear(); _loaded.Clear();
        }

        void ClipEncounterGrid(WalkGrid grid)
        {
            var state = _bootstrap?.Session?.World?.Strategic?.CharacterEncounter;
            if (state == null || state.EncounterId != _independentFieldId) return;
            ClipEncounterGrid(grid, state);
        }

        void ClipEncounterGrid(WalkGrid grid, CharacterEncounterState state)
        {
            if (grid == null || state == null) return;
            for (var y = 0; y < grid.Height; y++)
            for (var x = 0; x < grid.Width; x++)
            {
                grid.CellToWorldCenter(x, y, out var px, out var py);
                _mapper.PresentationToWorld(px, py, out var wx, out var wy);
                if (!state.Contains(wx, wy)) grid.SetBlocked(x, y, true);
            }
        }

        IEnumerator ClipPreparedEncounterGrid(WalkGrid grid, CharacterEncounterState state)
        {
            if (grid == null || state == null) yield break;
            var budget = 0;
            var slice = Time.realtimeSinceStartup;
            for (var y = 0; y < grid.Height; y++)
            for (var x = 0; x < grid.Width; x++)
            {
                grid.CellToWorldCenter(x, y, out var px, out var py);
                _mapper.PresentationToWorld(px, py, out var wx, out var wy);
                if (!state.Contains(wx, wy)) grid.SetBlocked(x, y, true);
                if (++budget == 8192)
                {
                    budget = 0;
                    if (Time.realtimeSinceStartup - slice >= .004f)
                    { yield return null; slice = Time.realtimeSinceStartup; }
                }
            }
        }

        public Result PrepareEncounterReturn(CharacterEncounterState state)
        {
            // The field owns its current clipped grid; do not synchronously compose the whole
            // source again while closing a battle.
            if (_compositeWalkGrid == null)
                return Result.Failure(ErrorCode.InvalidOperation, "Encounter navigation is unavailable for return.");
            var points = new List<Vector2>();
            foreach (var p in state.Participants)
            {
                _mapper.WorldToPresentation(p.OriginX, p.OriginY, out var px, out var py);
                if (!_compositeWalkGrid.TryWorldToCell(px, py, out var x, out var y))
                    return Result.Failure(ErrorCode.InvalidOperation, "Return anchor outside source: " + p.CharacterId);
                if (!_compositeWalkGrid.IsWalkable(x, y))
                {
                    if (!_compositeWalkGrid.TryFindNearestWalkable(x, y, 1, out x, out y))
                        return Result.Failure(ErrorCode.InvalidOperation, "No adjacent legal return anchor: " + p.CharacterId);
                    _compositeWalkGrid.CellToWorldCenter(x, y, out px, out py);
                }
                _mapper.PresentationToWorld(px, py, out var wx, out var wy);
                points.Add(new Vector2(wx, wy));
            }
            for (var i = 0; i < points.Count; i++)
            { state.Participants[i].OriginX = points[i].x; state.Participants[i].OriginY = points[i].y; }
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
            if (_independentNavigationRefresh != null) StopCoroutine(_independentNavigationRefresh);
            _independentNavigationRefresh = null;
            RemovePresentedField(); _independentFieldId = string.Empty;
            // Ordinary activation reads the unchanged original party motion and individual presence.
            IsActive = false;
            TryActivateAtCurrentWorldPosition();
        }
    }
}
