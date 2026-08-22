using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
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

        /// <summary>
        /// 探望弥留到站：若已有 PendingLingeringVisit，且至少一名活人进入支援半径，则弹接战窗。
        /// 半径内弥留仍由 ActivateLingeringOffer／Promote 强制纳入。
        /// </summary>
        public static bool TryResolvePendingLingeringVisitOffer(
            SimulationWorld world,
            IReadOnlyList<EntityId> roster)
        {
            if (world?.Strategic == null || roster == null)
                return false;
            var pending = world.Strategic.PendingLingeringVisitIncapId;
            if (pending == 0)
                return false;

            var focus = new EntityId(pending);
            if (!HasLingeringBattlefield(world) ||
                !LingeringBattlefieldPartyService.IsLingeringDowned(world, focus))
            {
                world.Strategic.ClearPendingLingeringVisit();
                return false;
            }

            if (!LingeringBattlefieldPartyService.TryResolveBattleAnchor(
                    world, focus, out var anchorNode, out var anchorRoute, out var anchorProgress))
                return false;

            var anyLivingNear = false;
            for (var i = 0; i < roster.Count; i++)
            {
                var id = roster[i];
                if (id.IsNone || !LingeringBattlefieldPartyService.IsLivingForMacroOrder(world, id))
                    continue;
                if (!world.WorldPresence.TryGet(id, out var wp) || wp == null)
                    continue;
                if (wp.Mode == PartyWorldPresenceMode.Traveling)
                    continue;
                if (!ReinforcementRangeService.IsWithinReinforcementRange(
                        world, wp, anchorNode, anchorRoute, anchorProgress))
                    continue;
                anyLivingNear = true;
                break;
            }

            if (!anyLivingNear)
                return false;

            var visitParty = CollectPendingLingeringVisitParty(world);
            return TryBuildOfferForLingeringBattlefield(
                world, roster, focus, "残留战场", visitParty);
        }

        /// <summary>残留战场再入：弹接战窗（我方弥留头像菜单／敌方残留栈再攻）。</summary>
        public static bool TryBuildOfferForLingeringBattlefield(
            SimulationWorld world,
            IReadOnlyList<EntityId> roster,
            EntityId focusIncap,
            string title = null,
            IReadOnlyList<EntityId> mandatoryLiving = null)
        {
            if (world?.Strategic?.Encounter == null || !HasLingeringBattlefield(world))
                return false;
            if (roster == null)
                return false;

            var party = new List<EntityId>(roster.Count);
            if (!LingeringBattlefieldPartyService.CanEnterLingeringBattlefield(
                    world,
                    roster,
                    focusIncap,
                    party,
                    mandatoryLiving) ||
                party.Count == 0)
                return false;

            var rt = world.Strategic.Encounter;
            var stackId = rt.ArmyStackId ?? string.Empty;
            if (string.IsNullOrEmpty(stackId))
                stackId = world.Strategic.Participants?.PrimaryEnemyStackId ?? string.Empty;

            ArmyStack enemy = null;
            if (!string.IsNullOrEmpty(stackId))
                world.Strategic.Armies.TryGet(stackId, out enemy);

            var offerTitle = string.IsNullOrEmpty(title) ? "残留战场" : title;

            if (world.Strategic.HasBattleOffer ||
                world.Strategic.IsModalEncounter ||
                world.Strategic.ClockFreeze.Reason == StrategicClockFreezeReason.InterruptQueue)
            {
                world.Strategic.InterruptQueue.Enqueue(
                    offerTitle,
                    stackId,
                    party,
                    world.Tick.Value * 1000UL + (ulong)world.Strategic.InterruptQueue.Count + 1UL);
                StrategicClockFreezeService.BeginOrPromote(
                    world, StrategicClockFreezeReason.BattleOffer);
                world.Strategic.ClearPendingLingeringVisit();
                return true;
            }

            if (!string.IsNullOrEmpty(stackId) && HasActiveEncounterForStack(world, stackId))
            {
                world.Strategic.InterruptQueue.Enqueue(
                    offerTitle,
                    stackId,
                    party,
                    world.Tick.Value * 1000UL + (ulong)world.Strategic.InterruptQueue.Count + 1UL);
                world.Strategic.ClearPendingLingeringVisit();
                return true;
            }

            world.Strategic.ClearArrivalNotice();
            world.Strategic.ClearPendingLingeringVisit();
            return ActivateLingeringOffer(world, party, enemy, stackId, offerTitle);
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
            offer.Title = ResolveOfferTitle(world, enemy, title);
            offer.EncounterLocalMapId = ResolveOfferEncounterLocalMapId(world, enemy);
            offer.SetPlayerParty(playerParty);
            offer.ExecuteOnWin = false;

            var snap = BattleParticipantSnapshotBuilder.Build(
                world, playerParty, enemy, offer.OfferId);
            snap.EncounterLocalMapId = offer.EncounterLocalMapId;
            world.Strategic.Participants.Clear();
            CopySnapshotInto(world.Strategic.Participants, snap);

            // 与残留再进同一原则：接战锚点半径内我方弥留 = 已在场上，强制参战（追击／再攻／首战皆同）
            PromoteInRangeIncapacitatedToMandatory(world, world.Strategic.Participants);
            var selected = world.Strategic.Participants.CollectSelectedFriendly();
            offer.SetPlayerParty(selected);
            ArrivalNoticeService.SuppressForParty(world, selected);

            RefreshOfferPowerLabels(world);
            StrategicClockFreezeService.BeginOrPromote(world, StrategicClockFreezeReason.BattleOffer);
            return true;
        }

        static bool ActivateLingeringOffer(
            SimulationWorld world,
            IReadOnlyList<EntityId> playerParty,
            ArmyStack enemy,
            string armyStackId,
            string title)
        {
            var offer = world.Strategic.BattleOffer;
            offer.Resolved = false;
            offer.OfferId = "linger-offer:" + (armyStackId ?? string.Empty) + ":" + world.Tick.Value;
            offer.ArmyStackId = armyStackId ?? string.Empty;
            offer.Title = string.IsNullOrEmpty(title) ? "残留战场" : title;
            offer.EncounterLocalMapId = ResolveActiveEncounterLocalMapId(world);
            offer.SetPlayerParty(playerParty);
            offer.ExecuteOnWin = false;

            var snap = world.Strategic.Participants;
            if (enemy != null)
            {
                var built = BattleParticipantSnapshotBuilder.Build(
                    world, playerParty, enemy, offer.OfferId);
                built.EncounterLocalMapId = offer.EncounterLocalMapId;
                CopySnapshotInto(snap, built);
            }
            else
            {
                var anchorNode = string.Empty;
                var anchorRoute = string.Empty;
                var anchorDest = string.Empty;
                var anchorProgress = -1f;
                if (snap != null &&
                    (!string.IsNullOrEmpty(snap.BattleAnchorNodeId) ||
                     !string.IsNullOrEmpty(snap.BattleAnchorRouteId)))
                {
                    anchorNode = snap.BattleAnchorNodeId ?? string.Empty;
                    anchorRoute = snap.BattleAnchorRouteId ?? string.Empty;
                    anchorDest = snap.BattleAnchorDestNodeId ?? string.Empty;
                    anchorProgress = snap.BattleAnchorProgress;
                }
                else if (playerParty.Count > 0 &&
                         LingeringBattlefieldPartyService.TryResolveBattleAnchor(
                             world,
                             playerParty[0],
                             out var node,
                             out var route,
                             out var progress))
                {
                    anchorNode = node;
                    anchorRoute = route;
                    anchorProgress = progress;
                }

                snap.Clear();
                snap.OfferId = offer.OfferId;
                snap.PrimaryEnemyStackId = armyStackId ?? string.Empty;
                snap.EncounterLocalMapId = offer.EncounterLocalMapId;
                snap.BattleAnchorNodeId = anchorNode;
                snap.BattleAnchorRouteId = anchorRoute;
                snap.BattleAnchorDestNodeId = anchorDest;
                snap.BattleAnchorProgress = anchorProgress;
                AddMandatoryPartyRecords(world, snap, playerParty);
            }

            // 残留再进：半径内我方弥留一律强制参战、不可勾掉
            PromoteInRangeIncapacitatedToMandatory(world, snap);
            var selected = snap.CollectSelectedFriendly();
            offer.SetPlayerParty(selected);
            ArrivalNoticeService.SuppressForParty(world, selected);

            RefreshOfferPowerLabels(world);
            StrategicClockFreezeService.BeginOrPromote(world, StrategicClockFreezeReason.BattleOffer);
            return true;
        }

        /// <summary>
        /// 支援半径内我方弥留 → MandatoryFriendly。
        /// 入口无关：追击接战／残留再进／再攻残留栈共用——人已在接战点，不是可选编队。
        /// </summary>
        public static void PromoteInRangeIncapacitatedToMandatory(
            SimulationWorld world,
            BattleParticipantSnapshot snap)
        {
            if (world?.WorldPresence?.All == null || snap == null)
                return;

            for (var i = snap.Records.Count - 1; i >= 0; i--)
            {
                var rec = snap.Records[i];
                if (rec.EntityId.IsNone)
                    continue;
                if (!LingeringBattlefieldPartyService.IsLingeringDowned(world, rec.EntityId))
                    continue;
                if (rec.Kind == BattleParticipantKind.OptionalFriendly)
                {
                    rec.Kind = BattleParticipantKind.MandatoryFriendly;
                    rec.Selected = true;
                    continue;
                }

                if (rec.Kind == BattleParticipantKind.MandatoryFriendly)
                    rec.Selected = true;
            }

            foreach (var kv in world.WorldPresence.All)
            {
                var id = new EntityId(kv.Key);
                var wp = kv.Value;
                if (wp == null || id.IsNone)
                    continue;
                if (!LingeringBattlefieldPartyService.IsLingeringDowned(world, id))
                    continue;
                if (!world.Entities.TryGet(id, out var ent) || ent == null)
                    continue;
                if ((ent.Tags & EntityTag.Npc) != 0)
                    continue;
                if (!ReinforcementRangeService.IsWithinReinforcementRange(
                        world,
                        wp,
                        snap.BattleAnchorNodeId,
                        snap.BattleAnchorRouteId,
                        snap.BattleAnchorProgress))
                    continue;
                if (snap.FindByEntity(id) != null)
                    continue;

                snap.Add(new BattleParticipantRecord
                {
                    Kind = BattleParticipantKind.MandatoryFriendly,
                    EntityId = id,
                    DisplayLabel = string.IsNullOrEmpty(ent.DisplayName) ? id.ToString() : ent.DisplayName,
                    CombatPower = CombatPowerCalculator.ForEntity(world, id),
                    Selected = true,
                    PreBattle = PreBattleWorldPresence.Capture(wp)
                });
            }
        }

        static string ResolveOfferTitle(SimulationWorld world, ArmyStack enemy, string title)
        {
            if (!string.IsNullOrEmpty(title))
                return title;
            if (IsLingeringReentryOffer(world, enemy))
                return "残留战场";
            return "遭遇敌军";
        }

        static string ResolveOfferEncounterLocalMapId(SimulationWorld world, ArmyStack enemy)
        {
            if (IsLingeringReentryOffer(world, enemy))
                return ResolveActiveEncounterLocalMapId(world);
            return StrategicEncounterCatalog.DefaultEncounterLocalMapId;
        }

        static bool IsLingeringReentryOffer(SimulationWorld world, ArmyStack enemy) =>
            HasLingeringBattlefield(world) ||
            (enemy != null && enemy.HasDownedRemnant);

        static void AddMandatoryPartyRecords(
            SimulationWorld world,
            BattleParticipantSnapshot snap,
            IReadOnlyList<EntityId> party)
        {
            if (world == null || snap == null || party == null)
                return;
            for (var i = 0; i < party.Count; i++)
            {
                var id = party[i];
                if (id.IsNone || snap.FindByEntity(id) != null)
                    continue;
                if (!world.Entities.TryGet(id, out var ent) || ent == null)
                    continue;
                world.WorldPresence.TryGet(id, out var wp);
                snap.Add(new BattleParticipantRecord
                {
                    Kind = BattleParticipantKind.MandatoryFriendly,
                    EntityId = id,
                    DisplayLabel = string.IsNullOrEmpty(ent.DisplayName) ? id.ToString() : ent.DisplayName,
                    CombatPower = CombatPowerCalculator.ForEntity(world, id),
                    Selected = true,
                    PreBattle = wp != null ? PreBattleWorldPresence.Capture(wp) : default
                });
            }
        }

        static List<EntityId> CollectPendingLingeringVisitParty(SimulationWorld world)
        {
            var list = new List<EntityId>(4);
            var ids = world?.Strategic?.PendingLingeringVisitPartyIds;
            if (ids == null)
                return list;
            for (var i = 0; i < ids.Count; i++)
            {
                var id = new EntityId(ids[i]);
                if (!id.IsNone)
                    list.Add(id);
            }

            return list;
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
            // 弥留不可从参战名单勾掉
            if (LingeringBattlefieldPartyService.IsLingeringDowned(world, id))
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
            // 闲置残留（大地图上、无人进场）：Park 后无 EngagedParty → false，表现层不锁回遭遇图
            // 再进后已 SetEngagedParty：即使 BattlefieldLingering 仍为 true，也算主动遭遇
            // （否则 ApplyParty 不落表现，我方弥留在 LocalMap 隐身）
            if (!rt.HasEngagedParty)
                return false;
            if (rt.SpawnOnNextMapLoad)
                return true;
            // 再进战场：场上可能只剩弥留刷怪（无 Alive），仍算遭遇进行中
            if (rt.SpawnedEntityIds.Count > 0)
                return true;
            if (StrategicEncounterSpawner.CountLivingTrackedSpawns(world) > 0)
                return true;
            // 仅我方弥留进场、敌尚未刷出：仍算遭遇中
            return true;
        }

        public static bool HasLingeringBattlefield(SimulationWorld world)
        {
            if (world?.Strategic?.Encounter == null)
                return false;
            if (world.Strategic.Encounter.BattlefieldLingering)
                return true;
            // 进图时曾误清标志：只要场上仍有弥留／残留栈，仍视为可再进
            return StrategicEncounterResolveService.HasLingeringBattlefieldRemnants(world);
        }

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
                StrategicPursuitService.ClearPursuitForEngagedKeepEnRoute(world, party);
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

            BindEncounterAfterAutoResolve(
                world,
                world.Strategic.Participants,
                offer,
                party,
                playerWon,
                executeOnWin);

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

        static void BindEncounterAfterAutoResolve(
            SimulationWorld world,
            BattleParticipantSnapshot snap,
            BattleOfferPending offer,
            IReadOnlyList<EntityId> party,
            bool playerWon,
            bool executeOnWin)
        {
            if (world?.Strategic?.Encounter == null || snap == null || offer == null)
                return;

            var rt = world.Strategic.Encounter;
            var stackId = !string.IsNullOrEmpty(offer.ArmyStackId)
                ? offer.ArmyStackId
                : snap.PrimaryEnemyStackId ?? string.Empty;
            if (!string.IsNullOrEmpty(stackId))
                rt.ArmyStackId = stackId;
            if (!string.IsNullOrEmpty(offer.OfferId))
                rt.EncounterLinkId = offer.OfferId;
            if (!string.IsNullOrEmpty(snap.EncounterLocalMapId))
                rt.LingeringLocalMapId = snap.EncounterLocalMapId;
            else if (string.IsNullOrEmpty(rt.LingeringLocalMapId))
                rt.LingeringLocalMapId = StrategicEncounterCatalog.DefaultEncounterLocalMapId;

            if (party != null && party.Count > 0)
                rt.SetEngagedParty(party);

            // 自动战胜：立刻把残留栈钉到接战点，大地图结算弹窗期间就能看见
            if (playerWon)
                StrategicEncounterResolveService.ParkPrimaryEnemyStackAtBattleAnchor(world, snap);

            // 自动战未进 LocalMap：立刻刷弥留／尸体实体 + 接战点 WorldPresence（与进图再出一致）
            if (playerWon &&
                !string.IsNullOrEmpty(stackId) &&
                world.Strategic.Armies.TryGet(stackId, out var stack) &&
                stack != null &&
                stack.HasDownedRemnant)
                StrategicEncounterSpawner.EnsureMacroRemnantSpawns(world, snap);
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
                {
                    var members = Math.Max(1, st.MemberCount);
                    st.MemberCount = members;
                    st.IncapacitatedMemberCount = 0;
                    st.CorpseMemberCount = members;
                    st.IsBattlefieldRemnant = true;
                }
                else
                {
                    // 与主栈一致：未处决 → 全员弥留残留
                    var members = Math.Max(1, st.MemberCount);
                    st.MemberCount = members;
                    st.IncapacitatedMemberCount = members;
                    st.CorpseMemberCount = 0;
                    st.IsBattlefieldRemnant = true;
                    st.CombatPower = Math.Max(1, st.CombatPower);
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
