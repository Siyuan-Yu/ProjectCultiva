using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Data.Content;

namespace XianXia.Data.Bootstrap
{
    /// <summary>Playable ???Hex ???????</summary>
    public static class HexStrategicSessionBootstrap
    {
        public const string DefaultStartSiteId = "base:site_huangcun";

        public static Result ApplyOpening(
            SimulationWorld world,
            OpeningScenarioDefinition scenario,
            GameStartLookup lookup = null,
            IList<OpeningSpawnEntry> spawnEntries = null)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "HexStrategic session bootstrap args null.");

            world.WorldPresence.Clear();
            world.PartyWorld.ClearSiteFocus();
            world.PartyWorld.SiteId = string.Empty;
            world.PartyWorld.LocalMapId = string.Empty;

            var startSiteId = DefaultStartSiteId;
            if (!world.Strategic.Sites.TryGet(startSiteId, out var site) || site == null)
            {
                return Result.Failure(
                    ErrorCode.NotFound,
                    "Default start WorldSite missing.",
                    startSiteId);
            }

            var party = CollectPartyEntityIds(scenario, lookup, spawnEntries);
            for (var i = 0; i < party.Count; i++)
            {
                if (party[i].IsNone)
                    continue;
                world.WorldPresence.SetAtSite(party[i], startSiteId);
            }

            var enter = WorldTravelService.EnterWorldSiteScene(world, startSiteId, string.Empty);
            if (enter.IsFailure)
                return enter;

            WorldTravelService.SyncPartyFocus(world);
            return Result.Success();
        }

        static List<EntityId> CollectPartyEntityIds(
            OpeningScenarioDefinition scenario,
            GameStartLookup lookup,
            IList<OpeningSpawnEntry> spawnEntries)
        {
            var list = new List<EntityId>(8);
            if (lookup == null)
                return list;
            var entries = spawnEntries ?? scenario?.Spawns;
            if (entries == null)
                return list;
            for (var i = 0; i < entries.Count; i++)
            {
                var spawn = entries[i];
                if (spawn == null || string.IsNullOrWhiteSpace(spawn.DefinitionId))
                    continue;
                if (!string.IsNullOrEmpty(spawn.EntityKind) &&
                    !string.Equals(spawn.EntityKind, "character", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!lookup.TryGetEntity(spawn.DefinitionId, out var id) || id.IsNone)
                    continue;
                list.Add(id);
            }

            return list;
        }
    }
}
