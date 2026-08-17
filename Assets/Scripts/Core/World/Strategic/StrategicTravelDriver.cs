using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Travel tick 后的战略层检定（遭遇／接战碰撞／AI 栈推进）。</summary>
    public static class StrategicTravelDriver
    {
        public static void AfterTravelTick(SimulationWorld world, int ticks = 1)
        {
            if (world?.Strategic == null || ticks < 1)
                return;

            ArmyStackService.AdvanceAll(world, ticks);

            if (world.Strategic.HasBlockingInterrupt)
                return;

            // 接战只来自同路可见敌军栈碰撞／追击抵达。
            CheckBattleCollisions(world);
            StrategicFollowService.AfterTravelTick(world);
            StrategicPursuitService.AfterTravelTick(world);
        }

        static void CheckBattleCollisions(SimulationWorld world)
        {
            var scratch = new List<EntityId>(8);
            foreach (var kv in world.WorldPresence.All)
            {
                var p = kv.Value;
                if (p == null || p.Mode != PartyWorldPresenceMode.Traveling || string.IsNullOrEmpty(p.RouteId))
                    continue;
                if (!IsPlayerAgent(world, p.EntityId))
                    continue;

                scratch.Clear();
                scratch.Add(p.EntityId);
                foreach (var stack in world.Strategic.Armies.AllOnRoute(p.RouteId))
                {
                    if (stack == null || string.IsNullOrEmpty(stack.FactionId))
                        continue;
                    if (!world.Strategic.Diplomacy.IsHostile(world.Strategic.PlayerFactionId, stack.FactionId))
                        continue;
                    // 路中锚定敌军栈：过路不弹接战，只走追击抵达或大地图主动攻击。
                    if (stack.IsRouteAnchored)
                        continue;
                    if (BattleOfferService.TryBuildOfferForArmy(world, scratch, stack, "行军遭遇"))
                        return;
                }
            }
        }

        static bool IsPlayerAgent(SimulationWorld world, EntityId id)
        {
            if (id.IsNone || !world.Entities.TryGet(id, out var entity) || entity == null)
                return false;
            return (entity.Tags & EntityTag.Npc) == 0;
        }
    }
}
