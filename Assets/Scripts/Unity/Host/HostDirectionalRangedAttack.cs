using UnityEngine;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;

namespace XianXia.Unity.Host
{
    /// <summary>Mouse input and visible contact adapter for ADR-0053.</summary>
    [DefaultExecutionOrder(-40)]
    public sealed class HostDirectionalRangedAttack : MonoBehaviour
    {
        PlayableHostBootstrap _host;
        HostPlayerPartyController _party;
        HostMeleeStrikeVfx _vfx;
        EntityView _aimView;
        SimulationWorld _world;
        EntityId _actor;
        float _cooldown;
        int _generation;
        bool _needsRelease = true;
        bool _manualControl;

        void Awake()
        {
            _host = GetComponent<PlayableHostBootstrap>();
            _party = GetComponent<HostPlayerPartyController>();
            _vfx = GetComponent<HostMeleeStrikeVfx>();
        }

        public static bool OwnsAttack(PlayableHostBootstrap host, EntityId actor) =>
            host?.Session?.PlayerParty?.ActiveCharacterId == actor &&
            DirectionalRangedAttackService.CanFire(host.Session.World, actor);

        public static bool ClaimsPointer(PlayableHostBootstrap host)
        {
            if (host?.Session == null || host.Session.IsPaused || HostInputGate.BlockWorldInteraction ||
                host.Session.World.ContentEvents.HasActive ||
                host.ContentInterrupt != null && host.ContentInterrupt.HasBlockingInterrupt ||
                host.WorldMapPanel != null && host.WorldMapPanel.IsOpen ||
                host.GetComponent<HostWorkTargetMode>()?.IsActive == true) return false;
            return OwnsAttack(host, host.Session.PlayerParty.ActiveCharacterId);
        }

        void Update()
        {
            // Bootstrap adds the party controller after this component on a fresh scene.
            if (_party == null) _party = GetComponent<HostPlayerPartyController>();
            var world = _host?.Session?.World;
            var actor = _party != null ? _party.ActiveCharacterId : EntityId.None;
            if (!ReferenceEquals(world, _world) || actor != _actor)
            {
                ResetAim(); _generation++; _world = world; _actor = actor;
                _cooldown = MeleeCombatService.DefaultMeleeIntervalSeconds;
                _needsRelease = true;
                _manualControl = false;
            }
            if (_host?.Session == null) return;
            var ownsAttack = OwnsAttack(_host, actor);
            if (ownsAttack && !_manualControl)
                _host.MoveController?.CancelPresentationMovementPublic(actor);
            _manualControl = ownsAttack;
            if (!_host.Session.IsPaused)
                _cooldown = Mathf.Max(0f, _cooldown - Time.unscaledDeltaTime);
            if (!ClaimsPointer(_host) || HostUiHitTest.ContainsScreenPoint(Input.mousePosition) ||
                Camera.main == null || _host.ViewSpawner == null ||
                !_host.ViewSpawner.Registry.TryGet(actor, out var view) || view == null)
            {
                ResetAim(); _needsRelease = true;
                return;
            }
            if (!Input.GetMouseButton(0)) _needsRelease = false;
            if (_needsRelease) return;
            if (!HostPresentationSpace.TryRaycastPlane(Camera.main, Input.mousePosition, out var mouse))
            { ResetAim(); return; }
            var direction = mouse - view.CombatVisualPoint;
            direction.z = 0f;
            if (direction.sqrMagnitude < .0001f) { ResetAim(); return; }
            direction.Normalize();
            _aimView = view;
            view.SetRangedAim(direction);
            if (!Input.GetMouseButton(0) || _cooldown > 0f) return;
            var participant = world.Strategic?.CharacterEncounter?.Find(actor.Value);
            if (participant != null && participant.Cooldown > 0f) return;
            if (_vfx == null) _vfx = GetComponent<HostMeleeStrikeVfx>();
            if (_vfx == null) return;
            _cooldown = MeleeCombatService.DefaultMeleeIntervalSeconds;
            if (participant != null) participant.Cooldown = _cooldown;
            // An explicit attack interrupts rest/cultivation through the existing action lifecycle.
            if (world.Entities.TryGet(actor, out var shooter) &&
                shooter.TryGet<XianXia.Core.Entities.ActionStateComponent>(out var action) && action.HasActiveAction)
                _host.Session.Loop.StopSubject(actor);
            var generation = _generation;
            var encounter = world.Strategic?.CharacterEncounter;
            var origin = view.CombatVisualPoint;
            view.PlayArtAttack(view.transform.position + direction, true);
            _vfx.PlayDirectionalProjectile(origin, direction, SpiritVeilRules.FoundationRangedEngageRange,
                (from, to) =>
                {
                    if (generation != _generation || !ReferenceEquals(_host.Session?.World, world) ||
                        !ReferenceEquals(world.Strategic?.CharacterEncounter, encounter) ||
                        !_host.ViewSpawner.Registry.TryGet(actor, out var currentShooter) || currentShooter != view ||
                        !OwnsAttack(_host, actor)) return true;
                    EntityView hit = null;
                    var nearest = float.PositiveInfinity;
                    foreach (var candidate in _host.ViewSpawner.Registry.All)
                    {
                        if (candidate == null || !candidate.gameObject.activeInHierarchy || candidate.EntityId == actor) continue;
                        if (HostMeleeStrikeVfx.SweptContact(from, to,
                            candidate.CombatVisualPoint, .42f, out var fraction) &&
                            fraction < nearest &&
                            DirectionalRangedAttackService.CanHit(world, actor, candidate.EntityId))
                        { nearest = fraction; hit = candidate; }
                    }
                    // Use foot-plane coordinates for the same conservative wall/terrain grid as existing attacks.
                    var end = hit != null ? Vector3.Lerp(from, to, nearest) : to;
                    if (_host.MoveController == null || (!FlightService.IsAirborne(world, actor) &&
                        (hit == null || !FlightService.IsAirborne(world, hit.EntityId)) && !_host.MoveController.HasProjectileSight(
                        from - Vector3.up * .35f, end - Vector3.up * .35f))) return true;
                    if (hit == null) return false;
                    if (DirectionalRangedAttackService.ResolveHit(world, actor, hit.EntityId, out var defeated))
                    {
                        var shooterState = encounter?.Find(actor.Value);
                        if (shooterState != null) shooterState.TargetId = hit.EntityId.Value;
                        hit.PlayHitFlash(.12f);
                        if (defeated) HostLifeStatePresentationSync.RefreshDownedOrDead(world,
                            _host.ViewSpawner, _host.MoveController, hit.EntityId, captureOrdinaryPlacement: encounter == null);
                        _party?.RefreshActiveControlAfterLifeStateChange();
                        _host.DispatchDrainedEvents();
                        HostAudioFeedback.Play(gameObject, HostAudioFeedback.Cue.Hit);
                    }
                    return true;
                });
        }

        void ResetAim() { if (_aimView != null) _aimView.ClearRangedAim(); _aimView = null; }
        void OnDisable() { ResetAim(); _generation++; _needsRelease = true; }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (_aimView == null || !ClaimsPointer(_host) || HostUiHitTest.ContainsScreenPoint(Input.mousePosition)) return;
            var point = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            var old = GUI.color;
            GUI.color = new Color(.4f, .9f, 1f, .9f);
            GUI.DrawTexture(new Rect(point.x - 7, point.y - 1, 14, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(point.x - 1, point.y - 7, 2, 14), Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
