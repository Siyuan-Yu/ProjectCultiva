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

            // 已有 Offer／Modal／Queue 头正在展示 → 入队，不丢
            if (world.Strategic.HasBattleOffer ||
                world.Strategic.IsModalEncounter ||
                world.Strategic.ClockFreeze.Reason == StrategicClockFreezeReason.InterruptQueue)
            {
                world.Strategic.InterruptQueue.Enqueue(
                    title ?? "遭遇敌军",
                    enemy.Id,
                    playerParty,
                    world.Tick.Value * 1000UL + (ulong)world.Strategic.InterruptQueue.Count + 1UL);
                StrategicClockFreezeService.BeginOrPromote(
                    world, StrategicClockFreezeReason.BattleOffer);
                return true;
            }

            world.Strategic.ClearArrivalNotice();

            // 同栈 Modal 进行中：入队等待（手动战时间停止，不做战中动态加入）
            if (HasActiveEncounterForStack(world, enemy.Id))
            {
                world.Strategic.InterruptQueue.Enqueue(
                    title ?? "遭遇敌军",
                    enemy.Id,
                    playerParty,
                    world.Tick.Value * 1000UL + (ulong)world.Strategic.InterruptQueue.Count + 1UL);
                return true;
            }

            return ActivateOffer(world, playerParty, enemy, title);
        }

        static bool ActivateOffer(
            SimulationWorld world,
            IReadOnlyList<EntityId> playerParty,
            ArmyStack enemy,
            string title)
        {
            var offer = world.Strategic.BattleOffer;
            offer.Resolved = false;
            offer.OfferId = "offer:" + enemy.Id + ":" + world.Tick.Value + ":" +
                            world.Strategic.InterruptQueue.Count;
            offer.ArmyStackId = enemy.Id;
            offer.Title = string.IsNullOrEmpty(title) ? "遭遇敌军" : title;
            offer.EncounterLocalMapId = StrategicEncounterCatalog.DefaultEncounterLocalMapId;
            offer.SetPlayerParty(playerParty);
            offer.ExecuteOnWin = false;

            var snap = BattleParticipantSnapshotBuilder.Build(
                world, playerParty, enemy, offer.OfferId);
            world.Strategic.Participants.Clear();
            CopySnapshotInto(world.Strategic.Participants, snap);

            RefreshOfferPowerLabels(world);
            StrategicClockFreezeService.BeginOrPromote(world, StrategicClockFreezeReason.BattleOffer);
            return true;
        }

        static void CopySnapshotInto(BattleParticipantSnapshot dst, BattleParticipantSnapshot src)
        {
            if (dst == null || src == null)
                return;
            dst.Clear();
            dst.OfferId = src.OfferId;
            dst.BattleAnchorNodeId = src.BattleAnchorNodeId;
            dst.BattleAnchorDestNodeId = src.BattleAnchorDestNodeId;
            dst.BattleAnchorRouteId = src.BattleAnchorRouteId;
            dst.BattleAnchorProgress = src.BattleAnchorProgress;
            dst.PrimaryEnemyStackId = src.PrimaryEnemyStackId;
            dst.EncounterLocalMapId = src.EncounterLocalMapId;
            for (var i = 0; i < src.Records.Count; i++)
                dst.Add(src.Records[i]);
        }

        public static void RefreshOfferPowerLabels(SimulationWorld world)
        {
            if (world?.Strategic == null)
                return;
            var offer = world.Strategic.BattleOffer;
            var snap = world.Strategic.Participants;
            var friendlies = snap.CollectSelectedFriendly();
            offer.SetPlayerParty(friendlies);

            var playerPower = CombatPowerCalculator.SumPartyPower(world, friendlies);
            var enemyPower = 0;
            var enemyStacks = snap.CollectEnemyStackIds();
            for (var i = 0; i < enemyStacks.Count; i++)
            {
                if (world.Strategic.Armies.TryGet(enemyStacks[i], out var st) && st != null)
                    enemyPower += CombatPowerCalculator.ForArmyStack(st);
            }

            offer.PlayerPower = playerPower;
            offer.EnemyPower = enemyPower;
            offer.AutoWinPercent = CombatPowerCalculator.EstimateAutoWinPercent(playerPower, enemyPower);
            offer.PlayerLabel = "我方 " + friendlies.Count + " 人";
            offer.EnemyLabel = enemyStacks.Count <= 1
                ? (string.IsNullOrEmpty(offer.EnemyLabel) ? "敌军" : DescribePrimaryEnemy(world, offer.ArmyStackId))
                : "敌军 " + enemyStacks.Count + " 栈";
            if (enemyStacks.Count == 1)
                offer.EnemyLabel = DescribePrimaryEnemy(world, enemyStacks[0]);
        }

        static string DescribePrimaryEnemy(SimulationWorld world, string stackId)
        {
            if (string.IsNullOrEmpty(stackId) ||
                !world.Strategic.Armies.TryGet(stackId, out var enemy) ||
                enemy == null)
                return "敌军";
            return StrategicFactionCatalog.DisplayName(enemy.FactionId) + " · " +
                   (string.IsNullOrEmpty(enemy.DisplayName) ? enemy.Id : enemy.DisplayName);
        }

        public static bool SetOptionalSelected(
            SimulationWorld world,
            EntityId id,
            bool selected)
        {
            if (world?.Strategic == null || id.IsNone)
                return false;
            var rec = world.Strategic.Participants.FindByEntity(id);
            if (rec == null || rec.Kind != BattleParticipantKind.OptionalFriendly)
                return false;
            rec.Selected = selected;
            RefreshOfferPowerLabels(world);
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
            // 残留战场在大地图上：不算 Modal 进行中，禁止把表现层锁回 Encounter 图
            if (rt.BattlefieldLingering)
                return false;
            if (!rt.HasEngagedParty)
                return false;
            if (rt.SpawnOnNextMapLoad)
                return true;
            // 再进战场：场上可能只剩弥留刷怪（无 Alive），仍算遭遇进行中
            if (rt.SpawnedEntityIds.Count > 0)
                return true;
            return StrategicEncounterSpawner.CountLivingTrackedSpawns(world) > 0;
        }

        public static bool HasLingeringBattlefield(SimulationWorld world) =>
            world?.Strategic?.Encounter != null && world.Strategic.Encounter.BattlefieldLingering;

        public static string ResolveActiveEncounterLocalMapId(SimulationWorld world)
        {
            var rt = world?.Strategic?.Encounter;
            if (rt != null &&
                !string.IsNullOrEmpty(rt.LingeringLocalMapId))
                return rt.LingeringLocalMapId;
            return StrategicEncounterCatalog.DefaultEncounterLocalMapId;
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

            RefreshOfferPowerLabels(world);
            var party = world.Strategic.Participants.CollectSelectedFriendly();
            if (party.Count == 0)
                party = StrategicPursuitService.CollectEngagedPartyFromOffer(offer);

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

                // 敌方增援栈：胜则一并削弱／移除（处决时移除）
                ApplyEnemyReinforcementAutoOutcome(world, executeOnWin, playerWon: true);
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
            world.Strategic.Participants.LastBattleSummary = string.IsNullOrEmpty(offer.LastAutoBattleSummary)
                ? (playerWon ? "自动战斗胜利。" : "自动战斗失利。")
                : offer.LastAutoBattleSummary;
            world.Strategic.Participants.PlayerWon = playerWon;
            world.Strategic.Participants.IsAutoSettlement = true;

            // 先关 Offer，进入战后结算弹窗；确认后再 Finish／出队
            world.Strategic.ClearBattleOffer();
            StrategicClockFreezeService.BeginOrPromote(
                world, StrategicClockFreezeReason.PostBattle);
            return Result.Success();
        }

        static void ApplyEnemyReinforcementAutoOutcome(
            SimulationWorld world,
            bool executeOnWin,
            bool playerWon)
        {
            if (!playerWon)
                return;
            var stacks = world.Strategic.Participants.CollectEnemyStackIds();
            for (var i = 0; i < stacks.Count; i++)
            {
                if (string.Equals(
                        stacks[i],
                        world.Strategic.Participants.PrimaryEnemyStackId,
                        StringComparison.Ordinal))
                    continue;
                if (!world.Strategic.Armies.TryGet(stacks[i], out var st) || st == null)
                    continue;
                if (executeOnWin)
                    world.Strategic.Armies.Remove(st.Id);
                else
                {
                    st.MemberCount = Math.Max(1, st.MemberCount / 2);
                    st.CombatPower = Math.Max(1, st.CombatPower / 2);
                }
            }
        }

        /// <summary>Offer／遭遇结束后：先解冻，再出队下一场或清空快照。</summary>
        public static Result FinishOfferResolution(SimulationWorld world)
        {
            if (world?.Strategic == null)
                return Result.Failure(ErrorCode.InvalidArgument, "null world");

            // 必须先结束 Modal，否则 TryPromote 会被 IsModalEncounter 挡住
            StrategicClockFreezeService.EndFreeze(world);
            if (world.Strategic.Participants != null)
                world.Strategic.Participants.IsAutoSettlement = false;

            if (TryPromoteNextQueuedOffer(world))
                return Result.Success();

            world.Strategic.Participants.Clear();
            return Result.Success();
        }

        public static bool TryPromoteNextQueuedOffer(SimulationWorld world)
        {
            if (world?.Strategic == null)
                return false;
            if (world.Strategic.IsModalEncounter)
                return false;
            if (!world.Strategic.InterruptQueue.TryDequeue(out var queued) || queued == null)
                return false;
            if (!world.Strategic.Armies.TryGet(queued.ArmyStackId, out var enemy) || enemy == null)
                return TryPromoteNextQueuedOffer(world);

            var party = queued.ToPartyList();
            if (party.Count == 0)
                return TryPromoteNextQueuedOffer(world);

            var ready = new List<EntityId>(party.Count);
            StrategicEngageRules.CollectPartyReadyToEngageStack(world, party, enemy, ready);
            if (ready.Count == 0)
            {
                // 排队轮到但人未到：上路追击，到站后由 AfterTravelTick 弹接战（禁止远程瞬开 Offer）
                StrategicPursuitService.BeginPursuit(world, party, enemy);
                StrategicPursuitService.SyncPursuersToStack(world, party, enemy);
                return false;
            }

            StrategicClockFreezeService.BeginOrPromote(
                world, StrategicClockFreezeReason.BattleOffer);
            return ActivateOffer(world, ready, enemy, queued.Title);
        }
    }
}
