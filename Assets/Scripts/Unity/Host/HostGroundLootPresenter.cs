using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Simulation;

namespace XianXia.Unity.Host
{
    public sealed class HostGroundLootPresenter : MonoBehaviour
    {
        const string Owner = "runtime-ground-loot";
        PlayableHostBootstrap _host;
        SimulationWorld _world;
        float _nextRefresh;
        bool _spotsDirty = true;
        int _spotGeneration = -1;
        readonly Dictionary<string, GameObject> _views = new Dictionary<string, GameObject>();
        readonly Dictionary<string, int> _counts = new Dictionary<string, int>();
        readonly HashSet<string> _visible = new HashSet<string>();

        void Update()
        {
            if (_host == null) _host = GetComponent<PlayableHostBootstrap>();
            if (_host?.Session?.World == null) return;
            if (Time.unscaledTime < _nextRefresh && ReferenceEquals(_world, _host.Session.World)) return;
            _nextRefresh = Time.unscaledTime + .25f;
            if (!ReferenceEquals(_world, _host.Session.World)) { Clear(); _world = _host.Session.World; }
            var surface = _host.ContinuousOutdoorSurfaceRuntime;
            _visible.Clear();
            if (surface != null && surface.IsActive)
                foreach (var pair in _world.GroundLoot)
                {
                    var drop = pair.Value;
                    if (drop.SurfaceId != surface.ActiveSurfaceId) continue;
                    surface.Mapper.WorldToPresentation(drop.WorldX, drop.WorldY, out var x, out var y);
                    if (!surface.IsPresentationPositionLoaded(x, y)) continue;
                    _visible.Add(drop.Id);
                    if (_views.TryGetValue(drop.Id, out var old) && old != null && _counts[drop.Id] == drop.Count) continue;
                    Remove(drop.Id);
                    var go = new GameObject(drop.Id); go.transform.SetParent(transform, false);
                    go.transform.position = HostPresentationSpace.FromPresentation(x, y, HostPresentationSpace.GroundZ);
                    var label = _world.InventoryCatalog.TryGet(drop.ItemId, out var item) ? item.Name : "物品";
                    var plot = go.AddComponent<HostMapPlotCell>();
                    plot.Configure(_world, drop.Id, "", HostInteractSpotKind.Loot, label, 0, 0, "loot", drop.Id, drop.ItemId);
                    plot.ConfigureInteractionBounds(new Rect(x - .5f, y - .5f, 1f, 1f));
                    var collider = go.AddComponent<BoxCollider2D>(); collider.size = Vector2.one; collider.isTrigger = true;
                    HostGroundLootVisual.Apply(go, _world, drop.ItemId, label, .85f, drop.Count);
                    _views[drop.Id] = go; _counts[drop.Id] = drop.Count;
                }
            var stale = new List<string>(_views.Keys);
            foreach (var id in stale) if (!_visible.Contains(id)) Remove(id);
            foreach (var go in _views.Values)
                if (go != null) HostMapObjectRegistry.Register(Owner, go.GetComponent<HostMapPlotCell>());
            if (!_spotsDirty && _spotGeneration == HostInteractSpots.LayoutGeneration) return;
            HostInteractSpots.RemoveOwner(Owner);
            foreach (var pair in _views)
            {
                if (pair.Value == null || !_world.GroundLoot.TryGetValue(pair.Key, out var drop)) continue;
                var p = pair.Value.transform.position;
                var label = _world.InventoryCatalog.TryGet(drop.ItemId, out var item) ? item.Name : "物品";
                HostInteractSpots.RegisterPlot(Owner, new HostInteractSpot("", HostInteractSpotKind.Loot, p.x, p.y,
                    label + " ×" + drop.Count, drop.Id, drop.ItemId));
            }
            HostInteractSpots.GetSpots(_world);
            _spotGeneration = HostInteractSpots.LayoutGeneration;
            _spotsDirty = false;
        }

        void Remove(string id)
        {
            _spotsDirty = true;
            if (_views.TryGetValue(id, out var go) && go != null) { go.SetActive(false); Destroy(go); }
            _views.Remove(id); _counts.Remove(id);
        }
        void Clear()
        {
            foreach (var go in _views.Values) if (go != null) { go.SetActive(false); Destroy(go); }
            _views.Clear(); _counts.Clear(); HostInteractSpots.RemoveOwner(Owner);
            _spotsDirty = true;
        }
        void OnDisable() => Clear();
    }
}
