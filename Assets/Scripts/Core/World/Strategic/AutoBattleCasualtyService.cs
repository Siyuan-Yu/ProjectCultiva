using System;
using System.Collections.Generic;
using System.Text;
using XianXia.Core.Attributes;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.World.Strategic
{
    public sealed class AutoBattleReport
    {
        public int PlayerKilled { get; set; }
        public int PlayerIncapacitated { get; set; }
        public int PlayerWounded { get; set; }
        public int EnemyMembersEliminated { get; set; }
        public int EnemyMembersSpared { get; set; }
        public string Summary { get; set; } = string.Empty;
    }

    /// <summary>大地图自动战：按战力结算双方伤亡（弥留／击杀）。</summary>
    public static class AutoBattleCasualtyService
    {
        public const double DefaultEnemyDirectKillChanceOnPlayerLoss = 0.45;

        public static AutoBattleReport ApplyPlayerVictory(
            SimulationWorld world,
            IReadOnlyList<EntityId> party,
            ArmyStack enemyStack,
            int playerPower,
            int enemyPower,
            bool executeOnWin)
        {
            var report = new AutoBattleReport();
            if (world == null || enemyStack == null)
                return report;

            var intensity = ResolveIntensity(playerPower, enemyPower);
            ApplyPlayerChipDamage(world, party, intensity, report);

            var members = Math.Max(1, enemyStack.MemberCount);
            if (executeOnWin)
            {
                report.EnemyMembersEliminated = members;
                report.EnemyMembersSpared = 0;
                enemyStack.IncapacitatedMemberCount = 0;
                enemyStack.IsBattlefieldRemnant = false;
                world.Strategic.Armies.Remove(enemyStack.Id);
            }
            else
            {
                // 未勾选处决：不记阵亡，全员弥留残留在接战点
                report.EnemyMembersEliminated = 0;
                report.EnemyMembersSpared = members;
                enemyStack.MemberCount = members;
                enemyStack.IncapacitatedMemberCount = members;
                enemyStack.IsBattlefieldRemnant = true;
                enemyStack.CombatPower = Math.Max(1, enemyStack.CombatPower);
            }

            report.Summary = BuildSummary(report, playerWon: true, executeOnWin);
            return report;
        }

        public static AutoBattleReport ApplyPlayerDefeat(
            SimulationWorld world,
            IReadOnlyList<EntityId> party,
            int playerPower,
            int enemyPower)
        {
            var report = new AutoBattleReport();
            if (world == null || party == null || party.Count == 0)
                return report;

            var intensity = ResolveIntensity(playerPower, enemyPower);
            var killChance = Math.Clamp(
                DefaultEnemyDirectKillChanceOnPlayerLoss + intensity * 0.35,
                0.15,
                0.85);

            for (var i = 0; i < party.Count; i++)
            {
                var id = party[i];
                if (id.IsNone || !world.Entities.TryGet(id, out var entity) || entity == null)
                    continue;
                if (!CombatLifeStateService.CanFight(entity))
                    continue;

                var roll = world.Random.NextDouble();
                if (roll < killChance)
                {
                    ApplyDirectKill(world, entity);
                    report.PlayerKilled++;
                }
                else if (roll < killChance + 0.35)
                {
                    ApplyIncapacitated(world, entity);
                    report.PlayerIncapacitated++;
                }
                else
                {
                    ApplyWound(world, entity, intensity);
                    report.PlayerWounded++;
                }
            }

            report.Summary = BuildSummary(report, playerWon: false, executeOnWin: false);
            return report;
        }

        static double ResolveIntensity(int playerPower, int enemyPower)
        {
            var p = Math.Max(1, playerPower);
            var e = Math.Max(1, enemyPower);
            return e / (p + e);
        }

        static void ApplyPlayerChipDamage(
            SimulationWorld world,
            IReadOnlyList<EntityId> party,
            double intensity,
            AutoBattleReport report)
        {
            if (party == null || party.Count == 0)
                return;

            var woundChance = 0.15 + intensity * 0.35;
            for (var i = 0; i < party.Count; i++)
            {
                var id = party[i];
                if (id.IsNone || !world.Entities.TryGet(id, out var entity) || entity == null)
                    continue;
                if (!CombatLifeStateService.CanFight(entity))
                    continue;
                if (world.Random.NextDouble() >= woundChance)
                    continue;

                ApplyWound(world, entity, intensity * 0.6);
                report.PlayerWounded++;
            }
        }

        static void ApplyDirectKill(SimulationWorld world, Entity entity)
        {
            if (entity == null)
                return;
            CombatDamageRules.EnsureVitals(entity);
            if (entity.TryGet<CombatVitalsComponent>(out var vitals))
                vitals.CurrentHp = 0;
            if (entity.TryGet<LifecycleComponent>(out var life))
                life.State = LifecycleState.Incapacitated;
            CombatLifeStateService.TryConfirmDeath(world, EntityId.None, entity, out _);
        }

        static void ApplyIncapacitated(SimulationWorld world, Entity entity)
        {
            if (entity == null)
                return;
            CombatDamageRules.EnsureVitals(entity);
            if (entity.TryGet<CombatVitalsComponent>(out var vitals))
                vitals.CurrentHp = 0;
            CombatLifeStateService.TryEnterIncapacitated(world, entity);
        }

        static void ApplyWound(SimulationWorld world, Entity entity, double severity)
        {
            if (entity == null)
                return;
            CombatDamageRules.EnsureVitals(entity);
            if (!entity.TryGet<CombatVitalsComponent>(out var vitals) ||
                !entity.TryGet<AttributesComponent>(out var attrs))
                return;

            var maxHp = Math.Max(1, attrs.GetFinal(AttributeId.MaxHp));
            var lossPct = 0.25 + severity * 0.45;
            var loss = Math.Max(1, (int)Math.Round(maxHp * lossPct));
            vitals.CurrentHp = Math.Max(1, vitals.CurrentHp - loss);
        }

        static string BuildSummary(AutoBattleReport report, bool playerWon, bool executeOnWin)
        {
            var sb = new StringBuilder(128);
            if (playerWon)
            {
                sb.Append("自动战斗胜利。");
                if (executeOnWin)
                    sb.Append(" 敌军全灭（" + report.EnemyMembersEliminated + " 人）。");
                else if (report.EnemyMembersSpared > 0)
                    sb.Append(" 敌军 " + report.EnemyMembersSpared + " 人全部弥留（未处决）。");
                else
                    sb.Append(" 敌军已溃。");
            }
            else
            {
                sb.Append("自动战斗失利。");
                if (report.PlayerKilled > 0)
                    sb.Append(" 我方 " + report.PlayerKilled + " 人阵亡。");
                if (report.PlayerIncapacitated > 0)
                    sb.Append(" " + report.PlayerIncapacitated + " 人弥留。");
                if (report.PlayerWounded > 0)
                    sb.Append(" " + report.PlayerWounded + " 人负伤。");
            }

            if (report.PlayerWounded > 0 && playerWon)
                sb.Append(" 我方 " + report.PlayerWounded + " 人负伤。");

            return sb.ToString().Trim();
        }
    }
}
