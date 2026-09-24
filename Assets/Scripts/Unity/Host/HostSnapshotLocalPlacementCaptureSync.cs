using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Persistence;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Save 前：把 EntityView 真实表现坐标回写到 Domain，供 LoadedLocalMap Placement Capture。
    /// EntityLocation override 是持久 placement，未必等于当前已物化 View 的实时位置；
    /// Save 前从 View 做最终捕获，避免写入 stale presentation。
    /// Active Separate Space：同步该图全部 persistent Character（含 NPC／stranded），不只 occupants。
    /// </summary>
    public static class HostSnapshotLocalPlacementCaptureSync
    {
        /// <summary>
        /// Freezes one active Separate Space Character's exact View position into its persistent
        /// EntityLocation placement. Does not change LocationId, membership or WorldPresence.
        /// </summary>
        public static bool TryCaptureCharacterPlacementFromView(
            SimulationWorld world,
            EntityViewSpawner spawner,
            EntityId id)
        {
            if (world?.LocalMap == null || !world.LocalMap.IsActive || spawner == null || id.IsNone)
                return false;

            var mapId = world.LocalMap.ActiveMapLayoutId?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(mapId) ||
                !world.Entities.TryGet(id, out var entity) || entity == null ||
                (entity.Tags & EntityTag.Character) == 0 ||
                !LoadedLocalMapPlacementSnapshotRestore.BelongsToActiveSeparateSpaceMap(
                    world, entity, mapId) ||
                !spawner.Registry.TryGet(id, out var view) || view == null)
                return false;

            if (!entity.TryGet<EntityLocationComponent>(out var loc) || loc == null)
            {
                loc = new EntityLocationComponent();
                entity.AddComponent(loc);
            }

            var p = HostPresentationSpace.ToPresentation(view.transform.position);
            loc.SetPresentationOverride(p.x, p.y);
            return true;
        }

        /// <summary>
        /// Final teardown boundary for an active Separate Space. Captures every persistent
        /// Character that belongs to the active map while LocalPlaces and Views still exist.
        /// </summary>
        public static int FlushActiveSeparateSpaceCharacterPlacementsFromViews(
            PlayableHostBootstrap bootstrap)
        {
            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return 0;

            var world = bootstrap.Session.World;
            var spawner = bootstrap.ViewSpawner;
            if (world?.LocalMap == null || !world.LocalMap.IsActive || spawner == null)
                return 0;

            var synced = 0;
            foreach (var entity in world.Entities.All)
            {
                if (entity != null &&
                    TryCaptureCharacterPlacementFromView(world, spawner, entity.Id))
                    synced++;
            }

            return synced;
        }

        public static int SyncLoadedLocalMapOccupantsFromViews(PlayableHostBootstrap bootstrap)
        {
            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return 0;

            var world = bootstrap.Session.World;
            var spawner = bootstrap.ViewSpawner;
            if (world?.LocalMap == null || spawner == null)
                return 0;
            var continuous = bootstrap.ContinuousOutdoorSurfaceRuntime;
            if (CharacterEncounterService.BlocksOrdinaryContinuousSurface(world))
            { continuous?.CaptureIndependentField(); return 0; }
            if (continuous != null && continuous.IsActive && !world.LocalMap.IsInInterior)
                return continuous.CaptureCurrentPersonalPlacements();

            var mapId = world.LocalMap.ActiveMapLayoutId?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(mapId))
                return 0;
            return FlushActiveSeparateSpaceCharacterPlacementsFromViews(bootstrap);
        }
    }
}
