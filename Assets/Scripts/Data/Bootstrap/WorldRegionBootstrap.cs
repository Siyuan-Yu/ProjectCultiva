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

            if (string.IsNullOrEmpty(world.WorldRegion.StartLocationId) ||
                !world.WorldRegion.TryGet(world.WorldRegion.StartLocationId, out _))
            {
                return Result.Failure(
                    ErrorCode.NotFound,
                    "World region startLocationId invalid.",
                    world.WorldRegion.StartLocationId);
            }

            // 名册开局时必须用 roster entries，不能只扫 scenario.Spawns（否则洞府残影等挂不上驻点）。
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
                        {
                            // 洞府内容威胁：区域未挂驻点时不要丢到地表开局点。
                            // 一般敌对仍可落在地表；洞府怪靠 residentNpc→内室地点配置。
                            continue;
                        }
                    }

                    var placed = EnsureLocation(entity, placeId);
                    if (placed.IsFailure)
                        return placed;
                }
            }

            // 兜底：凡地点声明了 residentNpc，按 definitionId 再钉一次（防漏扫）。
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

        /// <summary>内容 tag=cave：洞府／秘境内威胁，不是「所有敌人」。 </summary>
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
