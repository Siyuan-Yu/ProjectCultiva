using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Data.Content;
using XianXia.Core.Exploration;
using XianXia.Core.Navigation;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Surface;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    public sealed partial class ContinuousOutdoorSurfaceRuntime
    {
        string _independentFieldId = string.Empty;
        string _stagingIndependentFieldId = string.Empty;
        float _nextIndependentViewHealthCheck;
        string _lastIndependentViewFailure = string.Empty;
        bool _independentViewsHealthy = true;
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
                " fieldCells=" + (state.Width / surface.CellSize) + "x" + (state.Height / surface.CellSize) +
                " frozenWorldRect=" + state.CenterX + "," + state.CenterY + ";" + state.Width + "x" + state.Height +
                " approxChunks=" + (state.Width / surface.ChunkWidth) + "x" + (state.Height / surface.ChunkHeight) +
                " sourceWorldBounds=" + minWX + "," + minWY + ".." + maxWX + "," + maxWY +
                " cellSizeWorld=" + surface.CellSize + " presentationUnitsPerWorldUnit=" + _mapper.PresentationUnitsPerWorldUnit);
            for (var i = 0; i < surface.Chunks.Count; i++)
            {
                var chunk = surface.Chunks[i];
                if (!EncounterTouchesChunk(state, chunk.Coord)) continue;
                if (!ContinuousOutdoorStartupPlanner.TryValidateChunkData(
                        _bootstrap.Session.Registry, surface, chunk.Coord, out var failure))
                {
                    completed?.Invoke(Result.Failure(ErrorCode.ContentLoadFailed, failure), null);
                    yield break;
                }
                _mapper.ChunkLocalToWorld(chunk.Coord, 0f, 0f, out var wx, out var wy);
                _mapper.WorldToPresentation(wx, wy, out var px, out var py);
                var width = Mathf.RoundToInt(_mapper.ChunkWidth / _mapper.CellSize);
                var height = Mathf.RoundToInt(_mapper.ChunkHeight / _mapper.CellSize);
                var cell = _mapper.CellSize * _mapper.PresentationUnitsPerWorldUnit;
                inputs.Add(new WalkGridComposer.Input(new WalkGrid(px, py, cell, width, height), 0f, 0f));
                var blockers = BuildSiteBlockerGrid(chunk.Coord, px, py, cell, width, height);
                if (blockers != null) inputs.Add(new WalkGridComposer.Input(blockers, 0f, 0f));
                var geography = BuildGeographyBlockerGrid(chunk.Coord, px, py, cell, width, height);
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
            var placementOrder = new List<EncounterCharacter>(state.Participants);
            placementOrder.Sort((a, b) => a.CharacterId.CompareTo(b.CharacterId));
            var occupied = new HashSet<long>();
            var resolvedTactical = new Dictionary<ulong, WorldVec2>();
            foreach (var p in placementOrder)
            {
                var id = new EntityId(p.CharacterId);
                var currentView = default(WorldVec2);
                var hasCurrentView = state.Phase == CharacterEncounterPhase.Preparing &&
                                     TryGetCurrentEncounterViewWorld(world, id, out currentView);
                var hasPreferred = hasCurrentView || state.Phase != CharacterEncounterPhase.Preparing;
                var preferred = hasCurrentView
                    ? currentView : new WorldVec2(p.TacticalX, p.TacticalY);
                var preferredSource = hasCurrentView ? "CurrentMaterializedView" : "SavedTactical";
                var origin = new WorldVec2(p.OriginX, p.OriginY);
                if (!EncounterInitialTacticalPlacementResolver.TryResolve(
                        plan.Grid, origin, hasPreferred, preferred, preferredSource,
                        WorldToPreparedGrid, PreparedGridToWorld, point => state.Contains(point.X, point.Y),
                        occupied, 8, out var tactical, out _, out _, out _, out var reason))
                {
                    var reference = hasPreferred ? preferred : origin;
                    var projected = WorldToPreparedGrid(reference);
                    plan.Grid.TryWorldToCell(projected.X, projected.Y, out var cellX, out var cellY);
                    completed?.Invoke(Result.Failure(ErrorCode.InvalidOperation,
                        "Initial encounter tactical position is not walkable: CharacterId=" + p.CharacterId +
                        " SquadId=" + p.SquadId +
                        " SourceSpatialOwnerKind=" + p.SourceSpatialOwnerKind +
                        " SourceSquadId=" + p.SourceSquadId +
                        " Origin=" + origin +
                        " Reference=" + reference +
                        " CurrentView=" + (hasCurrentView ? currentView.ToString() : "none") +
                        " SurfaceId=" + state.SourceSurfaceId +
                        " PreparedCell=(" + cellX + "," + cellY + ")" +
                        " Reason=" + reason), null);
                    yield break;
                }
                resolvedTactical[p.CharacterId] = tactical;
            }
            foreach (var p in state.Participants)
                if (resolvedTactical.TryGetValue(p.CharacterId, out var tactical))
                { p.TacticalX = tactical.X; p.TacticalY = tactical.Y; }
            completed?.Invoke(Result.Success(), plan);
            Debug.Log("[IndependentEncounter] prepared Id=" + state.EncounterId + " chunks=" + plan.Chunks.Count +
                " inputs=" + plan.InputGridCount + " outputCells=" + plan.OutputGridCells +
                " backgroundRenderers=" + plan.Chunks.Count + " elapsedSeconds=" + (Time.realtimeSinceStartup - prepareStarted));
        }

        bool TryGetCurrentEncounterViewWorld(SimulationWorld world, EntityId id, out WorldVec2 point)
        {
            point = default;
            if (world == null || id.IsNone || !ReferenceEquals(world, _navigationStateWorld) ||
                !world.Entities.TryGet(id, out _) ||
                !world.ContinuousOutdoorMaterialization.IsMaterialized(id) ||
                _bootstrap?.ViewSpawner?.Registry == null ||
                !_bootstrap.ViewSpawner.Registry.TryGet(id, out var view) || view == null) return false;
            _mapper.PresentationToWorld(view.transform.position.x, view.transform.position.y,
                out var wx, out var wy);
            point = new WorldVec2(wx, wy);
            return true;
        }

        WorldVec2 WorldToPreparedGrid(WorldVec2 point)
        {
            _mapper.WorldToPresentation(point.X, point.Y, out var x, out var y);
            return new WorldVec2(x, y);
        }

        WorldVec2 PreparedGridToWorld(WorldVec2 point)
        {
            _mapper.PresentationToWorld(point.X, point.Y, out var x, out var y);
            return new WorldVec2(x, y);
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
                    !CharacterEncounterSpatialAuthorityResolver.TryResolveEncounterWorldPosition(
                        plan.World, id, plan.SurfaceId, out var point, out var owner,
                        out var armyId, out _) ||
                    owner != p.SourceSpatialOwnerKind ||
                    !string.Equals(owner == EncounterSpatialOwnerKind.Squad ? armyId : string.Empty,
                        p.SourceSquadId, StringComparison.Ordinal) ||
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
                plan.World.ContinuousOutdoorMaterialization.BindIndependentEncounter(
                    plan.State.EncounterId, plan.State.SourceSurfaceId);
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
                AlignIndependentEncounterViews(plan.State);
                var ready = ValidateIndependentEncounterViews(plan.State, out var displaySummary);
                Debug.Log(displaySummary, this);
                if (ready.IsFailure)
                    throw new InvalidOperationException(ready.Error.ToString());
                _independentViewsHealthy = true;
                _nextIndependentViewHealthCheck = Time.unscaledTime + 1f;
                _lastIndependentViewFailure = string.Empty;
                var focus = _bootstrap.Session.PlayerParty.ActiveCharacterId;
                if (focus.IsNone || plan.State.Find(focus.Value) == null)
                    focus = plan.State.Participants.Count > 0
                        ? new EntityId(plan.State.Participants[0].CharacterId)
                        : EntityId.None;
                _bootstrap.GetComponent<HostPlayerPartyController>()?.SnapCameraToEntityOnce(focus);
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
                plan?.World?.ContinuousOutdoorMaterialization.ClearIndependentEncounter(
                    plan?.State?.EncounterId);
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

        void AlignIndependentEncounterViews(CharacterEncounterState state)
        {
            if (state == null) return;
            for (var i = 0; i < state.Participants.Count; i++)
            {
                var participant = state.Participants[i];
                var id = new EntityId(participant.CharacterId);
                if (!_bootstrap.ViewSpawner.Registry.TryGet(id, out var view) || view == null)
                    continue;
                _mapper.WorldToPresentation(participant.TacticalX, participant.TacticalY,
                    out var x, out var y);
                view.transform.position = HostPresentationSpace.FromPresentation(x, y);
            }
        }

        /// <summary>Commits one legal presentation movement into the current encounter's
        /// tactical position authority. OriginX/Y and ordinary-world travel remain untouched.</summary>
        public bool TryCaptureIndependentParticipantPosition(
            EntityId id,
            Vector3 presentationPosition)
        {
            var world = _bootstrap?.Session?.World;
            var state = world?.Strategic?.CharacterEncounter;
            var binding = world?.ContinuousOutdoorMaterialization;
            if (!IsActive || world == null || !ReferenceEquals(world, _navigationStateWorld) ||
                state == null || binding == null || !binding.HasIndependentEncounterBinding ||
                !string.Equals(binding.IndependentEncounterId, state.EncounterId,
                    StringComparison.Ordinal) ||
                !string.Equals(binding.IndependentEncounterSurfaceId, state.SourceSurfaceId,
                    StringComparison.Ordinal) ||
                state.EncounterId != _independentFieldId ||
                (state.Phase != CharacterEncounterPhase.Active &&
                 state.Phase != CharacterEncounterPhase.ReadyToEnd) ||
                _mapper == null || _compositeWalkGrid == null)
                return false;
            var participant = state.Find(id.Value);
            if (participant == null ||
                !_compositeWalkGrid.TryWorldToCell(
                    presentationPosition.x, presentationPosition.y, out var cellX, out var cellY) ||
                !_compositeWalkGrid.IsWalkable(cellX, cellY))
                return false;
            _mapper.PresentationToWorld(
                presentationPosition.x, presentationPosition.y, out var worldX, out var worldY);
            if (!state.Contains(worldX, worldY))
                return false;
            participant.TacticalX = worldX;
            participant.TacticalY = worldY;
            if (world.Entities.TryGet(id, out var entity) && entity != null &&
                entity.TryGet<EntityLocationComponent>(out var location) && location != null)
                location.SetPresentationOverride(presentationPosition.x, presentationPosition.y);
            if (world.WorldPresence.TryGet(id, out var presence) && presence != null &&
                presence.Mode == XianXia.Core.World.PartyWorldPresenceMode.InEncounter &&
                string.Equals(presence.PersonalSurfaceId, state.SourceSurfaceId,
                    StringComparison.Ordinal))
            {
                presence.WorldPosX = worldX;
                presence.WorldPosY = worldY;
                presence.HasContinuousWorldPosition = true;
            }
            return true;
        }

        Result ValidateIndependentEncounterViews(
            CharacterEncounterState state,
            out string summary)
        {
            var world = _bootstrap?.Session?.World;
            var historical = state?.Participants?.Count ?? 0;
            var expected = 0;
            var materialized = 0;
            var validViews = 0;
            var failures = new System.Text.StringBuilder();
            if (world == null || state == null ||
                !ReferenceEquals(world.Strategic.CharacterEncounter, state))
            {
                summary = "[IndependentEncounterDisplay] World/state binding missing.";
                return Result.Failure(ErrorCode.InvalidOperation,
                    "Independent encounter display world binding is invalid.");
            }

            for (var i = 0; i < state.Participants.Count; i++)
            {
                var participant = state.Participants[i];
                var id = new EntityId(participant.CharacterId);
                var hasEntity = world.Entities.TryGet(id, out var entity) && entity != null;
                if (hasEntity && XianXia.Core.Combat.CombatLifeStateService.ShouldHideFromSpawn(entity))
                    continue;
                expected++;
                var isMaterialized = world.ContinuousOutdoorMaterialization.IsMaterialized(id);
                if (isMaterialized) materialized++;
                var visible = LocalMapVisibility.EvaluateIndependentEncounterVisibility(
                    world, id, out var visibilityReason);
                var hasLocation = hasEntity &&
                    entity.TryGet<EntityLocationComponent>(out var location) && location != null &&
                    location.HasPresentationOverride;
                var hasView = _bootstrap.ViewSpawner.Registry.TryGet(id, out var view) && view != null;
                var hasSquad = world.Strategic.Squads.TryGetForCharacter(id, out var squad) &&
                               squad != null && squad.SquadId == participant.SquadId;
                _mapper.WorldToPresentation(participant.TacticalX, participant.TacticalY,
                    out var expectedX, out var expectedY);
                var aligned = hasView &&
                    Mathf.Abs(view.transform.position.x - expectedX) <= .02f &&
                    Mathf.Abs(view.transform.position.y - expectedY) <= .02f;
                var renderers = hasView ? view.GetComponentsInChildren<SpriteRenderer>(true) : null;
                var bodyVisible = false;
                if (renderers != null)
                    for (var r = 0; r < renderers.Length; r++)
                        if (renderers[r] != null && renderers[r].enabled &&
                            renderers[r].sprite != null && renderers[r].gameObject.activeInHierarchy)
                        { bodyVisible = true; break; }
                var valid = hasEntity && hasSquad && isMaterialized && visible && hasLocation && hasView &&
                            view.IsBoundTo(world, id) && view.gameObject.activeInHierarchy &&
                            bodyVisible && aligned;
                if (valid) { validViews++; continue; }
                if (failures.Length > 0) failures.Append(" | ");
                world.WorldPresence.TryGet(id, out var presence);
                failures.Append("EntityId=").Append(id.Value)
                    .Append(" Name=").Append(hasEntity ? entity.DisplayName : "<missing>")
                    .Append(" Life=").Append(hasEntity
                        ? XianXia.Core.Combat.CombatLifeStateService.FormatLifeStateWithCountdown(world, entity)
                        : "Missing")
                    .Append(" Presence=").Append(presence?.Mode.ToString() ?? "None")
                    .Append(" Participant=").Append(state.Find(id.Value) != null)
                    .Append(" Squad=").Append(hasSquad)
                    .Append(" Visibility=").Append(visibilityReason)
                    .Append(" Override=").Append(hasLocation)
                    .Append(" View=").Append(hasView)
                    .Append(" Bound=").Append(hasView && view.IsBoundTo(world, id))
                    .Append(" Active=").Append(hasView && view.gameObject.activeInHierarchy)
                    .Append(" Renderer=").Append(bodyVisible)
                    .Append(" ViewPosition=").Append(hasView
                        ? "(" + view.transform.position.x.ToString("0.###") + "," +
                          view.transform.position.y.ToString("0.###") + ")"
                        : "None")
                    .Append(" ExpectedPosition=(").Append(expectedX.ToString("0.###"))
                    .Append(",").Append(expectedY.ToString("0.###")).Append(")")
                    .Append(" Aligned=").Append(aligned);
                if (hasView && !bodyVisible)
                {
                    var diagnosticRenderer = renderers != null && renderers.Length > 0
                        ? renderers[0]
                        : null;
                    var camera = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();
                    failures.Append(" RendererEnabled=").Append(
                            diagnosticRenderer != null && diagnosticRenderer.enabled)
                        .Append(" Sprite=").Append(
                            diagnosticRenderer != null && diagnosticRenderer.sprite != null)
                        .Append(" SortingLayer=").Append(
                            diagnosticRenderer != null ? diagnosticRenderer.sortingLayerName : "None")
                        .Append(" SortingOrder=").Append(
                            diagnosticRenderer != null ? diagnosticRenderer.sortingOrder : 0)
                        .Append(" Camera=").Append(camera != null ? camera.name : "None")
                        .Append(" CameraCulling=").Append(camera != null &&
                            (camera.cullingMask & (1 << view.gameObject.layer)) != 0);
                    if (camera != null)
                    {
                        var viewport = camera.WorldToViewportPoint(view.transform.position);
                        failures.Append(" Viewport=(").Append(viewport.x.ToString("0.###"))
                            .Append(",").Append(viewport.y.ToString("0.###"))
                            .Append(",").Append(viewport.z.ToString("0.###")).Append(")");
                    }
                }
            }

            summary = "[IndependentEncounterDisplay] EncounterId=" + state.EncounterId +
                      " World=" + System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(world) +
                      " Surface=" + state.SourceSurfaceId +
                      " Binding=" + world.ContinuousOutdoorMaterialization.IndependentEncounterId +
                      " Phase=" + state.Phase +
                      " Historical=" + historical +
                      " Expected=" + expected +
                      " Materialized=" + materialized +
                      " ValidViews=" + validViews +
                      (failures.Length > 0 ? " Failures=" + failures : string.Empty);
            return validViews == expected
                ? Result.Success()
                : Result.Failure(ErrorCode.InvalidOperation,
                    "Independent encounter views are not ready.", failures.ToString());
        }

        /// <summary>Low-frequency guard for an Active field. A missing/stale view is repaired
        /// through the same reconcile path; tactical simulation pauses for that frame if the
        /// authoritative participant set still cannot be presented.</summary>
        public bool MaintainIndependentEncounterViews()
        {
            var state = _bootstrap?.Session?.World?.Strategic?.CharacterEncounter;
            if (state == null || state.EncounterId != _independentFieldId)
                return false;
            if (Time.unscaledTime < _nextIndependentViewHealthCheck)
                return _independentViewsHealthy;
            _nextIndependentViewHealthCheck = Time.unscaledTime + 1f;
            var repairedPresence = CharacterEncounterService.ReconcileRuntimePresence(
                _bootstrap.Session.World);
            var ready = ValidateIndependentEncounterViews(state, out var initialSummary);
            if (ready.IsSuccess)
            {
                if (repairedPresence > 0)
                    Debug.Log("[IndependentEncounterDisplay] RepairedPresence EncounterId=" +
                              state.EncounterId + " Count=" + repairedPresence, this);
                _lastIndependentViewFailure = string.Empty;
                _independentViewsHealthy = true;
                return true;
            }

            ReconcileOutdoorEntityMaterialization();
            AlignIndependentEncounterViews(state);
            ready = ValidateIndependentEncounterViews(state, out var summary);
            if (ready.IsSuccess)
            {
                Debug.Log("[IndependentEncounterDisplay] Repaired EncounterId=" + state.EncounterId +
                          " Cause=" + initialSummary, this);
                _lastIndependentViewFailure = string.Empty;
                _independentViewsHealthy = true;
                return true;
            }
            _independentViewsHealthy = false;
            if (!string.Equals(_lastIndependentViewFailure, summary, StringComparison.Ordinal))
            {
                _lastIndependentViewFailure = summary;
                Debug.LogError(summary, this);
            }
            return false;
        }

        public string DescribeReadyToEndDiagnostics(CharacterEncounterState state)
        {
            var world = _bootstrap?.Session?.World;
            if (world == null || state == null)
                return "[IndependentEncounterReadyToEnd] unavailable";
            var controlledSquad = _bootstrap.Session.PlayerParty.ControlledSquadId;
            var rows = new System.Text.StringBuilder();
            for (var i = 0; i < state.Participants.Count; i++)
            {
                var participant = state.Participants[i];
                if (rows.Length > 0) rows.Append(" | ");
                var id = new EntityId(participant.CharacterId);
                var hasView = _bootstrap.ViewSpawner.Registry.TryGet(id, out var view) && view != null;
                rows.Append("Id=").Append(id.Value)
                    .Append(" Origin=(").Append(participant.OriginX.ToString("0.###"))
                    .Append(",").Append(participant.OriginY.ToString("0.###"))
                    .Append(") Tactical=(").Append(participant.TacticalX.ToString("0.###"))
                    .Append(",").Append(participant.TacticalY.ToString("0.###"))
                    .Append(") View=").Append(hasView
                        ? "(" + view.transform.position.x.ToString("0.###") + "," +
                          view.transform.position.y.ToString("0.###") + ")"
                        : "None")
                    .Append(" ControlledParticipant=").Append(
                        string.Equals(participant.SquadId, controlledSquad, StringComparison.Ordinal));
            }
            return "[IndependentEncounterReadyToEnd] EncounterId=" + state.EncounterId +
                   " ClockFreeze=" + world.Strategic.ClockFreeze.Reason +
                   " ManualPaused=" + _bootstrap.Session.ManualPaused +
                   " ControlledSquad=" + controlledSquad +
                   " Participants=" + rows;
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
                _mapper.ChunkLocalToWorld(chunk, 0, 0, out var wx, out var wy);
                _mapper.WorldToPresentation(wx, wy, out var px, out var py);
                var width = Mathf.RoundToInt(_mapper.ChunkWidth / _mapper.CellSize);
                var height = Mathf.RoundToInt(_mapper.ChunkHeight / _mapper.CellSize);
                var cell = _mapper.CellSize * _mapper.PresentationUnitsPerWorldUnit;
                var source = new WalkGrid(px, py, cell, width, height);
                var site = BuildSiteBlockerGrid(chunk, px, py, cell, width, height);
                var geography = BuildGeographyBlockerGrid(chunk, px, py, cell, width, height);
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
            AlignIndependentEncounterViews(state);
            var ready = ValidateIndependentEncounterViews(state, out var summary);
            Debug.Log(summary, this);
            if (ready.IsFailure)
                Debug.LogError(ready.Error, this);
        }

        public void CaptureIndependentField()
        {
            var state = _bootstrap?.Session?.World?.Strategic?.CharacterEncounter;
            if (state == null || state.EncounterId != _independentFieldId || state.Phase == CharacterEncounterPhase.Committed ||
                !ReferenceEquals(_navigationStateWorld, _bootstrap.Session.World)) return;
            foreach (var p in state.Participants)
                if (_bootstrap.ViewSpawner.Registry.TryGet(new EntityId(p.CharacterId), out var view) && view != null)
                    TryCaptureIndependentParticipantPosition(
                        new EntityId(p.CharacterId), view.transform.position);
        }

        public void LeaveIndependentField(CharacterEncounterState completed)
        {
            if (completed == null || completed.EncounterId != _independentFieldId) return;
            var world = _bootstrap.Session.World;
            world.ContinuousOutdoorMaterialization.ClearIndependentEncounter(completed.EncounterId);
            _independentViewsHealthy = true;
            _nextIndependentViewHealthCheck = 0f;
            _lastIndependentViewFailure = string.Empty;
            foreach (var p in completed.Participants)
            {
                var id = new EntityId(p.CharacterId);
                _bootstrap.MoveController.CancelPresentationMovementPublic(id);
                if (world.Entities.TryGet(id, out var entity) &&
                    !CombatLifeStateService.ShouldHideFromSpawn(entity) &&
                    entity.TryGet<EntityLocationComponent>(out var location))
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
            LogReturnedSiteResidualDiagnostics(completed);
        }

        /// <summary>One-shot producer diagnostic after Encounter return and ordinary population reconcile.</summary>
        void LogReturnedSiteResidualDiagnostics(CharacterEncounterState completed)
        {
            var world = _bootstrap?.Session?.World;
            if (world == null || completed == null)
                return;

            foreach (var participant in completed.Participants)
            {
                var id = new EntityId(participant.CharacterId);
                if (!ResidualCharacterPresenceService.IsResidualLifeCandidate(world, id))
                    continue;

                world.Entities.TryGet(id, out var entity);
                world.WorldPresence.TryGet(id, out var presence);
                var returnedMode = presence != null ? presence.Mode.ToString() : "Missing";
                var returnedSiteId = presence?.SiteId ?? string.Empty;
                var hasPosition = presence != null && presence.HasContinuousWorldPosition;
                var worldPosition = hasPosition
                    ? "(" + presence.WorldPosX.ToString("0.###") + "," + presence.WorldPosY.ToString("0.###") + ")"
                    : "None";
                var squadId = world.Strategic.Squads.TryGetForCharacter(id, out var squad) && squad != null
                    ? squad.SquadId
                    : participant.SquadId;
                WorldSite returnedSite = null;
                if (!string.IsNullOrEmpty(returnedSiteId))
                    world.Strategic.Sites.TryGet(returnedSiteId, out returnedSite);
                var rejectionReason = string.Empty;
                var personalResidualAtSite = returnedSite != null &&
                    StrategicWorldSitePopulationService.TryResolvePersonalResidualAtSite(
                        world, id, returnedSite, out rejectionReason);
                if (returnedSite == null)
                    rejectionReason = string.IsNullOrEmpty(returnedSiteId) ? "ReturnedSiteMissing" : "SiteDefinitionMissing";
                var included = returnedSite != null &&
                               StrategicWorldSitePopulationService.IsCharacterPresentAtWorldSite(world, id, returnedSite);
                var materialized = world.ContinuousOutdoorMaterialization.IsMaterialized(id);
                var hasView = _bootstrap.ViewSpawner.Registry.TryGet(id, out var view) && view != null;
                var lifeState = entity != null
                    ? (XianXia.Core.Combat.CombatLifeStateService.ResolveLifeStateLabel(entity) ?? "Living")
                    : "EntityMissing";
                var message = "[SiteResidualReturn] EntityId=" + id.Value +
                              " Name=" + (entity?.DisplayName ?? string.Empty) +
                              " LifeState=" + lifeState +
                              " SquadId=" + (squadId ?? string.Empty) +
                              " SourceMode=" + (XianXia.Core.World.PartyWorldPresenceMode)participant.SourceMode +
                              " ReturnedMode=" + returnedMode +
                              " SiteId=" + returnedSiteId +
                              " HasContinuousWorldPosition=" + hasPosition +
                              " WorldPosition=" + worldPosition +
                              " PersonalResidualAtSite=" + personalResidualAtSite +
                              " IncludedBySitePopulation=" + included +
                              " Materialized=" + materialized +
                              " View=" + hasView +
                              (included ? string.Empty : " Rejection=" + rejectionReason);
                if (included)
                    Debug.Log(message, this);
                else
                    Debug.LogWarning(message, this);
            }
        }
    }
}
