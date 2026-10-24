using System;
using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Opportunity;
using XianXia.Core.Simulation;

namespace XianXia.Unity.Host
{
    /// <summary>Materializes discovered/visible opportunity objects only while their Surface chunk is loaded.</summary>
    public sealed class HostDynamicOpportunityObjectPresenter : MonoBehaviour
    {
        PlayableHostBootstrap _host;
        readonly Dictionary<string, GameObject> _views = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        Transform _root;
        SimulationWorld _world;
        readonly HashSet<string> _wanted = new HashSet<string>(StringComparer.Ordinal);
        readonly List<string> _remove = new List<string>();

        public void Bind(PlayableHostBootstrap host) { _host = host; Reconcile(); }
        void LateUpdate()
        {
            using var timing = HostDebugHud.OpportunityLateUpdate.Auto();
            Reconcile();
        }
        void OnDisable() => Clear();

        public void Reconcile()
        {
            var world = _host?.Session?.World;
            var surface = _host?.ContinuousOutdoorSurfaceRuntime;
            if (world == null || surface == null || !surface.IsActive) { Clear(); return; }
            if (!ReferenceEquals(_world, world)) { Clear(); _world = world; }
            EnsureRoot();
            _wanted.Clear();
            var wanted = _wanted;
            foreach (var instance in world.WorldOpportunities.ActiveInstances.Values)
            {
                if (instance.SpawnKind != WorldOpportunitySpawnKind.WorldObject ||
                    (instance.DiscoveryMode == WorldOpportunityDiscoveryMode.HiddenUntilDiscovered && !instance.IsDiscovered) ||
                    !string.Equals(instance.SurfaceId, surface.ActiveSurfaceId, StringComparison.Ordinal) ||
                    !surface.IsWorldPositionLoaded(instance.SurfaceId, instance.WorldX, instance.WorldY) ||
                    !world.WorldOpportunities.TryGetSpec(instance.OpportunityDefinitionId, out var spec)) continue;
                wanted.Add(instance.WorldObjectInstanceId);
                if (!_views.TryGetValue(instance.WorldObjectInstanceId, out var view) || view == null)
                {
                    Remove(instance.WorldObjectInstanceId);
                    Create(instance, spec, surface);
                }
            }
            _remove.Clear();
            var remove = _remove;
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
                spec.WorldObjectWidthCells, spec.WorldObjectHeightCells, -18, true);
            var bounds = new Bounds(center, new Vector3(
                spec.WorldObjectWidthCells, spec.WorldObjectHeightCells, 1f));
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
            {
                view.SetActive(false);
                if (Application.isPlaying) Destroy(view); else DestroyImmediate(view);
            }
            _views.Remove(id);
        }
        void Clear()
        {
            if (_views.Count > 0)
            {
                _remove.Clear(); _remove.AddRange(_views.Keys);
                foreach (var id in _remove) Remove(id);
                _remove.Clear();
            }
            _wanted.Clear();
            _world = null;
        }
        void EnsureRoot()
        {
            if (_root != null) return;
            var go = new GameObject("DynamicOpportunityObjects"); go.transform.SetParent(transform, false); _root = go.transform;
        }
    }
}
