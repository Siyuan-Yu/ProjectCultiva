using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Npc;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Unity.Host
{
    /// <summary>Read-only camp range and constructed-asset presentation synchronization.</summary>
    public sealed class HostPrivateCampPresentation : MonoBehaviour
    {
        readonly Dictionary<string, LineRenderer> _rings = new Dictionary<string, LineRenderer>();
        PlayableHostBootstrap _host;
        SimulationWorld _world;
        Material _material;
        ulong _assetsRevision = ulong.MaxValue;
        int _revision = -1;
        void Awake() { _host = GetComponent<PlayableHostBootstrap>(); }
        void LateUpdate()
        {
            using var timing = HostDebugHud.PrivateCampLateUpdate.Auto();
            var w = _host?.Session?.World;
            if (!ReferenceEquals(w, _world)) { Clear(); _world = w; _assetsRevision = ulong.MaxValue; _revision = -1; }
            foreach (var ring in _rings.Values) if (ring != null) ring.enabled = false;
            var surface = _host?.ContinuousOutdoorSurfaceRuntime;
            if (w == null || surface == null || !surface.IsActive) return;
            var assetsRevision = w.OutdoorConstructedAssets.Revision;
            var revision = w.ContentCounters.Get("base:camp-visual-revision");
            if (_assetsRevision != assetsRevision || _revision != revision)
            {
                surface.RefreshRuntimeConstructedPlacementsForLoadedChunks();
                _assetsRevision = assetsRevision; _revision = revision;
            }
            if (_host.WorldMapPanel?.IsOpen == true || w.LocalMap.IsInInterior) return;
            foreach (var a in PrivateCampService.Assets(w))
            {
                if (a.Kind != PrivateCampService.MarkerKind || a.SurfaceId != surface.ActiveSurfaceId ||
                    !PrivateCampService.Owns(w, a.StableAssetId, w.Strategic.PlayerFactionId) ||
                    w.OutdoorStatefulObjects.IsDestructibleDestroyed(a.StableAssetId) ||
                    !w.SurfaceSpatial.TryGet(a.SurfaceId, out var metric)) continue;
                var center = new WorldVec2(a.WorldX + a.WorldWidth / 2, a.WorldY + a.WorldHeight / 2);
                if (!surface.TryWorldToPresentation(center, out var p) ||
                    !surface.TryWorldToPresentation(new WorldVec2(center.X + PrivateCampService.RadiusCells * metric.CellSize, center.Y), out var edge)) continue;
                if (!_rings.TryGetValue(a.StableAssetId, out var line))
                {
                    if (_material == null)
                    {
                        var shader = Shader.Find("Sprites/Default");
                        if (shader == null) continue;
                        _material = new Material(shader);
                    }
                    var go = new GameObject("PrivateCampRange:" + a.StableAssetId);
                    go.transform.SetParent(transform, false);
                    line = go.AddComponent<LineRenderer>(); line.sharedMaterial = _material;
                    HostPatrolWatchRings.ConfigureUnitCircle(line,80);
                    line.sortingOrder = 5100; line.startColor = line.endColor = new Color(.25f, .8f, 1f, .45f);
                    _rings.Add(a.StableAssetId, line);
                }
                var radius = Vector2.Distance(p, edge);
                line.startWidth = line.endWidth = radius / PrivateCampService.RadiusCells * .08f;
                line.transform.position = new Vector3(p.x,p.y,HostPresentationSpace.GroundZ);
                line.transform.localScale = Vector3.one * radius;
                line.enabled = true;
            }
        }
        void Clear() { foreach (var ring in _rings.Values) if (ring != null) Destroy(ring.gameObject); _rings.Clear(); }
        void OnDisable() { foreach (var ring in _rings.Values) if (ring != null) ring.enabled = false; }
        void OnDestroy() { Clear(); if (_material != null) Destroy(_material); }
    }
}
