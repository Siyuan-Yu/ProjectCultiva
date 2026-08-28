using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Exploration;
using XianXia.Core.Persistence;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Unity.Host
{
    /// <summary>WorldSite Snapshot LocalPlacement 一次性 Load Trace。</summary>
    public static class HostSnapshotLocalPlacementTrace
    {
        public static void LogWorldSiteLocalRestore(
            SimulationWorld world,
            EntityId id,
            string localMapId,
            string resolvedSpawnReason)
        {
            if (world == null || id.IsNone)
                return;

            var hasSaved = LoadedLocalMapPlacementSnapshotRestore.TryGetPendingPlacement(
                id.Value,
                localMapId,
                out var sx,
                out var sz);

            var worldLoc = LoadedLocalMapPlacementSnapshotRestore.DescribeWorldLocation(world, id);
            Debug.Log(
                "[WorldSiteLocalRestore] CharacterId=" + id.Value +
                " WorldLocation=" + worldLoc +
                " LocalMapId=" + (localMapId ?? string.Empty) +
                " SavedLocalPosition=(" + sx.ToString("0.##") + "," + sz.ToString("0.##") + ")" +
                " HasSavedLocalPosition=" + hasSaved +
                " ResolvedSpawnReason=" + resolvedSpawnReason);
        }

        public static void LogSnapshotLocalPlacement(
            SimulationWorld world,
            EntityId id,
            string localMapId,
            string placementSource,
            string phase)
        {
            if (world == null || id.IsNone)
                return;

            var hasSaved = LoadedLocalMapPlacementSnapshotRestore.TryGetPendingPlacement(
                id.Value,
                localMapId,
                out var sx,
                out var sz);
            var placementFound = hasSaved;

            float px = 0f;
            float pz = 0f;
            if (world.Entities.TryGet(id, out var ent) &&
                ent != null &&
                ent.TryGet<EntityLocationComponent>(out var loc) &&
                loc != null &&
                loc.HasPresentationOverride)
            {
                px = loc.PresentationOverrideX;
                pz = loc.PresentationOverrideZ;
            }

            var worldLoc = LoadedLocalMapPlacementSnapshotRestore.DescribeWorldLocation(world, id);
            Debug.Log(
                "[SnapshotLocalPlacement] phase=" + phase +
                " CharacterId=" + id.Value +
                " WorldLocation=" + worldLoc +
                " LocalMapId=" + (localMapId ?? string.Empty) +
                " SavedLocalPosition=(" + sx.ToString("0.##") + "," + sz.ToString("0.##") + ")" +
                " PlacementFound=" + placementFound +
                " ResolvedSpawnPosition=(" + px.ToString("0.##") + "," + pz.ToString("0.##") + ")" +
                " PlacementSource=" + placementSource +
                " PositionAfter" + phase + "=(" + px.ToString("0.##") + "," + pz.ToString("0.##") + ")");
        }

        public static void LogPartyMembersAfterPhase(
            SimulationWorld world,
            PlayerPartyRuntime party,
            string localMapId,
            string phase)
        {
            if (world == null || party == null || party.Count == 0)
                return;

            for (var i = 0; i < party.Members.Count; i++)
                LogSnapshotLocalPlacement(world, party.Members[i], localMapId, "-", phase);
        }
    }
}
