using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using XianXia.Core.Actions;
using XianXia.Core.Content;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Input;
using XianXia.Core.Navigation;
using XianXia.Core.Persistence;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;
using XianXia.Core.World.Surface;
using XianXia.Data.Content;

namespace XianXia.Unity.Host
{
    /// <summary>Active character camera follow policy (final Phase 1 rule).</summary>
    public enum HostActiveCameraFollowMode
    {
        /// <summary>Free look; middle-mouse pan owns the camera. RTS path never engages follow.</summary>
        Free = 0,
        /// <summary>WASD Direct Movement only: snap + continuous Hard Follow.</summary>
        WasdHardFollow = 1
    }

    /// <summary>
    /// Phase 1: PlayerParty follow AI, Active WASD, camera Hard Follow on WASD only.
    /// RTS / click path movement is fully decoupled from Camera.
    /// </summary>
    public sealed class HostPlayerPartyController : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] float followStopDistance = 2.2f;
        [SerializeField] float followRepathInterval = 0.35f;
        [SerializeField] float followerSpreadRadius = 1.1f;
        [SerializeField] float wasdMoveSpeed = 5.5f;
        [SerializeField] bool enableCameraFollow = true;
        [FormerlySerializedAs("localVisibleAutoTravelFollowLerp")]
        [SerializeField] float surfaceAutoTravelFollowLerp = 5f;

        bool _surfaceTravelExecutionArmed;
        bool _resumeSurfaceTravelRequested;
        float _surfaceTravelRetryCooldownUntil;
        const int SurfaceRouteLookaheadCount = 64;
        readonly List<float> _surfaceRouteCandidateXy = new List<float>(SurfaceRouteLookaheadCount * 2);
        int _currentSurfaceExecutionTargetIndex = -1;
        bool _continuousAutoTravelStallReported;
        int _continuousSubgoalRouteVersion = -1;
        int _continuousSubgoalRouteIndex = -1;
        int _continuousSubgoalNavigationGeneration = -1;
        int _continuousSubgoalGridRevision = -1;
        Vector3 _continuousSubgoalDesired;
        Vector3 _continuousResolvedSubgoal;
        bool _hasContinuousSubgoalResolution;
        bool _continuousSubgoalResolutionSucceeded;
        ContinuousWalkGridSubgoalKind _continuousSubgoalKind;
        string _continuousWaitingReason = string.Empty;
        int _continuousIssuedRouteVersion = -1;
        Vector3 _continuousIssuedSubgoal;
        bool _hasContinuousIssuedSubgoal;
        int _continuousSubgoalIssueCount;
        string _continuousLastIssueReason = string.Empty;
        Vector3 _continuousProgressAnchor;
        WorldVec2 _continuousProgressWorldAnchor;
        float _continuousProgressWindowStartedAt = -1f;
        int _continuousProgressRouteVersion = -1;
        Vector3 _continuousProgressSubgoal;
        // Surface travel diagnostics read by HostHudSnapshot.
        public static string LastTransitionStatus = "Idle";
        public static string LastTransitionFailureReason = string.Empty;
        public static int SurfaceLocalExecutionTargetIndex = -1;
        public static string SurfaceLocalSubgoal = "-";
        public static string SurfaceLocalSubgoalKind = "-";
        public static string SurfaceWaitingReason = string.Empty;
        public static int SurfaceHostPathIndex = -1;
        public static int SurfaceHostPathCount;
        public static int SurfaceCompositeGridRevision = -1;
        public static int SurfaceNavigationGeneration = -1;
        public static string SurfaceCurrentChunk = "-";

        // Phase 5C-W1 (2nd pass): middle-mouse pan detaches camera until a NEW AutoTravel session.
        bool _cameraDetachedByPlayer;
        bool _hasAutoTravelSession;

        readonly Dictionary<ulong, float> _nextFollowRepath = new Dictionary<ulong, float>();
        readonly Dictionary<ulong, HostPartySharedActivity> _followerSharedActivity =
            new Dictionary<ulong, HostPartySharedActivity>();
        HostPartySharedActivity _lastActiveSharedActivity = HostPartySharedActivity.FollowIdle;
        bool _wasdHeldLastFrame;
        bool _pendingSnapshotFollowRebind;
        HostActiveCameraFollowMode _cameraMode = HostActiveCameraFollowMode.Free;
        EntityId _observedActiveCharacterId = EntityId.None;
        bool _wasNeedingExternalHandoff;
        float _nextExternalHandoffRetryAt;
        bool _hasPendingExternalHandoffPresentation;
        PlayerFactionControlHandoffResult _pendingExternalHandoffPresentation;
        const float ExternalHandoffRetryIntervalSeconds = .75f;

        [SerializeField] float followerChopSearchRadius = 10f;

        HostMoveController _move;
        HostCommandBridge _commands;
        HostNpcMeleeAssault _melee;
        HostFarmFieldLabor _farm;
        HostDestructibleAssault _chop;
        HostWorkLoop _workLoop;
        PlayableHostCameraRig _cameraRig;
        EntityViewSpawner _spawner;

        public PlayerPartyRuntime Party =>
            bootstrap != null && bootstrap.Session != null
                ? bootstrap.Session.PlayerParty
                : null;

        public EntityId ActiveCharacterId =>
            Party != null ? Party.ActiveCharacterId : EntityId.None;

        public void Bind(PlayableHostBootstrap host)
        {
            bootstrap = host;
            if (host == null)
                return;

            _move = host.MoveController;
            _commands = host.CommandBridge;
            _melee = host.GetComponent<HostNpcMeleeAssault>();
            _farm = host.GetComponent<HostFarmFieldLabor>();
            _chop = host.GetComponent<HostDestructibleAssault>();
            _workLoop = host.GetComponent<HostWorkLoop>();
            _cameraRig = host.GetComponent<PlayableHostCameraRig>();
            _spawner = host.ViewSpawner;
            _observedActiveCharacterId = Party != null ? Party.ActiveCharacterId : EntityId.None;
            _wasNeedingExternalHandoff = Party?.NeedsExternalControlHandoff == true;
            _nextExternalHandoffRetryAt = 0f;
            _hasPendingExternalHandoffPresentation = false;
            _pendingExternalHandoffPresentation = default;
        }

        public bool TryFollowActive(EntityId candidate, out string error)
        {
            error = null;
            var session = bootstrap?.Session;
            if (session == null || !session.IsInitialized || Party == null)
            {
                error = "Session not ready.";
                return false;
            }

            if (!Party.TryAddMember(session.World, session.CharacterIds, candidate, out error))
                return false;

            NotifyMemberJoined(candidate);
            return true;
        }

        public void NotifyMemberJoined(EntityId candidate)
        {
            var world = bootstrap.Session.World;
            if (Party?.IsMember(candidate) != true) return;
            BackgroundCharacterTravelService.CancelTravelIfAny(world, candidate);
            if (PlayerPartyLocalCoPresenceQuery.IsContinuousOutdoorPresentationScope(world))
            {
                // §10：Continuous Outdoor 不是 LocalMap —— 不写 LocalMap occupant。
                // §11：立刻把新 follower 的 Domain presence 同步为当前 Party continuous travel
                // authority（否则 Party.Members 有他、presence 还是 AtSite(荒村) = split authority），
                // 并把新 follower 纳入 traveling members。
                // 不 teleport：view 保持当前 presentation 位置，随后 OrderFollowerTowardActive
                // 走 formation slot → realtime follow。
                PlayerPartyTransitionMembership.SyncMemberPresenceFromMotion(world, candidate);
                PlayerPartyTransitionMembership.CaptureTravelingMembersForPartyTransition(world, Party);
            }
            else
            {
                // Interior／Cave：保留 Separate Space occupant 语义。
                world.LocalMap.AddOccupant(candidate);
            }

            _nextFollowRepath.Remove(candidate.Value);
            OrderFollowerTowardActive(candidate);
            bootstrap.NotifyOutdoorEntityScopeChanged();
            bootstrap.FlushLoadedDestinationArrivals();
        }

        bool CanProcessCompanionDeparture()
        {
            var session = bootstrap?.Session;
            return session != null && session.IsInitialized && !session.InitialBootstrapPending &&
                session.PendingRestoredStrategicSnapshot == null && !session.ModalHardPaused &&
                !HostInputGate.BlockWorldInteraction && bootstrap.GetComponent<HostDialoguePresenter>()?.IsActive != true &&
                bootstrap.ContinuousOutdoorSurfaceRuntime?.IsTransitioning != true && _melee?.IsFighting != true;
        }

        void TickQuestCompanions()
        {
            var world = bootstrap.Session.World;
            QuestCompanionService.Reconcile(world);
            if (!CanProcessCompanionDeparture()) return;
            var ids = new List<EntityId>(world.QuestCompanions.Bindings.Keys);
            ids.Sort((a,b) => a.Value.CompareTo(b.Value));
            foreach (var id in ids)
            {
                if (!QuestCompanionService.CanDepart(world,id)) continue;
                if (Party.ActiveCharacterId == id && !TrySwitchActive(QuestCompanionService.DepartureSuccessor(world,id),out _)) continue;
                TryDepartQuestCompanion(id,out _);
            }
        }

        public bool TryStopFollow(EntityId id, out string error)
        {
            error = null;
            var world = bootstrap?.Session?.World;
            if (!PlayerPartyRuntime.CanPlayerControlCharacter(world, id))
            { error = "该角色不可手动解除同行；任务临时同行者只在任务结束后安全离队。"; return false; }
            return RemoveFollowerPreservingPosition(id, out error);
        }

        // Only the Quest lifecycle pump calls this; never exposed as a player command.
        bool TryDepartQuestCompanion(EntityId id, out string error)
        {
            error = null;
            if (!CanProcessCompanionDeparture() || !QuestCompanionService.CanDepart(bootstrap.Session.World, id))
            { error = "任务同行者尚未满足安全离队条件。"; return false; }
            return RemoveFollowerPreservingPosition(id, out error);
        }

        bool RemoveFollowerPreservingPosition(EntityId id, out string error)
        {
            error = null;
            if (Party == null || !Party.TryRemoveMember(id, out error))
                return false;

            _nextFollowRepath.Remove(id.Value);
            _followerSharedActivity.Remove(id.Value);
            StopFollowerPartyDerivedWork(id);
            StopFollowerDirectControl(id);

            var world = bootstrap?.Session?.World;
            if (world != null &&
                PlayerPartyLocalCoPresenceQuery.IsContinuousOutdoorPresentationScope(world))
            {
                // §12：Continuous 没有 LocalMap occupant 语义，也不得因为解除 membership 丢位置。
                // 保留 follower 当前 precise Continuous WorldPosition（优先取实际 view 的落点，
                // 其次取已有 precise anchor），恢复为普通独立角色 world presence。
                var captured = false;
                if (_spawner != null && _spawner.Registry.TryGet(id, out var view) && view != null)
                {
                    var surface = bootstrap.ContinuousOutdoorSurfaceRuntime;
                    if (surface != null &&
                        surface.PresentationToWorld(
                            view.transform.position.x, view.transform.position.y, out var wx, out var wy))
                    {
                        PlayerPartyTransitionMembership.SyncIndependentCharacterPresenceFromPosition(
                            world, id, new WorldVec2(wx, wy), surface.ActiveSurfaceId);
                        // 让 Domain presentation 与实际 view 一致：否则下一次 materialize reconcile 的
                        // RealignMaterializedViewPlacements 会把 view 拉回 follow 之前的过期 override
                        // （= 位置被回退）。这里写的就是当前 view 位置，因此是 no-op 对齐。
                        if (world.Entities.TryGet(id, out var stoppedEntity) && stoppedEntity != null &&
                            stoppedEntity.TryGet<XianXia.Core.Exploration.EntityLocationComponent>(out var stoppedLoc) &&
                            stoppedLoc != null)
                            stoppedLoc.SetPresentationOverride(
                                view.transform.position.x, view.transform.position.y);
                        captured = true;
                    }
                }

                if (!captured &&
                    world.WorldPresence.TryGet(id, out var presence) && presence != null &&
                    presence.HasContinuousWorldPosition)
                {
                    PlayerPartyTransitionMembership.SyncIndependentCharacterPresenceFromPosition(
                        world, id, presence.ContinuousWorldPosition, presence.PersonalSurfaceId);
                }

                world.LocalMap.RemoveOccupant(id);
                PlayerPartyTransitionMembership.CaptureTravelingMembersForPartyTransition(world, Party);
            }

            if (world != null) QuestCompanionService.FinishDeparture(world,id);
            bootstrap?.NotifyOutdoorEntityScopeChanged();
            bootstrap?.FlushLoadedDestinationArrivals();
            return true;
        }

        public bool TrySwitchActive(EntityId newActive, out string error)
        {
            error = null;
            var session = bootstrap?.Session;
            if (session == null || Party == null)
            {
                error = "Session not ready.";
                return false;
            }

            var oldActive = Party.ActiveCharacterId;
            if (oldActive == newActive)
                return true;

            var continuousCombat = session.World.Strategic?.ContinuousManualCombat;
            if (continuousCombat != null && continuousCombat.IsActive &&
                !continuousCombat.IsFriendly(newActive))
            {
                error = "该角色未参加当前战斗。";
                return false;
            }

            if (!Party.TrySetActive(session.World, newActive, out error))
                return false;

            ClearDirectControlFor(oldActive);
            _cameraMode = HostActiveCameraFollowMode.Free;
            // One-shot focus on new Active; does not enter permanent follow.
            FrameCameraOn(newActive);
            if (bootstrap?.SelectionController != null)
                bootstrap.SelectionController.SelectEntity(newActive, false);
            return true;
        }

        /// <summary>
        /// Snapshot Load / Active switch：一次性对准 Active Presentation，不进入 WASD Hard Follow。
        /// </summary>
        public void SnapCameraToActiveOnce()
        {
            if (Party == null || !Party.HasActive)
                return;

            _cameraMode = HostActiveCameraFollowMode.Free;
            SnapCameraTo(Party.ActiveCharacterId);
        }

        /// <summary>One-shot tactical framing; does not enable persistent follow.</summary>
        public void SnapCameraToEntityOnce(EntityId id)
        {
            _cameraMode = HostActiveCameraFollowMode.Free;
            SnapCameraTo(id);
        }

        /// <summary>Separate Space materialize 后重绑本地图移动与队伍跟随。</summary>
        public void OnLocalMapMaterialized(string localMapId)
        {
            if (Party == null)
                return;

            var mapId = localMapId?.Trim() ?? string.Empty;
            _move?.BindLocalMapContext(mapId);
            _move?.InvalidatePartyLocalMovement(Party.Members);
            InvalidatePartyDerivedLocalActions();
            ResetFollowAfterMaterialize();
        }

        /// <summary>
        /// WorldMap 打开时冻结当前 Presentation A*，但不改 Domain TravelPlan、Segment、
        /// Destination 或 canonical WorldPosition。
        /// </summary>
        public void FreezeSurfaceTravelForPlanning()
        {
            _resumeSurfaceTravelRequested = false;
            _surfaceTravelExecutionArmed = false;
            ResetSurfaceAutoTravelTracking();
            var active = Party != null ? Party.ActiveCharacterId : EntityId.None;
            if (!active.IsNone)
                _move?.CancelPresentationMovementPublic(active);
        }

        /// <summary>
        /// WorldMap 关闭后显式请求重发当前 Surface route subgoal。只清 modern Host transient，
        /// 不再次 Cancel Presentation movement，也不改 Domain TravelPlan。
        /// </summary>
        public void ResumeSurfaceTravelAfterPlanning()
        {
            _resumeSurfaceTravelRequested = true;
            ResetSurfaceAutoTravelTracking(preserveResumeRequest: true);
        }

        /// <summary>
        /// Snapshot 用新 SimulationWorld 替换 Session 后清理旧 Host 会话瞬态。
        /// 不写 Domain motion / PartyWorld / 路线；最终 LocalMap 落点仍由 materialize 流程建立。
        /// </summary>
        public void ResetTransientStateAfterSnapshotRestore()
        {
            _surfaceTravelExecutionArmed = false;
            ResetSurfaceAutoTravelTracking();
            _wasdHeldLastFrame = false;
            _pendingSnapshotFollowRebind = false;
            _cameraDetachedByPlayer = false;
            _hasAutoTravelSession = false;
            _cameraMode = HostActiveCameraFollowMode.Free;
            _nextFollowRepath.Clear();
            _followerSharedActivity.Clear();
            _lastActiveSharedActivity = HostPartySharedActivity.FollowIdle;
            _observedActiveCharacterId = Party != null ? Party.ActiveCharacterId : EntityId.None;
        }

        void InvalidatePartyDerivedLocalActions()
        {
            _lastActiveSharedActivity = HostPartySharedActivity.FollowIdle;
            ClearFollowerPartyDerivedWork();
        }

        void ResetFollowAfterMaterialize()
        {
            _nextFollowRepath.Clear();
            if (Party == null || _spawner == null)
                return;

            if (LoadedLocalMapPlacementSnapshotRestore.DeferFollowRebind)
            {
                _pendingSnapshotFollowRebind = true;
                return;
            }

            RebindAllFollowers();
        }

        void RebindAllFollowers()
        {
            if (CharacterEncounterService.BlocksOrdinaryContinuousSurface(
                    bootstrap?.Session?.World)) return;
            if (Party == null || _spawner == null)
                return;

            // Follower slot 按 Party.Members 稳定顺序分配（与 TickFollowers 同一 convention）。
            // 修复：旧实现把每个 follower 拉到 Active exact point（root cause 2 —— materialize 后
            // 所有人重新叠回 Active）。goal = Active + FollowerOffset(followerIndex)。
            var followerIndex = 0;
            for (var i = 0; i < Party.Members.Count; i++)
            {
                var id = Party.Members[i];
                if (Party.IsActive(id))
                    continue;
                if (!PlayerPartyTransitionMembership.ShouldMemberTransitionWithParty(
                        bootstrap.Session.World, Party, id))
                    continue;
                OrderFollowerTowardActive(id, followerIndex);
                followerIndex++;
            }
        }

        public void ClearDirectControlFor(EntityId id)
        {
            if (id.IsNone)
                return;

            _move?.CancelPresentationMovementPublic(id);
            _workLoop?.StopLoop(id);
            _farm?.Stop(id);
            _melee?.StopAutomatic(id);
            if (_commands != null && bootstrap?.Session != null)
                _commands.IssueOne(id, PlayerCommandKind.Stop, 0);
        }

        void Update()
        {
            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized || Party == null)
                return;

            RefreshActiveControlAfterLifeStateChange(requestImmediateExternalHandoffRetry: false);
            TickQuestCompanions();
            var encounter = bootstrap.Session.World.Strategic.CharacterEncounter;
            if (encounter != null)
            {
                _pendingSnapshotFollowRebind = false;
                if ((encounter.Phase == CharacterEncounterPhase.Active ||
                     encounter.Phase == CharacterEncounterPhase.ReadyToEnd) && Party.HasActive)
                    TickWasdForActive();
                if (encounter.Phase == CharacterEncounterPhase.ReadyToEnd)
                    TickReadyToEndEncounterFollowers(encounter);
                return; // Encounter owns followers and position; no ordinary travel/anchor synchronization.
            }
            SquadCommandService.SetExecution(bootstrap.Session.World, Party.ControlledSquadId,
                Party.ActiveCharacterId.IsNone ? SquadCommandKind.None : SquadCommandKind.FollowLeader,
                Party.ActiveCharacterId);
            if (Party.ActiveCharacterId.IsNone)
                return;

            if (_pendingSnapshotFollowRebind)
            {
                _pendingSnapshotFollowRebind = false;
                RebindAllFollowers();
            }

            TickWasdForActive();
            TickPartyDerivedGroupActivity();
            TickFollowers();
            TickCombatFollow();
            TickContinuousSurfaceAutoTravelMovement();
        }

        public void RefreshActiveControlAfterLifeStateChange(
            bool requestImmediateExternalHandoffRetry = true)
        {
            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized || Party == null)
                return;
            var previousActive = _observedActiveCharacterId;
            PlayerPartyLifeStateMembershipService.ReconcilePlayerPartyAfterLifeStateChange(
                bootstrap.Session.World);
            if (_hasPendingExternalHandoffPresentation)
            {
                if (Party.ActiveCharacterId != _pendingExternalHandoffPresentation.SuccessorId)
                {
                    _hasPendingExternalHandoffPresentation = false;
                    _pendingExternalHandoffPresentation = default;
                }
                else
                {
                    if (!PrepareExternalHandoffPresentation(
                            _pendingExternalHandoffPresentation))
                        return;
                    _hasPendingExternalHandoffPresentation = false;
                    _pendingExternalHandoffPresentation = default;
                    ApplyAutomaticActiveChange(previousActive, Party.ActiveCharacterId);
                    _observedActiveCharacterId = Party.ActiveCharacterId;
                    previousActive = _observedActiveCharacterId;
                }
            }
            var now = Time.unscaledTime;
            var shouldRetryExternalHandoff = Party.NeedsExternalControlHandoff &&
                !IsExternalHandoffBlockedByEncounterPresentation() &&
                (requestImmediateExternalHandoffRetry || !_wasNeedingExternalHandoff ||
                 now >= _nextExternalHandoffRetryAt);
            if (shouldRetryExternalHandoff)
            {
                // Core handoff intentionally does not know about EntityViews. Freeze persistent
                // cave placements while the old Separate Space still owns them.
                if (bootstrap.Session.World.LocalMap.IsActive)
                    HostSnapshotLocalPlacementCaptureSync
                        .FlushActiveSeparateSpaceCharacterPlacementsFromViews(bootstrap);
                var handoff = PlayerFactionControlHandoffService.TryResolve(
                    bootstrap.Session.World, Party);
                if (handoff.IsResolved)
                {
                    if (!PrepareExternalHandoffPresentation(handoff))
                    {
                        _hasPendingExternalHandoffPresentation = true;
                        _pendingExternalHandoffPresentation = handoff;
                        _wasNeedingExternalHandoff = Party.NeedsExternalControlHandoff;
                        _nextExternalHandoffRetryAt = now + ExternalHandoffRetryIntervalSeconds;
                        return;
                    }
                }
                _nextExternalHandoffRetryAt = now + ExternalHandoffRetryIntervalSeconds;
            }
            if (!Party.NeedsExternalControlHandoff)
                _nextExternalHandoffRetryAt = 0f;
            _wasNeedingExternalHandoff = Party.NeedsExternalControlHandoff;
            var currentActive = Party.ActiveCharacterId;
            if (previousActive != currentActive)
                ApplyAutomaticActiveChange(previousActive, currentActive);
            _observedActiveCharacterId = currentActive;
        }

        /// <summary>
        /// Snapshot v8 already restores every external handoff authority. This retry runs only after
        /// Content, squads, exact presence and Surface registrations have all been rehydrated;
        /// presentation is rebuilt by the caller immediately afterwards.
        /// </summary>
        public bool TryResolveExternalHandoffAfterWorldShellRestore()
        {
            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized || Party == null)
                return false;
            PlayerPartyLifeStateMembershipService.ReconcilePlayerPartyAfterLifeStateChange(
                bootstrap.Session.World);
            if (!Party.NeedsExternalControlHandoff || IsExternalHandoffBlockedByEncounterPresentation())
                return false;
            var result = PlayerFactionControlHandoffService.TryResolve(
                bootstrap.Session.World, Party);
            _wasNeedingExternalHandoff = Party.NeedsExternalControlHandoff;
            _nextExternalHandoffRetryAt = result.IsResolved
                ? 0f
                : Time.unscaledTime + ExternalHandoffRetryIntervalSeconds;
            _observedActiveCharacterId = Party.ActiveCharacterId;
            return result.IsResolved;
        }

        bool IsExternalHandoffBlockedByEncounterPresentation()
        {
            var encounterHost = bootstrap != null
                ? bootstrap.GetComponent<HostCharacterEncounter>()
                : null;
            return encounterHost != null && encounterHost.BlocksExternalControlHandoff;
        }

        bool PrepareExternalHandoffPresentation(PlayerFactionControlHandoffResult handoff)
        {
            var surface = bootstrap?.ContinuousOutdoorSurfaceRuntime;
            var session = bootstrap?.Session;
            if (surface == null || session?.World == null || bootstrap.ViewSpawner == null ||
                !handoff.IsResolved || handoff.SuccessorId.IsNone)
                return false;

            if (RequiresExternalHandoffHardReanchor(
                    surface.IsActive,
                    surface.IsWorldPositionInLoadedNeighborhood(
                        handoff.SurfaceId, handoff.WorldPosition)))
                surface.DeactivateForSuccessionReanchor();

            if (!surface.IsActive)
                if (!surface.TryActivateAtCurrentWorldPosition())
                    return false;

            // This is the single destination-presentation barrier for external handoff. Force the
            // complete current neighborhood/site/squad/population reconcile and view refresh
            // synchronously before camera or selection may observe the new Active identity.
            var generationBefore = surface.EntityReconcileGeneration;
            surface.ReconcileOutdoorEntityMaterializationForScopeChange();
            bootstrap.NotifyOutdoorEntityScopeChanged();
            bootstrap.FlushLoadedDestinationArrivals();
            surface.SyncPartyPresentation();

            var world = session.World;
            var motion = world.PlayerPartyTravel;
            var failure = string.Empty;
            var hasSuccessorView = bootstrap.ViewSpawner.Registry.TryGet(
                handoff.SuccessorId, out var successorView) && successorView != null;
            var surfaceInvariant = surface.TryValidateSurfaceActivationPostconditions(
                out failure);
            var ready = ExternalHandoffPresentationPostconditionsMet(
                handoff.SuccessorId,
                session.PlayerParty.ActiveCharacterId,
                handoff.SurfaceId,
                surface.ActiveSurfaceId,
                handoff.WorldPosition,
                motion != null ? motion.WorldPosition : default,
                surface.IsActive,
                motion != null && motion.HasPosition,
                motion != null && string.Equals(motion.SurfaceId, handoff.SurfaceId,
                    System.StringComparison.Ordinal),
                surface.IsWorldPositionInLoadedNeighborhood(
                    handoff.SurfaceId, handoff.WorldPosition),
                surface.EntityReconcileGeneration > generationBefore,
                world.ContinuousOutdoorMaterialization.IsMaterialized(handoff.SuccessorId),
                hasSuccessorView,
                surfaceInvariant);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!ready)
                Debug.LogError(
                    "[ExternalControlHandoffPresentationNotReady] Successor=" +
                    handoff.SuccessorId.Value + " Surface=" + handoff.SurfaceId +
                    " Failure=" + (failure ?? "postcondition"), this);
