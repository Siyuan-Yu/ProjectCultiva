using System;
using System.Collections.Generic;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;
using XianXia.Core.Combat;
using XianXia.Core.Construction;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Navigation;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Surface;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;
using XianXia.Data.Bootstrap;
using XianXia.Data.Content;

namespace XianXia.Unity.Host
{
    /// <summary>W1C presentation owner for the acceptance surface. Chunk ownership is transient only.</summary>
    public sealed partial class ContinuousOutdoorSurfaceRuntime : MonoBehaviour
    {
        public sealed class ManualCombatPreparation
        {
            internal readonly Dictionary<EntityId, Vector3> Placements = new Dictionary<EntityId, Vector3>();
            internal readonly Dictionary<EntityId, SavedPlacement> Previous = new Dictionary<EntityId, SavedPlacement>();
            internal readonly List<ActualBattleParticipant> Participants = new List<ActualBattleParticipant>();
            internal SimulationWorld World;
            internal SurfaceChunkCoord AnchorChunk;
            public string OfferId { get; internal set; } = string.Empty;
            public string SurfaceId { get; internal set; } = string.Empty;
            public WorldVec2 BattleWorldAnchor { get; internal set; }
            public int ExpectedCount => Placements.Count;
        }

        internal readonly struct SavedPlacement
        {
            public SavedPlacement(bool hadLocation, bool hadOverride, float x, float y)
            { HadLocation = hadLocation; HadOverride = hadOverride; X = x; Y = y; }
            public bool HadLocation { get; }
            public bool HadOverride { get; }
            public float X { get; }
            public float Y { get; }
        }
        string _surfaceId = string.Empty;
        readonly HashSet<SurfaceChunkCoord> _loaded = new HashSet<SurfaceChunkCoord>();
        // Presentation may temporarily contain the old logical neighborhood plus staged incoming
        // chunks. Gameplay scope remains exactly _loaded (radius-1 / 3x3).
        readonly HashSet<SurfaceChunkCoord> _presentedChunks = new HashSet<SurfaceChunkCoord>();
        readonly HashSet<SurfaceChunkCoord> _desired = new HashSet<SurfaceChunkCoord>();
        readonly HashSet<SurfaceChunkCoord> _add = new HashSet<SurfaceChunkCoord>();
        readonly HashSet<SurfaceChunkCoord> _remove = new HashSet<SurfaceChunkCoord>();
        readonly List<SurfaceChunkCoord> _pendingAdds = new List<SurfaceChunkCoord>(3);
        readonly List<SurfaceChunkCoord> _pendingRemoves = new List<SurfaceChunkCoord>(3);
        readonly List<WalkGridComposer.Input> _grids = new List<WalkGridComposer.Input>(9);
        WalkGrid _compositeWalkGrid;
        SimulationWorld _navigationStateWorld;
        ulong _observedDestructibleTopologyRevision;
        bool _dynamicNavigationDirty;
        SimulationWorld _legacyOutdoorRestoreMigrationWorld;
        SimulationWorld _snapshotRestoredWorld;
        bool _isSnapshotPresentationRebuild;
        readonly HashSet<EntityId> _newlyMaterializedEntities = new HashSet<EntityId>();
        PlayableHostBootstrap _bootstrap;
        HostDemoTileMap _tileMap;
        OutdoorSurfaceCoordinateMapper _mapper;
        OutdoorSurfaceGeographyDefinition _geography;
        ulong _lastStrategicMaterializationTick = ulong.MaxValue;
        readonly Dictionary<EntityId, Vector3> _lastLegalMembers = new Dictionary<EntityId, Vector3>();
        readonly HashSet<EntityId> _continuousSitePopulation = new HashSet<EntityId>();
        // Field FormalArmy members share the Continuous materialization board, but their
        // canonical position remains FormalArmy.WorldMotion. Never capture them as AtSite.
        readonly HashSet<EntityId> _continuousFormalArmyPopulation = new HashSet<EntityId>();
        readonly HashSet<EntityId> _desiredMaterializedEntities = new HashSet<EntityId>();
        /// <summary>§16：起点不在 CompositeWalkGrid 的 NPC（不得启动日程寻路）。</summary>
        readonly HashSet<ulong> _invalidSpawnEntityIds = new HashSet<ulong>();
        /// <summary>§11：已报告过 anchor 被拒的实体（一次，不重复刷）。</summary>
        readonly HashSet<ulong> _rejectedAnchorReported = new HashSet<ulong>();
        /// <summary>同一次 materialize pass 内已占用的 presentation 点（防多人重合）。</summary>
        readonly Dictionary<long, ulong> _materializePointUses = new Dictionary<long, ulong>();
        readonly List<EntityId> _sitePopulationScratch = new List<EntityId>();
        readonly List<ulong> _formalArmyMemberScratch = new List<ulong>(16);
        readonly HashSet<string> _distantFormalArmyFormationReported =
            new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> _materializedSitePlacementOwners = new HashSet<string>(StringComparer.Ordinal);
        /// <summary>同一 materialize pass 内两人最小 presentation 间距（≈2.5 个精灵）。</summary>
        const float MaterializeSeparationPresentation = 2.6f;
        /// <summary>§16 重定位候选点的同心环步长（presentation 单位）。</summary>
        const float surfaceCellSpacingPresentation = 3f;
        /// <summary>§16：已落入 grid 但格子 blocked 时，优先吸附到附近可走格。</summary>
        const int MaterializeNearestWalkableRadiusCells = 8;
        enum StreamTransitionPhase
        {
            None,
            BuildIncoming,
            CommitLogicalNeighborhood,
            RefreshGameplayScope,
            RefreshOverlay,
            RetireTrailing
        }
        StreamTransitionPhase _streamTransitionPhase;
        SurfaceChunkCoord _pendingCenter;
        int _pendingAddIndex;
        int _pendingRemoveIndex;
        HexCoord _diagnosticDerived, _diagnosticCommitted;
        bool _autoTravelPathBlocked;
        HexCoord _blockedNextHex, _blockedDestination;
        WorldVec2 _blockedFrom, _blockedCandidate;
        public string LastMovementDiagnostic { get; private set; } = string.Empty;
        public string SurfaceEgressStatus { get; private set; } = "None";
        public bool IsActive { get; private set; }
        public string ActiveSurfaceId => IsActive ? _surfaceId : string.Empty;
        public SurfaceChunkCoord CurrentChunk { get; private set; }
        public int LoadedChunkCount => _loaded.Count;
        public int PlaceRefreshGeneration { get; private set; }
        public int EntityReconcileGeneration { get; private set; }
        public int NavigationGeneration { get; private set; }
        public OutdoorSurfaceGeographyDefinition ActiveGeography => IsActive ? _geography : null;

        /// <summary>Opening population census（startup invariant 一次计算，仅供诊断显示）。</summary>
        public int OpeningPopulationExpected { get; private set; }
        public int OpeningPopulationMaterialized { get; private set; }
        public int OpeningPopulationViews { get; private set; }
        public string OpeningPopulationMissing { get; private set; } = string.Empty;
        /// <summary>§3：opening population 中真正落在「已加载 chunk + CompositeWalkGrid + 本 Site」的数量。</summary>
        public int OpeningPopulationSpatialValid { get; private set; }
        public string OpeningPopulationSpatialInvalid { get; private set; } = string.Empty;
        /// <summary>最近一次 materialize pass 中，把「已存在的 view」重新对齐到权威落点的数量。
        /// 非 0 即说明存在「materialize 之前就建好的 view」被 legacy 回退位置滞留。</summary>
        public int RealignedViewCount { get; private set; }
        public string SnapshotSpatialSummary { get; private set; } = string.Empty;

        /// <summary>§5 bake validation：opening spawn 的 checked-in anchor 是否与 shared bake 一致。</summary>
        public bool OpeningAnchorBakeValid =>
            !string.Equals(OpeningAnchorBakeStatus, nameof(OpeningAnchorBakeValidationOutcome.Failed),
                StringComparison.Ordinal);
        public string OpeningAnchorBakeStatus { get; private set; } =
            nameof(OpeningAnchorBakeValidationOutcome.Validated);
        public string OpeningAnchorBakeFailure { get; private set; } = string.Empty;
        readonly List<OpeningPlacementPlanRow> _openingPlacementPlan = new List<OpeningPlacementPlanRow>(32);

        /// <summary>Producer 诊断用：Expected／Materialized／Views／Missing 一行。
        /// §13：这只是 census 的一半 —— spatial validity 见 <see cref="OpeningSpatialCensusSummary"/>。</summary>
        public string OpeningPopulationSummary =>
            "ExpectedOpeningPopulation=" + OpeningPopulationExpected +
            " Materialized=" + OpeningPopulationMaterialized +
            " Views=" + OpeningPopulationViews +
            " Missing=[" + OpeningPopulationMissing + "]";

        /// <summary>§18：opening population 的空间 census 摘要（SpatialInvalid 非空时才展开详情）。</summary>
        public string OpeningSpatialCensusSummary =>
            "OpeningPopulation: Expected=" + OpeningPopulationExpected +
            " Materialized=" + OpeningPopulationMaterialized +
            " Views=" + OpeningPopulationViews +
            " SpatialValid=" + OpeningPopulationSpatialValid +
            " SpatialInvalid=[" + OpeningPopulationSpatialInvalid + "]";
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
        public bool TryGetCompositeWalkGrid(out WalkGrid grid)
        {
            grid = null;
            grid = _compositeWalkGrid;
            return IsActive && grid != null;
        }

        public void RefreshCompositeWalkGrid()
        {
            if (!string.IsNullOrEmpty(_independentFieldId))
            {
                MarkIndependentFlagNavigationDirty();
                return;
            }
            if (IsActive)
                RecomposeWalkGrid();
        }

        public bool CommitContinuousNpcPosition(EntityId id, Vector3 presentationPosition)
        {
            var world = _bootstrap?.Session?.World;
            if (_isSnapshotPresentationRebuild || !IsActive || world == null || _mapper == null ||
                !ReferenceEquals(_navigationStateWorld, world) ||
                !world.WorldPresence.TryGet(id, out var presence) || presence == null ||
                (presence.Mode != PartyWorldPresenceMode.AtSite &&
                 presence.Mode != PartyWorldPresenceMode.AtWorldPosition) ||
                world.LocalMap.IsInInterior || !IsPersonalPositionCaptureOwner(world, id))
                return false;
            _mapper.PresentationToWorld(
                presentationPosition.x, presentationPosition.y, out var worldX, out var worldY);
            if (float.IsNaN(worldX) || float.IsInfinity(worldX) ||
                float.IsNaN(worldY) || float.IsInfinity(worldY) ||
                !TryResolveSurface(out var surface) ||
                !OutdoorSurfaceCoverageResolver.ContainsWorldPosition(surface, worldX, worldY))
                return false;
            // Update the existing personal coordinate without changing site/residual ownership,
            // pursuit or movement state. Presentation coordinates always pass through the mapper.
            presence.WorldPosX = worldX;
            presence.WorldPosY = worldY;
            presence.HasContinuousWorldPosition = true;
            presence.PersonalSurfaceId = _surfaceId;
            if (world.Entities.TryGet(id, out var entity) &&
                entity.TryGet<XianXia.Core.Exploration.EntityLocationComponent>(out var location))
                location.SetPresentationOverride(presentationPosition.x, presentationPosition.y);
            return true;
        }

        /// <summary>Explicit scope-change hook for domain arrivals/departures, death/removal and
        /// snapshot rehydrate. Schedule movement must not call this.</summary>
        public void ReconcileOutdoorEntityMaterializationForScopeChange()
        {
            if (IsActive)
                ReconcileOutdoorEntityMaterialization();
        }

        /// <summary>Compatibility entry for existing arrival callers.</summary>
        public bool TryCaptureAtSiteAnchor(EntityId id, Vector3 presentationPosition) =>
            CommitContinuousNpcPosition(id, presentationPosition);

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
                   "\nCurrentWorldSiteGateway=DisabledForOutdoorMigration" +
                   " WorldSiteIngressStatus=" + LastMovementDiagnostic +
                   " LocalPlacesContext=" + (string.IsNullOrEmpty(_bootstrap?.Session?.World?.LocalPlaces?.ActiveMapLayoutId) ? "ContinuousEmpty" : _bootstrap.Session.World.LocalPlaces.ActiveMapLayoutId) +
                   "\nFormalArmyNearField=" + DescribeFormalArmyNearField(definition) +
                   "\nLastMovementDiagnostic=" + LastMovementDiagnostic;
        }

        public bool IsWorldPositionLoaded(string surfaceId, float worldX, float worldY)
        {
            if (!IsActive || _mapper == null ||
                !string.Equals(_surfaceId, surfaceId ?? string.Empty, StringComparison.Ordinal) ||
                !TryResolveSurface(out var surface) ||
                !OutdoorSurfaceCoverageResolver.ContainsWorldPosition(surface, worldX, worldY))
                return false;
            return _loaded.Contains(_mapper.WorldToChunk(worldX, worldY));
        }

        public bool IsBoundContinuousManualCombat(SimulationWorld world)
        {
            var state = world?.Strategic?.ContinuousManualCombat;
            return IsActive && state != null && state.IsActive &&
                   string.Equals(state.SurfaceId, _surfaceId, StringComparison.Ordinal);
        }

        /// <summary>Read-only preparation performed before declaration or world-position commit.</summary>
        public Result TryPrepareManualCombatEntry(
            SimulationWorld world,
            BattleOfferPending offer,
            out ManualCombatPreparation preparation)
        {
            preparation = null;
            if (world == null || offer == null || !IsActive ||
                !ReferenceEquals(world, _bootstrap?.Session?.World))
                return Result.Failure(ErrorCode.InvalidOperation, "Continuous Outdoor runtime 未绑定当前 World。");
            if (world.LocalMap == null || world.LocalMap.IsInInterior ||
                string.IsNullOrEmpty(_surfaceId) || _mapper == null || _compositeWalkGrid == null ||
                !TryResolveSurface(out var surface))
                return Result.Failure(ErrorCode.InvalidOperation, "Continuous Outdoor 战斗物理空间未就绪。");
            if (string.IsNullOrEmpty(offer.OfferId) || offer.Origin != BattleOfferOrigin.LocalMapHostileAction)
                return Result.Failure(ErrorCode.InvalidArgument, "该 BattleOffer 不是当前地面攻击入口。");
            if (!ArmyHexBattleAnchorService.TryGetBattleAnchorHex(world.Strategic.Participants, out var battleHex) ||
                world.HexWorld == null || !world.HexWorld.Contains(battleHex))
                return Result.Failure(ErrorCode.InvalidOperation, "Continuous 战斗缺少有效冻结锚点。");

            var plan = new ManualCombatPreparation
            {
                World = world,
                OfferId = offer.OfferId,
                SurfaceId = _surfaceId
            };
            var used = new HashSet<long>();
            var enemyCount = 0;
            var anchorResolved = false;
            var actualParticipants = ActualBattleParticipantQuery.Collect(world.Strategic.Participants);
            for (var i = 0; i < actualParticipants.Count; i++)
            {
                var actualParticipant = actualParticipants[i];
                var rec = actualParticipant.Record;
                if (!world.Entities.TryGet(rec.EntityId, out var entity) || entity == null ||
                    XianXia.Core.Combat.CombatLifeStateService.ShouldHideFromSpawn(entity))
                    return Result.Failure(ErrorCode.NotFound,
                        "Continuous 参战实体不存在或已移除：" + rec.EntityId.Value);

                var isEnemy = actualParticipant.IsEnemy;
                if (isEnemy) enemyCount++;
                if (!TryResolvePreparedParticipantPlacement(
                        world, rec.EntityId, rec.FormalArmyId, used,
                        out var placement, out var placementWorld, out var placementFailure))
                    return Result.Failure(ErrorCode.InvalidOperation,
                        "参战准备失败：" + entity.DisplayName + "（" + rec.EntityId.Value + "） " +
                        placementFailure + " OfferId=" + offer.OfferId + " Army=" + rec.FormalArmyId +
                        " PlayerPartyMember=" + (_bootstrap.Session.PlayerParty != null &&
                            _bootstrap.Session.PlayerParty.IsMember(rec.EntityId)));
                plan.Placements.Add(rec.EntityId, placement);
                plan.Participants.Add(actualParticipant);
                if (isEnemy && !anchorResolved)
                {
                    var authoritativeAnchor = placementWorld;
                    plan.BattleWorldAnchor = authoritativeAnchor;
                    plan.AnchorChunk = _mapper.WorldToChunk(authoritativeAnchor.X, authoritativeAnchor.Y);
                    anchorResolved = true;
                }
            }

            if (plan.Placements.Count == 0 || enemyCount == 0 || !anchorResolved)
                return Result.Failure(ErrorCode.InvalidOperation, "Continuous 战斗没有完整的冻结参战者。");
            if (!OutdoorSurfaceCoverageResolver.ContainsWorldPosition(
                    surface, plan.BattleWorldAnchor.X, plan.BattleWorldAnchor.Y) ||
                !_loaded.Contains(plan.AnchorChunk))
                return Result.Failure(ErrorCode.InvalidOperation, "地面目标不在当前已加载 Continuous neighborhood。");
            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            var targetHex = HexMath.WorldToHex(
                plan.BattleWorldAnchor.X, plan.BattleWorldAnchor.Y, hexSize);
            if (!targetHex.Equals(battleHex))
                return Result.Failure(
                    ErrorCode.InvalidOperation,
                    "地面目标与冻结 BattleAnchorHex 不一致：target=" + targetHex +
                    ", frozen=" + battleHex);

            preparation = plan;
            return Result.Success();
        }

