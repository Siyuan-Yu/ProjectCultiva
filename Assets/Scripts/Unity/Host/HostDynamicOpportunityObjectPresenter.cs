using System;
using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Opportunity;

namespace XianXia.Unity.Host
{
    /// <summary>Materializes discovered/visible opportunity objects only while their Surface chunk is loaded.</summary>
    public sealed class HostDynamicOpportunityObjectPresenter : MonoBehaviour
    {
        PlayableHostBootstrap _host;
        readonly Dictionary<string, GameObject> _views = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        Transform _root;

        public void Bind(PlayableHostBootstrap host) { _host = host; Reconcile(); }
        void LateUpdate() => Reconcile();
        void OnDisable() => Clear();

        public void Reconcile()
        {
            var world = _host?.Session?.World;
            var surface = _host?.ContinuousOutdoorSurfaceRuntime;
            if (world == null || surface == null || !surface.IsActive) { Clear(); return; }
            EnsureRoot();
            var wanted = new HashSet<string>(StringComparer.Ordinal);
            foreach (var instance in world.WorldOpportunities.ActiveInstances.Values)
            {
                if (instance.SpawnKind != WorldOpportunitySpawnKind.WorldObject ||
                    (instance.DiscoveryMode == WorldOpportunityDiscoveryMode.HiddenUntilDiscovered && !instance.IsDiscovered) ||
                    !string.Equals(instance.SurfaceId, surface.ActiveSurfaceId, StringComparison.Ordinal) ||
                    !surface.IsWorldPositionLoaded(instance.SurfaceId, instance.WorldX, instance.WorldY) ||
                    !world.WorldOpportunities.TryGetSpec(instance.OpportunityDefinitionId, out var spec)) continue;
                wanted.Add(instance.WorldObjectInstanceId);
                if (!_views.TryGetValue(instance.WorldObjectInstanceId, out var view) || view == null)
                    Create(instance, spec, surface);
            }
            var remove = new List<string>();
            foreach (var pair in _views) if (!wanted.Contains(pair.Key)) remove.Add(pair.Key);
            foreach (var id in remove) Remove(id);
        }

        void Create(WorldOpportunityInstance instance, WorldOpportunitySpec spec, ContinuousOutdoorSurfaceRuntime surface)
        {
            if (!MapKindCatalog.TryGet(spec.WorldObjectKind, out var kind)) return;
            surface.Mapper.WorldToPresentation(instance.WorldX, instance.WorldY, out var px, out var py);
            var center = HostPresentationSpace.FromPresentation(px, py, HostPresentationSpace.GroundZ);
            var view = HostMapObjectVisualFactory.Create(kind.Kind, kind.PrefabPath,
                "DynamicOpportunity_" + instance.WorldObjectInstanceId, _root, center,
                spec.WorldObjectWorldWidth, spec.WorldObjectWorldHeight, -18, true);
            var bounds = new Bounds(center, new Vector3(
                Mathf.Max(.2f, spec.WorldObjectWorldWidth), Mathf.Max(.2f, spec.WorldObjectWorldHeight), 1f));
            if (HostMapObjectVisualFactory.TryGetRendererBounds(view, out var rendererBounds)) bounds = rendererBounds;
            var entry = new HostDynamicWorldObjectEntry
            {
                WorldObjectInstanceId = instance.WorldObjectInstanceId,
                OpportunityInstanceId = instance.InstanceId,
                OpportunityDefinitionId = instance.OpportunityDefinitionId,
                SurfaceId = instance.SurfaceId,
                PresentationBounds = bounds,
                ApproachPosition = center,
                DisplayLabel = string.IsNullOrWhiteSpace(spec.WorldObjectLabel) ? spec.Name : spec.WorldObjectLabel,
                View = view
            };
            _views.Add(instance.WorldObjectInstanceId, view);
            HostDynamicWorldObjectRegistry.Register(entry);
        }

        void Remove(string id)
        {
            HostDynamicWorldObjectRegistry.Remove(id);
            if (_views.TryGetValue(id, out var view) && view != null)
                if (Application.isPlaying) Destroy(view); else DestroyImmediate(view);
            _views.Remove(id);
        }
        void Clear()
        {
            foreach (var id in new List<string>(_views.Keys)) Remove(id);
            HostDynamicWorldObjectRegistry.Clear();
        }
        void EnsureRoot()
        {
            if (_root != null) return;
            var go = new GameObject("DynamicOpportunityObjects"); go.transform.SetParent(transform, false); _root = go.transform;
        }
    }
}
