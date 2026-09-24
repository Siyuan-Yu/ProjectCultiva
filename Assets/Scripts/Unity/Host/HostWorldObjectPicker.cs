using System;
using UnityEngine;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Npc;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;
using XianXia.Data.Content;

namespace XianXia.Unity.Host
{
    public enum WorldObjectTargetKind
    {
        None = 0, ControlCore = 1, FactionFlag = 2, FarmPlot = 3,
        Destructible = 4, Housing = 5, WorkArea = 6, RecoverySpot = 7, StorageRoom = 8,
        OpportunityObject = 9
    }

    public enum WorldObjectPickPurpose { PlayerInteraction, WorkTarget }

    /// <summary>一次拾取得到的 persistent world-object identity；左键检视与右键行为共用。</summary>
    public readonly struct WorldObjectInteractionTarget
    {
        public WorldObjectInteractionTarget(WorldObjectTargetKind kind, string workAreaId = null,
            string factionFlagId = null, HostMapPlotCell plot = null, HostMapDestructible destructible = null,
            string displayLabel = null, Vector3? approachPosition = null,
            HostDynamicWorldObjectEntry opportunityObject = null)
        {
            Kind = kind; WorkAreaId = workAreaId ?? string.Empty; FactionFlagId = factionFlagId ?? string.Empty;
            Plot = plot; Destructible = destructible; DisplayLabel = displayLabel ?? string.Empty;
            OpportunityObject = opportunityObject;
            ApproachPosition = approachPosition ??
                (plot != null ? plot.transform.position : destructible != null ? destructible.transform.position : Vector3.zero);
            HasApproachPosition = approachPosition.HasValue || plot != null || destructible != null;
        }
        public WorldObjectTargetKind Kind { get; }
        public string WorkAreaId { get; }
        public string FactionFlagId { get; }
        public HostMapPlotCell Plot { get; }
        public HostMapDestructible Destructible { get; }
        public HostDynamicWorldObjectEntry OpportunityObject { get; }
        public string DisplayLabel { get; }
        public Vector3 ApproachPosition { get; }
        public bool HasApproachPosition { get; }
        public bool IsValid => Kind != WorldObjectTargetKind.None;
        public string KindKey
        {
            get
            {
                switch (Kind)
                {
                    case WorldObjectTargetKind.ControlCore: return "controlCore";
                    case WorldObjectTargetKind.FactionFlag: return "factionFlag";
                    case WorldObjectTargetKind.FarmPlot: return "farmPlot";
                    case WorldObjectTargetKind.Destructible: return "destructible";
                    case WorldObjectTargetKind.Housing: return "housing";
                    case WorldObjectTargetKind.WorkArea: return "workArea";
                    case WorldObjectTargetKind.RecoverySpot: return "recoverySpot";
                    case WorldObjectTargetKind.StorageRoom: return "storageRoom";
                    case WorldObjectTargetKind.OpportunityObject: return "opportunityObject";
                    default: return string.Empty;
                }
            }
        }
        public string StableObjectId =>
            Kind == WorldObjectTargetKind.FactionFlag ? FactionFlagId :
            Kind == WorldObjectTargetKind.FarmPlot || Kind == WorldObjectTargetKind.RecoverySpot || Kind == WorldObjectTargetKind.StorageRoom
                ? Plot != null ? Plot.StableCellId : string.Empty :
            Kind == WorldObjectTargetKind.Destructible ? Destructible != null ? Destructible.PlacementId : string.Empty :
            Kind == WorldObjectTargetKind.OpportunityObject ? OpportunityObject?.WorldObjectInstanceId ?? string.Empty :
            WorkAreaId;
        public string StableTargetKey => string.IsNullOrEmpty(KindKey) || string.IsNullOrEmpty(StableObjectId)
            ? string.Empty : KindKey + ":" + StableObjectId;

        public bool TryCreateContentContext(EntityId actor, out ContentInteractionContext context)
        {
            context = null;
            if (actor.IsNone || string.IsNullOrEmpty(StableTargetKey)) return false;
            context = new ContentInteractionContext
            {
                ActorId = actor,
                TargetKind = KindKey,
                TargetKey = StableTargetKey,
                TargetDefinitionId = Kind == WorldObjectTargetKind.OpportunityObject
                    ? OpportunityObject?.OpportunityDefinitionId ?? string.Empty : StableObjectId,
                TargetDisplayName = DisplayLabel
            };
            return true;
        }
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
            out WorldObjectInteractionTarget target, WorldObjectPickPurpose purpose = WorldObjectPickPurpose.PlayerInteraction)
        {
            target = default;
            var world = host?.Session?.World;
            if (world == null) return false;
            MapLayoutDefinition layout = null;
            var continuous = host.ContinuousOutdoorSurfaceRuntime;
            if (continuous == null || !continuous.IsActive) MapLayoutPick.TryGet(host.Session, out layout);

            if (purpose == WorldObjectPickPurpose.PlayerInteraction &&
                HostDynamicWorldObjectRegistry.TryPick(point, out var opportunityObject))
            {
                target = new WorldObjectInteractionTarget(WorldObjectTargetKind.OpportunityObject,
                    displayLabel: opportunityObject.DisplayLabel,
                    approachPosition: opportunityObject.ApproachPosition,
                    opportunityObject: opportunityObject);
                return true;
            }

            if (HostControlCoreQuery.TryPickAtWorld(world, layout, continuous, point, out var coreId) &&
                world.ControlCores.TryGet(coreId, out var core))
            {
                if (!HostControlCoreQuery.TryGetApproachPoint(world, layout, continuous, core, out var approach))
                    HostControlCoreQuery.TryGetCenter(world, layout, continuous, core, out approach);
                target = new WorldObjectInteractionTarget(WorldObjectTargetKind.ControlCore, coreId,
                    displayLabel: string.IsNullOrEmpty(core.Name) ? "议政厅" : core.Name,
                    approachPosition: approach);
                return true;
            }

            if (TryPickFactionFlag(world, continuous, point, out var flag))
            {
                UnityEngine.Vector3 approach;
                XianXia.Core.Navigation.WalkGrid grid = null;
                if (continuous != null) continuous.TryGetCompositeWalkGrid(out grid);
                if (!HostFactionFlagQuery.TryGetApproachPoint(flag, continuous, grid, out approach))
                    HostFactionFlagQuery.TryGetCenter(flag, continuous, out approach);
                target = new WorldObjectInteractionTarget(WorldObjectTargetKind.FactionFlag,
                    factionFlagId: flag.FlagId,
                    displayLabel: "势力旗·" + StrategicFactionCatalog.DisplayName(flag.FactionId),
                    approachPosition: approach);
                return true;
            }

            if (HostMapObjectRegistry.TryPickPlot(point, out var plot) && plot.IsRecoverySpot)
            {
                target = new WorldObjectInteractionTarget(WorldObjectTargetKind.RecoverySpot, plot: plot,
                    displayLabel: plot.KindDisplayName());
                return true;
            }

            if (plot != null && plot.IsStorageRoom)
            {
                target = new WorldObjectInteractionTarget(WorldObjectTargetKind.StorageRoom, plot: plot,
                    displayLabel: plot.KindDisplayName());
                return true;
            }

            if (plot != null && plot.IsPlantableField)
            {
                target = new WorldObjectInteractionTarget(WorldObjectTargetKind.FarmPlot, plot: plot,
                    displayLabel: plot.KindDisplayName());
                return true;
            }

            if (HostMapObjectRegistry.TryPickDestructible(point, out var destructible))
            {
                target = new WorldObjectInteractionTarget(WorldObjectTargetKind.Destructible,
                    destructible: destructible, displayLabel: destructible.DisplayName);
                return true;
            }

            // Logical schedule anchors have no physical presentation on the continuous map.
            // NPC work selection may still resolve them; legacy LocalMap keeps its click behavior.
            if (continuous != null && continuous.IsActive && purpose == WorldObjectPickPurpose.PlayerInteraction)
                return false;

            if (TryPickWorkArea(world, point, 6.5f, housing: true, out var workAreaId))
            {
                target = new WorldObjectInteractionTarget(WorldObjectTargetKind.Housing, workAreaId,
                    displayLabel: ResolveWorkAreaName(world, workAreaId), approachPosition: point);
                return true;
            }
            if (TryPickWorkArea(world, point, 6.5f, housing: false, out workAreaId))
            {
                target = new WorldObjectInteractionTarget(WorldObjectTargetKind.WorkArea, workAreaId,
                    displayLabel: ResolveWorkAreaName(world, workAreaId), approachPosition: point);
                return true;
            }
            return false;
        }

