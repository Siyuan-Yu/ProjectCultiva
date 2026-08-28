using System.Collections.Generic;
using XianXia.Core.Persistence;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Persistence
{
    public static class FormalArmySnapshotRestore
    {
        public static void Apply(SimulationWorld world, FormalArmy army, FormalArmySnapshotDto dto)
        {
            if (world == null || army == null || dto == null)
                return;

            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            var motion = army.WorldMotion;
            var currentHex = new HexCoord(dto.CurrentHexQ, dto.CurrentHexR);
            var destHex = new HexCoord(dto.DestinationHexQ, dto.DestinationHexR);

            var hasPhase3Location = dto.LocationKind > 0 &&
                                    (dto.LocationKind == (int)FormalArmyLocationKind.AtWorldSite
                                        ? !string.IsNullOrEmpty(dto.SiteId)
                                        : dto.WorldX != 0f || dto.WorldY != 0f);

            if (hasPhase3Location &&
                dto.LocationKind == (int)FormalArmyLocationKind.AtWorldSite &&
                world.Strategic.Sites.TryGet(dto.SiteId, out var site) &&
                site != null)
            {
                site.EnsurePresenceHexValid();
                motion.SetAtWorldSite(dto.SiteId, site.AnchorHex, hexSize);
            }
            else if (hasPhase3Location)
            {
                var derived = HexMath.WorldToHex(dto.WorldX, dto.WorldY, hexSize);
                motion.SetAtWorldPosition(new WorldVec2(dto.WorldX, dto.WorldY), derived);
            }
            else if (world.Strategic.Sites.TryGetAtHex(currentHex, out var legacySite) && legacySite != null)
            {
                legacySite.EnsurePresenceHexValid();
                motion.SetAtWorldSite(legacySite.SiteId, legacySite.AnchorHex, hexSize);
            }
            else
            {
                HexMath.ToWorldPosition(currentHex, hexSize, out var x, out var y);
                motion.SetAtWorldPosition(new WorldVec2(x, y), currentHex);
            }

            if (dto.HexPath != null && dto.HexPath.Count >= 2)
            {
                var path = new List<HexCoord>(dto.HexPath.Count);
                for (var p = 0; p < dto.HexPath.Count; p++)
                {
                    var c = dto.HexPath[p];
                    if (c != null)
                        path.Add(new HexCoord(c.Q, c.R));
                }

                var orderKind = dto.CurrentOrderKind > 0
                    ? (FormalArmyOrderKind)dto.CurrentOrderKind
                    : FormalArmyOrderKind.TravelToHex;
                var segmentIndex = dto.SegmentIndex > 0 ? dto.SegmentIndex : dto.CurrentPathIndex;
                var segmentProgress = dto.SegmentProgress > 0f ? dto.SegmentProgress : dto.StepProgress;
                motion.RestorePath(
                    orderKind,
                    path,
                    destHex,
                    dto.DestinationSiteId ?? string.Empty,
                    segmentIndex,
                    segmentProgress,
                    dto.OrderTargetArmyId);
                army.State = FormalArmyState.Moving;
            }
            else if (dto.CurrentOrderKind == (int)FormalArmyOrderKind.AttackFormalArmy &&
                     !string.IsNullOrEmpty(dto.OrderTargetArmyId))
            {
                motion.SetAttackOrder(dto.OrderTargetArmyId);
            }

            army.SyncLegacyFromWorldMotion();
            FormalArmyMemberPresenceSync.SyncAll(world, army);
        }
    }
}
