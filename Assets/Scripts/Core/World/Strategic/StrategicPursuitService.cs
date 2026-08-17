using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>大地图追击敌军栈：先到先接战，后到可加入。</summary>
    public static class StrategicPursuitService
    {
        public static void BeginPursuit(
            SimulationWorld world,
            IReadOnlyList<EntityId> party,
            ArmyStack stack)
        {
            if (world?.Strategic == null || stack == null || party == null || party.Count == 0)
                return;
            world.Strategic.Encounter.SetPursueParty(party);
            world.Strategic.Encounter.PursueStackId = stack.Id ?? string.Empty;
            for (var i = 0; i < party.Count; i++)
            {
                if (world.WorldPresence.TryGet(party[i], out var p) && p != null)
                    p.ClearFollow();
                WorldTravelService.ClampPursuitTravelToStackAnchor(world, party[i], stack);
            }
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

            var ready = new List<EntityId>(pursue.Count);
            StrategicNodeAccessService.CollectPartyReadyToEngageStack(world, pursue, stack, ready);
            if (ready.Count == 0)
                return;

            if (BattleOfferService.TryBuildOfferForArmy(world, ready, stack, "追击接战"))
                return;
        }

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
    }
}
