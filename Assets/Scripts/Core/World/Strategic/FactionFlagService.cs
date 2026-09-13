using System;
using System.Collections.Generic;
using System.Globalization;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    public sealed class FactionFlagSitePlacementRequest
    {
        public string SurfaceId { get; set; } = string.Empty;
        public WorldVec2 WorldPosition { get; set; }
        public HexCoord StrategicAnchor { get; set; }
        public float PresentationX { get; set; }
        public float PresentationZ { get; set; }
    }

    public static class WorldSiteCoreCoverageResolver
    {
        public static bool Contains(WorldSite site, string surfaceId, float worldX, float worldY)
        {
            if (site == null || !site.IsCoreActive || !site.HasContinuousCore ||
                !string.Equals(site.CoreSurfaceId, surfaceId ?? string.Empty, StringComparison.Ordinal))
                return false;
            var halfW = site.CoreRangeWidth * .5f;
            var halfH = site.CoreRangeHeight * .5f;
            return worldX >= site.CoreWorldX - halfW && worldX <= site.CoreWorldX + halfW &&
                   worldY >= site.CoreWorldY - halfH && worldY <= site.CoreWorldY + halfH;
        }

        public static bool TryResolve(
            SimulationWorld world, string surfaceId, float worldX, float worldY, out WorldSite site)
        {
            site = null;
            if (world?.Strategic?.Sites == null) return false;
            foreach (var pair in world.Strategic.Sites.Sites)
            {
                var candidate = pair.Value;
                if (!Contains(candidate, surfaceId, worldX, worldY)) continue;
                if (site == null || candidate.ControlEstablishedOrder < site.ControlEstablishedOrder ||
                    (candidate.ControlEstablishedOrder == site.ControlEstablishedOrder &&
                     string.CompareOrdinal(candidate.SiteId, site.SiteId) < 0))
                    site = candidate;
            }
            return site != null;
        }

        public static List<HexCoord> BuildStrategicSummary(SimulationWorld world, WorldSite site)
        {
            var result = new List<HexCoord>();
            if (world?.HexWorld == null || site == null || !site.IsCoreActive)
                return result;
            if (!site.HasContinuousCore)
                return new List<HexCoord>(StrategicTerritoryCoverageResolver.ExpandOneRing(site.EnumerateFootprintHexes()));
            for (var r = 0; r < world.HexWorld.Height; r++)
            for (var q = 0; q < world.HexWorld.Width; q++)
            {
                var hex = new HexCoord(q, r);
                HexMath.ToWorldPosition(hex, world.HexWorld.HexSize, out var x, out var y);
                if (Contains(site, site.CoreSurfaceId, x, y)) result.Add(hex);
            }
            if (!result.Contains(site.AnchorHex) && world.HexWorld.Contains(site.AnchorHex))
                result.Add(site.AnchorHex);
            return result;
        }
    }

    /// <summary>阵营旗的政治生命周期。所有变更后只通过 Territory Resolver 重建派生控制。</summary>
    public static class FactionFlagService
    {
        /// <summary>V1 阵营旗建筑防御；伤害与近战建筑公式统一。</summary>
        public const int StructureDefense = 4;

        public static int ComputeAssaultDamage(Entity attacker)
            => StrategicBuildingDamage.Compute(attacker, StructureDefense);

        public static Result ValidatePlacement(
            SimulationWorld world, string factionId, HexCoord anchor, out int neutralHexGain)
        {
            neutralHexGain = 0;
            if (world?.Strategic == null || string.IsNullOrEmpty(factionId))
                return Result.Failure(ErrorCode.InvalidArgument, "阵营旗参数无效。");
            if (world.HexWorld == null || !world.HexWorld.TryGetCell(anchor, out var anchorCell) ||
                anchorCell == null || !anchorCell.IsPassable)
                return Result.Failure(ErrorCode.InvalidOperation, "阵营旗只能建立在合法可通行的荒野格。");
            if (world.Strategic.Sites.TryGetAtHex(anchor, out var site) && site != null)
                return Result.Failure(ErrorCode.InvalidOperation, "WorldSite 范围内不能建立阵营旗。");
            if (world.Strategic.FactionFlags.TryGetAt(anchor, out _))
                return Result.Failure(ErrorCode.InvalidOperation,
                    "此 Hex 已有阵营控制建筑，需要先移除当前控制建筑。");

            var anchorController = TerritoryControlService.GetController(world, anchor);
            if (!string.IsNullOrEmpty(anchorController) &&
                !string.Equals(anchorController, factionId, StringComparison.Ordinal))
                return Result.Failure(ErrorCode.InvalidOperation, "敌方有效领土内不能建立阵营旗。");

            var nominal = StrategicTerritoryCoverageResolver.ExpandOneRing(new[] { anchor });
            for (var i = 0; i < nominal.Count; i++)
            {
                var hex = nominal[i];
                if (!world.HexWorld.Contains(hex))
                    continue;
                if (string.IsNullOrEmpty(TerritoryControlService.GetController(world, hex)))
                    neutralHexGain++;
            }
            if (neutralHexGain <= 0)
                return Result.Failure(ErrorCode.InvalidOperation, "候选范围没有可新增的无主 Hex。");
            return Result.Success();
        }

        public static Result ValidateSiteCorePlacement(
            SimulationWorld world,
            string factionId,
            FactionFlagSitePlacementRequest request,
            float rangeWidth,
            float rangeHeight,
            out int neutralHexGain)
        {
            neutralHexGain = 0;
            if (world?.Strategic == null || request == null || string.IsNullOrWhiteSpace(factionId) ||
                string.IsNullOrWhiteSpace(request.SurfaceId) || !IsFinite(request.WorldPosition.X) ||
                !IsFinite(request.WorldPosition.Y) || rangeWidth <= 0f || rangeHeight <= 0f)
                return Result.Failure(ErrorCode.InvalidArgument, "新据点核心放置参数无效。");
            if (world.HexWorld == null || !world.HexWorld.TryGetCell(request.StrategicAnchor, out var cell) ||
                cell == null || !cell.IsPassable)
                return Result.Failure(ErrorCode.InvalidOperation, "核心只能建立在合法可通行的战略区域。");

            foreach (var pair in world.Strategic.Sites.Sites)
            {
                var site = pair.Value;
                if (site == null || !site.IsCoreActive) continue;
                if (site.HasContinuousCore)
                {
                    if (RectanglesOverlap(request.SurfaceId, request.WorldPosition.X, request.WorldPosition.Y,
                            rangeWidth, rangeHeight, site))
                        return Result.Failure(ErrorCode.InvalidOperation,
                            "此处与现有据点的有效核心范围重叠；CW-03 请在无争议空地建站。");
                }
                else if (site.OccupiesHex(request.StrategicAnchor))
                    return Result.Failure(ErrorCode.InvalidOperation, "此处属于现有预设据点，不能建立第二核心。");
            }
            foreach (var pair in world.Strategic.FactionFlags.Flags)
            {
                var flag = pair.Value;
                if (flag == null || !flag.HasWorldPosition ||
                    !string.Equals(flag.SurfaceId, request.SurfaceId, StringComparison.Ordinal)) continue;
                var dx = flag.WorldX - request.WorldPosition.X;
                var dy = flag.WorldY - request.WorldPosition.Y;
                if (dx * dx + dy * dy < 16f)
                    return Result.Failure(ErrorCode.InvalidOperation, "此处已有势力核心建筑。");
            }

            var controller = TerritoryControlService.GetController(world, request.StrategicAnchor);
            if (!string.IsNullOrEmpty(controller) && !string.Equals(controller, factionId, StringComparison.Ordinal))
                return Result.Failure(ErrorCode.InvalidOperation, "敌方有效控制区域内不能建立核心。");
            var probe = new WorldSite
            {
                AnchorHex = request.StrategicAnchor,
                CoreSurfaceId = request.SurfaceId,
                HasCoreWorldPosition = true,
                CoreWorldX = request.WorldPosition.X,
                CoreWorldY = request.WorldPosition.Y,
                CoreRangeWidth = rangeWidth,
                CoreRangeHeight = rangeHeight,
                IsCoreActive = true
            };
            foreach (var hex in WorldSiteCoreCoverageResolver.BuildStrategicSummary(world, probe))
                if (string.IsNullOrEmpty(TerritoryControlService.GetController(world, hex))) neutralHexGain++;
            if (neutralHexGain <= 0)
                return Result.Failure(ErrorCode.InvalidOperation, "候选范围没有可新增的无主区域。");
            return Result.Success();
        }

        public static long NextEstablishedOrder(SimulationWorld world)
        {
            long max = 0;
            if (world?.Strategic == null)
                return 1;
            foreach (var pair in world.Strategic.Sites.Sites)
                if (pair.Value != null && pair.Value.ControlEstablishedOrder > max)
                    max = pair.Value.ControlEstablishedOrder;
            foreach (var pair in world.Strategic.FactionFlags.Flags)
                if (pair.Value != null && pair.Value.EstablishedOrder > max)
                    max = pair.Value.EstablishedOrder;
            return max == long.MaxValue ? long.MaxValue : max + 1;
        }

        /// <summary>
        /// Deterministic runtime id. The suffix reserves the existing Snapshot-persisted world entity
        /// sequence, so same-tick rebuilds and Save/Load cannot reuse a prior runtime FlagId.
        /// </summary>
        public static string NextRuntimeFlagId(SimulationWorld world, string factionId, HexCoord anchor)
        {
            var owner = string.IsNullOrEmpty(factionId) ? "unknown" : factionId.Replace(':', '_');
            var stem = "flag:runtime:" + owner + ":" + anchor.Q.ToString(CultureInfo.InvariantCulture) +
                       ":" + anchor.R.ToString(CultureInfo.InvariantCulture) + ":";
            var suffix = world?.Entities?.Ids.Next().Value ?? 1UL;
            while (world?.Strategic != null &&
                   world.Strategic.FactionFlags.Flags.ContainsKey(stem + suffix.ToString(CultureInfo.InvariantCulture)))
                suffix = world.Entities.Ids.Next().Value;
            return stem + suffix.ToString(CultureInfo.InvariantCulture);
        }

        public static string SiteIdForCoreFlag(string flagId) =>
            string.IsNullOrWhiteSpace(flagId) ? string.Empty : "site:runtime:" + flagId.Trim();

        public static Result TryPlaceSiteCore(
            SimulationWorld world,
            string flagId,
            string factionId,
            FactionFlagSitePlacementRequest request,
            long establishedOrder,
            string displayName,
            string siteType,
            int initialLevel,
            float rangeWidth,
            float rangeHeight,
            out string siteId)
        {
            siteId = SiteIdForCoreFlag(flagId);
            if (world?.Strategic?.SpatialRules == null)
                return Result.Failure(ErrorCode.InvalidOperation, "Core control range catalog missing.");
            CoreLevelControlRange controlRange;
            try { controlRange = world.Strategic.SpatialRules.RequireLevel(initialLevel); }
            catch (InvalidOperationException ex) { return Result.Failure(ErrorCode.InvalidArgument, ex.Message); }
            rangeWidth = controlRange.WidthWorld;
            rangeHeight = controlRange.HeightWorld;
            var valid = ValidateSiteCorePlacement(
                world, factionId, request, rangeWidth, rangeHeight, out _);
            if (valid.IsFailure) return valid;
            if (string.IsNullOrEmpty(siteId) || initialLevel < 1)
                return Result.Failure(ErrorCode.InvalidArgument, "新据点身份或等级无效。");
            var site = new WorldSite
            {
                SiteId = siteId,
                DisplayName = (string.IsNullOrWhiteSpace(displayName) ? "新建据点" : displayName.Trim()) +
                              "（" + request.StrategicAnchor.Q.ToString(CultureInfo.InvariantCulture) + "," +
                              request.StrategicAnchor.R.ToString(CultureInfo.InvariantCulture) + "）",
                SiteType = string.IsNullOrWhiteSpace(siteType) ? "Outpost" : siteType.Trim(),
                OwnerFactionId = factionId,
                ControlEstablishedOrder = establishedOrder,
                UsesContinuousOutdoorSurface = true,
                IsRuntimeCreated = true,
                CoreAssetId = flagId,
                CoreSurfaceId = request.SurfaceId,
                HasCoreWorldPosition = true,
                CoreWorldX = request.WorldPosition.X,
                CoreWorldY = request.WorldPosition.Y,
                CoreLevel = initialLevel,
                CoreRangeWidth = rangeWidth,
                CoreRangeHeight = rangeHeight,
                IsCoreActive = true,
                CoreIsRemovable = true,
                AnchorHex = request.StrategicAnchor,
                PresenceHex = request.StrategicAnchor,
                LocalMapId = string.Empty
            };
            site.SetFootprint(new[] { request.StrategicAnchor });
            try { world.Strategic.Sites.Register(site); }
            catch (Exception ex)
            {
                return Result.Failure(ErrorCode.InvalidOperation, "新据点身份注册失败。", ex.Message);
            }
            var flag = new FactionFlagState
            {
                FlagId = flagId,
                FactionId = factionId,
                AnchorHex = request.StrategicAnchor,
                EstablishedOrder = establishedOrder,
                CurrentHp = 100,
                MaxHp = 100,
                HasLocalPosition = true,
                LocalX = request.PresentationX,
                LocalZ = request.PresentationZ,
                HasWorldPosition = true,
                WorldX = request.WorldPosition.X,
                WorldY = request.WorldPosition.Y,
                SiteId = siteId,
                SurfaceId = request.SurfaceId,
                IsSiteCore = true
            };
            if (!world.Strategic.FactionFlags.Register(flag))
            {
                world.Strategic.Sites.RemoveRuntimeSite(siteId);
                siteId = string.Empty;
                return Result.Failure(ErrorCode.InvalidOperation, "核心建筑身份注册失败。");
            }
            StrategicTerritoryCoverageResolver.Rebuild(world);
            return Result.Success();
        }

        public static Result TryPlace(
            SimulationWorld world, string flagId, string factionId, HexCoord anchor,
            long establishedOrder, float localX, float localZ, bool hasLocalPosition)
        {
            if (world?.Strategic == null || string.IsNullOrEmpty(flagId) || string.IsNullOrEmpty(factionId))
                return Result.Failure(ErrorCode.InvalidArgument, "阵营旗参数无效。");
            var placement = ValidatePlacement(world, factionId, anchor, out _);
            if (placement.IsFailure)
                return placement;
            var flag = new FactionFlagState
            {
                FlagId=flagId, FactionId=factionId, AnchorHex=anchor, EstablishedOrder=establishedOrder,
                CurrentHp=100, MaxHp=100, HasLocalPosition=hasLocalPosition, LocalX=localX, LocalZ=localZ
            };
            if (!world.Strategic.FactionFlags.Register(flag))
                return Result.Failure(ErrorCode.InvalidOperation, "阵营旗 ID 或锚点重复。");
            StrategicTerritoryCoverageResolver.Rebuild(world);
            return Result.Success();
        }

        public static Result TryApplyAssault(
            SimulationWorld world, PlayerPartyRuntime party, string attackerFactionId, string flagId, int damage)
        {
            if (world?.Strategic == null || party == null || !party.HasActive || damage <= 0)
                return Result.Failure(ErrorCode.InvalidArgument, "阵营旗突击参数无效。");
            var military = StrategicMilitaryRules.ValidatePlayerPartyCanInitiateStrategicMilitaryAction(world, party);
            if (military.IsFailure)
                return military;
            if (!world.Strategic.FactionFlags.Flags.TryGetValue(flagId ?? string.Empty, out var flag) || flag == null)
                return Result.Failure(ErrorCode.NotFound, "阵营旗不存在。");
            if (string.Equals(attackerFactionId, flag.FactionId, StringComparison.Ordinal))
                return Result.Failure(ErrorCode.InvalidOperation, "不能攻击己方阵营旗。");
            if (!WarGateService.CanAttack(world, attackerFactionId, flag.FactionId))
                return Result.Failure(ErrorCode.InvalidOperation, "攻击阵营旗需要有效战争状态。");

            flag.CurrentHp = Math.Max(0, flag.CurrentHp - damage);
            if (flag.CurrentHp <= 0)
                return TryDestroy(world, flag.FlagId);
            return Result.Success();
        }

        /// <summary>角色按正式近战属性对阵营旗造成一击；政治／军事门槛仍由本服务校验。</summary>
        public static Result ApplyStrikeFromAttacker(
            SimulationWorld world,
            PlayerPartyRuntime party,
            string attackerFactionId,
            string flagId,
            EntityId attackerId,
            out int damageApplied)
        {
            damageApplied = 0;
            if (world == null || attackerId.IsNone || !world.Entities.TryGet(attackerId, out var attacker))
                return Result.Failure(ErrorCode.EntityNotFound, "攻击者不存在。");
            if (party == null || !party.IsMember(attackerId))
                return Result.Failure(ErrorCode.InvalidOperation, "攻击者不属于 PlayerParty。");
            damageApplied = ComputeAssaultDamage(attacker);
            return TryApplyAssault(world, party, attackerFactionId, flagId, damageApplied);
        }

        public static Result TryDestroy(SimulationWorld world, string flagId)
        {
            if (world?.Strategic == null ||
                !world.Strategic.FactionFlags.Flags.TryGetValue(flagId ?? string.Empty, out var flag) ||
                flag == null || !world.Strategic.FactionFlags.Remove(flagId))
                return Result.Failure(ErrorCode.NotFound, "阵营旗不存在。");
            if (!string.IsNullOrEmpty(flag.SiteId) &&
                world.Strategic.Sites.TryGet(flag.SiteId, out var site) && site != null &&
                string.Equals(site.CoreAssetId, flag.FlagId, StringComparison.Ordinal))
                site.IsCoreActive = false;
            // 不写攻击方、不硬编码无主：剩余 Control Assets 按 EstablishedOrder 自动重新求解。
            StrategicTerritoryCoverageResolver.Rebuild(world);
            return Result.Success();
        }

        public static bool TryRestoreRemovedCore(SimulationWorld world, FactionFlagState flag)
        {
            if (world?.Strategic == null || flag == null || !world.Strategic.FactionFlags.Register(flag))
                return false;
            if (!string.IsNullOrEmpty(flag.SiteId) &&
                world.Strategic.Sites.TryGet(flag.SiteId, out var site) && site != null &&
                string.Equals(site.CoreAssetId, flag.FlagId, StringComparison.Ordinal))
                site.IsCoreActive = true;
            StrategicTerritoryCoverageResolver.Rebuild(world);
            return true;
        }

        static bool RectanglesOverlap(
            string surfaceId, float x, float y, float width, float height, WorldSite other)
        {
            if (!string.Equals(surfaceId, other.CoreSurfaceId, StringComparison.Ordinal)) return false;
            return Math.Abs(x - other.CoreWorldX) * 2f < width + other.CoreRangeWidth &&
                   Math.Abs(y - other.CoreWorldY) * 2f < height + other.CoreRangeHeight;
        }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
