using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World;

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
            if (world.Strategic.HasBlockingInterrupt)
                return false;

            var playerPower = CombatPowerCalculator.SumPartyPower(world, playerParty);
            var enemyPower = CombatPowerCalculator.ForArmyStack(enemy);
            var offer = world.Strategic.BattleOffer;
            offer.Resolved = false;
            offer.OfferId = "offer:" + enemy.Id + ":" + world.Tick.Value;
            offer.ArmyStackId = enemy.Id;
            offer.Title = string.IsNullOrEmpty(title) ? "遭遇敌军" : title;
            offer.PlayerLabel = "我方 " + playerParty.Count + " 人";
            offer.EnemyLabel = StrategicFactionCatalog.DisplayName(enemy.FactionId) + " · " +
                               (string.IsNullOrEmpty(enemy.DisplayName) ? enemy.Id : enemy.DisplayName);
            offer.PlayerPower = playerPower;
            offer.EnemyPower = enemyPower;
            offer.AutoWinPercent = CombatPowerCalculator.EstimateAutoWinPercent(playerPower, enemyPower);
            offer.EncounterLocalMapId = RouteEncounterService.DefaultEncounterLocalMapId;
            return true;
        }

        public static Result ResolveAuto(SimulationWorld world)
        {
            if (world?.Strategic == null)
                return Result.Failure(ErrorCode.InvalidOperation, "No strategic board.");
            var offer = world.Strategic.BattleOffer;
            if (offer.Resolved || string.IsNullOrEmpty(offer.OfferId))
                return Result.Failure(ErrorCode.InvalidOperation, "No battle offer.");

            var roll = world.Random.NextDouble();
            var winChance = offer.AutoWinPercent / 100.0;
            offer.PlayerWonAuto = roll <= winChance;
            offer.Resolved = true;

            if (offer.PlayerWonAuto)
                world.Strategic.Armies.Remove(offer.ArmyStackId);
            else
            {
                // 败：栈仍在；Travel 由 Host 决定撤退或结束
            }

            world.Strategic.ClearBattleOffer();
            return Result.Success();
        }

        public static void CheckRouteCollisions(SimulationWorld world, IReadOnlyList<EntityId> playerParty)
        {
            if (world?.Strategic == null || playerParty == null || playerParty.Count == 0)
                return;
            if (world.Strategic.HasBlockingInterrupt)
                return;

            var playerFaction = world.Strategic.PlayerFactionId;
            foreach (var kv in world.WorldPresence.All)
            {
                var p = kv.Value;
                if (p == null || p.Mode != PartyWorldPresenceMode.Traveling || string.IsNullOrEmpty(p.RouteId))
                    continue;

                foreach (var stack in world.Strategic.Armies.AllOnRoute(p.RouteId))
                {
                    if (stack == null || string.IsNullOrEmpty(stack.FactionId))
                        continue;
                    if (!world.Strategic.Diplomacy.IsHostile(playerFaction, stack.FactionId))
                        continue;
                    if (TryBuildOfferForArmy(world, playerParty, stack, "行军遭遇"))
                        return;
                }
            }
        }
    }
}
