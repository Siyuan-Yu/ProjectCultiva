using System;
using System.Collections.Generic;
using XianXia.Core.Exploration;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.Settlement;
using XianXia.Data.Content;

namespace XianXia.Data.Bootstrap
{
    /// <summary>Activates local locations only for independent Interior, Cave, and encounter maps.</summary>
    public static class InteriorLocalPlaceBootstrap
    {
        public static Result ActivatePlacesForMapLayout(
            SimulationWorld world,
            DefinitionRegistry registry,
            string mapLayoutId)
        {
            if (world == null || registry == null)
                return Result.Failure(ErrorCode.InvalidArgument, "ActivatePlaces args null.");

            if (string.IsNullOrWhiteSpace(mapLayoutId))
            {
                world.LocalPlaces.ClearLocations();
                world.LocalPlaces.RegionId = string.Empty;
                world.LocalPlaces.RegionName = string.Empty;
                world.LocalPlaces.StartLocationId = string.Empty;
                return Result.Success();
            }

            foreach (var kv in registry.LocalPlaceSets)
            {
                var set = kv.Value;
                if (set == null)
                    continue;
                if (!string.Equals(set.MapLayoutId, mapLayoutId, StringComparison.Ordinal))
                    continue;
                var applied = FillBoardFromPlaceSet(world, set);
                if (applied.IsSuccess)
                    SettlementAuthoritySync.Rebuild(world);
                return applied;
            }

            // 无对应 place set：清空旧地点表（禁止荒村地点残留），不阻断切图
            world.LocalPlaces.ClearLocations();
            world.LocalPlaces.RegionId = string.Empty;
            world.LocalPlaces.RegionName = string.Empty;
            world.LocalPlaces.StartLocationId = string.Empty;
            return Result.Success();
        }

        static Result FillBoardFromPlaceSet(SimulationWorld world, LocalPlaceSetDefinition def)
        {
            world.LocalPlaces.ClearLocations();
            world.LocalPlaces.RegionId = def.Id.ToString();
            world.LocalPlaces.RegionName = def.Name ?? string.Empty;
            world.LocalPlaces.StartLocationId = def.StartLocationId ?? string.Empty;
            world.LocalPlaces.ActiveMapLayoutId = def.MapLayoutId ?? string.Empty;
            RegisterEntries(world, def.Locations);

            if (string.IsNullOrEmpty(world.LocalPlaces.StartLocationId) ||
                !world.LocalPlaces.TryGet(world.LocalPlaces.StartLocationId, out _))
            {
                return Result.Failure(
                    ErrorCode.NotFound,
                    "localPlaceSet startLocationId invalid.",
                    world.LocalPlaces.StartLocationId);
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
                world.LocalPlaces.Register(loc);
            }
        }
    }
}
