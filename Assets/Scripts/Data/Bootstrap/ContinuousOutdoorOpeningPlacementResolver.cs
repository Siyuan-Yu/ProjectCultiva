using XianXia.Core.World;
using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.World.Hex;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;
using XianXia.Data.Content;

namespace XianXia.Data.Bootstrap
{
    // The checked-in Surface anchor is the only NewGame outdoor position authority.
    public enum OpeningPlacementAuthority { Unresolved, CheckedInSurfaceAnchor }
    public enum OpeningAnchorBakeValidationOutcome { Validated, Skipped, Failed }

    public sealed class OpeningPlacementPlanRow
    {
        public EntityId EntityId;
        public string DefinitionId = string.Empty;
        public string SpawnKey = string.Empty;
        public string SourcePlaceId = string.Empty;
        public OpeningPlacementAuthority Authority;
        public int SlotIndex;
        public WorldVec2 SourceLocalPosition;
        public WorldVec2 BakedWorldPosition;
        public bool HasBakedPosition;
        public bool HasCheckedInAnchor;
        public WorldVec2 CheckedInWorldPosition;
        public float AnchorDelta;
        public bool AnchorMatchesBake;
        public string FailureReason = string.Empty;
    }

    public static class ContinuousOutdoorOpeningPlacementResolver
    {
        public static string DescribeAuthority(OpeningPlacementAuthority authority) =>
            authority == OpeningPlacementAuthority.CheckedInSurfaceAnchor
                ? "CheckedInSurfaceAnchor" : "Unresolved";

        public static bool TryBuildPlan(
            SimulationWorld world, DefinitionRegistry registry, OutdoorWorldSurfaceDefinition surface,
            WorldSite site, IReadOnlyList<EntityId> population, List<OpeningPlacementPlanRow> into,
            out string failure)
        {
            failure = string.Empty;
            if (into == null || world == null || surface == null || site == null || population == null)
            {
                failure = "opening Surface anchor inputs missing";
                return false;
            }
            into.Clear();
            foreach (var id in population)
            {
                if (id.IsNone || !world.Entities.TryGet(id, out var entity) || entity == null)
                    continue;
                var row = new OpeningPlacementPlanRow { EntityId = id, DefinitionId = entity.DefinitionId.ToString() };
                if (world.OpeningSpawnIdentities.TryGetSpawnKey(id, out var key))
                {
                    row.SpawnKey = key;
                    if (TryGetCheckedInAnchorDefinition(surface, site.SiteId, key, out var anchor) &&
                        string.Equals(anchor.DefinitionId, row.DefinitionId, StringComparison.Ordinal))
                    {
                        row.SourcePlaceId = anchor.SourceLocationId ?? string.Empty;
                        row.CheckedInWorldPosition = new WorldVec2(anchor.WorldX, anchor.WorldY);
                        row.BakedWorldPosition = row.CheckedInWorldPosition;
                        row.HasCheckedInAnchor = true;
                        row.HasBakedPosition = true;
                        row.AnchorMatchesBake = true;
                        row.Authority = OpeningPlacementAuthority.CheckedInSurfaceAnchor;
                    }
                    else
                        row.FailureReason = "missing or mismatched checked-in Surface anchor: " + key;
                }
                into.Add(row);
            }
            return true;
        }

        public static bool TryValidateBakedAnchors(
            SimulationWorld world, DefinitionRegistry registry, OutdoorWorldSurfaceDefinition surface,
            WorldSite site, IReadOnlyList<EntityId> population, List<OpeningPlacementPlanRow> plan,
            out string failure) =>
            ValidateBakedAnchors(world, registry, surface, site, population, plan, out failure) !=
            OpeningAnchorBakeValidationOutcome.Failed;

        public static OpeningAnchorBakeValidationOutcome ValidateBakedAnchors(
            SimulationWorld world, DefinitionRegistry registry, OutdoorWorldSurfaceDefinition surface,
            WorldSite site, IReadOnlyList<EntityId> population, List<OpeningPlacementPlanRow> plan,
            out string failure)
        {
            failure = string.Empty;
            if (surface == null || site == null || !HasCheckedInAnchors(surface, site.SiteId))
            {
                failure = "no checked-in openingEntityAnchors for site " + (site?.SiteId ?? string.Empty);
                return OpeningAnchorBakeValidationOutcome.Skipped;
            }
            if (registry == null ||
                !registry.TryGetOutdoorSurfaceGeography(surface.SurfaceId, out var geography) ||
                geography?.Navigation == null || world == null || plan == null)
            {
                failure = "opening Surface geography or runtime inputs missing";
                return OpeningAnchorBakeValidationOutcome.Failed;
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var errors = new List<string>();
            foreach (var anchor in surface.OpeningEntityAnchors)
            {
                if (anchor == null || !string.Equals(anchor.SiteId, site.SiteId, StringComparison.Ordinal))
                    continue;
                if (string.IsNullOrWhiteSpace(anchor.SpawnKey) ||
                    string.IsNullOrWhiteSpace(anchor.DefinitionId) ||
                    !seen.Add(anchor.SpawnKey) ||
                    !geography.Navigation.IsWalkable(anchor.WorldX, anchor.WorldY))
                    errors.Add("invalid or duplicate Surface anchor " + (anchor.SpawnKey ?? string.Empty));
            }
            if (!TryBuildPlan(world, registry, surface, site, population, plan, out var planFailure))
                errors.Add(planFailure);
            else
                foreach (var row in plan)
                    if (!string.IsNullOrEmpty(row.SpawnKey) && !row.HasCheckedInAnchor)
                        errors.Add(row.FailureReason);
            if (errors.Count == 0)
                return OpeningAnchorBakeValidationOutcome.Validated;
            failure = string.Join(" | ", errors);
            return OpeningAnchorBakeValidationOutcome.Failed;
        }

        public static bool HasCheckedInAnchors(OutdoorWorldSurfaceDefinition surface, string siteId)
        {
            if (surface?.OpeningEntityAnchors == null || string.IsNullOrEmpty(siteId)) return false;
            foreach (var anchor in surface.OpeningEntityAnchors)
                if (anchor != null && string.Equals(anchor.SiteId, siteId, StringComparison.Ordinal)) return true;
            return false;
        }

        public static bool TryGetCheckedInAnchor(
            OutdoorWorldSurfaceDefinition surface, string siteId, string spawnKey, out WorldVec2 position)
        {
            position = default;
            if (!TryGetCheckedInAnchorDefinition(surface, siteId, spawnKey, out var anchor)) return false;
            position = new WorldVec2(anchor.WorldX, anchor.WorldY);
            return true;
        }

        static bool TryGetCheckedInAnchorDefinition(
            OutdoorWorldSurfaceDefinition surface, string siteId, string spawnKey,
            out WorldSiteOpeningEntityAnchorDefinition found)
        {
            found = null;
            if (surface?.OpeningEntityAnchors == null || string.IsNullOrEmpty(siteId) ||
                string.IsNullOrEmpty(spawnKey)) return false;
            foreach (var anchor in surface.OpeningEntityAnchors)
                if (anchor != null && string.Equals(anchor.SiteId, siteId, StringComparison.Ordinal) &&
                    string.Equals(anchor.SpawnKey, spawnKey, StringComparison.Ordinal))
                {
                    if (found != null) return false;
                    found = anchor;
                }
            return found != null;
        }
    }
}
