using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Phase 2D-A：Background Simulation 统一低频调度器。
    /// Traveling Character 按 bucket 分批推进；Idle Character 零 CPU。
    /// 距离预算来自 elapsed Simulation World Time（非每帧 Update、非每 tick 重算 A*）。
    /// </summary>
    public static class BackgroundSimulationScheduler
    {
        public const int TravelBucketCount = 16;

        static readonly List<EntityId> TravelingScratch = new List<EntityId>(512);

        /// <summary>SimulationLoop 每 world tick 调用（staggered bucket）。</summary>
        public static void AfterSimulationTick(SimulationWorld world, int simulationTicksAdvanced = 1)
        {
            if (world?.BackgroundCharacterTravel == null || simulationTicksAdvanced < 1)
                return;

            for (var step = 0; step < simulationTicksAdvanced; step++)
            {
                var bucket = ResolveBucketForWorldTick(world.Tick.Value + (ulong)step);
                ProcessTravelBucket(world, bucket, world.Tick.Value + (ulong)step);
            }
        }

        /// <summary>测试／Debug：一次性为全部 Traveling 消耗 elapsed simulation ticks。</summary>
        public static void AdvanceTravelBatch(SimulationWorld world, ulong elapsedSimulationTicks)
        {
            if (world?.BackgroundCharacterTravel == null || elapsedSimulationTicks == 0)
                return;

            CollectTraveling(world, TravelingScratch);
            for (var i = 0; i < TravelingScratch.Count; i++)
                AdvanceTravelCharacter(world, TravelingScratch[i], elapsedSimulationTicks, advanceLastProcessed: false);
            TravelingScratch.Clear();
        }

        public static float DistanceBudgetFromElapsedSimulationTicks(float hexSize, ulong elapsedSimulationTicks)
        {
            if (elapsedSimulationTicks == 0)
                return 0f;
            return PlayerPartyHexTravelService.WorldUnitsPerTick(hexSize) * elapsedSimulationTicks;
        }

        public static int ResolveTravelBucket(EntityId characterId) =>
            characterId.IsNone ? 0 : (int)(characterId.Value % (ulong)TravelBucketCount);

        public static int ResolveBucketForWorldTick(ulong worldTickValue) =>
            (int)(worldTickValue % (ulong)TravelBucketCount);

        static void ProcessTravelBucket(SimulationWorld world, int bucket, ulong currentWorldTick)
        {
            TravelingScratch.Clear();
            foreach (var kv in world.BackgroundCharacterTravel.All)
            {
                if (kv.Value == null || !kv.Value.IsMoving)
                    continue;

                var id = new EntityId(kv.Key);
                if (ResolveTravelBucket(id) != bucket)
                    continue;

                if (currentWorldTick - kv.Value.LastProcessedWorldTick == 0)
                    continue;

                TravelingScratch.Add(id);
            }

            for (var i = 0; i < TravelingScratch.Count; i++)
            {
                var id = TravelingScratch[i];
                if (!world.BackgroundCharacterTravel.TryGet(id, out var motion) ||
                    motion == null ||
                    !motion.IsMoving)
                    continue;

                var elapsed = currentWorldTick - motion.LastProcessedWorldTick;
                if (elapsed == 0)
                    continue;

                AdvanceTravelCharacter(world, id, elapsed, advanceLastProcessed: true, currentWorldTick);
            }

            TravelingScratch.Clear();
        }

        static void AdvanceTravelCharacter(
            SimulationWorld world,
            EntityId characterId,
            ulong elapsedSimulationTicks,
            bool advanceLastProcessed,
            ulong currentWorldTick = 0)
        {
            if (!world.BackgroundCharacterTravel.TryGet(characterId, out var motion) ||
                motion == null ||
                !motion.IsMoving)
                return;

            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            var budget = DistanceBudgetFromElapsedSimulationTicks(hexSize, elapsedSimulationTicks);
            if (budget <= 0f)
                return;

            BackgroundCharacterTravelService.AdvanceDistanceBudget(world, characterId, budget);
            if (advanceLastProcessed)
                motion.LastProcessedWorldTick = currentWorldTick;
        }

        static void CollectTraveling(SimulationWorld world, List<EntityId> into)
        {
            into.Clear();
            foreach (var kv in world.BackgroundCharacterTravel.All)
            {
                if (kv.Value != null && kv.Value.IsMoving)
                    into.Add(new EntityId(kv.Key));
            }
        }
    }
}
