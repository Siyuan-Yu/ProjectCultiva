using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Events;
using XianXia.Core.Random;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;

namespace XianXia.Core.Bootstrap
{
    /// <summary>
    /// Bootstrap: world layout + Content-driven starting characters／NPCs + init events.
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

            var characters = new List<EntityId>();
            var npcs = new List<EntityId>();
            var byDefinition = new Dictionary<string, EntityId>(StringComparer.Ordinal);

            foreach (var spawn in spawns)
            {
                if (spawn == null)
                    return Result.Fail<GameStartResult>(ErrorCode.InvalidArgument, "Spawn request is null.");

                Result<Entity> entityResult;
                if (spawn.EntityKind == SpawnEntityKind.Npc)
                    entityResult = world.Entities.CreateNpc(spawn.DefinitionId, spawn.Name);
                else
                    entityResult = world.Entities.CreateCharacter(spawn.DefinitionId, spawn.Name);

                if (entityResult.IsFailure)
                    return Result.Fail<GameStartResult>(entityResult.Error);

                var entity = entityResult.Value;
                if (entity.TryGet<AttributesComponent>(out var attrs) && spawn.BaseAttributes != null)
                {
                    foreach (var kv in spawn.BaseAttributes)
                        attrs.SetBase(kv.Key, kv.Value);
                }

                if (entity.TryGet<PersonalityProfileComponent>(out var profile))
                    profile.SetTags(spawn.PersonalityTags);

                world.Events.Publish(
                    EventType.EntityCreated,
                    world.Tick,
                    target: entity.Id,
                    payload: spawn.DefinitionId.ToString());

                if (spawn.EntityKind == SpawnEntityKind.Npc)
                    npcs.Add(entity.Id);
                else
                    characters.Add(entity.Id);

                var defKey = spawn.DefinitionId.ToString();
                if (!string.IsNullOrEmpty(defKey))
                    byDefinition[defKey] = entity.Id;
            }

            if (characters.Count == 0)
            {
                return Result.Fail<GameStartResult>(
                    ErrorCode.InvalidArgument,
                    "Opening spawn list must include at least one Character.");
            }

            world.Events.Publish(
                EventType.WorldInitialized,
                world.Tick,
                payload: "region=" + primaryRegion.Value +
                         ";characters=" + characters.Count +
                         ";npcs=" + npcs.Count);

            return Result.Ok(new GameStartResult(world, characters, npcs, byDefinition));
        }
    }

    public sealed class GameStartResult
    {
        public GameStartResult(
            SimulationWorld world,
            IReadOnlyList<EntityId> characterIds,
            IReadOnlyList<EntityId> npcIds = null,
            IReadOnlyDictionary<string, EntityId> spawnedByDefinitionId = null)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
            CharacterIds = characterIds ?? Array.Empty<EntityId>();
            NpcIds = npcIds ?? Array.Empty<EntityId>();
            SpawnedByDefinitionId = spawnedByDefinitionId ?? new Dictionary<string, EntityId>(StringComparer.Ordinal);
        }

        public SimulationWorld World { get; }

        public IReadOnlyList<EntityId> CharacterIds { get; }

        public IReadOnlyList<EntityId> NpcIds { get; }

        /// <summary>DefinitionId.ToString() → EntityId (last spawn wins if duplicates).</summary>
        public IReadOnlyDictionary<string, EntityId> SpawnedByDefinitionId { get; }
    }
}
