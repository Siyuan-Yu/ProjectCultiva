using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
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

        public EntityId EntityId => _entityId;

        public bool IsBound => _bound && !_failed;

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
            _bound = false;
            _failed = false;
            _world = null;
            _entityId = EntityId.None;
            _highlight = false;
            activityText = string.Empty;
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
            ApplyDepthSort();
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
                bodyRenderer.color = _baseColor;

            if (selectionRing != null)
                selectionRing.enabled = _highlight;
        }

        void SyncFromCore(Entity entity)
        {
            var display = string.IsNullOrEmpty(entity.DisplayName)
                ? entity.Id.ToString()
                : entity.DisplayName;

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
