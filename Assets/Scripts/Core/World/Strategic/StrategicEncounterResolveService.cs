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
    /// 结束 Modal／结算：参战者落在 BattleAnchor（禁止瞬移回家）；
    /// 场上仍有弥留则保留遭遇战场，否则销毁。
    /// </summary>
    public static class StrategicEncounterResolveService
    {
        public static Result ResolveAndEnd(SimulationWorld world)
        {
            if (world?.Strategic == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld is null.");

            var snap = world.Strategic.Participants;
            RestoreParticipantsAfterBattle(world, snap);

            var linger = HasLingeringBattlefieldRemnants(world);
            if (linger)
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
            StrategicPursuitService.ClearPursuit(world);
            WorldTravelService.SyncPartyFocus(world);
            BattleOfferService.FinishOfferResolution(world);
            return Result.Success();
        }

        /// <summary>
        /// 解冻后把仍卡在 InEncounter 的宏观位置拨回 AtHex／AtNode／RouteAnchored，
        /// 避免「只有一人弥留、其他人却不能下令」。
        /// </summary>
        public static void NormalizePresenceAfterEncounterExit(SimulationWorld world)
        {
            if (world?.WorldPresence?.All == null)
                return;
            // 调用方须已 EndFreeze；若仍 Modal 则不要拨（战中）
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
                else if (wp.HasRoutePresentation)
                {
                    var progress = wp.RouteAnchorProgress >= 0f
                        ? Clamp01(wp.RouteAnchorProgress)
                        : Clamp01(wp.TravelProgress);
                    wp.Mode = PartyWorldPresenceMode.RouteAnchored;
                    wp.RouteAnchorProgress = progress;
                    wp.RemainingTravelTicks = 0;
                    wp.TravelTotalTicks = 0;
                    wp.ClearRouteSegment();
                }
                else
                {
                    wp.Mode = PartyWorldPresenceMode.AtNode;
                    wp.RouteId = string.Empty;
                    wp.DestNodeId = string.Empty;
                    wp.RouteAnchorProgress = -1f;
                    wp.RemainingTravelTicks = 0;
                    wp.TravelTotalTicks = 0;
                    wp.ClearRouteSegment();
                }

                wp.ClearFollow();
                wp.ClearCombatPursuit();
            }
        }

        /// <summary>场上已无弥留／尸体时销毁残留战场（补刀／清场后调用）。</summary>
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

        /// <summary>残留战场仍有倒下者（弥留或可见尸体）。</summary>
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

            // 快照已 Clear 后仍可能有我方弥留／尸体头像钉在宏观图上
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

        /// <summary>敌军清空或我方全倒 → PostBattle（仍冻结，可点结束战斗）。</summary>
        public static void TryEnterPostBattleFromManual(SimulationWorld world)
        {
            if (world?.Strategic == null)
                return;
            if (world.Strategic.ClockFreeze.Reason != StrategicClockFreezeReason.ManualEncounter)
                return;

            var fieldCleared = StrategicEncounterSpawner.IsFieldCleared(world);
            var friendliesDown = AreAllEngagedFriendliesDown(world);
            if (!fieldCleared && !friendliesDown)
                return;

            StrategicClockFreezeService.BeginOrPromote(
                world, StrategicClockFreezeReason.PostBattle);

            if (string.IsNullOrEmpty(world.Strategic.Participants.LastBattleSummary))
            {
                world.Strategic.Participants.LastBattleSummary = fieldCleared
                    ? "敌军已清空。可继续补刀／交互；点「结束战斗」退出 Modal（有弥留则战场仍留在接战点）。"
                    : "我方已全部倒下。点「结束战斗」退出；弥留者仍留在接战点，可再派人查看。";
            }

            world.Strategic.Participants.PlayerWon = fieldCleared;
        }

        public static bool AreAllEngagedFriendliesDown(SimulationWorld world)
        {
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
        /// 参战／勾选支援者一律落到 BattleAnchor；禁止 Apply PreBattle 瞬移回家。
        /// 未参战、未勾选者不改位置。
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

                // 强制参战、已上场／已 Engaged 的支援 → BattleAnchor；
                // 仅勾选、未上场的远处支援 → 留 PreBattle（禁止瞬移到接战点，也禁止把路上人送回家）
                var mustAnchor =
                    rec.Kind == BattleParticipantKind.MandatoryFriendly ||
                    world.Strategic.Encounter.IsEngaged(rec.EntityId) ||
                    wp.Mode == PartyWorldPresenceMode.InEncounter ||
                    (rec.PreBattle != null &&
                     (rec.PreBattle.Mode == PartyWorldPresenceMode.Traveling ||
                      rec.PreBattle.Mode == PartyWorldPresenceMode.RouteAnchored ||
                      rec.PreBattle.Mode == PartyWorldPresenceMode.InEncounter));

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

            // 自动战未进过 LocalMap：把主敌栈绑到 Encounter，否则再进／残留丢失
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

            // 抽象残留栈尚无实体 → 下次进图刷弥留／尸体；已有 tracked 则复用
            var trackedCount = parkedState?.SpawnedEntityIds.Count ?? 0;
            rt.SpawnOnNextMapLoad =
                parkedStack != null &&
                parkedStack.HasDownedRemnant &&
                trackedCount <= 0;

            // 给弥留／尸体补 WorldPresence，大地图能画头像（ClearEngagedParty 前仍可读 Engaged 名单）
            EnsureFriendlyDownedWorldPresence(world, snap);
            EnsureEnemyDownedWorldPresence(world, snap, parkedState?.SpawnedEntityIds);
            ArmyPostBattleSyncService.SyncEnemyArmyAfterBattle(world, snap);

            // 退出 Modal：人不再 InEncounter，但遭遇数据保留
            rt.ClearEngagedParty();
            world.PartyWorld.EncounterId = string.Empty;

            // 卸掉 ActiveMap 遭遇会话标记：LocalMap 切回焦点节点图由 Host 处理
            if (!string.IsNullOrEmpty(world.PartyWorld.NodeId) &&
                world.WorldGraph.TryGetNode(world.PartyWorld.NodeId, out var focus) &&
                focus != null &&
                !string.IsNullOrEmpty(focus.LocalMapId))
                world.PartyWorld.LocalMapId = focus.LocalMapId;
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
        /// 残留战场再进：Participants 快照必须使用「该 Hex」的 canonical Anchor，
        /// 禁止从敌军栈 Legacy NodeId（常为 spawn 点青石荒村）推导。
        /// 仅供 Lingering re-entry；禁止用于新 Active Enemy BattleOffer。
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
            snap.BattleAnchorNodeId = ArmyHexBattleAnchorService.ResolveLegacyNodeForHex(
                world, hex, snap.BattleAnchorNodeId);
            snap.BattleAnchorRouteId = string.Empty;
            snap.BattleAnchorProgress = -1f;
            return true;
        }

        /// <summary>
        /// 把本场接战 Hex 注册为残留战场锚点。
        /// 新场 snap Hex 优先；不得用旧残留 Hex 覆盖本场 Participants。
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

        /// <summary>残留战场 Hex 查询：优先 Encounter Runtime 最新锚点，其次 Participants。</summary>
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

        /// <summary>指定 Hex 是否已注册为残留战场（支持多场 H1/H2 并存）。</summary>
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
            if (ArmyHexBattleAnchorService.IsHexAnchorMode(world))
            {
                ArmyHexBattleAnchorService.ParkStackAtBattleAnchor(world, stack, snap);
                return;
            }

            stack.RemainingTravelTicks = 0;
            stack.TravelTotalTicks = 0;
            if (!string.IsNullOrEmpty(snap.BattleAnchorRouteId) &&
                snap.BattleAnchorProgress >= 0f)
            {
                stack.RouteId = snap.BattleAnchorRouteId;
                stack.RouteAnchorProgress = snap.BattleAnchorProgress;
                stack.NodeId = snap.BattleAnchorNodeId ?? string.Empty;
                stack.DestNodeId = ResolveAnchorDest(world, snap);
            }
            else
            {
                stack.RouteId = string.Empty;
                stack.RouteAnchorProgress = -1f;
                stack.NodeId = snap.BattleAnchorNodeId ?? stack.NodeId ?? string.Empty;
                stack.DestNodeId = string.Empty;
            }
        }

        /// <summary>给已 tracked 的敌军弥留／尸体补接战点 WorldPresence（自动战宏观刷怪后亦调用）。</summary>
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

            if (spawnIds == null)
                spawnIds = BattlefieldSpawnScope.GetSpawnList(world) ?? rt.SpawnedEntityIds;

            var slot = 0;
            for (var i = 0; i < spawnIds.Count; i++)
            {
                var id = new EntityId(spawnIds[i]);
                if (!world.Entities.TryGet(id, out var ent) || ent == null)
                    continue;
                // 弥留与可见尸体都要钉在接战点（再进 LocalMap／大地图倒计时同一套实体）
                if (!LingeringBattlefieldPartyService.IsLingeringDowned(world, id))
                    continue;

                if (!world.WorldPresence.TryGet(id, out var wp) || wp == null)
                    wp = world.WorldPresence.GetOrCreate(id);

                PlaceAtBattleAnchor(world, wp, snap);
                // 微偏进度避免完全重叠（仅 legacy RouteAnchored；Hex Residual 由聚合 Marker 负责）
                if (wp.Mode == PartyWorldPresenceMode.RouteAnchored)
                {
                    var bias = (slot % 5) * 0.008f;
                    wp.RouteAnchorProgress = Clamp01(wp.RouteAnchorProgress + bias);
                    slot++;
                }
            }
        }

        /// <summary>自动战／手动战后：我方弥留／尸体钉在接战点（Restore 可能因 PreBattle 漏掉）。</summary>
        /// <summary>自动战结算弹窗期间：我方弥留／尸体钉在接战点。</summary>
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

            var slot = 0;
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
                if (wp.Mode == PartyWorldPresenceMode.RouteAnchored)
                {
                    var bias = (slot % 5) * 0.008f;
                    wp.RouteAnchorProgress = Clamp01(wp.RouteAnchorProgress + bias);
                    slot++;
                }
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

            if (!string.IsNullOrEmpty(snap.BattleAnchorRouteId) &&
                snap.BattleAnchorProgress >= 0f)
            {
                wp.Mode = PartyWorldPresenceMode.RouteAnchored;
                wp.RouteId = snap.BattleAnchorRouteId;
                wp.NodeId = snap.BattleAnchorNodeId ?? string.Empty;
                wp.DestNodeId = ResolveAnchorDest(world, snap);
                // Dest 为空时从路网补齐，否则 HasRoutePresentation=false，改点路上目标会走挂路瞬移分支
                if ((string.IsNullOrEmpty(wp.DestNodeId) ||
                     string.Equals(wp.NodeId, wp.DestNodeId, System.StringComparison.Ordinal)) &&
                    world?.WorldGraph != null &&
                    world.WorldGraph.TryGetRoute(wp.RouteId, out var route) &&
                    route != null)
                {
                    if (string.Equals(route.FromNodeId, wp.NodeId, System.StringComparison.Ordinal))
                        wp.DestNodeId = route.ToNodeId ?? string.Empty;
                    else if (string.Equals(route.ToNodeId, wp.NodeId, System.StringComparison.Ordinal))
                        wp.DestNodeId = route.FromNodeId ?? string.Empty;
                    else
                    {
                        wp.NodeId = route.FromNodeId ?? wp.NodeId;
                        wp.DestNodeId = route.ToNodeId ?? string.Empty;
                    }
                }

                wp.RouteAnchorProgress = Clamp01(snap.BattleAnchorProgress);
                wp.RemainingTravelTicks = 0;
                wp.TravelTotalTicks = 0;
                wp.ClearRouteSegment();
                return;
            }

            wp.Mode = PartyWorldPresenceMode.AtNode;
            wp.NodeId = snap.BattleAnchorNodeId ?? wp.NodeId ?? string.Empty;
            wp.RouteId = string.Empty;
            wp.DestNodeId = string.Empty;
            wp.RouteAnchorProgress = -1f;
            wp.RemainingTravelTicks = 0;
            wp.TravelTotalTicks = 0;
            wp.ClearRouteSegment();
        }

        static string ResolveAnchorDest(SimulationWorld world, BattleParticipantSnapshot snap)
        {
            if (!string.IsNullOrEmpty(snap.BattleAnchorDestNodeId))
                return snap.BattleAnchorDestNodeId;
            if (world?.WorldGraph != null &&
                !string.IsNullOrEmpty(snap.BattleAnchorRouteId) &&
                world.WorldGraph.TryGetRoute(snap.BattleAnchorRouteId, out var route) &&
                route != null)
            {
                if (string.Equals(route.FromNodeId, snap.BattleAnchorNodeId, System.StringComparison.Ordinal))
                    return route.ToNodeId ?? string.Empty;
                return route.FromNodeId ?? string.Empty;
            }

            return string.Empty;
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
