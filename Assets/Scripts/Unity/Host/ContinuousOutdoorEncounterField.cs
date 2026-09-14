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
        HashSet<string> _navigationDestroyed = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<SurfaceChunkCoord> _explicitNavigationChunks = new HashSet<SurfaceChunkCoord>();
        HashSet<SurfaceChunkCoord> _flagNavigationChunks = new HashSet<SurfaceChunkCoord>();
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
            internal OutdoorWorldSurfaceDefinition Source;
            internal readonly Dictionary<string, ulong> SquadCommands = new Dictionary<string, ulong>();
            internal readonly Dictionary<string, int> SquadCounts = new Dictionary<string, int>();
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
            IndependentPreparationProgress = "读取同源地形";
            if (state == null || !IsActive || state.SourceSurfaceId != _surfaceId ||
                !TryResolveSurface(out var surface) || !ReferenceEquals(_navigationStateWorld, world))
            {
                completed?.Invoke(Result.Failure(ErrorCode.InvalidOperation, "Independent field source is unavailable."), null);
                yield break;
            }
            var plan = new PreparedIndependentField
            {
                World = world, State = state, SurfaceId = _surfaceId, Source = surface,
                TopologyRevision = world.OutdoorStatefulObjects?.DestructibleTopologyRevision ?? 0
            };
            foreach (var p in state.Participants)
                if (world.Strategic.Squads.TryGet(p.SquadId, out var squad))
                {
                    plan.SquadCommands[p.SquadId] = squad.CommandRevision;
                    plan.SquadCounts[p.SquadId] = squad.MemberCharacterIds.Count;
                }
            var inputs = new List<WalkGridComposer.Input>();
            var started = Time.realtimeSinceStartup;
            var prepareStarted = started;
            var minWX = float.PositiveInfinity; var minWY = float.PositiveInfinity;
            var maxWX = float.NegativeInfinity; var maxWY = float.NegativeInfinity;
            foreach (var authored in surface.Chunks)
            {
                _mapper.ChunkLocalToWorld(authored.Coord, 0, 0, out var x, out var y);
                minWX = Mathf.Min(minWX, x); minWY = Mathf.Min(minWY, y);
                maxWX = Mathf.Max(maxWX, x + surface.ChunkWidth); maxWY = Mathf.Max(maxWY, y + surface.ChunkHeight);
            }
            Debug.Log("[IndependentEncounter] prepare Id=" + state.EncounterId + " surface=" + surface.SurfaceId +
                " frozenWorldRect=" + state.CenterX + "," + state.CenterY + ";" + state.Width + "x" + state.Height +
                " sourceWorldBounds=" + minWX + "," + minWY + ".." + maxWX + "," + maxWY +
                " cellSizeWorld=" + surface.CellSize + " presentationUnitsPerWorldUnit=" + _mapper.PresentationUnitsPerWorldUnit);
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
                IndependentPreparationProgress = "读取同源地形 " + (i + 1) + "/" + surface.Chunks.Count;
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
                IndependentPreparationProgress = "合成导航 " + job.Width + "×" + job.Height + " 格";
                var slice = Time.realtimeSinceStartup;
                do { job.Step(8192); }
                while (!job.IsComplete && Time.realtimeSinceStartup - slice < .004f);
                yield return null;
            }
            plan.Grid = job.Result;
            plan.InputGridCount = job.InputCount;
            plan.OutputGridCells = job.Width * job.Height;
            IndependentPreparationProgress = "核对战场边界与人物落点";
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
            Debug.Log("[IndependentEncounter] prepared Id=" + state.EncounterId + " chunks=" + plan.Chunks.Count +
                " inputs=" + plan.InputGridCount + " outputCells=" + plan.OutputGridCells +
                " backgroundRenderers=" + plan.Chunks.Count + " elapsedSeconds=" + (Time.realtimeSinceStartup - prepareStarted));
        }

        bool PlanIsCurrent(PreparedIndependentField plan, bool restore)
        {
            if (plan == null || !ReferenceEquals(plan.World, _bootstrap?.Session?.World) ||
                plan.SurfaceId != _surfaceId || !TryResolveSurface(out var source) ||
                !ReferenceEquals(source, plan.Source) ||
                plan.TopologyRevision != plan.World.OutdoorStatefulObjects.DestructibleTopologyRevision) return false;
            if (restore) return ReferenceEquals(plan.World.Strategic.CharacterEncounter, plan.State);
            if (plan.World.Strategic.IsWorldTickFrozen || plan.World.Strategic.CharacterEncounter != null) return false;
            foreach (var p in plan.State.Participants)
            {
                var id = new EntityId(p.CharacterId);
                if (!plan.World.Strategic.Squads.TryGetForCharacter(id, out var squad) || squad.SquadId != p.SquadId ||
                    squad.CommandRevision != plan.SquadCommands[p.SquadId] ||
                    squad.MemberCharacterIds.Count != plan.SquadCounts[p.SquadId] ||
                    !CharacterPersonalSpaceQuery.TryResolveContinuous(plan.World, id, plan.SurfaceId, out var point, out _) ||
                    Math.Abs(point.X - p.OriginX) > .0001f || Math.Abs(point.Y - p.OriginY) > .0001f) return false;
            }
            return true;
        }

        /// <summary>Stages source presentation under a private owner, then performs one bounded takeover.</summary>
        public IEnumerator CommitPreparedIndependentField(
            PreparedIndependentField plan, Action<Result> completed, bool domainAlreadyBound = false)
        {
            if (!PlanIsCurrent(plan, domainAlreadyBound))
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
                if (!PlanIsCurrent(plan, domainAlreadyBound))
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
                    _tileMap.DeferInstanceActivation = true;
                    BuildChunk(plan.Chunks[i]);
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
                finally { _tileMap.DeferInstanceActivation = false; }
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
            if (!PlanIsCurrent(plan, domainAlreadyBound))
            {
                CancelPreparedIndependentField();
                return Result.Failure(ErrorCode.InvalidOperation, "Prepared field expired before takeover.");
            }
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
                _navigationDestroyed = CaptureDestroyed(plan.World);
                _flagNavigationChunks = CollectFlagChunks();
                _observedDestructibleTopologyRevision = plan.TopologyRevision;
                _dynamicNavigationDirty = false;
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
                _tileMap.ActivateInstanceOwner(plan.State.EncounterId + ":");
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
                if (normalRemoved && !domainAlreadyBound) { IsActive = false; TryActivateAtCurrentWorldPosition(); }
                return Result.Failure(ErrorCode.InvalidOperation, "Independent field takeover failed: " + ex);
            }
        }

        public bool BeginIndependentFieldRestore(CharacterEncounterState state)
        {
            if (state == null || IsIndependentFieldPreparing || !string.IsNullOrEmpty(_independentFieldId)) return false;
            var coordinator = _bootstrap.GetComponent<HostCharacterEncounter>();
            return coordinator != null && coordinator.RestoreField(state);
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
            yield return null;
            var world = _bootstrap.Session.World;
            var destroyed = CaptureDestroyed(world);
            var changed = new HashSet<string>(destroyed, StringComparer.Ordinal);
            changed.SymmetricExceptWith(_navigationDestroyed);
            var affected = new HashSet<SurfaceChunkCoord>(_explicitNavigationChunks);
            _explicitNavigationChunks.Clear();
            if (TryResolveSurface(out var surface))
            {
                foreach (var p in surface.SitePlacements)
                {
                    if (p == null) continue;
                    var touches = false;
                    foreach (var id in changed)
                        if (id == p.StableId || id.StartsWith(p.StableId + ":", StringComparison.Ordinal))
                        { touches = true; break; }
                    if (!touches) continue;
                    foreach (var chunk in _loaded)
                        if (PlacementTouchesChunk(p, chunk)) affected.Add(chunk);
                }
            }
            foreach (var chunk in affected)
            {
                if (!ReferenceEquals(world, _bootstrap.Session.World) || state.EncounterId != _independentFieldId) yield break;
                if (!TryResolveSource(chunk, out var layout)) continue;
                _mapper.ChunkLocalToWorld(chunk, 0, 0, out var wx, out var wy);
                _mapper.WorldToPresentation(wx, wy, out var px, out var py);
                var source = MapLayoutWalkGridBuilder.Create(layout);
                var site = BuildSiteBlockerGrid(chunk, px, py, layout);
                var geography = BuildGeographyBlockerGrid(chunk, px, py, layout);
                for (var y = 0; y < source.Height; y++)
                for (var x = 0; x < source.Width; x++)
                {
                    var cx = px + (x + .5f) * source.CellSize;
                    var cy = py + (y + .5f) * source.CellSize;
                    _mapper.PresentationToWorld(cx, cy, out wx, out wy);
                    if (_compositeWalkGrid.TryWorldToCell(cx, cy, out var ox, out var oy))
                        _compositeWalkGrid.SetBlocked(ox, oy, !source.IsWalkable(x, y) ||
                            (site != null && !site.IsWalkable(x, y)) ||
                            (geography != null && !geography.IsWalkable(x, y)) || !state.Contains(wx, wy));
                }
                foreach (var pair in world.Strategic.FactionFlags.Flags)
                    if (pair.Value != null && pair.Value.SurfaceId == state.SourceSurfaceId)
                        HostFactionFlagQuery.ApplyWalkGridBlock(pair.Value, this, _compositeWalkGrid);
                NavigationGeneration++;
                _bootstrap.MoveController.SetWalkGrid(_compositeWalkGrid);
                yield return null;
            }
            if (ReferenceEquals(world, _bootstrap.Session.World))
            {
                _navigationDestroyed = destroyed;
                _observedDestructibleTopologyRevision = requestedTopologyRevision;
                _dynamicNavigationDirty = _explicitNavigationChunks.Count > 0 ||
                    requestedTopologyRevision != world.OutdoorStatefulObjects.DestructibleTopologyRevision;
            }
            Debug.Log("[IndependentEncounter] navigation patch Id=" + state.EncounterId + " chunks=" + affected.Count);
            _independentNavigationRefresh = null;
        }

        static HashSet<string> CaptureDestroyed(SimulationWorld world)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in world.OutdoorStatefulObjects.Destructibles)
                if (pair.Value.Destroyed || pair.Value.Hp <= 0) result.Add(pair.Key);
            return result;
        }

        HashSet<SurfaceChunkCoord> CollectFlagChunks()
        {
            var result = new HashSet<SurfaceChunkCoord>();
            foreach (var pair in _bootstrap.Session.World.Strategic.FactionFlags.Flags)
            {
                var flag = pair.Value;
                if (flag == null || flag.SurfaceId != _surfaceId ||
                    !HostFactionFlagQuery.TryGetContinuousFootprint(flag, this, out var x0, out var x1, out var y0, out var y1)) continue;
                foreach (var coord in _loaded)
                {
                    _mapper.ChunkLocalToWorld(coord, 0, 0, out var wx, out var wy);
                    _mapper.WorldToPresentation(wx, wy, out var px, out var py);
                    if (px < x1 && px + _mapper.ChunkWidth * _mapper.PresentationUnitsPerWorldUnit > x0 &&
                        py < y1 && py + _mapper.ChunkHeight * _mapper.PresentationUnitsPerWorldUnit > y0) result.Add(coord);
                }
            }
            return result;
        }

        void MarkIndependentFlagNavigationDirty()
        {
            _explicitNavigationChunks.UnionWith(_flagNavigationChunks);
            _flagNavigationChunks = CollectFlagChunks();
            _explicitNavigationChunks.UnionWith(_flagNavigationChunks);
            _dynamicNavigationDirty = true;
        }

        void CancelIndependentNavigation()
        {
            if (_independentNavigationRefresh != null) StopCoroutine(_independentNavigationRefresh);
            _independentNavigationRefresh = null;
            _navigationDestroyed.Clear();
            _explicitNavigationChunks.Clear(); _flagNavigationChunks.Clear();
        }

        void OnDisable()
        {
            _bootstrap?.GetComponent<HostCharacterEncounter>()?.CancelPreparation();
            CancelIndependentNavigation();
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
