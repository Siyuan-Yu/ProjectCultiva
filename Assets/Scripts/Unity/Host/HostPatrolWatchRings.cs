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
            using var timing = HostDebugHud.PatrolRingsLateUpdate.Auto();
            var world = _host?.Session?.World;
            if (!ReferenceEquals(world, _world)) { Clear(); _world = world; }
            foreach (var pair in _rings) if (pair.Value != null) pair.Value.enabled = false;
            var surface = _host?.ContinuousOutdoorSurfaceRuntime;
            if (world == null ||
                surface == null || !surface.IsActive || _host.ViewSpawner == null || _host.WorldMapPanel?.IsOpen == true ||
                world.LocalMap.IsInInterior || world.Strategic.CharacterEncounter != null) return;

            var opening = world.HuangcunSurvival.Enabled && HuangcunOpeningAccess.IsLocked(world);
            foreach (var view in _host.ViewSpawner.Registry.All)
            {
                if (view == null || !view.IsBound || !view.gameObject.activeInHierarchy ||
                    !world.Entities.TryGet(view.EntityId,out var entity)) continue;
                var eligible = opening && HuangcunOpeningAccess.IsOpeningStaff(world,entity.Id) ||
                    SettlementPatrolService.HasIndividualDuty(entity);
                if (!eligible || !SupervisorAngerDriver.CanObserveOutdoors(world,entity) ||
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
                ring.transform.position = center;
                ring.transform.localScale = Vector3.one * radius;
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
            ConfigureUnitCircle(line, Segments);
            line.alignment = LineAlignment.View; line.textureMode = LineTextureMode.Stretch;
            line.numCornerVertices = 2; line.sortingOrder = 5200;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            _rings[id.Value] = line;
            return line;
        }

        internal static void ConfigureUnitCircle(LineRenderer line,int segments)
        {
            line.useWorldSpace=false; line.loop=true; line.positionCount=segments;
            var points=new Vector3[segments];
            for(var i=0;i<segments;i++)
            {var angle=Mathf.PI*2f*i/segments;points[i]=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0);}
            line.SetPositions(points);
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
