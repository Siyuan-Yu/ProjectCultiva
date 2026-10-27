using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.Npc;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>Read-only presentation of the Core Huangcun watch radius and eligibility.</summary>
    [DefaultExecutionOrder(150)]
    public sealed class HostPatrolWatchRings : MonoBehaviour
    {
        const int Segments = 80;
        readonly Dictionary<ulong, LineRenderer> _rings = new Dictionary<ulong, LineRenderer>();
        PlayableHostBootstrap _host;
        SimulationWorld _world;
        Material _material;

        void Awake() { _host = GetComponent<PlayableHostBootstrap>(); }

        void LateUpdate()
        {
            var world = _host?.Session?.World;
            if (!ReferenceEquals(world, _world)) { Clear(); _world = world; }
            foreach (var pair in _rings) if (pair.Value != null) pair.Value.enabled = false;
            var surface = _host?.ContinuousOutdoorSurfaceRuntime;
            if (world == null ||
                surface == null || !surface.IsActive || _host.ViewSpawner == null || _host.WorldMapPanel?.IsOpen == true ||
                world.LocalMap.IsInInterior || world.Strategic.CharacterEncounter != null) return;

            foreach (var entity in world.Entities.All)
            {
                var openingWatch = world.HuangcunSurvival.Enabled && HuangcunOpeningAccess.IsLocked(world) &&
                    SupervisorAngerDriver.IsOnWatch(world, entity);
                var outdoorWatch = SettlementPatrolService.HasIndividualDuty(entity) &&
                    SupervisorAngerDriver.CanObserveOutdoors(world, entity);
                if ((!openingWatch && !outdoorWatch) ||
                    !_host.ViewSpawner.Registry.TryGet(entity.Id, out var view) || view == null || !view.gameObject.activeInHierarchy ||
                    !CharacterWorldPresenceQuery.TryResolve(world, entity.Id, out var at) || !at.HasWorldPosition ||
                    at.SurfaceId != surface.ActiveSurfaceId ||
                    !surface.TryWorldToPresentation(at.WorldPosition, out var p0) ||
                    !surface.TryWorldToPresentation(new WorldVec2(at.WorldPosition.X +
                        world.OpeningArea.CellSize * SupervisorAngerDriver.ObservationRangeCells, at.WorldPosition.Y), out var p1)) continue;
                var ring = GetRing(entity.Id);
                if (ring == null) continue;
                var radius = Vector2.Distance(p0, p1);
                var center = view.transform.position;
                center.z = HostPresentationSpace.GroundZ;
                ring.startWidth = ring.endWidth = radius / SupervisorAngerDriver.ObservationRangeCells * .08f;
                var color = world.HuangcunSurvival.PunisherId == entity.Id.Value
                    ? new Color(1f, .25f, .15f, .65f) : new Color(1f, .78f, .24f, .45f);
                ring.startColor = ring.endColor = color;
                for (var i = 0; i < Segments; i++)
                {
                    var angle = Mathf.PI * 2f * i / Segments;
                    ring.SetPosition(i, center + new Vector3(Mathf.Cos(angle)*radius, Mathf.Sin(angle)*radius));
                }
                ring.enabled = true;
            }
        }

        LineRenderer GetRing(EntityId id)
        {
            if (_rings.TryGetValue(id.Value, out var existing) && existing != null) return existing;
            if (_material == null)
            {
                var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
                if (shader == null) return null;
                _material = new Material(shader);
            }
            var go = new GameObject("PatrolWatchRange:" + id.Value);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = _material;
            line.useWorldSpace = true; line.loop = true; line.positionCount = Segments;
            line.alignment = LineAlignment.View; line.textureMode = LineTextureMode.Stretch;
            line.numCornerVertices = 2; line.sortingOrder = 5200;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            _rings[id.Value] = line;
            return line;
        }

        void Clear()
        {
            foreach (var pair in _rings) if (pair.Value != null) Destroy(pair.Value.gameObject);
            _rings.Clear();
        }
        void OnDisable() { foreach (var pair in _rings) if (pair.Value != null) pair.Value.enabled = false; }
        void OnDestroy() { Clear(); if (_material != null) Destroy(_material); }
    }
}
