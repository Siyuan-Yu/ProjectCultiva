using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Minimal presentation binding for a Core Entity. Read-only sync only.
    /// Does not modify components, tick, or create Orders.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EntityView : MonoBehaviour
    {
        [SerializeField] Renderer bodyRenderer;
        [SerializeField] TextMesh label;

        SimulationWorld _world;
        EntityId _entityId;
        bool _bound;
        bool _failed;
        Color _baseColor = Color.gray;
        bool _highlight;

        public EntityId EntityId => _entityId;

        public bool IsBound => _bound && !_failed;

        public bool IsHighlightRequested => _highlight;

        /// <summary>V4-C will drive selection; Phase B only exposes the hook.</summary>
        public void SetHighlight(bool highlighted)
        {
            _highlight = highlighted;
            ApplyVisualState();
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
            ApplyBaseColorFromSlot();
            SyncFromCore(entity);
            ApplyVisualState();
            return true;
        }

        public void Unbind()
        {
            _bound = false;
            _failed = false;
            _world = null;
            _entityId = EntityId.None;
            _highlight = false;
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
        }

        void SyncFromCore(Entity entity)
        {
            var display = string.IsNullOrEmpty(entity.DisplayName)
                ? entity.Id.ToString()
                : entity.DisplayName;

            gameObject.name = "EntityView_" + entity.Id.Value + "_" + display;

            if (label != null)
            {
                var busy = entity.TryGet<ActionStateComponent>(out var action) && action.HasActiveAction;
                label.text = display + (busy ? " *" : string.Empty);
            }
        }

        public void SetBaseColor(Color color)
        {
            _baseColor = color;
            ApplyVisualState();
        }

        void EnsureVisualParts()
        {
            if (bodyRenderer == null)
                bodyRenderer = GetComponentInChildren<Renderer>();

            if (label == null)
                label = GetComponentInChildren<TextMesh>();
        }

        void ApplyBaseColorFromSlot()
        {
            // no-op placeholder; spawner sets color via SetBaseColor
        }

        void ApplyVisualState()
        {
            if (bodyRenderer == null)
                return;

            var color = _highlight ? Color.yellow : _baseColor;
            var block = new MaterialPropertyBlock();
            bodyRenderer.GetPropertyBlock(block);
            block.SetColor("_Color", color);
            bodyRenderer.SetPropertyBlock(block);

            if (bodyRenderer.sharedMaterial != null && bodyRenderer.sharedMaterial.HasProperty("_BaseColor"))
            {
                bodyRenderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", color);
                bodyRenderer.SetPropertyBlock(block);
            }
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
