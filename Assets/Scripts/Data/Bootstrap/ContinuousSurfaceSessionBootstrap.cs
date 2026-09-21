using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;
using XianXia.Core.World.Hex;
using XianXia.Data.Content;

namespace XianXia.Data.Bootstrap
{
    /// <summary>NewGame world presence and party position from checked-in Surface anchors.</summary>
    public static class ContinuousSurfaceSessionBootstrap
    {
        public static Result ApplyOpening(
            SimulationWorld world, OpeningScenarioDefinition scenario, GameStartLookup lookup,
            IList<OpeningSpawnEntry> spawnEntries, DefinitionRegistry registry)
        {
            if (world == null || registry == null || scenario == null || lookup == null)
                return Result.Failure(ErrorCode.InvalidArgument, "Opening Surface session arguments missing.");
            world.Strategic.ContinuousManualCombat.Clear();
            world.Strategic.ManualBattleSettlement.Clear();
            world.PartyWorld.ClearSiteFocus();
            world.PartyWorld.SiteId = string.Empty;
            world.PartyWorld.LocalMapId = string.Empty;
            if (!world.Strategic.Sites.TryGet(PlayableDayBootstrap.DefaultStartSiteId, out var startSite))
                return Result.Failure(ErrorCode.NotFound, "Opening Surface start Site missing.");

            var presence = OpeningSpawnWorldPresenceApplier.Apply(world, registry, scenario, lookup, spawnEntries);
            if (presence.IsFailure) return presence;

            var entries = spawnEntries ?? scenario.Spawns;
            if (entries == null) return Result.Failure(ErrorCode.ContentLoadFailed, "Opening spawns missing.");
            foreach (var spawn in entries)
            {
                if (spawn == null || string.IsNullOrWhiteSpace(spawn.DefinitionId) ||
                    (!string.IsNullOrWhiteSpace(spawn.EntityKind) &&
                     !string.Equals(spawn.EntityKind, "character", StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(spawn.WorldSiteId) &&
                     !string.Equals(spawn.WorldSiteId, startSite.SiteId, StringComparison.Ordinal)))
                    continue;
                if (!lookup.TryGetEntity(spawn.DefinitionId, out var id) || id.IsNone)
                    continue;
                if (!world.WorldPresence.TryGet(id, out var opening) || opening == null ||
                    !opening.HasContinuousWorldPosition)
                    return Result.Failure(ErrorCode.ContentLoadFailed,
                        "Opening player has no checked-in Surface anchor.", spawn.DefinitionId);
                var point = opening.ContinuousWorldPosition;
                if (!world.SurfaceGround.TryResolveContaining(point, out var navigation) || navigation == null)
                    return Result.Failure(ErrorCode.ContentLoadFailed,
                        "Opening player Surface authority is unavailable.", spawn.DefinitionId);
                var hexSize = world.LegacyHexWorld != null && world.LegacyHexWorld.HexSize > 0f
                    ? world.LegacyHexWorld.HexSize
                    : 1f;
                world.PlayerPartyTravel.SetAtSurfacePosition(
                    navigation.SurfaceId, point, HexMath.WorldToHex(point.X, point.Y, hexSize));
                world.PlayerPartyTravel.SetCurrentOutdoorWorldSiteContext(startSite.SiteId);
                world.PlayerPartyTravel.CaptureTravelingMembers(new[] { id });
                world.PartyWorld.Mode = PartyWorldPresenceMode.AtWorldPosition;
                return Result.Success();
            }
            return Result.Failure(ErrorCode.ContentLoadFailed, "Opening player character missing.");
        }
    }
}
