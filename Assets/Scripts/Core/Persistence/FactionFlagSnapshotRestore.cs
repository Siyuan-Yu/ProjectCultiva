using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Persistence
{
    /// <summary>FactionFlag Snapshot active-set 的验证、诊断与原子提交入口。</summary>
    public static class FactionFlagSnapshotRestore
    {
        public static Result TryApplyAuthoritativeSet(SimulationWorld world, StrategicSnapshotDto dto)
        {
            if (world?.Strategic?.FactionFlags == null || dto == null)
                return Result.Failure(ErrorCode.InvalidArgument, "FactionFlag snapshot restore requires world and dto.");
            if (!dto.HasFactionFlagSnapshotAuthority)
                return Result.Success();

            var source = dto.FactionFlags == null
                ? new List<FactionFlagSnapshotDto>()
                : new List<FactionFlagSnapshotDto>(dto.FactionFlags);
            var validated = new List<FactionFlagState>(source.Count);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var orders = new HashSet<long>();
            var contentTemplates = new Dictionary<string, FactionFlagState>(StringComparer.Ordinal);
            foreach (var pair in world.Strategic.FactionFlags.Flags)
                if (pair.Value != null)
                    contentTemplates[pair.Key] = pair.Value;
            for (var i = 0; i < source.Count; i++)
            {
                var item = source[i];
                if (item == null)
                    return Invalid(i, null, "entry is null");
                if (item.SiteCoreFormat != 1)
                    return Invalid(i, item, "legacy Site-Core format requires offline conversion");
                if (string.IsNullOrWhiteSpace(item.FlagId)) return Invalid(i, item, "FlagId is empty");
                if (string.IsNullOrWhiteSpace(item.FactionId)) return Invalid(i, item, "FactionId is empty");
                if (!ids.Add(item.FlagId)) return Invalid(i, item, "duplicate FlagId");
                if (item.EstablishedOrder <= 0) return Invalid(i, item, "EstablishedOrder must be positive");
                if (!orders.Add(item.EstablishedOrder)) return Invalid(i, item, "duplicate EstablishedOrder");
                if (item.MaxHp <= 0 || item.CurrentHp <= 0 || item.CurrentHp > item.MaxHp)
                    return Invalid(i, item, "HP must satisfy 0 < CurrentHp <= MaxHp");
                if (item.HasLocalPosition && (!IsFinite(item.LocalX) || !IsFinite(item.LocalZ)))
                    return Invalid(i, item, "local position is not finite");
                if (item.HasWorldPosition && (!IsFinite(item.WorldX) || !IsFinite(item.WorldY)))
                    return Invalid(i, item, "world position is not finite");

                contentTemplates.TryGetValue(item.FlagId, out var contentTemplate);
                var authored = contentTemplate != null && contentTemplate.IsAuthoredSiteCore
                    ? contentTemplate : null;
                var hasWorldPosition = item.HasWorldPosition;
                var worldX = item.WorldX;
                var worldY = item.WorldY;
                var siteId = item.SiteId ?? string.Empty;
                var surfaceId = item.SurfaceId ?? string.Empty;
                var isSiteCore = item.IsSiteCore;
                if (isSiteCore && (!hasWorldPosition || string.IsNullOrWhiteSpace(surfaceId) ||
                                   !string.Equals(siteId, FactionFlagService.SiteIdForCoreFlag(item.FlagId),
                                       StringComparison.Ordinal)))
                    return Invalid(i, item, "Site-Core identity requires precise position, Surface, and stable SiteId");
                if (isSiteCore && world.SurfaceSpatial != null &&
                    (!world.SurfaceSpatial.TryGet(surfaceId, out var surface) || surface == null ||
                     !surface.ContainsWorldPosition(worldX, worldY)))
                    return Invalid(i, item, "Site-Core world position is outside its Surface");
                if (item.SiteCoreFormat == 1 && authored != null && !isSiteCore)
                    return Invalid(i, item, "authored Site-Core flag is disabled by an authoritative snapshot entry");

                validated.Add(new FactionFlagState
                {
                    FlagId = item.FlagId,
                    FactionId = item.FactionId,
                    EstablishedOrder = item.EstablishedOrder,
                    CurrentHp = item.CurrentHp,
                    MaxHp = item.MaxHp,
                    HasLocalPosition = item.HasLocalPosition,
                    LocalX = item.LocalX,
                    LocalZ = item.LocalZ,
                    HasWorldPosition = hasWorldPosition,
                    WorldX = worldX,
                    WorldY = worldY,
                    SiteId = siteId,
                    SurfaceId = surfaceId,
                    IsSiteCore = isSiteCore,
                    IsAuthoredSiteCore = authored != null,
                    IsWorldMapDebugOnly = contentTemplate?.IsWorldMapDebugOnly ?? false,
                    AuthoredSiteDisplayName = authored?.AuthoredSiteDisplayName ?? string.Empty,
                    AuthoredSiteType = authored?.AuthoredSiteType ?? string.Empty,
                    AuthoredCoreLevel = authored?.AuthoredCoreLevel ?? 1
                });
            }

            if (!world.Strategic.FactionFlags.TryReplaceAll(validated, out var rejected))
            {
                return Result.Failure(ErrorCode.SnapshotInvalid,
                    "FactionFlag active-set atomic commit failed.",
                    Describe(rejected));
            }

            Log("FlagSnapshotRestored", validated);
#if DEBUG || UNITY_EDITOR || DEVELOPMENT_BUILD
            var missing = new List<string>();
            var extra = new List<string>();
            foreach (var id in ids)
                if (!world.Strategic.FactionFlags.Flags.ContainsKey(id)) missing.Add(id);
            foreach (var pair in world.Strategic.FactionFlags.Flags)
                if (!ids.Contains(pair.Key)) extra.Add(pair.Key);
            if (missing.Count > 0 || extra.Count > 0)
                Debug.Fail("[FlagSnapshotRestored] active set mismatch. Missing=" +
                    string.Join(",", missing) + " Extra=" + string.Join(",", extra));
#endif
            return Result.Success();
        }

        public static void LogDtos(string stage, IReadOnlyList<FactionFlagSnapshotDto> flags)
        {
#if DEBUG || UNITY_EDITOR || DEVELOPMENT_BUILD
            var lines = new List<string>();
            if (flags != null)
                for (var i = 0; i < flags.Count; i++)
                {
                    var f = flags[i];
                    lines.Add(f == null ? "<null>" : f.FlagId + " @ (" + f.AnchorQ + "," + f.AnchorR + ") Order=" + f.EstablishedOrder);
                }
            Debug.WriteLine("[" + stage + "] Count=" + lines.Count + " " + string.Join("; ", lines));
#endif
        }

        public static void Log(string stage, IReadOnlyList<FactionFlagState> flags)
        {
#if DEBUG || UNITY_EDITOR || DEVELOPMENT_BUILD
            var sb = new StringBuilder();
            sb.Append('[').Append(stage).Append("] Count=").Append(flags?.Count ?? 0);
            if (flags != null)
                for (var i = 0; i < flags.Count; i++)
                {
                    var f = flags[i];
                    if (f != null) sb.Append("; ").Append(Describe(f));
                }
            Debug.WriteLine(sb.ToString());
#endif
        }

        static Result Invalid(int index, FactionFlagSnapshotDto item, string reason) =>
            Result.Failure(ErrorCode.SnapshotInvalid, "Invalid FactionFlag snapshot entry.",
                "Index=" + index + " " + Describe(item) + " Reason=" + reason);

        static string Describe(FactionFlagSnapshotDto f) => f == null
            ? "Flag=<null>"
            : "FlagId='" + (f.FlagId ?? string.Empty) + "' Anchor=(" + f.AnchorQ + "," + f.AnchorR + ") Order=" + f.EstablishedOrder;

        static string Describe(FactionFlagState f) => f == null
            ? "Flag=<null>"
            : "FlagId='" + (f.FlagId ?? string.Empty) + "' Order=" + f.EstablishedOrder;

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