#endif
            return ready;
        }

        public static bool RequiresExternalHandoffHardReanchor(
            bool surfaceActive,
            bool destinationInLoadedNeighborhood) =>
            surfaceActive && !destinationInLoadedNeighborhood;

        public static bool ExternalHandoffPresentationPostconditionsMet(
            EntityId successor,
            EntityId active,
            string expectedSurfaceId,
            string activeSurfaceId,
            WorldVec2 expectedPosition,
            WorldVec2 actualPosition,
            bool surfaceActive,
            bool motionHasPosition,
            bool motionUsesExpectedSurface,
            bool positionLoaded,
            bool neighborhoodReconciled,
            bool successorMaterialized,
            bool successorViewExists,
            bool surfaceInvariant) =>
            !successor.IsNone && successor == active && surfaceActive &&
            string.Equals(activeSurfaceId, expectedSurfaceId,
                System.StringComparison.Ordinal) &&
            motionHasPosition && motionUsesExpectedSurface &&
            Mathf.Abs(actualPosition.X - expectedPosition.X) < .0001f &&
            Mathf.Abs(actualPosition.Y - expectedPosition.Y) < .0001f &&
            positionLoaded && neighborhoodReconciled && successorMaterialized &&
            successorViewExists && surfaceInvariant;

        void ApplyAutomaticActiveChange(EntityId previousActive, EntityId currentActive)
        {
            if (!previousActive.IsNone)
                ClearDirectControlFor(previousActive);
            _cameraMode = HostActiveCameraFollowMode.Free;
            _wasdHeldLastFrame = false;

            if (currentActive.IsNone)
                return;

            _pendingSnapshotFollowRebind = true;
            FrameCameraOn(currentActive);
            bootstrap?.SelectionController?.SelectEntity(currentActive, false);
        }

        void TickWasdForActive()
        {
            if (HostInputGate.BlockWorldInteraction)
                return;

            var active = Party.ActiveCharacterId;
            if (active.IsNone || _move == null)
                return;

            if (!bootstrap.Session.World.Entities.TryGet(active, out var ent) ||
                !CombatLifeStateService.CanFight(ent))
                return;

            var dir = ReadWasdDirection();
            var wasdHeld = dir.sqrMagnitude > 0.01f;
            if (wasdHeld)
            {
                if (!_wasdHeldLastFrame)
                {
                    CancelSurfaceAutoTravelForPlayerInterrupt();
                    // Cancel RTS/Click path; Camera snaps + Hard Follow (CAMERA-E).
                    _move.CancelPresentationMovementPublic(active);
                    EnterWasdHardFollow(active);
                }

                // Crossing Intent 必须在 WalkGrid Clamp 之前处理，否则永远无法 OutOfBounds。
                if (TryConsumeSurfaceEdgeCrossingFromMovement(active, dir, wasdMoveSpeed))
                {
                    _wasdHeldLastFrame = true;
                    return;
                }

                _move.TickDirectWasdMove(active, dir, wasdMoveSpeed);
            }
            else if (_wasdHeldLastFrame && _cameraMode == HostActiveCameraFollowMode.WasdHardFollow)
            {
                _cameraMode = HostActiveCameraFollowMode.Free;
            }

            _wasdHeldLastFrame = wasdHeld;
        }

        /// <summary>
        /// WASD 本帧位移若试图跨出 LocalMap playable bounds，先 Resolve Neighbor 再 Transition。
        /// 邻格非法时返回 false，交由 WalkGrid Clamp 挡住。
        /// </summary>
        bool TryConsumeSurfaceEdgeCrossingFromMovement(EntityId active, Vector2 dir, float speed)
        {
            var session = bootstrap?.Session;
            var world = session?.World;
            var party = Party;
            if (world == null || party == null || active.IsNone)
                return false;
            if (bootstrap?.ContinuousOutdoorSurfaceRuntime?.IsActive == true)
            {
                if (_spawner != null && _spawner.Registry.TryGet(active, out var continuousView) && continuousView != null)
                {
                    var continuousDeltaTime = Time.deltaTime > 0f ? Time.deltaTime : Time.unscaledDeltaTime;
                    return bootstrap.ContinuousOutdoorSurfaceRuntime.TryStepAcrossCoverageBoundary(
                        continuousView.transform.position + new Vector3(dir.x, dir.y, 0f) * speed * continuousDeltaTime);
                }
                return false;
            }
            return false;
        }

        void LateUpdate()
        {
            // After CameraRig middle-pan so WASD Hard Follow wins the same frame.
            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized || Party == null)
                return;
            if (Party.IsAwaitingSuccession || Party.ActiveCharacterId.IsNone)
                return;

            // 所有本帧移动完成后，把当前 Surface 表现同步回精确世界位置。
            if (bootstrap.WorldMapPanel == null || !bootstrap.WorldMapPanel.IsOpen)
                bootstrap.ContinuousOutdoorSurfaceRuntime?.SyncPartyPresentation();
            TickCameraFollow();
        }

        void TickContinuousSurfaceAutoTravelMovement()
        {
            if (HostInputGate.BlockWorldInteraction)
                return;
            var world = bootstrap?.Session?.World;
            var party = Party;
            var motion = world?.PlayerPartyTravel;
            var surface = bootstrap?.ContinuousOutdoorSurfaceRuntime;
            if (world == null || party == null || !party.HasActive ||
                !PlayerPartySurfaceTravelService.IsActiveSurfaceTravel(motion))
            {
                if (_surfaceTravelExecutionArmed)
                {
                    _surfaceTravelExecutionArmed = false;
                    ResetSurfaceAutoTravelTracking();
                }
                return;
            }
            if (surface == null || !surface.IsActive)
            {
                _continuousWaitingReason = "SurfacePresentationUnavailable";
                SurfaceWaitingReason = _continuousWaitingReason;
                LastTransitionStatus = _continuousWaitingReason;
                return;
            }

            if (_resumeSurfaceTravelRequested)
                ArmSurfaceTravelExecution(cancelPresentationMovement: false);
            else if (!_surfaceTravelExecutionArmed)
                ArmSurfaceTravelExecution(cancelPresentationMovement: true);

            if (Time.time < _surfaceTravelRetryCooldownUntil)
            {
                _continuousWaitingReason = "RetryCooldown";
                PublishSurfaceTravelDiagnostics(motion, default, surface);
                return;
            }

            var active = party.ActiveCharacterId;
            if (active.IsNone || _spawner == null ||
                !_spawner.Registry.TryGet(active, out var activeView) || activeView == null)
            {
                _continuousWaitingReason = "ActivePresentationUnavailable";
                LastTransitionStatus = _continuousWaitingReason;
                PublishSurfaceTravelDiagnostics(motion, active, surface);
                return;
            }

            if (motion.TryGetContinuousSurfaceWaypoint(out _))
                TickContinuousSurfaceAutoTravel(world, motion, active, activeView, surface);
            if (!motion.TryGetContinuousSurfaceWaypoint(out _))
                TryDriveFinalSurfaceArrival(world, motion, active, activeView, surface);
        }

        void TickContinuousSurfaceAutoTravel(
            SimulationWorld world,
            PlayerPartyWorldMotion motion,
            EntityId active,
            EntityView activeView,
            ContinuousOutdoorSurfaceRuntime surface)
        {
            if (!world.SurfaceGround.TryGet(motion.SurfaceId, out var navigation) || navigation == null)
            {
                _continuousWaitingReason = "SurfaceNavigationUnavailable";
                PublishSurfaceTravelDiagnostics(motion, active, surface);
                ReportSurfaceAutoTravelStalled(motion, active, surface, movementIssued: false);
                return;
            }

            var worldArrival = Mathf.Max(0.001f, navigation.CellSize * 0.8f);
            var routeAdvanced = false;
            while (motion.TryGetContinuousSurfaceWaypoint(out var canonicalWaypoint) &&
                   DistanceWorld(motion.WorldPosition, canonicalWaypoint) <= worldArrival)
            {
                motion.AdvanceContinuousSurfaceWaypoint();
                routeAdvanced = true;
            }
            if (routeAdvanced)
                InvalidateSurfaceSubgoalResolution();

            if (_currentSurfaceExecutionTargetIndex >= motion.ContinuousSurfaceRouteIndex &&
                _currentSurfaceExecutionTargetIndex < motion.ContinuousSurfaceRoute.Count)
            {
                var reachedTarget = motion.ContinuousSurfaceRoute[_currentSurfaceExecutionTargetIndex];
                surface.Mapper.WorldToPresentation(reachedTarget.X, reachedTarget.Y,
                    out var reachedX, out var reachedY);
                var physicalArrival = Mathf.Max((_move?.WalkGrid?.CellSize ?? 1f) * 0.35f,
                    worldArrival * surface.Mapper.PresentationUnitsPerWorldUnit);
                if (Vector3.Distance(activeView.transform.position,
                        new Vector3(reachedX, reachedY, HostPresentationSpace.EntityZ)) <= physicalArrival)
                {
                    motion.AdvanceContinuousSurfaceRouteTo(_currentSurfaceExecutionTargetIndex + 1);
                    InvalidateSurfaceSubgoalResolution();
                }
            }

            if (!motion.TryGetContinuousSurfaceWaypoint(out _))
                return;

            if (!TryResolveSurfaceRouteSubgoal(
                    motion, activeView.transform.position, surface,
                    out var subgoal, out var subgoalKind, out var failureReason))
            {
                _continuousWaitingReason = failureReason;
                _surfaceTravelRetryCooldownUntil = Time.time + 0.25f;
                LastTransitionStatus = failureReason;
                PublishSurfaceTravelDiagnostics(motion, active, surface);
                ReportSurfaceAutoTravelStalled(motion, active, surface, movementIssued: false);
                return;
            }

            _continuousWaitingReason = string.Empty;
            var movementIssued = DriveContinuousSubgoal(
                motion, active, activeView, subgoal, subgoalKind, surface.NavigationGeneration);
            LastTransitionStatus = "SurfaceRoute" + subgoalKind +
                                   " " + motion.ContinuousSurfaceRouteIndex + "/" +
                                   motion.ContinuousSurfaceRoute.Count;
            PublishSurfaceTravelDiagnostics(motion, active, surface);
            ReportSurfaceAutoTravelStalled(motion, active, surface, movementIssued);
        }

        void TryDriveFinalSurfaceArrival(
            SimulationWorld world,
            PlayerPartyWorldMotion motion,
            EntityId active,
            EntityView activeView,
            ContinuousOutdoorSurfaceRuntime continuous)
        {
            if (!PlayerPartySurfaceTravelService.IsActiveSurfaceTravel(motion) ||
                !motion.HasContinuousPhysicalDestination)
            {
                LastTransitionStatus = "SurfaceTravelInvariantViolation";
                return;
            }

            continuous.Mapper.WorldToPresentation(
                motion.ContinuousPhysicalDestination.X,
                motion.ContinuousPhysicalDestination.Y,
                out var px, out var py);
            var desired = new Vector3(px, py, HostPresentationSpace.EntityZ);
            if (!TryResolveContinuousSubgoal(
                    motion, activeView.transform.position, desired, continuous,
                    out var resolvedGoal, out var goalKind, out var failureReason))
            {
                _continuousWaitingReason = failureReason;
                LastTransitionStatus = failureReason;
                PublishSurfaceTravelDiagnostics(motion, active, continuous);
                ReportSurfaceAutoTravelStalled(motion, active, continuous, movementIssued: false);
                return;
            }

            var gridCell = _move?.WalkGrid?.CellSize ?? 1f;
            if (goalKind == ContinuousWalkGridSubgoalKind.ReachableApproach &&
                Vector3.Distance(desired, resolvedGoal) > gridCell * 8f)
            {
                _continuousWaitingReason = "FinalApproachOutsideBound";
                LastTransitionStatus = _continuousWaitingReason;
                PublishSurfaceTravelDiagnostics(motion, active, continuous);
                ReportSurfaceAutoTravelStalled(motion, active, continuous, movementIssued: false);
                return;
            }

            var arrivalRadius = Mathf.Max(
                gridCell * 0.35f,
                motion.ContinuousPhysicalArrivalRadius * continuous.Mapper.PresentationUnitsPerWorldUnit);
            if (goalKind != ContinuousWalkGridSubgoalKind.ReachableFrontier &&
                Vector3.Distance(activeView.transform.position, resolvedGoal) <= arrivalRadius)
            {
                continuous.SyncPartyPresentation();
                var finish = PlayerPartyTravelRuntimeService.CompleteSurfaceArrival(world);
                if (finish.IsSuccess)
                    _move.CancelPresentationMovementPublic(active);
                LastTransitionStatus = finish.IsSuccess ? "SurfaceArrived" : "SurfaceFinalArrivalRejected";
                LastTransitionFailureReason = finish.IsFailure ? finish.Error.ToString() : string.Empty;
                return;
            }

            var movementIssued = DriveContinuousSubgoal(
                motion, active, activeView, resolvedGoal, goalKind,
                continuous.NavigationGeneration);
            LastTransitionStatus = "SurfaceFinal" + goalKind;
            PublishSurfaceTravelDiagnostics(motion, active, continuous);
            ReportSurfaceAutoTravelStalled(motion, active, continuous, movementIssued);
        }

        static float DistanceWorld(WorldVec2 a, WorldVec2 b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        bool TryResolveSurfaceRouteSubgoal(
            PlayerPartyWorldMotion motion,
            Vector3 current,
            ContinuousOutdoorSurfaceRuntime surface,
            out Vector3 subgoal,
            out ContinuousWalkGridSubgoalKind kind,
            out string failureReason)
        {
            subgoal = default;
            kind = ContinuousWalkGridSubgoalKind.ReachableFrontier;
            failureReason = string.Empty;
            var grid = _move != null ? _move.WalkGrid : null;
            var routeIndex = motion.ContinuousSurfaceRouteIndex;
            var sameIdentity =
                _hasContinuousSubgoalResolution &&
                _continuousSubgoalRouteVersion == motion.TravelPlanVersion &&
                _continuousSubgoalRouteIndex == routeIndex &&
                _continuousSubgoalNavigationGeneration == surface.NavigationGeneration &&
                _continuousSubgoalGridRevision == (grid != null ? grid.Revision : -1);
            if (!sameIdentity)
            {
                _surfaceRouteCandidateXy.Clear();
                var end = Mathf.Min(motion.ContinuousSurfaceRoute.Count,
                    routeIndex + SurfaceRouteLookaheadCount);
                for (var i = routeIndex; i < end; i++)
                {
                    var point = motion.ContinuousSurfaceRoute[i];
                    surface.Mapper.WorldToPresentation(point.X, point.Y, out var mapX, out var mapY);
                    _surfaceRouteCandidateXy.Add(mapX);
                    _surfaceRouteCandidateXy.Add(mapY);
                }

                _hasContinuousSubgoalResolution = true;
                _continuousSubgoalRouteVersion = motion.TravelPlanVersion;
                _continuousSubgoalRouteIndex = routeIndex;
                _continuousSubgoalNavigationGeneration = surface.NavigationGeneration;
                _continuousSubgoalGridRevision = grid != null ? grid.Revision : -1;
                _continuousSubgoalResolutionSucceeded =
                    ContinuousSurfaceLocalRouteResolver.TryResolve(
                        grid, current.x, current.y, _surfaceRouteCandidateXy, routeIndex,
                        out _currentSurfaceExecutionTargetIndex,
                        out var x, out var y, out _continuousSubgoalKind,
                        out _continuousWaitingReason);
                _continuousResolvedSubgoal = new Vector3(x, y, HostPresentationSpace.EntityZ);
            }

            if (!_continuousSubgoalResolutionSucceeded)
            {
                failureReason = string.IsNullOrEmpty(_continuousWaitingReason)
                    ? "NoPhysicalRoute"
                    : _continuousWaitingReason;
                return false;
            }

            subgoal = _continuousResolvedSubgoal;
            kind = _continuousSubgoalKind;
            return true;
        }

        void InvalidateSurfaceSubgoalResolution()
        {
            _currentSurfaceExecutionTargetIndex = -1;
            _hasContinuousSubgoalResolution = false;
            _continuousSubgoalResolutionSucceeded = false;
            _continuousSubgoalRouteIndex = -1;
            _hasContinuousIssuedSubgoal = false;
            _continuousWaitingReason = string.Empty;
        }

        void PublishSurfaceTravelDiagnostics(
            PlayerPartyWorldMotion motion,
            EntityId active,
            ContinuousOutdoorSurfaceRuntime surface)
        {
            var pathIndex = -1;
            var pathCount = 0;
            if (_move != null && !active.IsNone)
                _move.TryGetPathProgress(active, out pathIndex, out pathCount);
            if (motion != null && motion.IsMoving &&
                motion.ExecutionMode == PlayerPartyTravelExecutionMode.SurfaceVisible &&
                pathCount <= 0 && string.IsNullOrEmpty(_continuousWaitingReason))
                _continuousWaitingReason = _move != null && !active.IsNone && _move.IsMoving(active)
                    ? "MovementIssuedAwaitingPath"
                    : "SurfaceLocalRouteNoProgress";
            SurfaceLocalExecutionTargetIndex = _currentSurfaceExecutionTargetIndex;
            SurfaceLocalSubgoal = _hasContinuousSubgoalResolution
                ? _continuousResolvedSubgoal.ToString()
                : "-";
            SurfaceLocalSubgoalKind = _hasContinuousSubgoalResolution
                ? _continuousSubgoalKind.ToString()
                : "-";
            SurfaceWaitingReason = _continuousWaitingReason ?? string.Empty;
            SurfaceHostPathIndex = pathIndex;
            SurfaceHostPathCount = pathCount;
            SurfaceCompositeGridRevision = _move?.WalkGrid?.Revision ?? -1;
            SurfaceNavigationGeneration = surface != null ? surface.NavigationGeneration : -1;
            SurfaceCurrentChunk = surface != null ? surface.CurrentChunk.ToString() : "-";
        }

        bool TryResolveContinuousSubgoal(
            PlayerPartyWorldMotion motion,
            Vector3 current,
            Vector3 desired,
            ContinuousOutdoorSurfaceRuntime surface,
            out Vector3 subgoal,
            out ContinuousWalkGridSubgoalKind kind,
            out string failureReason)
        {
            subgoal = default;
            kind = ContinuousWalkGridSubgoalKind.ReachableFrontier;
            failureReason = string.Empty;
            var grid = _move != null ? _move.WalkGrid : null;
            var sameIdentity =
                _hasContinuousSubgoalResolution &&
                _continuousSubgoalRouteVersion == motion.TravelPlanVersion &&
                _continuousSubgoalNavigationGeneration == surface.NavigationGeneration &&
                _continuousSubgoalGridRevision == (grid != null ? grid.Revision : -1) &&
                Vector3.Distance(_continuousSubgoalDesired, desired) < 0.05f;
            if (!sameIdentity)
            {
                _hasContinuousSubgoalResolution = true;
                _continuousSubgoalRouteVersion = motion.TravelPlanVersion;
                _continuousSubgoalNavigationGeneration = surface.NavigationGeneration;
                _continuousSubgoalGridRevision = grid != null ? grid.Revision : -1;
                _continuousSubgoalDesired = desired;
                _continuousSubgoalResolutionSucceeded =
                    ContinuousWalkGridSubgoalResolver.TryResolve(
                        grid, current.x, current.y, desired.x, desired.y,
                        out var x, out var y, out _continuousSubgoalKind,
                        out _continuousWaitingReason);
                _continuousResolvedSubgoal = new Vector3(x, y, HostPresentationSpace.EntityZ);
            }

            if (!_continuousSubgoalResolutionSucceeded)
            {
                failureReason = string.IsNullOrEmpty(_continuousWaitingReason)
                    ? "ContinuousRouteUnavailable"
                    : _continuousWaitingReason;
                return false;
            }

            subgoal = _continuousResolvedSubgoal;
            kind = _continuousSubgoalKind;
            return true;
        }

        bool DriveContinuousSubgoal(
            PlayerPartyWorldMotion motion,
            EntityId active,
            EntityView activeView,
            Vector3 subgoal,
            ContinuousWalkGridSubgoalKind kind,
            int navigationGeneration)
        {
            if (_move == null)
            {
                _continuousWaitingReason = "NavigationNotReady";
                return false;
            }
            var arrivalDistance = (_move.WalkGrid?.CellSize ?? 1f) * 0.35f;
            if (Vector3.Distance(activeView.transform.position, subgoal) <= arrivalDistance)
            {
                _continuousWaitingReason = kind == ContinuousWalkGridSubgoalKind.ReachableFrontier
                    ? "NeedsRouteData"
                    : "SubgoalReachedAwaitingCanonicalCommit";
                return false;
            }

            var sameIssuedGoal =
                _hasContinuousIssuedSubgoal &&
                _continuousIssuedRouteVersion == motion.TravelPlanVersion &&
                Vector3.Distance(_continuousIssuedSubgoal, subgoal) < 0.05f;
            if (sameIssuedGoal &&
                _move.HasMovementPath(active) &&
                _move.IsRemainingPathValid(active))
            {
                _continuousLastIssueReason = "KeepValidPath/NavGen=" + navigationGeneration;
                return true;
            }

            var reason = !sameIssuedGoal
                ? "RouteOrSubgoalChanged"
                : _move.HasMovementPath(active)
                    ? "RemainingPathInvalid"
                    : "NoActivePath";
            if (!_move.OrderEntityToWorldPoint(
                    active, subgoal, null, issueStop: false,
                    completionPolicy: HostMoveCompletionPolicy.PreserveCurrentCommand,
                    exactGoal: true))
            {
                _continuousWaitingReason = "SubgoalUnreachable";
                _surfaceTravelRetryCooldownUntil = Time.time + 0.5f;
                _continuousLastIssueReason = reason + ":Rejected";
                return false;
            }

            _hasContinuousIssuedSubgoal = true;
            _continuousIssuedRouteVersion = motion.TravelPlanVersion;
            _continuousIssuedSubgoal = subgoal;
            _continuousSubgoalIssueCount++;
            _continuousLastIssueReason = reason;
            return true;
        }

        /// <summary>玩家直接接管移动时取消当前 Surface 自动旅行。</summary>
        void CancelSurfaceAutoTravelForPlayerInterrupt()
        {
            var world = bootstrap?.Session?.World;
            if (!PlayerPartySurfaceTravelService.IsActiveSurfaceTravel(world?.PlayerPartyTravel))
                return;
            PlayerPartyTravelRuntimeService.CancelTravel(world);
            _surfaceTravelExecutionArmed = false;
            ResetSurfaceAutoTravelTracking();
        }

        void ResetSurfaceAutoTravelTracking(bool preserveResumeRequest = false)
        {
            if (!preserveResumeRequest)
                _resumeSurfaceTravelRequested = false;
            _surfaceTravelRetryCooldownUntil = 0f;
            _currentSurfaceExecutionTargetIndex = -1;
            _continuousAutoTravelStallReported = false;
            _continuousSubgoalRouteVersion = -1;
            _continuousSubgoalRouteIndex = -1;
            _continuousSubgoalNavigationGeneration = -1;
            _continuousSubgoalGridRevision = -1;
            _hasContinuousSubgoalResolution = false;
            _continuousSubgoalResolutionSucceeded = false;
            _continuousWaitingReason = string.Empty;
            _continuousIssuedRouteVersion = -1;
            _hasContinuousIssuedSubgoal = false;
            _continuousSubgoalIssueCount = 0;
            _continuousLastIssueReason = string.Empty;
            _continuousProgressWindowStartedAt = -1f;
            _continuousProgressRouteVersion = -1;
            SurfaceLocalExecutionTargetIndex = -1;
            SurfaceLocalSubgoal = "-";
            SurfaceLocalSubgoalKind = "-";
            SurfaceWaitingReason = string.Empty;
            SurfaceHostPathIndex = -1;
            SurfaceHostPathCount = 0;
            SurfaceCompositeGridRevision = -1;
            SurfaceNavigationGeneration = -1;
            SurfaceCurrentChunk = "-";
        }

        void ArmSurfaceTravelExecution(bool cancelPresentationMovement)
        {
            _surfaceTravelExecutionArmed = true;
            ResetSurfaceAutoTravelTracking(preserveResumeRequest: true);
            _resumeSurfaceTravelRequested = false;
            var active = Party != null ? Party.ActiveCharacterId : EntityId.None;
            if (cancelPresentationMovement && !active.IsNone)
                _move?.CancelPresentationMovementPublic(active);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"),
         System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        void ReportSurfaceAutoTravelStalled(
            PlayerPartyWorldMotion motion,
            EntityId active,
            ContinuousOutdoorSurfaceRuntime surface,
            bool movementIssued)
        {
            if (motion == null ||
                !motion.IsMoving ||
                motion.ExecutionMode != PlayerPartyTravelExecutionMode.SurfaceVisible ||
                surface == null ||
                !surface.IsActive)
                return;

            if (_spawner == null ||
                !_spawner.Registry.TryGet(active, out var activeView) ||
                activeView == null)
                return;

            var position = activeView.transform.position;
            var hostMoving = _move != null && _move.IsMoving(active);
            var subgoalChanged =
                _continuousProgressRouteVersion != motion.TravelPlanVersion ||
                Vector3.Distance(_continuousProgressSubgoal, _continuousResolvedSubgoal) >= 0.05f;
            if (_continuousProgressWindowStartedAt < 0f || subgoalChanged)
            {
                _continuousProgressRouteVersion = motion.TravelPlanVersion;
                _continuousProgressSubgoal = _continuousResolvedSubgoal;
                _continuousProgressAnchor = position;
                _continuousProgressWorldAnchor = motion.WorldPosition;
                _continuousProgressWindowStartedAt = Time.unscaledTime;
                _continuousAutoTravelStallReported = false;
                return;
            }

            var progressThreshold = (_move?.WalkGrid?.CellSize ?? 1f) * 0.25f;
            var actualDelta = Vector3.Distance(position, _continuousProgressAnchor);
            var canonicalDelta = DistanceWorld(motion.WorldPosition, _continuousProgressWorldAnchor);
            if (actualDelta >= progressThreshold || canonicalDelta >= 0.001f)
            {
                _continuousProgressAnchor = position;
                _continuousProgressWorldAnchor = motion.WorldPosition;
                _continuousProgressWindowStartedAt = Time.unscaledTime;
                _continuousAutoTravelStallReported = false;
                return;
            }
            if (Time.unscaledTime - _continuousProgressWindowStartedAt < 1.5f)
                return;
            if (_continuousAutoTravelStallReported)
                return;

            _continuousAutoTravelStallReported = true;
            var pathIndex = -1;
            var pathCount = 0;
            if (_move != null)
                _move.TryGetPathProgress(active, out pathIndex, out pathCount);
            if (pathCount > 0)
                return;
            var finalGoal = motion.HasContinuousPhysicalDestination
                ? motion.ContinuousPhysicalDestination.ToString()
                : "missing";
            Debug.LogWarning(
                "[SurfaceAutoTravelStalled]" +
                " ExecutionMode=" + motion.ExecutionMode +
                " PlanVersion=" + motion.TravelPlanVersion +
                " SurfaceId=" + (motion.SurfaceId ?? string.Empty) +
                " WorldPosition=" + motion.WorldPosition +
                " ActivePresentation=" + position +
                " DestinationWorldPosition=" + finalGoal +
                " SurfaceRoute=" + motion.ContinuousSurfaceRouteIndex + "/" +
                motion.ContinuousSurfaceRoute.Count +
                " LocalExecutionTargetIndex=" + _currentSurfaceExecutionTargetIndex +
                " Subgoal=" + _continuousResolvedSubgoal +
                " SubgoalKind=" + _continuousSubgoalKind +
                " CurrentChunk=" + surface.CurrentChunk +
                " NavigationGeneration=" + surface.NavigationGeneration +
                " CompositeGridRevision=" + (_move?.WalkGrid?.Revision ?? -1) +
                " PathIndex=" + pathIndex +
                " PathCount=" + pathCount +
                " MovementIssued=" + movementIssued +
                " SubgoalIssueCount=" + _continuousSubgoalIssueCount +
                " LastIssueReason=" + (_continuousLastIssueReason ?? string.Empty) +
                " ActualPositionDelta=" + actualDelta.ToString("0.###") +
                " CanonicalDelta=" + canonicalDelta.ToString("0.###") +
                " WaitingReason=" + (_continuousWaitingReason ?? string.Empty) +
                " HostMove.IsMoving=" + hostMoving,
                this);
        }

        static Vector2 ReadWasdDirection()
        {
            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
                return Vector2.zero;

            var dir = Vector2.zero;
            if (Input.GetKey(KeyCode.W)) dir.y += 1f;
            if (Input.GetKey(KeyCode.S)) dir.y -= 1f;
            if (Input.GetKey(KeyCode.A)) dir.x -= 1f;
            if (Input.GetKey(KeyCode.D)) dir.x += 1f;
            if (dir.sqrMagnitude > 1f)
                dir.Normalize();
            return dir;
        }

        void TickFollowers()
        {
            var active = Party.ActiveCharacterId;
            if (active.IsNone || _move == null || _spawner == null)
                return;

            if (!_spawner.Registry.TryGet(active, out var activeView) || activeView == null)
                return;

            var activePos = activeView.transform.position;
            var followerIndex = 0;
            for (var i = 0; i < Party.Members.Count; i++)
            {
                var id = Party.Members[i];
                if (Party.IsActive(id))
                    continue;

                if (!PlayerPartyTransitionMembership.ShouldMemberTransitionWithParty(
                        bootstrap.Session.World, Party, id))
                    continue;

                // Formation slot 按 Party.Members 稳定顺序分配：每遇到一个非 Active follower
                // 立即确定它自己的 slot；之后即使因 melee/farm/moving/cooldown 被 continue，
                // 也不会改变其他 follower 的 slot（修复：原实现 followerIndex++ 在多个
                // early continue 之后，slot 会随"本帧谁需要 repath"漂移）。
                var offset = FollowerOffset(followerIndex);
                followerIndex++;

                if (_melee != null && _melee.IsAttacker(id))
                    continue;
                if (_chop != null && _chop.IsAttacker(id))
                    continue;
                if (_farm != null && _farm.IsFarming(id))
                    continue;

                // Incapacitated / Dead follower：Party ownership 层直接跳过，绝不发 follow movement。
                // （下层 movement 即使会拒绝，也不应让弥留者被 follow 逻辑驱赶。）
                if (!IsLivingPartyMember(id))
                    continue;

                // 已跟上 Active（距 formation goal 足够近）：不再生成新 path。
                // 不做 stale movement 全局 Cancel —— 无法安全区分 Follow path 与
                // schedule 等特殊行为 path，保持最小改动（仅周期性 repath）。
                if (!_spawner.Registry.TryGet(id, out var view) || view == null)
                    continue;

                var goal = activePos + offset;
                goal.z = HostPresentationSpace.EntityZ;
                var dist = Vector2.Distance(
                    new Vector2(view.transform.position.x, view.transform.position.y),
                    new Vector2(activePos.x + offset.x, activePos.y + offset.y));
                if (dist <= followStopDistance)
                    continue;

                // 周期性 repath throttle：moving follower 也允许在 followRepathInterval 后
                // 读取 Active 最新位置重算 formation goal（修复：原实现以
                // _move.IsMoving(id) 永久跳过，导致持续 AutoTravel 时 follower 停在
                // Active 的历史位置）。OrderEntityToWorldPoint 内部 ClearPath 重建，无叠加。
                if (!ShouldRepathFollower(id))
                    continue;

                _move.OrderEntityToWorldPoint(id, goal, null, issueStop: false,
                    completionPolicy: HostMoveCompletionPolicy.PreserveCurrentCommand);
                _nextFollowRepath[id.Value] = Time.unscaledTime + followRepathInterval;
            }
        }

        void TickReadyToEndEncounterFollowers(CharacterEncounterState encounter)
        {
            var active = Party.ActiveCharacterId;
            if (encounter == null || active.IsNone || _move == null || _spawner == null ||
                !_spawner.Registry.TryGet(active, out var activeView) || activeView == null)
                return;
            var activeParticipant = encounter.Find(active.Value);
            if (activeParticipant == null ||
                !string.Equals(activeParticipant.SquadId, Party.ControlledSquadId,
                    System.StringComparison.Ordinal))
                return;

            var activePosition = activeView.transform.position;
            var followerIndex = 0;
            for (var i = 0; i < Party.Members.Count; i++)
            {
                var id = Party.Members[i];
                if (id == active)
                    continue;
                var participant = encounter.Find(id.Value);
                if (participant == null ||
                    !string.Equals(participant.SquadId, Party.ControlledSquadId,
                        System.StringComparison.Ordinal) ||
                    !IsLivingPartyMember(id))
                    continue;
                var offset = FollowerOffset(followerIndex++);
                if (!_spawner.Registry.TryGet(id, out var view) || view == null)
                    continue;
                var goal = activePosition + offset;
                goal.z = HostPresentationSpace.EntityZ;
                if (Vector2.Distance(view.transform.position, goal) <= followStopDistance ||
                    !ShouldRepathFollower(id))
                    continue;
                _move.OrderEntityToWorldPoint(id, goal, null, issueStop: false,
                    completionPolicy: HostMoveCompletionPolicy.PreserveCurrentCommand);
                _nextFollowRepath[id.Value] = Time.unscaledTime + followRepathInterval;
            }
        }

        bool IsLivingPartyMember(EntityId id)
        {
            if (bootstrap?.Session?.World == null)
                return true;
            if (!bootstrap.Session.World.Entities.TryGet(id, out var ent) || ent == null)
                return false;
            return CombatLifeStateService.CanFight(ent);
        }

        bool ShouldRepathFollower(EntityId id)
        {
            if (!_nextFollowRepath.TryGetValue(id.Value, out var next))
                return true;
            return Time.unscaledTime >= next;
        }

        Vector3 FollowerOffset(int index)
        {
            if (index <= 0)
                return new Vector3(-followerSpreadRadius, 0f, 0f);
            var angle = index * 137.5f * Mathf.Deg2Rad;
            var r = followerSpreadRadius * (1f + 0.15f * (index % 3));
            return new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, 0f);
        }

        void OrderFollowerTowardActive(EntityId follower)
        {
            OrderFollowerTowardActive(follower, ResolveFollowerSlotIndex(follower));
        }

        int ResolveFollowerSlotIndex(EntityId follower)
        {
            if (Party == null || follower.IsNone)
                return 0;
            var idx = 0;
            for (var i = 0; i < Party.Members.Count; i++)
            {
                var id = Party.Members[i];
                if (Party.IsActive(id))
                    continue;
                if (bootstrap?.Session?.World != null &&
                    !PlayerPartyTransitionMembership.ShouldMemberTransitionWithParty(
                        bootstrap.Session.World, Party, id))
                    continue;
                if (id == follower)
                    return idx;
                idx++;
            }

            return idx;
        }

        void OrderFollowerTowardActive(EntityId follower, int followerIndex)
        {
            if (!bootstrap.Session.World.Strategic.Squads.TryGet(Party.ControlledSquadId, out var command) ||
                command.CommandKind != SquadCommandKind.FollowLeader) return;
            var active = command.CommandTargetCharacterId.IsNone ? Party.ActiveCharacterId : command.CommandTargetCharacterId;
            if (follower.IsNone || active.IsNone || _move == null || _spawner == null)
                return;

            // 生命状态 gate：Incapacitated / Corpse follower 绝不向 Active 聚集。
            if (!IsLivingPartyMember(follower))
                return;

            if (!_spawner.Registry.TryGet(active, out var activeView) || activeView == null)
                return;

            // Formation goal：Active + deterministic slot offset（与 TickFollowers 同一 convention）。
            // 修复：旧实现 goal = Active exact transform → 所有 follower 反复叠回 Active 同一点。
            var offset = FollowerOffset(followerIndex);
            var goal = activeView.transform.position + offset;
            goal.z = HostPresentationSpace.EntityZ;
            // 普通 Follow / Rebind 是内部 presentation 追随，不是玩家 Stop 命令。
            // issueStop:true 会发 Domain Stop（StopOne → commandBridge → CancelTravel），
            // 错误取消整队 PlayerParty Surface AutoTravel。OrderEntityToWorldPoint(issueStop:false)
            // 仍会 ClearPath/ClearPending 并重建 Local A* path（见 HostMoveController:426）。
            _move.OrderEntityToWorldPoint(follower, goal, null, issueStop: false,
                completionPolicy: HostMoveCompletionPolicy.PreserveCurrentCommand);
            _nextFollowRepath[follower.Value] = Time.unscaledTime + followRepathInterval;
        }

        void TickCombatFollow()
        {
            if (_melee == null || !_melee.IsFighting)
                return;

            var active = Party.ActiveCharacterId;
            if (!_melee.IsAttacker(active))
                return;

            var world = bootstrap?.Session?.World;
            var defender = _melee.DefenderId;
            for (var i = 0; i < Party.Members.Count; i++)
            {
                var id = Party.Members[i];
                if (Party.IsActive(id) || _melee.IsAttacker(id))
                    continue;
                if (!bootstrap.Session.World.Entities.TryGet(id, out var ent) ||
                    !CombatLifeStateService.CanFight(ent))
                    continue;
                // SPACE-01：仅当前 Separate Space occupants（或非 Separate Space 时全体可战随从）协战。
                if (world != null &&
                    SeparateSpaceCombatPolicy.IsInPlaceCombatSpace(world) &&
                    !world.LocalMap.ContainsOccupant(id))
                    continue;

                _melee.BeginAutomatic(id, defender);
            }
        }

        void TickPartyDerivedGroupActivity()
        {
            var active = Party.ActiveCharacterId;
            if (active.IsNone)
                return;

            var current = ResolveActiveSharedActivity(active);
            if (current != _lastActiveSharedActivity)
            {
                for (var i = 0; i < Party.Members.Count; i++)
                {
                    var id = Party.Members[i];
                    if (Party.IsActive(id))
                        continue;
                    if (_followerSharedActivity.TryGetValue(id.Value, out var assigned) &&
                        assigned == current)
                        continue;
                    StopFollowerPartyDerivedWork(id);
                }

                _lastActiveSharedActivity = current;
            }

            if (!current.IsShareable)
            {
                ClearFollowerPartyDerivedWork();
                return;
            }

            for (var i = 0; i < Party.Members.Count; i++)
            {
                var id = Party.Members[i];
                if (Party.IsActive(id))
                    continue;
                if (!PlayerPartyTransitionMembership.ShouldMemberTransitionWithParty(
                        bootstrap.Session.World, Party, id))
                    continue;
                if (_followerSharedActivity.TryGetValue(id.Value, out var assigned) &&
                    assigned == current)
                    continue;

                if (TryAssignFollowerSharedActivity(id, current, active))
                    _followerSharedActivity[id.Value] = current;
            }
        }

        HostPartySharedActivity ResolveActiveSharedActivity(EntityId active)
        {
            if (_melee != null && _melee.IsAttacker(active))
                return HostPartySharedActivity.Combat;

            if (IsActivePlayerDrivenMoving(active))
                return HostPartySharedActivity.Movement;

            if (_farm != null && _farm.IsFarming(active))
            {
                var loc = ResolveFarmLocation(active);
                if (!string.IsNullOrEmpty(loc))
                    return HostPartySharedActivity.Farming(loc);
            }

            if (_chop != null && _chop.IsAttacker(active))
            {
                var target = _chop.GetTargetForAttacker(active);
                var instanceId = target != null ? target.GetInstanceID() : 0;
                return HostPartySharedActivity.Woodcutting(instanceId);
            }

            if (_workLoop != null && _workLoop.IsLooping(active) &&
                _workLoop.TryGetLoopKind(active, out var loopKind))
            {
                var loc = ResolveEntityLocation(active);
                if (!string.IsNullOrEmpty(loc))
                    return HostPartySharedActivity.Gathering(loc, loopKind);
            }

            return HostPartySharedActivity.FollowIdle;
        }

        bool TryAssignFollowerSharedActivity(
            EntityId follower,
            HostPartySharedActivity activity,
            EntityId active)
        {
            StopFollowerPartyDerivedWork(follower);

            switch (activity.Kind)
            {
                case HostPartySharedActivityKind.Farming:
                    return _farm != null &&
                           _farm.Begin(follower, activity.LocationId, fromPartyFollow: true);
                case HostPartySharedActivityKind.Woodcutting:
                    return TryBeginFollowerWoodcut(follower, active);
                case HostPartySharedActivityKind.Gathering:
                    if (_workLoop == null)
                        return false;
                    _workLoop.StartPartyDerivedLoop(follower, activity.LoopKind);
                    return _workLoop.IsPartyDerivedLooping(follower);
                default:
                    return false;
            }
        }

        bool TryBeginFollowerWoodcut(EntityId follower, EntityId active)
        {
            if (_chop == null || _spawner == null)
                return false;

            if (!_spawner.Registry.TryGet(follower, out var followerView) || followerView == null)
                return false;

            var from = followerView.transform.position;
            HostMapDestructible activeTarget = null;
            if (_chop.IsAttacker(active))
                activeTarget = _chop.GetTargetForAttacker(active);

            if (!HostMapObjectRegistry.TryFindNearestDestructible(
                    from,
                    followerChopSearchRadius,
                    out var tree,
                    treesOnly: true,
                    exclude: activeTarget) &&
                activeTarget != null)
                tree = activeTarget;

            if (tree == null)
                return false;

            _chop.Begin(follower, tree, fromPartyFollow: true);
            return _chop.IsAttacker(follower);
        }

        void StopFollowerPartyDerivedWork(EntityId id)
        {
            _farm?.StopPartyDerived(id);
            _chop?.StopPartyDerived(id);
            _workLoop?.StopPartyDerived(id);
            _followerSharedActivity.Remove(id.Value);
        }

        void ClearFollowerPartyDerivedWork()
        {
            for (var i = 0; i < Party.Members.Count; i++)
            {
                var id = Party.Members[i];
                if (Party.IsActive(id))
                    continue;
                if (_followerSharedActivity.ContainsKey(id.Value) ||
                    (_farm != null && _farm.IsPartyDerivedFarming(id)) ||
                    (_chop != null && _chop.IsPartyDerivedAttacker(id)) ||
                    (_workLoop != null && _workLoop.IsPartyDerivedLooping(id)))
                    StopFollowerPartyDerivedWork(id);
            }
        }

        string ResolveFarmLocation(EntityId id)
        {
            if (_farm != null && _farm.TryGetFarmLocation(id, out var loc))
                return loc;
            return ResolveEntityLocation(id);
        }

        string ResolveEntityLocation(EntityId id)
        {
            if (bootstrap?.Session?.World == null ||
                !bootstrap.Session.World.Entities.TryGet(id, out var ent) ||
                !ent.TryGet<XianXia.Core.Exploration.EntityLocationComponent>(out var loc) ||
                !loc.HasLocation)
                return null;
            return loc.LocationId;
        }

        void TickCameraFollow()
        {
            if (!enableCameraFollow || _cameraRig == null || _spawner == null)
                return;
            if (HostInputGate.BlockWorldCamera)
                return;

            var active = Party.ActiveCharacterId;
            if (active.IsNone ||
                !_spawner.Registry.TryGet(active, out var view) ||
                view == null)
                return;

            // AutoTravel follows Active; middle-mouse pan DETACHES the
            // camera until a NEW AutoTravel session starts (restored only on fresh session; not on
            // Open/Close WorldMap within the same travel).
            var world = bootstrap?.Session?.World;
            if (world?.PlayerPartyTravel != null)
            {
                var sessionMotion = world.PlayerPartyTravel;
                // Travel ended or cancelled: the next Surface route is a new camera session.
                if (!sessionMotion.IsMoving)
                    _hasAutoTravelSession = false;
            }

            if (world?.PlayerPartyTravel != null &&
                PlayerPartySurfaceTravelService.IsActiveSurfaceTravel(world.PlayerPartyTravel) &&
                world.PlayerPartyTravel.LocationKind == PlayerPartyLocationKind.AtWorldPosition)
            {
                if (!_hasAutoTravelSession)
                {
                    _hasAutoTravelSession = true;
                    _cameraDetachedByPlayer = false; // new AutoTravel session: follow by default
                }

                if (_cameraRig.ConsumeUserMiddlePanThisFrame())
                    _cameraDetachedByPlayer = true; // player took the camera: stay detached
                if (!_cameraDetachedByPlayer)
                    _cameraRig.SoftFollow(view.transform.position, surfaceAutoTravelFollowLerp);
                return;
            }

            var wasd = HasActiveWasdDirectInput();
            _cameraMode = ResolveCameraFollowMode(wasd);

            if (_cameraMode == HostActiveCameraFollowMode.WasdHardFollow)
                _cameraRig.HardFollow(view.transform.position);
        }

        void EnterWasdHardFollow(EntityId active)
        {
            _cameraMode = HostActiveCameraFollowMode.WasdHardFollow;
            SnapCameraTo(active);
        }

        /// <summary>
        /// Camera Hard Follow source: valid player WASD Direct Movement only.
        /// Not Character.IsMoving / RTS path.
        /// </summary>
        public bool HasActiveWasdDirectInput()
        {
            if (HostInputGate.BlockWorldInteraction)
                return false;
            if (Party == null || Party.ActiveCharacterId.IsNone)
                return false;
            return ReadWasdDirection().sqrMagnitude > 0.01f;
        }

        public HostActiveCameraFollowMode CameraFollowMode => _cameraMode;

        /// <summary>
        /// Final camera policy: only WASD Direct Input engages Hard Follow.
        /// RTS / click path never changes Camera mode.
        /// </summary>
        public static HostActiveCameraFollowMode ResolveCameraFollowMode(bool wasdDirectActive) =>
            wasdDirectActive
                ? HostActiveCameraFollowMode.WasdHardFollow
                : HostActiveCameraFollowMode.Free;

        /// <summary>Compatibility overload; previous mode is ignored (RTS never drives Camera).</summary>
        public static HostActiveCameraFollowMode ResolveCameraFollowMode(
            HostActiveCameraFollowMode _,
            bool wasdDirectActive) =>
            ResolveCameraFollowMode(wasdDirectActive);

        /// <summary>Active 正在 WASD 或点击寻路移动（玩家驱动）——供 Party 共享活动，非 Camera 策略。</summary>
        public bool IsActivePlayerDrivenMoving(EntityId active)
        {
            if (active.IsNone || Party == null || !Party.IsActive(active))
                return false;

            if (HasActiveWasdDirectInput())
                return true;

            return _move != null && _move.IsPlayerPartyPathMoving(active);
        }

        void FrameCameraOn(EntityId id) => SnapCameraTo(id);

        void SnapCameraTo(EntityId id)
        {
            if (!enableCameraFollow || _cameraRig == null || _spawner == null || id.IsNone)
                return;
            if (_spawner.Registry.TryGet(id, out var view) && view != null)
                _cameraRig.FrameWorldPoint(view.transform.position);
        }

        void StopFollowerDirectControl(EntityId id)
        {
            _move?.CancelPresentationMovementPublic(id);
            StopFollowerPartyDerivedWork(id);
            _melee?.StopAutomatic(id);
        }
    }
}
