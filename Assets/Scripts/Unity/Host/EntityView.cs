using UnityEngine;
using XianXia.Core.Actions;
using XianXia.Core.Combat;
using XianXia.Core.Construction;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Npc;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// 2D Sprite presentation binding for a Core Entity. Read-only sync.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(200)]
    public sealed class EntityView : MonoBehaviour
    {
        // Presentation only. Navigation and character spacing retain their cell-based footprint.
        public const float VisualSizeMultiplier = 2f;

        [SerializeField] SpriteRenderer bodyRenderer;
        [SerializeField] SpriteRenderer selectionRing;
        [SerializeField] TextMesh label;
        BoxCollider2D _bodyCollider;
        [SerializeField] string activityText = string.Empty;
        string _labelDisplay;
        string _labelActivity;
        TextMesh _labelTarget;
        TextMesh _labelRendererTarget;
        MeshRenderer _labelRenderer;
        string _namedDisplay;
        EntityId _namedEntity;

        [Header("Runtime animation diagnostics (updated by the view)")]
        [SerializeField] string animationDriver = "Unconfigured";
        [SerializeField] string animationBindingFailure = string.Empty;
        [SerializeField] HostStepAArt.Profile animationProfile;
        [SerializeField] HostStepAArt.Action animationAction;
        [SerializeField] string locomotionSource = "None";
        [SerializeField] string animationSuppression = string.Empty;
        [SerializeField] float locomotionDistance;
        [SerializeField] float walkPhase;
        [SerializeField] float rigWalkBlend;
        [SerializeField] bool rigBodyVisible;

        SimulationWorld _world;
        EntityId _entityId;
        bool _bound;
        bool _failed;
        Color _baseColor = Color.white;
        bool _highlight;
        float _hitFlashRemaining;
        string _encounterActivity;
        HostStepAArt.Profile _artProfile;
        HostStepAArt.RigSkin _artSkin;
        HostStepAArt.Direction _artDirection = HostStepAArt.Direction.South;
        HostStepAArt.Action _artAction = HostStepAArt.Action.Idle;
        HostMoveController _moveController;
        HostFarmFieldLabor _farmLabor;
        HostDestructibleAssault _destructibleAssault;
        Vector3 _artLastPosition;
        HostCharacterArtState _artState = new HostCharacterArtState();
        PlayableHostSession _artSession;
        Color _lifeColor;
        string _lifeActivity;
        bool _lifeTint;
        SpriteRenderer _sleepRenderer;
        HostBedVisualLayers _restBedVisual;
        float _nextBedVisualRetryTime;
        float _sleepEntryElapsed;
        Vector3 _sleepEntryOrigin;
        const float SleepEntrySeconds = .42f;
        SpriteRenderer _attackRenderer;
        Sprite _fallbackSprite;
        HumanCommonRigView _modularRig;
        bool _useModularRig;
        bool _requestedModularRig;
        bool _runtimeArtConfigured;
        float _nextRigRetryAt;
        string _reportedAnimationBinding;
        const float RigRetrySeconds = 2f;
        HumanCommonRigEquipment _equipmentVisual;
        HumanAttackStyle _attackStyle;
        int _locomotionFrame = -1;
        bool _hasLocomotionSample;
        Vector3 _locomotionDelta;
        float _locomotionDistance;
        float _projectedDistance;
        float _projectedSpeed;
        Vector3 _projectedDirection;
        HostCharacterWorkVisual _workContactVisual;
        float _workContactRemaining;
        bool _pendingWorkContact;
        int _carriedPresentationOrder;
        bool _hasCarriedPresentationOrder;

        public HumanCommonRigEquipment EquipmentVisual => _equipmentVisual;
        Vector3 _rangedAim;
        bool _hasRangedAim;
        public void SetRangedAim(Vector3 direction) { _rangedAim = direction; _hasRangedAim = true; }
        public void ClearRangedAim() { _hasRangedAim = false; }
        Vector3 _pointerFacing;
        int _pointerFacingFrame = -1;
        // A current-frame sample only: switching control cannot leave pointer ownership on the old view.
        public void SetPointerFacing(Vector3 direction) { _pointerFacing = direction; _pointerFacingFrame = Time.frameCount; }

        public Vector3 CarryFacing => ArtDirectionVector(_artDirection);

        public void SetCarriedPresentationOrder(int order)
        {
            _carriedPresentationOrder = order;
            _hasCarriedPresentationOrder = true;
        }

        /// <summary>Only locomotion executors call this; placement, separation and transport do not.</summary>
        public void RecordLocomotion(Vector3 displacement)
        {
            displacement.z = 0f;
            if (!IsFinite(displacement)) return;
            if (!_hasLocomotionSample || Time.frameCount - _locomotionFrame > 1)
            {
                _locomotionDelta = Vector3.zero;
                _locomotionDistance = 0f;
            }
            _locomotionFrame = Time.frameCount;
            _hasLocomotionSample = true;
            _locomotionDelta += displacement;
            _locomotionDistance += displacement.magnitude;
            // Even a blocked local step takes ownership away from a former macro gait.
            ClearProjectedLocomotion();
        }

        /// <summary>Spread an observed macro projection's gait over its tick interval; never moves the entity.</summary>
        public void RecordProjectedLocomotion(Vector3 displacement, float seconds)
        {
            displacement.z = 0f;
            var distance = displacement.magnitude;
            if (!IsFinite(displacement) || distance <= .0001f)
            {
                ClearProjectedLocomotion();
                return;
            }
            _projectedDirection = displacement / distance;
            _projectedDistance = distance;
            _projectedSpeed = distance / Mathf.Max(.05f, seconds);
        }

        public void ClearProjectedLocomotion()
        {
            _projectedDistance = 0f;
            _projectedSpeed = 0f;
            _projectedDirection = Vector3.zero;
        }

        void ClearLocomotionSamples()
        {
            _hasLocomotionSample = false;
            _locomotionFrame = -1;
            _locomotionDelta = Vector3.zero;
            _locomotionDistance = 0f;
        }

        static bool IsFinite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y);

        public bool HasRuntimeArtConfiguration(PlayableHostSession session, bool modular) =>
            _runtimeArtConfigured && _artState != null && ReferenceEquals(_artSession, session) &&
            _requestedModularRig == modular && bodyRenderer != null;

        public bool HasRuntimeArtBinding(PlayableHostSession session, bool modular) =>
            HasRuntimeArtConfiguration(session, modular) &&
            (_artProfile == HostStepAArt.Profile.None || !modular && !_useModularRig ||
             modular && _useModularRig && _modularRig != null && _modularRig.IsReady);

        /// <summary>Repair only the visual binding; retain the current gait/action clocks.</summary>
        public void TryRecoverRuntimeArtBinding()
        {
            if (!_requestedModularRig || _artProfile == HostStepAArt.Profile.None ||
                Time.realtimeSinceStartup < _nextRigRetryAt) return;
            _nextRigRetryAt = Time.realtimeSinceStartup + RigRetrySeconds;
            SetModularRigMode(true);
        }

        [ContextMenu("Log current character animation")]
        void LogCurrentAnimation() => Debug.Log("[CharacterAnimation] Entity=" + _entityId.Value +
            " Driver=" + animationDriver + " Profile=" + animationProfile +
            " Action=" + animationAction + " Source=" + locomotionSource +
            " Distance=" + locomotionDistance.ToString("0.####") +
            " WalkPhase=" + walkPhase.ToString("0.###") + " Suppression=" + animationSuppression +
            " BindingFailure=" + animationBindingFailure, this);

        void ReportAnimationBinding()
        {
            var binding = animationDriver + "|" + _artProfile + "|" + animationBindingFailure;
            if (_reportedAnimationBinding == binding) return;
            _reportedAnimationBinding = binding;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[CharacterAnimationBinding] Entity=" + _entityId.Value +
                " Name=" + (_world != null && _world.Entities.TryGet(_entityId, out var entity)
                    ? entity.DisplayName : gameObject.name) +
                " Driver=" + animationDriver + " Profile=" + _artProfile +
                " BindingFailure=" + animationBindingFailure, this);
