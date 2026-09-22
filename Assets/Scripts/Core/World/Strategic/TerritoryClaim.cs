using System;
using System.Collections.Generic;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// 一次不可改写的 Site 行政范围取得记录。Claim 记录取得历史，不保存政治 Owner；
    /// 当前 Owner 始终来自其 WorldSite.OwnerFactionId。
    /// </summary>
    public sealed class TerritoryClaimState
    {
        public string ClaimId { get; set; } = string.Empty;
        public string SiteId { get; set; } = string.Empty;
        public string SurfaceId { get; set; } = string.Empty;
        public long AcquiredOrder { get; set; }
        public float CenterX { get; set; }
        public float CenterY { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }

        public bool Contains(float x, float y) =>
            x >= CenterX - Width * .5f && x <= CenterX + Width * .5f &&
            y >= CenterY - Height * .5f && y <= CenterY + Height * .5f;
    }

    /// <summary>Claim identity/order authority. Mutation is restricted to TerritoryClaimService.</summary>
    public sealed class TerritoryClaimBoard
    {
        readonly Dictionary<string, TerritoryClaimState> _byId =
            new Dictionary<string, TerritoryClaimState>(StringComparer.Ordinal);
        readonly List<TerritoryClaimState> _ordered = new List<TerritoryClaimState>();
        IReadOnlyList<TerritoryClaimState> _view;

        /// <summary>False only before new-game bootstrap or authoritative snapshot restore establishes authority.</summary>
        public bool HasAuthority { get; internal set; }
        public IReadOnlyList<TerritoryClaimState> Claims =>
            _view ?? (_view = _ordered.AsReadOnly());

        public bool TryGet(string claimId, out TerritoryClaimState claim) =>
            _byId.TryGetValue(claimId ?? string.Empty, out claim);

        public IEnumerable<TerritoryClaimState> EnumerateForSite(string siteId)
        {
            for (var i = 0; i < _ordered.Count; i++)
                if (string.Equals(_ordered[i].SiteId, siteId ?? string.Empty, StringComparison.Ordinal))
                    yield return _ordered[i];
        }

        internal bool Add(TerritoryClaimState claim)
        {
            if (claim == null || string.IsNullOrWhiteSpace(claim.ClaimId) ||
                _byId.ContainsKey(claim.ClaimId)) return false;
            for (var i = 0; i < _ordered.Count; i++)
                if (_ordered[i].AcquiredOrder == claim.AcquiredOrder) return false;
            _byId.Add(claim.ClaimId, claim);
            _ordered.Add(claim);
            _ordered.Sort(Compare);
            return true;
        }

        internal bool Remove(string claimId)
        {
            if (!_byId.TryGetValue(claimId ?? string.Empty, out var claim)) return false;
            _byId.Remove(claimId);
            _ordered.Remove(claim);
            return true;
        }

        internal void ReplaceAll(IEnumerable<TerritoryClaimState> claims, bool hasAuthority)
        {
            _byId.Clear();
            _ordered.Clear();
            if (claims != null)
                foreach (var claim in claims)
                {
                    _byId.Add(claim.ClaimId, claim);
                    _ordered.Add(claim);
                }
            _ordered.Sort(Compare);
            HasAuthority = hasAuthority;
        }

        static int Compare(TerritoryClaimState a, TerritoryClaimState b)
        {
            var order = a.AcquiredOrder.CompareTo(b.AcquiredOrder);
            if (order != 0) return order;
            var claim = string.CompareOrdinal(a.ClaimId, b.ClaimId);
            return claim != 0 ? claim : string.CompareOrdinal(a.SiteId, b.SiteId);
        }
    }

    public static class TerritoryClaimService
    {
        /// <summary>Static world shell replacement only; ordinary queries/lifecycle never call this.</summary>
        public static void ResetForContentBootstrap(SimulationWorld world)
        {
            world?.Strategic?.TerritoryClaims?.ReplaceAll(null, false);
        }

        public static long NextAcquiredOrder(SimulationWorld world)
        {
            long max = 0;
            var claims = world?.Strategic?.TerritoryClaims?.Claims;
            if (claims != null)
                for (var i = 0; i < claims.Count; i++)
                    if (claims[i].AcquiredOrder > max) max = claims[i].AcquiredOrder;
            return max == long.MaxValue ? long.MaxValue : max + 1;
        }

        public static Result CreateInitialClaim(SimulationWorld world, WorldSite site)
            => CreateClaim(world, site, "initial", NextAcquiredOrder(world),
                site?.CoreRangeWidth ?? 0f, site?.CoreRangeHeight ?? 0f);

        public static Result CreateExpansionClaim(
            SimulationWorld world, WorldSite site, float width, float height)
            => CreateClaim(world, site, "expansion", NextAcquiredOrder(world), width, height);

        public static Result EstablishBaselineFromActiveCores(SimulationWorld world)
        {
            if (world?.Strategic?.TerritoryClaims == null)
                return Result.Failure(ErrorCode.InvalidArgument, "Territory claim world missing.");
            if (world.Strategic.TerritoryClaims.HasAuthority)
                return Result.Success();

            var candidates = new List<WorldSite>();
            foreach (var pair in world.Strategic.Sites.Sites)
            {
                var site = pair.Value;
                if (site == null || !site.HasContinuousCore) continue;
                world.Strategic.SpatialRules?.Bind(world, site);
                candidates.Add(site);
            }
            candidates.Sort((a, b) =>
            {
                var order = a.ControlEstablishedOrder.CompareTo(b.ControlEstablishedOrder);
                return order != 0 ? order : string.CompareOrdinal(a.SiteId, b.SiteId);
            });

            var claims = new List<TerritoryClaimState>(candidates.Count);
            var usedOrders = new HashSet<long>();
            long next = 1;
            for (var i = 0; i < candidates.Count; i++)
            {
                var site = candidates[i];
                var order = site.ControlEstablishedOrder > 0 && usedOrders.Add(site.ControlEstablishedOrder)
                    ? site.ControlEstablishedOrder
                    : NextUnused(usedOrders, ref next);
                usedOrders.Add(order);
                next = Math.Max(next, order == long.MaxValue ? long.MaxValue : order + 1);
                claims.Add(Build(site, "claim:baseline:" + site.SiteId, order,
                    site.CoreRangeWidth, site.CoreRangeHeight));
            }
            world.Strategic.TerritoryClaims.ReplaceAll(claims, true);
            return ValidateActiveCoreCenters(world, ErrorCode.InvalidOperation);
        }

        public static Result RestoreAuthoritative(
            SimulationWorld world, IReadOnlyList<TerritoryClaimState> source)
        {
            if (world?.Strategic?.TerritoryClaims == null)
                return Result.Failure(ErrorCode.InvalidArgument, "Territory claim restore world missing.");
            var claims = new List<TerritoryClaimState>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var orders = new HashSet<long>();
            var lastBySite = new Dictionary<string, TerritoryClaimState>(StringComparer.Ordinal);
            if (source != null)
                for (var i = 0; i < source.Count; i++)
                {
                    var claim = source[i];
                    if (!IsValid(claim) || !ids.Add(claim.ClaimId) || !orders.Add(claim.AcquiredOrder) ||
                        !world.Strategic.Sites.TryGet(claim.SiteId, out var site) || site == null ||
                        !site.HasContinuousCore ||
                        !string.Equals(claim.SurfaceId, site.CoreSurfaceId, StringComparison.Ordinal) ||
                        Math.Abs(claim.CenterX - site.CoreWorldX) > .001f ||
                        Math.Abs(claim.CenterY - site.CoreWorldY) > .001f)
                        return Result.Failure(ErrorCode.SnapshotInvalid,
                            "Invalid territory claim snapshot entry.", "Index=" + i);
                    var copy = Copy(claim);
                    claims.Add(copy);
                }
            claims.Sort((a, b) => a.AcquiredOrder.CompareTo(b.AcquiredOrder));
            // Validate per-Site history in acquisition order, not serializer array order.
            lastBySite.Clear();
            for (var i = 0; i < claims.Count; i++)
            {
                var claim = claims[i];
                if (lastBySite.TryGetValue(claim.SiteId, out var previous) &&
                    (claim.Width + .001f < previous.Width || claim.Height + .001f < previous.Height))
                    return Result.Failure(ErrorCode.SnapshotInvalid,
                        "Territory claim history is not monotonic.", claim.SiteId);
                lastBySite[claim.SiteId] = claim;
            }
            foreach (var pair in world.Strategic.Sites.Sites)
            {
                var site = pair.Value;
                if (site != null && site.HasContinuousCore &&
                    !lastBySite.ContainsKey(site.SiteId))
                    return Result.Failure(ErrorCode.SnapshotInvalid,
                        "Continuous Site is missing territory claim history.", site.SiteId);
                if (site != null && lastBySite.TryGetValue(site.SiteId, out var latest) &&
                    (Math.Abs(latest.Width - site.CoreRangeWidth) > .001f ||
                     Math.Abs(latest.Height - site.CoreRangeHeight) > .001f))
                    return Result.Failure(ErrorCode.SnapshotInvalid,
                        "Latest territory claim does not match current core range.", site.SiteId);
            }
            world.Strategic.TerritoryClaims.ReplaceAll(claims, true);
            return ValidateActiveCoreCenters(world, ErrorCode.SnapshotInvalid);
        }

        /// <summary>
        /// Active Core center invariant: a center must have an actual manager, and that manager
        /// must share the Core owner's faction. Claim history may keep an older same-faction Site
        /// as the unique manager without invalidating a newly placed Site.
        /// </summary>
        public static Result ValidateActiveCoreCenters(SimulationWorld world, ErrorCode errorCode)
        {
            if (world?.Strategic?.Sites == null)
                return Result.Failure(errorCode, "Territory core-center invariant world missing.");
            foreach (var pair in world.Strategic.Sites.Sites)
            {
                var site = pair.Value;
                if (site == null || !site.IsCoreActive || !site.HasContinuousCore) continue;
                if (!WorldSiteAdministrativeControlResolver.TryResolve(
                        world, site.CoreSurfaceId, site.CoreWorldX, site.CoreWorldY,
                        out var resolved, out var claim) || resolved == null ||
                    !string.Equals(resolved.OwnerFactionId, site.OwnerFactionId, StringComparison.Ordinal))
                    return Result.Failure(errorCode,
                        "Active Site core center has no same-faction actual manager.",
                        "Site=" + site.SiteId + " Winner=" + (resolved?.SiteId ?? "none") +
                        " SiteFaction=" + (site.OwnerFactionId ?? "none") +
                        " WinnerFaction=" + (resolved?.OwnerFactionId ?? "none") +
                        " Claim=" + (claim?.ClaimId ?? "none"));
            }
            return Result.Success();
        }

        static Result CreateClaim(
            SimulationWorld world, WorldSite site, string kind, long order, float width, float height)
        {
            if (world?.Strategic?.TerritoryClaims == null || site == null ||
                !site.HasContinuousCore || order <= 0 || width <= 0f || height <= 0f)
                return Result.Failure(ErrorCode.InvalidArgument, "Territory claim parameters invalid.");
            var claim = Build(site,
                "claim:" + kind + ":" + site.SiteId + ":" + order, order, width, height);
            if (!world.Strategic.TerritoryClaims.Add(claim))
                return Result.Failure(ErrorCode.InvalidOperation, "Territory claim identity/order duplicate.");
            world.Strategic.TerritoryClaims.HasAuthority = true;
            return Result.Success();
        }

        internal static bool RollbackClaim(SimulationWorld world, string claimId) =>
            world?.Strategic?.TerritoryClaims != null &&
            world.Strategic.TerritoryClaims.Remove(claimId);

        static TerritoryClaimState Build(
            WorldSite site, string claimId, long order, float width, float height) =>
            new TerritoryClaimState
            {
                ClaimId = claimId, SiteId = site.SiteId, SurfaceId = site.CoreSurfaceId,
                AcquiredOrder = order, CenterX = site.CoreWorldX, CenterY = site.CoreWorldY,
                Width = width, Height = height
            };

        static TerritoryClaimState Copy(TerritoryClaimState claim) => new TerritoryClaimState
        {
            ClaimId = claim.ClaimId, SiteId = claim.SiteId, SurfaceId = claim.SurfaceId,
            AcquiredOrder = claim.AcquiredOrder, CenterX = claim.CenterX, CenterY = claim.CenterY,
            Width = claim.Width, Height = claim.Height
        };

        static bool IsValid(TerritoryClaimState claim) => claim != null &&
            !string.IsNullOrWhiteSpace(claim.ClaimId) && !string.IsNullOrWhiteSpace(claim.SiteId) &&
            !string.IsNullOrWhiteSpace(claim.SurfaceId) && claim.AcquiredOrder > 0 &&
            IsFinite(claim.CenterX) && IsFinite(claim.CenterY) &&
            IsFinite(claim.Width) && IsFinite(claim.Height) && claim.Width > 0f && claim.Height > 0f;

        static long NextUnused(HashSet<long> used, ref long next)
        {
            while (used.Contains(next) && next < long.MaxValue) next++;
            return next;
        }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>Unique exact-position administrative authority derived from immutable claim history.</summary>
    public static class WorldSiteAdministrativeControlResolver
    {
        public static bool TryResolve(
            SimulationWorld world, string surfaceId, float x, float y,
            out WorldSite site, out TerritoryClaimState winningClaim)
        {
            site = null;
            winningClaim = null;
            if (world?.Strategic?.TerritoryClaims == null ||
                string.IsNullOrWhiteSpace(surfaceId)) return false;
            var claims = world.Strategic.TerritoryClaims.Claims;
            for (var i = 0; i < claims.Count; i++)
            {
                var claim = claims[i];
                if (!string.Equals(claim.SurfaceId, surfaceId, StringComparison.Ordinal) ||
                    !claim.Contains(x, y) ||
                    !world.Strategic.Sites.TryGet(claim.SiteId, out var candidate) || candidate == null ||
                    !WorldSiteCoreCoverageResolver.Contains(candidate, surfaceId, x, y)) continue;
                if (winningClaim == null || Compare(claim, winningClaim) < 0)
                {
                    site = candidate;
                    winningClaim = claim;
                }
            }
            return site != null;
        }

        /// <summary>Compatibility for Core callers that own a canonical point but not a cached Surface id.</summary>
        public static bool TryResolveOnRegisteredSurface(
            SimulationWorld world, float x, float y,
            out string surfaceId, out WorldSite site, out TerritoryClaimState winningClaim)
        {
            surfaceId = string.Empty;
            site = null;
            winningClaim = null;
            if (world?.SurfaceSpatial?.Registered == null) return false;
            foreach (var pair in world.SurfaceSpatial.Registered)
            {
                if (pair.Value == null || !pair.Value.ContainsWorldPosition(x, y) ||
                    !TryResolve(world, pair.Key, x, y, out var candidate, out var claim)) continue;
                if (winningClaim == null || Compare(claim, winningClaim) < 0)
                {
                    surfaceId = pair.Key;
                    site = candidate;
                    winningClaim = claim;
                }
            }
            return site != null;
        }

        static int Compare(TerritoryClaimState a, TerritoryClaimState b)
        {
            var order = a.AcquiredOrder.CompareTo(b.AcquiredOrder);
            if (order != 0) return order;
            var claim = string.CompareOrdinal(a.ClaimId, b.ClaimId);
            return claim != 0 ? claim : string.CompareOrdinal(a.SiteId, b.SiteId);
        }
    }

    public static class WorldSiteCoreLevelService
    {
        public static Result TryUpgrade(SimulationWorld world, string siteId, int targetLevel)
        {
            if (world?.Strategic?.SpatialRules == null ||
                !world.Strategic.Sites.TryGet(siteId ?? string.Empty, out var site) || site == null ||
                !site.IsCoreActive || !site.HasContinuousCore)
                return Result.Failure(ErrorCode.NotFound, "Active WorldSite core not found.", siteId);
            if (targetLevel <= site.CoreLevel)
                return Result.Failure(ErrorCode.InvalidArgument, "Target core level must be higher.");
            ResolvedWorldSpatialRange target;
            try { target = world.Strategic.SpatialRules.ResolveLevel(world, targetLevel, site.CoreSurfaceId); }
            catch (InvalidOperationException ex) { return Result.Failure(ErrorCode.InvalidArgument, ex.Message); }
            if (target.WidthWorld + .001f < site.CoreRangeWidth ||
                target.HeightWorld + .001f < site.CoreRangeHeight)
                return Result.Failure(ErrorCode.InvalidOperation, "Core upgrade range cannot shrink.");

            var claim = TerritoryClaimService.CreateExpansionClaim(
                world, site, target.WidthWorld, target.HeightWorld);
            if (claim.IsFailure) return claim;
            var claimId = "claim:expansion:" + site.SiteId + ":" +
                          TerritoryClaimService.NextAcquiredOrder(world).ToString();
            // The created claim is the current final entry; retain its exact identity for rollback.
            var claims = world.Strategic.TerritoryClaims.Claims;
            if (claims.Count > 0) claimId = claims[claims.Count - 1].ClaimId;
            var oldLevel = site.CoreLevel;
            var oldWidth = site.CoreRangeWidth;
            var oldHeight = site.CoreRangeHeight;
            try
            {
                site.CoreLevel = targetLevel;
                world.Strategic.SpatialRules.Bind(world, site);
                return Result.Success();
            }
            catch (Exception ex)
            {
                site.CoreLevel = oldLevel;
                site.CoreRangeWidth = oldWidth;
                site.CoreRangeHeight = oldHeight;
                TerritoryClaimService.RollbackClaim(world, claimId);
                return Result.Failure(ErrorCode.InvalidOperation, "Core upgrade rolled back.", ex.Message);
            }
        }
    }
}