        bool TryResolvePreparedParticipantPlacement(
            SimulationWorld world,
            EntityId id,
            string formalArmyId,
            HashSet<long> used,
            out Vector3 placement,
            out WorldVec2 placementWorld,
            out string failure)
        {
            placement = default;
            placementWorld = default;
            if (CharacterPersonalSpaceQuery.TryResolveContinuous(world, id, _surfaceId,
                    out var personalPosition, out _))
            {
                _mapper.WorldToPresentation(personalPosition.X, personalPosition.Y, out var personalX, out var personalY);
                return TryPreparePersonalPoint(HostPresentationSpace.FromPresentation(personalX, personalY),
                    "PersonalWorldPresence", used, out placement, out placementWorld, out failure);
            }
            if (world.WorldPresence.TryGet(id, out var explicitPersonal) &&
                !string.IsNullOrEmpty(explicitPersonal.PersonalSurfaceId))
            {
                failure = "Stage=PersonalSpace Source=PersonalWorldPresence Surface=" +
                    explicitPersonal.PersonalSurfaceId + " Reason=正式个人空间不匹配或损坏，禁止旧View回退";
                return false;
            }
            failure = "Stage=PositionSource Reason=位置缺失或未由当前World物化 Surface=" +
                _surfaceId + " World=<unknown> Loaded=false Raw=<unknown> Reference=<none>";
            FormalArmy participantArmy = null;
            var resolvedArmyId = formalArmyId ?? string.Empty;
            if (string.IsNullOrEmpty(resolvedArmyId) &&
                ArmyService.TryGetArmyForCharacter(world, id, out var boundArmy) && boundArmy != null)
            {
                participantArmy = boundArmy;
                resolvedArmyId = boundArmy.ArmyId;
            }
            if (participantArmy == null && !string.IsNullOrEmpty(resolvedArmyId))
            {
                world.Strategic.FormalArmies.TryGet(resolvedArmyId, out participantArmy);
            }
            // Only this live World/Surface's materialization can vouch for a personal presentation.
            // An old override alone is not a spatial identity, and the group anchor is not a veto.
            if (ReferenceEquals(_navigationStateWorld, world) &&
                world.ContinuousOutdoorMaterialization.IsMaterialized(id) &&
                (participantArmy == null || string.IsNullOrEmpty(participantArmy.WorldMotion.SurfaceId) ||
                 string.Equals(participantArmy.WorldMotion.SurfaceId, _surfaceId, StringComparison.Ordinal)) &&
                world.Entities.TryGet(id, out var entity) &&
                entity.TryGet<XianXia.Core.Exploration.EntityLocationComponent>(out var existing) &&
                existing != null && existing.HasPresentationOverride)
            {
                var current = HostPresentationSpace.FromPresentation(
                    existing.PresentationOverrideX, existing.PresentationOverrideZ);
                if (_bootstrap.ViewSpawner?.Registry != null &&
                    _bootstrap.ViewSpawner.Registry.TryGet(id, out var view) && view != null)
                    current = view.transform.position;
                return TryPreparePersonalPoint(current, "CurrentMaterializedCharacter", used,
                    out placement, out placementWorld, out failure);
            }

            // A precise personal presence takes priority over any legacy group position.
            if (world.WorldPresence.TryGet(id, out var personal) && personal != null &&
                personal.HasContinuousWorldPosition && participantArmy == null)
            {
                _mapper.WorldToPresentation(personal.WorldPosX, personal.WorldPosY, out var px, out var py);
                return TryPreparePersonalPoint(HostPresentationSpace.FromPresentation(px, py),
                    "PersonalWorldPresence", used, out placement, out placementWorld, out failure);
            }

            if (participantArmy != null)
            {
                failure = "Stage=LegacyArmyScope Source=ArmyWorldMotion Surface=" +
                    participantArmy.WorldMotion.SurfaceId + " World=" + participantArmy.WorldMotion.WorldPosition +
                    " HasPosition=" + participantArmy.WorldMotion.HasPosition + " Army=" + resolvedArmyId +
                    " Loaded=" + IsWorldPointInLoadedSurface(participantArmy.WorldMotion.WorldPosition) +
                    " Raw=<no-personal-point> Reference=<none>" +
                    " Reason=旧名单成员缺少可信个人近场位置或群体锚点范围外；旧支援资格待CW-U2B迁移";
                // Missing personal data must not turn a remote member into an anchor-spawned clone.
                return false;
            }
            return false;
        }

        bool IsWorldPointInLoadedSurface(WorldVec2 point) =>
            TryResolveSurface(out var surface) &&
            OutdoorSurfaceCoverageResolver.ContainsWorldPosition(surface, point.X, point.Y) &&
            _loaded.Contains(_mapper.WorldToChunk(point.X, point.Y));

        bool TryPreparePersonalPoint(Vector3 raw, string source, HashSet<long> used,
            out Vector3 placement, out WorldVec2 worldPoint, out string failure)
        {
            placement = default;
            _mapper.PresentationToWorld(raw.x, raw.y, out var wx, out var wy);
            worldPoint = new WorldVec2(wx, wy);
            var loaded = IsWorldPointInLoadedSurface(worldPoint);
            var prefix = "Stage=Placement Source=" + source + " Surface=" + _surfaceId +
                " World=" + worldPoint + " Loaded=" + loaded + " Raw=" + raw;
            if (!loaded)
            {
                failure = prefix + " Reference=<none> Reason=范围外（旧支援资格待CW-U2B迁移）";
                return false;
            }
            if (!TryResolvePreparedPointNear(raw, used, out placement, out var reference, out var reason))
            {
                failure = prefix + " Reference=" + reference + " Reason=" + reason;
                return false;
            }
            _mapper.PresentationToWorld(placement.x, placement.y, out wx, out wy);
            worldPoint = new WorldVec2(wx, wy);
            failure = string.Empty;
            return true;
        }

        public int CaptureCurrentPersonalPlacements()
        {
            var world = _bootstrap?.Session?.World;
            if (_isSnapshotPresentationRebuild || !IsActive || !ReferenceEquals(_navigationStateWorld, world) ||
                world == null || world.LocalMap.IsInInterior ||
                world.Strategic.ContinuousManualCombat.IsActive)
                return 0;
            var count = 0;
            foreach (var entity in world.Entities.All)
                if (world.ContinuousOutdoorMaterialization.IsMaterialized(entity.Id) &&
                    IsPersonalPositionCaptureOwner(world, entity.Id) &&
                    _bootstrap.ViewSpawner.Registry.TryGet(entity.Id, out var view) && view != null &&
                    CommitContinuousNpcPosition(entity.Id, view.transform.position))
                    count++;
            return count;
        }

        bool IsPersonalPositionCaptureOwner(SimulationWorld world, EntityId id) =>
            world != null && !id.IsNone &&
            (_bootstrap?.Session?.PlayerParty == null || !_bootstrap.Session.PlayerParty.IsMember(id)) &&
            !ArmyService.TryGetArmyForCharacter(world, id, out _) &&
            !world.BackgroundCharacterTravel.IsTraveling(id) &&
            (world.Strategic.ContinuousManualCombat == null ||
             !world.Strategic.ContinuousManualCombat.Contains(id)) &&
            !ActualBattleParticipantQuery.TryFind(world.Strategic.Participants, id, out _) &&
            world.Strategic.CharacterEncounter?.Find(id.Value) == null;

        bool TryAcceptPreparedPoint(Vector3 candidate, Vector3 reference,
            HashSet<long> used, out Vector3 accepted, out string reason)
        {
            accepted = default;
            reason = "无可走点";
            _mapper.PresentationToWorld(candidate.x, candidate.y, out var wx, out var wy);
            if (!IsWorldPointInLoadedSurface(new WorldVec2(wx, wy)) ||
                !_compositeWalkGrid.TryWorldToCell(candidate.x, candidate.y, out var x, out var y) ||
                !_compositeWalkGrid.IsWalkable(x, y)) return false;
            var key = QuantizeMaterializePoint(candidate.x, candidate.y);
            if (used.Contains(key)) { reason = "重复占用"; return false; }
            if (!GridPathfinder.IsWorldSegmentWalkable(
                    _compositeWalkGrid, reference.x, reference.y, candidate.x, candidate.y))
            { reason = "不连通"; return false; }
            // Occupancy is committed only after every check succeeds.
            used.Add(key);
            accepted = candidate;
            reason = string.Empty;
            return true;
        }

        bool TryResolvePreparedPointNear(Vector3 raw, HashSet<long> used,
            out Vector3 placement, out Vector3 reference, out string reason)
        {
            placement = default;
            reference = raw;
            reason = "无可走参考点";
            if (!_compositeWalkGrid.TryWorldToCell(raw.x, raw.y, out var cx, out var cy)) return false;
            if (!_compositeWalkGrid.IsWalkable(cx, cy))
            {
                // Only a one-cell correction with an unambiguous connected side is allowed.
                // Disconnected neighbours of a wall/river do not authorize choosing either bank.
                var neighbours = new List<Vector3>();
                for (var d = 0; d < 4; d++)
                {
                    var x = cx + (d == 0 ? 1 : d == 1 ? -1 : 0);
                    var y = cy + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (!_compositeWalkGrid.IsWalkable(x, y)) continue;
                    _compositeWalkGrid.CellToWorldCenter(x, y, out var px, out var py);
                    _mapper.PresentationToWorld(px, py, out var wx, out var wy);
                    if (IsWorldPointInLoadedSurface(new WorldVec2(wx, wy)))
                        neighbours.Add(HostPresentationSpace.FromPresentation(px, py));
                }
                if (neighbours.Count == 0) return false;
                reference = neighbours[0];
                for (var i = 1; i < neighbours.Count; i++)
                    if (!GridPathfinder.IsWorldSegmentWalkable(_compositeWalkGrid,
                            reference.x, reference.y, neighbours[i].x, neighbours[i].y))
                    { reason = "blocked原点两侧不连通，无法确定合法修正侧"; return false; }
            }
            if (TryAcceptPreparedPoint(reference, reference, used, out placement, out reason)) return true;
            var spacing = Mathf.Max(.05f, _compositeWalkGrid.CellSize * 1.5f);
            var failures = new HashSet<string>();
            failures.Add(reason);
            for (var ring = 1; ring <= 6; ring++)
            for (var direction = 0; direction < 8; direction++)
            {
                var angle = direction * Mathf.PI * .25f;
                var candidate = HostPresentationSpace.FromPresentation(
                    reference.x + Mathf.Cos(angle) * spacing * ring,
                    reference.y + Mathf.Sin(angle) * spacing * ring);
                if (TryAcceptPreparedPoint(candidate, reference, used, out placement, out reason)) return true;
                failures.Add(reason);
            }
            placement = default;
            reason = string.Join("/", failures);
            return false;
        }

        public Result CommitPreparedManualCombat(ManualCombatPreparation preparation)
        {
            var world = _bootstrap?.Session?.World;
            if (preparation == null || world == null || !ReferenceEquals(preparation.World, world) ||
                !IsActive || !string.Equals(preparation.SurfaceId, _surfaceId, StringComparison.Ordinal) ||
                !string.Equals(preparation.OfferId, world.Strategic.BattleOffer.OfferId, StringComparison.Ordinal) ||
                !_loaded.Contains(preparation.AnchorChunk))
                return Result.Failure(ErrorCode.InvalidOperation, "Continuous 战斗准备结果已失效。");

            var ids = new List<EntityId>(preparation.Participants.Count);
            for (var i = 0; i < preparation.Participants.Count; i++)
            {
                var id = preparation.Participants[i].EntityId;
                if (!preparation.Placements.TryGetValue(id, out var placement) ||
                    !world.Entities.TryGet(id, out var entity) || entity == null)
                    return RollbackPreparedManualCombat(preparation, "装配时参战实体已失效。");
                var hadLocation = entity.TryGet<XianXia.Core.Exploration.EntityLocationComponent>(out var loc) && loc != null;
                preparation.Previous[id] = new SavedPlacement(
                    hadLocation, hadLocation && loc.HasPresentationOverride,
                    hadLocation ? loc.PresentationOverrideX : 0f,
                    hadLocation ? loc.PresentationOverrideZ : 0f);
                if (!hadLocation)
                {
                    loc = new XianXia.Core.Exploration.EntityLocationComponent();
                    entity.AddComponent(loc);
                }
                loc.SetPresentationOverride(placement.x, placement.y);
                ids.Add(id);
            }

            world.Strategic.ContinuousManualCombat.Begin(
                preparation.OfferId, preparation.SurfaceId, preparation.BattleWorldAnchor,
                preparation.Participants);
            var applied = StrategicEncounterSpawner.ApplyPendingContinuousWorldCombat(
                world, world.Strategic.ContinuousManualCombat);
            if (applied.IsFailure)
                return RollbackPreparedManualCombat(preparation, applied.Error.Message);

            ReconcileOutdoorEntityMaterialization();
            var actual = 0;
            foreach (var id in ids)
                if (world.ContinuousOutdoorMaterialization.IsMaterialized(id) &&
                    _bootstrap.ViewSpawner.Registry.TryGet(id, out var view) && view != null)
                    actual++;
            if (actual != ids.Count)
                return RollbackPreparedManualCombat(
                    preparation, "Continuous 战斗 View 装配不完整：expected=" + ids.Count + ", actual=" + actual);
            return Result.Success();
        }

        Result RollbackPreparedManualCombat(ManualCombatPreparation preparation, string reason)
        {
            var world = _bootstrap?.Session?.World;
            if (world != null)
            {
                world.Strategic.ContinuousManualCombat.ClearOwned(preparation?.OfferId);
                if (preparation != null)
                    foreach (var pair in preparation.Previous)
                        if (world.Entities.TryGet(pair.Key, out var entity) &&
                            entity.TryGet<XianXia.Core.Exploration.EntityLocationComponent>(out var loc) && loc != null)
                        {
                            if (pair.Value.HadOverride) loc.SetPresentationOverride(pair.Value.X, pair.Value.Y);
                            else loc.ClearPresentationOverride();
                        }
                world.Strategic.Encounter?.ClearActiveEncounterSession();
                ReconcileOutdoorEntityMaterialization();
            }
            return Result.Failure(ErrorCode.InvalidOperation, reason ?? "Continuous 战斗装配失败。");
        }

        public void AbortPreparedManualCombat(ManualCombatPreparation preparation, string reason)
        {
            RollbackPreparedManualCombat(preparation, reason);
        }

        public void CompleteContinuousManualCombat(string offerId)
        {
            var world = _bootstrap?.Session?.World;
            if (world == null || !world.Strategic.ContinuousManualCombat.ClearOwned(offerId)) return;
            SyncPartyPresentation();
            ReconcileOutdoorEntityMaterialization();
        }

