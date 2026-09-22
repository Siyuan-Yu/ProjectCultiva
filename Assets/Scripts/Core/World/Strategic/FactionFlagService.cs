using XianXia.Core.World;
using System;
using System.Collections.Generic;
using System.Globalization;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    public sealed class FactionFlagSitePlacementRequest
    {
        public string SurfaceId { get; set; } = string.Empty;
        public WorldVec2 WorldPosition { get; set; }
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

        /// <summary>只查询当前理论覆盖；不得作为实际行政管理权威。</summary>
        public static bool TryResolve(
            SimulationWorld world, string surfaceId, float worldX, float worldY, out WorldSite site)
        {
            site = null;
            if (world?.Strategic?.Sites == null) return false;
            foreach (var pair in world.Strategic.Sites.Sites)
            {
                var candidate = pair.Value;
                if (!Contains(candidate, surfaceId, worldX, worldY)) continue;
                if (site == null || string.CompareOrdinal(candidate.SiteId, site.SiteId) < 0)
                    site = candidate;
            }
            return site != null;
        }

    }

    /// <summary>阵营旗的政治生命周期。所有变更后只通过 Territory Resolver 重建派生控制。</summary>
    public static class FactionFlagService
    {
        /// <summary>V1 阵营旗建筑防御；伤害与近战建筑公式统一。</summary>
        public const int StructureDefense = 4;
        const float CoincidentCoreEpsilonCells = .01f;

        public static int ComputeAssaultDamage(Entity attacker)
            => StrategicBuildingDamage.Compute(attacker, StructureDefense);

        public static Result ValidateSiteCorePlacement(
            SimulationWorld world,
            string factionId,
            FactionFlagSitePlacementRequest request,
            int initialLevel)
        {
            if (world?.Strategic == null || request == null || string.IsNullOrWhiteSpace(factionId) ||
                string.IsNullOrWhiteSpace(request.SurfaceId) || !IsFinite(request.WorldPosition.X) ||
                !IsFinite(request.WorldPosition.Y) || initialLevel < 1)
                return Result.Failure(ErrorCode.InvalidArgument, "新据点核心放置参数无效。");
            ResolvedWorldSpatialRange resolved;
            try { resolved = world.Strategic.SpatialRules.ResolveLevel(world, initialLevel, request.SurfaceId); }
            catch (Exception ex) { return Result.Failure(ErrorCode.InvalidArgument, ex.Message); }
            if (world.SurfaceSpatial == null ||
                !world.SurfaceSpatial.TryGet(request.SurfaceId, out var surface) || surface == null ||
                !surface.ContainsWorldPosition(request.WorldPosition.X, request.WorldPosition.Y))
                return Result.Failure(ErrorCode.InvalidOperation, "当前位置不属于有效Continuous Surface。");

            foreach (var pair in world.Strategic.Sites.Sites)
            {
                var site = pair.Value;
                if (site == null || !site.IsCoreActive) continue;
                if (site.HasContinuousCore &&
                    string.Equals(site.CoreSurfaceId, request.SurfaceId, StringComparison.Ordinal))
                {
                    var dx = site.CoreWorldX - request.WorldPosition.X;
                    var dy = site.CoreWorldY - request.WorldPosition.Y;
                    var epsilon = surface.CellSize * CoincidentCoreEpsilonCells;
                    if (dx * dx + dy * dy <= epsilon * epsilon)
                        return Result.Failure(ErrorCode.InvalidOperation,
                            "此位置已存在另一个核心实体。");
                }
            }

            if (WorldSiteAdministrativeControlResolver.TryResolve(
                    world, request.SurfaceId, request.WorldPosition.X, request.WorldPosition.Y,
                    out var manager, out _) && manager != null &&
                !string.Equals(manager.OwnerFactionId, factionId, StringComparison.Ordinal))
                return Result.Failure(ErrorCode.InvalidOperation,
                    "敌对/其它势力实际控制范围内不能建立势力旗。");
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
        public static string NextRuntimeFlagId(SimulationWorld world, string factionId)
        {
            var owner = string.IsNullOrEmpty(factionId) ? "unknown" : factionId.Replace(':', '_');
            // Runtime identities use the owning faction plus the persisted entity sequence;
            // exact world position is spatial authority and is not encoded in the identity.
            var stem = "flag:runtime:" + owner + ":";
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
            out string siteId)
        {
            siteId = SiteIdForCoreFlag(flagId);
            if (world?.Strategic?.SpatialRules == null)
                return Result.Failure(ErrorCode.InvalidOperation, "Core control range catalog missing.");
            ResolvedWorldSpatialRange controlRange;
            try { controlRange = world.Strategic.SpatialRules.ResolveLevel(world, initialLevel, request?.SurfaceId); }
            catch (InvalidOperationException ex) { return Result.Failure(ErrorCode.InvalidArgument, ex.Message); }
            var baseline = TerritoryClaimService.EstablishBaselineFromActiveCores(world);
            if (baseline.IsFailure) return baseline;
            var valid = ValidateSiteCorePlacement(
                world, factionId, request, initialLevel);
            if (valid.IsFailure) return valid;
            if (string.IsNullOrEmpty(siteId) || initialLevel < 1)
                return Result.Failure(ErrorCode.InvalidArgument, "新据点身份或等级无效。");
            var site = new WorldSite
            {
                SiteId = siteId,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? "新建据点" : displayName.Trim(),
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
                CoreRangeWidth = controlRange.WidthWorld,
                CoreRangeHeight = controlRange.HeightWorld,
                IsCoreActive = true,
                CoreIsRemovable = true,
                LocalMapId = string.Empty
            };
            try { world.Strategic.Sites.Register(site); }
            catch (Exception ex)
            {
                return Result.Failure(ErrorCode.InvalidOperation, "新据点身份注册失败。", ex.Message);
            }
            var flag = new FactionFlagState
            {
                FlagId = flagId,
                FactionId = factionId,
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
            var claimed = TerritoryClaimService.CreateInitialClaim(world, site);
            if (claimed.IsFailure)
            {
                world.Strategic.FactionFlags.Remove(flagId);
                world.Strategic.Sites.RemoveRuntimeSite(siteId);
                siteId = string.Empty;
                return claimed;
            }
            var identityValid = FactionFlagSiteCoreQuery.TryResolveFlagForSite(
                world, site, out var resolvedFlag) && ReferenceEquals(resolvedFlag, flag);
            var invariant = identityValid
                ? TerritoryClaimService.ValidateActiveCoreCenters(world, ErrorCode.InvalidOperation)
                : Result.Failure(ErrorCode.InvalidOperation,
                    "新建势力旗的 Site/CoreAsset/Flag identity 不一致。");
            if (invariant.IsFailure)
            {
                var claimIds = new List<string>();
                foreach (var claim in world.Strategic.TerritoryClaims.EnumerateForSite(siteId))
                    if (claim != null) claimIds.Add(claim.ClaimId);
                for (var i = 0; i < claimIds.Count; i++)
                    TerritoryClaimService.RollbackClaim(world, claimIds[i]);
                world.Strategic.FactionFlags.Remove(flagId);
                world.Strategic.Sites.RemoveRuntimeSite(siteId);
                siteId = string.Empty;
                return Result.Failure(ErrorCode.InvalidOperation,
                    "新建势力旗未能取得自身核心中心的实际行政控制。", invariant.Error.Message);
            }
            world.Strategic.SitePublicStocks.GetOrCreate(site.SiteId);
            return Result.Success();
        }

        public static Result TryApplyAssault(
            SimulationWorld world, PlayerPartyRuntime party, string attackerFactionId, string flagId, int damage)
        {
            if (world?.Strategic == null || party == null || !party.HasActive || damage <= 0)
                return Result.Failure(ErrorCode.InvalidArgument, "阵营旗突击参数无效。");
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
            // 不删除 TerritoryClaim：核心失效后历史保留，但 resolver 会忽略该 Site。
            CharacterEncounterService.NotifyStrategicObjectiveResolved(world, flag.SiteId, flag.FlagId);
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
            return true;
        }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
