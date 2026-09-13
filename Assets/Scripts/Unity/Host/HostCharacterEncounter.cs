using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.Results;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>Confirmation, rendering and tactical execution adapter for the domain encounter.</summary>
    public sealed class HostCharacterEncounter : MonoBehaviour
    {
        const string PauseOwner = "CharacterEncounterUI";
        public enum PresentationPhase { None, Pending, Preparing, ReadyToCommit, Active, ReadyToEnd, Report, Failed }
        PlayableHostBootstrap _host;
        SimulationWorld _world;
        EntityId _pendingAttacker, _pendingTarget;
        string _failure = "";
        Action _onEntered;
        readonly SpiritVeilService _veil = new SpiritVeilService();
        readonly MeleeCombatService _melee = new MeleeCombatService();
        Coroutine _entryRoutine;
        ContinuousOutdoorSurfaceRuntime.PreparedIndependentField _preparedField;
        bool _automaticRequest;
        string _progress = string.Empty;
        public PresentationPhase Phase { get; private set; }
        public string Failure => _failure;
        public string Progress => Phase == PresentationPhase.ReadyToCommit &&
            !string.IsNullOrEmpty(_host?.ContinuousOutdoorSurfaceRuntime?.IndependentPreparationProgress)
                ? _host.ContinuousOutdoorSurfaceRuntime.IndependentPreparationProgress : _progress;
        public EntityId PendingAttacker => _pendingAttacker;
        public EntityId PendingTarget => _pendingTarget;
        public bool CanCancel => HasPending && !_automaticRequest;
        public void Bind(PlayableHostBootstrap host) { _host = host; _world = host.Session.World; }
        public bool HasPending => !_pendingTarget.IsNone;

        public void Request(EntityId attacker, EntityId target, bool automatic = false, Action onEntered = null)
        {
            if (_host?.Session?.World == null) return;
            if (_host.Session.World.Strategic.CharacterEncounter != null)
            { SetTarget(attacker, target); return; }
            if (HasPending) return;
            var requestWorld = _host.Session.World;
            if (automatic && CharacterEncounterService.IsContactSuppressed(requestWorld, attacker, target)) return;
            if (!automatic) requestWorld.Strategic.SuppressedCharacterContacts.Remove(CharacterEncounterService.ContactKey(attacker, target));
            _world = _host.Session.World;
            _pendingAttacker = attacker; _pendingTarget = target; _failure = ""; _progress = "等待确认"; _onEntered = onEntered;
            _automaticRequest = automatic;
            Phase = PresentationPhase.Pending;
            HostInputGate.EncounterModalLock = true;
            _host.Session.AcquireModalPause(PauseOwner);
        }

        public void BeginConfirmed()
        {
            if (!HasPending || Phase == PresentationPhase.Preparing || _entryRoutine != null) return;
            _entryRoutine = StartCoroutine(RunEntry(PrepareAndEnter(_pendingAttacker, _pendingTarget)));
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
            var world = _host.Session.World;
            var surface = _host.ContinuousOutdoorSurfaceRuntime;
            if (surface == null || !surface.IsActive) { Fail("Continuous source is unavailable."); yield break; }
            surface.CaptureCurrentPersonalPlacements();
            var prepared = CharacterEncounterService.Prepare(world, attacker, target, surface.ActiveSurfaceId, out var state);
            if (prepared.IsFailure) { Fail(prepared.Error.Message); yield break; }
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
            new XianXia.Core.Social.SocialEventService().RecordCharacterAttacked(world, attacker, target);
            SetTarget(attacker, target);
            _pendingAttacker = _pendingTarget = EntityId.None;
            _preparedField = null; _entryRoutine = null; Phase = PresentationPhase.Active; _progress = "战术接管完成";
            HostInputGate.EncounterModalLock = false;
            _host.Session.ReleaseModalPause(PauseOwner);
            Debug.Log("[CharacterEncounter] Active Id=" + state.EncounterId +
                " manualPaused=" + _host.Session.ManualPaused + " paused=" + _host.Session.IsPaused +
                " inputBlocked=" + HostInputGate.BlockWorldInteraction);
            var action = _onEntered; _onEntered = null;
            action?.Invoke();
        }

        void Fail(string message)
        {
            _failure = message ?? "Encounter preparation failed.";
            _progress = "准备失败";
            _preparedField = null; _entryRoutine = null; Phase = PresentationPhase.Failed;
            _host?.ContinuousOutdoorSurfaceRuntime?.CancelPreparedIndependentField();
            Debug.LogError("[CharacterEncounter] phase=" + Phase + " error=" + _failure);
        }

        public void CancelPending()
        {
            if (!CanCancel) return;
            Debug.Log("[CharacterEncounter] cancel phase=" + Phase);
            if (_entryRoutine != null) StopCoroutine(_entryRoutine);
            _entryRoutine = null;
            _host.ContinuousOutdoorSurfaceRuntime.CancelPreparedIndependentField();
            _host.Session.World.Strategic.SuppressedCharacterContacts.Add(CharacterEncounterService.ContactKey(_pendingAttacker, _pendingTarget));
            _pendingAttacker = _pendingTarget = EntityId.None; _onEntered = null; _automaticRequest = false;
            _preparedField = null; _failure = ""; _progress = string.Empty; Phase = PresentationPhase.None;
            HostInputGate.EncounterModalLock = false;
            _host.Session.ReleaseModalPause(PauseOwner);
        }

        public void SetTarget(EntityId attacker, EntityId target)
        {
            var state = _host?.Session?.World?.Strategic?.CharacterEncounter;
            if (state == null || state.Phase != CharacterEncounterPhase.Active || !state.Opposing(attacker.Value, target.Value)) return;
            state.Find(attacker.Value).TargetId = target.Value;
        }

        public void Stop(EntityId id)
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
                _world = world; _pendingAttacker = _pendingTarget = EntityId.None; _onEntered = null; Phase = PresentationPhase.None;
                _host.Session.ReleaseModalPause(PauseOwner);
                HostInputGate.EncounterModalLock = false;
            }
            if (!world.Strategic.PendingCharacterTarget.IsNone)
            {
                var attacker = world.Strategic.PendingCharacterAttacker;
                var target = world.Strategic.PendingCharacterTarget;
                world.Strategic.PendingCharacterAttacker = world.Strategic.PendingCharacterTarget = EntityId.None;
                Request(attacker, target, automatic: true);
            }
            var state = world.Strategic.CharacterEncounter;
            if (state == null) return;
            if (state.Phase == CharacterEncounterPhase.ReadyToEnd) { Phase = PresentationPhase.ReadyToEnd; return; }
            if (state.Phase == CharacterEncounterPhase.Committed) { Phase = PresentationPhase.Report; return; }
            if (_host.Session.IsPaused) return;
            _host.ContinuousOutdoorSurfaceRuntime.CaptureIndependentField();
            if (state.Phase == CharacterEncounterPhase.Active)
            {
                var dt = _host.PresentationDeltaTime;
                foreach (var p in state.Participants)
                {
                    if (!CharacterEncounterService.IsLiving(world, p.CharacterId)) continue;
                    var id = new EntityId(p.CharacterId);
                    p.Cooldown = Mathf.Max(0f, p.Cooldown - dt);
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
                    if (Vector2.Distance(actorView.transform.position, targetView.transform.position) > _veil.ResolveEngageRange(actorEntity))
                    {
                        if (p.Cooldown <= 0f)
                        {
                            _host.MoveController.OrderEntityToWorldPoint(id, targetView.transform.position, arriveCommand: null, issueStop: false);
                            p.Cooldown = .2f;
                        }
                        continue;
                    }
                    if (p.Cooldown > 0f || !state.Opposing(id.Value, target.Value)) continue;
                    _host.MoveController.CancelPresentationMovementPublic(id);
                    p.Cooldown = MeleeCombatService.DefaultMeleeIntervalSeconds;
                    _melee.ApplyStrike(world, id, target, out _, out _);
                }
                var previousCount = state.Participants.Count;
                CharacterEncounterService.Advance(world, dt, _host.ContinuousOutdoorSurfaceRuntime.PrepareInterventionPlacement);
                if (state.Participants.Count != previousCount)
                    _host.ContinuousOutdoorSurfaceRuntime.PresentJoinedParticipants(previousCount);
                _host.GetComponent<HostPlayerPartyController>()?.RefreshActiveControlAfterLifeStateChange();
                _host.DispatchDrainedEvents();
            }
        }

        void OnDisable()
        {
            if (_entryRoutine != null) StopCoroutine(_entryRoutine);
            _host?.ContinuousOutdoorSurfaceRuntime?.CancelPreparedIndependentField();
            _host?.Session?.ReleaseModalPause(PauseOwner);
            HostInputGate.EncounterModalLock = false;
            _pendingAttacker = _pendingTarget = EntityId.None; _onEntered = null; _entryRoutine = null; _preparedField = null;
        }

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
            _host.ContinuousOutdoorSurfaceRuntime.LeaveIndependentField(state);
            Phase = PresentationPhase.Report;
            return true;
        }

        public void CloseReport()
        {
            var world = _host?.Session?.World;
            var state = world?.Strategic?.CharacterEncounter;
            if (state == null || state.Phase != CharacterEncounterPhase.Committed) return;
            _host.GetComponent<HostCombatSkillBar>()?.RestoreEncounterCooldowns(state);
            CharacterEncounterService.CloseReport(world);
            Phase = PresentationPhase.None; _progress = string.Empty;
        }
    }
}
