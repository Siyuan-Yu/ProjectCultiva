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
            var reuse = CanReuseLivingSpawns(world, armyStackId);
            if (!reuse)
                ClearSpawned(world);
            else
                PruneDeadSpawns(world);

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
            PruneDeadSpawns(world);
            // 删栈前先把道路进度落到参战者身上，否则清场后路锚变成 0、无法回程／像瞬移
            SnapshotEngagedRouteFromStack(world);
            SyncArmyStackMemberCount(world);
            TryMarkFieldCleared(world);
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
            PruneDeadSpawns(world);

            ArmyStack stack = null;
            var stackId = world.Strategic.Encounter.ArmyStackId;
            if (!string.IsNullOrEmpty(stackId))
                world.Strategic.Armies.TryGet(stackId, out stack);

            var living = CountLivingTracked(world);
            var targetCount = stack?.MemberCount > 0
                ? stack.MemberCount
                : world.Strategic.Encounter.FallbackMemberCount;
            var toSpawn = Math.Max(0, targetCount - living);
            if (toSpawn <= 0)
                return Result.Success();

            var power = stack?.CombatPower > 0
                ? stack.CombatPower
                : world.Strategic.Encounter.FallbackCombatPowerPerMember;
            var linkId = string.IsNullOrEmpty(world.Strategic.Encounter.EncounterLinkId)
                ? world.PartyWorld.EncounterId
                : world.Strategic.Encounter.EncounterLinkId;

            var startId = world.WorldRegion.StartLocationId;
            world.WorldRegion.TryGet(startId, out var startLoc);
            var baseX = startLoc?.PresentationX ?? 0f;
            var baseZ = startLoc?.PresentationZ ?? 0f;
            var spawnIndex = living;

            for (var i = 0; i < toSpawn; i++)
            {
                var faction = stack != null
                    ? StrategicFactionCatalog.DisplayName(stack.FactionId)
                    : "敌军";
                var label = (stack != null && !string.IsNullOrEmpty(stack.DisplayName)
                        ? stack.DisplayName
                        : faction) +
                    " " + (spawnIndex + i + 1);
                var created = world.Entities.CreateNpc(BanditGruntDef, label);
                if (created.IsFailure)
                    return Result.Failure(created.Error);

                var entity = created.Value;
                ConfigureCombatNpc(entity, power);
                if (!string.IsNullOrEmpty(linkId))
                {
                    entity.AddComponent(new EncounterLinkComponent { EncounterId = linkId });
                }

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

            SyncArmyStackMemberCount(world);
            return Result.Success();
        }

        public static int CountLivingTrackedSpawns(SimulationWorld world) => CountLivingTracked(world);

        public static Result JoinEngagedMembers(
            SimulationWorld world,
            IReadOnlyList<EntityId> newcomers)
        {
            if (world?.Strategic == null || newcomers == null || newcomers.Count == 0)
                return Result.Failure(ErrorCode.InvalidArgument, "No newcomers to join.");
            if (!BattleOfferService.HasActiveEncounterForStack(world, world.Strategic.Encounter.ArmyStackId))
                return Result.Failure(ErrorCode.InvalidOperation, "No active encounter to join.");

            var rt = world.Strategic.Encounter;
            var startId = world.WorldRegion.StartLocationId;
            world.WorldRegion.TryGet(startId, out var startLoc);
            var baseX = startLoc?.PresentationX ?? 0f;
            var baseZ = startLoc?.PresentationZ ?? 0f;
            var joined = 0;
            var slot = rt.EngagedPartyIds.Count;

            for (var i = 0; i < newcomers.Count; i++)
            {
                var id = newcomers[i];
                if (id.IsNone || rt.IsEngaged(id))
                    continue;

                rt.AddEngagedPartyMember(id);
                if (world.WorldPresence.TryGet(id, out var wp) && wp != null)
                    PreserveRouteProgressForEncounter(wp);

                if (!world.Entities.TryGet(id, out var entity))
                    continue;
                if (!entity.TryGet<EntityLocationComponent>(out var loc))
                {
                    loc = new EntityLocationComponent();
                    entity.AddComponent(loc);
                }

                loc.LocationId = startId ?? string.Empty;
                loc.SetPresentationOverride(baseX - 1.5f - slot * 1.1f, baseZ + 1.8f);
                slot++;
                joined++;
            }

            return joined > 0
                ? Result.Success()
                : Result.Failure(ErrorCode.InvalidOperation, "No eligible members joined.");
        }

        public static void RelocatePartyOnEncounterMap(
            SimulationWorld world,
            IReadOnlyList<EntityId> members)
        {
            if (world?.Strategic?.Encounter == null || members == null || members.Count == 0)
                return;

            var rt = world.Strategic.Encounter;
            var startId = world.WorldRegion.StartLocationId;
            world.WorldRegion.TryGet(startId, out var startLoc);
            var baseX = startLoc?.PresentationX ?? 0f;
            var baseZ = startLoc?.PresentationZ ?? 0f;

            for (var i = 0; i < members.Count; i++)
            {
                var id = members[i];
                if (id.IsNone || !rt.IsEngaged(id))
                    continue;

                var slot = 0;
                for (var j = 0; j < rt.EngagedPartyIds.Count; j++)
                {
                    if (rt.EngagedPartyIds[j] == id.Value)
                    {
                        slot = j;
                        break;
                    }
                }

                if (!world.Entities.TryGet(id, out var entity))
                    continue;
                if (!entity.TryGet<EntityLocationComponent>(out var loc))
                {
                    loc = new EntityLocationComponent();
                    entity.AddComponent(loc);
                }

                loc.LocationId = startId ?? string.Empty;
                loc.SetPresentationOverride(baseX - 1.5f - slot * 1.1f, baseZ + 1.8f);

                if (world.WorldPresence.TryGet(id, out var wp) && wp != null)
                    PreserveRouteProgressForEncounter(wp);
            }
        }

        public static void ClearSpawned(SimulationWorld world)
        {
            if (world?.Strategic == null)
                return;
            var rt = world.Strategic.Encounter;
            for (var i = 0; i < rt.SpawnedEntityIds.Count; i++)
            {
                var id = new EntityId(rt.SpawnedEntityIds[i]);
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
            PruneDeadSpawns(world);
            return CountLivingTracked(world) > 0;
        }

        static int CountLivingTracked(SimulationWorld world)
        {
            if (world?.Strategic == null)
                return 0;
            PruneDeadSpawns(world);
            return world.Strategic.Encounter.SpawnedEntityIds.Count;
        }

        static void PruneDeadSpawns(SimulationWorld world)
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

                if (entity.TryGet<LifecycleComponent>(out var life) &&
                    (life.IsDead || life.IsRemoved))
                {
                    rt.RemoveTrackedSpawnAt(i);
                }
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

            var living = CountLivingTracked(world);
            stack.MemberCount = Math.Max(0, living);
            if (stack.MemberCount <= 0)
                world.Strategic.Armies.Remove(stackId);
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
