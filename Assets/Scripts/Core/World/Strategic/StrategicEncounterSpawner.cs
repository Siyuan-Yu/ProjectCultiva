using System;
using System.Collections.Generic;
using XianXia.Core.Attributes;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>战略手动接战：进 Encounter LocalMap 时刷敌对 NPC。</summary>
    public static class StrategicEncounterSpawner
    {
        static readonly DefinitionId BanditGruntDef = new DefinitionId("base", "strategic_bandit_grunt");

        public static void PlanManualEncounter(
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
            // 残留战场再进：保留弥留刷怪，禁止 ClearSpawned
            var lingeringReuse = rt.BattlefieldLingering &&
                                 (string.IsNullOrEmpty(armyStackId) ||
                                  string.Equals(rt.ArmyStackId, armyStackId, StringComparison.Ordinal));
            if (lingeringReuse)
            {
                PruneRemovedSpawns(world);
                var hasTracked = HasReusableTrackedPresence(world);
                // 自动战残留尚无实体 → 进图刷弥留；已有弥留／尸体则复用（禁止重刷刷新倒计时）
                rt.SpawnOnNextMapLoad = !hasTracked;
                // 保持 BattlefieldLingering=true：可反复再进；仅 Destroy 时清除
                rt.FieldCleared = false;
                if (engagedParty != null && engagedParty.Count > 0)
                    rt.SetEngagedParty(engagedParty);
                MarkPartyInEncounter(world, engagedParty);
                if (!string.IsNullOrEmpty(rt.ArmyStackId) &&
                    world.Strategic.Armies.TryGet(rt.ArmyStackId, out var lingerStack) &&
                    lingerStack != null)
                    ApplyStackRouteToParty(world, engagedParty, lingerStack);

                // 已有弥留刷怪：再进时补 LocalMap 落点（人还在接战点，不能凭空消失）
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
                var hasTracked = HasReusableTrackedPresence(world);
                // 仅当完全没有场上实体时才清；有弥留／尸体绝不能 Clear（否则倒计时被刷回满）
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
                MarkPartyInEncounter(world, engagedParty);
                ApplyStackRouteToParty(world, engagedParty, remnant);
                EnsureTrackedSpawnsLocalPresentation(world);
                return;
            }

            // 安全网：本遭遇仍有弥留／尸体实体时，禁止走「清场重刷」（会把倒计时刷回满）
            if (!string.IsNullOrEmpty(armyStackId) &&
                string.Equals(rt.ArmyStackId, armyStackId, StringComparison.Ordinal) &&
                HasReusableTrackedPresence(world))
            {
                PruneRemovedSpawns(world);
                rt.SpawnOnNextMapLoad = false;
                rt.FieldCleared = false;
                if (engagedParty != null && engagedParty.Count > 0)
                    rt.SetEngagedParty(engagedParty);
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
        }

        public static void ApplyStackRouteToParty(
            SimulationWorld world,
            IReadOnlyList<EntityId> party,
            ArmyStack stack)
        {
            if (world == null || stack == null || party == null || !stack.IsRoutePositioned)
                return;

            for (var i = 0; i < party.Count; i++)
            {
                var id = party[i];
                if (id.IsNone || !world.WorldPresence.TryGet(id, out var wp) || wp == null)
                    continue;

                ApplyStackRouteToPresence(wp, stack);
            }
        }

        static void ApplyStackRouteToPresence(WorldAgentPresence wp, ArmyStack stack)
        {
            if (wp == null || stack == null || !stack.IsRoutePositioned)
                return;

            wp.NodeId = stack.NodeId ?? string.Empty;
            wp.DestNodeId = stack.DestNodeId ?? string.Empty;
            wp.RouteId = stack.RouteId ?? string.Empty;
            if (stack.IsRouteAnchored)
            {
                wp.RouteAnchorProgress = stack.GetRouteDisplayProgress();
                wp.RemainingTravelTicks = 0;
                wp.TravelTotalTicks = 0;
                wp.ClearRouteSegment();
                wp.Mode = PartyWorldPresenceMode.InEncounter;
            }
            else if (stack.IsTraveling)
            {
                wp.Mode = PartyWorldPresenceMode.InEncounter;
                wp.TravelTotalTicks = Math.Max(1, stack.TravelTotalTicks);
                wp.RemainingTravelTicks = Math.Max(0, stack.RemainingTravelTicks);
                wp.RouteAnchorProgress = -1f;
                wp.ClearRouteSegment();
            }
        }

        /// <summary>
        /// 遭遇战刷出的敌军倒下：同步伤亡；敌清空时标记 FieldCleared（无结算、不卸图、不弹大地图）。
        /// </summary>
        public static bool OnCombatantDefeated(SimulationWorld world, EntityId defenderId)
        {
            if (world?.Strategic == null || defenderId.IsNone || !IsTrackedSpawn(world, defenderId))
                return false;
            PruneRemovedSpawns(world);
            // 删栈前先把道路进度落到参战者身上，否则清场后路锚变成 0、无法回程／像瞬移
            SnapshotEngagedRouteFromStack(world);
            SyncArmyStackMemberCount(world);
            TryMarkFieldCleared(world);
            if (world.Strategic.Encounter.BattlefieldLingering &&
                !StrategicEncounterResolveService.HasLingeringBattlefieldRemnants(world))
                StrategicEncounterResolveService.TryDestroyIfNoRemnants(world);
            return true;
        }

        /// <summary>敌军栈尚在时，把宏观路点进度写入所有参战者。</summary>
        public static void SnapshotEngagedRouteFromStack(SimulationWorld world)
        {
            if (world?.Strategic == null)
                return;
            var rt = world.Strategic.Encounter;
            if (string.IsNullOrEmpty(rt.ArmyStackId) || !rt.HasEngagedParty)
                return;
            if (!world.Strategic.Armies.TryGet(rt.ArmyStackId, out var stack) || stack == null)
                return;
            if (!stack.IsRoutePositioned)
                return;

            for (var i = 0; i < rt.EngagedPartyIds.Count; i++)
            {
                var id = new EntityId(rt.EngagedPartyIds[i]);
                if (id.IsNone || !world.WorldPresence.TryGet(id, out var wp) || wp == null)
                    continue;
                ApplyStackRouteToPresence(wp, stack);
            }
        }

        /// <summary>场上已无存活遭遇敌军且仍有参战者 → 解锁宏观移动，不弹结算。</summary>
        public static bool TryMarkFieldCleared(SimulationWorld world)
        {
            if (world?.Strategic == null)
                return false;
            var rt = world.Strategic.Encounter;
            if (rt.FieldCleared || !rt.HasEngagedParty)
                return rt.FieldCleared;
            if (rt.SpawnOnNextMapLoad)
                return false;
            if (CountLivingTracked(world) > 0)
                return false;

            rt.FieldCleared = true;
            StrategicPursuitService.ClearPursuit(world);
            ArmyPostBattleSyncService.RefreshAttackerArmyFromMembers(world);
            StrategicEncounterResolveService.EnterPostBattleIfCleared(world);
            return true;
        }

        public static bool IsFieldCleared(SimulationWorld world) =>
            world?.Strategic?.Encounter != null && world.Strategic.Encounter.FieldCleared;

        /// <summary>清场后宏观上路：退出 Engaged，落回 AtNode／路锚（不卸 LocalMap）。</summary>
        public static void ReleaseEngagedForMacroTravel(SimulationWorld world, EntityId id)
        {
            if (world?.Strategic == null || id.IsNone)
                return;
            if (!world.WorldPresence.TryGet(id, out var wp) || wp == null)
                return;

            var rt = world.Strategic.Encounter;
            rt.RemoveEngagedPartyMember(id);

            if (!string.IsNullOrEmpty(wp.RouteId) &&
                !string.IsNullOrEmpty(wp.DestNodeId) &&
                !string.Equals(wp.NodeId, wp.DestNodeId, StringComparison.Ordinal))
            {
                wp.AnchorOnRoute(ResolveMacroRouteProgress(wp));
            }
            else
            {
                wp.Mode = PartyWorldPresenceMode.AtNode;
                wp.RemainingTravelTicks = 0;
                wp.TravelTotalTicks = 0;
                wp.RouteAnchorProgress = -1f;
                wp.ClearRouteSegment();
                if (string.IsNullOrEmpty(wp.DestNodeId) ||
                    string.Equals(wp.NodeId, wp.DestNodeId, StringComparison.Ordinal))
                {
                    wp.RouteId = string.Empty;
                    wp.DestNodeId = string.Empty;
                }
            }

            if (!rt.HasEngagedParty)
            {
                rt.FieldCleared = false;
                rt.ArmyStackId = string.Empty;
                rt.EncounterLinkId = string.Empty;
                ClearSpawned(world);
            }
        }

        /// <summary>
        /// 释放上路用的道路进度。丢失进度时用 0.5，避免钉在 0／1 导致「回不去出发端」或端点瞬移。
        /// </summary>
        public static float ResolveMacroRouteProgress(WorldAgentPresence wp)
        {
            if (wp == null)
                return 0.5f;
            if (wp.RouteAnchorProgress >= 0f && wp.RouteAnchorProgress <= 1f)
                return wp.RouteAnchorProgress;
            if (wp.TravelTotalTicks > 0)
            {
                var t = wp.TravelProgress;
                if (t < 0f)
                    return 0.5f;
                if (t > 1f)
                    return 1f;
                return t;
            }

            return 0.5f;
        }

        /// <summary>进 Encounter 图前：保留宏观路进度，再清旅行 tick。</summary>
        public static void PreserveRouteProgressForEncounter(WorldAgentPresence wp)
        {
            if (wp == null)
                return;
            if (wp.RouteAnchorProgress < 0f &&
                !string.IsNullOrEmpty(wp.RouteId) &&
                !string.IsNullOrEmpty(wp.DestNodeId))
            {
                if (wp.TravelTotalTicks > 0)
                    wp.RouteAnchorProgress = Math.Max(0f, Math.Min(1f, wp.TravelProgress));
                else
                    wp.RouteAnchorProgress = 0.5f;
            }

            wp.RemainingTravelTicks = 0;
            wp.TravelTotalTicks = 0;
            wp.ClearRouteSegment();
            wp.Mode = PartyWorldPresenceMode.InEncounter;
        }

        public static Result ApplyPending(SimulationWorld world)
        {
            if (world?.Strategic == null || !world.Strategic.Encounter.SpawnOnNextMapLoad)
                return Result.Success();

            world.Strategic.Encounter.SpawnOnNextMapLoad = false;
            PruneRemovedSpawns(world);

            ArmyStack stack = null;
            var stackId = world.Strategic.Encounter.ArmyStackId;
            if (!string.IsNullOrEmpty(stackId))
                world.Strategic.Armies.TryGet(stackId, out stack);

            if (stack != null &&
                ArmyStackAdapter.TryGetFormalArmy(world, stack, out var formalArmy) &&
                TryPrepareFormalArmyEncounterEntities(
                    world,
                    stack,
                    formalArmy,
                    BuildBattleAnchorSnapshotFromStack(world, stack),
                    stack.HasDownedRemnant
                        ? FormalArmyEncounterPick.DownedOnly
                        : FormalArmyEncounterPick.LivingOnly) > 0)
            {
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
            // 可见尸体也占坑：不能再刷新弥留把倒计时刷满
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
        /// 自动战未进 LocalMap：在接战点立刻刷弥留／尸体实体并钉 WorldPresence，
        /// 大地图个体头像与进图再出来一致。
        /// </summary>
        public static void EnsureMacroRemnantSpawns(
            SimulationWorld world,
            BattleParticipantSnapshot snap)
        {
            if (world?.Strategic?.Encounter == null || snap == null)
                return;

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

        enum FormalArmyEncounterPick
        {
            LivingOnly,
            DownedOnly
        }

        /// <summary>
        /// FormalArmy 链接敌军：复用真实成员实体，禁止再刷 strategic_bandit_grunt 占位（否则残留再进会双倍）。
        /// </summary>
        static int TryPrepareFormalArmyEncounterEntities(
            SimulationWorld world,
            ArmyStack stack,
            FormalArmy army,
            BattleParticipantSnapshot anchor,
            FormalArmyEncounterPick pick)
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

                if (!IsTrackedSpawn(world, id))
                    rt.TrackSpawn(id.Value);

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

            var rt = world.Strategic.Encounter;
            for (var i = rt.SpawnedEntityIds.Count - 1; i >= 0; i--)
            {
                var raw = rt.SpawnedEntityIds[i];
                if (army.ContainsMember(new EntityId(raw)))
                    continue;

                var id = new EntityId(raw);
                if (world.Entities.TryGet(id, out var entity) && entity != null)
                    CombatLifeStateService.FinalizeRemoval(world, entity);
                else
                    world.Entities.MarkRemoved(id);
                rt.RemoveTrackedSpawnAt(i);
            }
        }

        static bool ShouldIncludeFormalArmyMember(Entity entity, FormalArmyEncounterPick pick)
        {
            if (entity == null || !entity.TryGet<LifecycleComponent>(out var life) || life == null)
                return false;

            if (pick == FormalArmyEncounterPick.LivingOnly)
                return CombatLifeStateService.CanFight(entity);

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
                world.Strategic.Encounter.TrackSpawn(entity.Id.Value);
                spawnIndex++;
            }

            return Result.Success();
        }

        /// <summary>
        /// 再进残留战场：已 tracked 的敌军（含弥留）补上 LocalMap 表现坐标。
        /// 与我方弥留同一原则——人钉在接战点，进图必须仍在场上。
        /// </summary>
        public static void EnsureTrackedSpawnsLocalPresentation(SimulationWorld world)
        {
            if (world?.Strategic?.Encounter == null)
                return;
            PruneRemovedSpawns(world);
            var rt = world.Strategic.Encounter;
            var startId = world.WorldRegion.StartLocationId;
            world.WorldRegion.TryGet(startId, out var startLoc);
            var baseX = startLoc?.PresentationX ?? 0f;
            var baseZ = startLoc?.PresentationZ ?? 0f;
            var slot = 0;
            for (var i = 0; i < rt.SpawnedEntityIds.Count; i++)
            {
                var id = new EntityId(rt.SpawnedEntityIds[i]);
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
            var rt = world.Strategic.Encounter;
            var count = 0;
            for (var i = 0; i < rt.SpawnedEntityIds.Count; i++)
            {
                var id = new EntityId(rt.SpawnedEntityIds[i]);
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
            var rt = world.Strategic.Encounter;
            var count = 0;
            for (var i = 0; i < rt.SpawnedEntityIds.Count; i++)
            {
                var id = new EntityId(rt.SpawnedEntityIds[i]);
                if (!world.Entities.TryGet(id, out var entity) || entity == null)
                    continue;
                if (CombatLifeStateService.HasVisibleCorpse(entity))
                    count++;
            }

            return count;
        }


        /// <summary>场上仍有可复用的遭遇体（活／弥留／可见尸体）——再进时禁止清掉重刷。</summary>
        public static bool HasReusableTrackedPresence(SimulationWorld world) =>
            CountLivingTracked(world) > 0 ||
            CountIncapacitatedTracked(world) > 0 ||
            CountVisibleCorpseTracked(world) > 0;

        /// <summary>自动战未处决：把已刷新的存活敌军同步为弥留（与栈上 IncapacitatedMemberCount 一致）。</summary>
        public static void ApplyIncapacitatedToLivingTrackedSpawns(SimulationWorld world)
        {
            if (world?.Strategic == null)
                return;
            PruneRemovedSpawns(world);
            var rt = world.Strategic.Encounter;
            for (var i = 0; i < rt.SpawnedEntityIds.Count; i++)
            {
                var id = new EntityId(rt.SpawnedEntityIds[i]);
                if (!world.Entities.TryGet(id, out var entity) || entity == null)
                    continue;
                if (!CombatLifeStateService.CanFight(entity))
                    continue;
                CombatLifeStateService.TryEnterIncapacitated(world, entity);
            }

            SyncArmyStackMemberCount(world);
        }

        /// <summary>自动战处决：把已刷新的存活／弥留敌军同步为尸体（与栈上 CorpseMemberCount 一致）。</summary>
        public static void ApplyCorpseToLivingTrackedSpawns(SimulationWorld world)
        {
            if (world?.Strategic == null)
                return;
            PruneRemovedSpawns(world);
            var rt = world.Strategic.Encounter;
            for (var i = 0; i < rt.SpawnedEntityIds.Count; i++)
            {
                var id = new EntityId(rt.SpawnedEntityIds[i]);
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
            var rt = world.Strategic.Encounter;
            for (var i = 0; i < rt.SpawnedEntityIds.Count; i++)
            {
                var id = new EntityId(rt.SpawnedEntityIds[i]);
                if (world.Entities.TryGet(id, out var entity) && entity != null)
                    CombatLifeStateService.FinalizeRemoval(world, entity);
                else
                    world.Entities.MarkRemoved(id);
            }

            rt.ClearTrackedIds();
        }

        public static bool IsTrackedSpawn(SimulationWorld world, EntityId id)
        {
            if (world?.Strategic?.Encounter == null || id.IsNone)
                return false;
            var spawned = world.Strategic.Encounter.SpawnedEntityIds;
            for (var i = 0; i < spawned.Count; i++)
            {
                if (spawned[i] == id.Value)
                    return true;
            }

            return false;
        }

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
            var rt = world.Strategic.Encounter;
            var count = 0;
            for (var i = 0; i < rt.SpawnedEntityIds.Count; i++)
            {
                var id = new EntityId(rt.SpawnedEntityIds[i]);
                if (!world.Entities.TryGet(id, out var entity) || entity == null)
                    continue;
                if (entity.TryGet<LifecycleComponent>(out var life) &&
                    life.State == LifecycleState.Alive)
                    count++;
            }

            return count;
        }

        /// <summary>尸体腐烂／Removed 后：修剪刷怪追踪，并同步敌军栈（无可见残留则从大地图移除）。</summary>
        public static void ReconcileAfterLifeDecay(SimulationWorld world, EntityId removedId)
        {
            if (world?.Strategic == null)
                return;

            var wasTracked = !removedId.IsNone && IsTrackedSpawn(world, removedId);
            PruneRemovedSpawns(world);
            if (!wasTracked)
                return;

            SyncArmyStackMemberCount(world);

            // 该遭遇栈追踪的尸体已全部腐烂：连抽象弥留标记一并清掉，大地图不再留敌军点
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
            var rt = world.Strategic.Encounter;
            for (var i = rt.SpawnedEntityIds.Count - 1; i >= 0; i--)
            {
                var id = new EntityId(rt.SpawnedEntityIds[i]);
                if (!world.Entities.TryGet(id, out var entity) || entity == null)
                {
                    rt.RemoveTrackedSpawnAt(i);
                    continue;
                }

                if (entity.TryGet<LifecycleComponent>(out var life) && life.IsRemoved)
                    rt.RemoveTrackedSpawnAt(i);
            }
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

            // 尚无刷怪实体，但自动战已记下残留人数 → 保留抽象栈
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
                if (wp.Mode == PartyWorldPresenceMode.Traveling ||
                    wp.Mode == PartyWorldPresenceMode.RouteAnchored ||
                    wp.Mode == PartyWorldPresenceMode.AtNode ||
                    wp.Mode == PartyWorldPresenceMode.InEncounter)
                {
                    // 保留路锚坐标，仅切 Mode，便于进遭遇图
                    if (wp.Mode != PartyWorldPresenceMode.InEncounter &&
                        !string.IsNullOrEmpty(wp.RouteId) &&
                        wp.RouteAnchorProgress < 0f &&
                        wp.TravelTotalTicks > 0)
                        wp.RouteAnchorProgress = Math.Max(0f, Math.Min(1f, wp.TravelProgress));
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
