using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;

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
            if (!ValidateFormalAttackGate(world, playerParty, enemy, out _))
                return false;

            ArmyStackAdapter.TryResolveAttackerArmyId(world, playerParty, out var attackerArmyId);
            if (!string.IsNullOrEmpty(attackerArmyId))
                return TryBuildOfferForArmyVsArmy(world, attackerArmyId, playerParty, enemy, title);

            return TryBuildOfferInternal(world, playerParty, enemy, title);
        }

        public static bool TryBuildOfferForArmyVsArmy(
            SimulationWorld world,
            string attackerArmyId,
            IReadOnlyList<EntityId> playerParty,
            ArmyStack enemy,
            string title = null)
        {
            if (world?.Strategic == null || enemy == null || string.IsNullOrEmpty(attackerArmyId))
                return false;
            if (!ValidateFormalAttackGate(world, playerParty, enemy, out _))
                return false;

            return TryBuildOfferInternal(world, playerParty, enemy, title, attackerArmyId);
        }

        static bool ValidateFormalAttackGate(
            SimulationWorld world,
            IReadOnlyList<EntityId> playerParty,
            ArmyStack enemy,
            out GameError error)
        {
            error = default;
            if (world == null || enemy == null)
                return true;

            var attackerFaction = ResolveAttackerFaction(world, playerParty);
            if (string.IsNullOrEmpty(attackerFaction) || string.IsNullOrEmpty(enemy.FactionId))
                return true;
            if (string.Equals(attackerFaction, enemy.FactionId, StringComparison.Ordinal))
                return true;

            if (WarGateService.CanAttack(world, attackerFaction, enemy.FactionId))
                return true;

            error = new GameError(
                ErrorCode.InvalidOperation,
                "Formal military attack requires active war.",
                attackerFaction + "->" + enemy.FactionId);
            return false;
        }

        static string ResolveAttackerFaction(SimulationWorld world, IReadOnlyList<EntityId> party)
        {
            if (party == null || party.Count == 0)
                return world?.Strategic?.PlayerFactionId ?? string.Empty;
            if (ArmyStackAdapter.TryResolveAttackerArmyId(world, party, out var armyId) &&
                world.Strategic.FormalArmies.TryGet(armyId, out var army) &&
                army != null)
                return army.FactionId;
            return ArmyService.ResolveCharacterFactionId(world, party[0]);
        }

        static bool TryBuildOfferInternal(
            SimulationWorld world,
            IReadOnlyList<EntityId> playerParty,
            ArmyStack enemy,
            string title,
            string attackerArmyId = null)
        {
            if (world?.Strategic == null || enemy == null || playerParty == null || playerParty.Count == 0)
                return false;

            // 已有 Offer／Modal／Queue 头正在展�?�?入队，不�?
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

            return ActivateOffer(world, playerParty, enemy, title, attackerArmyId);
        }

        /// <summary>
        /// 探望弥留到站：若已有 PendingLingeringVisit，且至少一名活人进入支援半径，则弹接战窗�?
        /// 半径内弥留仍�?ActivateLingeringOffer／Promote 强制纳入�?
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
                !LingeringBattlefieldPartyService.IsFriendlyLingeringDowned(world, focus))
            {
                world.Strategic.ClearPendingLingeringVisit();
                return false;
            }

            if (!LingeringBattlefieldPartyService.TryResolveBattleAnchorHex(
                    world, focus, out var anchorHex))
                return false;

            var anyLivingNear = false;
            for (var i = 0; i < roster.Count; i++)
            {
                var id = roster[i];
                if (id.IsNone || !LingeringBattlefieldPartyService.IsLivingForMacroOrder(world, id))
                    continue;
                if (!world.WorldPresence.TryGet(id, out var wp) || wp == null)
                    continue;
                if (wp.Mode == PartyWorldPresenceMode.AtSite)
                    continue;
                if (!ReinforcementRangeService.IsWithinReinforcementRange(
                        world, wp, anchorHex))
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

        /// <summary>残留战场再入：我方弥留头像菜单／探望到站。敌方见 <see cref="TryBuildOfferForEnemyRemnantReentry"/>�?/summary>
        public static bool TryBuildOfferForLingeringBattlefield(
            SimulationWorld world,
            IReadOnlyList<EntityId> roster,
            EntityId focusIncap,
            string title = null,
            IReadOnlyList<EntityId> mandatoryLiving = null,
            HexCoord? lingeringHex = null)
        {
            if (world?.Strategic?.Encounter == null || !HasLingeringBattlefield(world))
                return false;
            if (roster == null)
                return false;
            if (!LingeringBattlefieldPartyService.IsFriendlyLingeringDowned(world, focusIncap))
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

            ArmyMacroPartyQueries.ExpandMandatoryLivingToFormalArmies(world, party);
            ArmyStackAdapter.TryResolveAttackerArmyId(world, party, out var attackerArmyId);

            var stackId = LingeringParticipantTrace.ResolveEnemyStackIdForLingeringHex(
                world, lingeringHex, focusIncap);

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
            return ActivateLingeringOffer(
                world, party, enemy, stackId, offerTitle, attackerArmyId, lingeringHex);
        }

        /// <summary>Hex：我方残留格右键直接进入（不要求先选军团）�?/summary>
        public static bool TryEnterFriendlyLingeringAtHex(
            SimulationWorld world,
            HexCoord hex,
            IReadOnlyList<EntityId> roster)
        {
            if (!LingeringBattlefieldQueryService.TryGetLingeringBattlefieldAtHex(world, hex, out var ctx) ||
                ctx.FriendlyFocusId.IsNone)
                return false;

            return TryBuildOfferForLingeringBattlefield(
                world,
                roster,
                ctx.FriendlyFocusId,
                "残留战场",
                mandatoryLiving: null,
                lingeringHex: hex);
        }

        /// <summary>Hex：已选军团攻击敌方残留战场（同格直接进，异格�?Hex 移动）�?/summary>
        public static bool TryAttackEnemyLingeringAtHex(
            SimulationWorld world,
            string attackerArmyId,
            HexCoord hex,
            out string statusHint)
        {
            statusHint = string.Empty;
            if (!LingeringBattlefieldQueryService.TryGetLingeringBattlefieldAtHex(world, hex, out var ctx) ||
                string.IsNullOrEmpty(ctx.EnemyStackId))
            {
                statusHint = "\u8be5\u683c\u65e0\u53ef\u653b\u51fb\u7684\u6b8b\u7559\u6218\u573a\u3002";
                return false;
            }

            var move = ArmyHexLingeringArrivalService.BeginMoveToAttackLingering(
                world, attackerArmyId, hex, ctx.EnemyStackId);
            if (move.IsFailure)
            {
                statusHint = move.Error.Message;
                return false;
            }

            if (world.Strategic.HasBattleOffer)
            {
                statusHint = "\u63a5\u6218\u5f39\u7a97\u5df2\u6253\u5f00";
                return true;
            }

            statusHint = "\u519b\u56e2\u5df2\u51fa\u53d1\uff0c\u62b5\u8fbe\u540e\u8fdb\u5165\u6b8b\u7559\u6218\u573a\u3002";
            return true;
        }

        /// <summary>
        /// 敌方弥留／尸体残留再入：已选我方活人军团在接战点附近时，直接弹接战窗（可选手动进入）�?
        /// </summary>
        public static bool TryBuildOfferForEnemyRemnantReentry(
            SimulationWorld world,
            IReadOnlyList<EntityId> livingParty,
            string enemyStackId,
            string title = null,
            HexCoord? lingeringHex = null)
        {
            if (world?.Strategic?.Encounter == null || !HasLingeringBattlefield(world))
                return false;
            if (livingParty == null || livingParty.Count == 0 || string.IsNullOrEmpty(enemyStackId))
                return false;
            if (!world.Strategic.Armies.TryGet(enemyStackId, out var enemy) || enemy == null)
                return false;
            if (!enemy.HasDownedRemnant && !enemy.IsBattlefieldRemnant)
                return false;

            var rt = world.Strategic.Encounter;

            var party = new List<EntityId>(livingParty.Count);
            for (var i = 0; i < livingParty.Count; i++)
            {
                var id = livingParty[i];
                if (id.IsNone || !LingeringBattlefieldPartyService.IsLivingForMacroOrder(world, id))
                    continue;
                party.Add(id);
            }

            if (party.Count == 0)
                return false;

            ArmyMacroPartyQueries.ExpandMandatoryLivingToFormalArmies(world, party);
            ArmyStackAdapter.TryResolveAttackerArmyId(world, party, out var attackerArmyId);
            var offerTitle = string.IsNullOrEmpty(title) ? "残留战场" : title;

            if (world.Strategic.HasBattleOffer ||
                world.Strategic.IsModalEncounter ||
                world.Strategic.ClockFreeze.Reason == StrategicClockFreezeReason.InterruptQueue)
            {
                world.Strategic.InterruptQueue.Enqueue(
                    offerTitle,
                    enemyStackId,
                    party,
                    world.Tick.Value * 1000UL + (ulong)world.Strategic.InterruptQueue.Count + 1UL);
                StrategicClockFreezeService.BeginOrPromote(
                    world, StrategicClockFreezeReason.BattleOffer);
                return true;
            }

            world.Strategic.ClearArrivalNotice();
            return ActivateLingeringOffer(
                world, party, enemy, enemyStackId, offerTitle, attackerArmyId, lingeringHex);
        }

        static bool ActivateOffer(
            SimulationWorld world,
            IReadOnlyList<EntityId> playerParty,
            ArmyStack enemy,
            string title,
            string attackerArmyId = null)
        {
            world.Strategic.Encounter.ClearActiveEncounterSession();
            var offer = world.Strategic.BattleOffer;
            offer.Resolved = false;
            offer.OfferId = "offer:" + enemy.Id + ":" + world.Tick.Value + ":" +
                            world.Strategic.InterruptQueue.Count;
            offer.ArmyStackId = enemy.Id;
            ArmyStackAdapter.TryResolveDefenderArmyId(enemy, out var defenderArmyId);
            offer.DefenderArmyId = defenderArmyId ?? string.Empty;
            if (string.IsNullOrEmpty(attackerArmyId))
                ArmyStackAdapter.TryResolveAttackerArmyId(world, playerParty, out attackerArmyId);
            offer.AttackerArmyId = attackerArmyId ?? string.Empty;
            offer.Title = ResolveOfferTitle(world, enemy, title);
            offer.EncounterLocalMapId = ResolveOfferEncounterLocalMapId(world, enemy);
            offer.SetPlayerParty(playerParty);
            offer.ExecuteOnWin = false;

            var snap = string.IsNullOrEmpty(offer.AttackerArmyId)
                ? BattleParticipantSnapshotBuilder.Build(
                    world, playerParty, enemy, offer.OfferId)
                : BattleParticipantSnapshotBuilder.BuildArmyVsArmy(
                    world, offer.AttackerArmyId, enemy, offer.OfferId);
            snap.EncounterLocalMapId = offer.EncounterLocalMapId;
            world.Strategic.Participants.Clear();
            CopySnapshotInto(world.Strategic.Participants, snap);

            // 禁止：旧残留战场 Hex 污染�?Active Enemy Encounter �?BattleAnchor�?
            // Canonical lingering 同步仅允�?ActivateLingeringOffer�?

            // 与残留再进同一原则：接战锚点半径内我方弥留 = 已在场上，强制参战（追击／再攻／首战皆同�?
            PromoteInRangeIncapacitatedToMandatory(world, world.Strategic.Participants);
            var selected = world.Strategic.Participants.CollectSelectedFriendly();
            offer.SetPlayerParty(selected);
            ArrivalNoticeService.SuppressForParty(world, selected);

            RefreshOfferPowerLabels(world);
            StrategicClockFreezeService.BeginOrPromote(world, StrategicClockFreezeReason.BattleOffer);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            FormalArmy atkArmy = null;
            if (!string.IsNullOrEmpty(offer.AttackerArmyId))
                world.Strategic.FormalArmies.TryGet(offer.AttackerArmyId, out atkArmy);
            SecondBattleAnchorTrace.EmitArmyHex("BattleOffer.ActivateOffer", world, atkArmy);
            EncounterAssemblyTrace.Emit(world, enemy, "ActivateOffer");
#endif
            return true;
        }

        static bool ActivateLingeringOffer(
            SimulationWorld world,
            IReadOnlyList<EntityId> playerParty,
            ArmyStack enemy,
            string armyStackId,
            string title,
            string attackerArmyId = null,
            HexCoord? lingeringHex = null)
        {
            // 禁止沿用上一�?LocalMap �?ActiveBattlefieldId，否�?EnsureMacroRemnantSpawns / Prune
            // 会写�?/ 清掉错误 battlefield �?SpawnedEntityIds�?
            world.Strategic.Encounter.ActiveBattlefieldId = string.Empty;

            var offer = world.Strategic.BattleOffer;
            offer.Resolved = false;
            offer.OfferId = "linger-offer:" + (armyStackId ?? string.Empty) + ":" + world.Tick.Value;
            offer.ArmyStackId = armyStackId ?? string.Empty;
            offer.Title = string.IsNullOrEmpty(title) ? "残留战场" : title;
            offer.EncounterLocalMapId = ResolveActiveEncounterLocalMapId(world);
            offer.SetPlayerParty(playerParty);
            offer.ExecuteOnWin = false;
            if (string.IsNullOrEmpty(attackerArmyId))
                ArmyStackAdapter.TryResolveAttackerArmyId(world, playerParty, out attackerArmyId);
            offer.AttackerArmyId = attackerArmyId ?? string.Empty;

            var snap = world.Strategic.Participants;
            if (LingeringParticipantTrace.TryResolveBattlefield(
                    world, lingeringHex, playerParty.Count > 0 ? playerParty[0] : EntityId.None,
                    out var storedBattlefield, out _))
            {
                LingeringBattlefieldParticipantService.ApplyStoredBattlefieldToOfferSnapshot(
                    world,
                    storedBattlefield,
                    snap,
                    playerParty,
                    offer.OfferId,
                    offer.AttackerArmyId);
            }
            else if (enemy != null)
            {
                BattleParticipantSnapshot built;
                if (!string.IsNullOrEmpty(attackerArmyId))
                {
                    built = BattleParticipantSnapshotBuilder.BuildArmyVsArmy(
                        world,
                        attackerArmyId,
                        enemy,
                        offer.OfferId);
                }
                else
                {
                    built = BattleParticipantSnapshotBuilder.Build(
                        world, playerParty, enemy, offer.OfferId);
                }

                built.EncounterLocalMapId = offer.EncounterLocalMapId;
                CopySnapshotInto(snap, built);
            }
            else
            {
                snap.Clear();
                snap.OfferId = offer.OfferId;
                snap.PrimaryEnemyStackId = armyStackId ?? string.Empty;
                snap.EncounterLocalMapId = offer.EncounterLocalMapId;
                if (!string.IsNullOrEmpty(attackerArmyId) &&
                    world.Strategic.FormalArmies.TryGet(attackerArmyId, out var atkArmy) &&
                    atkArmy != null &&
                    atkArmy.UsesHexStrategicPosition)
                {
                    ArmyHexBattleAnchorService.ApplyFormalArmyBattleAnchor(world, snap, atkArmy);
                }
                else if (enemy != null)
                {
                    ArmyHexBattleAnchorService.ApplyStackBattleAnchor(world, snap, enemy);
                }

                AddMandatoryPartyRecords(world, snap, playerParty);
                if (!string.IsNullOrEmpty(attackerArmyId))
                    snap.AttackerArmyId = attackerArmyId;
                BattleParticipantSnapshotBuilder.CollectOptionalFormalArmiesForOffer(
                    world, snap, snap.AttackerArmyId, playerParty);
            }

            // 残留再进：半径内我方弥留一律强制参战、不可勾�?
            PromoteInRangeIncapacitatedToMandatory(world, snap);
            // 必须用本场残�?Hex（如 H1），禁止被最新场 H2 顶掉
            StrategicEncounterResolveService.TryApplyCanonicalLingeringBattleAnchor(
                world, snap, lingeringHex);

            if (LingeringParticipantTrace.TryResolveBattlefield(
                    world, lingeringHex, playerParty.Count > 0 ? playerParty[0] : EntityId.None,
                    out var traceBattlefield, out _))
            {
                var traceIds = new List<EntityId>(8);
                snap.CollectEnemyEntityIds(traceIds);
                LingeringParticipantTrace.Emit(
                    world,
                    lingeringHex,
                    traceBattlefield,
                    traceIds,
                    "ActivateLingeringOffer");
            }

            var selected = snap.CollectSelectedFriendly();
            offer.SetPlayerParty(selected);
            ArrivalNoticeService.SuppressForParty(world, selected);

            if (lingeringHex.HasValue &&
                world.Strategic.LingeringBattlefields.TryGetAtHex(lingeringHex.Value, out var battlefield) &&
                battlefield != null)
                world.Strategic.Encounter.PendingLingeringEnterBattlefieldId = battlefield.BattlefieldId;

            RefreshOfferPowerLabels(world);
            StrategicClockFreezeService.BeginOrPromote(world, StrategicClockFreezeReason.BattleOffer);
            return true;
        }

        /// <summary>
        /// 支援半径内我方弥�?�?MandatoryFriendly�?
        /// 入口无关：追击接战／残留再进／再攻残留栈共用——人已在接战点，不是可选编队�?
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
                if (!ArmyHexBattleAnchorService.TryGetBattleAnchorHex(snap, out var anchorHex))
                    continue;
                if (!ReinforcementRangeService.IsWithinReinforcementRange(
                        world,
                        wp,
                        anchorHex))
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
            enemy != null && (enemy.HasDownedRemnant || enemy.IsBattlefieldRemnant);

        public static void AddMandatoryPartyRecordsForLingering(
            SimulationWorld world,
            BattleParticipantSnapshot snap,
            IReadOnlyList<EntityId> party) =>
            AddMandatoryPartyRecords(world, snap, party);

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
                ArmyService.TryGetArmyForCharacter(world, id, out var army);
                snap.Add(new BattleParticipantRecord
                {
                    Kind = BattleParticipantKind.MandatoryFriendly,
                    EntityId = id,
                    FormalArmyId = army?.ArmyId ?? string.Empty,
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
            dst.BattleAnchorHexQ = src.BattleAnchorHexQ;
            dst.BattleAnchorHexR = src.BattleAnchorHexR;
            dst.PrimaryEnemyStackId = src.PrimaryEnemyStackId;
            dst.AttackerArmyId = src.AttackerArmyId;
            dst.DefenderArmyId = src.DefenderArmyId;
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
                    enemyPower += CombatPowerCalculator.ForArmyStack(world, st);
            }

            offer.PlayerPower = playerPower;
            offer.EnemyPower = enemyPower;
            offer.AutoWinPercent = CombatPowerCalculator.EstimateAutoWinPercent(playerPower, enemyPower);
            // 试炼弱匪：战力刻度被凡人压平后仍可能「掷骰翻车」；测试夹具直接视为必胜�?
            if (enemyStacks.Count == 1 &&
                ArmyStackAdapter.IsTrivialTestEnemyStack(enemyStacks[0]))
                offer.AutoWinPercent = 99;
            else if (enemyStacks.Count == 1 &&
                     ArmyStackAdapter.IsCasualtyTestEnemyStack(enemyStacks[0]))
                offer.AutoWinPercent = 95;
            offer.PlayerLabel = "\u6211\u65b9 " + friendlies.Count + " \u4eba";
            offer.EnemyLabel = enemyStacks.Count <= 1
                ? (string.IsNullOrEmpty(offer.EnemyLabel) ? "\u654c\u519b" : DescribePrimaryEnemy(world, offer.ArmyStackId))
                : "\u654c\u519b " + enemyStacks.Count + " \u652f";
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
            // 弥留不可从参战名单勾�?
            if (LingeringBattlefieldPartyService.IsLingeringDowned(world, id))
                return false;
            var rec = world.Strategic.Participants.FindByEntity(id);
            if (rec == null || rec.Kind != BattleParticipantKind.OptionalFriendly)
                return false;
            if (!string.IsNullOrEmpty(rec.FormalArmyId))
                return SetOptionalFormalArmySelected(world, rec.FormalArmyId, selected);
            rec.Selected = selected;
            RefreshOfferPowerLabels(world);
            return true;
        }

        public static bool SetOptionalFormalArmySelected(
            SimulationWorld world,
            string formalArmyId,
            bool selected)
        {
            if (world?.Strategic == null || string.IsNullOrEmpty(formalArmyId))
                return false;

            var changed = false;
            var snap = world.Strategic.Participants;
            for (var i = 0; i < snap.Records.Count; i++)
            {
                var rec = snap.Records[i];
                if (rec.Kind != BattleParticipantKind.OptionalFriendly)
                    continue;
                if (!string.Equals(rec.FormalArmyId, formalArmyId, StringComparison.Ordinal))
                    continue;
                if (rec.Selected == selected)
                    continue;
                rec.Selected = selected;
                changed = true;
            }

            if (changed)
                RefreshOfferPowerLabels(world);
            return changed;
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
            // 闲置残留（大地图上、无人进场）：Park 后无 EngagedParty �?false，表现层不锁回遭遇图
            // 再进后已 SetEngagedParty：即�?BattlefieldLingering 仍为 true，也算主动遭�?
            // （否�?ApplyParty 不落表现，我方弥留在 LocalMap 隐身�?
            if (!rt.HasEngagedParty)
                return false;
            if (rt.SpawnOnNextMapLoad)
                return true;
            // 再进战场：场上可能只剩弥留刷怪（�?Alive），仍算遭遇进行�?
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
            if (world.Strategic.LingeringBattlefields.Count > 0)
                return true;
            if (world.Strategic.Encounter.BattlefieldLingering)
                return true;
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
            // 试炼弱匪：夹具必胜，避免战力刻度压平后仍被自动战掷骰团灭
            playerWon = ArmyStackAdapter.IsTrivialTestEnemyStack(offer.ArmyStackId) ||
                        ArmyStackAdapter.IsCasualtyTestEnemyStack(offer.ArmyStackId) ||
                        roll <= winChance;
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
                    : new AutoBattleReport { Summary = "\u81ea\u52a8\u6218\u6597\u80dc\u5229\u3002" };

                // 敌方增援栈：胜则一并削弱／移除（处决时移除�?
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            FormalArmy settleArmy = null;
            if (!string.IsNullOrEmpty(world.Strategic.Participants?.AttackerArmyId))
                world.Strategic.FormalArmies.TryGet(
                    world.Strategic.Participants.AttackerArmyId, out settleArmy);
            SecondBattleAnchorTrace.EmitArmyHex(
                "AutoResolve.AfterBind",
                world,
                settleArmy);
#endif

            offer.LastAutoBattleSummary = report?.Summary ?? string.Empty;
            world.Strategic.Participants.LastBattleSummary = string.IsNullOrEmpty(offer.LastAutoBattleSummary)
                ? (playerWon ? "\u81ea\u52a8\u6218\u6597\u80dc\u5229\u3002" : "\u81ea\u52a8\u6218\u6597\u5931\u5229\u3002")
                : offer.LastAutoBattleSummary;
            world.Strategic.Participants.PlayerWon = playerWon;
            world.Strategic.Participants.IsAutoSettlement = true;

            // 先关 Offer，进入战后结算弹窗；确认后再 Finish／出�?
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

            StrategicEncounterResolveService.RestoreParticipantsAfterBattle(world, snap);
            StrategicEncounterResolveService.EnsureFriendlyDownedWorldPresenceForAutoBattle(world, snap);
            ArmyPostBattleSyncService.SyncAttackerArmyAfterBattle(world, snap);

            // 自动战胜：立刻把残留栈钉到接战点，大地图结算弹窗期间就能看见
            if (playerWon)
                StrategicEncounterResolveService.ParkPrimaryEnemyStackAtBattleAnchor(world, snap);

            // 自动战未�?LocalMap：立刻刷弥留／尸体实�?+ 接战�?WorldPresence（与进图再出一致）
            // 强制�?Active session spawn list，禁止写进上一�?lingering scope�?
            rt.ActiveBattlefieldId = string.Empty;
            if (playerWon &&
                !string.IsNullOrEmpty(stackId) &&
                world.Strategic.Armies.TryGet(stackId, out var stack) &&
                stack != null &&
                stack.HasDownedRemnant)
                StrategicEncounterSpawner.EnsureMacroRemnantSpawns(world, snap);

            // Presence 钉好后再 Detach 敌军 Downed／Dead（否�?Residual Query 会因 FormalArmy membership 全排除）
            if (playerWon)
                ArmyPostBattleSyncService.SyncEnemyArmyAfterBattle(world, snap);

            // 自动战结算弹窗期间即可右键攻击残留：提前 park lingering + Hex 锚点
            if (playerWon && StrategicEncounterResolveService.HasLingeringBattlefieldRemnants(world))
            {
                rt.BattlefieldLingering = true;
                StrategicEncounterResolveService.PersistLingeringBattleAnchor(world, snap, rt);
                var parkedState = LingeringBattlefieldRegistry.CommitActiveSession(world, snap);
                if (parkedState != null && parkedState.SpawnedEntityIds.Count > 0)
                    rt.SpawnOnNextMapLoad = false;
                if (string.IsNullOrEmpty(rt.ArmyStackId) && !string.IsNullOrEmpty(stackId))
                    rt.ArmyStackId = stackId;
            }

            AutoResidualTrace.EmitAfterAutoBind(world, snap, playerWon);
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
                // FormalArmy 真源：禁止只刷抽象残留（DrawArmyStacks 会藏标记、又无弥留头�?�?整队「消失」）
                if (ArmyStackAdapter.HasFormalArmyLink(st))
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
                    // 与主栈一致：未处�?�?全员弥留残留
                    var members = Math.Max(1, st.MemberCount);
                    st.MemberCount = members;
                    st.IncapacitatedMemberCount = members;
                    st.CorpseMemberCount = 0;
                    st.IsBattlefieldRemnant = true;
                    st.CombatPower = Math.Max(1, st.CombatPower);
                }
            }
        }

        /// <summary>Offer／遭遇结束后：先解冻，再出队下一场或清空快照�?/summary>
        public static Result FinishOfferResolution(SimulationWorld world)
        {
            if (world?.Strategic == null)
                return Result.Failure(ErrorCode.InvalidArgument, "null world");

            // 必须先结�?Modal，否�?TryPromote 会被 IsModalEncounter 挡住
            StrategicClockFreezeService.EndFreeze(world);
            if (world.Strategic.Participants != null)
                world.Strategic.Participants.IsAutoSettlement = false;

            if (TryPromoteNextQueuedOffer(world))
                return Result.Success();

            // 残留战场期间：Participants.Clear 前把 Hex 锚点写入 Registry
            if (world.Strategic.Encounter != null &&
                (world.Strategic.Encounter.BattlefieldLingering ||
                 StrategicEncounterResolveService.HasLingeringBattlefieldRemnants(world)))
            {
                LingeringBattlefieldRegistry.CommitActiveSession(
                    world, world.Strategic.Participants);
                world.Strategic.Encounter.BattlefieldLingering =
                    world.Strategic.LingeringBattlefields.Count > 0;
            }

            world.Strategic.Participants.Clear();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            SecondBattleAnchorTrace.Emit("FinishOfferResolution.AfterClear", world);
#endif
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
                // 排队轮到但人未到：上路追击，到站后由 AfterTravelTick 弹接战（禁止远程瞬开 Offer�?
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
