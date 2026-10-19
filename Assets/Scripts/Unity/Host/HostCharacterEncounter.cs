using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Npc;
using XianXia.Core.Simulation;
using XianXia.Core.Results;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>Confirmation, rendering and tactical execution adapter for the domain encounter.</summary>
    public sealed class HostCharacterEncounter : MonoBehaviour
    {
        const string PauseOwner = "CharacterEncounterUI";
        public enum PresentationPhase { None, Pending, Preparing, ReadyToCommit, ReadyToStart, Active, ReadyToEnd, Report, Failed }
        PlayableHostBootstrap _host;
        SimulationWorld _world;
        EntityId _pendingAttacker, _pendingTarget;
        string _failure = "";
        Action _onEntered;
        string _assaultSiteId;
        readonly CharacterEncounterStartGate _startGate = new CharacterEncounterStartGate();
        readonly SpiritVeilService _veil = new SpiritVeilService();
        readonly MeleeCombatService _melee = new MeleeCombatService();
        HostMeleeStrikeVfx _strikeVfx;
        Coroutine _entryRoutine;
        ContinuousOutdoorSurfaceRuntime.PreparedIndependentField _preparedField;
        bool _allowPendingCancel;
        string _progress = string.Empty;
        CharacterEncounterState _restoreState;
        readonly List<EntityId> _previewFriendly = new List<EntityId>();
        readonly List<EntityId> _previewEnemy = new List<EntityId>();
        int _requestSequence;
        string _requestId = string.Empty;
        bool _loggedTacticalClock;
        string _readyToEndEncounterId = string.Empty;
        public string RequestId => _preparedField?.State?.EncounterId ?? _restoreState?.EncounterId ?? _requestId;
        public bool IsRestoring => _restoreState != null;
        public PresentationPhase Phase { get; private set; }
        public string Failure => _failure;
        public string Progress => (Phase == PresentationPhase.ReadyToCommit || Phase == PresentationPhase.Preparing) &&
            !string.IsNullOrEmpty(_host?.ContinuousOutdoorSurfaceRuntime?.IndependentPreparationProgress)
                ? _host.ContinuousOutdoorSurfaceRuntime.IndependentPreparationProgress : _progress;
        public EntityId PendingAttacker => _pendingAttacker;
        public EntityId PendingTarget => _pendingTarget;
        public bool ReadyToStartIsRestore => _startGate.IsRestore;
        public bool CanCancel => HasPending && _allowPendingCancel;
        /// <summary>Preparing/pending presentation is Host-owned before Core publishes a state.</summary>
        public bool BlocksExternalControlHandoff =>
            HasPending || _entryRoutine != null || _preparedField != null || _restoreState != null ||
            Phase == PresentationPhase.Pending ||
            Phase == PresentationPhase.Preparing ||
            Phase == PresentationPhase.ReadyToCommit ||
            Phase == PresentationPhase.ReadyToStart ||
            Phase == PresentationPhase.Active ||
            Phase == PresentationPhase.ReadyToEnd;
        public static bool CanAdvanceTacticalPresentation(PresentationPhase phase, bool isPaused) =>
            phase != PresentationPhase.ReadyToStart && !isPaused;
        public void Bind(PlayableHostBootstrap host)
        {
            _host = host;
            _world = host.Session.World;
            _strikeVfx = host.GetComponent<HostMeleeStrikeVfx>() ??
                         host.gameObject.AddComponent<HostMeleeStrikeVfx>();
        }
        public bool HasPending => !_pendingAttacker.IsNone &&
            (!_pendingTarget.IsNone || !string.IsNullOrEmpty(_assaultSiteId));

        public void Request(
            EntityId attacker,
            EntityId target,
            bool automatic = false,
            Action onEntered = null,
            bool allowPendingCancel = true)
        {
            if (_host?.Session?.World == null) return;
            if (!automatic && !_host.Session.PlayerParty.IsPlayerControllableMember(attacker)) return;
            // SPACE-01 guard：Separate Space 不得创建 Independent CharacterEncounter。
            if (SeparateSpaceCombatPolicy.AreBothInActiveSeparateSpace(
                    _host.Session.World, attacker, target))
            {
                Debug.Log(
                    "[SeparateSpaceCombat] Independent CharacterEncounter bypassed; " +
                    "combat must resolve in-place. attacker=" + attacker.Value +
                    " target=" + target.Value);
                return;
            }
            if (_host.Session.World.Strategic.CharacterEncounter != null)
            {
                if (Phase == PresentationPhase.Active || Phase == PresentationPhase.ReadyToEnd)
                {
                    if (automatic) SetTargetAutomatic(attacker,target);
                    else SetTarget(attacker,target);
                }
                return;
            }
            if (HasPending) return;
            _assaultSiteId = null;
            var requestWorld = _host.Session.World;
            if (automatic && CharacterEncounterService.IsContactSuppressed(requestWorld, attacker, target)) return;
            if (!automatic) requestWorld.Strategic.SuppressedCharacterContacts.Remove(CharacterEncounterService.ContactKey(attacker, target));
            _world = _host.Session.World;
            _pendingAttacker = attacker; _pendingTarget = target; _failure = ""; _progress = "等待确认"; _onEntered = onEntered;
            _allowPendingCancel = allowPendingCancel && !automatic;
            _requestId = "request:" + (++_requestSequence);
            _previewFriendly.Clear(); _previewEnemy.Clear();
            if (requestWorld.Strategic.Squads.TryGetForCharacter(attacker, out var a) &&
                requestWorld.Strategic.Squads.TryGetForCharacter(target, out var b))
            {
                var friendly = requestWorld.Strategic.PlayerPartyContext?.IsMember(attacker) == true;
                foreach (var id in a.MemberCharacterIds)
                    if (CharacterEncounterService.IsLiving(requestWorld, id))
                        (friendly ? _previewFriendly : _previewEnemy).Add(new EntityId(id));
                foreach (var id in b.MemberCharacterIds)
                    if (CharacterEncounterService.IsLiving(requestWorld, id))
                        (friendly ? _previewEnemy : _previewFriendly).Add(new EntityId(id));
            }
            else if (target.IsNone && requestWorld.Strategic.Squads.TryGetForCharacter(attacker, out var playerSquad))
                foreach (var id in playerSquad.MemberCharacterIds)
                    if (CharacterEncounterService.IsLiving(requestWorld, id))
                        _previewFriendly.Add(new EntityId(id));
            Phase = PresentationPhase.Pending;
            HostInputGate.EncounterModalLock = true;
            _host.Session.AcquireModalPause(PauseOwner);
            LogStage("Pending");
        }

        public void CopyPreviewTo(List<EntityId> friendly, List<EntityId> enemy)
        {
            var state = _host?.Session?.World?.Strategic?.CharacterEncounter ?? _preparedField?.State;
            if (state != null)
                foreach (var p in state.Participants) (p.Enemy ? enemy : friendly).Add(new EntityId(p.CharacterId));
            else { friendly.AddRange(_previewFriendly); enemy.AddRange(_previewEnemy); }
        }

        public void RequestWorldSiteAssault(EntityId attacker, EntityId defender, string siteId, Action onEntered)
        {
            if (HasPending) return;
            Request(attacker, defender, onEntered: onEntered, allowPendingCancel: true);
            if (Phase == PresentationPhase.Pending && _pendingAttacker == attacker &&
                _pendingTarget == defender)
                _assaultSiteId = siteId;
        }

        void LogStage(string stage)
        {
            var session = _host.Session;
            Debug.Log("[CharacterEncounter] Id=" + RequestId + " stage=" + stage +
                " world=" + System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(session.World) +
                " manualPaused=" + session.ManualPaused + " modalOwners=" + session.ModalPauseDiagnostics +
                " freeze=" + session.World.Strategic.ClockFreeze.Reason +
                " inputBlocked=" + HostInputGate.BlockWorldInteraction +
                " field=" + _host.ContinuousOutdoorSurfaceRuntime.IndependentFieldId);
        }

        public void BeginConfirmed()
        {
            if (_restoreState != null && _entryRoutine == null)
            { RestoreField(_restoreState); return; }
            if (!HasPending || Phase == PresentationPhase.Preparing || _entryRoutine != null) return;
            _failure = string.Empty;
            Phase = PresentationPhase.Preparing;
            _entryRoutine = StartCoroutine(RunEntry(PrepareAndEnter(_pendingAttacker, _pendingTarget)));
        }

        public bool RestoreField(CharacterEncounterState state)
        {
            if (state == null || _entryRoutine != null) return false;
            ClearStartStaging();
            _world = _host.Session.World;
            _restoreState = state;
            _failure = string.Empty;
            _host.Session.AcquireModalPause(PauseOwner);
            HostInputGate.EncounterModalLock = true;
            Phase = PresentationPhase.Preparing;
            _entryRoutine = StartCoroutine(RunEntry(RestorePreparedField(state)));
            return true;
        }

        IEnumerator RestorePreparedField(CharacterEncounterState state)
        {
            var surface = _host.ContinuousOutdoorSurfaceRuntime;
            _progress = "恢复本场地形与导航";
            Result result = default;
            yield return surface.PrepareIndependentField(state, (r, field) => { result = r; _preparedField = field; });
            if (result.IsFailure || _preparedField == null) { Fail(result.Error.Message); yield break; }
            Phase = PresentationPhase.ReadyToCommit;
            yield return surface.CommitPreparedIndependentField(_preparedField, r => result = r, domainAlreadyBound: true);
            if (result.IsFailure) { Fail(result.Error.Message); yield break; }
            _preparedField = null; _restoreState = null;
            _requestId = state.EncounterId;
            if (state.Phase == CharacterEncounterPhase.ReadyToEnd)
            {
                Phase = PresentationPhase.ReadyToEnd;
                _host.Session.ReleaseModalPause(PauseOwner);
                HostInputGate.EncounterModalLock = false;
                LogStage("ReadyToEndRestored");
                yield break;
            }

            Action onStart = null;
            if (state.Objective != null && !state.Objective.Resolved &&
                state.Find(_host.Session.PlayerParty.ActiveCharacterId.Value)?.TargetId == ulong.MaxValue)
                onStart = () => HostWorldSiteCoreWarfare.ContinueAssault(_host, state.Objective.SiteId);
            _startGate.Stage(_world, state.EncounterId, EntityId.None, EntityId.None, onStart, isRestore: true,
                recordCharacterAttack: false);
            Phase = PresentationPhase.ReadyToStart;
            _progress = "战场已恢复 · 当前全场暂停";
            LogStage("ReadyToStartRestored");
        }

        // Own all nested iterators in one coroutine: stopping the request stops its children,
        // and an exception anywhere reaches the same retryable failure UI.
        IEnumerator RunEntry(IEnumerator entry)
        {
            yield return null; // Allow StartCoroutine to assign its handle before completion.
            var stack = new Stack<IEnumerator>();
            stack.Push(entry);
            try
            {
                while (stack.Count > 0)
                {
                    object current = null;
                    bool moved = false;
                    Exception error = null;
                    try { moved = stack.Peek().MoveNext(); if (moved) current = stack.Peek().Current; }
                    catch (Exception ex) { error = ex; }
                    if (error != null) { Fail("阶段 " + Phase + ": " + error); yield break; }
                    if (!moved) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                    if (current is IEnumerator nested) stack.Push(nested);
                    else yield return current;
                }
            }
            finally
            {
                while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
                _entryRoutine = null;
            }
        }

        IEnumerator PrepareAndEnter(EntityId attacker, EntityId target)
        {
            Phase = PresentationPhase.Preparing;
            _progress = "准备同源战场";
            LogStage("Preparing");
            var world = _host.Session.World;
            var surface = _host.ContinuousOutdoorSurfaceRuntime;
            if (surface == null || !surface.IsActive) { Fail("Continuous source is unavailable."); yield break; }
            surface.CaptureCurrentPersonalPlacements();
            CharacterEncounterState state;
            var prepared = _assaultSiteId == null
                ? CharacterEncounterService.Prepare(world, attacker, target, surface.ActiveSurfaceId, out state)
                : CharacterEncounterService.PrepareForWorldSiteAssault(world, attacker, target, _assaultSiteId, out state);
            if (prepared.IsFailure) { Fail(prepared.Error.Message); yield break; }
            _requestId = state.EncounterId;
            Result prepResult = default;
            yield return surface.PrepareIndependentField(state, (result, field) => { prepResult = result; _preparedField = field; });
            if (prepResult.IsFailure || _preparedField == null) { Fail(prepResult.Error.Message); yield break; }
            Phase = PresentationPhase.ReadyToCommit;
            _progress = "构建独立战场";
            _host.GetComponent<HostCombatSkillBar>()?.CaptureEncounterCooldowns(state);
            Result commitResult = default;
            yield return surface.CommitPreparedIndependentField(_preparedField, result => commitResult = result);
            if (commitResult.IsFailure)
            {
                Fail(commitResult.Error.Message);
                yield break;
            }
            _host.GetComponent<HostNpcMeleeAssault>()?.Clear();
            _startGate.Stage(world, state.EncounterId,
                target.IsNone ? EntityId.None : attacker, target, _onEntered, isRestore: false,
                recordCharacterAttack: !target.IsNone);
            _onEntered = null;
            _pendingAttacker = _pendingTarget = EntityId.None;
            _preparedField = null; Phase = PresentationPhase.ReadyToStart; _progress = "战场已就绪 · 当前全场暂停";
            _loggedTacticalClock = false;
            LogStage("ReadyToStart");
            Debug.Log("[CharacterEncounter] ReadyToStart Id=" + state.EncounterId +
                " manualPaused=" + _host.Session.ManualPaused + " paused=" + _host.Session.IsPaused +
                " inputBlocked=" + HostInputGate.BlockWorldInteraction);
        }

        public bool StartBattle()
        {
            if (Phase != PresentationPhase.ReadyToStart)
                return false;
            var session = _host?.Session;
            var world = session?.World;
            var state = world?.Strategic?.CharacterEncounter;
            var validation = _startGate.ValidateStart(
                world,
                state,
                _host?.ContinuousOutdoorSurfaceRuntime?.IndependentFieldId,
                session != null && session.HasModalPauseOwner(PauseOwner),
                HostInputGate.EncounterModalLock);
            if (validation.IsFailure)
            {
                _failure = "无法开始战斗：" + validation.Error.Message;
                _progress = _failure;
                session?.AcquireModalPause(PauseOwner);
                HostInputGate.EncounterModalLock = true;
                Debug.LogError("[CharacterEncounter] " + _failure, this);
                return false;
            }

            var attacker = _startGate.Attacker;
            var target = _startGate.Target;
            if (!attacker.IsNone && !target.IsNone)
            {
                // ValidateStart already proved both characters belong to opposing sides. Assign
                // the staged initial target here instead of opening public SetTarget during the
                // ReadyToStart input lock.
                state.Find(attacker.Value).TargetId = target.Value;
                if (_startGate.RecordCharacterAttack)
                    new XianXia.Core.Social.SocialEventService().RecordCharacterAttacked(world, attacker, target);
            }

            var action = _startGate.TakeStartAction();
            Phase = PresentationPhase.Active;
            action?.Invoke();
            _startGate.Clear();
            _assaultSiteId = null;
            _allowPendingCancel = false;
            _failure = string.Empty;
            _progress = "战斗进行中";
            session.ManualPaused = false;
            session.ReleaseModalPause(PauseOwner);
            HostInputGate.EncounterModalLock = false;
            _loggedTacticalClock = false;
            LogStage("Active");
            return true;
        }

        void Fail(string message)
        {
            _failure = message ?? "Encounter preparation failed.";
            _progress = "准备失败";
            _preparedField = null; _entryRoutine = null; Phase = PresentationPhase.Failed;
            _host?.ContinuousOutdoorSurfaceRuntime?.CancelPreparedIndependentField();
            LogStage("Failed");
            Debug.LogError("[CharacterEncounter] phase=" + Phase + " error=" + _failure);
            if (!string.IsNullOrEmpty(_assaultSiteId))
                HostWorldSiteCoreWarfare.Feedback(_host, "据点守军接战准备失败：" + _failure);
        }

        public void CancelPending()
        {
            if (!CanCancel) return;
            var cancelledSiteAssault = !string.IsNullOrEmpty(_assaultSiteId);
            MortalCivilianService.CancelCaptureRequest(_host.Session.World, _pendingAttacker, _pendingTarget);
            Debug.Log("[CharacterEncounter] cancel phase=" + Phase);
            if (_entryRoutine != null) StopCoroutine(_entryRoutine);
            _entryRoutine = null;
            _host.ContinuousOutdoorSurfaceRuntime.CancelPreparedIndependentField();
            if (!_pendingTarget.IsNone)
                _host.Session.World.Strategic.SuppressedCharacterContacts.Add(
                    CharacterEncounterService.ContactKey(_pendingAttacker, _pendingTarget));
            _pendingAttacker = _pendingTarget = EntityId.None; _onEntered = null;
            _allowPendingCancel = false; _assaultSiteId = null; _startGate.Clear();
            _preparedField = null; _failure = ""; _progress = string.Empty; Phase = PresentationPhase.None;
            HostInputGate.EncounterModalLock = false;
            _host.Session.ReleaseModalPause(PauseOwner);
            if (cancelledSiteAssault)
                _host.StrategicInterrupt?.ShowTransientToast("已撤回本次攻击；战争状态不变。");
        }

        public void SetTarget(EntityId attacker, EntityId target)
        {
            if (_host?.Session?.PlayerParty?.IsPlayerControllableMember(attacker) != true) return;
            SetTargetAutomatic(attacker,target);
        }

        internal void SetTargetAutomatic(EntityId attacker,EntityId target)
        {
            if (Phase == PresentationPhase.ReadyToStart)
                return;
            var world = _host?.Session?.World;
            var state = world?.Strategic?.CharacterEncounter;
            if (state == null || !state.Opposing(attacker.Value, target.Value)) return;
            var transportPermission = MortalTransportConstraintService.ValidateAttack(world, attacker);
            if (transportPermission.IsFailure)
            {
                var participant = state.Find(attacker.Value);
                if (participant != null) participant.TargetId = 0;
                if (_host.Session.PlayerParty.IsPlayerControllableMember(attacker))
                    _host.StrategicInterrupt?.ShowTransientToast(transportPermission.Error.Message);
                return;
            }
            if (state.Phase == CharacterEncounterPhase.ReadyToEnd)
            {
                if (_host.Session.PlayerParty.ActiveCharacterId != attacker ||
                    !world.Entities.TryGet(attacker, out var attackerEntity) ||
                    !CombatLifeStateService.CanFight(attackerEntity) ||
                    !world.Entities.TryGet(target, out var targetEntity) ||
                    !CombatLifeStateService.CanBeAttacked(targetEntity))
                    return;
            }
            else if (state.Phase != CharacterEncounterPhase.Active)
                return;
            state.Find(attacker.Value).TargetId = target.Value;
        }

        public void Stop(EntityId id)
        {
            if (_host?.Session?.PlayerParty?.IsPlayerControllableMember(id) != true) return;
            StopAutomatic(id);
        }

        internal void StopAutomatic(EntityId id)
        {
            var member = _host?.Session?.World?.Strategic?.CharacterEncounter?.Find(id.Value);
            if (member != null) member.TargetId = ulong.MaxValue;
        }

        void Update()
        {
            if (_host?.Session?.World == null) return;
            var world = _host.Session.World;
            if (!ReferenceEquals(_world, world))
            {
                _host?.ContinuousOutdoorSurfaceRuntime?.CancelPreparedIndependentField();
                if (_entryRoutine != null) StopCoroutine(_entryRoutine);
                _entryRoutine = null; _preparedField = null;
                _restoreState = null;
                _readyToEndEncounterId = string.Empty;
                _world = world; _pendingAttacker = _pendingTarget = EntityId.None; _onEntered = null;
                _assaultSiteId = null; _allowPendingCancel = false; _startGate.Clear();
                Phase = PresentationPhase.None;
                _host.Session.ReleaseModalPause(PauseOwner);
                HostInputGate.EncounterModalLock = false;
            }
            if (!world.Strategic.PendingCharacterTarget.IsNone)
            {
                var attacker = world.Strategic.PendingCharacterAttacker;
                var target = world.Strategic.PendingCharacterTarget;
                world.Strategic.PendingCharacterAttacker = world.Strategic.PendingCharacterTarget = EntityId.None;
                // Separate Space：Pending 不应升级为 Independent Encounter（RequiresEntry 已挡，此处双保险）。
                if (SeparateSpaceCombatPolicy.AreBothInActiveSeparateSpace(world, attacker, target))
                {
                    Debug.Log(
                        "[SeparateSpaceCombat] Independent CharacterEncounter bypassed; " +
                        "combat must resolve in-place. pendingAttacker=" + attacker.Value +
                        " pendingTarget=" + target.Value);
                }
                else
                    Request(attacker, target, automatic: true);
            }
            var state = world.Strategic.CharacterEncounter;
            if (state == null) return;
            // Restored domain state is stable, but its presentation must finish before tactics/end.
            if (_restoreState != null) return;
            // Domain Active means the field is legally bound. Host ReadyToStart still owns the
            // tactical start gate, so no target selection, elapsed time, intervention or damage
            // may advance even if another system accidentally changes the effective pause.
            if (Phase == PresentationPhase.ReadyToStart) return;
            if (state.Phase == CharacterEncounterPhase.Committed) { Phase = PresentationPhase.Report; return; }
            if (state.Phase == CharacterEncounterPhase.ReadyToEnd)
            {
                Phase = PresentationPhase.ReadyToEnd;
                EnterReadyToEndOnce(world, state);
                if (_host.Session.IsPaused)
                    return;
                _host.ContinuousOutdoorSurfaceRuntime.CaptureIndependentField();
                if (!_host.ContinuousOutdoorSurfaceRuntime.MaintainIndependentEncounterViews())
                    return;
                var readyDeltaTime = _host.PresentationDeltaTime;
                CharacterEncounterService.AdvanceReadyToEnd(world, readyDeltaTime, Time.unscaledDeltaTime);
                TickReadyToEndManualAttack(world, state);
                _host.GetComponent<HostPlayerPartyController>()?.RefreshActiveControlAfterLifeStateChange();
                _host.DispatchDrainedEvents();
                return;
            }
            if (!CanAdvanceTacticalPresentation(Phase, _host.Session.IsPaused)) return;
            _host.ContinuousOutdoorSurfaceRuntime.CaptureIndependentField();
            if (state.Phase == CharacterEncounterPhase.Active)
            {
                if (!_host.ContinuousOutdoorSurfaceRuntime.MaintainIndependentEncounterViews())
                    return;
                var dt = _host.PresentationDeltaTime;
                if (!_loggedTacticalClock)
                {
                    Debug.Log("[CharacterEncounter] Id=" + state.EncounterId + " tacticalDeltaTime=" + dt);
                    _loggedTacticalClock = true;
                }
                if (SiteCoreEncounterDefenseService.AdvanceAndStrike(world, dt,
                        out var coreTarget, out var coreDefeated))
                {
                    var ward = state.SiteCoreDefense;
                    if (ward != null && _strikeVfx != null &&
                        _host.ViewSpawner.Registry.TryGet(coreTarget, out var coreTargetView) &&
                        coreTargetView != null &&
                        _host.ContinuousOutdoorSurfaceRuntime.TryWorldToPresentation(
                            new WorldVec2((ward.MinX + ward.MaxX) * .5f,
                                (ward.MinY + ward.MaxY) * .5f), out var from))
                    {
                        _strikeVfx.PlayProjectile(from + Vector3.up * .35f,
                            coreTargetView.transform.position + Vector3.up * .35f, coreTargetView);
                    }
                    if (coreDefeated) RefreshDefeatedParticipant(coreTarget);
                }
                foreach (var p in state.Participants)
                {
                    if (!CharacterEncounterService.IsLiving(world, p.CharacterId)) continue;
                    var id = new EntityId(p.CharacterId);
                    p.Cooldown = Mathf.Max(0f, p.Cooldown - dt);
                    if (MortalTransportConstraintService.ValidateAttack(world, id).IsFailure)
                    { p.TargetId = 0; continue; }
                    if (p.TargetId == ulong.MaxValue && _host.Session.PlayerParty.ActiveCharacterId == id) continue;
                    if (!state.Opposing(p.CharacterId, p.TargetId) || !CharacterEncounterService.IsLiving(world, p.TargetId))
                    {
                        p.TargetId = 0; var nearest = float.PositiveInfinity;
                        foreach (var other in state.Participants)
                            if (other.Enemy != p.Enemy && CharacterEncounterService.IsLiving(world, other.CharacterId))
                            {
                                var distance = Mathf.Abs(other.TacticalX - p.TacticalX) + Mathf.Abs(other.TacticalY - p.TacticalY);
                                if (distance < nearest) { nearest = distance; p.TargetId = other.CharacterId; }
                            }
                    }
                    var target = new EntityId(p.TargetId);
                    if (target.IsNone || !_host.ViewSpawner.Registry.TryGet(id, out var actorView) || actorView == null ||
                        !_host.ViewSpawner.Registry.TryGet(target, out var targetView) || targetView == null) continue;
                    world.Entities.TryGet(id, out var actorEntity);
                    _veil.TryAutoActivateForNonPlayer(world, id);
                    _veil.DeactivateIfSpiritEmpty(actorEntity);
                    var range = _veil.ResolveEngageRange(actorEntity);
                    if (Vector2.Distance(actorView.transform.position, targetView.transform.position) > range ||
                        !_host.MoveController.IsCharacterStandClear(id))
                    {
                        if (p.Cooldown <= 0f)
                        {
                            _host.MoveController.OrderCharacterApproach(id, target, range);
                            p.Cooldown = .2f;
                        }
                        continue;
                    }
                    _host.MoveController.CancelPresentationMovementPublic(id);
                    if (p.Cooldown > 0f || !state.Opposing(id.Value, target.Value)) continue;
                    p.Cooldown = MeleeCombatService.DefaultMeleeIntervalSeconds;
                    var strike = _melee.ApplyStrike(
                        world, id, target, out _, out var defenderDefeated);
                    if (strike.IsSuccess)
                        PlayStrikePresentation(id, target, actorView, targetView,
                            SpiritVeilService.IsActive(actorEntity));
                    if (strike.IsSuccess && defenderDefeated)
                        RefreshDefeatedParticipant(target);
                }
                var previousCount = state.Participants.Count;
                CharacterEncounterService.Advance(
                    world, dt, Time.unscaledDeltaTime,
                    _host.ContinuousOutdoorSurfaceRuntime.PrepareInterventionPlacement);
                if (state.Participants.Count != previousCount)
                    _host.ContinuousOutdoorSurfaceRuntime.PresentJoinedParticipants(previousCount);
                _host.GetComponent<HostPlayerPartyController>()?.RefreshActiveControlAfterLifeStateChange();
                _host.DispatchDrainedEvents();
            }
        }

        void EnterReadyToEndOnce(SimulationWorld world, CharacterEncounterState state)
        {
            if (state == null || string.Equals(_readyToEndEncounterId, state.EncounterId,
                    StringComparison.Ordinal))
                return;
            _readyToEndEncounterId = state.EncounterId;
            for (var i = 0; i < state.Participants.Count; i++)
            {
                var participant = state.Participants[i];
                if (participant.TargetId == ulong.MaxValue && state.Objective != null && !state.Objective.Resolved) continue;
                participant.TargetId = 0;
                _host.MoveController.CancelPresentationMovementPublic(
                    new EntityId(participant.CharacterId));
            }
            Debug.Log(_host.ContinuousOutdoorSurfaceRuntime.DescribeReadyToEndDiagnostics(state), this);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogReturnDiagnostics(world, state, "ReadyToEnd");
#endif
        }

        void TickReadyToEndManualAttack(
            SimulationWorld world,
            CharacterEncounterState state)
        {
            var attackerId = _host.Session.PlayerParty.ActiveCharacterId;
            var attacker = state.Find(attackerId.Value);
            if (attacker == null || attacker.TargetId == 0 || attacker.TargetId == ulong.MaxValue ||
                !world.Entities.TryGet(attackerId, out var attackerEntity) ||
                !CombatLifeStateService.CanFight(attackerEntity) ||
                MortalTransportConstraintService.ValidateAttack(world, attackerId).IsFailure)
                return;
            var targetId = new EntityId(attacker.TargetId);
            if (!state.Opposing(attackerId.Value, targetId.Value) ||
                !world.Entities.TryGet(targetId, out var targetEntity) ||
                !CombatLifeStateService.CanBeAttacked(targetEntity))
            {
                attacker.TargetId = 0;
                return;
            }
            if (!_host.ViewSpawner.Registry.TryGet(attackerId, out var attackerView) || attackerView == null ||
                !_host.ViewSpawner.Registry.TryGet(targetId, out var targetView) || targetView == null)
                return;
            var range = _veil.ResolveEngageRange(attackerEntity);
            if (Vector2.Distance(attackerView.transform.position, targetView.transform.position) > range ||
                !_host.MoveController.IsCharacterStandClear(attackerId))
            {
                if (attacker.Cooldown <= 0f)
                {
                    _host.MoveController.OrderCharacterApproach(
                        attackerId, targetId, range,
                        completionPolicy: HostMoveCompletionPolicy.PreserveCurrentCommand);
                    attacker.Cooldown = .2f;
                }
                return;
            }
            _host.MoveController.CancelPresentationMovementPublic(attackerId);
            if (attacker.Cooldown > 0f)
                return;
            attacker.Cooldown = MeleeCombatService.DefaultMeleeIntervalSeconds;
            var strike = _melee.ApplyStrike(
                world, attackerId, targetId, out _, out var defenderDefeated);
            if (strike.IsSuccess)
                PlayStrikePresentation(attackerId, targetId, attackerView, targetView,
                    SpiritVeilService.IsActive(attackerEntity));
            if (strike.IsSuccess && defenderDefeated)
                RefreshDefeatedParticipant(targetId);
            if (!CombatLifeStateService.CanBeAttacked(targetEntity))
                attacker.TargetId = 0;
        }

        void PlayStrikePresentation(EntityId attacker, EntityId target,
            EntityView actorView, EntityView targetView, bool ranged)
        {
            if (ranged && _strikeVfx != null)
            {
                _strikeVfx.PlayRangedBetween(_host.ViewSpawner, attacker, target);
                return;
            }
            actorView.PlayArtAttack(targetView.transform.position, ranged);
        }

        void RefreshDefeatedParticipant(EntityId id)
        {
            if (_host?.Session?.World == null || id.IsNone)
                return;
            if (_host.ViewSpawner.Registry.TryGet(id, out var view) && view != null)
                _host.ContinuousOutdoorSurfaceRuntime
                    ?.TryCaptureIndependentParticipantPosition(id, view.transform.position);
            HostLifeStatePresentationSync.RefreshDownedOrDead(
                _host.Session.World,
                _host.ViewSpawner,
                _host.MoveController,
                id,
                captureOrdinaryPlacement: false);
        }

        public void CancelPreparation()
        {
            MortalCivilianService.CancelCaptureRequest(_host?.Session?.World, _pendingAttacker, _pendingTarget);
            if (_entryRoutine != null) StopCoroutine(_entryRoutine);
            _host?.ContinuousOutdoorSurfaceRuntime?.CancelPreparedIndependentField();
            _host?.Session?.ReleaseModalPause(PauseOwner);
            HostInputGate.EncounterModalLock = false;
            _pendingAttacker = _pendingTarget = EntityId.None; _onEntered = null; _entryRoutine = null; _preparedField = null;
            _restoreState = null; _assaultSiteId = null; _allowPendingCancel = false;
            _startGate.Clear(); Phase = PresentationPhase.None;
        }

        void OnDisable() => CancelPreparation();

        public bool TryFinishBattle()
        {
            var world = _host?.Session?.World;
            if (world == null) return false;
            var state = world.Strategic.CharacterEncounter;
            if (state == null || state.Phase != CharacterEncounterPhase.ReadyToEnd) return false;
            _host.ContinuousOutdoorSurfaceRuntime.CaptureIndependentField();
            var anchors = _host.ContinuousOutdoorSurfaceRuntime.PrepareEncounterReturn(state);
            if (anchors.IsFailure) { _failure = anchors.Error.Message; return false; }
            var result = CharacterEncounterService.CommitAndReturn(world);
            if (result.IsFailure) { _failure = result.Error.Message; return false; }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogReturnDiagnostics(world, state, "AfterCommit");
#endif
            // CommitAndReturn clears the encounter-owned participant scope. Re-evaluate Active
            // control in that ordinary scope before Continuous startup asks for its EntityView;
            // waiting for the next HostPlayerPartyController.Update leaves a one-frame stale
            // ActiveCharacterId/ControlState and makes the synchronous startup invariant fail.
            _host.GetComponent<HostPlayerPartyController>()?.RefreshActiveControlAfterLifeStateChange();
            _host.ContinuousOutdoorSurfaceRuntime.LeaveIndependentField(state);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogReturnDiagnostics(world, state, "AfterLeaveIndependentField");
#endif
            Phase = PresentationPhase.Report;
            // A party with no eligible controller may only transfer authority after
            // CommitAndReturn and after the independent field has released the old battlefield.
            _host.GetComponent<HostPlayerPartyController>()?.RefreshActiveControlAfterLifeStateChange();
            return true;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        void LogReturnDiagnostics(
            SimulationWorld world,
            CharacterEncounterState state,
            string stage)
        {
            var independentPhase = state.Phase == CharacterEncounterPhase.Active ||
                                   state.Phase == CharacterEncounterPhase.ReadyToEnd;
            foreach (var participant in state.Participants)
            {
                var id = new EntityId(participant.CharacterId);
                world.Entities.TryGet(id, out var entity);
                var life = entity != null && entity.TryGet<LifecycleComponent>(out var lifecycle)
                    ? lifecycle.State.ToString()
                    : "Missing";
                world.WorldPresence.TryGet(id, out var presence);
                var actualMode = presence != null ? presence.Mode.ToString() : "Missing";
                var actualPosition = presence != null && presence.HasContinuousWorldPosition
                    ? "(" + presence.WorldPosX.ToString("0.###") + "," +
                      presence.WorldPosY.ToString("0.###") + ")"
                    : "None";
                var squadId = world.Strategic.Squads.TryGetForCharacter(id, out var squad) &&
                              squad != null
                    ? squad.SquadId
                    : string.Empty;
                var resolved = CharacterWorldPresenceQuery.TryResolve(world, id, out var location);
                var resolvedPosition = resolved && location.HasWorldPosition
                    ? "(" + location.WorldPosition.X.ToString("0.###") + "," +
                      location.WorldPosition.Y.ToString("0.###") + ")"
                    : "None";
                var materialized = world.ContinuousOutdoorMaterialization.IsMaterialized(id);
                var hasView = _host.ViewSpawner.Registry.TryGet(id, out var view) && view != null;
                var activeView = hasView && view.gameObject.activeInHierarchy;
                bool visibilityEligible;
                string visibilityReason;
                if (independentPhase)
                    visibilityEligible = LocalMapVisibility.EvaluateIndependentEncounterVisibility(
                        world, id, out visibilityReason);
                else
                    visibilityEligible = LocalMapVisibility.EvaluateContinuousMaterializedVisibility(
                        world, id, out visibilityReason);
                var visible = visibilityEligible && activeView;
                if (visibilityEligible && !hasView)
                    visibilityReason = "EntityViewMissing";
                else if (visibilityEligible && !activeView)
                    visibilityReason = "EntityViewInactive";
                Debug.Log(
                    "[CharacterEncounterReturn] Stage=" + stage +
                    " EncounterId=" + state.EncounterId +
                    " Phase=" + state.Phase +
                    " EntityId=" + participant.CharacterId +
                    " Name=" + (entity?.DisplayName ?? string.Empty) +
                    " LifeState=" + life +
                    " SourceOwner=" + participant.SourceSpatialOwnerKind +
                    " SquadId=" + squadId +
                    " FrozenReturnWorldPosition=(" +
                        participant.ReturnX.ToString("0.###") + "," +
                        participant.ReturnY.ToString("0.###") + ")" +
                    " ActualMode=" + actualMode +
                    " ActualSiteId=" + (presence?.SiteId ?? string.Empty) +
                    " ActualSurface=" + (presence?.PersonalSurfaceId ?? string.Empty) +
                    " ActualWorldPosition=" + actualPosition +
                    " ResolvedOwner=" + (resolved ? location.State.ToString() : "Unresolved") +
                    " ResolvedSiteId=" + (resolved ? location.SiteId : string.Empty) +
                    " ResolvedSurface=" + (resolved ? location.SurfaceId : string.Empty) +
                    " ResolvedWorldPosition=" + resolvedPosition +
                    " Origin=(" + participant.OriginX.ToString("0.###") + "," +
                    participant.OriginY.ToString("0.###") + ")" +
                    " Return=(" + participant.ReturnX.ToString("0.###") + "," +
                    participant.ReturnY.ToString("0.###") + ")" +
                    " FinalTactical=(" + participant.TacticalX.ToString("0.###") + "," +
                    participant.TacticalY.ToString("0.###") + ")" +
                    " Materialized=" + materialized +
                    " View=" + hasView +
                    " ViewActive=" + activeView +
                    " Visible=" + visible +
                    " VisibilityReason=" + visibilityReason,
                    this);
            }
        }
#endif

        public void CloseReport()
        {
            var world = _host?.Session?.World;
            var state = world?.Strategic?.CharacterEncounter;
            if (state == null || state.Phase != CharacterEncounterPhase.Committed) return;
            _host.GetComponent<HostCombatSkillBar>()?.RestoreEncounterCooldowns(state);
            CharacterEncounterService.CloseReport(world);
            _readyToEndEncounterId = string.Empty;
            ClearStartStaging();
            Phase = PresentationPhase.None; _progress = string.Empty;
        }

        void ClearStartStaging()
        {
            _startGate.Clear();
            _pendingAttacker = _pendingTarget = EntityId.None;
            _onEntered = null;
            _assaultSiteId = null;
            _allowPendingCancel = false;
        }
    }
}
