using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Bootstrap;
using XianXia.Core.Entities;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;
using XianXia.Data.Content;

namespace XianXia.Data.Bootstrap
{
    /// <summary>Current content bootstrap: authored NPC groups become Squad + SquadWorldMotion only.</summary>
    public static class NpcSquadContentBootstrap
    {
        public static Result Apply(SimulationWorld world, DefinitionRegistry registry,
            OpeningScenarioDefinition scenario, GameStartLookup openingLookup = null)
        {
            if (world?.Strategic == null || registry == null)
                return Result.Failure(ErrorCode.InvalidArgument, "NPC Squad bootstrap requires world + registry.");
            if (scenario?.InitialNpcSquadIds == null) return Result.Success();
            for (var i = 0; i < scenario.InitialNpcSquadIds.Count; i++)
            {
                var parsed = DefinitionId.Parse(scenario.InitialNpcSquadIds[i]);
                if (parsed.IsFailure) return Result.Failure(parsed.Error);
                if (!registry.TryGetNpcSquad(parsed.Value, out var definition) || definition == null)
                    return Result.Failure(ErrorCode.NotFound, "Opening scenario references missing NpcSquadDefinition.", scenario.InitialNpcSquadIds[i]);
                var result = ApplyDefinition(world, registry, definition, openingLookup);
                if (result.IsFailure) return result;
            }
            return Result.Success();
        }

        internal static Result ApplyDefinition(SimulationWorld world, DefinitionRegistry registry,
            NpcSquadDefinition definition, GameStartLookup openingLookup)
        {
            if (world.Strategic.Squads.TryGet(definition.SquadId, out _)) return Result.Success();
            var atSite = definition.InitialSurfacePosition == null &&
                         definition.InitialSurfaceDeployment == null;
            var surfaceId = string.Empty;
            var position = default(WorldVec2);
            if (!atSite && !TryResolveInitialPosition(world, registry, definition, out surfaceId, out position))
                return Result.Failure(ErrorCode.InvalidOperation, "NPC Squad initial Surface position is unavailable.", definition.Id.ToString());
            var members = new List<EntityId>(definition.Members.Count);
            var leader = EntityId.None;
            for (var i = 0; i < definition.Members.Count; i++)
            {
                var source = definition.Members[i];
                Entity entity;
                if (source.ReuseOpeningSpawn)
                {
                    if (openingLookup == null || !openingLookup.TryGetEntity(source.CharacterDefinitionId, out var existingId) ||
                        !world.Entities.TryGet(existingId, out entity) || entity == null)
                        return Result.Failure(ErrorCode.NotFound, "NPC Squad reuseOpeningSpawn member missing.", source.CharacterDefinitionId);
                    if (!entity.TryGet<FactionMembershipComponent>(out var faction) ||
                        !string.Equals(faction.FactionId, definition.FactionId, StringComparison.Ordinal))
                        return Result.Failure(ErrorCode.InvalidOperation, "NPC Squad reused member faction mismatch.", source.CharacterDefinitionId);
                }
                else
                {
                    var built = ContentGameStart.BuildSpawnFromDefinition(registry, source.CharacterDefinitionId, true, source.DisplayName);
                    if (built.IsFailure) return Result.Failure(built.Error);
                    var spawned = GameStartBootstrap.SpawnIntoWorld(world, built.Value);
                    if (spawned.IsFailure) return Result.Failure(spawned.Error);
                    entity = spawned.Value;
                    entity.Get<FactionMembershipComponent>().Assign(definition.FactionId, FactionRoleKind.Member);
                }
                members.Add(entity.Id);
                if (source.Leader) leader = entity.Id;
            }
            var created = SquadMembershipService.Create(world, definition.SquadId, members, leader,
                string.Empty, SquadCommandKind.SquadWorldMotion, false, definition.Name, definition.FactionId);
            if (created.IsFailure) return Result.Failure(created.Error);
            var initialized = atSite
                ? SquadWorldMotionService.InitializeAtSite(world, definition.SquadId, definition.AssemblySiteId)
                : SquadWorldMotionService.Initialize(world, definition.SquadId, surfaceId, position);
            return initialized.IsFailure ? Result.Failure(initialized.Error) : Result.Success();
        }

        static bool TryResolveInitialPosition(SimulationWorld world, DefinitionRegistry registry,
            NpcSquadDefinition definition, out string surfaceId, out WorldVec2 point)
        {
            surfaceId = definition.InitialSurfaceDeployment?.SurfaceId ?? definition.InitialSurfacePosition?.SurfaceId ?? string.Empty;
            point = default;
            if (definition.InitialSurfacePosition != null)
                point = new WorldVec2(definition.InitialSurfacePosition.WorldX, definition.InitialSurfacePosition.WorldY);
            else if (definition.InitialSurfaceDeployment != null)
            {
                if (!TryResolveCoreCenter(registry, definition.InitialSurfaceDeployment.AnchorSiteId,
                        surfaceId, out point)) return false;
                if (registry.TryGetOutdoorSurfaceGeography(surfaceId, out var geography))
                    point = new WorldVec2(point.X + definition.InitialSurfaceDeployment.OffsetCellsX * geography.Navigation.CellSize,
                        point.Y + definition.InitialSurfaceDeployment.OffsetCellsY * geography.Navigation.CellSize);
            }
            else return false;
            return world.SurfaceGround.TryGet(surfaceId, out var navigation) && navigation.Contains(point.X, point.Y) && navigation.IsWalkable(point.X, point.Y);
        }

        internal static bool TryResolveCoreCenter(DefinitionRegistry registry, string siteId, string surfaceId, out WorldVec2 center)
        {
            center = default;
            var found = false;
            foreach (var pair in registry.OutdoorSurfaces)
                foreach (var placement in pair.Value.SitePlacements)
                {
                    if (placement == null || !string.Equals(pair.Value.SurfaceId, surfaceId, StringComparison.Ordinal) ||
                        !string.Equals(placement.SiteId, siteId, StringComparison.Ordinal) ||
                        !string.Equals(placement.Kind, "controlCore", StringComparison.OrdinalIgnoreCase)) continue;
                    if (found) return false;
                    center = new WorldVec2(placement.WorldX + placement.WorldWidth * .5f, placement.WorldY + placement.WorldHeight * .5f);
                    found = true;
                }
            return found;
        }
    }
}
