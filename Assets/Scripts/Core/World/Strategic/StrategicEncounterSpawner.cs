using System;
using System.Collections.Generic;
using System.Diagnostics;
using XianXia.Core.Attributes;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>战略手动接战：进 Encounter LocalMap 时刷敌对 NPC�?/summary>
    public static class StrategicEncounterSpawner
    {
        static readonly DefinitionId BanditGruntDef = new DefinitionId("base", "strategic_bandit_grunt");

        /// <summary>�?LocalMap 前绑�?Registry 内独�?Battlefield（Hex �?E1/E2）�?/summary>
        public static bool TryPrepareLingeringLocalMapSession(
            SimulationWorld world,
            HexCoord? hex = null)
        {
            if (world?.Strategic?.Encounter == null)
                return false;

            var rt = world.Strategic.Encounter;
            if (!string.IsNullOrEmpty(rt.PendingLingeringEnterBattlefieldId) &&
                world.Strategic.LingeringBattlefields.TryGetById(
                    rt.PendingLingeringEnterBattlefieldId, out var pending) &&
                pending != null)
            {
                LingeringBattlefieldRegistry.BeginLocalMapSession(world, pending);
                rt.PendingLingeringEnterBattlefieldId = string.Empty;
                return true;
            }

            if (hex.HasValue &&
                world.Strategic.LingeringBattlefields.TryGetAtHex(hex.Value, out var atHex) &&
                atHex != null)
            {
                LingeringBattlefieldRegistry.BeginLocalMapSession(world, atHex);
                return true;
            }

            if (ArmyHexBattleAnchorService.TryGetBattleAnchorHex(
                    world.Strategic.Participants, out var snapHex) &&
                world.Strategic.LingeringBattlefields.TryGetAtHex(snapHex, out var fromSnap) &&
                fromSnap != null)
            {
                LingeringBattlefieldRegistry.BeginLocalMapSession(world, fromSnap);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 新的 living-world battle（WORLD_COMBAT）专用 planning path —— 绝不经过旧
        /// PlanManualEncounter 的 lingering reuse 分支（ActiveBattlefieldId reuse /
        /// BattlefieldLingering reuse / LingeringBattlefields.Count reuse /
        /// stack.HasDownedRemnant → DownedOnly re-entry / HasReusableTrackedPresence reuse）。
        /// 语义：同 Hex 有历史 casualty 不影响本场 —— 本场 enemy participants 只来自当前
        /// frozen BattleParticipantSnapshot（living FormalArmy）。
        /// 保持 WORLD_COMBAT 架构原则 markPartyInEncounter = false：真实 Character 的存在
        /// 由 PlayerPartyWorldMotion / FormalArmy.WorldMotion / StrategicResidualPresence 负责，
        /// 不绑定 Lingering Registry。
        /// </summary>
        public static void PlanFreshWorldCombatManualEncounter(
            SimulationWorld world,
            string armyStackId,
            string encounterLinkId,
            IReadOnlyList<EntityId> engagedParty = null,
            int fallbackMembers = StrategicEncounterCatalog.DefaultFallbackMemberCount,
            int fallbackPowerPerMember = StrategicEncounterCatalog.DefaultFallbackCombatPower)
        {
            if (world?.Strategic == null)
                return;

            var rt = world.Strategic.Encounter;
            // 清理 Active Encounter transient（ActiveBattlefieldId / tracked / engaged /
            // SpawnOnNextMapLoad）。LingeringBattlefields Registry / StrategicResidualPresence /
            // 旧 Hex residual 是世界历史，不是本场 —— 不清。
            rt.ClearActiveEncounterSession();

            rt.ResetSpawnPlan();
            rt.SpawnOnNextMapLoad = true;
            rt.ArmyStackId = armyStackId ?? string.Empty;
            rt.EncounterLinkId = encounterLinkId ?? string.Empty;
            rt.FallbackMemberCount = Math.Max(1, fallbackMembers);
            rt.FallbackCombatPowerPerMember = Math.Max(1, fallbackPowerPerMember);
            if (engagedParty != null && engagedParty.Count > 0)
                rt.SetEngagedParty(engagedParty);
        }

        public static void PlanManualEncounter(
            SimulationWorld world,
            string armyStackId,
            string encounterLinkId,
            IReadOnlyList<EntityId> engagedParty = null,
            int fallbackMembers = StrategicEncounterCatalog.DefaultFallbackMemberCount,
            int fallbackPowerPerMember = StrategicEncounterCatalog.DefaultFallbackCombatPower,
            bool markPartyInEncounter = true)
        {
            if (world?.Strategic == null)
                return;

            var rt = world.Strategic.Encounter;
            if (!string.IsNullOrEmpty(armyStackId) &&
                !string.Equals(rt.ArmyStackId, armyStackId, StringComparison.Ordinal))
            {
                UntrackSpawnsForSessionSwitch(world);
            }
            else if (!string.IsNullOrEmpty(armyStackId))
            {
                PruneTrackedSpawnsForStack(world, armyStackId);
            }

            // 残留战场再进：保留弥留刷怪，禁止 ClearSpawned
            ArmyStack reuseStack = null;
            var lingeringReuse = !string.IsNullOrEmpty(rt.ActiveBattlefieldId);
            if (!lingeringReuse &&
                (rt.BattlefieldLingering || world.Strategic.LingeringBattlefields.Count > 0) &&
                !string.IsNullOrEmpty(armyStackId) &&
                world.Strategic.Armies.TryGet(armyStackId, out reuseStack) &&
                reuseStack != null &&
                (reuseStack.HasDownedRemnant || reuseStack.IsBattlefieldRemnant))
                lingeringReuse = true;
            else if (lingeringReuse &&
                     !string.IsNullOrEmpty(armyStackId))
                world.Strategic.Armies.TryGet(armyStackId, out reuseStack);

            if (lingeringReuse)
            {
                PruneRemovedSpawns(world);
                if (!string.IsNullOrEmpty(armyStackId))
                    PruneTrackedSpawnsForStack(world, armyStackId);
                var hasTracked = HasReusableTrackedPresence(world);
                // 自动战残留尚无实�?�?进图刷弥留；已有弥留／尸体则复用（禁止重刷刷新倒计时）
                rt.SpawnOnNextMapLoad = !hasTracked;
                // 保持 BattlefieldLingering=true：可反复再进；仅 Destroy 时清�?
                rt.FieldCleared = false;
                rt.ArmyStackId = armyStackId;
                if (engagedParty != null && engagedParty.Count > 0)
                    rt.SetEngagedParty(engagedParty);
                if (markPartyInEncounter)
                    MarkPartyInEncounter(world, engagedParty);
                ApplyStackRouteToParty(world, engagedParty, reuseStack);

                if (!hasTracked &&
                    LingeringBattlefieldParticipantService.TryGetActiveStoredParticipants(
                        world, out var activeBattlefield, out var storedParticipants) &&
                    TryPrepareStoredLingeringEnemyParticipants(
                        world,
                        storedParticipants,
                        world.Strategic.Participants) > 0)
                {
                    rt.SpawnOnNextMapLoad = false;
                    var finalIds = new List<EntityId>(8);
                    storedParticipants.CollectEnemyEntityIds(finalIds);
                    LingeringParticipantTrace.Emit(
                        world,
                        activeBattlefield?.BattleAnchorHex,
                        activeBattlefield,
                        finalIds,
                        "PlanManualEncounter.StoredParticipants");
                }

                // 已有弥留刷怪：再进时补 LocalMap 落点（人还在接战点，不能凭空消失�?
                EnsureTrackedSpawnsLocalPresentation(world);
                return;
            }

            // 残留栈再攻：BattlefieldLingering 可能已清，仍按弥留刷怪，禁止刷满血
            if (!string.IsNullOrEmpty(armyStackId) &&
                world.Strategic.Armies.TryGet(armyStackId, out var remnant) &&
                remnant != null &&
                remnant.HasDownedRemnant)
            {
                PruneRemovedSpawns(world);
                PruneTrackedSpawnsForStack(world, armyStackId);
                var hasTracked = HasReusableTrackedPresence(world);
                // 仅当完全没有场上实体时才清；有弥留／尸体绝不�?Clear（否则倒计时被刷回满）
                if (!hasTracked)
                    ClearSpawned(world);
                var keepLingerMap = rt.LingeringLocalMapId;
                rt.SpawnOnNextMapLoad = !hasTracked;
                rt.FieldCleared = false;
                // 再攻残留栈：标记为仍可反复进入的残留战场
                rt.BattlefieldLingering = true;
                rt.ArmyStackId = armyStackId;
                rt.EncounterLinkId = encounterLinkId ?? string.Empty;
                rt.FallbackMemberCount = Math.Max(
                    1,
                    Math.Max(remnant.IncapacitatedMemberCount, remnant.CorpseMemberCount));
                rt.FallbackCombatPowerPerMember = Math.Max(1, remnant.CombatPower);
                if (string.IsNullOrEmpty(rt.LingeringLocalMapId))
                    rt.LingeringLocalMapId = string.IsNullOrEmpty(keepLingerMap)
                        ? StrategicEncounterCatalog.DefaultEncounterLocalMapId
                        : keepLingerMap;
                if (engagedParty != null && engagedParty.Count > 0)
                    rt.SetEngagedParty(engagedParty);
                if (markPartyInEncounter)
                    MarkPartyInEncounter(world, engagedParty);
                ApplyStackRouteToParty(world, engagedParty, remnant);
                EnsureTrackedSpawnsLocalPresentation(world);
                return;
            }

            // 安全网：本遭遇仍有弥留／尸体实体时，禁止走「清场重刷」（会把倒计时刷回满�?
            if (!string.IsNullOrEmpty(armyStackId) &&
                string.Equals(rt.ArmyStackId, armyStackId, StringComparison.Ordinal) &&
                HasReusableTrackedPresence(world))
            {
                PruneRemovedSpawns(world);
                rt.SpawnOnNextMapLoad = false;
                rt.FieldCleared = false;
                if (engagedParty != null && engagedParty.Count > 0)
                    rt.SetEngagedParty(engagedParty);
                if (markPartyInEncounter)
                    MarkPartyInEncounter(world, engagedParty);
                if (world.Strategic.Armies.TryGet(armyStackId, out var keepStack) && keepStack != null)
                    ApplyStackRouteToParty(world, engagedParty, keepStack);
                EnsureTrackedSpawnsLocalPresentation(world);
                return;
            }

            var reuse = CanReuseLivingSpawns(world, armyStackId);
            if (!reuse)
                ClearSpawned(world);
            else
                PruneRemovedSpawns(world);

            rt.ResetSpawnPlan();
            rt.SpawnOnNextMapLoad = true;
            rt.ArmyStackId = armyStackId ?? string.Empty;
            rt.EncounterLinkId = encounterLinkId ?? string.Empty;
            rt.FallbackMemberCount = Math.Max(1, fallbackMembers);
            rt.FallbackCombatPowerPerMember = Math.Max(1, fallbackPowerPerMember);
            if (engagedParty != null && engagedParty.Count > 0)
                rt.SetEngagedParty(engagedParty);

            if (!string.IsNullOrEmpty(armyStackId) &&
                world.Strategic.Armies.TryGet(armyStackId, out var stack) &&
                stack != null)
            {
                ApplyStackRouteToParty(world, engagedParty, stack);
            }

            if (markPartyInEncounter)
                MarkPartyInEncounter(world, engagedParty);
        }

        public static void ApplyStackRouteToParty(
            SimulationWorld world,
            IReadOnlyList<EntityId> party,
            ArmyStack stack)
        {
            // Pure Hex: presence is already hex/site anchored before encounter.
        }

        static void ApplyStackRouteToPresence(WorldAgentPresence wp, ArmyStack stack)
        {
        }

        /// <summary>
        /// 遭遇战刷出的敌军倒下：同步伤亡；敌清空时标记 FieldCleared（无结算、不卸图、不弹大地图）。
        /// 新 WORLD_COMBAT：正式 participant authority = BattleParticipantSnapshot（真实 FormalArmy
        /// 已不进入 spawn scope）；legacy synthetic fallback 仍按 tracked spawn 处理。
        /// </summary>
        public static bool OnCombatantDefeated(SimulationWorld world, EntityId defenderId)
        {
            if (world?.Strategic == null || defenderId.IsNone)
                return false;

            var snap = world.Strategic.Participants;
            var isSnapshotEnemy = snap != null && snap.IsEnemyParticipant(defenderId);
            var isSnapshotFriendly = snap != null && snap.IsSelectedFriendlyParticipant(defenderId);
            var isTrackedOwnedSpawn = IsTrackedSpawn(world, defenderId);
            if (!isSnapshotEnemy && !isSnapshotFriendly && !isTrackedOwnedSpawn)
                return false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogBattleCasualty(world, defenderId, snap?.FindByEntity(defenderId));
#endif

            // 真实友军 FormalArmy participant 不属于 encounter-owned spawn。其倒地只应驱动
            // Manual 战斗是否可进入 PostBattle；不得走敌军清场、栈计数同步或 scope 清理。
            if (isSnapshotFriendly)
            {
                StrategicEncounterResolveService.TryEnterPostBattleFromManual(world);
                return true;
            }

            PruneRemovedSpawns(world);
            // 删栈前先把道路进度落到参战者身上，否则清场后路锚变�?0、无法回程／像瞬�?
            SnapshotEngagedRouteFromStack(world);
            // 真实 FormalArmy enemy 不依赖 tracked 数同步（scope 不含它，按 tracked 同步会把
            // 真实 Army count 写成 0）；正式 Army count 由 ArmyPostBattleSyncService 负责。
            if (isTrackedOwnedSpawn)
                SyncArmyStackMemberCount(world);
            TryMarkFieldCleared(world);
            if (world.Strategic.Encounter.BattlefieldLingering &&
                !StrategicEncounterResolveService.HasLingeringBattlefieldRemnants(world))
                StrategicEncounterResolveService.TryDestroyIfNoRemnants(world);
            return true;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static void LogBattleCasualty(
            SimulationWorld world,
            EntityId defenderId,
            BattleParticipantRecord record)
        {
            var name = defenderId.ToString();
            var tags = "(entity missing)";
            var lifeState = "(entity missing)";
            var npcTag = false;
            var hasPresentationOverride = false;
            if (world.Entities.TryGet(defenderId, out var entity) && entity != null)
            {
                name = string.IsNullOrEmpty(entity.DisplayName) ? defenderId.ToString() : entity.DisplayName;
                tags = entity.Tags.ToString();
                lifeState = CombatLifeStateService.ResolveLifeStateLabel(entity);
                npcTag = (entity.Tags & EntityTag.Npc) != 0;
                hasPresentationOverride = entity.TryGet<EntityLocationComponent>(out var location) &&
                                          location != null && location.HasPresentationOverride;
            }

            var formalArmyId = record?.FormalArmyId ?? string.Empty;
            var isArmyMember = ArmyService.TryGetArmyForCharacter(world, defenderId, out var army) && army != null;
            if (string.IsNullOrEmpty(formalArmyId) && isArmyMember)
                formalArmyId = army.ArmyId;

            Debug.WriteLine(
                "[BattleCasualty]" +
                " EntityId=" + defenderId +
                " Name=" + name +
                " Kind=" + (record != null ? record.Kind.ToString() : "(none)") +
                " FormalArmyId=" + formalArmyId +
                " NpcTag=" + npcTag +
                " Tags=" + tags +
                " LifeState=" + lifeState +
                " InSnapshot=" + (record != null) +
                " InEngaged=" + (world.Strategic.Encounter != null && world.Strategic.Encounter.IsEngaged(defenderId)) +
                " InLocalMapOccupants=" + world.LocalMap.ContainsOccupant(defenderId) +
                " HasPresentationOverride=" + hasPresentationOverride +
                " VisibleNow=" + StrategicEncounterHostilityService.IsVisibleOnEncounterLocalMap(world, defenderId) +
                " IsArmyMemberBeforeResolve=" + isArmyMember);
        }
#endif

        /// <summary>敌军栈尚在时，把宏观路点进度写入所有参战者�?/summary>
        public static void SnapshotEngagedRouteFromStack(SimulationWorld world)
        {
        }

        /// <summary>场上已无存活遭遇敌军且仍有参战�?�?解锁宏观移动，不弹结算�?/summary>
        public static bool TryMarkFieldCleared(SimulationWorld world)
        {
            if (world?.Strategic == null)
                return false;
            var rt = world.Strategic.Encounter;
            if (rt.FieldCleared || !rt.HasEngagedParty)
                return rt.FieldCleared;
            if (rt.SpawnOnNextMapLoad)
                return false;
            // Phase 5S：正式 participant authority = frozen BattleParticipantSnapshot
            // （真实 FormalArmy enemy）。legacy / fallback synthetic 仍按 tracked 判断。
            if (HasCombatCapableEnemyParticipant(world))
                return false;
            if (CountLivingTracked(world) > 0)
                return false;

            rt.FieldCleared = true;
            StrategicPursuitService.ClearPursuit(world);
            ArmyPostBattleSyncService.RefreshAttackerArmyFromMembers(world);
            StrategicEncounterResolveService.EnterPostBattleIfCleared(world);
            return true;
        }

        /// <summary>
        /// frozen snapshot 中是否仍有可战斗的敌方 participant（EnemyPrimary / EnemyReinforcement）。
        /// 只认 snapshot membership + 生命状态；不查 spawn scope —— 真实 FormalArmy enemy
        /// 的生命状态由实体自身决定。
        /// </summary>
        static bool HasCombatCapableEnemyParticipant(SimulationWorld world)
        {
            var snap = world?.Strategic?.Participants;
            if (snap == null)
                return false;
            for (var i = 0; i < snap.Records.Count; i++)
            {
                var rec = snap.Records[i];
                if (rec.EntityId.IsNone)
                    continue;
                if (rec.Kind != BattleParticipantKind.EnemyPrimary &&
                    rec.Kind != BattleParticipantKind.EnemyReinforcement)
                    continue;
                if (!world.Entities.TryGet(rec.EntityId, out var ent) || ent == null)
                    continue;
                if (CombatLifeStateService.CanFight(ent))
                    return true;
            }

            return false;
        }

        public static bool IsFieldCleared(SimulationWorld world) =>
            world?.Strategic?.Encounter != null && world.Strategic.Encounter.FieldCleared;

        /// <summary>清场后宏观上路：退�?Engaged，落�?AtSite／路锚（不卸 LocalMap）�?/summary>
        public static void ReleaseEngagedForMacroTravel(SimulationWorld world, EntityId id)
        {
            if (world?.Strategic == null || id.IsNone)
                return;
            if (!world.WorldPresence.TryGet(id, out var wp) || wp == null)
                return;

            var rt = world.Strategic.Encounter;
            rt.RemoveEngagedPartyMember(id);

            wp.Mode = PartyWorldPresenceMode.AtSite;
            if (!rt.HasEngagedParty)
            {
                rt.FieldCleared = false;
                rt.ArmyStackId = string.Empty;
                rt.EncounterLinkId = string.Empty;
                ClearSpawned(world);
            }
        }

        public static Result ApplyPending(SimulationWorld world)
        {
            if (world?.Strategic == null || !world.Strategic.Encounter.SpawnOnNextMapLoad)
                return Result.Success();

            world.Strategic.Encounter.SpawnOnNextMapLoad = false;
            PruneRemovedSpawns(world);

            HexCoord? requestedHex = null;
            if (ArmyHexBattleAnchorService.TryGetBattleAnchorHex(
                    world.Strategic.Participants, out var anchorHex))
                requestedHex = anchorHex;

            // Phase 5S：新鲜 WORLD_COMBAT（WorldSite / Wilderness Manual Battle）
            // —— 只认当前 frozen Participants；绝不经 stored Lingering / 残留栈 reuse。
            var kind = world.Strategic.Participants?.LocalMapResolutionKind
                       ?? BattleLocalMapResolutionKind.ExplicitEncounterMap;
            var freshWorldCombat =
                kind == BattleLocalMapResolutionKind.WorldSite ||
                kind == BattleLocalMapResolutionKind.Wilderness;

            if (!freshWorldCombat &&
                LingeringBattlefieldParticipantService.TryGetActiveStoredParticipants(
                    world, out var activeBattlefield, out var storedParticipants) &&
                TryPrepareSnapshotEnemyParticipants(
                    world,
                    storedParticipants,
                    world.Strategic.Participants,
                    FormalArmyEncounterPick.ByDomainLifeState,
                    trackInEncounterScope: true) > 0)
            {
                var finalIds = new List<EntityId>(8);
                storedParticipants.CollectEnemyEntityIds(finalIds);
                LingeringParticipantTrace.Emit(
                    world,
                    requestedHex,
                    activeBattlefield,
                    finalIds,
                    "ApplyPending.StoredParticipants");
                EmitAssemblyTraceAfterSpawn(world, finalIds);
                SyncArmyStackMemberCount(world);
                return Result.Success();
            }

            ArmyStack stack = null;
            var stackId = world.Strategic.Encounter.ArmyStackId;
            if (!string.IsNullOrEmpty(stackId))
            {
                PruneTrackedSpawnsForStack(world, stackId);
                world.Strategic.Armies.TryGet(stackId, out stack);
            }

            var anchor = BuildBattleAnchorSnapshotFromStack(world, stack);
            var isLingeringEntry = !freshWorldCombat &&
                                   (!string.IsNullOrEmpty(world.Strategic.Encounter.ActiveBattlefieldId) ||
                                    (stack != null && stack.HasDownedRemnant));
            var pick = !string.IsNullOrEmpty(world.Strategic.Encounter.ActiveBattlefieldId)
                ? FormalArmyEncounterPick.ByDomainLifeState
                : isLingeringEntry
                    ? FormalArmyEncounterPick.DownedOnly
                    : FormalArmyEncounterPick.LivingOnly;

            if (TryPrepareSnapshotEnemyParticipants(
                    world,
                    world.Strategic.Participants,
                    anchor,
                    pick,
                    trackInEncounterScope: !freshWorldCombat) > 0)
            {
                var spawnedIds = new List<EntityId>(8);
                world.Strategic.Participants.CollectEnemyEntityIds(spawnedIds);
                EmitAssemblyTraceAfterSpawn(world, spawnedIds);
                SyncArmyStackMemberCount(world);
                return Result.Success();
            }

            if (stack != null &&
                ArmyStackAdapter.TryGetFormalArmy(world, stack, out var formalArmy) &&
                TryPrepareFormalArmyEncounterEntities(
                    world,
                    stack,
                    formalArmy,
                    anchor,
                    pick,
                    trackInEncounterScope: !freshWorldCombat) > 0)
            {
                var spawnedIds = new List<EntityId>(8);
                world.Strategic.Participants.CollectEnemyEntityIds(spawnedIds);
                EmitAssemblyTraceAfterSpawn(world, spawnedIds);
                SyncArmyStackMemberCount(world);
                return Result.Success();
            }

            var living = CountLivingTracked(world);
            var incap = CountIncapacitatedTracked(world);
            var corpses = CountVisibleCorpseTracked(world);
            var targetCount = stack?.MemberCount > 0
                ? stack.MemberCount
                : world.Strategic.Encounter.FallbackMemberCount;
            if (stack != null && stack.HasCorpseRemnant)
                targetCount = Math.Max(targetCount, stack.CorpseMemberCount);
            else if (stack != null && stack.HasIncapacitatedRemnant)
                targetCount = Math.Max(targetCount, stack.IncapacitatedMemberCount);
            // 可见尸体也占坑：不能再刷新弥留把倒计时刷�?
            var toSpawn = Math.Max(0, targetCount - living - incap - corpses);
            if (toSpawn <= 0)
                return Result.Success();

            var power = stack?.CombatPower > 0
                ? stack.CombatPower
                : world.Strategic.Encounter.FallbackCombatPowerPerMember;
            var spawnAsCorpse = stack != null && stack.HasCorpseRemnant;
            var spawnAsIncap = stack != null && stack.HasIncapacitatedRemnant && !spawnAsCorpse;
            var linkId = string.IsNullOrEmpty(world.Strategic.Encounter.EncounterLinkId)
                ? world.PartyWorld.EncounterId
                : world.Strategic.Encounter.EncounterLinkId;

            var spawned = SpawnRemnantNpcEntities(
                world,
                stack,
                toSpawn,
                living + incap + corpses,
                linkId,
                power,
                spawnAsCorpse,
                spawnAsIncap);
            if (spawned.IsFailure)
                return spawned;

            SyncArmyStackMemberCount(world);
            return Result.Success();
        }

        /// <summary>
        /// 自动战未�?LocalMap：在接战点立刻刷弥留／尸体实体并�?WorldPresence�?
        /// 大地图个体头像与进图再出来一致�?
        /// </summary>
        public static void EnsureMacroRemnantSpawns(
            SimulationWorld world,
            BattleParticipantSnapshot snap)
        {
            if (world?.Strategic?.Encounter == null || snap == null)
                return;

            // Macro park 必须�?Active session，禁止污染上一�?LocalMap �?ActiveBattlefieldId scope
            world.Strategic.Encounter.ActiveBattlefieldId = string.Empty;
            PruneRemovedSpawns(world);
            var rt = world.Strategic.Encounter;

            ArmyStack stack = null;
            var stackId = rt.ArmyStackId;
            if (string.IsNullOrEmpty(stackId))
                stackId = snap.PrimaryEnemyStackId ?? string.Empty;
            if (string.IsNullOrEmpty(stackId) ||
                !world.Strategic.Armies.TryGet(stackId, out stack) ||
                stack == null ||
                !stack.HasDownedRemnant)
                return;

            if (ArmyStackAdapter.TryGetFormalArmy(world, stack, out var formalArmy) &&
                TryPrepareFormalArmyEncounterEntities(
                    world,
                    stack,
                    formalArmy,
                    snap,
                    FormalArmyEncounterPick.DownedOnly) > 0)
            {
                StrategicEncounterResolveService.RefreshEnemyDownedWorldPresence(world, snap);
                SyncArmyStackMemberCount(world);
                if (HasReusableTrackedPresence(world))
                    rt.SpawnOnNextMapLoad = false;
                return;
            }

            var living = CountLivingTracked(world);
            var incap = CountIncapacitatedTracked(world);
            var corpses = CountVisibleCorpseTracked(world);
            var targetCount = Math.Max(
                stack.MemberCount,
                stack.HasCorpseRemnant ? stack.CorpseMemberCount : stack.IncapacitatedMemberCount);
            var toSpawn = Math.Max(0, targetCount - living - incap - corpses);
            if (toSpawn > 0)
            {
                var power = stack.CombatPower > 0
                    ? stack.CombatPower
                    : rt.FallbackCombatPowerPerMember;
                var spawnAsCorpse = stack.HasCorpseRemnant;
                var spawnAsIncap = stack.HasIncapacitatedRemnant && !spawnAsCorpse;
                var linkId = string.IsNullOrEmpty(rt.EncounterLinkId)
                    ? world.PartyWorld.EncounterId
                    : rt.EncounterLinkId;
                SpawnRemnantNpcEntities(
                    world,
                    stack,
                    toSpawn,
                    living + incap + corpses,
                    linkId,
                    power,
                    spawnAsCorpse,
                    spawnAsIncap);
            }

            StrategicEncounterResolveService.RefreshEnemyDownedWorldPresence(world, snap);
            SyncArmyStackMemberCount(world);
            if (HasReusableTrackedPresence(world))
                rt.SpawnOnNextMapLoad = false;
        }

        /// <summary>
        /// �?ParticipantSnapshot 敌军记录�?LocalMap（Primary + Reinforcement EntityId）�?
        /// </summary>
        /// <summary>
        /// 是否当前处于「真实 LocalMap 手动遭遇」：WorldSite / Wilderness 且 ManualEncounter
        /// 活跃（Encounter.HasEngagedParty）。ExplicitEncounterMap 不算；战后 Participants.Clear
        /// 会把 LocalMapResolutionKind 重置回 ExplicitEncounterMap，此 gate 自动失效。
        /// </summary>
        public static bool HasActiveRealLocalMapManualEncounter(SimulationWorld world)
        {
            if (world?.Strategic?.Participants == null)
                return false;
            var kind = world.Strategic.Participants.LocalMapResolutionKind;
            if (kind != BattleLocalMapResolutionKind.WorldSite &&
                kind != BattleLocalMapResolutionKind.Wilderness)
                return false;
            return BattleOfferService.HasActiveManualEncounter(world);
        }

        /// <summary>
        /// 真实 LocalMap 手动遭遇（worldCombat）：在正确 Battle LocalMap 已加载后、enemy
        /// ApplyPending 前，给本场实际 selected Friendly（MandatoryFriendly + 勾选的
        /// OptionalFriendly，PlayerParty 之外）补 battle tactical presentation。
        /// 使用原始 EntityId / Character Entity —— 禁止 clone、禁止加入 PlayerParty、禁止为了
        /// visibility 塞进 BattlefieldSpawnScope（该 scope 有 encounter-spawn/remnant cleanup
        /// 语义，不能污染真实 Friendly FormalArmy member）。
        /// 本场 active real battle map 上，battle tactical assembly 覆盖旧 normal-world local
        /// override（上一轮 Normal Army population 可能已给 member 建过 PresentationOverride）；
        /// 只补本场所需落点，不修改 Canonical WorldPosition / FormalArmy ownership。
        /// PlayerParty 成员跳过 —— 已由 PlayerParty materializer 按 BattleHex 放置。
        /// 位置锚点与敌军同一 StartLocation 基准，slot 用独立偏移带避免与敌军重叠。
        /// </summary>
        public static void MaterializeFriendlyParticipantsForRealLocalMap(
            SimulationWorld world,
            PlayerPartyRuntime party,
            bool preserveExistingLoadedPlacement = false)
        {
            if (world?.Strategic == null)
                return;
            var snap = world.Strategic.Participants;
            if (snap == null)
                return;

            var startId = string.Empty;
            var baseX = 0f;
            var baseZ = 0f;
            if (world.WorldRegion != null)
            {
                startId = world.WorldRegion.StartLocationId ?? string.Empty;
                if (world.WorldRegion.TryGet(startId, out var startLoc) && startLoc != null)
                {
                    baseX = startLoc.PresentationX;
                    baseZ = startLoc.PresentationZ;
                }
            }

            var slot = 0;
            for (var i = 0; i < snap.Records.Count; i++)
            {
                var rec = snap.Records[i];
                if (rec.EntityId.IsNone)
                    continue;
                if (rec.Kind != BattleParticipantKind.MandatoryFriendly &&
                    !(rec.Kind == BattleParticipantKind.OptionalFriendly && rec.Selected))
                    continue;

                var id = rec.EntityId;
                // 已属于 PlayerParty 的成员由既有 party materialization 显示，不重新定位。
                if (party != null && party.IsMember(id))
                    continue;
                if (!world.Entities.TryGet(id, out var entity) || entity == null)
                    continue;

                var alreadyLoadedWithOverride = preserveExistingLoadedPlacement &&
                                                world.LocalMap.ContainsOccupant(id) &&
                                                entity.TryGet<EntityLocationComponent>(out var existingLocation) &&
                                                existingLocation != null &&
                                                existingLocation.HasPresentationOverride;

                if (!world.LocalMap.ContainsOccupant(id))
                    world.LocalMap.AddOccupant(id);

                if (alreadyLoadedWithOverride)
                    continue;

                if (!entity.TryGet<EntityLocationComponent>(out var loc) || loc == null)
                {
                    loc = new EntityLocationComponent();
                    entity.AddComponent(loc);
                }

                // 本场 active real battle map：覆盖旧 normal-world local override（不再跳过）。
                loc.LocationId = startId;
                loc.SetPresentationOverride(baseX - 4.2f - slot * 1.1f, baseZ + 2.2f);
                slot++;
            }
        }

        static int TryPrepareSnapshotEnemyParticipants(
            SimulationWorld world,
            BattleParticipantSnapshot storedParticipants,
            BattleParticipantSnapshot anchor,
            FormalArmyEncounterPick pick,
            bool trackInEncounterScope = true)
        {
            if (world?.Strategic?.Encounter == null || storedParticipants == null)
                return 0;

            PruneRemovedSpawns(world);
            var startId = world.WorldRegion.StartLocationId;
            world.WorldRegion.TryGet(startId, out var startLoc);
            var baseX = startLoc?.PresentationX ?? 0f;
            var baseZ = startLoc?.PresentationZ ?? 0f;
            var slot = 0;
            var prepared = 0;

            for (var i = 0; i < storedParticipants.Records.Count; i++)
            {
                var rec = storedParticipants.Records[i];
                if (rec.EntityId.IsNone)
                    continue;
                if (rec.Kind != BattleParticipantKind.EnemyPrimary &&
                    rec.Kind != BattleParticipantKind.EnemyReinforcement)
                    continue;
                if (!world.Entities.TryGet(rec.EntityId, out var entity) || entity == null)
                    continue;
                if (!ShouldIncludeFormalArmyMember(entity, pick))
                    continue;

                // 真实 FormalArmy participant 不是 encounter-owned spawn：生命周期由
                // FormalArmy members / ArmyPostBattleSyncService / StrategicResidualPresence
                // 负责，绝不由 BattlefieldSpawnScope.ClearSpawned() 决定生死。
                if (trackInEncounterScope && !IsTrackedSpawn(world, rec.EntityId))
                    BattlefieldSpawnScope.TrackSpawn(world, rec.EntityId.Value);

                if (!entity.TryGet<EntityLocationComponent>(out var loc) || loc == null)
                {
                    loc = new EntityLocationComponent();
                    entity.AddComponent(loc);
                }

                loc.LocationId = startId ?? string.Empty;
                loc.SetPresentationOverride(baseX + 3.5f + slot * 1.1f, baseZ + 2.2f);

                if (anchor != null)
                {
                    if (!world.WorldPresence.TryGet(rec.EntityId, out var wp) || wp == null)
                        wp = world.WorldPresence.GetOrCreate(rec.EntityId);
                    StrategicEncounterResolveService.PlaceAtBattleAnchor(world, wp, anchor);
                }

                slot++;
                prepared++;
            }

            if (prepared > 0 && anchor != null)
                StrategicEncounterResolveService.RefreshEnemyDownedWorldPresence(world, anchor);

            return prepared;
        }

        /// <summary>
        /// �?Registry 冻结 Participant Records 恢复敌军 LocalMap（禁�?Living-only / 重查 Active Army）�?
        /// </summary>
        public static int TryPrepareStoredLingeringEnemyParticipants(
            SimulationWorld world,
            BattleParticipantSnapshot storedParticipants,
            BattleParticipantSnapshot anchor) =>
            TryPrepareSnapshotEnemyParticipants(
                world,
                storedParticipants,
                anchor,
                FormalArmyEncounterPick.ByDomainLifeState);

        enum FormalArmyEncounterPick
        {
            LivingOnly,
            DownedOnly,
            ByDomainLifeState
        }

        /// <summary>
        /// FormalArmy 链接敌军：复用真实成员实体，禁止再刷 strategic_bandit_grunt 占位（否则残留再进会双倍）�?
        /// </summary>
        static int TryPrepareFormalArmyEncounterEntities(
            SimulationWorld world,
            ArmyStack stack,
            FormalArmy army,
            BattleParticipantSnapshot anchor,
            FormalArmyEncounterPick pick,
            bool trackInEncounterScope = true)
        {
            if (world?.Strategic?.Encounter == null || stack == null || army == null)
                return 0;

            PruneGenericDuplicateSpawnsForFormalArmy(world, army);

            var rt = world.Strategic.Encounter;
            var startId = world.WorldRegion.StartLocationId;
            world.WorldRegion.TryGet(startId, out var startLoc);
            var baseX = startLoc?.PresentationX ?? 0f;
            var baseZ = startLoc?.PresentationZ ?? 0f;
            var slot = 0;
            var prepared = 0;

            for (var i = 0; i < army.MemberCharacterIds.Count; i++)
            {
                var id = new EntityId(army.MemberCharacterIds[i]);
                if (id.IsNone || !world.Entities.TryGet(id, out var entity) || entity == null)
                    continue;
                if (!ShouldIncludeFormalArmyMember(entity, pick))
                    continue;

                // 真实 FormalArmy member 不是 encounter-owned spawn：禁止进入
                // BattlefieldSpawnScope（该 scope 的 ClearSpawned 会 FinalizeRemoval）。
                if (trackInEncounterScope && !IsTrackedSpawn(world, id))
                    BattlefieldSpawnScope.TrackSpawn(world, id.Value);

                if (!entity.TryGet<EntityLocationComponent>(out var loc) || loc == null)
                {
                    loc = new EntityLocationComponent();
                    entity.AddComponent(loc);
                }

                loc.LocationId = startId ?? string.Empty;
                loc.SetPresentationOverride(baseX + 3.5f + slot * 1.1f, baseZ + 2.2f);

                if (anchor != null)
                {
                    if (!world.WorldPresence.TryGet(id, out var wp) || wp == null)
                        wp = world.WorldPresence.GetOrCreate(id);
                    StrategicEncounterResolveService.PlaceAtBattleAnchor(world, wp, anchor);
                }

                slot++;
                prepared++;
            }

            if (prepared > 0 && anchor != null)
                StrategicEncounterResolveService.RefreshEnemyDownedWorldPresence(world, anchor);

            return prepared;
        }

        static void PruneGenericDuplicateSpawnsForFormalArmy(
            SimulationWorld world,
            FormalArmy army)
        {
            if (world?.Strategic?.Encounter == null || army == null)
                return;

            var scoped = BattlefieldSpawnScope.GetMutableSpawnList(world);
            if (scoped == null)
                return;

            for (var i = scoped.Count - 1; i >= 0; i--)
            {
                var raw = scoped[i];
                if (army.ContainsMember(new EntityId(raw)))
                    continue;

                var id = new EntityId(raw);
                BattlefieldSpawnScope.AssertNotCrossBattlefieldFinalize(
                    world, id, nameof(PruneGenericDuplicateSpawnsForFormalArmy));

                // 属于其他 Battlefield �?entity：禁�?FinalizeRemoval（只从当�?scope 列表摘掉引用�?
                if (BattlefieldSpawnScope.ShouldProtectFromScopedRemoval(world, id, scoped))
                {
                    BattlefieldSpawnScope.RemoveTrackedSpawnAt(world, i);
                    continue;
                }

                if (world.Entities.TryGet(id, out var entity) && entity != null)
                    CombatLifeStateService.FinalizeRemoval(world, entity);
                else
                    world.Entities.MarkRemoved(id);
                BattlefieldSpawnScope.RemoveTrackedSpawnAt(world, i);
            }
        }

        static bool ShouldIncludeFormalArmyMember(Entity entity, FormalArmyEncounterPick pick)
        {
            if (entity == null || !entity.TryGet<LifecycleComponent>(out var life) || life == null)
                return false;

            if (pick == FormalArmyEncounterPick.LivingOnly)
                return CombatLifeStateService.CanFight(entity);

            if (pick == FormalArmyEncounterPick.ByDomainLifeState)
                return !CombatLifeStateService.ShouldHideFromSpawn(entity);

            return life.IsIncapacitated ||
                   CombatLifeStateService.HasVisibleCorpse(entity);
        }

        static int CountFormalArmyLivingMembers(SimulationWorld world, FormalArmy army)
        {
            if (world == null || army == null)
                return 0;

            var count = 0;
            for (var i = 0; i < army.MemberCharacterIds.Count; i++)
            {
                var id = new EntityId(army.MemberCharacterIds[i]);
                if (id.IsNone || !world.Entities.TryGet(id, out var entity) || entity == null)
                    continue;
                if (CombatLifeStateService.CanFight(entity))
                    count++;
            }

            return count;
        }

        static BattleParticipantSnapshot BuildBattleAnchorSnapshotFromStack(
            SimulationWorld world,
            ArmyStack stack)
        {
            var snap = new BattleParticipantSnapshot();
            ArmyHexBattleAnchorService.ApplyStackBattleAnchor(world, snap, stack);
            return snap;
        }

        static Result SpawnRemnantNpcEntities(
            SimulationWorld world,
            ArmyStack stack,
            int toSpawn,
            int spawnIndexStart,
            string linkId,
            int powerPerMember,
            bool spawnAsCorpse,
            bool spawnAsIncap)
        {
            if (world?.Strategic?.Encounter == null || toSpawn <= 0)
                return Result.Success();

            var startId = world.WorldRegion.StartLocationId;
            world.WorldRegion.TryGet(startId, out var startLoc);
            var baseX = startLoc?.PresentationX ?? 0f;
            var baseZ = startLoc?.PresentationZ ?? 0f;
            var spawnIndex = spawnIndexStart;
            var power = Math.Max(1, powerPerMember);

            for (var i = 0; i < toSpawn; i++)
            {
                var faction = stack != null
                    ? StrategicFactionCatalog.DisplayName(stack.FactionId)
                    : "敌军";
                var label = (stack != null && !string.IsNullOrEmpty(stack.DisplayName)
                        ? stack.DisplayName
                        : faction) +
                    " " + (spawnIndex + 1);
                var created = world.Entities.CreateNpc(BanditGruntDef, label);
                if (created.IsFailure)
                    return Result.Failure(created.Error);

                var entity = created.Value;
                ConfigureCombatNpc(entity, power);
                if (spawnAsCorpse)
                    ApplyImmediateCorpse(world, entity);
                else if (spawnAsIncap)
                    CombatLifeStateService.TryEnterIncapacitated(world, entity);
                if (!string.IsNullOrEmpty(linkId))
                    entity.AddComponent(new EncounterLinkComponent { EncounterId = linkId });

                if (!entity.TryGet<EntityLocationComponent>(out var loc))
                {
                    loc = new EntityLocationComponent();
                    entity.AddComponent(loc);
                }

                loc.LocationId = startId ?? string.Empty;
                loc.SetPresentationOverride(baseX + 3.5f + spawnIndex * 1.1f, baseZ + 2.2f);
                BattlefieldSpawnScope.TrackSpawn(world, entity.Id.Value);
                spawnIndex++;
            }

            return Result.Success();
        }

        /// <summary>
        /// 再进残留战场：已 tracked 的敌军（含弥留）补上 LocalMap 表现坐标�?
        /// 与我方弥留同一原则——人钉在接战点，进图必须仍在场上�?
        /// </summary>
        public static void EnsureTrackedSpawnsLocalPresentation(SimulationWorld world)
        {
            if (world?.Strategic?.Encounter == null)
                return;
            PruneRemovedSpawns(world);
            var scoped = BattlefieldSpawnScope.GetMutableSpawnList(world);
            if (scoped == null)
                return;

            var startId = world.WorldRegion.StartLocationId;
            world.WorldRegion.TryGet(startId, out var startLoc);
            var baseX = startLoc?.PresentationX ?? 0f;
            var baseZ = startLoc?.PresentationZ ?? 0f;
            var slot = 0;
            for (var i = 0; i < scoped.Count; i++)
            {
                var id = new EntityId(scoped[i]);
                if (!world.Entities.TryGet(id, out var entity) || entity == null)
                    continue;
                if (CombatLifeStateService.ShouldHideFromSpawn(entity))
                    continue;

                if (!entity.TryGet<EntityLocationComponent>(out var loc) || loc == null)
                {
                    loc = new EntityLocationComponent();
                    entity.AddComponent(loc);
                }

                if (!loc.HasPresentationOverride)
                {
                    loc.LocationId = startId ?? string.Empty;
                    loc.SetPresentationOverride(baseX + 3.5f + slot * 1.1f, baseZ + 2.2f);
                }

                slot++;
            }
        }

        public static int CountLivingTrackedSpawns(SimulationWorld world) => CountLivingTracked(world);

        public static int CountIncapacitatedTrackedSpawns(SimulationWorld world) =>
            CountIncapacitatedTracked(world);

        static int CountIncapacitatedTracked(SimulationWorld world)
        {
            if (world?.Strategic == null)
                return 0;
            PruneRemovedSpawns(world);
            var scoped = BattlefieldSpawnScope.GetSpawnList(world);
            if (scoped == null)
                return 0;
            var count = 0;
            for (var i = 0; i < scoped.Count; i++)
            {
                var id = new EntityId(scoped[i]);
                if (!world.Entities.TryGet(id, out var entity) || entity == null)
                    continue;
                if (entity.TryGet<LifecycleComponent>(out var life) && life.IsIncapacitated)
                    count++;
            }

            return count;
        }

        static int CountVisibleCorpseTracked(SimulationWorld world)
        {
            if (world?.Strategic == null)
                return 0;
            PruneRemovedSpawns(world);
            var scoped = BattlefieldSpawnScope.GetSpawnList(world);
            if (scoped == null)
                return 0;
            var count = 0;
            for (var i = 0; i < scoped.Count; i++)
            {
                var id = new EntityId(scoped[i]);
                if (!world.Entities.TryGet(id, out var entity) || entity == null)
                    continue;
                if (CombatLifeStateService.HasVisibleCorpse(entity))
                    count++;
            }

            return count;
        }


        /// <summary>场上仍有可复用的遭遇体（活／弥留／可见尸体）——再进时禁止清掉重刷�?/summary>
        public static bool HasReusableTrackedPresence(SimulationWorld world) =>
            CountLivingTracked(world) > 0 ||
            CountIncapacitatedTracked(world) > 0 ||
            CountVisibleCorpseTracked(world) > 0;

        /// <summary>自动战未处决：把已刷新的存活敌军同步为弥留（与栈�?IncapacitatedMemberCount 一致）�?/summary>
        public static void ApplyIncapacitatedToLivingTrackedSpawns(SimulationWorld world)
        {
            if (world?.Strategic == null)
                return;
            PruneRemovedSpawns(world);
            var scoped = BattlefieldSpawnScope.GetSpawnList(world);
            if (scoped == null)
                return;
            for (var i = 0; i < scoped.Count; i++)
            {
                var id = new EntityId(scoped[i]);
                if (!world.Entities.TryGet(id, out var entity) || entity == null)
                    continue;
                if (!CombatLifeStateService.CanFight(entity))
                    continue;
                CombatLifeStateService.TryEnterIncapacitated(world, entity);
            }

            SyncArmyStackMemberCount(world);
        }

        /// <summary>自动战处决：把已刷新的存活／弥留敌军同步为尸体（与栈�?CorpseMemberCount 一致）�?/summary>
        public static void ApplyCorpseToLivingTrackedSpawns(SimulationWorld world)
        {
            if (world?.Strategic == null)
                return;
            PruneRemovedSpawns(world);
            var scoped = BattlefieldSpawnScope.GetSpawnList(world);
            if (scoped == null)
                return;
            for (var i = 0; i < scoped.Count; i++)
            {
                var id = new EntityId(scoped[i]);
                if (!world.Entities.TryGet(id, out var entity) || entity == null)
                    continue;
                if (CombatLifeStateService.HasVisibleCorpse(entity))
                    continue;
                ApplyImmediateCorpse(world, entity);
            }

            SyncArmyStackMemberCount(world);
        }

        static void ApplyImmediateCorpse(SimulationWorld world, Entity entity)
        {
            if (entity == null)
                return;
            CombatDamageRules.EnsureVitals(entity);
            if (entity.TryGet<CombatVitalsComponent>(out var vitals))
                vitals.CurrentHp = 0;
            CombatLifeStateService.TryConfirmDeath(world, EntityId.None, entity, out _);
        }

        public static void ClearSpawned(SimulationWorld world)
        {
            if (world?.Strategic == null)
                return;
            var scoped = BattlefieldSpawnScope.GetMutableSpawnList(world);
            if (scoped == null)
                return;
            for (var i = scoped.Count - 1; i >= 0; i--)
            {
                var id = new EntityId(scoped[i]);
                BattlefieldSpawnScope.AssertNotCrossBattlefieldFinalize(
                    world, id, nameof(ClearSpawned));
                if (BattlefieldSpawnScope.ShouldProtectFromScopedRemoval(world, id, scoped))
                {
                    BattlefieldSpawnScope.RemoveTrackedSpawnAt(world, i);
                    continue;
                }

                if (world.Entities.TryGet(id, out var entity) && entity != null)
                    CombatLifeStateService.FinalizeRemoval(world, entity);
                else
                    world.Entities.MarkRemoved(id);
                BattlefieldSpawnScope.RemoveTrackedSpawnAt(world, i);
            }
        }

        static void EmitAssemblyTraceAfterSpawn(SimulationWorld world, IList<EntityId> spawnedEnemyIds)
        {
            ArmyStack stack = null;
            var stackId = world?.Strategic?.Encounter?.ArmyStackId;
            if (!string.IsNullOrEmpty(stackId))
                world.Strategic.Armies.TryGet(stackId, out stack);

            var finalActors = new List<EntityId>(8);
            var scoped = BattlefieldSpawnScope.GetSpawnList(world);
            if (scoped != null)
            {
                for (var i = 0; i < scoped.Count; i++)
                    finalActors.Add(new EntityId(scoped[i]));
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            EncounterAssemblyTrace.Emit(
                world,
                stack,
                "ApplyPending",
                spawnedEnemyIds,
                finalActors);
#endif
        }

        public static bool IsTrackedSpawn(SimulationWorld world, EntityId id) =>
            BattlefieldSpawnScope.IsTrackedInCurrentScope(world, id);

        static bool CanReuseLivingSpawns(SimulationWorld world, string armyStackId)
        {
            if (world?.Strategic == null || string.IsNullOrEmpty(armyStackId))
                return false;
            var rt = world.Strategic.Encounter;
            if (!string.Equals(rt.ArmyStackId, armyStackId, StringComparison.Ordinal))
                return false;
            PruneRemovedSpawns(world);
            return CountLivingTracked(world) > 0;
        }

        static int CountLivingTracked(SimulationWorld world)
        {
            if (world?.Strategic == null)
                return 0;
            PruneRemovedSpawns(world);
            var scoped = BattlefieldSpawnScope.GetSpawnList(world);
            if (scoped == null)
                return 0;
            var count = 0;
            for (var i = 0; i < scoped.Count; i++)
            {
                var id = new EntityId(scoped[i]);
                if (!world.Entities.TryGet(id, out var entity) || entity == null)
                    continue;
                if (entity.TryGet<LifecycleComponent>(out var life) &&
                    life.State == LifecycleState.Alive)
                    count++;
            }

            return count;
        }

        /// <summary>尸体腐烂／Removed 后：修剪刷怪追踪，并同步敌军栈（无可见残留则从大地图移除）�?/summary>
        public static void ReconcileAfterLifeDecay(SimulationWorld world, EntityId removedId)
        {
            if (world?.Strategic == null)
                return;

            var wasTracked = !removedId.IsNone && IsTrackedSpawn(world, removedId);
            PruneRemovedSpawns(world);
            if (!wasTracked)
                return;

            SyncArmyStackMemberCount(world);

            // 该遭遇栈追踪的尸体已全部腐烂：连抽象弥留标记一并清掉，大地图不再留敌军�?
            var leftover = CountLivingTracked(world) +
                           CountIncapacitatedTracked(world) +
                           CountVisibleCorpseTracked(world);
            if (leftover > 0)
                return;

            var stackId = world.Strategic.Encounter.ArmyStackId;
            if (string.IsNullOrEmpty(stackId) ||
                !world.Strategic.Armies.TryGet(stackId, out var stack) ||
                stack == null)
                return;

            stack.IncapacitatedMemberCount = 0;
            stack.CorpseMemberCount = 0;
            stack.IsBattlefieldRemnant = false;
            world.Strategic.Armies.Remove(stackId);
        }

        static void PruneRemovedSpawns(SimulationWorld world)
        {
            if (world?.Strategic == null)
                return;
            var scoped = BattlefieldSpawnScope.GetMutableSpawnList(world);
            if (scoped == null)
                return;
            for (var i = scoped.Count - 1; i >= 0; i--)
            {
                var id = new EntityId(scoped[i]);
                if (!world.Entities.TryGet(id, out var entity) || entity == null)
                {
                    BattlefieldSpawnScope.RemoveTrackedSpawnAt(world, i);
                    continue;
                }

                if (entity.TryGet<LifecycleComponent>(out var life) && life.IsRemoved)
                    BattlefieldSpawnScope.RemoveTrackedSpawnAt(world, i);
            }
        }

        /// <summary>
        /// 切换／再进目标栈时：剔除不属于该 FormalArmy �?tracked 占位（仅当前 Encounter scope）�?
        /// </summary>
        public static void PruneTrackedSpawnsForStack(SimulationWorld world, string armyStackId)
        {
            if (world?.Strategic?.Encounter == null || string.IsNullOrEmpty(armyStackId))
                return;
            if (!world.Strategic.Armies.TryGet(armyStackId, out var stack) || stack == null)
                return;
            if (!ArmyStackAdapter.TryGetFormalArmy(world, stack, out var army) || army == null)
                return;

            PruneRemovedSpawns(world);
            var scoped = BattlefieldSpawnScope.GetMutableSpawnList(world);
            if (scoped == null)
                return;
            for (var i = scoped.Count - 1; i >= 0; i--)
            {
                var id = new EntityId(scoped[i]);
                if (army.ContainsMember(id))
                    continue;

                BattlefieldSpawnScope.AssertNotCrossBattlefieldFinalize(
                    world, id, nameof(PruneTrackedSpawnsForStack));
                if (world.Entities.TryGet(id, out var entity) && entity != null)
                    CombatLifeStateService.FinalizeRemoval(world, entity);
                else
                    world.Entities.MarkRemoved(id);
                BattlefieldSpawnScope.RemoveTrackedSpawnAt(world, i);
            }
        }

        /// <summary>切换 Active Encounter 栈：清空 Active scope spawns（Registry 内已 park 的不动）�?/summary>
        static void UntrackSpawnsForSessionSwitch(SimulationWorld world)
        {
            if (world?.Strategic?.Encounter == null)
                return;

            PruneRemovedSpawns(world);
            BattlefieldSpawnScope.ClearScopedSpawns(world);
        }

        static void SyncArmyStackMemberCount(SimulationWorld world)
        {
            if (world?.Strategic == null)
                return;
            var stackId = world.Strategic.Encounter.ArmyStackId;
            if (string.IsNullOrEmpty(stackId) ||
                !world.Strategic.Armies.TryGet(stackId, out var stack) ||
                stack == null)
                return;

            if (ArmyStackAdapter.TryGetFormalArmy(world, stack, out var formalArmy))
            {
                var living = CountFormalArmyLivingMembers(world, formalArmy);
                var incap = ArmyStackAdapter.GetIncapacitatedMemberCount(world, stack);
                var corpses = ArmyStackAdapter.GetCorpseMemberCount(world, stack);
                var total = living + incap + corpses;
                if (total > 0)
                {
                    stack.MemberCount = total;
                    stack.IncapacitatedMemberCount = incap;
                    stack.CorpseMemberCount = corpses;
                    stack.IsBattlefieldRemnant = living == 0 && (incap > 0 || corpses > 0);
                    ArmyStackAdapter.RefreshDerivedPresentation(world, stack);
                    return;
                }
            }

            var livingTracked = CountLivingTracked(world);
            var incapTracked = CountIncapacitatedTracked(world);
            var corpsesTracked = CountVisibleCorpseTracked(world);
            var totalTracked = livingTracked + incapTracked + corpsesTracked;
            if (totalTracked > 0)
            {
                stack.MemberCount = totalTracked;
                stack.IncapacitatedMemberCount = incapTracked;
                stack.CorpseMemberCount = corpsesTracked;
                stack.IsBattlefieldRemnant = livingTracked == 0;
                return;
            }

            // 尚无刷怪实体，但自动战已记下残留人�?�?保留抽象�?
            if (stack.HasDownedRemnant)
            {
                stack.MemberCount = Math.Max(1, Math.Max(stack.IncapacitatedMemberCount, stack.CorpseMemberCount));
                return;
            }

            stack.IncapacitatedMemberCount = 0;
            stack.CorpseMemberCount = 0;
            stack.IsBattlefieldRemnant = false;
            world.Strategic.Armies.Remove(stackId);
        }

        static void MarkPartyInEncounter(SimulationWorld world, IReadOnlyList<EntityId> party)
        {
            if (world == null || party == null)
                return;
            for (var i = 0; i < party.Count; i++)
            {
                var id = party[i];
                if (id.IsNone || !world.WorldPresence.TryGet(id, out var wp) || wp == null)
                    continue;
                if (wp.Mode == PartyWorldPresenceMode.AtSite ||
                    wp.Mode == PartyWorldPresenceMode.AtHex ||
                    wp.Mode == PartyWorldPresenceMode.InEncounter)
                {
                    wp.Mode = PartyWorldPresenceMode.InEncounter;
                }
            }
        }

        static void ConfigureCombatNpc(Entity entity, int combatPowerPerMember)
        {
            var power = Math.Max(1, combatPowerPerMember);
            if (!entity.TryGet<AttributesComponent>(out var attrs) || attrs == null)
                return;
            attrs.SetBase(AttributeId.Attack, 6 + power * 3);
            attrs.SetBase(AttributeId.Defense, power);
            attrs.SetBase(AttributeId.MaxHp, 30 + power * 15);
            attrs.SetBase(AttributeId.Speed, 8);
            CombatDamageRules.EnsureVitals(entity);

            if (!entity.TryGet<PersonalityProfileComponent>(out var profile) || profile == null)
            {
                profile = new PersonalityProfileComponent();
                entity.AddComponent(profile);
            }

            profile.AddTag("hostile");
        }
    }
}