        public static bool TryResolveStableTarget(PlayableHostBootstrap host, string kind, string stableId,
            out WorldObjectInteractionTarget target)
        {
            target = default;
            var world = host?.Session?.World;
            if (world == null || string.IsNullOrEmpty(kind) || string.IsNullOrEmpty(stableId)) return false;
            MapLayoutDefinition layout = null;
            var continuous = host.ContinuousOutdoorSurfaceRuntime;
            if (continuous == null || !continuous.IsActive) MapLayoutPick.TryGet(host.Session, out layout);
            if (string.Equals(kind, "opportunityObject", StringComparison.OrdinalIgnoreCase) &&
                HostDynamicWorldObjectRegistry.TryResolve(stableId, out var opportunityObject) &&
                world.WorldOpportunities.TryGetByWorldObject(stableId, out var opportunityInstance) &&
                opportunityInstance.IsDiscovered)
            {
                target = new WorldObjectInteractionTarget(WorldObjectTargetKind.OpportunityObject,
                    displayLabel: opportunityObject.DisplayLabel,
                    approachPosition: opportunityObject.ApproachPosition,
                    opportunityObject: opportunityObject);
                return true;
            }
            if (string.Equals(kind, "controlCore", StringComparison.OrdinalIgnoreCase) &&
                world.ControlCores.TryGet(stableId, out var core))
            {
                if (!HostControlCoreQuery.TryGetApproachPoint(
                        world, layout, continuous, core, out var coreApproach) &&
                    !HostControlCoreQuery.TryGetCenter(
                        world, layout, continuous, core, out coreApproach)) return false;
                target = new WorldObjectInteractionTarget(WorldObjectTargetKind.ControlCore, stableId,
                    displayLabel: string.IsNullOrEmpty(core.Name) ? "议政厅" : core.Name,
                    approachPosition: coreApproach);
                return true;
            }
            if (string.Equals(kind, "factionFlag", StringComparison.OrdinalIgnoreCase) &&
                world.Strategic.FactionFlags.Flags.TryGetValue(stableId, out var flag) && flag != null)
            {
                XianXia.Core.Navigation.WalkGrid grid = null;
                if (continuous != null) continuous.TryGetCompositeWalkGrid(out grid);
                if (!HostFactionFlagQuery.TryGetApproachPoint(flag, continuous, grid, out var approach) &&
                    !HostFactionFlagQuery.TryGetCenter(flag, continuous, out approach)) return false;
                target = new WorldObjectInteractionTarget(WorldObjectTargetKind.FactionFlag,
                    factionFlagId: stableId,
                    displayLabel: "势力旗·" + StrategicFactionCatalog.DisplayName(flag.FactionId),
                    approachPosition: approach);
                return true;
            }
            var plots = HostMapObjectRegistry.AllPlots;
            for (var i = 0; i < plots.Count; i++)
            {
                var plot = plots[i];
                if (plot == null || !string.Equals(plot.StableCellId, stableId, StringComparison.Ordinal)) continue;
                var plotKind = plot.IsRecoverySpot ? WorldObjectTargetKind.RecoverySpot :
                    plot.IsStorageRoom ? WorldObjectTargetKind.StorageRoom :
                    plot.IsPlantableField ? WorldObjectTargetKind.FarmPlot : WorldObjectTargetKind.None;
                var expected = new WorldObjectInteractionTarget(plotKind, plot: plot,
                    displayLabel: plot.KindDisplayName());
                if (plotKind != WorldObjectTargetKind.None &&
                    string.Equals(expected.KindKey, kind, StringComparison.OrdinalIgnoreCase))
                { target = expected; return true; }
            }
            if (string.Equals(kind, "destructible", StringComparison.OrdinalIgnoreCase))
            {
                var objects = HostMapObjectRegistry.AllDestructibles;
                for (var i = 0; i < objects.Count; i++)
                    if (objects[i] != null && !objects[i].IsDestroyed &&
                        string.Equals(objects[i].PlacementId, stableId, StringComparison.Ordinal))
                    {
                        target = new WorldObjectInteractionTarget(WorldObjectTargetKind.Destructible,
                            destructible: objects[i], displayLabel: objects[i].DisplayName);
                        return true;
                    }
            }
            if ((string.Equals(kind, "housing", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(kind, "workArea", StringComparison.OrdinalIgnoreCase)) &&
                world.TryGetWorkArea(stableId, out var area) && area != null &&
                TryGetWorkAreaApproach(world, area, out var workApproach))
            {
                var targetKind = string.Equals(kind, "housing", StringComparison.OrdinalIgnoreCase)
                    ? WorldObjectTargetKind.Housing : WorldObjectTargetKind.WorkArea;
                target = new WorldObjectInteractionTarget(targetKind, stableId,
                    displayLabel: ResolveWorkAreaName(world, stableId), approachPosition: workApproach);
                return true;
            }
            return false;
        }

        static bool TryGetWorkAreaApproach(SimulationWorld world, WorkAreaDefinition area, out Vector3 point)
        {
            point = default;
            if (area == null || !HostZoneQuery.TryGetLocationCenter(world, area.LocationId, out var center)) return false;
            var p = HostPresentationSpace.ToPresentation(center);
            point = HostPresentationSpace.FromPresentation(p.x + area.OffsetX, p.y + area.OffsetZ);
            return true;
        }

        static bool TryPickFactionFlag(SimulationWorld world,
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
            return false;
        }

        static bool IsContinuousFlagLoaded(FactionFlagState flag, ContinuousOutdoorSurfaceRuntime continuous)
        {
            return flag.HasWorldPosition &&
                   string.Equals(flag.SurfaceId, continuous.ActiveSurfaceId, StringComparison.Ordinal) &&
                   continuous.IsWorldPositionLoaded(
                       continuous.ActiveSurfaceId, flag.WorldX, flag.WorldY);
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
                     !world.LocalPlaces.TryGet(area.LocationId, out loc))) continue;
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
