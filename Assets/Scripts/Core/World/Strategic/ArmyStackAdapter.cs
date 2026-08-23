using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// ArmyStack 收敛 Adapter（Phase C+）：FormalArmy 为真源；ArmyStack 为 Battle/Presentation 兼容视图。
    /// 禁止在 ArmyStack 上独立维护与 FormalArmy 冲突的 living roster。
    /// </summary>
    public static class ArmyStackAdapter
    {
        public const string BanditPatrolStackId = "army:bandit_patrol_1";
        public const string BanditPatrolFormalArmyId = "army:formal_bandit_patrol_1";
        public const string BanditWeakPatrolStackId = "army:bandit_patrol_weak";
        public const string BanditWeakPatrolFormalArmyId = "army:formal_bandit_patrol_weak";
        public const string BanditScoutStackId = "army:bandit_patrol_auto";
        public const string BanditScoutFormalArmyId = "army:formal_bandit_scout";

        /// <summary>Prototype 试炼弱匪：专供自动战／弥留回归，自动战视为必胜。</summary>
        public static bool IsTrivialTestEnemyStack(string stackId) =>
            string.Equals(stackId, BanditWeakPatrolStackId, StringComparison.Ordinal);

        public static bool IsTrivialTestEnemyArmy(string formalArmyId) =>
            string.Equals(formalArmyId, BanditWeakPatrolFormalArmyId, StringComparison.Ordinal);

        public static bool HasFormalArmyLink(ArmyStack stack) =>
            stack != null && !string.IsNullOrEmpty(stack.FormalArmyId);

        public static bool TryGetFormalArmy(SimulationWorld world, ArmyStack stack, out FormalArmy army)
        {
            army = null;
            if (world?.Strategic?.FormalArmies == null || stack == null ||
                string.IsNullOrEmpty(stack.FormalArmyId))
                return false;
            return world.Strategic.FormalArmies.TryGet(stack.FormalArmyId, out army) && army != null;
        }

        /// <summary>Living member count：FormalArmy 链接时从 MemberCharacterIds 派生。</summary>
        public static int GetMemberCount(SimulationWorld world, ArmyStack stack)
        {
            if (stack == null)
                return 1;
            if (TryGetFormalArmy(world, stack, out var army))
                return Math.Max(1, CountLivingMembers(world, army));
            return Math.Max(1, stack.LegacyMemberCount);
        }

        public static int GetCombatPower(SimulationWorld world, ArmyStack stack)
        {
            if (stack == null)
                return 1;
            if (TryGetFormalArmy(world, stack, out var army))
            {
                var sum = 0;
                for (var i = 0; i < army.MemberCharacterIds.Count; i++)
                {
                    var id = new EntityId(army.MemberCharacterIds[i]);
                    if (!IsLivingMember(world, id))
                        continue;
                    sum += CombatPowerCalculator.ForEntity(world, id);
                }

                return Math.Max(1, sum);
            }

            var basePower = stack.LegacyCombatPower > 0 ? stack.LegacyCombatPower : 1;
            var count = Math.Max(1, stack.LegacyMemberCount);
            return Math.Max(1, basePower * count);
        }

        /// <summary>将 FormalArmy 的 living 统计同步到 Stack 兼容字段（只读展示用）。</summary>
        public static void RefreshDerivedPresentation(SimulationWorld world, ArmyStack stack)
        {
            if (world == null || stack == null || !HasFormalArmyLink(stack))
                return;
            stack.DerivedMemberCount = GetMemberCount(world, stack);
            stack.DerivedCombatPower = GetCombatPower(world, stack);
        }

        public static void RefreshAll(SimulationWorld world)
        {
            if (world?.Strategic?.Armies == null)
                return;
            foreach (var kv in world.Strategic.Armies.Stacks)
                RefreshDerivedPresentation(world, kv.Value);
        }

        public static Result<FormalArmy> EnsureBanditPatrolArmy(
            SimulationWorld world,
            string nodeId,
            string routeId,
            string destNodeId,
            float routeAnchorProgress)
        {
            if (world == null)
                return Result.Fail<FormalArmy>(ErrorCode.InvalidArgument, "world null");

            if (world.Strategic.FormalArmies.TryGet(BanditPatrolFormalArmyId, out var existing) &&
                existing != null)
            {
                SyncBanditStackView(
                    world,
                    existing,
                    BanditPatrolStackId,
                    "荒村山匪",
                    nodeId,
                    routeId,
                    destNodeId,
                    routeAnchorProgress);
                return Result.Ok(existing);
            }

            var members = TestStrategicBootstrap.EnsureBanditCharacters(world, nodeId);
            if (members.Count < 1)
                return Result.Fail<FormalArmy>(ErrorCode.InvalidOperation, "Failed to spawn bandit characters.");

            var army = new FormalArmy
            {
                ArmyId = BanditPatrolFormalArmyId,
                FactionId = StrategicFactionCatalog.BanditId,
                LeaderCharacterId = members[0],
                NodeId = nodeId,
                State = FormalArmyState.AtNode
            };
            var ids = new List<ulong>(members.Count);
            for (var i = 0; i < members.Count; i++)
                ids.Add(members[i].Value);
            army.ReplaceMembers(ids);
            world.Strategic.FormalArmies.Register(army);
            SyncMembershipForBanditArmy(world, army);

            SyncBanditStackView(
                world,
                army,
                BanditPatrolStackId,
                "荒村山匪",
                nodeId,
                routeId,
                destNodeId,
                routeAnchorProgress);
            return Result.Ok(army);
        }

        public static Result<FormalArmy> EnsureBanditWeakPatrolArmy(
            SimulationWorld world,
            string nodeId,
            string routeId,
            string destNodeId,
            float routeAnchorProgress)
        {
            if (world == null)
                return Result.Fail<FormalArmy>(ErrorCode.InvalidArgument, "world null");

            if (world.Strategic.FormalArmies.TryGet(BanditWeakPatrolFormalArmyId, out var existing) &&
                existing != null)
            {
                SyncBanditStackView(
                    world,
                    existing,
                    BanditWeakPatrolStackId,
                    "试炼弱匪（自动必胜）",
                    nodeId,
                    routeId,
                    destNodeId,
                    routeAnchorProgress);
                return Result.Ok(existing);
            }

            var members = TestStrategicBootstrap.EnsureWeakBanditCharacters(world, nodeId);
            if (members.Count < 1)
                return Result.Fail<FormalArmy>(ErrorCode.InvalidOperation, "Failed to spawn weak bandit characters.");

            var army = new FormalArmy
            {
                ArmyId = BanditWeakPatrolFormalArmyId,
                FactionId = StrategicFactionCatalog.BanditId,
                LeaderCharacterId = members[0],
                NodeId = nodeId,
                State = FormalArmyState.AtNode
            };
            var ids = new List<ulong>(members.Count);
            for (var i = 0; i < members.Count; i++)
                ids.Add(members[i].Value);
            army.ReplaceMembers(ids);
            world.Strategic.FormalArmies.Register(army);
            SyncMembershipForBanditArmy(world, army);

            SyncBanditStackView(
                world,
                army,
                BanditWeakPatrolStackId,
                "试炼弱匪（自动必胜）",
                nodeId,
                routeId,
                destNodeId,
                routeAnchorProgress);
            return Result.Ok(army);
        }

        public static Result<FormalArmy> EnsureBanditScoutArmy(
            SimulationWorld world,
            string nodeId,
            string routeId,
            string destNodeId,
            float routeAnchorProgress,
            int travelTicks)
        {
            if (world == null)
                return Result.Fail<FormalArmy>(ErrorCode.InvalidArgument, "world null");

            if (world.Strategic.FormalArmies.TryGet(BanditScoutFormalArmyId, out var existing) &&
                existing != null)
            {
                SyncBanditStackView(
                    world,
                    existing,
                    BanditScoutStackId,
                    "山匪斥候",
                    nodeId,
                    routeId,
                    destNodeId,
                    routeAnchorProgress,
                    travelTicks);
                return Result.Ok(existing);
            }

            var members = TestStrategicBootstrap.EnsureBanditScoutCharacters(world, nodeId);
            if (members.Count < 1)
                return Result.Fail<FormalArmy>(ErrorCode.InvalidOperation, "Failed to spawn bandit scout characters.");

            var army = new FormalArmy
            {
                ArmyId = BanditScoutFormalArmyId,
                FactionId = StrategicFactionCatalog.BanditId,
                LeaderCharacterId = members[0],
                NodeId = nodeId,
                State = FormalArmyState.OnRoute,
                RouteId = routeId ?? string.Empty,
                DestNodeId = destNodeId ?? string.Empty,
                TravelTotalTicks = Math.Max(1, travelTicks),
                RemainingTravelTicks = Math.Max(1, travelTicks)
            };
            var ids = new List<ulong>(members.Count);
            for (var i = 0; i < members.Count; i++)
                ids.Add(members[i].Value);
            army.ReplaceMembers(ids);
            world.Strategic.FormalArmies.Register(army);
            SyncMembershipForBanditArmy(world, army);

            SyncBanditStackView(
                world,
                army,
                BanditScoutStackId,
                "山匪斥候",
                nodeId,
                routeId,
                destNodeId,
                routeAnchorProgress,
                travelTicks);
            ArmyPresenceAdapter.SyncFromArmy(world, army);
            return Result.Ok(army);
        }

        static void SyncBanditStackView(
            SimulationWorld world,
            FormalArmy army,
            string stackId,
            string displayName,
            string nodeId,
            string routeId,
            string destNodeId,
            float routeAnchorProgress,
            int travelTicks = 0)
        {
            world.Strategic.Armies.Remove(stackId);
            var stack = new ArmyStack
            {
                Id = stackId,
                FormalArmyId = army.ArmyId,
                FactionId = army.FactionId,
                DisplayName = displayName ?? string.Empty,
                NodeId = nodeId,
                RouteId = routeId ?? string.Empty,
                DestNodeId = destNodeId ?? string.Empty,
                RouteAnchorProgress = routeAnchorProgress
            };
            if (travelTicks > 0)
            {
                stack.TravelTotalTicks = travelTicks;
                stack.RemainingTravelTicks = travelTicks;
            }
            RefreshDerivedPresentation(world, stack);
            world.Strategic.Armies.Register(stack);
        }

        static void SyncBanditStackView(
            SimulationWorld world,
            FormalArmy army,
            string nodeId,
            string routeId,
            string destNodeId,
            float routeAnchorProgress)
        {
            SyncBanditStackView(
                world,
                army,
                BanditPatrolStackId,
                "荒村山匪",
                nodeId,
                routeId,
                destNodeId,
                routeAnchorProgress);
        }

        static void SyncMembershipForBanditArmy(SimulationWorld world, FormalArmy army)
        {
            for (var i = 0; i < army.MemberCharacterIds.Count; i++)
            {
                var memberId = new EntityId(army.MemberCharacterIds[i]);
                if (!world.Entities.TryGet(memberId, out var entity))
                    continue;
                ArmyInvariants.EnsureMembershipComponent(entity);
                entity.Get<ArmyMembershipComponent>().SetArmyId(army.ArmyId);
            }
        }

        static int CountLivingMembers(SimulationWorld world, FormalArmy army)
        {
            var count = 0;
            for (var i = 0; i < army.MemberCharacterIds.Count; i++)
            {
                if (IsLivingMember(world, new EntityId(army.MemberCharacterIds[i])))
                    count++;
            }

            return count;
        }

        static bool IsLivingMember(SimulationWorld world, EntityId id)
        {
            if (id.IsNone || !world.Entities.TryGet(id, out var entity))
                return false;
            if (!entity.TryGet<LifecycleComponent>(out var life))
                return true;
            return !life.IsDead && !life.IsRemoved;
        }

        public static bool TryResolveDefenderArmyId(ArmyStack stack, out string armyId)
        {
            armyId = string.Empty;
            if (stack == null)
                return false;
            if (!string.IsNullOrEmpty(stack.FormalArmyId))
            {
                armyId = stack.FormalArmyId;
                return true;
            }

            armyId = stack.Id ?? string.Empty;
            return !string.IsNullOrEmpty(armyId);
        }

        public static bool TryResolveAttackerArmyId(
            SimulationWorld world,
            IReadOnlyList<EntityId> party,
            out string armyId)
        {
            armyId = string.Empty;
            if (world == null || party == null || party.Count == 0)
                return false;

            string shared = null;
            for (var i = 0; i < party.Count; i++)
            {
                if (party[i].IsNone ||
                    !world.Entities.TryGet(party[i], out var entity) ||
                    !entity.TryGet<ArmyMembershipComponent>(out var mem) ||
                    string.IsNullOrEmpty(mem.ArmyId))
                    return false;

                if (shared == null)
                    shared = mem.ArmyId;
                else if (!string.Equals(shared, mem.ArmyId, StringComparison.Ordinal))
                    return false;
            }

            armyId = shared ?? string.Empty;
            return !string.IsNullOrEmpty(armyId);
        }

        public static List<EntityId> CollectLivingMemberIds(SimulationWorld world, FormalArmy army)
        {
            var list = new List<EntityId>(army?.MemberCharacterIds.Count ?? 0);
            if (world == null || army == null)
                return list;
            for (var i = 0; i < army.MemberCharacterIds.Count; i++)
            {
                var id = new EntityId(army.MemberCharacterIds[i]);
                if (IsLivingMember(world, id))
                    list.Add(id);
            }

            return list;
        }

        public static int GetIncapacitatedMemberCount(SimulationWorld world, ArmyStack stack)
        {
            if (stack == null)
                return 0;
            if (!TryGetFormalArmy(world, stack, out var army))
                return Math.Max(0, stack.IncapacitatedMemberCount);

            var count = 0;
            for (var i = 0; i < army.MemberCharacterIds.Count; i++)
            {
                var id = new EntityId(army.MemberCharacterIds[i]);
                if (!world.Entities.TryGet(id, out var entity) ||
                    !entity.TryGet<LifecycleComponent>(out var life))
                    continue;
                if (life.IsIncapacitated)
                    count++;
            }

            return count;
        }

        public static int GetCorpseMemberCount(SimulationWorld world, ArmyStack stack)
        {
            if (stack == null)
                return 0;
            if (!TryGetFormalArmy(world, stack, out var army))
                return Math.Max(0, stack.CorpseMemberCount);

            var count = 0;
            for (var i = 0; i < army.MemberCharacterIds.Count; i++)
            {
                var id = new EntityId(army.MemberCharacterIds[i]);
                if (!world.Entities.TryGet(id, out var entity) ||
                    !entity.TryGet<LifecycleComponent>(out var life))
                    continue;
                if (life.IsDead)
                    count++;
            }

            return count;
        }

        public static void SyncDownedCountsFromMembers(SimulationWorld world, ArmyStack stack)
        {
            if (world == null || stack == null || !HasFormalArmyLink(stack))
                return;
            stack.IncapacitatedMemberCount = GetIncapacitatedMemberCount(world, stack);
            stack.CorpseMemberCount = GetCorpseMemberCount(world, stack);
            RefreshDerivedPresentation(world, stack);
        }

        /// <summary>FormalArmy 战略位置 → ArmyStack 展示/接战兼容视图（禁止双轨 Advance）。</summary>
        public static void SyncStackTravelFromFormalArmy(SimulationWorld world, ArmyStack stack)
        {
            if (world == null || stack == null || !TryGetFormalArmy(world, stack, out var army) || army == null)
                return;

            stack.FactionId = army.FactionId ?? string.Empty;
            stack.NodeId = army.NodeId ?? string.Empty;
            stack.DestNodeId = army.DestNodeId ?? string.Empty;

            if (army.UsesHexStrategicPosition)
            {
                stack.RouteId = string.Empty;
                stack.RouteAnchorProgress = -1f;
                stack.ClearTravel();
                if (world.Strategic.Sites.TryGetAtHex(army.CurrentHex, out var site) && site != null)
                    stack.NodeId = string.IsNullOrEmpty(site.LegacyNodeId) ? site.SiteId : site.LegacyNodeId;
                stack.DestNodeId = stack.NodeId;
                return;
            }

            if (army.IsTraveling && !string.IsNullOrEmpty(army.RouteId))
            {
                stack.RouteId = army.RouteId;
                stack.RouteAnchorProgress = army.GetRouteDisplayProgress();
                stack.RemainingTravelTicks = 0;
                stack.TravelTotalTicks = 0;
                return;
            }

            if (army.IsRouteAnchored)
            {
                stack.RouteId = army.RouteId;
                stack.RouteAnchorProgress = army.RouteAnchorProgress;
                stack.RemainingTravelTicks = 0;
                stack.TravelTotalTicks = 0;
                return;
            }

            stack.RouteId = string.Empty;
            stack.RouteAnchorProgress = -1f;
            stack.ClearTravel();
        }

        public static void SyncAllLinkedStacksFromFormalArmies(SimulationWorld world)
        {
            if (world?.Strategic?.Armies == null)
                return;
            foreach (var kv in world.Strategic.Armies.Stacks)
            {
                var stack = kv.Value;
                if (stack == null || !HasFormalArmyLink(stack))
                    continue;
                SyncStackTravelFromFormalArmy(world, stack);
                RefreshDerivedPresentation(world, stack);
            }
        }
    }
}
