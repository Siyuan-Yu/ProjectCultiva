using System;
using System.Collections.Generic;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Persistence
{
    /// <summary>Hex/Site shell 就绪后的 FormalArmy motion Snapshot overlay。</summary>
    public static class FormalArmySnapshotRestore
    {
        public static Result Validate(SimulationWorld world, FormalArmySnapshotDto dto)
        {
            if (world == null || dto == null)
                return Result.Failure(ErrorCode.InvalidArgument, "FormalArmy motion restore requires world and dto.");
            if (!IsFinite(dto.WorldX) || !IsFinite(dto.WorldY) ||
                !IsFinite(dto.SegmentProgress) || dto.SegmentProgress < 0f || dto.SegmentProgress > 1f)
                return Invalid(dto, "world position or segment progress is invalid");
            if (dto.LocationKind == 0 &&
                (!IsFinite(dto.StepProgress) || dto.StepProgress < 0f || dto.StepProgress > 1f))
                return Invalid(dto, "legacy step progress is invalid");

            if (dto.LocationKind == (int)FormalArmyLocationKind.AtWorldSite)
            {
                if (string.IsNullOrWhiteSpace(dto.SiteId) ||
                    !world.Strategic.Sites.TryGet(dto.SiteId, out var site) || site == null)
                    return Invalid(dto, "AtWorldSite references a missing SiteId");
            }
            else if (dto.LocationKind != 0 &&
                     dto.LocationKind != (int)FormalArmyLocationKind.AtWorldPosition)
                return Invalid(dto, "unknown LocationKind");

            var current = new HexCoord(dto.CurrentHexQ, dto.CurrentHexR);
            if (world.HexWorld == null || !world.HexWorld.IsInBounds(current.Q, current.R))
                return Invalid(dto, "CurrentHex is out of bounds");
            if (dto.HexPath != null)
                for (var i = 0; i < dto.HexPath.Count; i++)
                {
                    var p = dto.HexPath[i];
                    if (p == null || !world.HexWorld.IsInBounds(p.Q, p.R))
                        return Invalid(dto, "HexPath contains an invalid coordinate at index " + i);
                }
            var pathCount = dto.HexPath?.Count ?? 0;
            var segmentIndex = dto.LocationKind > 0 ? dto.SegmentIndex : dto.CurrentPathIndex;
            if ((pathCount >= 2 && (segmentIndex < 0 || segmentIndex >= pathCount - 1)) ||
                (pathCount < 2 && segmentIndex != 0))
                return Invalid(dto, "segment index is outside the saved path");
            if (!Enum.IsDefined(typeof(HexTravelMode), dto.TravelMode))
                return Invalid(dto, "unknown TravelMode");

            if (dto.HasSiteDepartureState && dto.IsSiteDeparturePending)
            {
                if (dto.LocationKind != (int)FormalArmyLocationKind.AtWorldSite)
                    return Invalid(dto, "site departure requires AtWorldSite location");
                if (dto.HexPath == null || dto.HexPath.Count < 2)
                    return Invalid(dto, "pending site departure requires an active path");
                if (!IsFinite(dto.SiteDepartureVirtualX) || !IsFinite(dto.SiteDepartureVirtualY) ||
                    !IsFinite(dto.SiteDepartureBoundaryX) || !IsFinite(dto.SiteDepartureBoundaryY))
                    return Invalid(dto, "site departure position is not finite");
                if (!world.HexWorld.IsInBounds(dto.SiteDepartureFootprintQ, dto.SiteDepartureFootprintR) ||
                    !world.HexWorld.IsInBounds(dto.SiteDepartureExitQ, dto.SiteDepartureExitR))
                    return Invalid(dto, "site departure footprint/exit is out of bounds");
            }
            return Result.Success();
        }

        public static Result Apply(SimulationWorld world, FormalArmy army, FormalArmySnapshotDto dto)
        {
            var valid = Validate(world, dto);
            if (valid.IsFailure)
                return valid;
            if (army == null)
                return Result.Failure(ErrorCode.SnapshotInvalid, "FormalArmy motion target is null.", dto.ArmyId);

            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            var motion = army.WorldMotion;
            var currentHex = new HexCoord(dto.CurrentHexQ, dto.CurrentHexR);
            if (dto.LocationKind == (int)FormalArmyLocationKind.AtWorldSite)
            {
                world.Strategic.Sites.TryGet(dto.SiteId, out var site);
                site.EnsurePresenceHexValid();
                motion.SetAtWorldSite(dto.SiteId, site.AnchorHex, hexSize);
            }
            else if (dto.LocationKind == (int)FormalArmyLocationKind.AtWorldPosition)
            {
                // LocationKind 是 field authority；(0,0) 是合法连续世界坐标。
                motion.SetAtWorldPosition(new WorldVec2(dto.WorldX, dto.WorldY), currentHex);
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

            var path = new List<HexCoord>(dto.HexPath?.Count ?? 0);
            if (dto.HexPath != null)
                for (var i = 0; i < dto.HexPath.Count; i++)
                    path.Add(new HexCoord(dto.HexPath[i].Q, dto.HexPath[i].R));
            var orderKind = dto.CurrentOrderKind > 0
                ? (FormalArmyOrderKind)dto.CurrentOrderKind
                : path.Count >= 2 ? FormalArmyOrderKind.TravelToHex : FormalArmyOrderKind.None;
            var hasMotionAuthority = dto.LocationKind > 0;
            var segmentIndex = hasMotionAuthority ? dto.SegmentIndex : dto.CurrentPathIndex;
            var segmentProgress = hasMotionAuthority ? dto.SegmentProgress : dto.StepProgress;
            motion.RestoreSnapshotMotion(
                orderKind, path, currentHex,
                new HexCoord(dto.DestinationHexQ, dto.DestinationHexR),
                dto.DestinationSiteId, (HexTravelMode)dto.TravelMode,
                segmentIndex, segmentProgress, dto.OrderTargetArmyId,
                dto.HasSiteDepartureState, dto.IsSiteDeparturePending,
                new WorldVec2(dto.SiteDepartureVirtualX, dto.SiteDepartureVirtualY),
                new WorldVec2(dto.SiteDepartureBoundaryX, dto.SiteDepartureBoundaryY),
                new HexCoord(dto.SiteDepartureFootprintQ, dto.SiteDepartureFootprintR),
                new HexCoord(dto.SiteDepartureExitQ, dto.SiteDepartureExitR));
            army.SyncLegacyFromWorldMotion();
            return Result.Success();
        }

        static Result Invalid(FormalArmySnapshotDto dto, string reason) =>
            Result.Failure(ErrorCode.SnapshotInvalid, "Invalid FormalArmy motion snapshot.",
                "ArmyId='" + (dto?.ArmyId ?? string.Empty) + "' Reason=" + reason);

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
