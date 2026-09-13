using System;
using UnityEngine;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>Confirmation, rendering and tactical execution adapter for the domain encounter.</summary>
    public sealed class HostCharacterEncounter : MonoBehaviour
    {
        const string PauseOwner = "CharacterEncounterUI";
        PlayableHostBootstrap _host;
        SimulationWorld _world;
        EntityId _pendingAttacker, _pendingTarget;
        string _failure = "";
        readonly MeleeCombatService _melee = new MeleeCombatService();
        public void Bind(PlayableHostBootstrap host) { _host = host; _world = host.Session.World; }
        public bool HasPending => !_pendingTarget.IsNone;

        public void Request(EntityId attacker, EntityId target)
        {
            if (_host?.Session?.World == null) return;
            if (_host.Session.World.Strategic.CharacterEncounter != null)
            { SetTarget(attacker, target); return; }
            if (HasPending) return;
            _world = _host.Session.World;
            _pendingAttacker = attacker; _pendingTarget = target; _failure = "";
            HostInputGate.BlockWorldInteraction = true;
            _host.Session.AcquireModalPause(PauseOwner);
        }

        public bool BeginConfirmed(EntityId attacker, EntityId target)
        {
            var world = _host.Session.World;
            var surface = _host.ContinuousOutdoorSurfaceRuntime;
            surface.CaptureCurrentPersonalPlacements();
            var prepared = CharacterEncounterService.Prepare(world, attacker, target, surface.ActiveSurfaceId, out var state);
            if (prepared.IsFailure) { _failure = prepared.Error.Message; Debug.LogWarning("[EncounterPrepare] " + _failure); return false; }
            var preflight = surface.PreflightIndependentField(state);
            if (preflight.IsFailure) { _failure = preflight.Error.Message; return false; }
            var begun = CharacterEncounterService.Begin(world, state);
            if (begun.IsFailure) { _failure = begun.Error.Message; return false; }
            var entered = surface.EnterIndependentField(state);
            if (entered.IsFailure)
            {
                _failure = entered.Error.Message;
                Debug.LogError("[EncounterEntry] " + _failure);
                return false;
            }
            SetTarget(attacker, target);
            _pendingAttacker = _pendingTarget = EntityId.None;
            HostInputGate.BlockWorldInteraction = false;
            _host.Session.ReleaseModalPause(PauseOwner);
            return true;
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
                _world = world; _pendingAttacker = _pendingTarget = EntityId.None;
                _host.Session.ReleaseModalPause(PauseOwner);
            }
            var state = world.Strategic.CharacterEncounter;
            if (state == null || state.Phase == CharacterEncounterPhase.Committed || _host.Session.IsPaused) return;
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
                    if (Vector2.Distance(actorView.transform.position, targetView.transform.position) > MeleeCombatService.DefaultMeleeRange)
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
                CharacterEncounterService.Advance(world, dt);
                var party = _host.Session.PlayerParty;
                if (!party.HasActive || !CharacterEncounterService.IsLiving(world, party.ActiveCharacterId.Value))
                    foreach (var id in party.Members)
                        if (state.Find(id.Value) != null && !state.Find(id.Value).Enemy && CharacterEncounterService.IsLiving(world, id.Value))
                        { _host.GetComponent<HostPlayerPartyController>()?.TrySwitchActive(id, out _); break; }
                _host.DispatchDrainedEvents();
            }
        }

        void OnGUI()
        {
            if (_host?.Session?.World == null) return;
            if (HasPending)
            {
                GUILayout.BeginArea(new Rect(Screen.width * .5f - 230, Screen.height * .5f - 100, 460, 230), GUI.skin.box);
                GUILayout.Label("确认人物遭遇：初始双方仅为发起者与目标各自的小队。");
                if (!string.IsNullOrEmpty(_failure)) GUILayout.Label(_failure);
                if (GUILayout.Button("进入独立战场")) BeginConfirmed(_pendingAttacker, _pendingTarget);
                if (GUILayout.Button("取消"))
                { _pendingAttacker = _pendingTarget = EntityId.None; HostInputGate.BlockWorldInteraction = false; _host.Session.ReleaseModalPause(PauseOwner); }
                GUILayout.EndArea(); return;
            }
            var world = _host.Session.World;
            var state = world.Strategic.CharacterEncounter;
            if (state == null) return;
            if (state.Phase == CharacterEncounterPhase.ReadyToEnd)
            {
                if (GUI.Button(new Rect(Screen.width * .5f - 90, 65, 180, 36), "结束战斗"))
                {
                    _host.ContinuousOutdoorSurfaceRuntime.CaptureIndependentField();
                    var result = CharacterEncounterService.CommitAndReturn(world);
                    if (result.IsSuccess)
                    { _host.ContinuousOutdoorSurfaceRuntime.LeaveIndependentField(state); _host.Session.AcquireModalPause(PauseOwner); }
                    else Debug.LogError("[EncounterSettlement] " + result.Error.Message);
                }
            }
            if (state.Phase == CharacterEncounterPhase.Committed)
            {
                GUILayout.BeginArea(new Rect(Screen.width * .5f - 250, 80, 500, Screen.height - 160), GUI.skin.box);
                GUILayout.Label(state.PlayerWon ? "战报：胜利" : "战报：失去战斗能力");
                var report = world.Strategic.ManualBattleSettlement.CommittedReport;
                if (report != null) foreach (var row in report.Participants)
                    GUILayout.Label(row.Name + "  " + row.EntryCondition + " → " + row.FinalCondition + "  HP " + row.EntryHp + " → " + row.FinalHp);
                if (GUILayout.Button("继续"))
                { CharacterEncounterService.CloseReport(world); _host.Session.ReleaseModalPause(PauseOwner); }
                GUILayout.EndArea();
            }
        }
    }
}
