using System;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>势力外交总览使用的正式只读关系类型。</summary>
    public enum FactionDiplomacyRelation
    {
        Self = 0,
        War = 1,
        Alliance = 2,
        Overlord = 3,
        Vassal = 4,
        Neutral = 5
    }

    /// <summary>
    /// 势力外交关系的领域只读查询。
    /// 优先级固定为：自己、战争、联盟、直接附庸、普通；Host 不得自行重组这些规则。
    /// </summary>
    public static class FactionDiplomacyRelationQuery
    {
        public static FactionDiplomacyRelation GetRelation(
            SimulationWorld world,
            string observerFactionId,
            string targetFactionId)
        {
            if (string.IsNullOrEmpty(observerFactionId) || string.IsNullOrEmpty(targetFactionId))
                return FactionDiplomacyRelation.Neutral;

            if (string.Equals(observerFactionId, targetFactionId, StringComparison.Ordinal))
                return FactionDiplomacyRelation.Self;

            if (WarGateService.IsAtWar(world, observerFactionId, targetFactionId))
                return FactionDiplomacyRelation.War;

            if (world?.Strategic?.Alliances != null &&
                world.Strategic.Alliances.AreAllied(observerFactionId, targetFactionId))
                return FactionDiplomacyRelation.Alliance;

            var vassalages = world?.Strategic?.Vassalages;
            if (vassalages != null &&
                vassalages.TryGetOverlord(observerFactionId, out var observerOverlord) &&
                string.Equals(observerOverlord, targetFactionId, StringComparison.Ordinal))
                return FactionDiplomacyRelation.Overlord;

            if (vassalages != null &&
                vassalages.TryGetOverlord(targetFactionId, out var targetOverlord) &&
                string.Equals(targetOverlord, observerFactionId, StringComparison.Ordinal))
                return FactionDiplomacyRelation.Vassal;

            return FactionDiplomacyRelation.Neutral;
        }

        public static FactionDiplomacyRelation GetRelationToPlayer(
            SimulationWorld world,
            string targetFactionId)
        {
            var playerFactionId = world?.Strategic?.PlayerFactionId ?? StrategicFactionCatalog.PlayerFactionId;
            return GetRelation(world, playerFactionId, targetFactionId);
        }
    }
}
