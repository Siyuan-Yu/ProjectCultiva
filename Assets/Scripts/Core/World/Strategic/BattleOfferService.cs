using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    public static class BattleOfferService
    {
        public static bool TryBuildOfferForArmy(
            SimulationWorld world,
            IReadOnlyList<EntityId> playerParty,
            ArmyStack enemy,
            string title = null)
        {
            if (world?.Strategic == null || enemy == null || playerParty == null || playerParty.Count == 0)
                return false;
            // 接战优先于到站提示
            if (world.Strategic.HasBattleOffer)
                return false;
            world.Strategic.ClearArrivalNotice();

            if (HasActiveEncounterForStack(world, enemy.Id))
                return TryBuildJoinOngoingOffer(world, playerParty, enemy, title);

            var playerPower = CombatPowerCalculator.SumPartyPower(world, playerParty);
            var enemyPower = CombatPowerCalculator.ForArmyStack(enemy);
            var offer = world.Strategic.BattleOffer;
            offer.Resolved = false;
            offer.IsJoinOngoingBattle = false;
            offer.OfferId = "offer:" + enemy.Id + ":" + world.Tick.Value;
            offer.ArmyStackId = enemy.Id;
            offer.Title = string.IsNullOrEmpty(title) ? "遭遇敌军" : title;
            offer.PlayerLabel = "我方 " + playerParty.Count + " 人";
            offer.EnemyLabel = StrategicFactionCatalog.DisplayName(enemy.FactionId) + " · " +
                               (string.IsNullOrEmpty(enemy.DisplayName) ? enemy.Id : enemy.DisplayName);
            offer.PlayerPower = playerPower;
            offer.EnemyPower = enemyPower;
            offer.AutoWinPercent = CombatPowerCalculator.EstimateAutoWinPercent(playerPower, enemyPower);
            offer.EncounterLocalMapId = StrategicEncounterCatalog.DefaultEncounterLocalMapId;
            offer.SetPlayerParty(playerParty);
            return true;
        }

        public static bool HasActiveEncounterForStack(SimulationWorld world, string armyStackId)
        {
            if (world?.Strategic?.Encounter == null || string.IsNullOrEmpty(armyStackId))
                return false;
            var rt = world.Strategic.Encounter;
            if (!string.Equals(rt.ArmyStackId, armyStackId, StringComparison.Ordinal))
                return false;
            return HasActiveManualEncounter(world);
        }

        public static bool HasActiveManualEncounter(SimulationWorld world)
        {
            if (world?.Strategic?.Encounter == null)
                return false;
            var rt = world.Strategic.Encounter;
            if (!rt.HasEngagedParty)
                return false;
            if (rt.SpawnOnNextMapLoad)
                return true;
            return StrategicEncounterSpawner.CountLivingTrackedSpawns(world) > 0;
        }

        public static string ResolveActiveEncounterLocalMapId(SimulationWorld world) =>
            StrategicEncounterCatalog.DefaultEncounterLocalMapId;

        static bool TryBuildJoinOngoingOffer(
            SimulationWorld world,
            IReadOnlyList<EntityId> playerParty,
            ArmyStack enemy,
            string title)
        {
            var newcomers = CollectNotYetEngaged(world, playerParty);
            if (newcomers.Count == 0)
                return false;

            var playerPower = CombatPowerCalculator.SumPartyPower(world, newcomers);
            var enemyPower = CombatPowerCalculator.ForArmyStack(enemy);
            var offer = world.Strategic.BattleOffer;
            offer.Resolved = false;
            offer.IsJoinOngoingBattle = true;
            offer.OfferId = "join:" + enemy.Id + ":" + world.Tick.Value;
            offer.ArmyStackId = enemy.Id;
            offer.Title = string.IsNullOrEmpty(title) ? "加入进行中的战斗" : title;
            offer.PlayerLabel = "增援 " + newcomers.Count + " 人";
            offer.EnemyLabel = StrategicFactionCatalog.DisplayName(enemy.FactionId) + " · " +
                               (string.IsNullOrEmpty(enemy.DisplayName) ? enemy.Id : enemy.DisplayName);
            offer.PlayerPower = playerPower;
            offer.EnemyPower = enemyPower;
            offer.AutoWinPercent = CombatPowerCalculator.EstimateAutoWinPercent(playerPower, enemyPower);
            offer.EncounterLocalMapId = StrategicEncounterCatalog.DefaultEncounterLocalMapId;
            offer.SetPlayerParty(newcomers);
            return true;
        }

        static List<EntityId> CollectNotYetEngaged(SimulationWorld world, IReadOnlyList<EntityId> party)
        {
            var list = new List<EntityId>(party?.Count ?? 0);
            if (world?.Strategic?.Encounter == null || party == null)
                return list;
            var rt = world.Strategic.Encounter;
            for (var i = 0; i < party.Count; i++)
            {
                if (party[i].IsNone || rt.IsEngaged(party[i]))
                    continue;
                list.Add(party[i]);
            }

            return list;
        }

        public static Result ResolveAuto(
            SimulationWorld world,
            bool executeOnWin,
            out bool playerWon,
            out AutoBattleReport report)
        {
            playerWon = false;
            report = null;
            if (world?.Strategic == null)
                return Result.Failure(ErrorCode.InvalidOperation, "No strategic board.");
            var offer = world.Strategic.BattleOffer;
            if (offer.Resolved || string.IsNullOrEmpty(offer.OfferId))
                return Result.Failure(ErrorCode.InvalidOperation, "No battle offer.");

            var party = StrategicPursuitService.CollectEngagedPartyFromOffer(offer);
            world.Strategic.Armies.TryGet(offer.ArmyStackId, out var enemyStack);

            var roll = world.Random.NextDouble();
            var winChance = offer.AutoWinPercent / 100.0;
            playerWon = roll <= winChance;
            offer.PlayerWonAuto = playerWon;
            offer.Resolved = true;

            if (playerWon)
            {
                report = enemyStack != null
                    ? AutoBattleCasualtyService.ApplyPlayerVictory(
                        world,
                        party,
                        enemyStack,
                        offer.PlayerPower,
                        offer.EnemyPower,
                        executeOnWin)
                    : new AutoBattleReport { Summary = "自动战斗胜利。" };
                StrategicPursuitService.ClearPursuit(world);
            }
            else
            {
                report = AutoBattleCasualtyService.ApplyPlayerDefeat(
                    world,
                    party,
                    offer.PlayerPower,
                    offer.EnemyPower);
                StrategicPursuitService.ClearPursuitForEngagedKeepEnRoute(world, party);
            }

            offer.LastAutoBattleSummary = report?.Summary ?? string.Empty;
            world.Strategic.ClearBattleOffer();
            return Result.Success();
        }
    }
}
