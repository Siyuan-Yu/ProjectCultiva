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

        /// <summary>
        /// 调试：接战名单恰好 1 人时，自动战强制该人进弥留（胜／负都生效）。
        /// 大地图工具栏可勾选；默认关。
        /// </summary>
        public static bool DebugForceSoloAutoBattleIncapacitated = false;

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
            var debugForced = TryDebugForceSoloIncapacitated(world, party, report);

            if (ArmyStackAdapter.TryGetFormalArmy(world, enemyStack, out var formalArmy))
            {
                ApplyFormalArmyEnemyOutcome(world, formalArmy, enemyStack, executeOnWin, report);
            }
            else
            {
                ApplyLegacyStackEnemyOutcome(world, enemyStack, executeOnWin, report);
            }

            ArmyStackAdapter.SyncDownedCountsFromMembers(world, enemyStack);
            report.Summary = BuildSummary(report, playerWon: true, executeOnWin, debugForced);
            return report;
        }

        static void ApplyLegacyStackEnemyOutcome(
            SimulationWorld world,
            ArmyStack enemyStack,
            bool executeOnWin,
            AutoBattleReport report)
        {
            var members = Math.Max(1, enemyStack.MemberCount);
            if (executeOnWin)
            {
                report.EnemyMembersEliminated = members;
                report.EnemyMembersSpared = 0;
                enemyStack.MemberCount = members;
                enemyStack.IncapacitatedMemberCount = 0;
                enemyStack.CorpseMemberCount = members;
                enemyStack.IsBattlefieldRemnant = true;
                StrategicEncounterSpawner.ApplyCorpseToLivingTrackedSpawns(world);
            }
            else
            {
                report.EnemyMembersEliminated = 0;
                report.EnemyMembersSpared = members;
                enemyStack.MemberCount = members;
                enemyStack.IncapacitatedMemberCount = members;
                enemyStack.CorpseMemberCount = 0;
                enemyStack.IsBattlefieldRemnant = true;
                enemyStack.CombatPower = Math.Max(1, enemyStack.CombatPower);
                StrategicEncounterSpawner.ApplyIncapacitatedToLivingTrackedSpawns(world);
            }
        }

        static void ApplyFormalArmyEnemyOutcome(
            SimulationWorld world,
            FormalArmy formalArmy,
            ArmyStack enemyStack,
            bool executeOnWin,
            AutoBattleReport report)
        {
            var eliminated = 0;
            var spared = 0;
            for (var i = 0; i < formalArmy.MemberCharacterIds.Count; i++)
            {
                var id = new EntityId(formalArmy.MemberCharacterIds[i]);
                if (id.IsNone || !world.Entities.TryGet(id, out var entity) || entity == null)
                    continue;
                if (!CombatLifeStateService.CanFight(entity) &&
                    !CombatLifeStateService.CanBeAttacked(entity))
                    continue;

                if (executeOnWin)
                {
                    CombatDamageRules.EnsureVitals(entity);
                    if (entity.TryGet<CombatVitalsComponent>(out var vitals))
                        vitals.CurrentHp = 0;
                    if (CombatLifeStateService.TryConfirmDeath(world, EntityId.None, entity, out _))
                        eliminated++;
                }
                else if (CombatLifeStateService.TryEnterIncapacitated(world, entity))
                {
                    spared++;
                }
            }

            report.EnemyMembersEliminated = eliminated;
            report.EnemyMembersSpared = spared;
            enemyStack.IsBattlefieldRemnant = true;
            if (executeOnWin)
                StrategicEncounterSpawner.ApplyCorpseToLivingTrackedSpawns(world);
            else
                StrategicEncounterSpawner.ApplyIncapacitatedToLivingTrackedSpawns(world);
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

            if (TryDebugForceSoloIncapacitated(world, party, report))
            {
                report.Summary = BuildSummary(report, playerWon: false, executeOnWin: false, debugForcedSoloIncap: true);
                return report;
            }

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
                    if (ApplyDirectKill(world, entity))
                        report.PlayerKilled++;
                }
                else if (roll < killChance + 0.35)
                {
                    if (ApplyIncapacitated(world, entity))
                        report.PlayerIncapacitated++;
                }
                else
                {
                    ApplyWound(world, entity, intensity);
                    report.PlayerWounded++;
                }
            }

            report.Summary = BuildSummary(report, playerWon: false, executeOnWin: false, debugForcedSoloIncap: false);
            return report;
        }

        /// <summary>名单恰好 1 人且调试开：强制进弥留。返回是否生效。</summary>
        static bool TryDebugForceSoloIncapacitated(
            SimulationWorld world,
            IReadOnlyList<EntityId> party,
            AutoBattleReport report)
        {
            if (!DebugForceSoloAutoBattleIncapacitated ||
                world == null ||
                party == null ||
                party.Count != 1 ||
                report == null)
                return false;

            var id = party[0];
            if (id.IsNone || !world.Entities.TryGet(id, out var entity) || entity == null)
                return false;

            if (!CombatLifeStateService.CanFight(entity))
            {
                // 已是弥留：仍算调试命中，方便摘要一致
                if (!LingeringBattlefieldPartyService.IsIncapacitated(world, id))
                    return false;
                report.PlayerKilled = 0;
                report.PlayerWounded = 0;
                report.PlayerIncapacitated = 1;
                return true;
            }

            ApplyIncapacitated(world, entity);
            report.PlayerKilled = 0;
            report.PlayerWounded = 0;
            report.PlayerIncapacitated = 1;
            return true;
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

        static bool ApplyDirectKill(SimulationWorld world, Entity entity)
        {
            if (entity == null)
                return false;
            CombatDamageRules.EnsureVitals(entity);
            if (entity.TryGet<CombatVitalsComponent>(out var vitals))
                vitals.CurrentHp = 0;
            // 直接阵亡（跳过弥留）；TryConfirmDeath 接受 Alive+0 血
            return CombatLifeStateService.TryConfirmDeath(
                world,
                EntityId.None,
                entity,
                out var confirmed) &&
                confirmed;
        }

        static bool ApplyIncapacitated(SimulationWorld world, Entity entity)
        {
            if (entity == null)
                return false;
            CombatDamageRules.EnsureVitals(entity);
            if (entity.TryGet<CombatVitalsComponent>(out var vitals))
                vitals.CurrentHp = 0;
            return CombatLifeStateService.TryEnterIncapacitated(world, entity);
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

        static string BuildSummary(
            AutoBattleReport report,
            bool playerWon,
            bool executeOnWin,
            bool debugForcedSoloIncap)
        {
            var sb = new StringBuilder(128);
            if (playerWon)
            {
                sb.Append("自动战斗胜利。");
                if (executeOnWin)
                    sb.Append(" 敌军 " + report.EnemyMembersEliminated + " 人阵亡（尸体留场）。");
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

            if (debugForcedSoloIncap)
                sb.Append(" 【调试：单人强制弥留】");

            return sb.ToString().Trim();
        }
    }
}