        string DescribeFormalArmyNearField(OutdoorWorldSurfaceDefinition surface)
        {
            var world = _bootstrap?.Session?.World;
            if (world?.Strategic?.FormalArmies == null || surface == null || _mapper == null)
                return "Unavailable";
            var rows = new List<string>();
            var geography = world.SurfaceGround.TryGet(_surfaceId, out var activeGeography)
                ? activeGeography
                : null;
            foreach (var pair in world.Strategic.FormalArmies.Armies)
            {
                var army = pair.Value;
                if (army == null || !army.WorldMotion.HasPosition)
                    continue;
                var position = army.WorldMotion.WorldPosition;
                var chunk = _mapper.WorldToChunk(position.X, position.Y);
                var living = 0;
                var materialized = 0;
                var views = 0;
                for (var i = 0; i < army.MemberCharacterIds.Count; i++)
                {
                    var id = new EntityId(army.MemberCharacterIds[i]);
                    if (!LingeringBattlefieldPartyService.IsLivingForMacroOrder(world, id))
                        continue;
                    living++;
                    if (world.ContinuousOutdoorMaterialization.IsMaterialized(id))
                        materialized++;
                    if (_bootstrap.ViewSpawner?.Registry != null &&
                        _bootstrap.ViewSpawner.Registry.TryGet(id, out var view) && view != null)
                        views++;
                }
                world.Strategic.Squads.TryGet(army.SquadId, out var squadCommand);
                var hasCommandTarget = SquadCommandService.TryResolveWorldTarget(world, squadCommand, out var commandTarget);
                rows.Add(
                    "ArmyId=" + army.ArmyId +
                    " SquadCommand=" + squadCommand?.CommandKind + " Revision=" + squadCommand?.CommandRevision +
                    " CommandTarget=" + (hasCommandTarget ? commandTarget.ToString() : "None") +
                    " WorldPosition=" + position +
                    " SurfaceCoverage=" + OutdoorSurfaceCoverageResolver.ContainsWorldPosition(
                        surface, position.X, position.Y) +
                    " Chunk=" + chunk +
                    " ChunkLoaded=" + _loaded.Contains(chunk) +
                    " GeographyCovered=" + (geography != null && geography.Contains(position.X, position.Y)) +
                    " LivingMembers=" + living +
                    " MaterializedMembers=" + materialized +
                    " Views=" + views);
            }
            return rows.Count > 0 ? string.Join("; ", rows) : "None";
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
            if (!string.IsNullOrEmpty(_independentFieldId))
            {
                // Independent fields still consume real destructible topology changes.  The
                // active field's clipped grid is rebuilt through the existing owner only.
                RefreshDynamicNavigationIfDirty();
                return;
            }
            if (!string.IsNullOrEmpty(_stagingIndependentFieldId)) return;
            RefreshDynamicNavigationIfDirty();
            var motion = _bootstrap?.Session?.World?.PlayerPartyTravel;
            // Restore compatibility is an activation boundary, never a per-frame repair that can
            // erase an already established travel plan.
            if (!IsActive &&
                (motion == null || !motion.IsMoving) &&
                !ReferenceEquals(_legacyOutdoorRestoreMigrationWorld, _bootstrap?.Session?.World))
            {
                TryMigrateLegacyOutdoorSiteRestore();
                motion = _bootstrap?.Session?.World?.PlayerPartyTravel;
            }
            var currentWorld = _bootstrap?.Session?.World;
            var continuousCombat = IsBoundContinuousManualCombat(currentWorld);
            if (motion == null || !motion.HasPosition || motion.LocationKind != PlayerPartyLocationKind.AtWorldPosition ||
                currentWorld.LocalMap.IsInInterior ||
                (BattleOfferService.HasActiveManualEncounter(currentWorld) && !continuousCombat))
            {
                if (IsActive) DeactivatePresentationOnly();
                return;
            }
            if (!OutdoorSurfaceCoverageResolver.TryResolveAtWorldPosition(_bootstrap.Session.Registry, motion.WorldPosition.X, motion.WorldPosition.Y, out var surface))
            { if (IsActive) DeactivatePresentationOnly(); return; }
            var mapper = new OutdoorSurfaceCoordinateMapper(surface.ChunkWidth, surface.ChunkHeight, surface.CellSize, presentationUnitsPerWorldUnit: 1f / surface.CellSize, originWorldX: surface.OriginWorldX, originWorldY: surface.OriginWorldY);
            var chunk = mapper.WorldToChunk(motion.WorldPosition.X, motion.WorldPosition.Y);
            if (!IsActive || !string.Equals(_surfaceId, surface.SurfaceId, StringComparison.Ordinal))
            {
                ActivateSurface(surface, chunk);
                return;
            }

            if (chunk != CurrentChunk)
            {
                RequestNeighborhoodTransition(chunk);
                // Boundary-detection frame only records/coalesces state. Heavy work starts on a
                // later Update, so crossing itself never shares a frame with chunk construction.
                return;
            }
            TickNeighborhoodTransition();
            var worldTick = _bootstrap.Session.World.Tick.Value;
            if (_streamTransitionPhase == StreamTransitionPhase.None &&
                worldTick != _lastStrategicMaterializationTick)
            {
                _lastStrategicMaterializationTick = worldTick;
                ReconcileOutdoorEntityMaterialization();
            }
        }

        /// <summary>Coalesces authored destructible topology changes until the next runtime update.</summary>
        public void NotifyOutdoorDestructibleStateChanged(string stableObjectId)
        {
            if (IsActive && !string.IsNullOrEmpty(stableObjectId))
                _dynamicNavigationDirty = true;
        }

        void RefreshDynamicNavigationIfDirty()
        {
            if (!IsActive)
                return;
            var world = _bootstrap?.Session?.World;
            var board = world?.OutdoorStatefulObjects;
            if (world == null || board == null)
                return;
            if (!ReferenceEquals(_navigationStateWorld, world) ||
                _observedDestructibleTopologyRevision != board.DestructibleTopologyRevision)
                _dynamicNavigationDirty = true;
            if (!_dynamicNavigationDirty || _streamTransitionPhase != StreamTransitionPhase.None)
                return;
            if (!string.IsNullOrEmpty(_independentFieldId))
            {
                BeginIndependentNavigationRefresh(board.DestructibleTopologyRevision);
                return;
            }
            RecomposeWalkGrid();
            _navigationStateWorld = world;
            _observedDestructibleTopologyRevision = board.DestructibleTopologyRevision;
            _dynamicNavigationDirty = false;
        }

        public bool PresentationToWorld(float x, float y, out float worldX, out float worldY)
        { _mapper.PresentationToWorld(x, y, out worldX, out worldY); return IsActive; }

        public bool TryWorldToPresentation(WorldVec2 worldPosition, out Vector3 presentation)
        {
            presentation = default;
            if (!IsActive || _mapper == null ||
                !_loaded.Contains(_mapper.WorldToChunk(worldPosition.X, worldPosition.Y)))
                return false;
            _mapper.WorldToPresentation(worldPosition.X, worldPosition.Y, out var x, out var y);
            presentation = HostPresentationSpace.FromPresentation(x, y);
            return true;
        }

        /// <summary>Surface membership comes from the authored Outdoor Surface, never from the
        /// optional regional SurfaceGround geography.</summary>
        public bool IsFieldFormalArmyInLoadedNeighborhood(FormalArmy army)
        {
            if (!IsActive || army == null || !army.WorldMotion.HasPosition ||
                army.State == FormalArmyState.Garrisoned ||
                army.WorldMotion.LocationKind == FormalArmyLocationKind.AtWorldSite ||
                _bootstrap?.Session?.World == null || !TryResolveSurface(out var surface) ||
                FormalArmyMemberPresenceSync.IsArmyEngaged(_bootstrap.Session.World, army))
                return false;
            var motionSurfaceId = army.WorldMotion.SurfaceId ?? string.Empty;
            if (!string.IsNullOrEmpty(motionSurfaceId) &&
                !string.Equals(motionSurfaceId, _surfaceId, StringComparison.Ordinal))
                return false;
            var position = army.WorldMotion.WorldPosition;
            return OutdoorSurfaceCoverageResolver.ContainsWorldPosition(
                       surface, position.X, position.Y) &&
                   _loaded.Contains(_mapper.WorldToChunk(position.X, position.Y));
        }

        /// <summary>
        /// Resolves a transient member formation point on the current loaded presentation grid.
        /// FormalArmy.WorldMotion remains the canonical position for every member.
        /// </summary>
        public bool TryResolveFormalArmyMemberPresentationPosition(
            FormalArmy army,
            int stableSlot,
            IReadOnlyList<WorldVec2> trail,
            out Vector3 presentation)
        {
            presentation = default;
            if (!IsFieldFormalArmyInLoadedNeighborhood(army) ||
                !TryResolveSurface(out var surface) || _compositeWalkGrid == null ||
                !TryWorldToPresentation(army.WorldMotion.WorldPosition, out var rawAnchor) ||
                !TryResolveCompositeWalkableAnchor(rawAnchor, out var anchor))
                return false;

            if (stableSlot <= 0)
            {
                presentation = anchor;
                return ValidateFormalArmyFormationAnchor(army, rawAnchor, presentation);
            }

            var world = _bootstrap.Session.World;
            var geography = world.SurfaceGround.TryGet(_surfaceId, out var candidateNavigation) &&
                            candidateNavigation.Contains(
                                army.WorldMotion.WorldPosition.X,
                                army.WorldMotion.WorldPosition.Y)
                ? candidateNavigation
                : null;
            var worldSpacing = Mathf.Max(.001f, surface.CellSize * 3f);
            if (geography != null &&
                TryWorldToPresentation(FormalArmyContinuousFormationResolver.Resolve(
                    army.WorldMotion, stableSlot, geography, worldSpacing / geography.CellSize,
                    trail), out var candidate) &&
                IsCompositeFormationSegmentWalkable(anchor, candidate))
            {
                presentation = candidate;
                return ValidateFormalArmyFormationAnchor(army, rawAnchor, presentation);
            }

            // A blocked or unavailable Core formation point falls back to the corrected anchor.
            presentation = anchor;
            return ValidateFormalArmyFormationAnchor(army, rawAnchor, presentation);
        }

        bool ValidateFormalArmyFormationAnchor(FormalArmy army, Vector3 expected,
            Vector3 presentation)
        {
            var cellPresentation = _mapper.CellSize * _mapper.PresentationUnitsPerWorldUnit;
            if (Vector3.Distance(expected, presentation) <= cellPresentation * 16f)
                return true;
            if (_distantFormalArmyFormationReported.Add(army.ArmyId))
                Debug.LogError("FormalArmy member presentation exceeds formation radius of Army.WorldMotion: " +
                               army.ArmyId);
            return false;
        }

        bool TryResolveCompositeWalkableAnchor(Vector3 rawAnchor, out Vector3 anchor)
        {
            anchor = rawAnchor;
            if (_compositeWalkGrid == null ||
                !_compositeWalkGrid.TryWorldToCell(rawAnchor.x, rawAnchor.y, out var x, out var y))
                return false;
            if (!_compositeWalkGrid.IsWalkable(x, y))
            {
                if (!_compositeWalkGrid.TryFindNearestWalkable(
                        x, y, MaterializeNearestWalkableRadiusCells, out x, out y))
                    return false;
                _compositeWalkGrid.CellToWorldCenter(x, y, out var px, out var py);
                anchor = HostPresentationSpace.FromPresentation(px, py);
            }
            return true;
        }

        bool IsCompositeFormationSegmentWalkable(Vector3 anchor, Vector3 candidate) =>
            _compositeWalkGrid != null &&
            GridPathfinder.IsWorldSegmentWalkable(
                _compositeWalkGrid, anchor.x, anchor.y, candidate.x, candidate.y);

        /// <summary>Resolves the formal physical endpoint without requiring its chunk to be loaded.</summary>
        public bool TryResolveContinuousAutoTravelGoal(
            HexCoord requestedHex,
            string destinationSiteId,
            out WorldVec2 goal,
            out float arrivalRadius,
            out string failureReason)
        {
            goal = default;
            arrivalRadius = 0f;
            failureReason = string.Empty;
            if (!IsActive || !TryResolveSurface(out var surface))
            {
                failureReason = "ContinuousSurfaceInactive";
                return false;
            }

            if (!string.IsNullOrEmpty(destinationSiteId))
            {
                var region = surface.SiteRegions?.Find(r =>
                    r != null && string.Equals(r.SiteId, destinationSiteId, StringComparison.Ordinal));
                if (region == null)
                {
                    failureReason = "OutdoorSiteArrivalMissing:" + destinationSiteId;
                    return false;
                }
                goal = new WorldVec2(region.ArrivalWorldX, region.ArrivalWorldY);
            }
            else
            {
                var size = _bootstrap.Session.World.HexWorld.HexSize > 0f
                    ? _bootstrap.Session.World.HexWorld.HexSize
                    : 1f;
                HexMath.ToWorldPosition(requestedHex, size, out var x, out var y);
                goal = new WorldVec2(x, y);
            }

            arrivalRadius = Math.Max(0.001f, surface.CellSize * 0.75f);
            return true;
        }

