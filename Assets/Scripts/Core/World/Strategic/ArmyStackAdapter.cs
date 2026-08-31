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
    /// ArmyStack 收敛 Adapter（Phase C+）：FormalArmy 为真源；ArmyStack �?Battle/Presentation 兼容视图�?
    /// 禁止�?ArmyStack 上独立维护与 FormalArmy 冲突�?living roster�?
    /// </summary>
    public static class ArmyStackAdapter
    {
        public const string BanditPatrolStackId = "army:bandit_patrol_1";
        public const string BanditPatrolFormalArmyId = "army:formal_bandit_patrol_1";
        public const string BanditWeakPatrolStackId = "army:bandit_patrol_weak";
        public const string BanditWeakPatrolFormalArmyId = "army:formal_bandit_patrol_weak";
        public const string BanditScoutStackId = "army:bandit_patrol_auto";
        public const string BanditScoutFormalArmyId = "army:formal_bandit_scout";
        public const string BanditCasualtyTestStackId = "army:bandit_patrol_casualty_test";
        public const string BanditCasualtyTestFormalArmyId = "army:formal_bandit_casualty_test";

        /// <summary>Prototype 试炼弱匪：专供自动战／弥留回归，自动战视为必胜。</summary>
        public static bool IsTrivialTestEnemyStack(string stackId) =>
            string.Equals(stackId, BanditWeakPatrolStackId, StringComparison.Ordinal);

        public static bool IsTrivialTestEnemyArmy(string formalArmyId) =>
            string.Equals(formalArmyId, BanditWeakPatrolFormalArmyId, StringComparison.Ordinal);

        /// <summary>Prototype 试炼强匪：高战力展示 + 自动战必胜但必对我方造成 1 人弥留或阵亡（测试夹具）。</summary>
        public static bool IsCasualtyTestEnemyStack(string stackId) =>
            string.Equals(stackId, BanditCasualtyTestStackId, StringComparison.Ordinal);

        public static bool IsCasualtyTestEnemyArmy(string formalArmyId) =>
            string.Equals(formalArmyId, BanditCasualtyTestFormalArmyId, StringComparison.Ordinal);

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

        /// <summary>Living member count：FormalArmy 链接时从 MemberCharacterIds 派生�?/summary>
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

        /// <summary>�?FormalArmy �?living 统计同步�?Stack 兼容字段（只读展示用）�?/summary>
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

        /// <summary>
        /// FormalArmy 链接的 ArmyStack 兼容视图（Content bootstrap 通用入口）。
        /// 创建或刷新 stack 并同步 FormalArmy 派生的 travel / 展示字段；重复调用幂等。
        /// </summary>
        public static void EnsureLinkedStackView(
            SimulationWorld world,
            FormalArmy army,
            string stackId,
            string displayName)
        {
            if (world?.Strategic?.Armies == null || army == null || string.IsNullOrEmpty(stackId))
                return;

            world.Strategic.Armies.Remove(stackId);
            var stack = new ArmyStack
            {
                Id = stackId,
                FormalArmyId = army.ArmyId,
                FactionId = army.FactionId ?? string.Empty,
                DisplayName = displayName ?? string.Empty
            };
            SyncStackTravelFromFormalArmy(world, stack);
            RefreshDerivedPresentation(world, stack);
            world.Strategic.Armies.Register(stack);
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

        /// <summary>FormalArmy 战略位置 �?ArmyStack 展示/接战兼容视图（禁止双�?Advance）�?/summary>
        public static void SyncStackTravelFromFormalArmy(SimulationWorld world, ArmyStack stack)
        {
            if (world == null || stack == null || !TryGetFormalArmy(world, stack, out var army) || army == null)
                return;

            stack.FactionId = army.FactionId ?? string.Empty;
            stack.SiteId = string.Empty;
            if (ArmyService.TryResolveArmySiteId(world, army, out var siteId))
                stack.SiteId = siteId;
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

        public static bool IsPlayerFactionFormalArmy(SimulationWorld world, FormalArmy army)
        {
            if (army == null || string.IsNullOrEmpty(army.FactionId))
                return false;
            var playerFactionId = world?.Strategic?.PlayerFactionId;
            if (string.IsNullOrEmpty(playerFactionId))
                playerFactionId = StrategicFactionCatalog.PlayerFactionId;
            return string.Equals(army.FactionId, playerFactionId, StringComparison.Ordinal);
        }

        /// <summary>
        /// Snapshot Restore 后：从 FormalArmy 真源重建 ArmyStack 展示视图（不创建 Character、不 respawn fixture）。
        /// 玩家 FormalArmy 与正常 Gameplay 一致，不注册 Stack（由 WorldMap FormalArmy 头像呈现）。
        /// </summary>
        public static void EnsurePresentationStacksFromFormalArmies(SimulationWorld world)
        {
            if (world?.Strategic?.FormalArmies == null || world.Strategic.Armies == null)
                return;

            PurgePlayerFactionPresentationStacks(world);

            foreach (var kv in world.Strategic.FormalArmies.Armies)
            {
                var army = kv.Value;
                if (army == null || string.IsNullOrEmpty(army.ArmyId))
                    continue;
                if (IsPlayerFactionFormalArmy(world, army))
                    continue;

                var stackId = ResolvePresentationStackId(army.ArmyId);
                if (!world.Strategic.Armies.TryGet(stackId, out var stack) || stack == null)
                {
                    stack = new ArmyStack
                    {
                        Id = stackId,
                        FormalArmyId = army.ArmyId,
                        FactionId = army.FactionId ?? string.Empty,
                        DisplayName = ResolvePresentationStackDisplayName(army.ArmyId) ?? string.Empty,
                    };
                    world.Strategic.Armies.Register(stack);
                }
                else
                {
                    stack.FormalArmyId = army.ArmyId;
                    stack.FactionId = army.FactionId ?? string.Empty;
                    var displayName = ResolvePresentationStackDisplayName(army.ArmyId);
                    if (!string.IsNullOrEmpty(displayName))
                        stack.DisplayName = displayName;
                }

                SyncStackTravelFromFormalArmy(world, stack);
                RefreshDerivedPresentation(world, stack);
            }
        }

        static void PurgePlayerFactionPresentationStacks(SimulationWorld world)
        {
            if (world?.Strategic?.FormalArmies == null || world.Strategic.Armies == null)
                return;

            foreach (var kv in world.Strategic.FormalArmies.Armies)
            {
                var army = kv.Value;
                if (army == null || !IsPlayerFactionFormalArmy(world, army))
                    continue;
                world.Strategic.Armies.Remove(ResolvePresentationStackId(army.ArmyId));
            }
        }

        static string ResolvePresentationStackId(string formalArmyId)
        {
            if (string.Equals(formalArmyId, BanditPatrolFormalArmyId, StringComparison.Ordinal))
                return BanditPatrolStackId;
            if (string.Equals(formalArmyId, BanditWeakPatrolFormalArmyId, StringComparison.Ordinal))
                return BanditWeakPatrolStackId;
            if (string.Equals(formalArmyId, BanditScoutFormalArmyId, StringComparison.Ordinal))
                return BanditScoutStackId;
            if (string.Equals(formalArmyId, BanditCasualtyTestFormalArmyId, StringComparison.Ordinal))
                return BanditCasualtyTestStackId;
            return formalArmyId ?? string.Empty;
        }

        static string ResolvePresentationStackDisplayName(string formalArmyId)
        {
            if (string.Equals(formalArmyId, BanditPatrolFormalArmyId, StringComparison.Ordinal))
                return "荒村山匪";
            if (string.Equals(formalArmyId, BanditWeakPatrolFormalArmyId, StringComparison.Ordinal))
                return "试炼弱匪（自动必胜）";
            if (string.Equals(formalArmyId, BanditScoutFormalArmyId, StringComparison.Ordinal))
                return "山匪斥候";
            if (string.Equals(formalArmyId, BanditCasualtyTestFormalArmyId, StringComparison.Ordinal))
                return "试炼强匪（自动伤亡）";
            return string.Empty;
        }
    }
}
