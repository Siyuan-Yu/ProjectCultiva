using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Data.Content;

namespace XianXia.Data.Bootstrap
{
    public struct ContinuousOpeningPresenceCensus
    {
        public int AnchorCount;
        public int SpawnedEntityCount;
        public int PresenceCount;
        public int MissingPresenceCount;
        public int WrongSiteCount;
        public int MissingExactPositionCount;
    }

    /// <summary>Shared NewGame authority for an opening spawn's Site, position and logical location.</summary>
    public static class ContinuousOpeningSpawnPresenceResolver
    {
        /// <summary>Independent opening expected set, sourced from Surface anchors rather than WorldPresence.</summary>
        public static Result ValidateOpening(
            SimulationWorld world, OutdoorWorldSurfaceDefinition surface,
            IList<OpeningSpawnEntry> spawnEntries,
            out ContinuousOpeningPresenceCensus census)
        {
            census = new ContinuousOpeningPresenceCensus();
            if (world == null || surface?.OpeningEntityAnchors == null || spawnEntries == null)
                return Result.Failure(ErrorCode.InvalidArgument, "Opening Surface census inputs missing.");
            census.SpawnedEntityCount = world.OpeningSpawnIdentities.Count;
            var firstFailure = string.Empty;
            var ordinals = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var spawn in spawnEntries)
            {
                if (spawn == null || string.IsNullOrWhiteSpace(spawn.DefinitionId)) continue;
                var definitionId = spawn.DefinitionId.Trim();
                ordinals.TryGetValue(definitionId, out var ordinal);
                ordinals[definitionId] = ordinal + 1;
                var spawnKey = OpeningSpawnIdentityBoard.BuildStableKey(definitionId, ordinal);
                census.AnchorCount++;
                ContinuousOutdoorOpeningAnchorResolver.TryFindOpeningEntityAnchor(
                    surface, spawnKey, definitionId, out var anchor, out _);
                var entityId = EntityId.None;
                Entity entity = null;
                var key = string.Empty;
                if (anchor == null ||
                    !world.OpeningSpawnIdentities.TryGetEntity(anchor.SpawnKey, out entityId) ||
                    !world.Entities.TryGet(entityId, out entity) || entity == null ||
                    !string.Equals(entity.DefinitionId.ToString(), anchor.DefinitionId, StringComparison.Ordinal) ||
                    !world.OpeningSpawnIdentities.TryGetSpawnKey(entityId, out key) ||
                    !string.Equals(key, anchor.SpawnKey, StringComparison.Ordinal))
                {
                    census.MissingPresenceCount++;
                    if (firstFailure.Length == 0)
                        firstFailure = "Opening anchor has no matching spawned entity/identity: " +
                                       spawnKey + " entity=" + entityId + " actual=" +
                                       entity?.DefinitionId.ToString() + " key=" + key +
                                       " identities=" + world.OpeningSpawnIdentities.Count;
                    continue;
                }
                if (!world.WorldPresence.TryGet(entityId, out var presence) || presence == null)
                {
                    census.MissingPresenceCount++;
                    if (firstFailure.Length == 0)
                        firstFailure = "Opening WorldPresence missing: " + anchor.SpawnKey;
                    continue;
                }
                census.PresenceCount++;
                if (presence.Mode != PartyWorldPresenceMode.AtSite ||
                    !string.Equals(presence.SiteId, anchor.SiteId, StringComparison.Ordinal))
                {
                    census.WrongSiteCount++;
                    if (firstFailure.Length == 0)
                        firstFailure = "Opening WorldPresence Site mismatch: " + anchor.SpawnKey;
                }
                if (!presence.HasContinuousWorldPosition ||
                    !string.Equals(presence.PersonalSurfaceId, surface.SurfaceId, StringComparison.Ordinal) ||
                    Math.Abs(presence.WorldPosX - anchor.WorldX) > 0.0001f ||
                    Math.Abs(presence.WorldPosY - anchor.WorldY) > 0.0001f)
                {
                    census.MissingExactPositionCount++;
                    if (firstFailure.Length == 0)
                        firstFailure = "Opening exact Surface position missing/mismatched: " + anchor.SpawnKey;
                }
                if (!string.IsNullOrWhiteSpace(anchor.SourceLocationId) &&
                    (!entity.TryGet<EntityLocationComponent>(out var location) || location == null ||
                     !string.Equals(location.LocationId, anchor.SourceLocationId, StringComparison.Ordinal)))
                {
                    if (firstFailure.Length == 0)
                        firstFailure = "Opening logical SourceLocationId missing/mismatched: " + anchor.SpawnKey;
                }
            }
            return firstFailure.Length == 0
                ? Result.Success()
                : Result.Failure(ErrorCode.ContentLoadFailed, firstFailure);
        }

        public static bool TryApply(
            SimulationWorld world, OutdoorWorldSurfaceDefinition surface, EntityId entityId,
            string definitionId, string explicitSiteId, out string failure)
        {
            failure = string.Empty;
            if (world == null || surface == null || entityId.IsNone ||
                !world.Entities.TryGet(entityId, out var entity) || entity == null ||
                !string.Equals(entity.DefinitionId.ToString(), definitionId, StringComparison.Ordinal) ||
                !world.OpeningSpawnIdentities.TryGetSpawnKey(entityId, out var spawnKey))
            {
                failure = "Missing opening entity or SpawnKey: " + definitionId;
                return false;
            }
            if (!ContinuousOutdoorOpeningAnchorResolver.TryFindOpeningEntityAnchor(
                    surface, spawnKey, definitionId, out var anchor, out var lookupFailure))
            {
                failure = lookupFailure + " opening Surface anchor: " + spawnKey;
                return false;
            }
            if (!string.IsNullOrWhiteSpace(explicitSiteId) &&
                !string.Equals(anchor.SiteId, explicitSiteId.Trim(), StringComparison.Ordinal))
            {
                failure = "Opening Surface anchor Site mismatch: " + spawnKey + " expected=" +
                          explicitSiteId + " actual=" + anchor.SiteId;
                return false;
            }
            if (!world.Strategic.Sites.TryGet(anchor.SiteId, out var site) || site == null)
            {
                failure = "Opening Surface anchor Site missing: " + spawnKey + " site=" + anchor.SiteId;
                return false;
            }

            world.WorldPresence.SetAtSiteWithAnchor(
                entityId, anchor.SiteId, new WorldVec2(anchor.WorldX, anchor.WorldY), surface.SurfaceId);
            if (!string.IsNullOrWhiteSpace(anchor.SourceLocationId))
            {
                if (!entity.TryGet<EntityLocationComponent>(out var location) || location == null)
                {
                    location = new EntityLocationComponent();
                    entity.AddComponent(location);
                }
                location.LocationId = anchor.SourceLocationId;
                location.ClearPresentationOverride();
            }
            return true;
        }
    }
}
