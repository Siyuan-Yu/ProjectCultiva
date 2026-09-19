using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Persistence;
using XianXia.Core.Simulation;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Save 前：把 EntityView 真实表现坐标回写到 Domain，供 LoadedLocalMap Placement Capture。
    /// HostMoveController.SyncLocation 仅在靠近 WorldRegion 地点时才写 Override，远离 Zone 时会漏采。
    /// Active Separate Space：同步该图全部 persistent Character（含 NPC／stranded），不只 occupants。
    /// </summary>
    public static class HostSnapshotLocalPlacementCaptureSync
    {
        public static int SyncLoadedLocalMapOccupantsFromViews(PlayableHostBootstrap bootstrap)
        {
            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return 0;

            var world = bootstrap.Session.World;
            var spawner = bootstrap.ViewSpawner;
            if (world?.LocalMap == null || spawner == null)
                return 0;
            var continuous = bootstrap.ContinuousOutdoorSurfaceRuntime;
            if (world.Strategic.CharacterEncounter != null)
            { continuous?.CaptureIndependentField(); return 0; }
            if (continuous != null && continuous.IsActive && !world.LocalMap.IsInInterior)
                return continuous.CaptureCurrentPersonalPlacements();

            var mapId = world.LocalMap.ActiveMapLayoutId?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(mapId))
                return 0;

            var synced = 0;
            foreach (var entity in world.Entities.All)
            {
                if (entity == null || (entity.Tags & EntityTag.Character) == 0)
                    continue;
                if (!LoadedLocalMapPlacementSnapshotRestore.BelongsToActiveSeparateSpaceMap(
                        world, entity, mapId))
                    continue;
                if (!spawner.Registry.TryGet(entity.Id, out var view) || view == null)
                    continue;

                if (!entity.TryGet<EntityLocationComponent>(out var loc) || loc == null)
                {
                    loc = new EntityLocationComponent();
                    entity.AddComponent(loc);
                }

                var p = HostPresentationSpace.ToPresentation(view.transform.position);
                loc.SetPresentationOverride(p.x, p.y);
                synced++;
            }

            return synced;
        }
    }
}
