using XianXia.Core.Exploration;
using XianXia.Core.Simulation;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Save 前：把 EntityView 真实表现坐标回写到 Domain，供 LoadedLocalMap Placement Capture。
    /// HostMoveController.SyncLocation 仅在靠近 WorldRegion 地点时才写 Override，远离 Zone 时会漏采。
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

            var mapId = world.LocalMap.ActiveMapLayoutId?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(mapId))
                return 0;

            var synced = 0;
            var occupants = world.LocalMap.OccupantIds;
            for (var i = 0; i < occupants.Count; i++)
            {
                var id = occupants[i];
                if (id.IsNone)
                    continue;
                if (!spawner.Registry.TryGet(id, out var view) || view == null)
                    continue;
                if (!world.Entities.TryGet(id, out var ent) || ent == null)
                    continue;

                if (!ent.TryGet<EntityLocationComponent>(out var loc) || loc == null)
                {
                    loc = new EntityLocationComponent();
                    ent.AddComponent(loc);
                }

                var p = HostPresentationSpace.ToPresentation(view.transform.position);
                loc.SetPresentationOverride(p.x, p.y);
                world.LocalMap.AddOccupant(id);
                synced++;
            }

            return synced;
        }
    }
}
