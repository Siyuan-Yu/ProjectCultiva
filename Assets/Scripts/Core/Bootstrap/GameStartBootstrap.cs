using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Events;
using XianXia.Core.Random;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.Bootstrap
{
    /// <summary>
    /// Vertical Slice 0.1 bootstrap: create world layout + starting characters + init events.
    /// No movement, work, combat, cultivation, or schedule systems.
    /// </summary>
    public sealed class GameStartBootstrap
    {
        public Result<GameStartResult> Start(
            WorldInitData worldData,
            IReadOnlyList<CharacterSpawnRequest> spawns,
            IRandomSource random = null,
            string packageId = null,
            string packageVersion = null)
        {
            if (worldData == null)
                return Result.Fail<GameStartResult>(ErrorCode.InvalidArgument, "WorldInitData is null.");
            if (spawns == null || spawns.Count == 0)
                return Result.Fail<GameStartResult>(ErrorCode.InvalidArgument, "At least one CharacterSpawnRequest required.");

            if (worldData.Regions == null || worldData.Regions.Count == 0)
                return Result.Fail<GameStartResult>(ErrorCode.MissingRequiredField, "WorldInitData.Regions required.");

            var primaryRegion = worldData.Regions[0].Id;
            if (primaryRegion.IsNone)
                return Result.Fail<GameStartResult>(ErrorCode.InvalidArgument, "Primary RegionId must be non-zero.");

            var world = new SimulationWorld(random: random ?? new DeterministicRandom(1), regionId: primaryRegion)
            {
                WorldLayout = worldData,
                EnabledPackageId = packageId ?? "base",
                EnabledPackageVersion = packageVersion ?? "0.0.1"
            };

            var created = new List<EntityId>(spawns.Count);
            foreach (var spawn in spawns)
            {
                if (spawn == null)
                    return Result.Fail<GameStartResult>(ErrorCode.InvalidArgument, "Spawn request is null.");

                var entityResult = world.Entities.CreateCharacter(spawn.DefinitionId, spawn.Name);
                if (entityResult.IsFailure)
                    return Result.Fail<GameStartResult>(entityResult.Error);

                var entity = entityResult.Value;
                if (entity.TryGet<AttributesComponent>(out var attrs) && spawn.BaseAttributes != null)
                {
                    foreach (var kv in spawn.BaseAttributes)
                        attrs.SetBase(kv.Key, kv.Value);
                }

                world.Events.Publish(EventType.EntityCreated, world.Tick, target: entity.Id, payload: spawn.DefinitionId.ToString());
                created.Add(entity.Id);
            }

            world.Events.Publish(
                EventType.WorldInitialized,
                world.Tick,
                payload: "region=" + primaryRegion.Value + ";characters=" + created.Count);

            return Result.Ok(new GameStartResult(world, created));
        }
    }

    public sealed class GameStartResult
    {
        public GameStartResult(SimulationWorld world, IReadOnlyList<EntityId> characterIds)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
            CharacterIds = characterIds ?? Array.Empty<EntityId>();
        }

        public SimulationWorld World { get; }

        public IReadOnlyList<EntityId> CharacterIds { get; }
    }
}
