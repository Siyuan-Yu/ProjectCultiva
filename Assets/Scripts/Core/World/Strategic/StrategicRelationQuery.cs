using System;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    public enum StrategicRelationBucket
    {
        Self = 0,
        Ally = 1,
        Other = 2,
        Enemy = 3
    }

    /// <summary>相对玩家势力的动态战略关系（只读；不持久化）。</summary>
    public static class StrategicRelationQuery
    {
        public static StrategicRelationBucket GetRelationToPlayer(
            SimulationWorld world,
            string factionId)
        {
            var playerFaction = world?.Strategic?.PlayerFactionId ?? StrategicFactionCatalog.PlayerFactionId;
            return GetRelation(world, playerFaction, factionId);
        }

        public static StrategicRelationBucket GetRelation(
            SimulationWorld world,
            string viewerFactionId,
            string subjectFactionId)
        {
            if (string.IsNullOrEmpty(viewerFactionId) || string.IsNullOrEmpty(subjectFactionId))
                return StrategicRelationBucket.Other;

            if (string.Equals(viewerFactionId, subjectFactionId, StringComparison.Ordinal))
                return StrategicRelationBucket.Self;

            if (AreAllied(world, viewerFactionId, subjectFactionId))
                return StrategicRelationBucket.Ally;

            if (WarGateService.IsAtWar(world, viewerFactionId, subjectFactionId))
                return StrategicRelationBucket.Enemy;

            return StrategicRelationBucket.Other;
        }

        public static bool AreAllied(SimulationWorld world, string factionA, string factionB)
        {
            if (world?.Strategic?.Alliances == null)
                return false;
            return world.Strategic.Alliances.AreAllied(factionA, factionB);
        }
    }
}
