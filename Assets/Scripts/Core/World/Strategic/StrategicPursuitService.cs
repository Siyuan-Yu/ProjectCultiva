using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>大地图追击敌军栈：抵达同站／同路后自动弹出接战。</summary>
    public static class StrategicPursuitService
    {
        public static void BeginPursuit(
            SimulationWorld world,
            IReadOnlyList<EntityId> party,
            ArmyStack stack)
        {
            if (world?.Strategic == null || stack == null || party == null || party.Count == 0)
                return;
            world.Strategic.Encounter.SetEngagedParty(party);
            world.Strategic.Encounter.PursueStackId = stack.Id ?? string.Empty;
        }

        public static void ClearPursuit(SimulationWorld world)
        {
            if (world?.Strategic == null)
                return;
            world.Strategic.Encounter.ClearPursuit();
        }

        public static void AfterTravelTick(SimulationWorld world)
        {
            if (world?.Strategic == null || world.Strategic.HasBlockingInterrupt)
                return;

            var rt = world.Strategic.Encounter;
            if (string.IsNullOrEmpty(rt.PursueStackId) || !rt.HasEngagedParty)
                return;
            if (!world.Strategic.Armies.TryGet(rt.PursueStackId, out var stack) || stack == null)
            {
                ClearPursuit(world);
                return;
            }

            var party = CollectEngagedParty(world, rt);
            if (party.Count == 0)
                return;

            if (!StrategicNodeAccessService.CanEngageStackNow(world, party, stack))
                return;

            if (BattleOfferService.TryBuildOfferForArmy(world, party, stack, "追击接战"))
                ClearPursuit(world);
        }

        public static List<EntityId> CollectEngagedParty(
            SimulationWorld world,
            StrategicEncounterRuntime runtime)
        {
            var list = new List<EntityId>(runtime.EngagedPartyIds.Count);
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
    }
}
