using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;
using XianXia.Data.Content;

namespace XianXia.Data.Bootstrap
{
    /// <summary>Legacy content migration only; never creates runtime FormalArmy or ArmyStack.</summary>
    public static class FormalArmyContentBootstrap
    {
        public static Result Apply(SimulationWorld world, DefinitionRegistry registry,
            OpeningScenarioDefinition scenario, GameStartLookup openingLookup = null)
        {
            if (scenario?.InitialFormalArmyIds == null) return Result.Success();
            for (var i = 0; i < scenario.InitialFormalArmyIds.Count; i++)
            {
                var parsed = DefinitionId.Parse(scenario.InitialFormalArmyIds[i]);
                if (parsed.IsFailure) return Result.Failure(parsed.Error);
                if (!registry.TryGetFormalArmy(parsed.Value, out var legacy) || legacy == null)
                    return Result.Failure(ErrorCode.NotFound, "Legacy FormalArmyDefinition is missing.", scenario.InitialFormalArmyIds[i]);
                var result = NpcSquadContentBootstrap.ApplyDefinition(world, registry, Convert(world, legacy), openingLookup);
                if (result.IsFailure) return result;
            }
            return Result.Success();
        }

        static NpcSquadDefinition Convert(SimulationWorld world, FormalArmyDefinition legacy)
        {
            var definition = new NpcSquadDefinition
            {
                Id = legacy.Id,
                SquadId = string.IsNullOrWhiteSpace(legacy.RuntimeArmyId)
                    ? "squad:legacy:" + legacy.Id.ToString().Replace(':', '_')
                    : "squad:migrated:" + legacy.RuntimeArmyId.Replace(':', '_'),
                Name = legacy.Name, FactionId = legacy.FactionId, AssemblySiteId = legacy.AssemblySiteId
            };
            if (legacy.InitialSurfacePosition != null)
                definition.InitialSurfacePosition = new NpcSquadInitialSurfacePositionDefinition
                { SurfaceId = legacy.InitialSurfacePosition.SurfaceId, WorldX = legacy.InitialSurfacePosition.WorldX, WorldY = legacy.InitialSurfacePosition.WorldY };
            else if (legacy.InitialSurfaceDeployment != null)
                definition.InitialSurfaceDeployment = new NpcSquadInitialSurfaceDeploymentDefinition
                {
                    SurfaceId = legacy.InitialSurfaceDeployment.SurfaceId, AnchorSiteId = legacy.InitialSurfaceDeployment.AnchorSiteId,
                    OffsetCellsX = legacy.InitialSurfaceDeployment.OffsetCellsX, OffsetCellsY = legacy.InitialSurfaceDeployment.OffsetCellsY
                };
            else if (legacy.InitialHex != null && world?.HexWorld?.HasGrid == true)
            {
                HexMath.ToWorldPosition(new HexCoord(legacy.InitialHex.Q, legacy.InitialHex.R), world.HexWorld.HexSize, out var x, out var y);
                definition.InitialSurfacePosition = new NpcSquadInitialSurfacePositionDefinition
                { SurfaceId = ResolveOnlySurfaceId(world), WorldX = x, WorldY = y };
            }
            for (var i = 0; i < legacy.Members.Count; i++)
                definition.Members.Add(new NpcSquadMemberDefinition
                {
                    CharacterDefinitionId = legacy.Members[i].CharacterDefinitionId,
                    DisplayName = legacy.Members[i].DisplayName, Leader = legacy.Members[i].Leader,
                    ReuseOpeningSpawn = legacy.Members[i].ReuseOpeningSpawn
                });
            return definition;
        }

        static string ResolveOnlySurfaceId(SimulationWorld world)
        {
            foreach (var pair in world.SurfaceGround.Registered) return pair.Key;
            return string.Empty;
        }
    }
}
