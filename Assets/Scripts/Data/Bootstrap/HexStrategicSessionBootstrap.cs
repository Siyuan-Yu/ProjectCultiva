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

            if (world.Strategic.Sites.TryResolveSitePresenceHex(startSiteId, out var presenceHex))
            {
                var hexSize = world.HexWorld != null && world.HexWorld.HexSize > 0f
                    ? world.HexWorld.HexSize
                    : 1f;
                // Phase 2C：开局必须是 AtWorldSite，禁止 SetIdleAt（会写成 AtWorldPosition）。
                world.PlayerPartyTravel.SetAtWorldSite(startSiteId, presenceHex, hexSize);
                world.PlayerPartyTravel.CaptureTravelingMembers(party);
            }

            var enter = WorldTravelService.EnterWorldSiteScene(world, startSiteId, string.Empty);
            if (enter.IsFailure)
                return enter;

            WorldTravelService.SyncPartyFocus(world);
            // 确保 Travel 与 PartyWorld 同为 AtWorldSite（EnterWorldSiteScene 不改 Travel）。
            if (world.Strategic.Sites.TryResolveSitePresenceHex(startSiteId, out var presenceAgain))
            {
                var hexSize = world.HexWorld != null && world.HexWorld.HexSize > 0f
                    ? world.HexWorld.HexSize
                    : 1f;
                world.PlayerPartyTravel.SetAtWorldSite(startSiteId, presenceAgain, hexSize);
                world.PlayerPartyTravel.CaptureTravelingMembers(party);
            }

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
