using System.Collections.Generic;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    public enum HexStrategicContextActionKind
    {
        AttackArmy,
        EnterLingeringBattlefield,
        AttackLingeringBattlefield,
        MoveToHex
    }

    /// <summary>Hex 上可攻击的 Active Enemy FormalArmy（目标真源 = ArmyId / StackId）。</summary>
    public sealed class HexActiveEnemyArmyTarget
    {
        public string FormalArmyId { get; set; } = string.Empty;
        public string StackId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool CanAttack { get; set; }
        public string BlockReason { get; set; } = string.Empty;
    }

    /// <summary>Hex 右键 Context：Active Army + Presentation Count + Lingering Runtime 组合查询。</summary>
    public sealed class HexResidualContextView
    {
        public HexCoord Hex { get; set; }
        public LingeringBattlefieldContext Lingering { get; set; }
        public bool HasActiveLingering { get; set; }
        public bool CanEnterFriendlyLingering { get; set; }
        public bool CanAttackEnemyLingering { get; set; }
        public string FriendlyResidualSummary { get; set; } = string.Empty;
        public string EnemyResidualSummary { get; set; } = string.Empty;
        public List<HexActiveEnemyArmyTarget> ActiveEnemyArmies { get; } = new List<HexActiveEnemyArmyTarget>(2);

        public bool HasActiveEnemyArmy => ActiveEnemyArmies.Count > 0;

        public HexActiveEnemyArmyTarget PrimaryActiveEnemyArmy =>
            ActiveEnemyArmies.Count > 0 ? ActiveEnemyArmies[0] : null;

        public bool CanAttackActiveEnemyArmy =>
            PrimaryActiveEnemyArmy != null && PrimaryActiveEnemyArmy.CanAttack;
    }

    public static class HexResidualContextQuery
    {
        public static HexResidualContextView Build(SimulationWorld world, HexCoord hex) =>
            Build(world, hex, null);

        public static HexResidualContextView Build(
            SimulationWorld world,
            HexCoord hex,
            string attackerFactionId)
        {
            var view = new HexResidualContextView { Hex = hex };
            ApplyResidualPresentationAtHex(world, hex, view);

            view.HasActiveLingering = LingeringBattlefieldQueryService.TryGetLingeringBattlefieldAtHex(
                world, hex, out var linger);
            view.Lingering = linger;

            if (linger != null)
            {
                if (string.IsNullOrEmpty(view.FriendlyResidualSummary) &&
                    (linger.SelfDownedCount > 0 || linger.SelfDeadCount > 0))
                {
                    view.FriendlyResidualSummary = FormatSummary(linger.SelfDownedCount, linger.SelfDeadCount);
                }

                if (string.IsNullOrEmpty(view.EnemyResidualSummary) &&
                    (linger.EnemyDownedCount > 0 || linger.EnemyDeadCount > 0))
                {
                    view.EnemyResidualSummary = FormatSummary(linger.EnemyDownedCount, linger.EnemyDeadCount);
                }

                view.CanEnterFriendlyLingering =
                    !linger.FriendlyFocusId.IsNone;
                view.CanAttackEnemyLingering = linger.HasAttackableEnemyLingering;
            }

            HexActiveEnemyArmyQuery.CollectAtHex(world, hex, attackerFactionId, view.ActiveEnemyArmies);
            return view;
        }

        /// <summary>与 Marker 绘制同源：不依赖 BattlefieldLingering Runtime。</summary>
        static void ApplyResidualPresentationAtHex(
            SimulationWorld world,
            HexCoord hex,
            HexResidualContextView view)
        {
            if (world == null || view == null)
                return;

            var counts = StrategicResidualPresentationQuery.CountAtHex(world, hex);
            if (counts.SelfDowned + counts.SelfDead > 0)
                view.FriendlyResidualSummary = FormatSummary(counts.SelfDowned, counts.SelfDead);
            if (counts.EnemyDowned + counts.EnemyDead > 0)
                view.EnemyResidualSummary = FormatSummary(counts.EnemyDowned, counts.EnemyDead);
        }

        /// <summary>
        /// 生产菜单动作：只因为真正 Active Enemy FormalArmy 产生 AttackArmy。
        /// Residual 只是 world population 信息（CanEnterFriendlyLingering / CanAttackEnemyLingering
        /// 保留为 legacy query 兼容字段），不再产生任何特殊 Encounter action。
        /// </summary>
        public static List<HexStrategicContextActionKind> BuildMenuActionKinds(HexResidualContextView view)
        {
            var actions = new List<HexStrategicContextActionKind>(3);
            if (view == null)
                return actions;

            if (view.HasActiveEnemyArmy)
                actions.Add(HexStrategicContextActionKind.AttackArmy);
            return actions;
        }

        [System.Obsolete("Use HexRightClickResolver.Resolve for RTS right-click semantics.")]
        public static List<HexStrategicContextActionKind> BuildActionKinds(
            HexResidualContextView view,
            bool hasSelectedMovableArmy,
            bool passableHex)
        {
            var actions = BuildMenuActionKinds(view);
            if (hasSelectedMovableArmy && passableHex &&
                actions.Count == 0)
                actions.Add(HexStrategicContextActionKind.MoveToHex);
            return actions;
        }

        static string FormatSummary(int downed, int dead)
        {
            if (downed > 0 && dead > 0)
                return "弥留 " + downed + " / 阵亡 " + dead;
            if (downed > 0)
                return "弥留 " + downed;
            if (dead > 0)
                return "阵亡 " + dead;
            return string.Empty;
        }
    }
}
