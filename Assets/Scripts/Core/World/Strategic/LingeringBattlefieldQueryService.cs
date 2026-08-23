using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// 残留战场 Runtime 查询（真源：BattlefieldLingering + BattleAnchorHex，非 Marker Count）。
    /// </summary>
    public sealed class LingeringBattlefieldContext
    {
        public HexCoord BattleAnchorHex { get; set; }
        public string EnemyStackId { get; set; } = string.Empty;
        public EntityId FriendlyFocusId { get; set; }
        public int SelfDownedCount { get; set; }
        public int SelfDeadCount { get; set; }
        public int EnemyDownedCount { get; set; }
        public int EnemyDeadCount { get; set; }

        public bool HasFriendlyResidual => SelfDownedCount + SelfDeadCount > 0;
        public bool HasEnemyResidual => EnemyDownedCount + EnemyDeadCount > 0;

        public bool HasAttackableEnemyLingering =>
            !string.IsNullOrEmpty(EnemyStackId) && HasEnemyResidual;
    }

    public static class LingeringBattlefieldQueryService
    {
        public static bool TryGetLingeringBattlefieldAtHex(
            SimulationWorld world,
            HexCoord hex,
            out LingeringBattlefieldContext context)
        {
            context = null;
            if (world?.Strategic?.Encounter == null)
                return false;
            if (!world.Strategic.Encounter.BattlefieldLingering)
                return false;
            if (!StrategicEncounterResolveService.HasLingeringBattlefieldRemnants(world))
                return false;
            // 多场残留并存：匹配 Registry 中该 Hex 的独立 Battlefield
            if (!world.Strategic.LingeringBattlefields.HasAtHex(hex))
            {
                var counts = StrategicResidualPresentationQuery.CountAtHex(world, hex);
                if (counts.EnemyDowned + counts.EnemyDead + counts.SelfDowned + counts.SelfDead <= 0)
                    return false;
            }

            context = BuildContext(world, hex);
            return context != null;
        }

        static LingeringBattlefieldContext BuildContext(SimulationWorld world, HexCoord hex)
        {
            var ctx = new LingeringBattlefieldContext { BattleAnchorHex = hex };
            var groups = StrategicResidualPresentationQuery.Query(world);
            for (var i = 0; i < groups.Count; i++)
            {
                var g = groups[i];
                if (g == null || !g.Hex.Equals(hex))
                    continue;

                if (g.Relation == StrategicRelationBucket.Self ||
                    g.Relation == StrategicRelationBucket.Ally)
                {
                    if (g.State == ResidualStateBucket.Dead)
                        ctx.SelfDeadCount += g.Count;
                    else
                        ctx.SelfDownedCount += g.Count;
                }
                else if (g.Relation == StrategicRelationBucket.Enemy)
                {
                    if (g.State == ResidualStateBucket.Dead)
                        ctx.EnemyDeadCount += g.Count;
                    else
                        ctx.EnemyDownedCount += g.Count;
                }
            }

            ctx.FriendlyFocusId = ResolveFriendlyFocusAtHex(world, hex, groups);
            ctx.EnemyStackId = ResolveEnemyStackId(world, hex);
            if (ctx.HasEnemyResidual && string.IsNullOrEmpty(ctx.EnemyStackId))
                ctx.EnemyStackId = ResolveFallbackEnemyStackId(world, hex);
            return ctx;
        }

        static EntityId ResolveFriendlyFocusAtHex(
            SimulationWorld world,
            HexCoord hex,
            System.Collections.Generic.List<ResidualMarkerGroupView> groups)
        {
            for (var i = 0; i < groups.Count; i++)
            {
                var g = groups[i];
                if (g == null || !g.Hex.Equals(hex))
                    continue;
                if (g.Relation != StrategicRelationBucket.Self &&
                    g.Relation != StrategicRelationBucket.Ally)
                    continue;

                for (var j = 0; j < g.Characters.Count; j++)
                {
                    var row = g.Characters[j];
                    if (row == null || row.CharacterId.IsNone)
                        continue;
                    if (LingeringBattlefieldPartyService.IsFriendlyLingeringDowned(world, row.CharacterId))
                        return row.CharacterId;
                }
            }

            return EntityId.None;
        }

        static string ResolveEnemyStackId(SimulationWorld world, HexCoord hex)
        {
            if (world.Strategic.LingeringBattlefields.TryGetAtHex(hex, out var battlefield) &&
                battlefield != null &&
                !string.IsNullOrEmpty(battlefield.EnemyStackId))
                return battlefield.EnemyStackId;

            return string.Empty;
        }

        static string ResolveFallbackEnemyStackId(SimulationWorld world, HexCoord hex)
        {
            if (world.Strategic.LingeringBattlefields.TryGetAtHex(hex, out var battlefield) &&
                battlefield != null)
                return battlefield.EnemyStackId ?? string.Empty;

            return string.Empty;
        }
    }
}
