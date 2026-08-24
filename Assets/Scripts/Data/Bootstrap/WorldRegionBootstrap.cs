using System;
using System.Collections.Generic;
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
            GameStartLookup lookup,
            IList<OpeningSpawnEntry> spawnEntries = null)
        {
            if (world == null || registry == null || scenario == null)
                return Result.Failure(ErrorCode.InvalidArgument, "WorldRegion bootstrap args null.");

            if (!string.IsNullOrWhiteSpace(scenario.OpeningLocalPlaceSetId))
            {
                var place = ApplyLocalPlaceSetId(
                    world, registry, scenario.OpeningLocalPlaceSetId.Trim());
                if (place.IsFailure)
                    return place;
            }
            else if (!string.IsNullOrWhiteSpace(scenario.OpeningWorldRegionId))
            {
                var region = ApplyWorldRegionId(
                    world, registry, scenario.OpeningWorldRegionId.Trim());
                if (region.IsFailure)
                    return region;
            }
            else
            {
                return Result.Success();
            }

            return PlaceOpeningSpawns(world, scenario, lookup, spawnEntries);
        }

        /// <summary>换 WorldSite 后按 mapLayout 切换村内地点表（库存／任务保留）。</summary>
        public static Result ActivatePlacesForMapLayout(
            SimulationWorld world,
            DefinitionRegistry registry,
            string mapLayoutId)
        {
            if (world == null || registry == null)
                return Result.Failure(ErrorCode.InvalidArgument, "ActivatePlaces args null.");

            if (string.IsNullOrWhiteSpace(mapLayoutId))
            {
                world.WorldRegion.ClearLocations();
                world.WorldRegion.RegionId = string.Empty;
                world.WorldRegion.RegionName = string.Empty;
                world.WorldRegion.StartLocationId = string.Empty;
                return Result.Success();
            }

            foreach (var kv in registry.LocalPlaceSets)
            {
                var set = kv.Value;
                if (set == null)
                    continue;
                if (!string.Equals(set.MapLayoutId, mapLayoutId, StringComparison.Ordinal))
                    continue;
                return FillBoardFromPlaceSet(world, set);
            }

            // 无对应 place set：清空旧地点表（禁止荒村地点残留），不阻断切图
            world.WorldRegion.ClearLocations();
            world.WorldRegion.RegionId = string.Empty;
            world.WorldRegion.RegionName = string.Empty;
            world.WorldRegion.StartLocationId = string.Empty;
            return Result.Success();
        }

        static Result ApplyLocalPlaceSetId(
            SimulationWorld world,
            DefinitionRegistry registry,
            string idText)
        {
            var parsed = DefinitionId.Parse(idText);
            if (parsed.IsFailure)
                return Result.Failure(parsed.Error);
            if (!registry.TryGetLocalPlaceSet(parsed.Value, out var def))
            {
                return Result.Failure(
                    ErrorCode.NotFound,
                    "Opening localPlaceSet missing.",
                    idText);
            }

            return FillBoardFromPlaceSet(world, def);
        }

        static Result ApplyWorldRegionId(
            SimulationWorld world,
            DefinitionRegistry registry,
            string idText)
        {
            var parsed = DefinitionId.Parse(idText);
            if (parsed.IsFailure)
                return Result.Failure(parsed.Error);
            if (!registry.TryGetWorldRegion(parsed.Value, out var def))
            {
                return Result.Failure(
                    ErrorCode.NotFound,
                    "Opening world region missing.",
                    idText);
            }

            world.WorldRegion.ClearLocations();
            world.WorldRegion.RegionId = def.Id.ToString();
            world.WorldRegion.RegionName = def.Name ?? string.Empty;
            world.WorldRegion.StartLocationId = def.StartLocationId ?? string.Empty;
            RegisterEntries(world, def.Locations);

            if (string.IsNullOrEmpty(world.WorldRegion.StartLocationId) ||
                !world.WorldRegion.TryGet(world.WorldRegion.StartLocationId, out _))
            {
                return Result.Failure(
                    ErrorCode.NotFound,
                    "World region startLocationId invalid.",
                    world.WorldRegion.StartLocationId);
            }

            return Result.Success();
        }

        static Result FillBoardFromPlaceSet(SimulationWorld world, LocalPlaceSetDefinition def)
        {
            world.WorldRegion.ClearLocations();
            world.WorldRegion.RegionId = def.Id.ToString();
            world.WorldRegion.RegionName = def.Name ?? string.Empty;
            world.WorldRegion.StartLocationId = def.StartLocationId ?? string.Empty;
            RegisterEntries(world, def.Locations);

            if (string.IsNullOrEmpty(world.WorldRegion.StartLocationId) ||
                !world.WorldRegion.TryGet(world.WorldRegion.StartLocationId, out _))
            {
                return Result.Failure(
                    ErrorCode.NotFound,
                    "localPlaceSet startLocationId invalid.",
                    world.WorldRegion.StartLocationId);
            }

            return Result.Success();
        }

        static void RegisterEntries(SimulationWorld world, List<WorldLocationEntry> entries)
        {
            if (entries == null)
                return;
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id))
                    continue;
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
                    PresentationZ = entry.PresentationZ,
                    LocalMapId = entry.LocalMapId ?? string.Empty,
                    EnterLocalMapId = entry.EnterLocalMapId ?? string.Empty,
                    EnterSpawnLocationId = entry.EnterSpawnLocationId ?? string.Empty,
                    SurveySenseRequired = entry.SurveySenseRequired
                };
                if (entry.AdjacentIds != null)
                    loc.AdjacentIds.AddRange(entry.AdjacentIds);
                if (entry.EnterConditions != null)
                    loc.EnterConditions.AddRange(entry.EnterConditions);
                if (entry.QuestOfferIds != null)
                    loc.QuestOfferIds.AddRange(entry.QuestOfferIds);
                if (entry.Tags != null)
                    loc.Tags.AddRange(entry.Tags);
                if (entry.AllowedActivities != null)
                    loc.AllowedActivities.AddRange(entry.AllowedActivities);
                world.WorldRegion.Register(loc);
            }
        }

        static Result PlaceOpeningSpawns(
            SimulationWorld world,
            OpeningScenarioDefinition scenario,
            GameStartLookup lookup,
            IList<OpeningSpawnEntry> spawnEntries)
        {
            var entries = spawnEntries ?? scenario.Spawns;
            if (entries != null)
            {
                foreach (var spawn in entries)
                {
                    if (spawn == null || string.IsNullOrWhiteSpace(spawn.DefinitionId))
                        continue;
                    if (!lookup.TryGetEntity(spawn.DefinitionId, out var entityId))
                        continue;
                    if (!world.Entities.TryGet(entityId, out var entity))
                        continue;

                    var placeId = world.WorldRegion.StartLocationId;
                    if (!string.IsNullOrEmpty(spawn.EntityKind) &&
                        string.Equals(spawn.EntityKind, "npc", StringComparison.OrdinalIgnoreCase))
                    {
                        var resident = FindResidentLocation(world, spawn.DefinitionId);
                        if (!string.IsNullOrEmpty(resident))
                            placeId = resident;
                        else if (IsCaveBoundNpc(entity))
                            continue;
                    }

                    var placed = EnsureLocation(entity, placeId);
                    if (placed.IsFailure)
                        return placed;
                }
            }

            foreach (var kv in world.WorldRegion.Locations)
            {
                var residentDef = kv.Value.ResidentNpcDefinitionId;
                if (string.IsNullOrWhiteSpace(residentDef))
                    continue;
                if (!lookup.TryGetEntity(residentDef, out var residentId))
                    continue;
                if (!world.Entities.TryGet(residentId, out var resident))
                    continue;
                var pinned = EnsureLocation(resident, kv.Key);
                if (pinned.IsFailure)
                    return pinned;
            }

            return Result.Success();
        }

        static bool IsCaveBoundNpc(XianXia.Core.Entities.Entity entity)
        {
            if (entity == null)
                return false;
            if (!entity.TryGet<XianXia.Core.Social.PersonalityProfileComponent>(out var profile))
                return false;
            return profile.HasTag("cave");
        }

        static Result EnsureLocation(XianXia.Core.Entities.Entity entity, string placeId)
        {
            if (entity == null || string.IsNullOrWhiteSpace(placeId))
                return Result.Success();
            if (!entity.TryGet<EntityLocationComponent>(out var locComp))
            {
                locComp = new EntityLocationComponent();
                var added = entity.AddComponent(locComp);
                if (added.IsFailure)
                    return added;
            }

            locComp.LocationId = placeId;
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
