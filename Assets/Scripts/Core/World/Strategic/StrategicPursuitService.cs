using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>大地图追击敌军栈：先到先接战，后到可加入。追击到站绝不弹「是否查看」。</summary>
    public static class StrategicPursuitService
    {
        public static void BeginPursuit(
            SimulationWorld world,
            IReadOnlyList<EntityId> party,
            ArmyStack stack)
        {
            if (world?.Strategic == null || stack == null || party == null || party.Count == 0)
                return;

            ArmyStackAdapter.TryResolveAttackerArmyId(world, party, out var attackerArmyId);
            BeginPursuitInternal(world, party, stack, attackerArmyId);
        }

        /// <summary>Phase E：Army 追 Army Adapter（内部仍写 CombatPursuitStackId）。</summary>
        public static void BeginPursuitArmy(
            SimulationWorld world,
            string attackerArmyId,
            ArmyStack defenderStack)
        {
            if (world?.Strategic == null || defenderStack == null || string.IsNullOrEmpty(attackerArmyId))
                return;
            if (!world.Strategic.FormalArmies.TryGet(attackerArmyId, out var attackerArmy) || attackerArmy == null)
                return;

            var party = ArmyStackAdapter.CollectLivingMemberIds(world, attackerArmy);
            if (party.Count == 0)
                return;

            BeginPursuitInternal(world, party, defenderStack, attackerArmyId);
        }

        static void BeginPursuitInternal(
            SimulationWorld world,
            IReadOnlyList<EntityId> party,
            ArmyStack stack,
            string attackerArmyId)
        {
            if (world?.Strategic == null || stack == null || party == null || party.Count == 0)
                return;
            if (string.IsNullOrEmpty(attackerArmyId) && ContainsPlayerAgent(world, party))
                return;

            world.Strategic.ClearArrivalNotice();
            var rt = world.Strategic.Encounter;
            var stackId = stack.Id ?? string.Empty;
            rt.PursueAttackerArmyId = attackerArmyId ?? string.Empty;
            ArmyStackAdapter.TryResolveDefenderArmyId(stack, out var defenderArmyId);
            rt.PursueDefenderArmyId = defenderArmyId ?? string.Empty;

            // 同栈增援：合并追击名单，勿覆盖先到者／仍在路上的人
            if (string.Equals(rt.PursueStackId, stackId, System.StringComparison.Ordinal) &&
                rt.HasPursueParty)
            {
                var merged = CollectPursueParty(world, rt);
                for (var i = 0; i < party.Count; i++)
                {
                    if (party[i].IsNone)
                        continue;
                    var found = false;
                    for (var j = 0; j < merged.Count; j++)
                    {
                        if (merged[j] == party[i])
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                        merged.Add(party[i]);
                }

                rt.SetPursueParty(merged);
            }
            else
            {
                rt.SetPursueParty(party);
                rt.PursueStackId = stackId;
            }

            for (var i = 0; i < party.Count; i++)
            {
                var id = party[i];
                if (id.IsNone || !world.WorldPresence.TryGet(id, out var p) || p == null)
                    continue;
                p.ClearFollow();
                p.CombatPursuitStackId = stackId;
            }
        }

        public static void ClearPursuit(SimulationWorld world)
        {
            if (world?.Strategic == null)
                return;

            foreach (var kv in world.WorldPresence.All)
            {
                if (kv.Value != null)
                    kv.Value.ClearCombatPursuit();
            }

            world.Strategic.Encounter.ClearPursuit();
        }

        /// <summary>先到者进手动战后：只清他们的追击标记，保留路上增援的 CombatPursuit。</summary>
        public static void ClearPursuitForEngagedKeepEnRoute(
            SimulationWorld world,
            IReadOnlyList<EntityId> engaged)
        {
            if (world?.Strategic == null)
                return;

            if (engaged != null)
            {
                for (var i = 0; i < engaged.Count; i++)
                {
                    if (engaged[i].IsNone ||
                        !world.WorldPresence.TryGet(engaged[i], out var p) ||
                        p == null)
                        continue;
                    p.ClearCombatPursuit();
                }
            }

            RebuildPursueListFromAgentMarks(world);
        }

        public static void ClearPursuitForAgents(SimulationWorld world, IReadOnlyList<EntityId> agents)
        {
            if (world?.Strategic == null || agents == null)
                return;
            for (var i = 0; i < agents.Count; i++)
            {
                if (agents[i].IsNone ||
                    !world.WorldPresence.TryGet(agents[i], out var p) ||
                    p == null)
                    continue;
                p.ClearCombatPursuit();
            }

            RebuildPursueListFromAgentMarks(world);
        }

        public static void RebuildPursueListFromAgentMarks(SimulationWorld world)
        {
            if (world?.Strategic == null)
                return;

            var rt = world.Strategic.Encounter;
            string stackId = rt.PursueStackId ?? string.Empty;
            var merged = new List<EntityId>(8);

            foreach (var kv in world.WorldPresence.All)
            {
                var p = kv.Value;
                if (p == null || !p.IsCombatPursuing)
                    continue;
                // 已在遭遇里的人不再算「追击增援」
                if (p.Mode == PartyWorldPresenceMode.InEncounter)
                {
                    p.ClearCombatPursuit();
                    continue;
                }

                if (string.IsNullOrEmpty(stackId))
                    stackId = p.CombatPursuitStackId;
                if (!string.Equals(p.CombatPursuitStackId, stackId, System.StringComparison.Ordinal))
                    continue;
                merged.Add(p.EntityId);
            }

            if (merged.Count == 0)
            {
                rt.ClearPursuit();
                return;
            }

            rt.PursueStackId = stackId;
            rt.SetPursueParty(merged);
        }

        public static bool IsCombatPursuitTraveler(SimulationWorld world, EntityId id)
        {
            if (world?.WorldPresence == null || id.IsNone)
                return false;
            if (!world.WorldPresence.TryGet(id, out var p) || p == null)
                return false;
            if (p.IsCombatPursuing)
                return true;
            var rt = world.Strategic?.Encounter;
            return rt != null && rt.IsPursue(id);
        }

        public static void AfterTravelTick(SimulationWorld world)
        {
            if (world?.Strategic == null)
                return;
            if (world.Strategic.HasBattleOffer)
            {
                // 已有弹窗时仍尝试入队其他栈（同栈 TryBuild 内去重）
            }

            EnsurePursuePartyFromAgentMarks(world);

            var rt = world.Strategic.Encounter;
            if (string.IsNullOrEmpty(rt.PursueStackId) || !rt.HasPursueParty)
                return;
            if (!world.Strategic.Armies.TryGet(rt.PursueStackId, out var stack) || stack == null)
            {
                ClearPursuit(world);
                return;
            }

            var pursue = CollectPursueParty(world, rt);
            if (pursue.Count == 0)
                return;

            // 敌军挪位：持续改道贴上去，追上再弹窗
            SyncPursuersToStack(world, pursue, stack);

            var ready = new List<EntityId>(pursue.Count);
            StrategicEngageRules.CollectPartyReadyToEngageStack(world, pursue, stack, ready);
            if (ready.Count == 0)
                return;

            var title = stack.HasIncapacitatedRemnant || stack.IsBattlefieldRemnant
                ? "残留战场"
                : "追击接战";
            if (BattleOfferService.TryBuildOfferForArmy(world, ready, stack, title))
                world.Strategic.ClearArrivalNotice();
        }

        /// <summary>追击中：每 tick 把未重合的人改道到敌军栈当前宏观位置。</summary>
        public static void SyncPursuersToStack(
            SimulationWorld world,
            IReadOnlyList<EntityId> pursue,
            ArmyStack stack)
        {
            if (world == null || stack == null || pursue == null)
                return;

            var rt = world.Strategic?.Encounter;
            if (rt != null &&
                !string.IsNullOrEmpty(rt.PursueAttackerArmyId) &&
                world.Strategic.FormalArmies.TryGet(rt.PursueAttackerArmyId, out var attackerArmy) &&
                attackerArmy != null)
            {
                ArmyPursuitCommandService.SyncFormalArmyPursuersToStack(
                    world,
                    attackerArmy,
                    stack,
                    pursue);

                for (var i = 0; i < pursue.Count; i++)
                {
                    var id = pursue[i];
                    if (id.IsNone || attackerArmy.ContainsMember(id))
                        continue;
                    SyncSoloPursuerToStack(world, id, stack);
                }

                return;
            }

            for (var i = 0; i < pursue.Count; i++)
                SyncSoloPursuerToStack(world, pursue[i], stack);
        }

        static void SyncSoloPursuerToStack(
            SimulationWorld world,
            EntityId id,
            ArmyStack stack)
        {
            if (id.IsNone || !world.WorldPresence.TryGet(id, out var p) || p == null)
                return;
            if (p.Mode == PartyWorldPresenceMode.InEncounter &&
                !StrategicEncounterSpawner.IsFieldCleared(world))
                return;
            if (!WorldTravelService.CanReceiveTravelOrder(world, id))
                return;
            if (StrategicEngageRules.IsAgentColocatedWithStack(world, p, stack))
                return;

            WorldTravelService.StartTravelToStackAnchor(world, id, stack);
        }

        /// <summary>用角色身上的 CombatPursuitStackId 补全追击名单（防止只上路未 BeginPursuit）。</summary>
        static void EnsurePursuePartyFromAgentMarks(SimulationWorld world) =>
            RebuildPursueListFromAgentMarks(world);

        public static List<EntityId> CollectPursueParty(
            SimulationWorld world,
            StrategicEncounterRuntime runtime)
        {
            var list = new List<EntityId>(runtime?.PursuePartyIds.Count ?? 0);
            if (world == null || runtime == null)
                return list;
            for (var i = 0; i < runtime.PursuePartyIds.Count; i++)
            {
                var id = new EntityId(runtime.PursuePartyIds[i]);
                if (!id.IsNone)
                    list.Add(id);
            }

            return list;
        }

        public static List<EntityId> CollectEngagedParty(
            SimulationWorld world,
            StrategicEncounterRuntime runtime)
        {
            var list = new List<EntityId>(runtime?.EngagedPartyIds.Count ?? 0);
            if (world == null || runtime == null)
                return list;
            for (var i = 0; i < runtime.EngagedPartyIds.Count; i++)
            {
                var id = new EntityId(runtime.EngagedPartyIds[i]);
                if (!id.IsNone)
                    list.Add(id);
            }

            return list;
        }

        public static List<EntityId> CollectEngagedPartyFromOffer(BattleOfferPending offer)
        {
            var list = new List<EntityId>(offer?.PlayerPartyIds.Count ?? 0);
            if (offer == null)
                return list;
            for (var i = 0; i < offer.PlayerPartyIds.Count; i++)
            {
                var id = new EntityId(offer.PlayerPartyIds[i]);
                if (!id.IsNone)
                    list.Add(id);
            }

            return list;
        }

        static bool ContainsPlayerAgent(SimulationWorld world, IReadOnlyList<EntityId> party)
        {
            if (world == null || party == null)
                return false;
            for (var i = 0; i < party.Count; i++)
            {
                if (party[i].IsNone || !world.Entities.TryGet(party[i], out var entity) || entity == null)
                    continue;
                if ((entity.Tags & EntityTag.Npc) == 0)
                    return true;
            }

            return false;
        }
    }
}