        /// <summary>Called after all realtime writers. Canonical is the last accepted safe point;
        /// rejected transforms never become position authority. No Stop command / CancelTravel.</summary>
        public void SyncPartyPresentation()
        {
            if (!IsActive) return;
            var session = _bootstrap.Session;
            var world = session.World;
            // Independent CharacterEncounter owns participant tactical coordinates and presence.
            // The ordinary Continuous WorldPosition synchronizer must not project PartyTravel
            // back onto those members from LateUpdate.
            if (world.Strategic.CharacterEncounter != null)
                return;
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
                    ResolveCurrentOutdoorSiteId(world, motion.WorldPosition));
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
                    !IsContinuousMoveLegal(world, new WorldVec2(oldX, oldY), oldHex, new WorldVec2(wx, wy)))
                {
                    _bootstrap.MoveController.CancelPresentationMovementPublic(id);
                    RestoreMember(id, previous);
                }
                _lastLegalMembers[id] = view.transform.position;
                CommitContinuousNpcPosition(id, view.transform.position);
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
                IsContinuousMoveLegal(world, motion.WorldPosition, motion.CurrentHex, _blockedCandidate))
                _autoTravelPathBlocked = false;
            return _autoTravelPathBlocked;
        }

        static bool IsContinuousMoveLegal(
            SimulationWorld world, WorldVec2 from, HexCoord committedHex, WorldVec2 to)
        {
            var nav = world?.SurfaceGround?.Active;
            var oldCovered = nav != null && nav.Contains(from.X, from.Y);
            var newCovered = nav != null && nav.Contains(to.X, to.Y);
            return oldCovered && newCovered && nav.IsSegmentWalkable(from.X, from.Y, to.X, to.Y);
        }

        void RestoreMember(EntityId id, Vector3 position)
        {
            if (_bootstrap.ViewSpawner.Registry.TryGet(id, out var view) && view != null)
                view.transform.position = position;
            if (_bootstrap.Session.World.Entities.TryGet(id, out var entity) &&
                entity.TryGet<XianXia.Core.Exploration.EntityLocationComponent>(out var location))
                location.SetPresentationOverride(position.x, position.y);
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
            SurfaceEgressStatus = "SurfaceBoundary";
            _mapper.WorldToPresentation(egress.BoundaryWorldX, egress.BoundaryWorldY, out var px, out var py);
            boundaryPresentation = new Vector3(px, py, HostPresentationSpace.EntityZ);
            return true;
        }

        // Independent encounters and the authored Surface edge are closed movement boundaries.
        public bool TryStepAcrossCoverageBoundary(Vector3 proposed)
        {
            var board = _bootstrap?.Session?.World?.ContinuousOutdoorMaterialization;
            if (board != null && board.HasIndependentEncounterBinding &&
                string.Equals(board.IndependentEncounterId, _independentFieldId,
                    StringComparison.Ordinal))
            {
                if (_compositeWalkGrid != null &&
                    _compositeWalkGrid.TryWorldToCell(proposed.x, proposed.y, out _, out _))
                    return false;
                LastMovementDiagnostic = "IndependentEncounterBoundary";
                return true;
            }
            if (!IsActive || !TryResolveSurface(out var surface)) return false;
            var motion = _bootstrap.Session.World.PlayerPartyTravel;
            _mapper.PresentationToWorld(proposed.x, proposed.y, out var x, out var y);
            if (!OutdoorSurfaceBoundaryEgressResolver.TryResolve(surface,
                    motion.WorldPosition.X, motion.WorldPosition.Y, x, y, out var egress) ||
                egress.EgressState != OutdoorSurfaceBoundaryEgressResolver.State.CrossingOuterBoundary)
                return false;
            SurfaceEgressStatus = "SurfaceBoundary";
            LastMovementDiagnostic = "SurfaceCoverageBoundary";
            return true;
        }

        public bool TryActivateAtCurrentWorldPosition()
        {
            TryMigrateLegacyOutdoorSiteRestore();
            var motion = _bootstrap?.Session?.World?.PlayerPartyTravel;
            var world = _bootstrap?.Session?.World;
            if (motion == null || !motion.HasPosition || motion.LocationKind != PlayerPartyLocationKind.AtWorldPosition ||
                world.LocalMap.IsInInterior ||
                (BattleOfferService.HasActiveManualEncounter(world) && !IsBoundContinuousManualCombat(world))) return false;
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
            if (world == null || ReferenceEquals(_legacyOutdoorRestoreMigrationWorld, world))
                return;
            _legacyOutdoorRestoreMigrationWorld = world;
            var motion = world?.PlayerPartyTravel;
            if (world?.HexWorld == null || motion == null ||
                motion.LocationKind != PlayerPartyLocationKind.AtWorldSite ||
                string.IsNullOrEmpty(motion.SiteId) ||
                !world.Strategic.Sites.TryGet(motion.SiteId, out var site) ||
                !WorldSiteOutdoorMigrationPolicy.UsesContinuousOutdoorSurface(site) ||
                motion.IsMoving)
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
                    if (PlayerPartyTransitionMembership.ShouldMemberTransitionWithParty(
                            world, party, party.Members[i]))
                        world.WorldPresence.SetAtWorldPosition(
                            party.Members[i], position, hex, _surfaceId);
            world.PartyWorld.ClearSiteFocus();
            world.PartyWorld.SiteId = string.Empty;
            world.PartyWorld.LocalMapId = string.Empty;
            world.PartyWorld.Mode = PartyWorldPresenceMode.AtWorldPosition;
            world.LocalMap.ActiveMapLayoutId = string.Empty;
            world.LocalMap.OverworldMapLayoutId = string.Empty;
        }

        void ActivateSurface(OutdoorWorldSurfaceDefinition surface, SurfaceChunkCoord center)
        {
            if (surface == null)
            {
                Debug.LogError("[ContinuousStartupInvariantFailure] Surface definition is null.", this);
                return;
            }
            var previousSurfaceId = _surfaceId;
            var previousMapper = _mapper;
            var previousGeography = _geography;
            _surfaceId = surface.SurfaceId;
            _bootstrap.Session.Registry.TryGetOutdoorSurfaceGeography(surface.SurfaceId, out _geography);
            _mapper = new OutdoorSurfaceCoordinateMapper(surface.ChunkWidth, surface.ChunkHeight, surface.CellSize, presentationUnitsPerWorldUnit: 1f / surface.CellSize, originWorldX: surface.OriginWorldX, originWorldY: surface.OriginWorldY);
            if (!TryPreflightNeighborhood(surface, center, out var activationFailure))
            {
                LogActivationFailure(surface, center, activationFailure);
                _surfaceId = previousSurfaceId;
                _mapper = previousMapper;
                _geography = previousGeography;
                return;
            }
            if (_bootstrap?.ContinuousWildernessLoadedSet?.IsActive == true)
                _bootstrap.DeactivateContinuousWildernessIfActive();
            _tileMap.RemoveLayoutInstance("legacy:active-localmap");
            IsActive = true;
            _bootstrap.Session.World.SurfaceGround.Activate(_geography?.Navigation);
            _navigationStateWorld = _bootstrap.Session.World;
            _observedDestructibleTopologyRevision =
                _bootstrap.Session.World.OutdoorStatefulObjects.DestructibleTopologyRevision;
            _dynamicNavigationDirty = false;
            var activeMotion = _bootstrap.Session.World.PlayerPartyTravel;
            activeMotion.SetCurrentOutdoorWorldSiteContext(
                ResolveCurrentOutdoorSiteId(
                    _bootstrap.Session.World, activeMotion.WorldPosition));
            _lastLegalMembers.Clear();
            LastMovementDiagnostic = string.Empty;
            _autoTravelPathBlocked = false;
            SurfaceEgressStatus = "None";
            _bootstrap.MoveController.InvalidatePartyLocalMovement(_bootstrap.Session.PlayerParty.Members);
            // A continuous surface is not a LocalMap. Dispose the previous WorldSite-only
            // labels/interactions only after every source needed by the initial neighborhood has
            // passed preflight. From this point activation cannot silently leave an empty view.
            _bootstrap.FinalizeContinuousWildernessPresentationHandoff();
            InitializeNeighborhood(center);
            AlignPartyPresentationToWorld();
            _bootstrap.SurfaceExitZonePresenter?.Clear();
            _bootstrap.MoveController.BindLocalMapContext("ContinuousSurface:" + _surfaceId);
            if (!TryValidateSurfaceActivationPostconditions(out var invariantFailure))
                Debug.LogError("[ContinuousStartupInvariantFailure] " + invariantFailure, this);
            Debug.Log("[W1C] Activated " + DescribeDiagnostics(), this);
        }

        /// <summary>Surface activation/hard handoff only. Ordinary adjacent crossings are staged.</summary>
        void InitializeNeighborhood(SurfaceChunkCoord center)
        {
            CancelNeighborhoodTransition();
            SurfaceChunkNeighborhood.CollectSquare(center, 1, _desired);
            _desired.RemoveWhere(coord => !HasChunk(coord));
            SurfaceChunkNeighborhood.Diff(_loaded, _desired, _add, _remove);
            foreach (var coord in _remove)
            {
                _tileMap.RemoveLayoutInstance(SurfaceOwnerKey(coord));
                _tileMap.RemoveLayoutInstance(GeographyOwnerKey(coord));
                RemoveSitePlacementInstances(coord);
                _presentedChunks.Remove(coord);
            }
            foreach (var coord in _add)
            {
                BuildChunk(coord);
                _presentedChunks.Add(coord);
            }
            _loaded.ExceptWith(_remove); _loaded.UnionWith(_add); CurrentChunk = center;
            RecomposeWalkGrid();
            RefreshLoadedOutdoorPlaces();
            ReconcileOutdoorEntityMaterialization();
            _bootstrap.RefreshContinuousOutdoorOverlaysOnce();
            Debug.Log("[W1C] Neighborhood add=" + _add.Count + " remove=" + _remove.Count + " " + DescribeDiagnostics(), this);
        }

        /// <summary>
        /// Coalesces an ordinary rectangular chunk crossing to the latest center. Existing incoming
        /// presentation that is still desired is retained; no player/camera/movement authority is touched.
        /// </summary>
        void RequestNeighborhoodTransition(SurfaceChunkCoord center)
        {
            if (_streamTransitionPhase != StreamTransitionPhase.None && center == _pendingCenter)
                return;

            CurrentChunk = center;
            _pendingCenter = center;
            SurfaceChunkNeighborhood.CollectSquare(center, 1, _desired);
            _desired.RemoveWhere(coord => !HasChunk(coord));

            _pendingAdds.Clear();
            foreach (var coord in _desired)
                if (!_presentedChunks.Contains(coord))
                    _pendingAdds.Add(coord);
            _pendingAdds.Sort((a, b) =>
            {
                // If rapid movement outruns a previous transition, build the current center first.
                var aCenter = a == center;
                var bCenter = b == center;
                if (aCenter != bCenter) return aCenter ? -1 : 1;
                return a.CompareTo(b);
            });

            _pendingRemoves.Clear();
            foreach (var coord in _presentedChunks)
                if (!_desired.Contains(coord))
                    _pendingRemoves.Add(coord);
            _pendingRemoves.Sort();
            _pendingAddIndex = 0;
            _pendingRemoveIndex = 0;
            if (_loaded.SetEquals(_desired))
            {
                // Common boundary jitter/coalesce case: gameplay scope is already the requested
                // one. Only retire presentation built for an abandoned pending transition.
                _streamTransitionPhase = _pendingRemoves.Count > 0
                    ? StreamTransitionPhase.RetireTrailing
                    : StreamTransitionPhase.None;
                return;
            }
            _streamTransitionPhase = _pendingAdds.Count > 0
                ? StreamTransitionPhase.BuildIncoming
                : StreamTransitionPhase.CommitLogicalNeighborhood;
        }

        /// <summary>Consumes at most one staged phase action per Update.</summary>
        void TickNeighborhoodTransition()
        {
            switch (_streamTransitionPhase)
            {
                case StreamTransitionPhase.None:
                    return;
                case StreamTransitionPhase.BuildIncoming:
                {
                    if (_pendingAddIndex >= _pendingAdds.Count)
                    {
                        _streamTransitionPhase = StreamTransitionPhase.CommitLogicalNeighborhood;
                        return;
                    }
                    var chunk = _pendingAdds[_pendingAddIndex++];
                    if (!_presentedChunks.Contains(chunk))
                    {
                        var started = Stopwatch.GetTimestamp();
                        BuildChunk(chunk);
                        _presentedChunks.Add(chunk);
                        LogStreamTiming("BuildIncoming", chunk, started);
                    }
                    if (_pendingAddIndex >= _pendingAdds.Count)
                        _streamTransitionPhase = StreamTransitionPhase.CommitLogicalNeighborhood;
                    return;
                }
                case StreamTransitionPhase.CommitLogicalNeighborhood:
                {
                    var started = Stopwatch.GetTimestamp();
                    _loaded.Clear();
                    _loaded.UnionWith(_desired);
                    RecomposeWalkGrid();
                    LogStreamTiming("ComposeWalkGrid", null, started);
                    _streamTransitionPhase = StreamTransitionPhase.RefreshGameplayScope;
                    return;
                }
                case StreamTransitionPhase.RefreshGameplayScope:
                {
                    var started = Stopwatch.GetTimestamp();
                    RefreshLoadedOutdoorPlaces();
                    ReconcileOutdoorEntityMaterialization();
                    LogStreamTiming("Materialization", null, started);
                    _streamTransitionPhase = StreamTransitionPhase.RefreshOverlay;
                    return;
                }
                case StreamTransitionPhase.RefreshOverlay:
                {
                    var started = Stopwatch.GetTimestamp();
                    _bootstrap.RefreshContinuousOutdoorOverlaysOnce();
                    LogStreamTiming("Overlay", null, started);
                    _streamTransitionPhase = StreamTransitionPhase.RetireTrailing;
                    return;
                }
                case StreamTransitionPhase.RetireTrailing:
                {
                    if (_pendingRemoveIndex >= _pendingRemoves.Count)
                    {
                        CompleteNeighborhoodTransition();
                        return;
                    }
                    var chunk = _pendingRemoves[_pendingRemoveIndex++];
                    if (!_desired.Contains(chunk) && _presentedChunks.Remove(chunk))
                    {
                        var started = Stopwatch.GetTimestamp();
                        _tileMap.RemoveLayoutInstance(SurfaceOwnerKey(chunk));
                        _tileMap.RemoveLayoutInstance(GeographyOwnerKey(chunk));
                        RemoveSitePlacementInstances(chunk);
                        LogStreamTiming("RetireTrailing", chunk, started);
                    }
                    if (_pendingRemoveIndex >= _pendingRemoves.Count)
                        CompleteNeighborhoodTransition();
                    return;
                }
            }
        }

        void CompleteNeighborhoodTransition()
        {
            _streamTransitionPhase = StreamTransitionPhase.None;
            _pendingAdds.Clear();
            _pendingRemoves.Clear();
            _pendingAddIndex = 0;
            _pendingRemoveIndex = 0;
        }

        void CancelNeighborhoodTransition()
        {
            CompleteNeighborhoodTransition();
            _desired.Clear();
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        void LogStreamTiming(string phase, SurfaceChunkCoord? chunk, long started)
        {
            var elapsedMs = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
            Debug.Log(
                "[ContinuousStream] center=" + _pendingCenter +
                " phase=" + phase +
                (chunk.HasValue ? " chunk=" + chunk.Value : string.Empty) +
                " ms=" + elapsedMs.ToString("F3"),
                this);
        }

        void BuildChunk(SurfaceChunkCoord coord)
        {
            if (!TryResolveSurface(out var surface))
                throw new InvalidOperationException("Continuous surface unavailable.");
            if (!ContinuousOutdoorStartupPlanner.TryValidateChunkData(_bootstrap.Session.Registry, surface, coord, out var failure))
                throw new InvalidOperationException("Continuous chunk data unavailable: " + failure);
            _bootstrap.Session.Registry.TryGetContinuousSurfaceWorldMap(surface.SurfaceId, out var terrain);
            _tileMap.BuildContinuousSurfaceChunkInstance(SurfaceOwnerKey(coord), terrain, _mapper, coord);
            if (_geography != null && _geography.CoverageChunks.Contains(coord))
                _tileMap.BuildOutdoorGeographyInstance(GeographyOwnerKey(coord), _geography, _mapper, coord);
            BuildBakedOutdoorSitePlacements(coord);
            BuildRuntimeConstructedOutdoorPlacements(coord);
        }

        string GeographyOwnerKey(SurfaceChunkCoord coord) =>
            SurfaceOwnerKey(coord) + ":geography";

        void RemoveSitePlacementInstances(SurfaceChunkCoord coord, string fieldId = null)
        {
            // Remove by presentation ownership, even when snapshot restore has already replaced the World board.
            var runtimePrefix = SitePlacementOwnerKey(coord, "runtime:", fieldId);
            foreach (var owner in new List<string>(_materializedSitePlacementOwners))
                if (owner.StartsWith(runtimePrefix, StringComparison.Ordinal))
                {
                    _tileMap.RemoveLayoutInstance(owner);
                    _materializedSitePlacementOwners.Remove(owner);
                }
            var sites = _bootstrap?.Session?.World?.Strategic?.Sites?.Sites;
            if (sites == null) return;
            foreach (var entry in sites)
            {
                var owner = SitePlacementOwnerKey(coord, entry.Key, fieldId);
                _tileMap.RemoveLayoutInstance(owner);
                _materializedSitePlacementOwners.Remove(owner);
            }
        }

        string SitePlacementOwnerKey(SurfaceChunkCoord coord, string siteId, string fieldId = null)
        {
            if (fieldId == null)
                fieldId = string.IsNullOrEmpty(_independentFieldId) ? _stagingIndependentFieldId : _independentFieldId;
            return (fieldId + ":surface:") + coord.X + ":" + coord.Y + ":site:" + (siteId ?? string.Empty);
        }

        /// <summary>
        /// Renders checked-in baked placements directly into the active continuous chunk.
        /// Empty LocalMap ground is intentionally ignored. Source-local cells are retained only as
        /// authored stamping semantics; actor movement reads canonical WorldPosition exclusively.
        /// </summary>
        void BuildBakedOutdoorSitePlacements(SurfaceChunkCoord chunk)
        {
            if (!TryResolveSurface(out var surface) || surface.SitePlacements == null) return;
            // Chunk presentation is built before the ordinary place-registry refresh. Seed cave
            // authority first so the initial stamp can already apply party reveal visibility.
            var world = _bootstrap?.Session?.World;
            if (world != null)
                for (var i = 0; i < surface.SitePlacements.Count; i++)
                {
                    var cave = surface.SitePlacements[i];
                    if (cave == null || !PlacementTouchesChunk(cave, chunk) ||
                        !string.Equals(MapKindCatalog.NormalizeKind(cave.Kind), "cave", StringComparison.Ordinal) ||
                        string.IsNullOrEmpty(cave.BoundLocationId)) continue;
                    for (var p = 0; p < surface.SitePlaces.Count; p++)
                    {
                        var place = surface.SitePlaces[p];
                        if (place == null || !string.Equals(place.LocationId, cave.BoundLocationId, StringComparison.Ordinal))
                            continue;
                        world.ContinuousOutdoorMaterialization.RegisterPlace(
                            place.SiteId, CreateMaterializedPlace(place, _mapper));
                        break;
                    }
                }
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
            {
                _tileMap.BuildOutdoorPlacementInstance(
                    SitePlacementOwnerKey(chunk, entry.Key), entry.Value, _mapper, chunk);
                _materializedSitePlacementOwners.Add(SitePlacementOwnerKey(chunk, entry.Key));
            }
        }

        /// <summary>Materialize only missing per-asset chunk owners; preserve active farm workers.</summary>
        public void RefreshRuntimeConstructedPlacementsForLoadedChunks()
        {
            if (!IsActive || _tileMap == null) return;
            foreach (var chunk in _loaded) BuildRuntimeConstructedOutdoorPlacements(chunk);
            RecomposeWalkGrid();
        }

        void BuildRuntimeConstructedOutdoorPlacements(SurfaceChunkCoord chunk)
        {
            var world = _bootstrap?.Session?.World;
            var assets = world?.OutdoorConstructedAssets;
            if (assets == null) return;
            foreach (var asset in assets.Assets.Values)
            {
                if (asset.SurfaceId != _surfaceId) continue;
                var placement = new OutdoorSurfacePlacementDefinition {
                    StableId = asset.StableAssetId, SiteId = asset.BoundWorldSiteId, Kind = asset.Kind,
                    WorldX = asset.WorldX, WorldY = asset.WorldY,
                    WorldWidth = asset.WorldWidth, WorldHeight = asset.WorldHeight,
                    SourceCellsW = asset.CellsW, SourceCellsH = asset.CellsH,
                    BoundLocationId = asset.BoundLocationId,
                    BlocksMovement = string.Equals(asset.Kind,
                        OutdoorConstructedAssetSemantics.StorageRoomKind, StringComparison.Ordinal),
                    Label = world.ConstructionCatalog.TryGet(asset.BuildingId, out var spec)
                        ? spec.DisplayName
                        : string.Equals(asset.Kind, "recoverySpot", StringComparison.Ordinal) ? "恢复处" :
                          string.Equals(asset.Kind, OutdoorConstructedAssetSemantics.StorageRoomKind,
                              StringComparison.Ordinal) ? "储藏室" : "农田"
                };
                if (!PlacementTouchesChunk(placement, chunk)) continue;
                var owner = SitePlacementOwnerKey(chunk, "runtime:" + asset.StableAssetId);
                if (_materializedSitePlacementOwners.Contains(owner)) continue;
                _tileMap.BuildOutdoorPlacementInstance(owner,
                    new List<OutdoorSurfacePlacementDefinition> { placement }, _mapper, chunk);
                _materializedSitePlacementOwners.Add(owner);
            }
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
                _mapper.ChunkLocalToWorld(coord, 0f, 0f, out var wx, out var wy);
                _mapper.WorldToPresentation(wx, wy, out var px, out var py);
                var width = Mathf.RoundToInt(_mapper.ChunkWidth / _mapper.CellSize);
                var height = Mathf.RoundToInt(_mapper.ChunkHeight / _mapper.CellSize);
                var cell = _mapper.CellSize * _mapper.PresentationUnitsPerWorldUnit;
                _grids.Add(new WalkGridComposer.Input(new WalkGrid(px, py, cell, width, height), 0f, 0f));
                var blockers = BuildSiteBlockerGrid(coord, px, py, cell, width, height);
                if (blockers != null) _grids.Add(new WalkGridComposer.Input(blockers, 0f, 0f));
                var geographyBlockers = BuildGeographyBlockerGrid(coord, px, py, cell, width, height);
                if (geographyBlockers != null) _grids.Add(new WalkGridComposer.Input(geographyBlockers, 0f, 0f));
            }
            if (_grids.Count > 0)
            {
                var composite = WalkGridComposer.Compose(_grids);
                var flags = _bootstrap.Session.World.Strategic.FactionFlags.Flags;
                foreach (var pair in flags)
                    if (pair.Value != null &&
                        (string.IsNullOrEmpty(pair.Value.SurfaceId) ||
                         string.Equals(pair.Value.SurfaceId, ActiveSurfaceId, StringComparison.Ordinal)) &&
                        (!pair.Value.HasWorldPosition ||
                         IsWorldPositionLoaded(ActiveSurfaceId, pair.Value.WorldX, pair.Value.WorldY)))
                        HostFactionFlagQuery.ApplyWalkGridBlock(pair.Value, this, composite);
                ClipEncounterGrid(composite);
                _compositeWalkGrid = composite;
                _bootstrap.MoveController.SetWalkGrid(composite);
                NavigationGeneration++;
            }
        }

        WalkGrid BuildGeographyBlockerGrid(
            SurfaceChunkCoord coord, float originX, float originY, float cell, int width, int height)
        {
            if (_geography?.Navigation == null || !_geography.CoverageChunks.Contains(coord)) return null;
            var grid = new WalkGrid(originX, originY, cell, width, height);
            _mapper.ChunkLocalToWorld(coord, 0f, 0f, out var chunkX, out var chunkY);
            var any = false;
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var wx = chunkX + (x + .5f) * _mapper.CellSize;
                var wy = chunkY + (y + .5f) * _mapper.CellSize;
                if (!_geography.Navigation.TryGetCell(wx, wy, out var kind)) continue;
                var blocked = (kind & SurfaceGroundCellKind.Solid) != 0 ||
                              ((kind & SurfaceGroundCellKind.Water) != 0 &&
                               (kind & SurfaceGroundCellKind.Bridge) == 0);
                if (!blocked) continue;
                grid.SetBlocked(x, y, true);
                any = true;
            }
            return any ? grid : null;
        }

        WalkGrid BuildSiteBlockerGrid(SurfaceChunkCoord coord, float originX, float originY, float cell, int width, int height)
        {
            if (!TryResolveSurface(out var surface) || surface.SitePlacements == null) return null;
            var grid = new WalkGrid(originX, originY, cell, width, height);
            var any = false;
            for (var i = 0; i < surface.SitePlacements.Count; i++)
            {
                var p = surface.SitePlacements[i];
                if (p == null || !p.BlocksMovement || !PlacementTouchesChunk(p, coord)) continue;
                if (OutdoorStatefulPlacementResolver.IsPerCellDestructible(p))
                {
                    var cellsW = Mathf.Max(1, p.SourceCellsW);
                    var cellsH = Mathf.Max(1, p.SourceCellsH);
                    var worldCellW = p.WorldWidth / cellsW;
                    var worldCellH = p.WorldHeight / cellsH;
                    for (var gy = 0; gy < cellsH; gy++)
                    for (var gx = 0; gx < cellsW; gx++)
                    {
                        if (!OutdoorStatefulPlacementResolver.IsBlockerActive(
                                _bootstrap.Session.World.OutdoorStatefulObjects, p, gx, gy))
                            continue;
                        RasterBlocker(
                            grid, originX, originY, cell,
                            p.WorldX + gx * worldCellW,
                            p.WorldY + gy * worldCellH,
                            worldCellW,
                            worldCellH);
                        any = true;
                    }
                    continue;
                }
                if (!OutdoorStatefulPlacementResolver.IsBlockerActive(
                        _bootstrap.Session.World.OutdoorStatefulObjects, p))
                    continue;
                RasterBlocker(grid, originX, originY, cell, p.WorldX, p.WorldY, p.WorldWidth, p.WorldHeight);
                any = true;
            }

            var runtimeAssets = _bootstrap.Session.World.OutdoorConstructedAssets;
            foreach (var asset in runtimeAssets.Assets.Values)
            {
                if (!string.Equals(asset.Kind, OutdoorConstructedAssetSemantics.StorageRoomKind,
                        StringComparison.Ordinal) || !string.Equals(asset.SurfaceId, _surfaceId, StringComparison.Ordinal))
                    continue;
                var placement = new OutdoorSurfacePlacementDefinition {
                    WorldX = asset.WorldX, WorldY = asset.WorldY,
                    WorldWidth = asset.WorldWidth, WorldHeight = asset.WorldHeight
                };
                if (!PlacementTouchesChunk(placement, coord)) continue;
                RasterBlocker(grid, originX, originY, cell,
                    asset.WorldX, asset.WorldY, asset.WorldWidth, asset.WorldHeight);
                any = true;
            }

            return any ? grid : null;
        }

        string ResolveCurrentOutdoorSiteId(SimulationWorld world, WorldVec2 position)
        {
            if (WorldSiteAdministrativeControlResolver.TryResolve(
                    world, _surfaceId, position.X, position.Y, out var dynamicSite, out _))
                return dynamicSite.SiteId;
            if (world?.Strategic?.TerritoryClaims?.HasAuthority == true)
                return string.Empty;
            return WorldSitePhysicalRegionQuery.ResolveSiteIdOrEmpty(world, position);
        }

        void RasterBlocker(
            WalkGrid grid, float originX, float originY, float cell,
            float worldX, float worldY, float worldWidth, float worldHeight)
        {
            _mapper.WorldToPresentation(worldX, worldY, out var left, out var bottom);
            _mapper.WorldToPresentation(worldX + worldWidth, worldY + worldHeight, out var right, out var top);
            // Cell-center rasterization preserves authored doorways. A sub-cell-thin blocker still
            // occupies its midpoint cell, without expanding along the long axis.
            ResolveCenterCoveredCellRange(left, right, originX, cell, out var minX, out var maxX);
            ResolveCenterCoveredCellRange(bottom, top, originY, cell, out var minY, out var maxY);
            grid.SetBlockedRect(minX, minY, maxX, maxY, true);
        }

        static void ResolveCenterCoveredCellRange(
            float edgeA,
            float edgeB,
            float gridOrigin,
            float cellSize,
            out int minCell,
            out int maxCell)
        {
            var min = (Mathf.Min(edgeA, edgeB) - gridOrigin) / cellSize;
            var max = (Mathf.Max(edgeA, edgeB) - gridOrigin) / cellSize;
            minCell = Mathf.CeilToInt(min - 0.5f);
            maxCell = Mathf.CeilToInt(max - 0.5f) - 1;
            if (minCell <= maxCell)
                return;

            // A blocker thinner than one cell may contain no cell center. Preserve it at the
            // cell containing its midpoint instead of silently deleting collision altogether.
            minCell = maxCell = Mathf.FloorToInt((min + max) * 0.5f);
        }

        /// <summary>Only the surface presentation owner may clear its chunk state. It never chooses a destination authority.</summary>
        public void DeactivatePresentationOnly() => DeactivatePresentationOnly(captureEntityPositions: true);

        public void DeactivateForInteriorTransition() => DeactivatePresentationOnly(captureEntityPositions: false);

        void DeactivatePresentationOnly(bool captureEntityPositions)
        {
            _bootstrap?.GetComponent<HostCharacterEncounter>()?.CancelPreparation();
            CancelIndependentNavigation();
            if (captureEntityPositions) CaptureCurrentPersonalPlacements();
            var world = _navigationStateWorld;
            world?.ContinuousOutdoorMaterialization.ClearIndependentEncounter(_independentFieldId);
            var combat = world?.Strategic?.ContinuousManualCombat;
            if (combat != null && combat.IsActive &&
                string.Equals(combat.SurfaceId, _surfaceId, StringComparison.Ordinal))
                combat.ClearOwned(combat.OfferId);
            foreach (var coord in _presentedChunks)
            {
                _tileMap?.RemoveLayoutInstance(SurfaceOwnerKey(coord));
                _tileMap?.RemoveLayoutInstance(GeographyOwnerKey(coord));
                RemoveSitePlacementInstances(coord);
            }
            CancelNeighborhoodTransition();
            _presentedChunks.Clear();
            _loaded.Clear(); _desired.Clear(); _add.Clear(); _remove.Clear(); IsActive = false;
            _materializedSitePlacementOwners.Clear();
            _grids.Clear();
            _compositeWalkGrid = null;
            if (world != null && world.SurfaceGround.Active == _geography?.Navigation)
                world.SurfaceGround.Clear();
            _geography = null;
            _observedDestructibleTopologyRevision = 0;
            _dynamicNavigationDirty = false;
            _lastLegalMembers.Clear();
            ReleaseContinuousSitePopulation(capturePositions: captureEntityPositions);
            _navigationStateWorld = null;
            _bootstrap?.MoveController?.InvalidatePartyLocalMovement(_bootstrap.Session.PlayerParty.Members);
            _bootstrap?.MoveController?.SetWalkGrid(null);
            _bootstrap?.MoveController?.BindLocalMapContext(string.Empty);
            Debug.Log("[W1C] Deactivated surface=" + _surfaceId + " Loaded=0", this);
            _surfaceId = string.Empty;
            _independentFieldId = string.Empty;
        }

        /// <summary>
        /// Snapshot restore replaces SimulationWorld. Rebuild the active Surface once so every
        /// retained presentation, registry, SurfaceGround and navigation input binds the new world.
        /// Ordinary chunk streaming never calls this hard boundary.
        /// </summary>
        public bool RebuildAfterWorldRestore()
        {
            if (IsActive)
                DeactivatePresentationOnly(captureEntityPositions: false);
            _independentFieldId = string.Empty;
            _legacyOutdoorRestoreMigrationWorld = null;
            var world = _bootstrap?.Session?.World;
            _snapshotRestoredWorld = world;
            // SPACE-01：Active Separate Space 不得重建 Continuous Outdoor presentation。
            if (world?.LocalMap != null && world.LocalMap.IsActive)
                return false;
            // Snapshot-restored EntityLocation placement is persistence truth even while its
            // Separate Space is inactive. Outdoor materializers overwrite only entities for which
            // they hold positive Outdoor authority; presentation rebuild must never reset every
            // Character in the world merely because the current view is Outdoor.
            _isSnapshotPresentationRebuild = true;
            try
            {
                var activated = TryActivateAtCurrentWorldPosition();
                var state = world?.Strategic?.CharacterEncounter;
                if (activated && state != null && state.Phase != CharacterEncounterPhase.Committed)
                {
                    CharacterEncounterService.BindRuntime(world);
                    return BeginIndependentFieldRestore(state);
                }
                if (activated)
                    DiagnoseSnapshotMaterialization(world);
                return activated;
            }
            finally { _isSnapshotPresentationRebuild = false; }
        }

        void DiagnoseSnapshotMaterialization(SimulationWorld world)
        {
            if (world == null || _mapper == null || !TryResolveSurface(out var surface)) return;
            var restored = 0;
            var eligible = 0;
            var materialized = 0;
            var rejectedChunk = 0;
            var rejectedSite = 0;
            var missingSpatial = 0;
            foreach (var pair in world.WorldPresence.All)
            {
                var presence = pair.Value;
                if (presence == null || presence.EntityId.IsNone ||
                    (_bootstrap.Session.PlayerParty?.IsMember(presence.EntityId) ?? false) ||
                    ArmyService.TryGetArmyForCharacter(world, presence.EntityId, out _))
                    continue;
                restored++;
                if (!presence.HasContinuousWorldPosition)
                {
                    if (presence.Mode == PartyWorldPresenceMode.AtSite) missingSpatial++;
                    continue;
                }
                if (!string.Equals(presence.PersonalSurfaceId, _surfaceId, StringComparison.Ordinal))
                    continue;
                var inChunk = _loaded.Contains(_mapper.WorldToChunk(presence.WorldPosX, presence.WorldPosY));
                if (!inChunk)
                {
                    rejectedChunk++;
                    if (_bootstrap.ViewSpawner.Registry.TryGet(presence.EntityId, out var leaked) &&
                        leaked != null)
                        Debug.LogError("[SnapshotMaterializationLeak] Entity=" + presence.EntityId.Value +
                            " SavedChunk=" + _mapper.WorldToChunk(presence.WorldPosX, presence.WorldPosY) +
                            " ActiveChunk=" + CurrentChunk, this);
                    continue;
                }
                if (presence.Mode == PartyWorldPresenceMode.AtSite &&
                    !IsContinuousSiteRelevantToLoadedNeighborhood(
                        world, surface, presence.SiteId, presence.WorldPosX, presence.WorldPosY))
                {
                    rejectedSite++;
                    continue;
                }
                eligible++;
                if (world.ContinuousOutdoorMaterialization.IsMaterialized(presence.EntityId))
                    materialized++;
            }
            foreach (var pair in world.Strategic.FormalArmies.Armies)
            {
                var army = pair.Value;
                if (army == null || !army.WorldMotion.HasPosition ||
                    !string.Equals(army.WorldMotion.SurfaceId, _surfaceId, StringComparison.Ordinal) ||
                    _loaded.Contains(_mapper.WorldToChunk(
                        army.WorldMotion.WorldPosition.X, army.WorldMotion.WorldPosition.Y)))
                    continue;
                foreach (var rawId in army.MemberCharacterIds)
                    if (_bootstrap.ViewSpawner.Registry.TryGet(new EntityId(rawId), out var leakedArmyView) &&
                        leakedArmyView != null)
                        Debug.LogError("[SnapshotMaterializationLeak] Army=" + army.ArmyId +
                            " Member=" + rawId + " SavedChunk=" +
                            _mapper.WorldToChunk(army.WorldMotion.WorldPosition.X,
                                army.WorldMotion.WorldPosition.Y), this);
            }
            SnapshotSpatialSummary = "RestoredPersonalPresences=" + restored +
                " LoadedNeighborhoodEligible=" + eligible + " Materialized=" + materialized +
                " RejectedDifferentChunk=" + rejectedChunk + " RejectedDifferentSite=" + rejectedSite +
                " LegacyAnchorMigrated=" + HostSnapshotSessionRehydration.LastLegacyAnchorMigrated +
                " MissingSpatialAuthority=" + missingSpatial;
            Debug.Log("[SnapshotMaterializationCensus] " + SnapshotSpatialSummary, this);
        }

        /// <summary>Rebuilds the transient place registry from baked continuous content. Called
        /// only when the loaded chunk neighborhood changes.</summary>
        void RefreshLoadedOutdoorPlaces()
        {
            var world = _bootstrap?.Session?.World;
            if (world == null || !TryResolveSurface(out var surface)) return;
            PlaceRefreshGeneration++;
            world.ContinuousOutdoorMaterialization.ClearPlaces();
            var loadedPlaces = 0;
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
                    world.ContinuousOutdoorMaterialization.RegisterPlace(
                        region.SiteId, CreateMaterializedPlace(place, _mapper));
                    loadedPlaces++;
                }
            }

            LoadedPlaceCount = loadedPlaces;
        }

        public static XianXia.Core.Exploration.WorldLocationState CreateMaterializedPlace(
            WorldSitePlaceDefinition place, OutdoorSurfaceCoordinateMapper mapper)
        {
            if (place == null) throw new ArgumentNullException(nameof(place));
            if (mapper == null) throw new ArgumentNullException(nameof(mapper));
            mapper.WorldToPresentation(place.WorldX, place.WorldY, out var placeX, out var placeY);
            var kind = XianXia.Core.Exploration.LocationKind.Wild;
            if (!string.IsNullOrWhiteSpace(place.Kind) &&
                Enum.TryParse(place.Kind, true, out XianXia.Core.Exploration.LocationKind parsedKind))
                kind = parsedKind;
            var result = new XianXia.Core.Exploration.WorldLocationState
            {
                Id = place.LocationId, Name = place.Name, PresentationX = placeX, PresentationZ = placeY,
                Kind = kind,
                ResourceOnExploreId = place.ResourceOnExploreId ?? string.Empty,
                ResourceOnExploreAmount = place.ResourceOnExploreAmount,
                OpportunitySiteId = place.OpportunitySiteId ?? string.Empty,
                ResidentNpcDefinitionId = place.ResidentNpcDefinitionId ?? string.Empty,
                LocalMapId = place.LocalMapId ?? string.Empty,
                EnterLocalMapId = place.EnterLocalMapId ?? string.Empty,
                EnterSpawnLocationId = place.EnterSpawnLocationId ?? string.Empty,
                SurveySenseRequired = place.SurveySenseRequired
            };
            result.AdjacentIds.AddRange(place.AdjacentIds);
            result.EnterConditions.AddRange(place.EnterConditions);
            result.QuestOfferIds.AddRange(place.QuestOfferIds);
            result.Tags.AddRange(place.Tags);
            result.AllowedActivities.AddRange(place.AllowedActivities);
            return result;
        }

        /// <summary>Reconciles GameObject membership from loaded chunks and domain presence.
        /// Existing members keep their realtime presentation position.</summary>
        void ReconcileOutdoorEntityMaterialization()
        {
            var world = _bootstrap?.Session?.World;
            var motion = world?.PlayerPartyTravel;
            if (world == null || motion == null || !TryResolveSurface(out var surface))
                return;
            EntityReconcileGeneration++;
            _desiredMaterializedEntities.Clear();
            _continuousFormalArmyPopulation.Clear();
            _materializePointUses.Clear();

            // ManualEncounter/PostBattle owns an isolated character scope on this Surface.
            // Keep only the frozen actual participants; ordinary Party, Army, Site and residual
            // population passes resume after the offer-owned context is cleared.
            var combat = world.Strategic?.ContinuousManualCombat;
            if (combat != null && combat.IsActive &&
                string.Equals(combat.SurfaceId, _surfaceId, StringComparison.Ordinal))
            {
                foreach (var rawId in combat.ParticipantIds)
                {
                    var id = new EntityId(rawId);
                    if (id.IsNone || !world.Entities.TryGet(id, out var entity) || entity == null ||
                        XianXia.Core.Combat.CombatLifeStateService.ShouldHideFromSpawn(entity) ||
                        !entity.TryGet<XianXia.Core.Exploration.EntityLocationComponent>(out var location) ||
                        location == null || !location.HasPresentationOverride)
                        continue;
                    _desiredMaterializedEntities.Add(id);
                    if (ArmyService.TryGetArmyForCharacter(world, id, out _))
                        _continuousFormalArmyPopulation.Add(id);
                }
                ApplyOutdoorEntityMaterializationReconcile(world);
                return;
            }

            var party = _bootstrap.Session.PlayerParty;
            if (party != null)
            {
                world.SurfaceGround.TryGet(_surfaceId, out var partyNavigation);
                for (var i = 0; i < party.Members.Count; i++)
                {
                    var memberId = party.Members[i];
                    if (!PlayerPartyTransitionMembership.ShouldMemberTransitionWithParty(
                            world, party, memberId))
                        continue;
                    _desiredMaterializedEntities.Add(memberId);
                    if (!world.ContinuousOutdoorMaterialization.IsMaterialized(memberId) &&
                        world.Entities.TryGet(memberId, out var member) &&
                        member.TryGet<XianXia.Core.Exploration.EntityLocationComponent>(out var memberLoc))
                    {
                        if (PlayerPartyContinuousFormationResolver.TryResolve(world, party, memberId,
                                partyNavigation, out var partyPoint))
                        {
                            _mapper.WorldToPresentation(partyPoint.X, partyPoint.Y, out var px, out var py);
                            memberLoc.SetPresentationOverride(px, py);
                        }
                    }
                }
            }

            // An engaged FormalArmy is intentionally absent from the ordinary field-army pass.
            // The bound Continuous combat context owns exactly these frozen participants for the
            // lifetime of ManualEncounter and PostBattle, so reconcile must retain their Views.
            // Field FormalArmy members are ordinary near-field population on the active
            // Continuous Surface. Membership and placement derive only from WorldMotion;
            // stale per-character WorldPresence must never override or capture this authority.
            foreach (var pair in world.Strategic.FormalArmies.Armies)
            {
                var army = pair.Value;
                if (army == null || !army.WorldMotion.HasPosition ||
                    army.State == FormalArmyState.Garrisoned ||
                    army.WorldMotion.LocationKind == FormalArmyLocationKind.AtWorldSite ||
                    FormalArmyMemberPresenceSync.IsArmyEngaged(world, army))
                    continue;
                var inLoadedNeighborhood = IsFieldFormalArmyInLoadedNeighborhood(army);

                _formalArmyMemberScratch.Clear();
                for (var i = 0; i < army.MemberCharacterIds.Count; i++)
                    _formalArmyMemberScratch.Add(army.MemberCharacterIds[i]);
                _formalArmyMemberScratch.Sort();
                for (var slot = 0; slot < _formalArmyMemberScratch.Count; slot++)
                {
                    var id = new EntityId(_formalArmyMemberScratch[slot]);
                    if (id.IsNone || (party != null && party.IsMember(id)) ||
                        !LingeringBattlefieldPartyService.IsLivingForMacroOrder(world, id) ||
                        !world.Entities.TryGet(id, out var entity) ||
                        !ArmyService.TryGetArmyForCharacter(world, id, out var boundArmy) ||
                        boundArmy == null ||
                        !string.Equals(boundArmy.ArmyId, army.ArmyId, StringComparison.Ordinal))
                        continue;

                    // Mark every valid field-army member as Army-owned even when its Surface,
                    // chunk or placement is currently rejected. The generic WorldPresence pass
                    // must not rematerialize it through a stale derived record.
                    _continuousFormalArmyPopulation.Add(id);
                    if (!inLoadedNeighborhood)
                        continue;

                    if (!TryResolveFormalArmyMemberPresentationPosition(
                            army, slot, null, out var presentation))
                        continue;
                    if (!entity.TryGet<XianXia.Core.Exploration.EntityLocationComponent>(out var loc))
                    {
                        loc = new XianXia.Core.Exploration.EntityLocationComponent();
                        entity.AddComponent(loc);
                    }
                    loc.SetPresentationOverride(presentation.x, presentation.y);
                    if (!_materializePointUses.ContainsKey(
                            QuantizeMaterializePoint(presentation.x, presentation.y)))
                        _materializePointUses[
                            QuantizeMaterializePoint(presentation.x, presentation.y)] = id.Value;

                    // Commit membership only after a valid presentation point exists. View
                    // creation remains the later EntityViewSpawner step and is not a prerequisite.
                    _desiredMaterializedEntities.Add(id);
                    _continuousSitePopulation.Add(id);
                }
            }

            for (var regionIndex = 0; regionIndex < surface.SiteRegions.Count; regionIndex++)
            {
                var region = surface.SiteRegions[regionIndex];
                if (region == null || !IsSiteInLoadedNeighborhood(surface, region.SiteId) ||
                    !world.Strategic.Sites.TryGet(region.SiteId, out var site) ||
                    !WorldSiteOutdoorMigrationPolicy.UsesContinuousOutdoorSurface(site))
                    continue;
                StrategicWorldSitePopulationService.CollectCharacterIdsPresentAtWorldSite(
                    world, site, null, _sitePopulationScratch);
                for (var i = 0; i < _sitePopulationScratch.Count; i++)
                {
                    var id = _sitePopulationScratch[i];
                    if (_bootstrap.Session.PlayerParty.IsMember(id) ||
                        (combat != null && combat.Contains(id)) ||
                        !world.Entities.TryGet(id, out var entity)) continue;
                    // Only members actually claimed by the field-army pass above are skipped
                    // here. StrategicWorldSitePopulationService is the authority for every
                    // army member it resolves at this loaded Site, including the opening
                    // supervisor whose army remains Idle rather than Garrisoned.
                    if (_continuousFormalArmyPopulation.Contains(id))
                        continue;
                    if (world.WorldPresence.TryGet(id, out var scopedPresence) &&
                        scopedPresence != null && scopedPresence.HasContinuousWorldPosition &&
                        !CharacterPersonalSpaceQuery.TryResolveContinuous(
                            world, id, _surfaceId, out _, out _))
                        continue;
                    if (CharacterPersonalSpaceQuery.TryResolveContinuous(world, id, _surfaceId,
                            out var savedPersonal, out _))
                    {
                        if (!_loaded.Contains(_mapper.WorldToChunk(savedPersonal.X, savedPersonal.Y))) continue;
                        _desiredMaterializedEntities.Add(id);
                        _continuousSitePopulation.Add(id);
                        if (!world.ContinuousOutdoorMaterialization.IsMaterialized(id))
                        {
                            if (!entity.TryGet<XianXia.Core.Exploration.EntityLocationComponent>(out var restoredLocation))
                            {
                                restoredLocation = new XianXia.Core.Exploration.EntityLocationComponent();
                                entity.AddComponent(restoredLocation);
                            }
                            _mapper.WorldToPresentation(savedPersonal.X, savedPersonal.Y, out var savedX, out var savedY);
                            restoredLocation.SetPresentationOverride(savedX, savedY);
                        }
                        continue;
                    }
                    // A restored world cannot invent a resident position at Site arrival.
                    // Missing current spatial authority must be migrated explicitly before presentation.
                    if (ReferenceEquals(_snapshotRestoredWorld, world))
                        continue;
                    // 落点优先级（§9）：precise Continuous authored anchor
                    //　→ EntityLocation.LocationId 对应的 baked SitePlace
                    //　→ deterministic fallback。
                    _desiredMaterializedEntities.Add(id);
                    _continuousSitePopulation.Add(id);
                    if (world.ContinuousOutdoorMaterialization.IsMaterialized(id))
                        continue;
                    var wx = region.ArrivalWorldX; var wy = region.ArrivalWorldY;
                    var hasRuntimeAnchor = false;
                    var runtimeX = 0f;
                    var runtimeY = 0f;
                    if (world.WorldPresence.TryGet(id, out var sitePresence) &&
                        sitePresence != null &&
                        sitePresence.Mode == XianXia.Core.World.PartyWorldPresenceMode.AtSite &&
                        string.Equals(sitePresence.SiteId, region.SiteId, StringComparison.Ordinal) &&
                        sitePresence.HasContinuousWorldPosition)
                    {
                        hasRuntimeAnchor = true;
                        runtimeX = sitePresence.WorldPosX;
                        runtimeY = sitePresence.WorldPosY;
                    }

                    if (!entity.TryGet<XianXia.Core.Exploration.EntityLocationComponent>(out var loc))
                    {
                        loc = new XianXia.Core.Exploration.EntityLocationComponent();
                        entity.AddComponent(loc);
                    }

                    // §9：RuntimePreciseAnchor → BakedOpeningEntityAnchor → BakedSitePlace
                    //　→ SiteArrivalFallback。§11：明显不属于本 Site baked envelope 的 runtime
                    // anchor 被拒绘并记录，不删除 Domain entity。
                    // §4：落点 identity 用 SpawnStableKey（GameStart 建立、可从 Entity 反查），
                    // 绝不再把 DefinitionId 当 spawnKey（同一 Definition 多次 spawn 会吃 first-match）。
                    var definitionId = entity.DefinitionId.ToString();
                    var spawnKey = ResolveOpeningSpawnKey(world, id, definitionId);
                    var anchorSource = ContinuousOutdoorOpeningAnchorResolver.ResolveInitialPlacement(
                        surface,
                        region.SiteId,
                        spawnKey,
                        loc.LocationId ?? string.Empty,
                        hasRuntimeAnchor,
                        runtimeX,
                        runtimeY,
                        out var resolvedAnchor,
                        out var rejectedReason,
                        out var rejectedAnchor);
                    // 仅首次采用 authored opening anchor 时同步逻辑出生地点。Runtime precise
                    // anchor（NPC 已移动／dematerialize capture）绝不把 LocationId 重置回出生地。
                    if (!hasRuntimeAnchor &&
                        anchorSource == OpeningInitialPlacementSource.BakedOpeningEntityAnchor &&
                        ContinuousOutdoorOpeningAnchorResolver.TryGetBakedEntityAnchorDefinition(
                            surface, region.SiteId, spawnKey, out var bakedAnchorDefinition) &&
                        !string.IsNullOrWhiteSpace(bakedAnchorDefinition.SourceLocationId))
                        loc.LocationId = bakedAnchorDefinition.SourceLocationId;
                    if (rejectedAnchor && _rejectedAnchorReported.Add(id.Value))
                        Debug.LogWarning(
                            "[ContinuousResidentAnchorRejected] Entity=" + id.Value +
                            " Site=" + region.SiteId + " " + rejectedReason +
                            " -> fallback to baked opening anchor / SitePlace", this);
                    if (anchorSource != OpeningInitialPlacementSource.SiteArrivalFallback)
                    {
                        wx = resolvedAnchor.X;
                        wy = resolvedAnchor.Y;
                    }
                    else
                    {
                        AddDeterministicFallbackOffset(id, ref wx, ref wy, surface.CellSize);
                    }

                    _mapper.WorldToPresentation(wx, wy, out var px, out var py);
                    SeparateMaterializePoint(id, ref px, ref py);
                    EnforceWalkableMaterializePoint(id, region.SiteId, definitionId, ref px, ref py);
                    loc.SetPresentationOverride(px, py);
                }
            }

            // Continuous positions and residual AtHex presences in loaded chunks are part of the
            // same transient presentation scope, including downed characters and visible corpses.
            foreach (var pair in world.WorldPresence.All)
            {
                var presence = pair.Value;
                var id = presence != null ? presence.EntityId : EntityId.None;
                if (id.IsNone || _desiredMaterializedEntities.Contains(id) ||
                    !world.Entities.TryGet(id, out var entity) ||
                    XianXia.Core.Combat.CombatLifeStateService.ShouldHideFromSpawn(entity)) continue;
                if (FormalArmyMemberPresenceSync.IsArmyControlledMember(world, id))
                    continue;
                if (!string.Equals(presence.PersonalSurfaceId, _surfaceId, StringComparison.Ordinal))
                    continue;
                WorldVec2 position;
                var hasPrecisePosition = false;
                if (presence.Mode == PartyWorldPresenceMode.AtWorldPosition && presence.HasContinuousWorldPosition)
                {
                    position = presence.ContinuousWorldPosition;
                    hasPrecisePosition = true;
                }
                else if (presence.Mode == PartyWorldPresenceMode.AtHex && presence.HasContinuousWorldPosition)
                {
                    position = presence.ContinuousWorldPosition;
                    hasPrecisePosition = true;
                }
                else if (presence.Mode == PartyWorldPresenceMode.AtHex && presence.UsesHexPresence)
                {
                    HexMath.ToWorldPosition(presence.ResidualHex, world.HexWorld.HexSize, out var hx, out var hy);
                    position = new WorldVec2(hx, hy);
                }
                else continue;
                if (!_loaded.Contains(_mapper.WorldToChunk(position.X, position.Y))) continue;
                _desiredMaterializedEntities.Add(id);
                _continuousSitePopulation.Add(id);
                if (world.ContinuousOutdoorMaterialization.IsMaterialized(id))
                    continue;
                var wx = position.X; var wy = position.Y;
                if (!hasPrecisePosition)
                    AddDeterministicFallbackOffset(id, ref wx, ref wy, surface.CellSize);
                _mapper.WorldToPresentation(wx, wy, out var px, out var py);
                if (!entity.TryGet<XianXia.Core.Exploration.EntityLocationComponent>(out var loc))
                {
                    loc = new XianXia.Core.Exploration.EntityLocationComponent();
                    entity.AddComponent(loc);
                }
                loc.SetPresentationOverride(px, py);
                if (!_materializePointUses.ContainsKey(QuantizeMaterializePoint(px, py)))
                    _materializePointUses[QuantizeMaterializePoint(px, py)] = id.Value;
            }

            ApplyOutdoorEntityMaterializationReconcile(world);
        }

        void ApplyOutdoorEntityMaterializationReconcile(SimulationWorld world)
        {
            _newlyMaterializedEntities.Clear();
            var preserveHiddenPlacement = world.Strategic?.ContinuousManualCombat != null &&
                                          world.Strategic.ContinuousManualCombat.IsActive &&
                                          string.Equals(
                                              world.Strategic.ContinuousManualCombat.SurfaceId,
                                              _surfaceId,
                                              StringComparison.Ordinal);
            world.ContinuousOutdoorMaterialization.ReconcileEntities(
                _desiredMaterializedEntities,
                onAdd: id => _newlyMaterializedEntities.Add(id),
                onRemove: id =>
                {
                    var hasView = _bootstrap.ViewSpawner.Registry.TryGet(id, out var view) && view != null;
                    if (!_isSnapshotPresentationRebuild && !preserveHiddenPlacement && hasView)
                        TryCaptureAtSiteAnchor(id, view.transform.position);
                    if (world.Entities.TryGet(id, out var entity) &&
                        entity.TryGet<XianXia.Core.Exploration.EntityLocationComponent>(out var loc))
                    {
                        // Isolation only destroys the View. Keep the last presentation anchor so
                        // normal population reconciliation can restore the same person in place.
                        if (preserveHiddenPlacement)
                        {
                            if (hasView)
                                loc.SetPresentationOverride(view.transform.position.x, view.transform.position.y);
                        }
                        else
                            loc.ClearPresentationOverride();
                    }
                    _continuousSitePopulation.Remove(id);
                    _continuousFormalArmyPopulation.Remove(id);
                });
            _bootstrap.Session.RefreshViewableEntityIds();
            _bootstrap.ViewSpawner.PruneHiddenViews(_bootstrap.Session);
            _bootstrap.ViewSpawner.SpawnMissingVisibleViews(_bootstrap.Session);
            RealignMaterializedViewPlacements(_newlyMaterializedEntities);
            if (!_isSnapshotPresentationRebuild)
                CaptureCurrentPersonalPlacements();
        }

        /// <summary>
        /// materialize 已写入权威 PresentationOverride，但 <c>SpawnMissingVisibleViews</c> 只补
        /// 「缺失」view、绝不搬动已存在的 view。若 view 在 activation 之前就由
        /// <c>EntityViewSpawner.Rebuild</c> 建好（那时只能退回 legacy WorldRegion 地点 presentation +
        /// stack 偏移），它就会永久停在回退位置：Expected=Materialized=Views 全部成立，人却在镜头外。
        /// 这里在同一 pass 内把已存在的 view 对齐到权威落点（策略见
        /// <see cref="ContinuousMaterializePlacementSync"/>）：PlayerParty 成员与正在移动的实体跳过。
        /// 只在 materialize pass（startup barrier／chunk neighborhood 变化／scope change）执行，
        /// 绝不进入 per-tick 路径（§17-F：普通 tick 不得增长 reconcile。
        /// </summary>
        void RealignMaterializedViewPlacements(IEnumerable<EntityId> candidates)
        {
            var session = _bootstrap?.Session;
            var world = session?.World;
            var registry = _bootstrap?.ViewSpawner?.Registry;
            if (world == null || registry == null || _mapper == null)
            {
                RealignedViewCount = 0;
                return;
            }

            var move = _bootstrap.MoveController;
            var party = session.PlayerParty;
            var realigned = 0;
            foreach (var id in candidates)
            {
                if (id.IsNone || !registry.TryGet(id, out var view) || view == null)
                    continue;
                if (!world.Entities.TryGet(id, out var entity) || entity == null ||
                    !entity.TryGet<XianXia.Core.Exploration.EntityLocationComponent>(out var loc) || loc == null)
                    continue;

                var isPartyMember = party != null && party.IsMember(id);
                var isMoving = IsEntityUnderContinuousMovementAuthority(world, id, move);
                var target = HostPresentationSpace.FromPresentation(
                    loc.PresentationOverrideX, loc.PresentationOverrideZ, view.transform.position.z);
                if (!ContinuousMaterializePlacementSync.ShouldRealign(
                        true,
                        isPartyMember,
                        isMoving,
                        loc.HasPresentationOverride,
                        view.transform.position.x,
                        view.transform.position.y,
                        target.x,
                        target.y))
                    continue;

                view.transform.position = target;
                realigned++;
            }

            RealignedViewCount = realigned;
        }

        bool IsEntityUnderContinuousMovementAuthority(
            SimulationWorld world, EntityId id, HostMoveController move) =>
            (move != null && move.IsMoving(id)) ||
            world.BackgroundCharacterTravel.IsTraveling(id) ||
            ArmyService.TryGetArmyForCharacter(world, id, out _) ||
            (_bootstrap?.Session?.PlayerParty?.IsMember(id) ?? false) ||
            ActualBattleParticipantQuery.TryFind(world.Strategic.Participants, id, out _) ||
            world.Strategic.CharacterEncounter?.Find(id.Value) != null;

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

        /// <summary>Rebuild only loaded baked owners touched by a newly revealed entrance.</summary>
        public bool RefreshRevealedOpportunityPresentation(string boundLocationId)
        {
            if (!IsActive || _tileMap == null || string.IsNullOrEmpty(boundLocationId) ||
                !TryResolveSurface(out var surface) || surface.SitePlacements == null)
                return false;
            var touched = false;
            for (var i = 0; i < surface.SitePlacements.Count; i++)
            {
                var entrance = surface.SitePlacements[i];
                if (entrance == null ||
                    !string.Equals(entrance.BoundLocationId, boundLocationId, StringComparison.Ordinal) ||
                    !string.Equals(MapKindCatalog.NormalizeKind(entrance.Kind), "cave", StringComparison.Ordinal))
                    continue;
                foreach (var chunk in _loaded)
                {
                    if (!PlacementTouchesChunk(entrance, chunk)) continue;
                    var owner = SitePlacementOwnerKey(chunk, entrance.SiteId);
                    var sitePlacements = new List<OutdoorSurfacePlacementDefinition>();
                    for (var p = 0; p < surface.SitePlacements.Count; p++)
                    {
                        var candidate = surface.SitePlacements[p];
                        if (candidate != null &&
                            string.Equals(candidate.SiteId, entrance.SiteId, StringComparison.Ordinal) &&
                            PlacementTouchesChunk(candidate, chunk))
                            sitePlacements.Add(candidate);
                    }
                    _tileMap.RemoveLayoutInstance(owner);
                    _materializedSitePlacementOwners.Remove(owner);
                    _tileMap.BuildOutdoorPlacementInstance(owner, sitePlacements, _mapper, chunk);
                    _materializedSitePlacementOwners.Add(owner);
                    touched = true;
                }
            }
            return touched;
        }

        bool IsContinuousSiteRelevantToLoadedNeighborhood(
            SimulationWorld world, OutdoorWorldSurfaceDefinition surface, string siteId,
            float personalWorldX, float personalWorldY)
        {
            // The caller first requires the exact personal chunk to be loaded. Site context
            // must never materialize a member who is actually far from this neighborhood.
            if (!_loaded.Contains(_mapper.WorldToChunk(personalWorldX, personalWorldY)))
                return false;
            if (world?.Strategic?.Sites == null ||
                !world.Strategic.Sites.TryGet(siteId, out var site) || site == null)
                return false;
            if (!site.IsRuntimeCreated)
                return IsSiteInLoadedNeighborhood(surface, siteId);
            return site.HasContinuousCore &&
                   string.Equals(site.CoreSurfaceId, _surfaceId, StringComparison.Ordinal) &&
                   _loaded.Contains(_mapper.WorldToChunk(site.CoreWorldX, site.CoreWorldY));
        }

        bool HasSitePlacementInLoadedNeighborhood(
            OutdoorWorldSurfaceDefinition surface,
            string siteId)
        {
            if (surface?.SitePlacements == null || string.IsNullOrEmpty(siteId))
                return false;
            for (var i = 0; i < surface.SitePlacements.Count; i++)
            {
                var placement = surface.SitePlacements[i];
                if (placement == null ||
                    !string.Equals(placement.SiteId, siteId, StringComparison.Ordinal))
                    continue;
                foreach (var chunk in _loaded)
                    if (PlacementTouchesChunk(placement, chunk))
                        return true;
            }
            return false;
        }

        /// <summary>
        /// §4：该 entity 的 SpawnStableKey。GameStart（<c>OpeningSpawnWorldPresenceApplier</c>）已把
        /// authored key 写入 <c>World.OpeningSpawnIdentities</c>，因此可从 spawned Entity 反查。
        /// 缺 key（旧存档／非 OpeningScenario spawn）时退回 authored index 0 —— 与 checked-in
        /// content 的 key 形态一致，但绝不再用 DefinitionId 去匹配别人的 anchor。
        /// </summary>
        static string ResolveOpeningSpawnKey(SimulationWorld world, EntityId id, string definitionId)
        {
            if (world != null && world.OpeningSpawnIdentities.TryGetSpawnKey(id, out var spawnKey) &&
                !string.IsNullOrEmpty(spawnKey))
                return spawnKey;
            return OpeningSpawnIdentityBoard.BuildStableKey(definitionId, 0);
        }

        static void AddDeterministicFallbackOffset(EntityId id, ref float x, ref float y, float surfaceCellSize)
        {
            var ordinal = (int)(id.Value % 23UL) + 1;
            var angle = ordinal * 2.39996322972865332;
            var radius = Math.Max(surfaceCellSize * 2f, surfaceCellSize * (2f + ordinal / 6f));
            x += (float)Math.Cos(angle) * radius;
            y += (float)Math.Sin(angle) * radius;
        }

        /// <summary>
        /// 同一 materialize pass 内不得两点重合：12 名 opening entity 共享同一个 LocationId 时，
        /// baked SitePlace 中心会完全重合，而相同的 PresentationOverride 还会绕过
        /// EntityViewSpawner 的 stack 分散 → 视觉上“只剩一个人”。
        /// 按 EntityId 确定性地推开（纯函数：只依赖 pass 顺序与 id，无随机／时间）。
        /// </summary>
        void SeparateMaterializePoint(EntityId id, ref float px, ref float py)
        {
            var key = QuantizeMaterializePoint(px, py);
            if (!_materializePointUses.TryGetValue(key, out var owner))
            {
                _materializePointUses[key] = id.Value;
                return;
            }

            for (var attempt = 1; attempt <= 32; attempt++)
            {
                var ordinal = (int)(id.Value % 23UL) + 1;
                var angle = ordinal * 2.39996322972865332 + attempt * 0.7f;
                var radius = MaterializeSeparationPresentation * attempt;
                var candidateX = px + (float)Math.Cos(angle) * radius;
                var candidateY = py + (float)Math.Sin(angle) * radius;
                var candidateKey = QuantizeMaterializePoint(candidateX, candidateY);
                if (!_materializePointUses.ContainsKey(candidateKey))
                {
                    px = candidateX;
                    py = candidateY;
                    _materializePointUses[candidateKey] = id.Value;
                    return;
                }
            }

            // 极端拥挤下仍保证可用：保留原位置，但记录占用以便后续实体继续避开。
            _materializePointUses[key] = owner;
        }

        static long QuantizeMaterializePoint(float x, float y)
        {
            const float Quantum = 0.05f;
            var qx = (long)Math.Round(x / Quantum);
            var qy = (long)Math.Round(y / Quantum);
            return qx * 1000003L + qy;
        }

        /// <summary>
        /// §16：非法起点不得直接启动 AI 寻路。落点必须既在 CompositeWalkGrid 内又处于可走格；
        /// blocked 格先就近吸附，grid 外再按确定性同心环寻找可走候选。仍然找不到则记录
        /// <c>ContinuousMaterializationInvalidSpawn</c> 并把这个实体加入 invalid 集合，
        /// 由 <see cref="HostNpcScheduleMover"/> 跳过寻路（不再无限 retry A*）。
        /// </summary>
        void EnforceWalkableMaterializePoint(
            EntityId id,
            string siteId,
            string definitionId,
            ref float px,
            ref float py)
        {
            if (_compositeWalkGrid != null &&
                _compositeWalkGrid.TryWorldToCell(px, py, out var cellX, out var cellY))
            {
                if (_compositeWalkGrid.IsWalkable(cellX, cellY))
                {
                    _invalidSpawnEntityIds.Remove(id.Value);
                    return;
                }

                if (_compositeWalkGrid.TryFindNearestWalkable(
                        cellX, cellY, MaterializeNearestWalkableRadiusCells, out var walkX, out var walkY))
                {
                    _compositeWalkGrid.CellToWorldCenter(walkX, walkY, out px, out py);
                    _invalidSpawnEntityIds.Remove(id.Value);
                    return;
                }
            }

            var spacing = Math.Max(1.5f, surfaceCellSpacingPresentation);
            if (_compositeWalkGrid != null &&
                ContinuousOutdoorOpeningAnchorResolver.TryFindWalkableCandidate(
                    px,
                    py,
                    spacing,
                    64,
                    (cx, cy) => _compositeWalkGrid.TryWorldToCell(cx, cy, out var gx, out var gy) &&
                                _compositeWalkGrid.IsWalkable(gx, gy),
                    out var candidate))
            {
                px = candidate.X;
                py = candidate.Y;
                _invalidSpawnEntityIds.Remove(id.Value);
                return;
            }

            if (_invalidSpawnEntityIds.Add(id.Value))
                Debug.LogWarning(
                    "[ContinuousMaterializationInvalidSpawn] Entity=" + id.Value +
                    " " + definitionId + " Site=" + siteId +
                    " presentation=(" + px.ToString("0.###") + "," + py.ToString("0.###") +
                    ") outside or blocked in CompositeWalkGrid; schedule pathing disabled for this entity until relocated.",
                    this);
        }

        /// <summary>§16：该 NPC 当前起点非法（不得启动日程寻路）。</summary>
        public bool IsEntitySpawnPositionInvalid(EntityId id) =>
            !id.IsNone && _invalidSpawnEntityIds.Contains(id.Value);

        public int InvalidSpawnEntityCount => _invalidSpawnEntityIds.Count;

        void ReleaseContinuousSitePopulation(bool pruneViews = true, bool capturePositions = true)
        {
            // Release the world that created these views, never the replacement snapshot world.
            var world = _navigationStateWorld;
            if (world != null)
            {
                foreach (var id in _continuousSitePopulation)
                {
                    if (capturePositions && !_continuousFormalArmyPopulation.Contains(id) &&
                        !ArmyService.TryGetArmyForCharacter(world, id, out _) &&
                        _bootstrap.ViewSpawner.Registry.TryGet(id, out var view) && view != null)
                        TryCaptureAtSiteAnchor(id, view.transform.position);
                    if (world.Entities.TryGet(id, out var entity) &&
                        entity.TryGet<XianXia.Core.Exploration.EntityLocationComponent>(out var loc))
                        loc.ClearPresentationOverride();
                }
                world.ContinuousOutdoorMaterialization.Clear();
            }
            _continuousSitePopulation.Clear();
            _continuousFormalArmyPopulation.Clear();
            _desiredMaterializedEntities.Clear();
            _sitePopulationScratch.Clear();
            if (pruneViews && _bootstrap?.Session?.IsInitialized == true)
            {
                _bootstrap.Session.RefreshViewableEntityIds();
                _bootstrap.ViewSpawner?.PruneHiddenViews(_bootstrap.Session);
            }
        }

        /// <summary>Deactivate the current Surface presentation.</summary>
        public void DeactivateSurface()
        {
            DeactivatePresentationOnly();
        }

        /// <summary>
        /// Read-only startup preflight used by the NewGame startup transaction. It writes no
        /// runtime field: the caller commits canonical position／clears legacy authority only
        /// after this returns true.
        /// </summary>
        public bool TryPreflightStartupActivation(
            OutdoorWorldSurfaceDefinition surface, SurfaceChunkCoord center, out string failure) =>
            ContinuousOutdoorStartupPlanner.TryPreflightNeighborhood(
                _bootstrap?.Session?.Registry, surface, center, out failure);

        bool TryPreflightNeighborhood(
            OutdoorWorldSurfaceDefinition surface, SurfaceChunkCoord center, out string failure)
        {
            // neighborhood 由 activation/staged transition 按同一规则重算，因此这里不需要预写 _desired。
            return ContinuousOutdoorStartupPlanner.TryPreflightNeighborhood(
                _bootstrap?.Session?.Registry, surface, center, out failure);
        }

        /// <summary>W1C 诊断：当前已 materialize 的 Site population 数量（性能面板用）。</summary>
        public int MaterializedOutdoorEntityCount => _continuousSitePopulation.Count;

        /// <summary>W1C 诊断：Loaded chunk 数量与最近一次 place refresh 代数。</summary>
        public int LoadedPlaceCount { get; private set; }

        void LogActivationFailure(
            OutdoorWorldSurfaceDefinition surface, SurfaceChunkCoord center, string failure)
        {
            var motion = _bootstrap?.Session?.World?.PlayerPartyTravel;
            OutdoorSurfaceChunkDefinition chunk = null;
            if (surface?.Chunks != null)
                for (var i = 0; i < surface.Chunks.Count; i++)
                    if (surface.Chunks[i].Coord == center) { chunk = surface.Chunks[i]; break; }
            Debug.LogError(
                "[ContinuousStartupInvariantFailure] SurfaceId=" + (surface?.SurfaceId ?? string.Empty) +
                " CanonicalWorldPosition=" + (motion != null ? motion.WorldPosition.ToString() : "Missing") +
                " ResolvedChunk=" + center +
                " ChunkExists=" + (chunk != null) +
                " ContinuousDataResolved=false" +
                " MetricValid=" + (surface != null && surface.CellSize > 0f && surface.ChunkWidth > 0f && surface.ChunkHeight > 0f) +
                " Failure=" + failure,
                this);
        }

        public bool TryValidateSurfaceActivationPostconditions(out string failure)
        {
            var failures = new List<string>();
            var session = _bootstrap?.Session;
            var world = session?.World;
            var motion = world?.PlayerPartyTravel;
            if (!IsActive) failures.Add("ContinuousSurfaceRuntime.IsActive=false");
            var acceptanceOnly = false;
            if (!TryResolveSurface(out var surface)) failures.Add("ActiveSurface unresolved");
            else acceptanceOnly = surface.AcceptanceOnly;
            // Acceptance-only surfaces are diagnostic targets (LevelTester W1C Acceptance), not the
            // main playable surface: they carry no "must be the main surface" or opening-site contract.
            if (!acceptanceOnly)
            {
                if (session?.Registry == null || motion == null ||
                    !OutdoorSurfaceCoverageResolver.TryResolveAtWorldPosition(
                        session.Registry, motion.WorldPosition.X, motion.WorldPosition.Y, out var expectedSurface))
                    failures.Add("Main Continuous Surface unresolved from canonical position");
                else if (!string.Equals(expectedSurface.SurfaceId, ActiveSurfaceId, StringComparison.Ordinal))
                    failures.Add("ActiveSurfaceId=" + ActiveSurfaceId + " expected=" + expectedSurface.SurfaceId);
            }
            if (_loaded.Count <= 0) failures.Add("LoadedChunkCount=0");
            if (_compositeWalkGrid == null) failures.Add("CompositeWalkGrid=null");

            var party = session?.PlayerParty;
            EntityView activeView = null;
            if (party == null)
                failures.Add("PlayerParty missing");
            else if (party.ActiveCharacterId.IsNone)
            {
                // TemporarilyUnavailable and AllMembersDead deliberately have no Active identity.
                // Their visible downed/corpse party members are still reconciled normally; an
                // Active EntityView is not a valid startup requirement for either state.
                if (party.ControlState == PlayerPartyControlState.Active)
                    failures.Add("ActiveCharacter identity missing while ControlState=Active");
            }
            else if (world == null || !world.Entities.TryGet(party.ActiveCharacterId, out var activeEntity) ||
                     activeEntity == null)
                failures.Add("ActiveCharacter entity missing: " + party.ActiveCharacterId.Value);
            else if (CombatLifeStateService.ShouldHideFromSpawn(activeEntity))
                failures.Add("ActiveCharacter points to Removed entity: " + party.ActiveCharacterId.Value);
            else if (_bootstrap?.ViewSpawner?.Registry == null ||
                     !_bootstrap.ViewSpawner.Registry.TryGet(party.ActiveCharacterId, out activeView) ||
                     activeView == null)
            {
                LocalMapVisibility.EvaluateContinuousMaterializedVisibility(
                    world, party.ActiveCharacterId, out var visibilityReason);
                failures.Add(
                    "ActiveCharacter EntityView missing: " + party.ActiveCharacterId.Value +
                    " Visibility=" + visibilityReason +
                    " Materialized=" + world.ContinuousOutdoorMaterialization.IsMaterialized(
                        party.ActiveCharacterId));
            }
            else if (_compositeWalkGrid == null || !_compositeWalkGrid.TryWorldToCell(
                         activeView.transform.position.x, activeView.transform.position.y, out _, out _))
                failures.Add("ActiveCharacter outside CompositeWalkGrid");

            failure = string.Join("; ", failures);
            return failures.Count == 0;
        }

        /// <summary>Only the initial New Game opening boundary calls this authored-site census.</summary>
        public bool TryValidateOpeningPostconditions(out string failure)
        {
            var failures = new List<string>();
            var session = _bootstrap?.Session;
            var world = session?.World;
            var motion = world?.PlayerPartyTravel;
            if (!TryResolveSurface(out var surface))
                failures.Add("Opening authored Surface unresolved");
            var openingSiteId = world != null && motion != null
                ? WorldSitePhysicalRegionQuery.ResolveSiteIdOrEmpty(world, motion.WorldPosition)
                : string.Empty;
            if (string.IsNullOrEmpty(openingSiteId))
                failures.Add("Opening authored Site unresolved");
            else if (!world.Strategic.Sites.TryGet(openingSiteId, out var openingSite) ||
                     openingSite == null || openingSite.IsRuntimeCreated)
                failures.Add("Opening Site is not authored: " + openingSiteId);
            else
            {
                if (surface != null && !IsSiteInLoadedNeighborhood(surface, openingSiteId))
                    failures.Add("Opening Site PhysicalRegion not loaded: " + openingSiteId);
                else
                {
                    // A multi-hex PhysicalRegion can be in the current neighborhood while all of
                    // its sparse authored placements are outside it. Only require a materialized
                    // owner when placement geometry actually intersects a loaded chunk.
                    if (HasSitePlacementInLoadedNeighborhood(surface, openingSiteId))
                    {
                        var placementLoaded = false;
                        foreach (var chunkCoord in _loaded)
                            if (_materializedSitePlacementOwners.Contains(
                                    SitePlacementOwnerKey(chunkCoord, openingSiteId)))
                            { placementLoaded = true; break; }
                        if (!placementLoaded)
                            failures.Add("Opening Site baked placements missing: " + openingSiteId);
                    }

                    if (world != null && world.Strategic.Sites.TryGet(openingSiteId, out var site))
                    {
                        // §13：opening population 必须逐个 expected id 验证
                        // （materialized + EntityView），不再以「至少有一个」通过。
                        var expected = new List<EntityId>();
                        StrategicWorldSitePopulationService.CollectCharacterIdsPresentAtWorldSite(
                            world, site, null, expected);
                        var materialized = 0;
                        var views = 0;
                        var missing = new List<string>();
                        var missingIds = new List<EntityId>();
                        var populationComplete = ContinuousOutdoorStartupPlanner.TryCheckPopulationComplete(
                            expected,
                            id => world.ContinuousOutdoorMaterialization.IsMaterialized(id),
                            id => _bootstrap?.ViewSpawner?.Registry != null &&
                                  _bootstrap.ViewSpawner.Registry.TryGet(id, out var probeView) &&
                                  probeView != null,
                            missingIds);
                        for (var i = 0; i < expected.Count; i++)
                        {
                            var id = expected[i];
                            var isMaterialized = world.ContinuousOutdoorMaterialization.IsMaterialized(id);
                            var hasView = _bootstrap?.ViewSpawner?.Registry != null &&
                                          _bootstrap.ViewSpawner.Registry.TryGet(id, out var npcView) &&
                                          npcView != null;
                            if (isMaterialized) materialized++;
                            if (hasView) views++;
                        }

                        for (var i = 0; i < missingIds.Count; i++)
                            missing.Add(DescribeOpeningPopulationMiss(
                                world,
                                missingIds[i],
                                openingSiteId,
                                world.ContinuousOutdoorMaterialization.IsMaterialized(missingIds[i]),
                                _bootstrap?.ViewSpawner?.Registry != null &&
                                _bootstrap.ViewSpawner.Registry.TryGet(missingIds[i], out var missView) &&
                                missView != null));

                        OpeningPopulationExpected = expected.Count;
                        OpeningPopulationMaterialized = materialized;
                        OpeningPopulationViews = views;
                        OpeningPopulationMissing = string.Join(" | ", missing);

                        // §3/§13：EntityView 存在 ≠ 位置合法。逐个验证 view 真的落在
                        // loaded chunks + CompositeWalkGrid + 本 Site（或本 Site baked envelope）。
                        var spatialValid = 0;
                        var spatiallyInvalid = new List<string>();
                        var spatiallyInvalidIds = new List<EntityId>();
                        var spatialComplete = ContinuousOutdoorStartupPlanner.TryCheckPopulationSpatiallyValid(
                            expected,
                            id => IsOpeningEntitySpatiallyValid(world, surface, openingSiteId, id),
                            spatiallyInvalidIds);
                        for (var i = 0; i < expected.Count; i++)
                        {
                            if (!spatiallyInvalidIds.Contains(expected[i]))
                                spatialValid++;
                        }

                        // 详情只在真的有 invalid 时才构造（不刷屏）。
                        for (var i = 0; i < spatiallyInvalidIds.Count; i++)
                            spatiallyInvalid.Add(DescribeOpeningSpatialRow(
                                world, surface, openingSiteId, spatiallyInvalidIds[i], out _));

                        OpeningPopulationSpatialValid = spatialValid;
                        OpeningPopulationSpatialInvalid = string.Join(" | ", spatiallyInvalid);

                        if (expected.Count <= 0)
                            failures.Add("Opening Site expected population=0: " + openingSiteId);
                        else if (!populationComplete)
                            failures.Add(
                                "Opening Site population incomplete: Expected=" + expected.Count +
                                " Materialized=" + materialized + " Views=" + views +
                                " Missing=[" + string.Join(" | ", missing) + "]");
                        else if (!spatialComplete)
                            failures.Add(
                                "Opening Site population spatially invalid: Expected=" + expected.Count +
                                " SpatialValid=" + spatialValid +
                                " SpatialInvalid=[" + string.Join(" | ", spatiallyInvalid) + "]");

                        // §5：bake validation —— 不只是「anchor 存在」。每个 opening spawn 必须存在
                        // 唯一 checked-in anchor，且 shared bake(source local point) ≈ checked-in anchor。
                        // Content 查不到 authored 真源 → Skipped（只在诊断显示，不打断启动）。
                        var bakeOutcome = ContinuousOutdoorOpeningPlacementResolver.ValidateBakedAnchors(
                            world,
                            session.Registry,
                            surface,
                            site,
                            expected,
                            _openingPlacementPlan,
                            out var anchorFailure);
                        OpeningAnchorBakeStatus = bakeOutcome.ToString();
                        OpeningAnchorBakeFailure = anchorFailure;
                        if (bakeOutcome == OpeningAnchorBakeValidationOutcome.Failed)
                            failures.Add("Opening Site baked anchors invalid: " + anchorFailure);
                    }
                }
            }
            failure = string.Join("; ", failures);
            return failures.Count == 0;
        }

        /// <summary>§2/§3/§18：单个 opening entity 的 spatial census 数据。</summary>
        struct OpeningSpatialRow
        {
            public EntityId Id;
            public string Name;
            public string AnchorSource;
            public string Presence;
            public string PresenceWorld;
            public string ViewWorld;
            public string ViewChunk;
            public bool HasView;
            public bool InLoaded;
            public bool InWalkGrid;
            public bool Walkable;
            public bool InsideEnvelope;
            public string ResolvedSite;
            public bool Valid;
        }

        /// <summary>§3：该 opening entity 的 presentation 位置是否真的在本 loaded scope 内。</summary>
        bool IsOpeningEntitySpatiallyValid(
            SimulationWorld world,
            OutdoorWorldSurfaceDefinition surface,
            string expectedSiteId,
            EntityId id) =>
            BuildOpeningSpatialRow(world, surface, expectedSiteId, id).Valid;

        OpeningSpatialRow BuildOpeningSpatialRow(
            SimulationWorld world,
            OutdoorWorldSurfaceDefinition surface,
            string expectedSiteId,
            EntityId id)
        {
            var row = new OpeningSpatialRow
            {
                Id = id,
                Name = string.Empty,
                AnchorSource = "-",
                Presence = "missing",
                PresenceWorld = "-",
                ViewWorld = "-",
                ViewChunk = "-",
                ResolvedSite = "-",
                Valid = false
            };
            var definitionId = string.Empty;
            var locationId = string.Empty;

            if (world != null && world.Entities.TryGet(id, out var entity) && entity != null)
            {
                definitionId = entity.DefinitionId.ToString();
                if (entity.TryGet<IdentityComponent>(out var identity) && identity != null)
                    row.Name = identity.DisplayName;
                if (entity.TryGet<XianXia.Core.Exploration.EntityLocationComponent>(out var loc) && loc != null)
                    locationId = loc.HasLocation ? loc.LocationId : string.Empty;
            }

            if (world?.WorldPresence != null && world.WorldPresence.TryGet(id, out var presenceState) &&
                presenceState != null)
            {
                row.Presence = presenceState.Mode + "(" + presenceState.SiteId + ")";
                row.PresenceWorld = presenceState.HasContinuousWorldPosition
                    ? presenceState.WorldPosX.ToString("F4") + "," + presenceState.WorldPosY.ToString("F4")
                    : "-";
            }

            if (surface != null)
                row.AnchorSource = ContinuousOutdoorOpeningAnchorResolver.ResolveInitialPlacement(
                    surface,
                    expectedSiteId,
                    ResolveOpeningSpawnKey(world, id, definitionId),
                    locationId,
                    false,
                    0f,
                    0f,
                    out _,
                    out _,
                    out _).ToString();

            EntityView view = null;
            if (_bootstrap?.ViewSpawner?.Registry != null)
                _bootstrap.ViewSpawner.Registry.TryGet(id, out view);
            row.HasView = view != null;
            if (!row.HasView || _mapper == null)
                return row;

            var pxv = view.transform.position.x;
            var pyv = view.transform.position.y;
            _mapper.PresentationToWorld(pxv, pyv, out var wx, out var wy);
            row.ViewWorld = wx.ToString("F4") + "," + wy.ToString("F4");
            var chunk = _mapper.WorldToChunk(wx, wy);
            row.ViewChunk = "(" + chunk.X + "," + chunk.Y + ")";
            row.InLoaded = _loaded.Contains(chunk);
            if (_compositeWalkGrid != null &&
                _compositeWalkGrid.TryWorldToCell(pxv, pyv, out var gridX, out var gridY))
            {
                row.InWalkGrid = true;
                row.Walkable = _compositeWalkGrid.IsWalkable(gridX, gridY);
            }
            if (world != null)
                row.ResolvedSite = ResolveCurrentOutdoorSiteId(world, new WorldVec2(wx, wy));
            row.InsideEnvelope = ContinuousOutdoorOpeningAnchorResolver
                .IsInsideSiteBakedEnvelope(surface, expectedSiteId, wx, wy);
            row.Valid = row.InLoaded && row.InWalkGrid && row.Walkable &&
                        !IsEntitySpawnPositionInvalid(id) &&
                        (string.Equals(row.ResolvedSite, expectedSiteId, StringComparison.Ordinal) ||
                         row.InsideEnvelope);
            return row;
        }

        string DescribeOpeningSpatialRow(
            SimulationWorld world,
            OutdoorWorldSurfaceDefinition surface,
            string expectedSiteId,
            EntityId id,
            out bool valid)
        {
            var row = BuildOpeningSpatialRow(world, surface, expectedSiteId, id);
            valid = row.Valid;
            return "Entity=" + row.Id.Value + " " + row.Name + " AnchorSource=" + row.AnchorSource +
                   " PresenceWorld=" + row.PresenceWorld + " ViewWorld=" + row.ViewWorld +
                   " ViewChunk=" + row.ViewChunk + " InLoaded=" + row.InLoaded +
                   " InWalkGrid=" + row.InWalkGrid + " Walkable=" + row.Walkable +
                   " ResolvedSite=" + row.ResolvedSite +
                   " Presence=" + row.Presence + " ExpectedSite=" + expectedSiteId;
        }

        /// <summary>§11：只在 startup invariant 失败时调用一次；逐实体说明为何没出现。</summary>
        string DescribeOpeningPopulationMiss(
            SimulationWorld world,
            EntityId id,
            string expectedSiteId,
            bool materialized,
            bool hasView)
        {
            var name = string.Empty;
            var tags = string.Empty;
            var locationId = string.Empty;
            var presence = "missing";
            var siteLoaded = false;
            if (world != null && world.Entities.TryGet(id, out var entity) && entity != null)
            {
                tags = entity.Tags.ToString();
                name = entity.TryGet<IdentityComponent>(out var identity) && identity != null
                    ? identity.DisplayName
                    : string.Empty;
                if (entity.TryGet<XianXia.Core.Exploration.EntityLocationComponent>(out var loc) && loc != null)
                    locationId = loc.HasLocation ? loc.LocationId : string.Empty;
            }

            if (world?.WorldPresence != null && world.WorldPresence.TryGet(id, out var presenceState) && presenceState != null)
                presence = presenceState.Mode + "(" + presenceState.SiteId + ")";
            if (TryResolveSurface(out var surface))
                siteLoaded = IsSiteInLoadedNeighborhood(surface, expectedSiteId);

            return "Entity=" + id.Value + " Name=" + name + " Tags=" + tags +
                   " LocationId=" + locationId + " WorldPresence=" + presence +
                   " ExpectedSite=" + expectedSiteId + " ContinuousSiteLoaded=" + siteLoaded +
                   " Materialized=" + materialized + " EntityView=" + hasView;
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
                if (PlayerPartyTransitionMembership.ShouldMemberTransitionWithParty(
                        _bootstrap.Session.World, party, member) &&
                    registry.TryGet(member, out var view) && view != null)
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
