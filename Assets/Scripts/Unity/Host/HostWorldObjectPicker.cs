using System;
using UnityEngine;
using XianXia.Core.Npc;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;
using XianXia.Data.Content;

namespace XianXia.Unity.Host
{
    public enum WorldObjectTargetKind
    {
        None = 0, ControlCore = 1, FactionFlag = 2, FarmPlot = 3,
        Destructible = 4, Housing = 5, WorkArea = 6
    }

    /// <summary>一次拾取得到的 persistent world-object identity；左键检视与右键行为共用。</summary>
    public readonly struct WorldObjectInteractionTarget
    {
        public WorldObjectInteractionTarget(WorldObjectTargetKind kind, string workAreaId = null,
            string factionFlagId = null, HostMapPlotCell plot = null, HostMapDestructible destructible = null,
            string displayLabel = null)
        {
            Kind = kind; WorkAreaId = workAreaId ?? string.Empty; FactionFlagId = factionFlagId ?? string.Empty;
            Plot = plot; Destructible = destructible; DisplayLabel = displayLabel ?? string.Empty;
        }
        public WorldObjectTargetKind Kind { get; }
        public string WorkAreaId { get; }
        public string FactionFlagId { get; }
        public HostMapPlotCell Plot { get; }
        public HostMapDestructible Destructible { get; }
        public string DisplayLabel { get; }
        public bool IsValid => Kind != WorldObjectTargetKind.None;
    }

    public static class HostWorldObjectPicker
    {
        public static bool TryPickAtScreenPoint(PlayableHostBootstrap host, Camera camera, Vector2 screenPoint,
            out WorldObjectInteractionTarget target)
        {
            target = default;
            return camera != null && HostPresentationSpace.TryRaycastPlane(camera, screenPoint, out var point) &&
                   TryPickAtWorldPoint(host, point, out target);
        }

        public static bool TryPickAtWorldPoint(PlayableHostBootstrap host, Vector3 point,
            out WorldObjectInteractionTarget target)
        {
            target = default;
            var world = host?.Session?.World;
            if (world == null) return false;
            MapLayoutDefinition layout = null;
            var continuous = host.ContinuousOutdoorSurfaceRuntime;
            if (continuous == null || !continuous.IsActive) MapLayoutPick.TryGet(host.Session, out layout);

            if (HostControlCoreQuery.TryPickAtWorld(world, layout, continuous, point, out var coreId) &&
                world.ControlCores.TryGet(coreId, out var core))
            {
                target = new WorldObjectInteractionTarget(WorldObjectTargetKind.ControlCore, coreId,
                    displayLabel: string.IsNullOrEmpty(core.Name) ? "议政厅" : core.Name);
                return true;
            }

            if (TryPickFactionFlag(world, layout, continuous, point, out var flag))
            {
                target = new WorldObjectInteractionTarget(WorldObjectTargetKind.FactionFlag,
                    factionFlagId: flag.FlagId,
                    displayLabel: "势力旗·" + StrategicFactionCatalog.DisplayName(flag.FactionId));
                return true;
            }

            if (HostMapObjectRegistry.TryPickPlot(point, 1.35f, out var plot) && plot.IsPlantableField)
            {
                target = new WorldObjectInteractionTarget(WorldObjectTargetKind.FarmPlot, plot: plot,
                    displayLabel: plot.KindDisplayName());
                return true;
            }

            if (HostMapObjectRegistry.TryPickDestructible(point, 2.2f, out var destructible))
            {
                target = new WorldObjectInteractionTarget(WorldObjectTargetKind.Destructible,
                    destructible: destructible, displayLabel: destructible.DisplayName);
                return true;
            }

            if (TryPickWorkArea(world, point, 6.5f, housing: true, out var workAreaId))
            {
                target = new WorldObjectInteractionTarget(WorldObjectTargetKind.Housing, workAreaId,
                    displayLabel: ResolveWorkAreaName(world, workAreaId));
                return true;
            }
            if (TryPickWorkArea(world, point, 6.5f, housing: false, out workAreaId))
            {
                target = new WorldObjectInteractionTarget(WorldObjectTargetKind.WorkArea, workAreaId,
                    displayLabel: ResolveWorkAreaName(world, workAreaId));
                return true;
            }
            return false;
        }

