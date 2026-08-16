using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Data.Content;

namespace XianXia.Data.Bootstrap
{
    public static class WorldGraphBootstrap
    {
        public const string DefaultCh01GraphId = "base:graph_ch01";

        public static Result ApplyOpening(
            SimulationWorld world,
            DefinitionRegistry registry,
            OpeningScenarioDefinition scenario,
            GameStartLookup lookup = null,
            IList<OpeningSpawnEntry> spawnEntries = null)
        {
            if (world == null || registry == null)
                return Result.Failure(ErrorCode.InvalidArgument, "WorldGraph bootstrap args null.");

            world.WorldGraph.Clear();
            world.WorldPresence.Clear();
            world.PartyWorld.ClearTravel();
            world.PartyWorld.NodeId = string.Empty;
            world.PartyWorld.LocalMapId = string.Empty;

            var graphIdText = scenario != null ? scenario.OpeningWorldGraphId : null;
            if (string.IsNullOrWhiteSpace(graphIdText))
                graphIdText = DefaultCh01GraphId;

            var parsed = DefinitionId.Parse(graphIdText.Trim());
            if (parsed.IsFailure)
                return Result.Failure(parsed.Error);
            if (!registry.TryGetWorldGraph(parsed.Value, out var def))
                return Result.Success();

            world.WorldGraph.GraphId = def.Id.ToString();
            world.WorldGraph.GraphName = def.Name ?? string.Empty;
            world.WorldGraph.StartNodeId = def.StartNodeId ?? string.Empty;

            if (def.Nodes != null)
            {
                for (var i = 0; i < def.Nodes.Count; i++)
                {
                    var n = def.Nodes[i];
                    if (n == null || string.IsNullOrWhiteSpace(n.Id))
                        continue;
                    var node = new WorldNodeState
                    {
                        Id = n.Id,
                        Name = n.Name ?? n.Id,
                        Kind = n.Kind ?? string.Empty,
                        LocalMapId = n.LocalMapId ?? string.Empty,
                        WorldX = n.WorldX,
                        WorldY = n.WorldY,
                        OwnerId = n.OwnerId ?? string.Empty,
                        State = n.State ?? string.Empty
                    };
                    if (n.Tags != null)
                        node.Tags.AddRange(n.Tags);
                    world.WorldGraph.RegisterNode(node);
                }
            }

            if (def.Routes != null)
            {
                for (var i = 0; i < def.Routes.Count; i++)
                {
                    var r = def.Routes[i];
                    if (r == null || string.IsNullOrWhiteSpace(r.Id))
                        continue;
                    var route = new WorldRouteState
                    {
                        Id = r.Id,
                        FromNodeId = r.FromNodeId ?? string.Empty,
                        ToNodeId = r.ToNodeId ?? string.Empty,
                        Kind = r.Kind ?? string.Empty,
                        TravelCost = r.TravelCost,
                        Danger = r.Danger,
                        OwnerId = r.OwnerId ?? string.Empty,
                        State = string.IsNullOrWhiteSpace(r.State) ? "Open" : r.State,
                        Directed = r.Directed,
                        EncounterPoolId = r.EncounterPoolId ?? string.Empty
                    };
                    // 通行条件数据可保留，运行时旅行不再检查（产品：无令牌门槛）
                    if (r.TraversalRequirements != null)
                        route.TraversalRequirements.AddRange(r.TraversalRequirements);
                    world.WorldGraph.RegisterRoute(route);
                }
            }

            var start = world.WorldGraph.StartNodeId;
            if (string.IsNullOrWhiteSpace(start) || !world.WorldGraph.TryGetNode(start, out _))
            {
                return Result.Failure(
                    ErrorCode.NotFound,
                    "WorldGraph startNodeId invalid.",
                    start);
            }

            var party = CollectPartyEntityIds(scenario, lookup, spawnEntries);
            if (party.Count > 0)
                return WorldTravelService.PlaceAgentsAtNode(world, party, start);

            return WorldTravelService.EnterNode(world, start);
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
