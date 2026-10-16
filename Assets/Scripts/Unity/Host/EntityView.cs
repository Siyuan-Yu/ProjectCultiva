using UnityEngine;
using XianXia.Core.Actions;
using XianXia.Core.Combat;
using XianXia.Core.Construction;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Npc;
using XianXia.Core.Simulation;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// 2D Sprite presentation binding for a Core Entity. Read-only sync.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EntityView : MonoBehaviour
    {
        [SerializeField] SpriteRenderer bodyRenderer;
        [SerializeField] SpriteRenderer selectionRing;
        [SerializeField] TextMesh label;
        [SerializeField] string activityText = string.Empty;

        SimulationWorld _world;
        EntityId _entityId;
        bool _bound;
        bool _failed;
        Color _baseColor = Color.white;
        bool _highlight;
        float _hitFlashUntil;
        HostStepAArt.Profile _artProfile;
        HostStepAArt.Direction _artDirection = HostStepAArt.Direction.South;
        HostStepAArt.Action _artAction = HostStepAArt.Action.Idle;
        HostMoveController _moveController;
        HostFarmFieldLabor _farmLabor;
        HostDestructibleAssault _destructibleAssault;
        Vector3 _artLastPosition;
        float _artClock;
        float _artAttackStarted;
        float _artAttackUntil;
        bool _lifeTint;
        SpriteRenderer _sleepRenderer;

        public EntityId EntityId => _entityId;

        public bool IsBound => _bound && !_failed;

        public bool IsBoundTo(SimulationWorld world, EntityId entityId) =>
            IsBound && ReferenceEquals(_world, world) && _entityId == entityId;

        public bool IsHighlightRequested => _highlight;

        public string ActivityText => activityText;

        public void SetHighlight(bool highlighted)
        {
            _highlight = highlighted;
            ApplyVisualState();
        }

        public void SetActivityText(string text)
        {
            activityText = text ?? string.Empty;
            RefreshLabel();
        }

        public void ConfigureRuntimeArt(HostStepAArt.Profile profile, HostMoveController moveController,
            HostFarmFieldLabor farmLabor, HostDestructibleAssault destructibleAssault)
        {
            _artProfile = profile;
            _moveController = moveController;
            _farmLabor = farmLabor;
            _destructibleAssault = destructibleAssault;
            _artLastPosition = transform.position;
            _artClock = 0f;
            _artAction = HostStepAArt.Action.Idle;
            if (profile != HostStepAArt.Profile.None && bodyRenderer != null)
            {
                var frame = HostStepAArt.CharacterFrame(profile, _artAction, _artDirection, 0);
                if (frame != null)
                    bodyRenderer.sprite = frame;
                else
                    _artProfile = HostStepAArt.Profile.None;
            }
            ApplyVisualState();
        }

        /// <summary>Only called when an existing Host strike actually fires; no combat timing authority here.</summary>
        public void PlayArtAttack(Vector3 targetPosition)
        {
            if (_artProfile == HostStepAArt.Profile.None ||
                _world == null || !_world.Entities.TryGet(_entityId, out var entity) ||
                !CombatLifeStateService.CanFight(entity))
                return;
            var delta = targetPosition - transform.position;
            if (delta.sqrMagnitude > 0.0001f)
                SetArtDirection(delta);
            _artAttackStarted = Time.time;
            _artAttackUntil = Time.time + 0.36f;
        }

        public bool Bind(SimulationWorld world, EntityId entityId)
        {
            _failed = false;
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
            SetSleepVisual(null, transform.position);
            _bound = false;
            _failed = false;
            _world = null;
            _entityId = EntityId.None;
            _highlight = false;
            _hitFlashUntil = 0f;
            activityText = string.Empty;
            _artProfile = HostStepAArt.Profile.None;
            _moveController = null;
            _farmLabor = null;
            _destructibleAssault = null;
        }

        void LateUpdate()
        {
            if (!_bound || _failed)
                return;

            if (_world == null || !_world.Entities.TryGet(_entityId, out var entity))
            {
                FailSafe("Bound Core Entity disappeared: " + _entityId);
                return;
            }

            SyncFromCore(entity);
            UpdateSleepVisual(entity);
            UpdateRuntimeArt(entity);
            ApplyDepthSort();
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
            if (Time.unscaledTime >= _hitFlashUntil)
            {
                bodyRenderer.color = _artProfile != HostStepAArt.Profile.None && !_lifeTint
                    ? Color.white : _baseColor;
                return;
            }

            // 叠一层偏白，压过阵营底色
            bodyRenderer.color = Color.Lerp(_artProfile != HostStepAArt.Profile.None && !_lifeTint
                ? new Color(1f, .55f, .55f, 1f) : _baseColor, Color.white, 0.78f);
        }

        /// <summary>
        /// Y-sort among units only; always keep units above map tiles／buildings
        /// (tiles use roughly -30…100; northern Y used to push order to -thousands).
        /// </summary>
        void ApplyDepthSort()
        {
            const int entitySortBase = 800;
            var yBand = Mathf.Clamp(Mathf.RoundToInt(-transform.position.y * 2f), -200, 200);
            var order = entitySortBase + yBand;
            if (bodyRenderer != null)
                bodyRenderer.sortingOrder = order;
            if (_sleepRenderer != null)
                _sleepRenderer.sortingOrder = order;
            if (selectionRing != null)
                selectionRing.sortingOrder = order - 1;
            if (label != null)
            {
                var mr = label.GetComponent<MeshRenderer>();
                if (mr != null)
                    mr.sortingOrder = order + 1;
            }
        }

        void ApplyVisualState()
        {
            // Selection = ring only; do not tint the body (colors already encode faction／slot).
            if (bodyRenderer != null)
                bodyRenderer.color = _artProfile != HostStepAArt.Profile.None && !_lifeTint
                    ? Color.white : _baseColor;
            if (_sleepRenderer != null)
                _sleepRenderer.color = _artProfile != HostStepAArt.Profile.None && !_lifeTint
                    ? Color.white : _baseColor;

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

            gameObject.name = "EntityView_" + entity.Id.Value + "_" + display;

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
                _baseColor = isCorpse
                    ? new Color(0.35f, 0.32f, 0.30f, 0.85f)
                    : new Color(0.72f, 0.45f, 0.42f, 0.92f);
                ApplyVisualState();
            }
            else if (_lifeTint)
            {
                _lifeTint = false;
                ApplyVisualState();
            }
        }

        void UpdateRuntimeArt(Entity entity)
        {
            if (_artProfile == HostStepAArt.Profile.None || bodyRenderer == null)
                return;
            var delta = transform.position - _artLastPosition;
            _artLastPosition = transform.position;
            // A large reconciliation/teleport is not a walking step. The threshold follows
            // the existing Host speed so a low frame rate does not force a false Idle.
            var maxStep = Mathf.Max(.5f, (_moveController != null
                ? _moveController.GetEffectiveMoveSpeed(_entityId) : 6f) * Time.deltaTime * 1.5f);
            var moved = delta.sqrMagnitude > 0.000004f &&
                delta.sqrMagnitude < maxStep * maxStep;
            var moving = _moveController != null &&
                (_moveController.IsMoving(_entityId) || _moveController.WasDirectMovedThisFrame(_entityId));
            if (moved)
                SetArtDirection(delta);
            var canAct = CombatLifeStateService.CanFight(entity);
            var action = canAct && _sleepRenderer != null && _sleepRenderer.enabled
                ? HostStepAArt.Action.Idle
                : canAct && Time.time < _artAttackUntil
                ? HostStepAArt.Action.Attack
                : canAct && moved && (moving || delta.sqrMagnitude > 0.000025f) ? HostStepAArt.Action.Walk
                : canAct && HostStepAArt.HasWorkAnimation(_artProfile) && IsWorking(entity)
                    ? HostStepAArt.Action.Work : HostStepAArt.Action.Idle;
            if (action != _artAction)
            {
                _artAction = action;
                _artClock = 0f;
            }
            else if (action != HostStepAArt.Action.Attack)
                _artClock += Time.deltaTime;
            var frame = action == HostStepAArt.Action.Idle ? 0 :
                action == HostStepAArt.Action.Attack
                    ? Mathf.Clamp(Mathf.FloorToInt((Time.time - _artAttackStarted) / .09f), 0, 3)
                    : Mathf.FloorToInt(_artClock / (action == HostStepAArt.Action.Walk ? .12f : .16f)) % 4;
            var sprite = HostStepAArt.CharacterFrame(_artProfile, action, _artDirection, frame);
            if (sprite != null && bodyRenderer.sprite != sprite)
                bodyRenderer.sprite = sprite;
        }

        void UpdateSleepVisual(Entity entity)
        {
            if (_artProfile == HostStepAArt.Profile.None || bodyRenderer == null ||
                entity == null || !entity.TryGet<LifecycleComponent>(out var life) ||
                life.IsRemoved || life.IsDead || life.IsIncapacitated)
            {
                SetSleepVisual(null, transform.position);
                return;
            }
            var ground = _world.Civilians.TryGet(_entityId, out var civilian) &&
                civilian.SleepPhase == CivilianSleepPhase.SleepingOnGround;
            var residence = civilian != null &&
                civilian.SleepPhase == CivilianSleepPhase.SleepingAtResidence;
            var bedAction = entity.TryGet<ActionStateComponent>(out var state) &&
                state.HasActiveAction && _world.ActiveActions.TryGetValue(state.ActiveActionId, out var active) &&
                active is BedRestAction rest && rest.Status == ActionStatus.Running;
            if (!ground && !residence && !bedAction)
            {
                SetSleepVisual(null, transform.position);
                return;
            }
            var sprite = HostStepAArt.SleepSprite(_artProfile);
            if (sprite == null)
            {
                SetSleepVisual(null, transform.position);
                return;
            }
            if (ground)
            {
                SetSleepVisual(sprite, transform.position);
                return;
            }
            // Bed visuals are presentation-only. Occupancy, access point and Core position stay unchanged.
            if (!RoomBedFacilityService.IsValidOccupiedBed(_world, _entityId,
                    RoomUsage.Residential, out var bed) &&
                !RoomBedFacilityService.IsValidOccupiedBed(_world, _entityId,
                    RoomUsage.Prison, out bed) &&
                !RoomBedFacilityService.IsValidOccupiedBed(_world, _entityId,
                    RoomUsage.Medical, out bed))
            {
                SetSleepVisual(null, transform.position);
                return;
            }
            var plots = HostMapObjectRegistry.AllPlots;
            for (var i = 0; i < plots.Count; i++)
            {
                var plot = plots[i];
                if (plot == null || !plot.IsBed || plot.StableCellId != bed.StableAssetId)
                    continue;
                SetSleepVisual(sprite, plot.transform.position);
                return;
            }
            SetSleepVisual(null, transform.position); // No loaded bed view: never fabricate a second actor.
        }

        void SetSleepVisual(Sprite sprite, Vector3 position)
        {
            if (sprite == null)
            {
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
            }
            _sleepRenderer.sprite = sprite;
            _sleepRenderer.transform.position = new Vector3(position.x, position.y, transform.position.z);
            _sleepRenderer.enabled = true;
            if (label != null)
                label.transform.position = new Vector3(position.x, position.y + 0.72f,
                    label.transform.position.z);
            bodyRenderer.enabled = false;
        }

        bool IsWorking(Entity entity)
        {
            if (_farmLabor != null && _farmLabor.IsPerformingWork(_entityId))
                return true;
            if (_destructibleAssault != null && _destructibleAssault.IsCivilianPerformingWork(_entityId))
                return true;
            if (entity.TryGet<ActionStateComponent>(out var state) && state.HasActiveAction &&
                _world.ActiveActions.TryGetValue(state.ActiveActionId, out var action) &&
                (action is WorkAction || action is LaborAction))
                return true;
            return false;
        }

        void SetArtDirection(Vector3 delta)
        {
            _artDirection = Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
                ? delta.x < 0f ? HostStepAArt.Direction.West : HostStepAArt.Direction.East
                : delta.y < 0f ? HostStepAArt.Direction.South : HostStepAArt.Direction.North;
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
            _hitFlashUntil = Time.unscaledTime + Mathf.Max(0.04f, seconds);
        }

        void EnsureVisualParts()
        {
            if (bodyRenderer == null)
                bodyRenderer = GetComponent<SpriteRenderer>();
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
            if (label != null)
                label.text = "ERR";
            return false;
        }
    }
}
