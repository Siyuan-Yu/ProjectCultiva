using System;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Exploration;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Data.Content;

namespace XianXia.Data.Bootstrap
{
    public static class WorldRegionBootstrap
    {
        public static Result ApplyOpening(
            SimulationWorld world,
            DefinitionRegistry registry,
            OpeningScenarioDefinition scenario,
            GameStartLookup lookup)
        {
            if (world == null || registry == null || scenario == null)
                return Result.Failure(ErrorCode.InvalidArgument, "WorldRegion bootstrap args null.");

            if (string.IsNullOrWhiteSpace(scenario.OpeningWorldRegionId))
                return Result.Success();

            var parsed = DefinitionId.Parse(scenario.OpeningWorldRegionId);
            if (parsed.IsFailure)
                return Result.Failure(parsed.Error);
            if (!registry.TryGetWorldRegion(parsed.Value, out var def))
            {
                return Result.Failure(
                    ErrorCode.NotFound,
                    "Opening world region missing.",
                    scenario.OpeningWorldRegionId);
            }

            world.WorldRegion.RegionId = def.Id.ToString();
            world.WorldRegion.RegionName = def.Name ?? string.Empty;
            world.WorldRegion.StartLocationId = def.StartLocationId ?? string.Empty;

            foreach (var entry in def.Locations)
            {
                if (!Enum.TryParse(entry.Kind ?? "Wild", true, out LocationKind kind))
                    kind = LocationKind.Wild;

                var loc = new WorldLocationState
                {
                    Id = entry.Id,
                    Name = entry.Name ?? entry.Id,
                    Kind = kind,
                    ResourceOnExploreId = entry.ResourceOnExploreId ?? string.Empty,
                    ResourceOnExploreAmount = entry.ResourceOnExploreAmount,
                    OpportunitySiteId = entry.OpportunitySiteId ?? string.Empty,
                    ResidentNpcDefinitionId = entry.ResidentNpcDefinitionId ?? string.Empty,
                    PresentationX = entry.PresentationX,
                    PresentationZ = entry.PresentationZ
                };
                if (entry.AdjacentIds != null)
                    loc.AdjacentIds.AddRange(entry.AdjacentIds);
                world.WorldRegion.Register(loc);
            }

            if (string.IsNullOrEmpty(world.WorldRegion.StartLocationId) ||
                !world.WorldRegion.TryGet(world.WorldRegion.StartLocationId, out _))
            {
                return Result.Failure(
                    ErrorCode.NotFound,
                    "World region startLocationId invalid.",
                    world.WorldRegion.StartLocationId);
            }

            foreach (var spawn in scenario.Spawns)
            {
                if (!lookup.TryGetEntity(spawn.DefinitionId, out var entityId))
                    continue;
                if (!world.Entities.TryGet(entityId, out var entity))
                    continue;

                var placeId = world.WorldRegion.StartLocationId;
                if (!string.IsNullOrEmpty(spawn.EntityKind) &&
                    string.Equals(spawn.EntityKind, "npc", StringComparison.OrdinalIgnoreCase))
                {
                    placeId = FindResidentLocation(world, spawn.DefinitionId) ?? placeId;
                }

                if (!entity.TryGet<EntityLocationComponent>(out var locComp))
                {
                    locComp = new EntityLocationComponent();
                    var added = entity.AddComponent(locComp);
                    if (added.IsFailure)
                        return added;
                }

                locComp.LocationId = placeId;
            }

            return Result.Success();
        }

        static string FindResidentLocation(SimulationWorld world, string npcDefinitionId)
        {
            foreach (var kv in world.WorldRegion.Locations)
            {
                if (string.Equals(kv.Value.ResidentNpcDefinitionId, npcDefinitionId, StringComparison.Ordinal))
                    return kv.Key;
            }

            return null;
        }
    }
}
