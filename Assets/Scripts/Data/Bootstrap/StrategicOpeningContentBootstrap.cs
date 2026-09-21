using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;
using XianXia.Data.Content;

namespace XianXia.Data.Bootstrap
{
    /// <summary>只在新游戏装配时把 Scenario 战略初始状态写入既有 Runtime Board。</summary>
    public static class StrategicOpeningContentBootstrap
    {
        public static Result Apply(SimulationWorld world, DefinitionRegistry registry, OpeningScenarioDefinition scenario)
        {
            var opening = scenario?.StrategicOpening;
            if (opening == null) return Result.Success();
            world.Strategic.PlayerFactionId = opening.PlayerFactionId;
            foreach (var v in opening.Vassalages)
            {
                if (world.Strategic.Vassalages.TryGetOverlord(v.VassalFactionId, out var old))
                {
                    if (old == v.OverlordFactionId) continue;
                    return Result.Failure(ErrorCode.InvalidOperation, "Opening vassalage conflicts with runtime board.");
                }
                if (!world.Strategic.Vassalages.TryBindVassalage(v.VassalFactionId, v.OverlordFactionId))
                    return Result.Failure(ErrorCode.InvalidOperation, "Opening vassalage is invalid.");
            }
            foreach (var a in opening.Alliances)
            {
                if (world.Strategic.Alliances.AreAllied(a.FactionAId, a.FactionBId)) continue;
                if (!world.Strategic.Alliances.FormAlliance(a.FactionAId, a.FactionBId, out _))
                    return Result.Failure(ErrorCode.InvalidOperation, "Opening alliance is invalid.");
            }
            foreach (var war in opening.InitialWars)
            {
                var r = WarGateService.DeclareWar(world, war.DeclarerFactionId, war.TargetFactionId);
                if (r.IsFailure) return r;
            }
            return Result.Success();
        }
    }
}