#endif
        }

        public EntityId EntityId => _entityId;

        public bool IsBound => _bound && !_failed;

        public bool IsBoundTo(SimulationWorld world, EntityId entityId) =>
            IsBound && ReferenceEquals(_world, world) && _entityId == entityId;

        public bool IsHighlightRequested => _highlight;

        public bool UsesRestBodyBounds => _useModularRig && _modularRig != null && _modularRig.UsesPoseBounds ||
            _sleepRenderer != null && _sleepRenderer.enabled;

        public string ActivityText => activityText;

        /// <summary>Current visible body, excluding names, selection rings, weapons and cargo.</summary>
        public bool TryGetVisibleBodyBounds(out Bounds bounds)
        {
            bounds = default;
            if (!IsBound || !gameObject.activeInHierarchy) return false;
            if (_world != null && _world.Entities.TryGet(_entityId, out var entity) &&
                entity.TryGet<LifecycleComponent>(out var life) && life.IsRemoved) return false;
            if (_useModularRig && _modularRig != null)
                return _modularRig.TryGetVisibleBodyBounds(out bounds);
            var visible = _sleepRenderer != null && _sleepRenderer.enabled ? _sleepRenderer :
                _attackRenderer != null && _attackRenderer.enabled ? _attackRenderer : bodyRenderer;
            if (visible == null || !visible.enabled || visible.sprite == null ||
                !visible.gameObject.activeInHierarchy) return false;
            bounds = visible.bounds;
            return true;
        }

        public bool TryHitVisibleBody(Ray ray, out float distance)
        {
            distance = 0f;
            if (!TryGetVisibleBodyBounds(out var body) || Mathf.Abs(ray.direction.z) < .00001f)
                return false;
            distance = (body.center.z - ray.origin.z) / ray.direction.z;
            if (distance < 0f) return false;
            var point = ray.GetPoint(distance);
            return point.x >= body.min.x && point.x <= body.max.x &&
                   point.y >= body.min.y && point.y <= body.max.y;
        }

        public Vector3 VitalsGroundAnchor
        {
            get
            {
                var foot = transform.position;
                if (!TryGetVisibleBodyBounds(out var body)) return foot;
                if (_restBedVisual != null && _restBedVisual.TryGetBedBounds(out var bed))
                    return new Vector3(bed.center.x, Mathf.Min(bed.min.y, body.min.y), foot.z);
                // Upright rigs are registered to this semantic foot point. Gait and arm
                // bounds must not make the bars bounce; lying/bed poses have a different base.
                if (_useModularRig && _modularRig != null)
                    return _modularRig.UsesPoseBounds
                        ? new Vector3(body.center.x, body.min.y, foot.z) : foot;
                return new Vector3(body.center.x, Mathf.Min(foot.y, body.min.y), foot.z);
            }
        }

        void SyncBodyCollider()
        {
            if (_bodyCollider == null) _bodyCollider = GetComponent<BoxCollider2D>();
            if (_bodyCollider == null || !TryGetVisibleBodyBounds(out var body)) return;
            // This is the existing presentation proxy. Navigation still uses its original
            // walk grid/foot point, with no new rigidbody, pushing or character blocking.
            var center = transform.InverseTransformPoint(body.center);
            var min = transform.InverseTransformPoint(body.min);
            var max = transform.InverseTransformPoint(body.max);
            var offset = new Vector2(center.x, center.y);
            var size = new Vector2(Mathf.Max(.01f, Mathf.Abs(max.x - min.x)),
                Mathf.Max(.01f, Mathf.Abs(max.y - min.y)));
            if (!_bodyCollider.offset.Equals(offset)) _bodyCollider.offset = offset;
            if (!_bodyCollider.size.Equals(size)) _bodyCollider.size = size;
        }

        public void SetHighlight(bool highlighted)
        {
            var newlyHighlighted = highlighted && !_highlight;
            _highlight = highlighted;
            ApplyVisualState();
            // Hidden rings skip bounds/depth work; restore their current placement immediately.
            if (newlyHighlighted) ApplyDepthSort();
        }

        public void SetActivityText(string text)
        {
            activityText = text ?? string.Empty;
            RefreshLabel();
        }

        public void ConfigureRuntimeArt(HostStepAArt.Profile profile, HostMoveController moveController,
            HostFarmFieldLabor farmLabor, HostDestructibleAssault destructibleAssault,
            PlayableHostSession session = null, bool useModularRig = true,
            HostStepAArt.RigSkin skin = null)
        {
            if (_modularRig != null && (_artProfile != profile || _artSkin != skin))
            {
                _modularRig.gameObject.name = "RetiredHumanCommonRig";
                _modularRig.gameObject.SetActive(false);
                Destroy(_modularRig.gameObject);
                _modularRig = null;
                _useModularRig = false;
            }
            _artProfile = profile;
            _artSkin = skin;
            EnsureVisualParts();
            _equipmentVisual = GetComponent<HumanCommonRigEquipment>() ?? gameObject.AddComponent<HumanCommonRigEquipment>();
            _equipmentVisual.ConfigureDefault(profile);
            ClearLocomotionSamples();
            ClearProjectedLocomotion();
            _nextRigRetryAt = 0f;
            _reportedAnimationBinding = null;
            animationBindingFailure = string.Empty;
            _workContactRemaining = 0f;
            _pendingWorkContact = false;
            if (_attackRenderer != null) _attackRenderer.enabled = false;
            _moveController = moveController;
            _farmLabor = farmLabor != null ? farmLabor : moveController != null ? moveController.GetComponent<HostFarmFieldLabor>() : null;
            _destructibleAssault = destructibleAssault != null ? destructibleAssault : moveController != null ? moveController.GetComponent<HostDestructibleAssault>() : null;
            _artLastPosition = transform.position;
            _artSession = session;
            _artState = new HostCharacterArtState();
            _artAction = HostStepAArt.Action.Idle;
            animationProfile = profile;
            animationAction = HostStepAArt.Action.Idle;
            locomotionSource = "None";
            animationSuppression = string.Empty;
            locomotionDistance = 0f;
            walkPhase = 0f;
            if (bodyRenderer != null) bodyRenderer.sprite = _fallbackSprite;
            SetModularRigMode(useModularRig);
            _runtimeArtConfigured = true;
            ApplyVisualState();
        }

        /// <summary>Switches only this EntityView's visual driver; Core identity and position stay put.</summary>
        public void SetModularRigMode(bool enabled)
        {
            _requestedModularRig = enabled;
            rigWalkBlend = 0f;
            rigBodyVisible = false;
            var eligible = _artProfile != HostStepAArt.Profile.None;
            if (!enabled || !eligible)
            {
                _useModularRig = false;
                animationBindingFailure = string.Empty;
                animationDriver = eligible ? "LegacySprite (explicit debug preview)" : "Placeholder (no profile)";
                if (_modularRig != null) _modularRig.gameObject.SetActive(false);
                if (_sleepRenderer == null || !_sleepRenderer.enabled)
                    if (bodyRenderer != null) bodyRenderer.enabled = true;
                ApplyLegacyFrame();
                ReportAnimationBinding();
                return;
            }
            if (_modularRig == null)
                _modularRig = transform.Find("HumanCommonRig")?.GetComponent<HumanCommonRigView>();
            if (_modularRig != null && !_modularRig.IsReady)
            {
                _modularRig.gameObject.name = "RetiredHumanCommonRig";
                _modularRig.gameObject.SetActive(false);
                Destroy(_modularRig.gameObject);
                _modularRig = null;
            }
            if (_modularRig == null)
            {
                var go = new GameObject("HumanCommonRig");
                go.transform.SetParent(transform, false);
                var rig = go.AddComponent<HumanCommonRigView>();
                if (!rig.Initialize(_artProfile, _equipmentVisual, _artSkin))
                {
                    _useModularRig = false;
                    _nextRigRetryAt = Time.realtimeSinceStartup + RigRetrySeconds;
                    animationBindingFailure = rig.InitializationFailureReason;
                    animationDriver = "LegacySprite (rig unavailable; recovery pending)";
                    var failure = animationDriver + "|" + _artProfile + "|" + animationBindingFailure;
                    if (_reportedAnimationBinding != failure)
                        Debug.LogError("[HumanCommonRig] Entity=" + _entityId.Value +
                            " Profile=" + _artProfile + " " + animationBindingFailure +
                            " Retrying visual binding every " + RigRetrySeconds + " real seconds.", this);
                    go.name = "RetiredHumanCommonRig";
                    go.SetActive(false);
                    Destroy(go);
                    if (bodyRenderer != null) bodyRenderer.enabled = true;
                    ApplyLegacyFrame();
                    ReportAnimationBinding();
                    return;
                }
                _modularRig = rig;
            }
            _useModularRig = true;
            animationBindingFailure = string.Empty;
            animationDriver = "HumanCommonRig";
            _modularRig.gameObject.SetActive(true);
            if (_attackRenderer != null) _attackRenderer.enabled = false;
            if (_sleepRenderer != null) _sleepRenderer.enabled = false;
            if (bodyRenderer != null) bodyRenderer.enabled = false;
            ApplyVisualState();
            ReportAnimationBinding();
        }

        void ApplyLegacyFrame()
        {
            if (bodyRenderer == null || _artProfile == HostStepAArt.Profile.None) return;
            var frame = HostStepAArt.CustomStaticSprite(_artSkin) ??
                HostStepAArt.CharacterFrame(_artProfile, _artAction, _artDirection,
                    _artState != null ? _artState.Frame : 0);
            if (frame != null) bodyRenderer.sprite = frame;
        }

        /// <summary>Align the visual stroke with a real successful work contact; never applies damage/yield.</summary>
        public void PlayArtWorkContact(Vector3 targetPosition, HumanWorkMotion motion,
            HumanCommonRigView.Equipment tool)
        {
            if (_world == null || !_world.Entities.TryGet(_entityId, out var entity) ||
                !CombatLifeStateService.CanFight(entity) || IsSleeping(entity) ||
                (_artSession != null && _artSession.IsPaused)) return;
            _workContactVisual = HostCharacterWorkVisual.For(motion, tool);
            _workContactVisual.HasTarget = true;
            _workContactVisual.Target = targetPosition;
            _workContactRemaining = .14f;
            _pendingWorkContact = true;
        }

        /// <summary>Only called when an existing Host strike actually fires; no combat timing authority here.</summary>
        public void PlayArtAttack(Vector3 targetPosition, bool ranged = false, HumanAttackStyle? styleOverride = null)
        {
            if (_artProfile == HostStepAArt.Profile.None ||
                _world == null || !_world.Entities.TryGet(_entityId, out var entity) ||
                !CombatLifeStateService.CanFight(entity) || IsSleeping(entity) ||
                (_artSession != null && _artSession.IsPaused))
                return;
            var delta = targetPosition - transform.position;
            delta.z = 0f;
            if (delta.sqrMagnitude > 0.0001f)
                SetArtDirection(delta, true);
            _attackStyle = styleOverride ?? (ranged ? HumanAttackStyle.Cast :
                _equipmentVisual != null ? _equipmentVisual.AttackStyle : HumanAttackStyle.Unarmed);
            _artState.Strike();
        }

        public bool Bind(SimulationWorld world, EntityId entityId)
        {
            _failed = false;
            _namedDisplay = null;
            _labelTarget = null;
            _world = world;
            _entityId = entityId;

            if (world == null)
                return FailSafe("SimulationWorld is null.");
            if (entityId.IsNone)
                return FailSafe("EntityId is None.");
            if (!world.Entities.TryGet(entityId, out var entity))
                return FailSafe("Core Entity not found: " + entityId);

            _bound = true;
            EnsureVisualParts();
            SyncFromCore(entity);
            ApplyVisualState();
            ApplyDepthSort();
            return true;
        }

        public void Unbind()
        {
            _namedDisplay = null;
            _labelTarget = null;
            _pointerFacingFrame = -1;
            ClearRangedAim();
            if (_flightVisual != null) { Destroy(_flightVisual.gameObject); _flightVisual = null; }
            SetSleepVisual(null, transform.position);
            if (_attackRenderer != null) _attackRenderer.enabled = false;
            _bound = false;
            _failed = false;
            _world = null;
            _entityId = EntityId.None;
            _highlight = false;
            _hitFlashRemaining = 0f;
            activityText = string.Empty;
            _artProfile = HostStepAArt.Profile.None;
            _artSkin = null;
            _artSession = null;
            _runtimeArtConfigured = false;
            _workContactRemaining = 0f;
            _pendingWorkContact = false;
            ClearLocomotionSamples();
            ClearProjectedLocomotion();
            _lifeTint = false;
            _lifeActivity = null;
            _moveController = null;
            _farmLabor = null;
            _destructibleAssault = null;
            _useModularRig = false;
            _hasCarriedPresentationOrder = false;
            if (_modularRig != null) _modularRig.gameObject.SetActive(false);
        }

        HostFlightVisual _flightVisual;
        public Vector3 CombatVisualPoint => transform.position + Vector3.up *
            ((.35f + (_flightVisual != null ? _flightVisual.Height : 0f)) * VisualSizeMultiplier);

        void UpdateFlightVisual(Entity entity)
        {
            var flying = FlightService.IsAirborne(entity);
            if (flying && _flightVisual == null)
            {
                var child = new GameObject("SpiritWings"); child.transform.SetParent(transform, false);
                _flightVisual = child.AddComponent<HostFlightVisual>();
            }
            if (_flightVisual == null) return;
            var height = _flightVisual.Present(flying, _artSession?.IsPaused == true,
                bodyRenderer != null ? bodyRenderer.sortingOrder : 0);
            if (_useModularRig && _modularRig != null) _modularRig.transform.localPosition += Vector3.up * height;
        }

        void LateUpdate()
        {
            using var timing = HostDebugHud.EntityLateUpdate.Auto();
            if (!_bound || _failed)
                return;

            if (_world == null || !_world.Entities.TryGet(_entityId, out var entity))
            {
                FailSafe("Bound Core Entity disappeared: " + _entityId);
                return;
            }

            SyncFromCore(entity);
            using (HostDebugHud.EntitySleep.Auto()) UpdateSleepVisual(entity);
            using (HostDebugHud.EntityArt.Auto()) UpdateRuntimeArt(entity);
            using (HostDebugHud.EntityDepth.Auto()) ApplyDepthSort();
            // Wings must consume this frame's depth, not the preceding position's order.
            UpdateFlightVisual(entity);
            using (HostDebugHud.EntityBounds.Auto()) SyncBodyCollider();
            ApplyHitFlash();
            ApplyRemovedHide(entity);
        }

        void ApplyRemovedHide(Entity entity)
        {
            if (entity == null || !entity.TryGet<LifecycleComponent>(out var life) || !life.IsRemoved)
                return;
            if (bodyRenderer != null)
                bodyRenderer.enabled = false;
            if (_sleepRenderer != null)
                _sleepRenderer.enabled = false;
            if (_attackRenderer != null)
                _attackRenderer.enabled = false;
            if (_modularRig != null)
                _modularRig.gameObject.SetActive(false);
            if (selectionRing != null)
                selectionRing.enabled = false;
            if (label != null)
                label.gameObject.SetActive(false);
            var cols = GetComponents<Collider2D>();
            for (var i = 0; i < cols.Length; i++)
                cols[i].enabled = false;
        }

        void ApplyHitFlash()
        {
            if (bodyRenderer == null)
                return;
            if (_hitFlashRemaining <= 0f)
            {
                bodyRenderer.color = _artProfile != HostStepAArt.Profile.None && !_lifeTint
                    ? Color.white : _lifeTint ? _lifeColor : _baseColor;
                if (_attackRenderer != null) _attackRenderer.color = bodyRenderer.color;
                if (_modularRig != null) _modularRig.SetTint(bodyRenderer.color);
                return;
            }

            // 叠一层偏白，压过阵营底色
            bodyRenderer.color = Color.Lerp(_artProfile != HostStepAArt.Profile.None && !_lifeTint
                ? new Color(1f, .55f, .55f, 1f) : _lifeTint ? _lifeColor : _baseColor, Color.white, 0.78f);
            if (_attackRenderer != null) _attackRenderer.color = bodyRenderer.color;
            if (_modularRig != null) _modularRig.SetTint(bodyRenderer.color);
            if (_artSession == null || !_artSession.IsPaused)
                _hitFlashRemaining = Mathf.Max(0f, _hitFlashRemaining - Time.unscaledDeltaTime);
        }

        /// <summary>
        /// Shared foot depth for actors and environment props.
        /// </summary>
        void ApplyDepthSort()
        {
            var order = HostArtDepth.Order(transform.position.y);
            if (_hasCarriedPresentationOrder && _world != null)
            {
                HostCharacterVisualContext.ResolveCarry(_world, _entityId,
                    out _, out _, out var beingCarried);
                if (beingCarried) order = _carriedPresentationOrder;
                else _hasCarriedPresentationOrder = false;
            }
            if (bodyRenderer != null)
                bodyRenderer.sortingOrder = order;
            if (_modularRig != null && _useModularRig)
            {
                if (_modularRig.TryGetBedDepth(out var bedLayer, out var bedActorOrder))
                    _modularRig.SetDepth(bedActorOrder, _artDirection, bedLayer);
                else
                    _modularRig.SetDepth(order, _artDirection);
            }
            if (_sleepRenderer != null)
            {
                if (_restBedVisual != null && _restBedVisual.TryGetSleepFrame(
                        out _, out _, out var layer, out var bedOrder))
                {
                    _sleepRenderer.sortingLayerID = layer;
                    _sleepRenderer.sortingOrder = bedOrder +
                        (_sleepEntryElapsed < SleepEntrySeconds ? 3 : 1);
                }
                else
                {
                    _sleepRenderer.sortingLayerID = 0;
                    _sleepRenderer.sortingOrder = order;
                }
            }
            if (_attackRenderer != null)
                _attackRenderer.sortingOrder = order;
            UpdateSelectionRing(order);
            if (label != null)
            {
                if (_labelRendererTarget != label)
                {
                    _labelRendererTarget = label;
                    _labelRenderer = label.GetComponent<MeshRenderer>();
                }
                var mr = _labelRenderer;
                if (mr != null)
                {
                    if (_restBedVisual != null && _restBedVisual.TryGetSleepFrame(
                            out var pillow, out _, out var layer, out var bedOrder))
                    {
                        mr.sortingLayerID = layer;
                        mr.sortingOrder = bedOrder + 4;
                        label.transform.position = new Vector3(pillow.x, pillow.y + .62f,
                            label.transform.position.z);
                    }
                    else
                    {
                        mr.sortingLayerID = 0;
                        mr.sortingOrder = order + 1;
                    }
                }
            }
        }

        void UpdateSelectionRing(int defaultOrder)
        {
            if (selectionRing == null || !selectionRing.enabled) return;
            var resting = UsesRestBodyBounds;
            if (resting && selectionRing.sprite != null && TryGetVisibleBodyBounds(out var body))
            {
                // The authoritative root can stay at the bed access point while the
                // presentation body lies on the mattress. Select the actual whole body.
                var visual = selectionRing.transform;
                visual.position = new Vector3(body.center.x, body.center.y, transform.position.z + .1f);
                var spriteSize = selectionRing.sprite.bounds.size;
                var rootScale = transform.lossyScale;
                visual.localScale = new Vector3(
                    (body.size.x + .14f) / Mathf.Max(.0001f, spriteSize.x * Mathf.Abs(rootScale.x)),
                    (body.size.y + .14f) / Mathf.Max(.0001f, spriteSize.y * Mathf.Abs(rootScale.y)), 1f);
                if (_restBedVisual != null && _restBedVisual.TryGetSleepFrame(
                        out _, out _, out var layer, out var bedOrder))
                {
                    selectionRing.sortingLayerID = layer;
                    selectionRing.sortingOrder = bedOrder + 5;
                    return;
                }
                selectionRing.sortingLayerID = 0;
                selectionRing.sortingOrder = defaultOrder + 1;
                return;
            }
            selectionRing.transform.localPosition = new Vector3(0f, -.15f, .1f);
            selectionRing.transform.localScale = Vector3.one * 1.35f;
            selectionRing.sortingLayerID = 0;
            selectionRing.sortingOrder = defaultOrder - 1;
        }

        void ApplyVisualState()
        {
            // Selection = ring only; do not tint the body (colors already encode faction／slot).
            if (bodyRenderer != null)
                bodyRenderer.color = _artProfile != HostStepAArt.Profile.None && !_lifeTint
                    ? Color.white : _lifeTint ? _lifeColor : _baseColor;
            if (_sleepRenderer != null)
                _sleepRenderer.color = _artProfile != HostStepAArt.Profile.None && !_lifeTint
                    ? Color.white : _lifeTint ? _lifeColor : _baseColor;
            if (_modularRig != null)
                _modularRig.SetTint(bodyRenderer != null ? bodyRenderer.color : Color.white);

            if (selectionRing != null)
                selectionRing.enabled = _highlight;
        }

        void SyncFromCore(Entity entity)
        {
            var display = string.IsNullOrEmpty(entity.DisplayName)
                ? entity.Id.ToString()
                : entity.DisplayName;
            if ((entity.Tags & EntityTag.Npc) != 0 &&
                HostNpcInteraction.IsHostileEntity(entity) &&
                !display.Contains("敌对"))
                display = display + "·敌对";

            if (_namedEntity != entity.Id || _namedDisplay != display)
            {
                gameObject.name = "EntityView_" + entity.Id.Value + "_" + display;
                _namedEntity = entity.Id;
                _namedDisplay = display;
            }

            if (string.IsNullOrEmpty(activityText) &&
                entity.TryGet<ActionStateComponent>(out var action) &&
                action.HasActiveAction)
            {
                activityText = "行动中";
            }
            else if (!entity.TryGet<ActionStateComponent>(out action) || !action.HasActiveAction)
            {
                if (activityText == "行动中")
                    activityText = string.Empty;
            }

            var encounterActivity = HostCharacterEncounter.DescribeParticipantActivity(_world, entity.Id);
            if (encounterActivity != null)
                activityText = _encounterActivity = encounterActivity;
            else if (_encounterActivity != null)
            {
                if (activityText == _encounterActivity) activityText = string.Empty;
                _encounterActivity = null;
            }
            RefreshLabel(display);
            ApplyLifeStateVisual(entity);
        }

        void ApplyLifeStateVisual(Entity entity)
        {
            if (entity == null || !entity.TryGet<LifecycleComponent>(out var life))
                return;
            var isCorpse = life.IsDead && entity.TryGet<XianXia.Core.Combat.CorpseComponent>(out _);
            if (isCorpse || life.IsIncapacitated)
            {
                _lifeTint = true;
                var stamped = XianXia.Core.Combat.CombatLifeStateService.FormatLifeStateWithCountdown(
                    _world,
                    entity);
                if (!string.IsNullOrEmpty(stamped) && activityText != stamped)
                    activityText = stamped;
                _lifeActivity = stamped;
                _lifeColor = isCorpse
                    ? new Color(0.35f, 0.32f, 0.30f, 0.85f)
                    : new Color(0.72f, 0.45f, 0.42f, 0.92f);
                ApplyVisualState();
            }
            else if (_lifeTint)
            {
                if ((life.State == LifecycleState.Alive || life.State == LifecycleState.Captured) &&
                    life.LastLifeTransitionReason == "Recovery")
                    SyncRecoveredStandingPosition();
                _lifeTint = false;
                if (activityText == _lifeActivity) activityText = string.Empty;
                _lifeActivity = null;
                ApplyVisualState();
            }
        }

        void SyncRecoveredStandingPosition()
        {
            if (CharacterEncounterService.OwnsParticipantSpatialState(_world, _entityId)) return;
            // Core has stood the patient at a real bed access point. Consume that exact position
            // once, before a new civilian path starts from the obsolete bed-centre view position.
            var host = _moveController != null ? _moveController.GetComponent<PlayableHostBootstrap>() : null;
            var surface = host?.ContinuousOutdoorSurfaceRuntime;
            if (surface == null || !surface.IsActive ||
                !CharacterWorldPresenceQuery.TryResolve(_world, _entityId, out var presence) ||
                !presence.HasWorldPosition || presence.SurfaceId != surface.ActiveSurfaceId) return;
            _moveController.CancelPresentationMovementPublic(_entityId);
            var mover = host.GetComponent<HostNpcScheduleMover>();
            mover?.CancelCivilianIntent(_entityId);
            surface.Mapper.WorldToPresentation(presence.WorldPosition.X, presence.WorldPosition.Y, out var x, out var y);
            transform.position = new Vector3(x, y, transform.position.z);
            _artLastPosition = transform.position;
            mover?.NotifyNpcReleased(_entityId);
        }

        void UpdateRuntimeArt(Entity entity)
        {
            if (_artProfile == HostStepAArt.Profile.None || bodyRenderer == null)
                return;
            if (_artState == null) _artState = new HostCharacterArtState();
            var delta = transform.position - _artLastPosition;
            _artLastPosition = transform.position;
            var paused = _artSession != null && _artSession.IsPaused;
            HostCharacterVisualContext.ResolveCarry(_world, _entityId,
                out var carryingCargo, out var carryingPerson, out var beingCarried);
            var sleeping = IsSleeping(entity);
            var canAct = CombatLifeStateService.CanFight(entity) && !beingCarried;
            var presentationDt = _moveController != null
                ? _moveController.GaitPresentationDeltaTime : Time.unscaledDeltaTime;
            var maxStep = Mathf.Min(8f, Mathf.Max(.5f, (_moveController != null
                ? _moveController.GetEffectiveMoveSpeed(_entityId) : 6f) *
                Mathf.Max(Time.unscaledDeltaTime, presentationDt) * 1.75f + .1f));
            // Consume once, including a source submitted after the previous LateUpdate.
            // A zero sample is meaningful (blocked), so it must not fall back to crowd drift.
            var recordedMove = _hasLocomotionSample && Time.frameCount - _locomotionFrame <= 1;
            var movement = recordedMove ? _locomotionDelta : Vector3.zero;
            var distance = recordedMove ? _locomotionDistance : 0f;
            locomotionSource = recordedMove ? "Locomotion executor" : "None";
            if (!paused && canAct && !sleeping && !recordedMove && _projectedDistance > 0f)
            {
                distance = Mathf.Min(_projectedDistance, _projectedSpeed * Time.unscaledDeltaTime);
                _projectedDistance -= distance;
                movement = _projectedDirection * distance;
                recordedMove = true;
                locomotionSource = "Observed squad projection";
            }
            if (!paused && canAct && !sleeping && !recordedMove && IsFinite(delta) &&
                delta.sqrMagnitude > .00000001f && delta.sqrMagnitude <= maxStep * maxStep &&
                _moveController != null && _moveController.HasActiveLocomotionTarget(_entityId))
            {
                movement = delta;
                distance = delta.magnitude;
                locomotionSource = "Active path observed displacement";
            }
            ClearLocomotionSamples();
            if (!canAct || sleeping) ClearProjectedLocomotion();
            var moved = !paused && canAct && !sleeping && distance > .0001f && IsFinite(movement);
            var work = HostCharacterVisualContext.ResolveWork(_world, entity, _farmLabor, _destructibleAssault);
            if (CharacterEncounterService.OwnsParticipantSpatialState(_world, _entityId))
            { _pendingWorkContact = false; _workContactRemaining = 0f; }
            if ((work.Motion == HumanWorkMotion.BoardPlace || work.Motion == HumanWorkMotion.BoardThink) &&
                ChessRecreationService.IsPlaying(_world, _entityId, out _, out var boardDirection))
            { work.HasTarget = true; work.Target = transform.position + new Vector3(boardDirection.X, boardDirection.Y, 0).normalized; }
            if (!canAct || sleeping || carryingCargo || carryingPerson || moved && !_pendingWorkContact)
            {
                _workContactRemaining = 0f;
                _pendingWorkContact = false;
            }
            else if (!work.Active && _workContactRemaining > 0f)
                work = _workContactVisual; // The final real hit can outlive the destroyed target's work session.
            var airborne = FlightService.IsAirborne(entity);
            var aiming = _hasRangedAim && canAct && !sleeping && !carryingCargo && !carryingPerson;
            var pointerFacing = _pointerFacingFrame == Time.frameCount;
            var aimDirection = pointerFacing ? _pointerFacing : _rangedAim;
            if (aiming && !paused) SetArtDirection(aimDirection, true);
            else if (pointerFacing && canAct && !sleeping && !paused)
                SetArtDirection(_pointerFacing, true);
            else if (_pendingWorkContact && !paused && work.HasTarget && !_artState.AttackPending)
                SetArtDirection(work.Target - transform.position, true);
            else if (moved && !paused && !_artState.AttackPending)
                SetArtDirection(movement);
            else if (!paused && !moved && work.HasTarget && !_artState.AttackPending)
            {
                var targetDelta = work.Target - transform.position;
                targetDelta.z = 0f;
                if (targetDelta.sqrMagnitude > .01f) SetArtDirection(targetDelta);
            }
            _artState.Advance(Time.unscaledDeltaTime, paused, canAct, sleeping,
                moved && !airborne, work.Active, moved && !airborne ? distance : 0f, presentationDt,
                _pendingWorkContact && work.Motion == _workContactVisual.Motion);
            _artAction = _artState.Action;
            animationProfile = _artProfile;
            animationAction = _artAction;
            locomotionDistance = moved ? distance : 0f;
            walkPhase = _artState.WalkPhase01;
            animationSuppression = paused ? "Paused" : beingCarried ? "Being carried" :
                !canAct ? "Life state" : sleeping ? "Sleeping" : string.Empty;
            if (!_useModularRig)
            {
                var sprite = HostStepAArt.CustomStaticSprite(_artSkin) ??
                    HostStepAArt.CharacterFrame(_artProfile, _artAction, _artDirection, _artState.Frame);
                if (sprite != null && bodyRenderer.sprite != sprite)
                    bodyRenderer.sprite = sprite;
            }
            if (_useModularRig && _modularRig != null)
            {
                if (entity.TryGet<LifecycleComponent>(out var rigLife))
                    _modularRig.SetLifePose(rigLife.IsIncapacitated, rigLife.IsDead, beingCarried,
                        !FlightService.IsAirborne(entity) && !CombatInjuryService.CanWalk(entity));
                if (_pendingWorkContact && !paused && work.Motion == _workContactVisual.Motion)
                    _modularRig.NotifyWorkContact(work.Motion);
                _modularRig.Apply(_artAction, _artDirection, _artState.WalkPhase01,
                    _artState.Clock, work.Tool, carryingCargo, carryingPerson, work.Motion, _attackStyle,
                    paused ? 0f : Time.unscaledDeltaTime, _artState.IsLocomoting,
                    aiming ? (Vector3?)aimDirection : null, airborne);
                _modularRig.SetMissingLimbs(entity.TryGet<CombatInjuryComponent>(out var injury) ? injury.MissingLimbs : 0);
                if (bodyRenderer != null) bodyRenderer.enabled = false;
                rigWalkBlend = _modularRig.WalkBlend;
                rigBodyVisible = _modularRig.HasVisibleBody;
            }
            if (!paused)
            {
                _pendingWorkContact = false;
                _workContactRemaining = Mathf.Max(0f, _workContactRemaining -
                    Mathf.Min(presentationDt, Time.unscaledDeltaTime * 2f));
            }
            UpdateAttackVisual();
        }

        void UpdateAttackVisual()
        {
            if (_useModularRig)
            {
                if (_attackRenderer != null) _attackRenderer.enabled = false;
                return;
            }
            var attacking = _artAction == HostStepAArt.Action.Attack && bodyRenderer.sprite != null;
            if (!attacking)
            {
                if (_attackRenderer != null) _attackRenderer.enabled = false;
                return;
            }
            if (_attackRenderer == null)
            {
                var child = new GameObject("AttackVisual");
                child.transform.SetParent(transform, false);
                _attackRenderer = child.AddComponent<SpriteRenderer>();
            }
            var direction = ArtDirectionVector(_artDirection);
            _attackRenderer.sprite = bodyRenderer.sprite;
            _attackRenderer.sortingLayerID = bodyRenderer.sortingLayerID;
            _attackRenderer.transform.localPosition = direction * (.08f * Mathf.Sin(
                Mathf.Clamp01(_artState.Clock / .36f) * Mathf.PI));
            _attackRenderer.enabled = true;
            bodyRenderer.enabled = false;
        }

        bool IsActiveCombatParticipant(Entity entity)
        {
            if (_world == null || !CombatLifeStateService.CanFight(entity)) return false;
            var encounter = _world.Strategic.CharacterEncounter;
            return encounter != null &&
                   (encounter.Phase == CharacterEncounterPhase.Active ||
                    encounter.Phase == CharacterEncounterPhase.ReadyToEnd) &&
                   encounter.Find(_entityId.Value) != null;
        }

        bool IsSleeping(Entity entity)
        {
            // A live encounter participant must stand and play its real attack frames even
            // before the next civilian tick releases a stale bed/sleep state.
            if (IsActiveCombatParticipant(entity)) return false;
            if (_world.Civilians.TryGet(_entityId, out var civilian) &&
                (civilian.SleepPhase == CivilianSleepPhase.SleepingOnGround ||
                 civilian.SleepPhase == CivilianSleepPhase.SleepingAtResidence)) return true;
            return entity.TryGet<ActionStateComponent>(out var state) && state.HasActiveAction &&
                _world.ActiveActions.TryGetValue(state.ActiveActionId, out var active) &&
                (active is BedRestAction rest && rest.Status == ActionStatus.Running ||
                 active is RestAction ground && ground.Status == ActionStatus.Running);
        }

        void UpdateSleepVisual(Entity entity)
        {
            if (_world == null || _artProfile == HostStepAArt.Profile.None || bodyRenderer == null ||
                entity == null || !entity.TryGet<LifecycleComponent>(out var life) ||
                life.IsRemoved || life.IsDead)
            {
                SetSleepVisual(null, transform.position);
                return;
            }
            HostCharacterVisualContext.ResolveCarry(_world, _entityId, out _, out _, out var beingCarried);
            if (beingCarried)
            {
                SetSleepVisual(null, transform.position);
                return;
            }
            if (IsActiveCombatParticipant(entity))
            {
                SetSleepVisual(null, transform.position);
                return;
            }
            var sprite = _useModularRig ? null : HostStepAArt.SleepSprite(_artProfile, _artSkin);
            if (life.IsIncapacitated)
            {
                // Rescue can legally place an incapacitated person in an ordinary bed too.
                // Consume that existing occupancy without assigning beds or granting treatment.
                if (TryGetValidOccupiedRestBed(out var occupiedBed) && TryApplyBedRest(sprite, occupiedBed))
                    return;
                // The modular rig owns its downed pose. The legacy sprite driver must also
                // display a lying body rather than merely stop on its standing idle frame.
                SetSleepVisual(sprite, transform.position, !_useModularRig);
                return;
            }
            var ground = _world.Civilians.TryGet(_entityId, out var civilian) &&
                civilian.SleepPhase == CivilianSleepPhase.SleepingOnGround;
            var residence = civilian != null &&
                civilian.SleepPhase == CivilianSleepPhase.SleepingAtResidence;
            var bedAction = false;
            if (entity.TryGet<ActionStateComponent>(out var state) && state.HasActiveAction &&
                _world.ActiveActions.TryGetValue(state.ActiveActionId, out var active))
            {
                bedAction = active is BedRestAction rest && rest.Status == ActionStatus.Running;
                ground |= active is RestAction restGround && restGround.Status == ActionStatus.Running;
            }
            if (!ground && !residence && !bedAction)
            {
                SetSleepVisual(null, transform.position);
                return;
            }
            if (sprite == null && !_useModularRig)
            {
                SetSleepVisual(null, transform.position);
                return;
            }
            if (ground)
            {
                SetSleepVisual(sprite, transform.position, true);
                return;
            }
            // Bed visuals are presentation-only. Occupancy, access point and Core position stay unchanged.
            if (!TryGetValidOccupiedRestBed(out var bed))
            {
                SetSleepVisual(sprite, transform.position, true);
                return;
            }
            if (TryApplyBedRest(sprite, bed)) return;
            // Preserve sleep at the authoritative foot point if the bed visual is unavailable.
            SetSleepVisual(sprite, transform.position, true);
        }

        bool TryGetValidOccupiedRestBed(out OutdoorConstructedAssetState bed) =>
            HostCharacterVisualContext.TryGetValidOccupiedRestBed(_world, _entityId, out bed);

        bool TryApplyBedRest(Sprite sprite, OutdoorConstructedAssetState bed)
        {
            if (!HostMapObjectRegistry.TryGetBedPlot(bed.StableAssetId, out var plot)) return false;
            var layers = plot.GetComponent<HostBedVisualLayers>();
            if (layers == null || !layers.TryGetSleepFrame(out _, out _, out _, out _))
            {
                // A refreshed bed sprite or temporarily unavailable visual must be able to
                // recover, without reconfiguring every actor frame while it stays unavailable.
                if (Time.realtimeSinceStartup < _nextBedVisualRetryTime) return false;
                _nextBedVisualRetryTime = Time.realtimeSinceStartup + 1f;
                HostBedVisualStyle.Apply(plot.gameObject, _world, bed.StableAssetId);
                layers = plot.GetComponent<HostBedVisualLayers>();
                if (layers == null || !layers.TryGetSleepFrame(out _, out _, out _, out _))
                    return false;
            }
            SetSleepVisual(sprite, plot.transform.position, true, bed.QuarterTurns, true, layers);
            return true;
        }

        void SetSleepVisual(Sprite sprite, Vector3 position, bool sleeping = false,
            int quarterTurns = 0, bool onBed = false, HostBedVisualLayers bedVisual = null)
        {
            _restBedVisual = sleeping && onBed ? bedVisual : null;
            if (_useModularRig && _modularRig != null)
            {
                if (_sleepRenderer != null) _sleepRenderer.enabled = false;
                _modularRig.SetSleepPose(sleeping, position, quarterTurns, onBed, _restBedVisual);
                if (bodyRenderer != null) bodyRenderer.enabled = false;
                if (label != null)
                    label.transform.position = sleeping
                        ? new Vector3(position.x, position.y + .72f * VisualSizeMultiplier, label.transform.position.z)
                        : new Vector3(transform.position.x, transform.position.y + 1.48f * VisualSizeMultiplier,
                            label.transform.position.z);
                return;
            }
            if (sprite == null)
            {
                _sleepEntryElapsed = 0f;
                if (_sleepRenderer != null) _sleepRenderer.enabled = false;
                if (bodyRenderer != null) bodyRenderer.enabled = true;
                if (label != null) label.transform.localPosition = new Vector3(0f, 0.95f, -0.1f);
                return;
            }
            if (_sleepRenderer == null)
            {
                var child = new GameObject("SleepVisual");
                child.transform.SetParent(transform, false);
                _sleepRenderer = child.AddComponent<SpriteRenderer>();
                _sleepRenderer.enabled = false;
            }
            if (onBed && !_sleepRenderer.enabled)
            {
                _sleepEntryOrigin = transform.position;
                _sleepEntryElapsed = 0f;
            }
            else if (!onBed)
                _sleepEntryElapsed = SleepEntrySeconds;
            _sleepRenderer.sprite = sprite;
            var sleepTarget = new Vector3(position.x, position.y, transform.position.z);
            if (onBed && _sleepEntryElapsed < SleepEntrySeconds)
            {
                _sleepEntryElapsed = Mathf.Min(SleepEntrySeconds, _sleepEntryElapsed +
                    (_artSession != null && _artSession.IsPaused ? 0f : Time.unscaledDeltaTime));
                var t = Mathf.Clamp01(_sleepEntryElapsed / SleepEntrySeconds);
                t = t * t * (3f - 2f * t);
                sleepTarget = Vector3.Lerp(_sleepEntryOrigin, sleepTarget, t);
            }
            _sleepRenderer.transform.position = sleepTarget;
            _sleepRenderer.enabled = true;
            if (label != null)
                label.transform.position = new Vector3(position.x, position.y + 0.72f * VisualSizeMultiplier,
                    label.transform.position.z);
            bodyRenderer.enabled = false;
        }

        void SetArtDirection(Vector3 delta, bool exact = false)
        {
            if (delta.sqrMagnitude < .0001f) return;
            var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            var sector = (Mathf.RoundToInt(angle / 45f) + 8) % 8;
            var next = sector == 0 ? HostStepAArt.Direction.East :
                sector == 1 ? HostStepAArt.Direction.NorthEast :
                sector == 2 ? HostStepAArt.Direction.North :
                sector == 3 ? HostStepAArt.Direction.NorthWest :
                sector == 4 ? HostStepAArt.Direction.West :
                sector == 5 ? HostStepAArt.Direction.SouthWest :
                sector == 6 ? HostStepAArt.Direction.South : HostStepAArt.Direction.SouthEast;
            if (!exact && next != _artDirection)
            {
                var current = ArtDirectionVector(_artDirection);
                var currentAngle = Mathf.Atan2(current.y, current.x) * Mathf.Rad2Deg;
                if (Mathf.Abs(Mathf.DeltaAngle(currentAngle, angle)) < 28.5f) return;
            }
            _artDirection = next;
        }

        static Vector3 ArtDirectionVector(HostStepAArt.Direction direction)
        {
            switch (direction)
            {
                case HostStepAArt.Direction.North: return Vector3.up;
                case HostStepAArt.Direction.South: return Vector3.down;
                case HostStepAArt.Direction.West: return Vector3.left;
                case HostStepAArt.Direction.NorthEast: return new Vector3(.7071068f, .7071068f);
                case HostStepAArt.Direction.SouthEast: return new Vector3(.7071068f, -.7071068f);
                case HostStepAArt.Direction.SouthWest: return new Vector3(-.7071068f, -.7071068f);
                case HostStepAArt.Direction.NorthWest: return new Vector3(-.7071068f, .7071068f);
                default: return Vector3.right;
            }
        }

        void RefreshLabel(string display = null)
        {
            if (label == null)
                return;
            if (display == null)
            {
                display = _bound && _world != null && _world.Entities.TryGet(_entityId, out var e)
                    ? (string.IsNullOrEmpty(e.DisplayName) ? e.Id.ToString() : e.DisplayName)
                    : string.Empty;
            }

            if (_labelTarget == label && _labelDisplay == display && _labelActivity == activityText)
                return;
            _labelTarget = label;
            _labelDisplay = display;
            _labelActivity = activityText;
            label.text = string.IsNullOrEmpty(activityText)
                ? display
                : display + "\n" + activityText;
        }

        public void SetBaseColor(Color color)
        {
            _baseColor = color;
            ApplyVisualState();
        }

        /// <summary>近战受击闪白（表现层，不改 Core）。</summary>
        public void PlayHitFlash(float seconds = 0.12f)
        {
            _hitFlashRemaining = Mathf.Max(0.04f, seconds);
        }

        void EnsureVisualParts()
        {
            if (bodyRenderer == null)
                bodyRenderer = GetComponent<SpriteRenderer>();
            if (_fallbackSprite == null && bodyRenderer != null)
                _fallbackSprite = bodyRenderer.sprite;
            if (selectionRing == null)
            {
                var ring = transform.Find("SelectionRing");
                if (ring != null)
                    selectionRing = ring.GetComponent<SpriteRenderer>();
            }

            if (label == null)
                label = GetComponentInChildren<TextMesh>();
        }

        bool FailSafe(string reason)
        {
            _bound = false;
            _failed = true;
            Debug.LogError("[EntityView] " + reason, this);
            gameObject.name = "EntityView_FAILED";
            if (bodyRenderer != null)
                bodyRenderer.enabled = false;
            if (_modularRig != null) _modularRig.gameObject.SetActive(false);
            if (_sleepRenderer != null) _sleepRenderer.enabled = false;
            if (_attackRenderer != null) _attackRenderer.enabled = false;
            if (label != null)
                label.text = "ERR";
            return false;
        }
    }
}