        static bool TryPickFactionFlag(SimulationWorld world, MapLayoutDefinition layout,
            ContinuousOutdoorSurfaceRuntime continuous, Vector3 point, out FactionFlagState picked)
        {
            picked = null;
            if (continuous != null && continuous.IsActive)
            {
                foreach (var pair in world.Strategic.FactionFlags.Flags)
                {
                    var flag = pair.Value;
                    if (flag == null || (!string.IsNullOrEmpty(flag.SurfaceId) &&
                        !string.Equals(flag.SurfaceId, continuous.ActiveSurfaceId, StringComparison.Ordinal)) ||
                        !IsContinuousFlagLoaded(flag, continuous) ||
                        !HostFactionFlagQuery.TryPickAtWorld(flag, continuous, point, out _)) continue;
                    picked = flag; return true;
                }
                return false;
            }
            if (!LoadedLocalMapBelongingQuery.TryResolveLoadedLocalMap(world, out var local) ||
                local.Kind != LoadedLocalMapBelongingQuery.LoadedLocalMapKind.WildernessHex ||
                !world.Strategic.FactionFlags.TryGetAt(local.WildernessHex, out var legacy) || legacy == null ||
                !HostFactionFlagQuery.TryPickAtWorld(legacy, layout, point, out _)) return false;
            picked = legacy; return true;
        }

        static bool IsContinuousFlagLoaded(FactionFlagState flag, ContinuousOutdoorSurfaceRuntime continuous)
        {
            var x = flag.WorldX; var y = flag.WorldY;
            if (!flag.HasWorldPosition)
            {
                if (flag.IsSiteCore) return false;
                XianXia.Core.World.Hex.HexMath.ToWorldPosition(flag.AnchorHex, continuous.ActiveHexSize, out x, out y);
            }
            return continuous.IsWorldPositionLoaded(continuous.ActiveSurfaceId, x, y);
        }

        static bool TryPickWorkArea(SimulationWorld world, Vector3 point, float radius, bool housing,
            out string workAreaId)
        {
            workAreaId = string.Empty; var p = HostPresentationSpace.ToPresentation(point);
            var best = float.MaxValue;
            foreach (var pair in world.WorkAreas)
            {
                var area = pair.Value;
                if (area == null || area.IsControlCore || HousingAssignmentService.IsHousingArea(area) != housing)
                    continue;
                if (string.IsNullOrEmpty(area.LocationId) ||
                    (!world.ContinuousOutdoorMaterialization.TryGetAnyPlace(area.LocationId, out var loc) &&
                     !world.WorldRegion.TryGet(area.LocationId, out loc))) continue;
                float distance;
                if (HostFarmFieldRules.IsFarmTaggedWorkArea(area))
                {
                    if (!HostFarmFieldRegistry.TryFindPlotAt(point, out var plot) || plot == null ||
                        !string.Equals(plot.LocationId, area.LocationId, StringComparison.Ordinal)) continue;
                    distance = HostFarmFieldRules.XyDistance(plot.transform.position, point);
                }
                else
                {
                    var dx = loc.PresentationX + area.OffsetX - p.x;
                    var dy = loc.PresentationZ + area.OffsetZ - p.y;
                    distance = Mathf.Sqrt(dx * dx + dy * dy);
                    if (distance > radius) continue;
                }
                if (distance >= best) continue;
                best = distance; workAreaId = area.Id;
            }
            return !string.IsNullOrEmpty(workAreaId);
        }

        static string ResolveWorkAreaName(SimulationWorld world, string id) =>
            world.TryGetWorkArea(id, out var area) && !string.IsNullOrEmpty(area.Name) ? area.Name : id;
    }
}
