using System;
using System.Collections.Generic;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// 结束 Modal／结算：参战者落�?BattleAnchor（禁止瞬移回家）�?
    /// 场上仍有弥留则保留遭遇战场，否则销毁�?
    /// </summary>
    public static class StrategicEncounterResolveService
    {
        public static Result ResolveAndEnd(SimulationWorld world)
        {
            if (world?.Strategic == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld is null.");

            var snap = world.Strategic.Participants;
            // Phase 5S-B2-3.1：区分真实世界战（WorldSite / Wilderness）与 Explicit EncounterMap。
            var realWorldCombat = snap != null &&
                                  (snap.LocalMapResolutionKind == BattleLocalMapResolutionKind.WorldSite ||
                                   snap.LocalMapResolutionKind == BattleLocalMapResolutionKind.Wilderness);

            // WORLD_COMBAT 不走 legacy RestoreParticipantsAfterBattle（PreBattle 模型）：
            // PlayerParty + FormalArmy 已在「点击 Manual」时 commit 到真实 BattleHex，
            // End Battle 不需要 restore / return / re-anchor living participant。
            if (!realWorldCombat)
                RestoreParticipantsAfterBattle(world, snap);

            var linger = HasLingeringBattlefieldRemnants(world);
            if (realWorldCombat)
            {
                // 真实 WORLD_COMBAT：先释放 battle scope（不删实体），再 detach / sync 所有
                // participant FormalArmies —— detach 阶段由 DetachMemberAtArmyLocation 直接把
                // downed 成员钉到 army.WorldMotion.CurrentHex（residual-safe），因此这里不再提前
                // 钉 residual（旧顺序会被后续 detach 覆盖）。residual 的 final authority 移到
                // 三个 Army sync 之后、Participants.Clear 之前统一收口。
                ReleaseWorldCombatScopeWithoutRemovingEntities(world);
                world.Strategic.ClearBattleOffer();
            }
            else if (linger)
            {
                ParkLingeringBattlefield(world, snap);
                world.Strategic.ClearBattleOffer();
                if (snap != null)
                    snap.IsAutoSettlement = false;
            }
            else
            {
                DestroyBattlefieldCompletely(world);
                world.Strategic.ClearBattleOffer();
            }

            NormalizePresenceAfterEncounterExit(world);
            ArmyPostBattleSyncService.SyncAttackerArmyAfterBattle(world, snap);
            ArmyPostBattleSyncService.SyncEnemyArmyAfterBattle(world, snap);
            // Phase 5S：补齐 support / reinforcement FormalArmy（跳过已处理的 Attacker/Enemy primary）
            ArmyPostBattleSyncService.SyncParticipantFormalArmiesAfterBattle(world, snap);
            StrategicPursuitService.ClearPursuit(world);
            WorldTravelService.SyncPartyFocus(world);

            if (realWorldCombat)
            {
                // FINAL RESIDUAL AUTHORITY：FormalArmy detach 全部完成后，把本场所有 downed
                // participant（friendly + enemy）钉到 BattleAnchorHex，并 assert 不变量。
                // 必须在 Participants.Clear（FinishOfferResolution）之前，因为需要 frozen snapshot。
                EnsureFriendlyDownedWorldPresence(world, snap);
                EnsureEnemyDownedWorldPresence(world, snap);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                AssertFinalResidualAuthority(world, snap);
                LogBattleResidualAfterResolve(world, snap);
#endif
            }

            BattleOfferService.FinishOfferResolution(world);
            return Result.Success();
        }

        /// <summary>
        /// 解冻后把仍卡�?InEncounter 的宏观位置拨�?AtHex／AtSite�?
        /// 避免「只有一人弥留、其他人却不能下令」�?
        /// </summary>
        public static void NormalizePresenceAfterEncounterExit(SimulationWorld world)
        {
            if (world?.WorldPresence?.All == null)
                return;
            // 调用方须�?EndFreeze；若�?Modal 则不要拨（战中）
            if (StrategicClockFreezeService.IsModalEncounter(world))
                return;

            foreach (var kv in world.WorldPresence.All)
            {
                var wp = kv.Value;
                if (wp == null || wp.Mode != PartyWorldPresenceMode.InEncounter)
                    continue;

                if (wp.HexQ != WorldAgentPresence.InvalidHexComponent &&
                    wp.HexR != WorldAgentPresence.InvalidHexComponent &&
                    StrategicResidualPresenceService.IsResidualLifeCandidate(world, wp.EntityId))
                {
                    wp.SetAtHex(new HexCoord(wp.HexQ, wp.HexR));
                }
                else if (wp.UsesHexPresence)
                {
                    wp.SetAtHex(wp.ResidualHex);
                }
                else if (!string.IsNullOrEmpty(wp.SiteId))
                {
                    wp.Mode = PartyWorldPresenceMode.AtSite;
                }
                else
                {
                    wp.Mode = PartyWorldPresenceMode.AtSite;
                }

                wp.ClearFollow();
                wp.ClearCombatPursuit();
            }
        }

        /// <summary>场上已无弥留／尸体时销毁残留战场（补刀／清场后调用）�?/summary>
        public static Result TryDestroyIfNoRemnants(SimulationWorld world)
        {
            if (world?.Strategic == null)
                return Result.Failure(ErrorCode.InvalidArgument, "null");
            if (HasLingeringBattlefieldRemnants(world))
                return Result.Success();
            DestroyBattlefieldCompletely(world);
            WorldTravelService.SyncPartyFocus(world);
            return Result.Success();
        }

        /// <summary>残留战场仍有倒下者（弥留或可见尸体）�?/summary>
        public static bool HasLingeringBattlefieldRemnants(SimulationWorld world)
        {
            if (world?.Strategic == null)
                return false;

            var snap = world.Strategic.Participants;
            if (snap != null)
            {
                for (var i = 0; i < snap.Records.Count; i++)
                {
                    var rec = snap.Records[i];
                    if (rec.EntityId.IsNone)
                        continue;
                    if (LingeringBattlefieldPartyService.IsLingeringDowned(world, rec.EntityId))
                        return true;
                }
            }

            var rt = world.Strategic.Encounter;
            if (rt != null)
            {
                var scoped = BattlefieldSpawnScope.GetSpawnList(world);
                if (scoped != null)
                {
                    for (var i = 0; i < scoped.Count; i++)
                    {
                        var id = new EntityId(scoped[i]);
                        if (LingeringBattlefieldPartyService.IsLingeringDowned(world, id))
                            return true;
                    }
                }
            }

            foreach (var battlefield in world.Strategic.LingeringBattlefields.Enumerate())
            {
                if (battlefield == null)
                    continue;
                for (var i = 0; i < battlefield.SpawnedEntityIds.Count; i++)
                {
                    var id = new EntityId(battlefield.SpawnedEntityIds[i]);
                    if (LingeringBattlefieldPartyService.IsLingeringDowned(world, id))
                        return true;
                }
            }

            if (rt != null &&
                !string.IsNullOrEmpty(rt.ArmyStackId) &&
                world.Strategic.Armies.TryGet(rt.ArmyStackId, out var stack) &&
                stack != null &&
                stack.HasDownedRemnant)
                return true;

            // 自动战后尚未绑到 Encounter.ArmyStackId 时，看快照主敌栈
            var primary = world.Strategic.Participants?.PrimaryEnemyStackId;
            if (!string.IsNullOrEmpty(primary) &&
                world.Strategic.Armies.TryGet(primary, out var primaryStack) &&
                primaryStack != null &&
                primaryStack.HasDownedRemnant)
                return true;

            // 快照�?Clear 后仍可能有我方弥留／尸体头像钉在宏观图上
            if (world.WorldPresence?.All != null)
            {
                foreach (var kv in world.WorldPresence.All)
                {
                    var id = new EntityId(kv.Key);
                    if (id.IsNone || !world.Entities.TryGet(id, out var ent) || ent == null)
                        continue;
                    if ((ent.Tags & EntityTag.Npc) != 0)
                        continue;
                    if (LingeringBattlefieldPartyService.IsLingeringDowned(world, id))
                        return true;
                }
            }

            return false;
        }

        public static void EnterPostBattleIfCleared(SimulationWorld world) =>
            TryEnterPostBattleFromManual(world);

        /// <summary>敌军清空或我方全�?�?PostBattle（仍冻结，可点结束战斗）�?/summary>
        public static void TryEnterPostBattleFromManual(SimulationWorld world)
        {
            if (world?.Strategic == null)
                return;
            if (world.Strategic.ClockFreeze.Reason != StrategicClockFreezeReason.ManualEncounter)
                return;

            if (!TryEvaluateManualBattleOutcome(world, out var terminal, out var playerWon) || !terminal)
                return;

            StrategicClockFreezeService.BeginOrPromote(
                world, StrategicClockFreezeReason.PostBattle);

            if (string.IsNullOrEmpty(world.Strategic.Participants.LastBattleSummary))
            {
                world.Strategic.Participants.LastBattleSummary = playerWon
                    ? "敌军已全部失去战斗能力。可查看现场；点击「结束战斗」后恢复世界时间。"
                    : "我方已全部失去战斗能力。点击「结束战斗」后结束本次战斗并恢复世界时间。";
            }

            world.Strategic.Participants.PlayerWon = playerWon;
        }

        /// <summary>真实 WORLD_COMBAT 只以 frozen participant snapshot 的 CanFight 判定终局。</summary>
        public static bool TryEvaluateManualBattleOutcome(SimulationWorld world, out bool terminal, out bool playerWon)
        {
            terminal = false; playerWon = false;
            var snap = world?.Strategic?.Participants;
            if (snap == null) return false;
            var real = snap.LocalMapResolutionKind == BattleLocalMapResolutionKind.WorldSite ||
                       snap.LocalMapResolutionKind == BattleLocalMapResolutionKind.Wilderness;
            if (!real)
            {
                var cleared = StrategicEncounterSpawner.IsFieldCleared(world);
                var down = AreAllEngagedFriendliesDown(world);
                terminal = cleared || down; playerWon = cleared;
                return true;
            }
            var enemyCanFight = false; var friendlyCanFight = false; var hasFriendly = false;
            for (var i = 0; i < snap.Records.Count; i++)
            {
                var r = snap.Records[i];
                if (r.EntityId.IsNone || !world.Entities.TryGet(r.EntityId, out var e) || e == null) continue;
                var canFight = CombatLifeStateService.CanFight(e);
                if (r.Kind == BattleParticipantKind.EnemyPrimary || r.Kind == BattleParticipantKind.EnemyReinforcement) enemyCanFight |= canFight;
                else if (r.Kind == BattleParticipantKind.MandatoryFriendly || (r.Kind == BattleParticipantKind.OptionalFriendly && r.Selected)) { hasFriendly = true; friendlyCanFight |= canFight; }
            }
            if (!enemyCanFight) { terminal = true; playerWon = true; }
            else if (hasFriendly && !friendlyCanFight) { terminal = true; playerWon = false; }
            return true;
        }

        public static bool AreAllEngagedFriendliesDown(SimulationWorld world)
        {
            var snap = world?.Strategic?.Participants;
            if (snap != null && (snap.LocalMapResolutionKind == BattleLocalMapResolutionKind.WorldSite || snap.LocalMapResolutionKind == BattleLocalMapResolutionKind.Wilderness))
            {
                var anySelectedFriendly = false;
                for (var i = 0; i < snap.Records.Count; i++)
                {
                    var r = snap.Records[i];
                    if (r.Kind != BattleParticipantKind.MandatoryFriendly && !(r.Kind == BattleParticipantKind.OptionalFriendly && r.Selected)) continue;
                    if (r.EntityId.IsNone || !world.Entities.TryGet(r.EntityId, out var e) || e == null) continue;
                    anySelectedFriendly = true; if (CombatLifeStateService.CanFight(e)) return false;
                }
                return anySelectedFriendly;
            }
            var rt = world?.Strategic?.Encounter;
            if (rt == null || !rt.HasEngagedParty)
                return false;
            var any = false;
            for (var i = 0; i < rt.EngagedPartyIds.Count; i++)
            {
                var id = new EntityId(rt.EngagedPartyIds[i]);
                if (id.IsNone || !world.Entities.TryGet(id, out var ent) || ent == null)
                    continue;
                any = true;
                if (CombatLifeStateService.CanFight(ent))
                    return false;
            }

            return any;
        }

        /// <summary>
        /// 参战／勾选支援者一律落�?BattleAnchor；禁�?Apply PreBattle 瞬移回家�?
        /// 未参战、未勾选者不改位置�?
        /// </summary>
        public static void RestoreParticipantsAfterBattle(
            SimulationWorld world,
            BattleParticipantSnapshot snap)
        {
            if (world == null || snap == null)
                return;

            for (var i = 0; i < snap.Records.Count; i++)
            {
                var rec = snap.Records[i];
                if (rec.EntityId.IsNone)
                    continue;
                if (rec.Kind != BattleParticipantKind.MandatoryFriendly &&
                    !(rec.Kind == BattleParticipantKind.OptionalFriendly && rec.Selected))
                    continue;
                if (!world.WorldPresence.TryGet(rec.EntityId, out var wp) || wp == null)
                    continue;
                if (!world.Entities.TryGet(rec.EntityId, out var ent) || ent == null)
                    continue;

                // 强制参战、已上场／已 Engaged 的支�?�?BattleAnchor�?
                // 仅勾选、未上场的远处支�?�?�?PreBattle（禁止瞬移到接战点，也禁止把路上人送回家）
                // Phase 5S：真实 LocalMap 手动战（WorldSite/Wilderness）中 selected OptionalFriendly
                // 已被 materialize 进本场战斗，属于实际参战者 —— 同样必须落 BattleAnchor，
                // 禁止走 PreBattle 回战前位置。
                var realLocalMapBattle =
                    snap.LocalMapResolutionKind == BattleLocalMapResolutionKind.WorldSite ||
                    snap.LocalMapResolutionKind == BattleLocalMapResolutionKind.Wilderness;
                var mustAnchor =
                    rec.Kind == BattleParticipantKind.MandatoryFriendly ||
                    (rec.Kind == BattleParticipantKind.OptionalFriendly &&
                     rec.Selected &&
                     realLocalMapBattle) ||
                    world.Strategic.Encounter.IsEngaged(rec.EntityId) ||
                    wp.Mode == PartyWorldPresenceMode.InEncounter ||
                    (rec.PreBattle != null &&
                     rec.PreBattle.Mode == PartyWorldPresenceMode.InEncounter);

                if (mustAnchor)
                    PlaceAtBattleAnchor(world, wp, snap);
                else if (rec.PreBattle != null)
                    rec.PreBattle.ApplyTo(wp);

                wp.ClearFollow();
                wp.ClearCombatPursuit();
            }

            var engaged = new List<EntityId>(CollectEngaged(world));
            for (var i = 0; i < engaged.Count; i++)
            {
                var id = engaged[i];
                if (snap.FindByEntity(id) != null)
                    continue;
                if (!world.WorldPresence.TryGet(id, out var wp) || wp == null)
                    continue;
                PlaceAtBattleAnchor(world, wp, snap);
                wp.ClearFollow();
                wp.ClearCombatPursuit();
            }
        }

        static void ParkLingeringBattlefield(SimulationWorld world, BattleParticipantSnapshot snap)
        {
            var rt = world.Strategic.Encounter;
            rt.BattlefieldLingering = true;
            rt.FieldCleared = true;
            PersistLingeringBattleAnchor(world, snap, rt);

            LingeringBattlefieldState parkedState = LingeringBattlefieldRegistry.CommitActiveSession(world, snap);
            if (snap != null && !string.IsNullOrEmpty(snap.EncounterLocalMapId))
                rt.LingeringLocalMapId = snap.EncounterLocalMapId;
            else if (string.IsNullOrEmpty(rt.LingeringLocalMapId))
                rt.LingeringLocalMapId = StrategicEncounterCatalog.DefaultEncounterLocalMapId;

            // 自动战未进过 LocalMap：把主敌栈绑�?Encounter，否则再进／残留丢失
            if (string.IsNullOrEmpty(rt.ArmyStackId) &&
                snap != null &&
                !string.IsNullOrEmpty(snap.PrimaryEnemyStackId))
                rt.ArmyStackId = snap.PrimaryEnemyStackId;

            ArmyStack parkedStack = null;
            if (!string.IsNullOrEmpty(rt.ArmyStackId) &&
                world.Strategic.Armies.TryGet(rt.ArmyStackId, out parkedStack) &&
                parkedStack != null)
            {
                ParkStackAtBattleAnchor(world, parkedStack, snap);
                var downedSpawns = CountLingeringDownedSpawns(world, parkedState);
                if (parkedStack.HasDownedRemnant || downedSpawns > 0)
                {
                    parkedStack.IsBattlefieldRemnant = true;
                    if (parkedStack.IncapacitatedMemberCount <= 0 && parkedStack.CorpseMemberCount <= 0)
                    {
                        if (downedSpawns > 0)
                            parkedStack.CorpseMemberCount = Math.Max(1, downedSpawns);
                    }

                    var downedCount = Math.Max(
                        parkedStack.IncapacitatedMemberCount,
                        parkedStack.CorpseMemberCount);
                    if (parkedStack.MemberCount < downedCount)
                        parkedStack.MemberCount = downedCount;
                }
            }

            // 抽象残留栈尚无实�?�?下次进图刷弥留／尸体；已�?tracked 则复�?
            var trackedCount = parkedState?.SpawnedEntityIds.Count ?? 0;
            rt.SpawnOnNextMapLoad =
                parkedStack != null &&
                parkedStack.HasDownedRemnant &&
                trackedCount <= 0;

            // 给弥留／尸体�?WorldPresence，大地图能画头像（ClearEngagedParty 前仍可读 Engaged 名单�?
            EnsureFriendlyDownedWorldPresence(world, snap);
            EnsureEnemyDownedWorldPresence(world, snap, parkedState?.SpawnedEntityIds);
            ArmyPostBattleSyncService.SyncEnemyArmyAfterBattle(world, snap);

            // 退�?Modal：人不再 InEncounter，但遭遇数据保留
            rt.ClearEngagedParty();
            world.PartyWorld.EncounterId = string.Empty;

            // 卸掉 ActiveMap 遭遇会话标记：LocalMap 切回焦点节点图由 Host 处理
            if (!string.IsNullOrEmpty(world.PartyWorld.SiteId) &&
                world.Strategic.Sites.TryGet(world.PartyWorld.SiteId, out var focusSite) &&
                focusSite != null &&
                !string.IsNullOrEmpty(focusSite.LocalMapId))
                world.PartyWorld.LocalMapId = focusSite.LocalMapId;
        }

        static void DestroyBattlefieldCompletely(SimulationWorld world)
        {
            var rt = world.Strategic.Encounter;
            StrategicEncounterSpawner.ClearSpawned(world);
            if (!string.IsNullOrEmpty(rt.ArmyStackId) &&
                world.Strategic.Armies.TryGet(rt.ArmyStackId, out var stack) &&
                stack != null &&
                stack.IsBattlefieldRemnant)
                world.Strategic.Armies.Remove(stack.Id);

            rt.ClearActiveEncounterSession();
            rt.FieldCleared = false;
            rt.ArmyStackId = string.Empty;
            rt.EncounterLinkId = string.Empty;
            rt.SpawnOnNextMapLoad = false;
            rt.LingeringLocalMapId = string.Empty;
            world.PartyWorld.EncounterId = string.Empty;
            world.Strategic.Participants.Clear();

            if (world.Strategic.LingeringBattlefields.Count > 0)
            {
                rt.BattlefieldLingering = true;
                return;
            }

            rt.BattlefieldLingering = false;
            rt.ClearLingeringBattleAnchorHex();
            rt.ClearAllLingeringBattlefieldHexes();
            rt.ClearAllLingeringBattlefields();
        }

        /// <summary>
        /// Phase 5S-B2-3.1：WORLD_COMBAT 非破坏性 release —— 只解除 Encounter scope 对
        /// tracked spawn 的引用并清空 Active 遭遇会话，<b>不</b> FinalizeRemoval 任何仍存在的
        /// gameplay entity（living survivor / downed / visible corpse 都是真实世界实体）。
        /// 这些实体由 LoadedStrategicPopulationMaterializer 作为普通 LocalMap population 显示。
        /// </summary>
        static void ReleaseWorldCombatScopeWithoutRemovingEntities(SimulationWorld world)
        {
            var scoped = BattlefieldSpawnScope.GetMutableSpawnList(world);
            if (scoped != null)
            {
                for (var i = scoped.Count - 1; i >= 0; i--)
                {
                    var id = new EntityId(scoped[i]);
                    BattlefieldSpawnScope.RemoveTrackedSpawnAt(world, i);
                    // 不删除实体：living survivor / downed / corpse 继续存在。
                }
            }

            // 完整清本场 active battle transient（含 FieldCleared / ArmyStackId /
            // EncounterLinkId / LingeringLocalMapId / PendingLingeringEnterBattlefieldId），
            // 结束后的 Runtime 不再像旧战斗；Registry / Residual / Pursuit 不动。
            world.Strategic.Encounter?.ClearCompletedWorldCombatSession();
        }

        /// <summary>
        /// 残留战场再进：Participants 快照必须使用「该 Hex」的 canonical Anchor�?
        /// 禁止从敌军栈 Legacy NodeId（常�?spawn 点青石荒村）推导�?
        /// 仅供 Lingering re-entry；禁止用于新 Active Enemy BattleOffer�?
        /// </summary>
        public static bool TryApplyCanonicalLingeringBattleAnchor(
            SimulationWorld world,
            BattleParticipantSnapshot snap,
            HexCoord? preferredHex = null)
        {
            if (world?.Strategic == null || snap == null)
                return false;
            if (!ArmyHexBattleAnchorService.IsHexAnchorMode(world))
                return false;

            HexCoord hex;
            if (preferredHex.HasValue &&
                world.HexWorld != null &&
                world.HexWorld.Contains(preferredHex.Value) &&
                world.Strategic.Encounter != null &&
                world.Strategic.Encounter.HasLingeringBattlefieldAtHex(preferredHex.Value))
            {
                hex = preferredHex.Value;
            }
            else if (!TryGetLingeringBattleAnchorHex(world, out hex))
            {
                return false;
            }

            if (world.HexWorld != null && world.HexWorld.HasGrid && !world.HexWorld.Contains(hex))
                return false;

            ArmyHexBattleAnchorService.SetBattleAnchorHex(snap, hex);
            return true;
        }

        /// <summary>
        /// 把本场接�?Hex 注册为残留战场锚点�?
        /// 新场 snap Hex 优先；不得用旧残�?Hex 覆盖本场 Participants�?
        /// </summary>
        public static void PersistLingeringBattleAnchor(
            SimulationWorld world,
            BattleParticipantSnapshot snap,
            StrategicEncounterRuntime rt = null)
        {
            if (world?.Strategic == null)
                return;
            rt = rt ?? world.Strategic.Encounter;
            if (rt == null)
                return;

            if (ArmyHexBattleAnchorService.TryGetBattleAnchorHex(snap, out var snapHex) &&
                world.HexWorld != null &&
                world.HexWorld.Contains(snapHex))
            {
                rt.SetLingeringBattleAnchorHex(snapHex);
                rt.RegisterLingeringBattlefield(
                    snapHex,
                    snap?.PrimaryEnemyStackId ?? rt.ArmyStackId ?? string.Empty);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                SecondBattleAnchorTrace.Emit(
                    "PersistLingeringBattleAnchor.SnapWins",
                    world,
                    "PersistedHex=" + snapHex);
#endif
                return;
            }

            if (rt.TryGetLingeringBattleAnchorHex(out _))
                return;

            var stackId = !string.IsNullOrEmpty(rt.ArmyStackId)
                ? rt.ArmyStackId
                : snap?.PrimaryEnemyStackId ?? string.Empty;
            if (!string.IsNullOrEmpty(stackId) &&
                world.Strategic.Armies.TryGet(stackId, out var stack) &&
                stack != null &&
                ArmyStackAdapter.TryGetFormalArmy(world, stack, out var army) &&
                army != null &&
                army.UsesHexStrategicPosition &&
                world.HexWorld != null &&
                world.HexWorld.Contains(army.CurrentHex))
            {
                rt.SetLingeringBattleAnchorHex(army.CurrentHex);
                rt.RegisterLingeringBattlefield(army.CurrentHex, stackId);
            }
        }

        /// <summary>残留战场 Hex 查询：优�?Encounter Runtime 最新锚点，其次 Participants�?/summary>
        public static bool TryGetLingeringBattleAnchorHex(
            SimulationWorld world,
            out HexCoord hex)
        {
            hex = default;
            if (world?.Strategic == null)
                return false;

            var rt = world.Strategic.Encounter;
            if (rt != null && rt.TryGetLingeringBattleAnchorHex(out hex))
                return true;

            return ArmyHexBattleAnchorService.TryGetBattleAnchorHex(
                world.Strategic.Participants, out hex);
        }

        /// <summary>指定 Hex 是否已注册为残留战场（支持多�?H1/H2 并存）�?/summary>
        public static bool HasLingeringBattlefieldRegisteredAtHex(
            SimulationWorld world,
            HexCoord hex)
        {
            return world?.Strategic?.LingeringBattlefields != null &&
                   world.Strategic.LingeringBattlefields.HasAtHex(hex);
        }

        public static void ParkPrimaryEnemyStackAtBattleAnchor(
            SimulationWorld world,
            BattleParticipantSnapshot snap)
        {
            if (world?.Strategic?.Armies == null || snap == null)
                return;
            var stackId = world.Strategic.Encounter?.ArmyStackId;
            if (string.IsNullOrEmpty(stackId))
                stackId = snap.PrimaryEnemyStackId ?? string.Empty;
            if (string.IsNullOrEmpty(stackId) ||
                !world.Strategic.Armies.TryGet(stackId, out var stack) ||
                stack == null)
                return;
            ParkStackAtBattleAnchor(world, stack, snap);
        }

        static void ParkStackAtBattleAnchor(
            SimulationWorld world,
            ArmyStack stack,
            BattleParticipantSnapshot snap)
        {
            if (stack == null || snap == null)
                return;
            ArmyHexBattleAnchorService.ParkStackAtBattleAnchor(world, stack, snap);
        }

        /// <summary>给已 tracked 的敌军弥留／尸体补接战点 WorldPresence（自动战宏观刷怪后亦调用）�?/summary>
        public static void RefreshEnemyDownedWorldPresence(
            SimulationWorld world,
            BattleParticipantSnapshot snap) =>
            EnsureEnemyDownedWorldPresence(world, snap);

        static void EnsureEnemyDownedWorldPresence(
            SimulationWorld world,
            BattleParticipantSnapshot snap,
            IReadOnlyList<ulong> spawnIds = null)
        {
            var rt = world.Strategic.Encounter;
            if (rt == null || snap == null)
                return;

            // A. frozen snapshot 真实敌军（WORLD_COMBAT 不再依赖 spawn scope 也能把真实
            // enemy residual 钉到 BattleAnchorHex）。
            for (var i = 0; i < snap.Records.Count; i++)
            {
                var rec = snap.Records[i];
                if (rec.EntityId.IsNone)
                    continue;
                if (rec.Kind != BattleParticipantKind.EnemyPrimary &&
                    rec.Kind != BattleParticipantKind.EnemyReinforcement)
                    continue;
                if (!LingeringBattlefieldPartyService.IsLingeringDowned(world, rec.EntityId))
                    continue;
                if (!world.Entities.TryGet(rec.EntityId, out var ent) || ent == null)
                    continue;
                if (!world.WorldPresence.TryGet(rec.EntityId, out var wp) || wp == null)
                    wp = world.WorldPresence.GetOrCreate(rec.EntityId);
                PlaceAtBattleAnchor(world, wp, snap);
            }

            // B. legacy tracked fallback synthetic spawn（encounter-owned NPC）
            if (spawnIds == null)
                spawnIds = BattlefieldSpawnScope.GetSpawnList(world) ?? rt.SpawnedEntityIds;

            for (var i = 0; i < spawnIds.Count; i++)
            {
                var id = new EntityId(spawnIds[i]);
                if (snap.IsEnemyParticipant(id))
                    continue; // 已在 A 处理，避免重复定位
                if (!world.Entities.TryGet(id, out var ent) || ent == null)
                    continue;
                // 弥留与可见尸体都要钉在接战点（再进 LocalMap／大地图倒计时同一套实体）
                if (!LingeringBattlefieldPartyService.IsLingeringDowned(world, id))
                    continue;

                if (!world.WorldPresence.TryGet(id, out var wp) || wp == null)
                    wp = world.WorldPresence.GetOrCreate(id);

                PlaceAtBattleAnchor(world, wp, snap);
            }
        }

        /// <summary>自动战／手动战后：我方弥留／尸体钉在接战点（Restore 可能漏 PreBattle）。</summary>
        /// <summary>自动战结算弹窗期间：我方弥留／尸体钉在接战点�?/summary>
        public static void EnsureFriendlyDownedWorldPresenceForAutoBattle(
            SimulationWorld world,
            BattleParticipantSnapshot snap) =>
            EnsureFriendlyDownedWorldPresence(world, snap);

        static void EnsureFriendlyDownedWorldPresence(
            SimulationWorld world,
            BattleParticipantSnapshot snap)
        {
            if (world == null || snap == null)
                return;

            for (var i = 0; i < snap.Records.Count; i++)
            {
                var rec = snap.Records[i];
                if (rec.EntityId.IsNone)
                    continue;
                if (rec.Kind != BattleParticipantKind.MandatoryFriendly &&
                    !(rec.Kind == BattleParticipantKind.OptionalFriendly && rec.Selected))
                    continue;
                if (!LingeringBattlefieldPartyService.IsLingeringDowned(world, rec.EntityId))
                    continue;
                if (!world.WorldPresence.TryGet(rec.EntityId, out var wp) || wp == null)
                    wp = world.WorldPresence.GetOrCreate(rec.EntityId);
                PlaceAtBattleAnchor(world, wp, snap);
            }

            var engaged = CollectEngaged(world);
            for (var i = 0; i < engaged.Count; i++)
            {
                var id = engaged[i];
                if (snap.FindByEntity(id) != null)
                    continue;
                if (!LingeringBattlefieldPartyService.IsLingeringDowned(world, id))
                    continue;
                if (!world.WorldPresence.TryGet(id, out var wp) || wp == null)
                    wp = world.WorldPresence.GetOrCreate(id);
                PlaceAtBattleAnchor(world, wp, snap);
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// 真实 WORLD_COMBAT 收口 invariant：本场所有 downed participant（friendly + enemy）
        /// 若已脱离 FormalArmy，最终必须 Mode == AtHex 且 ResidualHex == BattleAnchorHex。
        /// 若仍挂在 FormalArmy 上则是 DetachNonLivingMembersAtBattlefield 的 bug——不 silently
        /// mask，直接 assert 暴露。仅 DEVELOPMENT_BUILD / UNITY_EDITOR 下生效，不增加 runtime log。
        /// </summary>
        static void AssertFinalResidualAuthority(
            SimulationWorld world,
            BattleParticipantSnapshot snap)
        {
            if (world == null || snap == null)
                return;
            if (!ArmyHexBattleAnchorService.TryGetBattleAnchorHex(snap, out var anchorHex))
                return;

            for (var i = 0; i < snap.Records.Count; i++)
            {
                var rec = snap.Records[i];
                if (rec.EntityId.IsNone)
                    continue;
                if (rec.Kind != BattleParticipantKind.MandatoryFriendly &&
                    rec.Kind != BattleParticipantKind.EnemyPrimary &&
                    rec.Kind != BattleParticipantKind.EnemyReinforcement &&
                    !(rec.Kind == BattleParticipantKind.OptionalFriendly && rec.Selected))
                    continue;
                if (!LingeringBattlefieldPartyService.IsLingeringDowned(world, rec.EntityId))
                    continue;

                if (ArmyService.TryGetArmyForCharacter(world, rec.EntityId, out _))
                {
                    System.Diagnostics.Debug.Assert(
                        false,
                        "WORLD_COMBAT downed participant still in FormalArmy after post-battle sync: " +
                        rec.EntityId.Value);
                    continue;
                }

                if (!world.WorldPresence.TryGet(rec.EntityId, out var wp) || wp == null)
                    continue;
                System.Diagnostics.Debug.Assert(
                    wp.Mode == PartyWorldPresenceMode.AtHex && wp.ResidualHex.Equals(anchorHex),
                    "WORLD_COMBAT residual not anchored at BattleAnchorHex: " + rec.EntityId.Value);
            }
        }
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>战后同步完成、Participants 清除前记录 FormalArmy 参战者的残留归属。</summary>
        static void LogBattleResidualAfterResolve(
            SimulationWorld world,
            BattleParticipantSnapshot snap)
        {
            if (world?.Entities == null || snap == null)
                return;

            for (var i = 0; i < snap.Records.Count; i++)
            {
                var record = snap.Records[i];
                if (record.EntityId.IsNone || string.IsNullOrEmpty(record.FormalArmyId))
                    continue;
                if (!world.Entities.TryGet(record.EntityId, out var entity) || entity == null)
                    continue;

                var lifeState = CombatLifeStateService.ResolveLifeStateLabel(entity);
                var inArmy = ArmyService.TryGetArmyForCharacter(world, record.EntityId, out var army) && army != null;
                var presenceMode = world.WorldPresence.TryGet(record.EntityId, out var wp) && wp != null
                    ? wp.Mode.ToString()
                    : "(none)";
                var residualHex = wp != null ? wp.ResidualHex.ToString() : "(none)";
                System.Diagnostics.Debug.WriteLine(
                    "[BattleResidual]" +
                    " EntityId=" + record.EntityId +
                    " Name=" + (string.IsNullOrEmpty(entity.DisplayName) ? record.EntityId.ToString() : entity.DisplayName) +
                    " Kind=" + record.Kind +
                    " FormalArmyId=" + record.FormalArmyId +
                    " LifeState=" + lifeState +
                    " ArmyMembershipAfterResolve=" + inArmy +
                    " WorldPresenceMode=" + presenceMode +
                    " ResidualHex=" + residualHex +
                    " InLoadedLocalMap=" + world.LocalMap.ContainsOccupant(record.EntityId) +
                    " VisibleNow=" + StrategicEncounterHostilityService.IsVisibleOnEncounterLocalMap(world, record.EntityId) +
                    " IsResidual=" + LingeringBattlefieldPartyService.IsLingeringDowned(world, record.EntityId));
            }
        }
#endif

        public static void PlaceAtBattleAnchor(
            SimulationWorld world,
            WorldAgentPresence wp,
            BattleParticipantSnapshot snap)
        {
            if (wp == null || snap == null)
                return;
            if (ArmyHexBattleAnchorService.IsHexAnchorMode(world))
            {
                if (StrategicResidualPresenceService.TryResolveEncounterHex(world, snap, out var hex))
                {
                    wp.SetAtHex(hex);
                    return;
                }

                ArmyHexBattleAnchorService.PlacePresenceAtBattleAnchor(world, wp, snap);
                return;
            }
        }

        static int CountLingeringDownedSpawns(
            SimulationWorld world,
            LingeringBattlefieldState parkedState = null)
        {
            if (parkedState != null)
                return CountLingeringDownedSpawnsInList(world, parkedState.SpawnedEntityIds);

            var scoped = BattlefieldSpawnScope.GetSpawnList(world);
            if (scoped != null)
                return CountLingeringDownedSpawnsInList(world, scoped);

            var rt = world?.Strategic?.Encounter;
            return rt == null
                ? 0
                : CountLingeringDownedSpawnsInList(world, rt.SpawnedEntityIds);
        }

        static int CountLingeringDownedSpawnsInList(
            SimulationWorld world,
            IReadOnlyList<ulong> spawnIds)
        {
            if (world == null || spawnIds == null)
                return 0;
            var n = 0;
            for (var i = 0; i < spawnIds.Count; i++)
            {
                var id = new EntityId(spawnIds[i]);
                if (LingeringBattlefieldPartyService.IsLingeringDowned(world, id))
                    n++;
            }

            return n;
        }

        static List<EntityId> CollectEngaged(SimulationWorld world)
        {
            var list = new List<EntityId>();
            var rt = world.Strategic?.Encounter;
            if (rt == null)
                return list;
            for (var i = 0; i < rt.EngagedPartyIds.Count; i++)
                list.Add(new EntityId(rt.EngagedPartyIds[i]));
            return list;
        }

        static float Clamp01(float v)
        {
            if (v < 0f)
                return 0f;
            if (v > 1f)
                return 1f;
            return v;
        }
    }
}
