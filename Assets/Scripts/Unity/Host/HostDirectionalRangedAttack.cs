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
        int _structureFrame = -1;
        string _structureKey;
        EntityId _structureActor;

        public bool ConsumeStructureSwing(EntityId actor, string key)
        {
            if (_structureFrame != Time.frameCount || _structureActor != actor || _structureKey != key) return false;
            _structureFrame = -1;
            return true;
        }

        bool PrepareStructureSwing(EntityId actor, Vector3 from, Vector3 direction, float range)
        {
            string key = null; Vector3 point = default;
            var wall = _host.GetComponent<HostDestructibleAssault>()?.GetTargetForAttacker(actor);
            if (wall != null && wall.IsWall && !wall.IsDestroyed)
            {
                key = wall.PlacementId;
                point = HostRpgControls.InteractionPoint(new WorldObjectInteractionTarget(
                    WorldObjectTargetKind.Destructible, destructible: wall), from);
            }
            else
            {
                var coreId = _host.GetComponent<HostControlCoreAssault>()?.TargetWorkAreaId;
                if (!string.IsNullOrEmpty(coreId) && _world.ControlCores.TryGet(coreId, out var core) &&
                    HostControlCoreQuery.TryGetFootprint(_world, null, _host.ContinuousOutdoorSurfaceRuntime, core,
                        out var minX, out var maxX, out var minY, out var maxY, out _, out _))
                {
                    key = coreId; point = new Vector3(Mathf.Clamp(from.x, minX, maxX), Mathf.Clamp(from.y, minY, maxY), from.z);
                }
                else
                {
                    var flagId = _host.GetComponent<HostFactionFlagAssault>()?.TargetFlagId;
                    if (!string.IsNullOrEmpty(flagId) && _world.Strategic.FactionFlags.Flags.TryGetValue(flagId, out var flag) &&
                        HostFactionFlagQuery.TryGetCenter(flag, _host.ContinuousOutdoorSurfaceRuntime, out point)) key = flagId;
                }
            }
            if (key == null) return false;
            var delta = point - from; delta.z = 0;
            if (delta.magnitude > range || delta.sqrMagnitude > .01f && Vector3.Dot(direction, delta.normalized) < .5f ||
                !_host.MoveController.HasProjectileSight(from, point - delta.normalized * Mathf.Min(.3f, delta.magnitude))) return false;
            _structureKey = key; _structureActor = actor; _structureFrame = Time.frameCount;
            return true;
        }

        void Awake()
        {
            _host = GetComponent<PlayableHostBootstrap>();
            _party = GetComponent<HostPlayerPartyController>();
            _vfx = GetComponent<HostMeleeStrikeVfx>();
        }

        public static bool OwnsAttack(PlayableHostBootstrap host, EntityId actor) =>
            host?.Session?.PlayerParty?.ActiveCharacterId == actor &&
            (HostRpgControls.ManualActor(host, actor) ||
             DirectionalRangedAttackService.CanFire(host.Session.World, actor));

        public void RequireFreshPress()
        { _needsRelease = true; _structureFrame = -1; _cooldown = Mathf.Max(_cooldown, MeleeCombatService.DefaultMeleeIntervalSeconds); ResetAim(); }

        public static bool ClaimsPointer(PlayableHostBootstrap host)
        {
            if (host?.Session == null || host.Session.IsPaused || HostInputGate.BlockWorldInteraction ||
                host.Session.World.ContentEvents.HasActive ||
                host.ContentInterrupt != null && host.ContentInterrupt.HasBlockingInterrupt ||
                host.WorldMapPanel != null && host.WorldMapPanel.IsOpen ||
                host.GetComponent<HostWorkTargetMode>()?.IsActive == true) return false;
            return OwnsAttack(host, host.Session.PlayerParty.ActiveCharacterId) &&
                (HostRpgControls.SceneRts || HostRpgControls.InCombat(host));
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
                _cooldown = Mathf.Max(_cooldown, MeleeCombatService.DefaultMeleeIntervalSeconds);
                _needsRelease = true;
                _manualControl = false;
            }
            if (_host?.Session == null) return;
            var ownsAttack = OwnsAttack(_host, actor) &&
                (HostRpgControls.SceneRts || HostRpgControls.InCombat(_host));
            if (ownsAttack && !_manualControl)
            {
                _host.MoveController?.CancelPresentationMovementPublic(actor);
                if (!HostRpgControls.SceneRts) _host.GetComponent<HostHousingAreaSelection>()?.Clear();
            }
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
            view.SetPointerFacing(direction);
            if (world.Entities.TryGet(actor, out var aimingEntity) && SpiritVeilService.IsActive(aimingEntity))
                view.SetRangedAim(direction);
            else view.ClearRangedAim();
            if (!Input.GetMouseButton(0) || _cooldown > 0f) return;
            if (!world.Entities.TryGet(actor, out var attackingEntity) ||
                !CombatLifeStateService.CanFight(attackingEntity) ||
                XianXia.Core.Npc.PartyRestService.BlocksMovement(attackingEntity) ||
                XianXia.Core.Npc.MortalTransportConstraintService.ValidateAttack(world, actor).IsFailure) return;
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
            if (!HostRpgControls.SceneRts && PrepareStructureSwing(actor, view.transform.position, direction,
                    new SpiritVeilService().ResolveEngageRange(attackingEntity)))
            { view.PlayArtAttack(view.transform.position + direction, SpiritVeilService.IsActive(attackingEntity)); return; }
            if (!SpiritVeilService.IsActive(attackingEntity))
            {
                // A swing has a direction and can miss; it never creates an approach order.
                view.PlayArtAttack(view.transform.position + direction);
                EntityView victim = null;
                var nearest = MeleeCombatService.DefaultMeleeRange;
                foreach (var candidate in _host.ViewSpawner.Registry.All)
                {
                    if (candidate == null || !candidate.gameObject.activeInHierarchy ||
                        !DirectionalRangedAttackService.CanHitMelee(world, actor, candidate.EntityId)) continue;
                    var delta = candidate.transform.position - view.transform.position; delta.z = 0;
                    if (delta.magnitude > nearest || Vector3.Dot(direction, delta.normalized) < .5f ||
                        !_host.MoveController.HasCharacterSight(actor, candidate.EntityId)) continue;
                    nearest = delta.magnitude; victim = candidate;
                }
                if (victim != null && new MeleeCombatService().ApplyStrike(world, actor, victim.EntityId,
                        out _, out var downed).IsSuccess)
                {
                    if (participant != null) participant.TargetId = victim.EntityId.Value;
                    victim.PlayHitFlash(.12f);
                    if (downed) HostLifeStatePresentationSync.RefreshDownedOrDead(world,
                        _host.ViewSpawner, _host.MoveController, victim.EntityId, captureOrdinaryPlacement: encounter == null);
                    _party?.RefreshActiveControlAfterLifeStateChange();
                    _host.DispatchDrainedEvents();
                    HostAudioFeedback.Play(gameObject, HostAudioFeedback.Cue.Hit);
                }
                return;
            }
            if (!DirectionalRangedAttackService.CanFire(world, actor)) return;
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
