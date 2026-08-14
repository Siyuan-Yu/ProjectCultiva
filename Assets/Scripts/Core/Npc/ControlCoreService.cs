using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Events;
using XianXia.Core.Exploration;
using XianXia.Core.Results;
using XianXia.Core.Settlement;
using XianXia.Core.Simulation;

namespace XianXia.Core.Npc
{
    /// <summary>Damage／occupy／capture for settlement control cores.</summary>
    public static class ControlCoreService
    {
        public const int TestMeleeDamagePerHit = 20;
        public const float TestMeleeIntervalSeconds = 1f;
        public const float DefaultStandRadius = 8f;

        public static Result ApplyStrike(SimulationWorld world, string workAreaId, int damage = TestMeleeDamagePerHit)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "world null");
            if (string.IsNullOrEmpty(workAreaId))
                return Result.Failure(ErrorCode.InvalidArgument, "workAreaId empty");
            if (!world.ControlCores.TryGet(workAreaId, out var core))
                return Result.Failure(ErrorCode.NotFound, "No control core for work area.");
            if (core.PlayerControlled)
                return Result.Failure(ErrorCode.InvalidOperation, "Already player-controlled.");
            if (core.CurrentDurability <= 0)
                return Result.Failure(ErrorCode.InvalidOperation, "Already breached; stand to occupy.");

            var breached = world.ControlCores.ApplyDamage(workAreaId, damage, out core);
            world.Events.Publish(
                EventType.ControlCoreDamaged,
                world.Tick,
                payload: workAreaId + ":" + core.CurrentDurability + "/" + core.MaxDurability);
            if (breached)
            {
                world.Flags.Set("control_core_breach:" + workAreaId);
                world.Flags.Set("control_core_capture_available");
            }

            return Result.Success();
        }

        /// <summary>Capture after breach + occupy hold completed; grants content privileges.</summary>
        public static Result TryCapture(SimulationWorld world, string workAreaId)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "world null");
            if (!world.ControlCores.TryGet(workAreaId, out var before))
                return Result.Failure(ErrorCode.NotFound, "No control core.");
            if (!world.ControlCores.TryCapture(workAreaId, out var core))
                return Result.Failure(ErrorCode.InvalidOperation, "Occupy hold not finished.");

            world.Flags.Set("settlement_player_controlled");
            world.Flags.Set("control_core_owned:" + workAreaId);
            world.Flags.Clear("control_core_capture_available");
            world.SettlementAuthority.GrantAll(core.GrantsPrivileges);
            world.Events.Publish(
                EventType.ControlCoreCaptured,
                world.Tick,
                payload: workAreaId);
            return Result.Success();
        }

        public static bool TryFindNearest(
            SimulationWorld world,
            float worldX,
            float worldZ,
            float maxDistSq,
            out ControlCoreState core,
            out float distSq)
        {
            core = null;
            distSq = float.MaxValue;
            if (world == null)
                return false;

            foreach (var kv in world.ControlCores.All)
            {
                var c = kv.Value;
                if (c == null || string.IsNullOrEmpty(c.LocationId))
                    continue;
                if (!world.WorldRegion.TryGet(c.LocationId, out var loc))
                    continue;
                var dx = loc.PresentationX - worldX;
                var dz = loc.PresentationZ - worldZ;
                var d = dx * dx + dz * dz;
                if (d > maxDistSq || d >= distSq)
                    continue;
                distSq = d;
                core = c;
            }

            return core != null;
        }

        public static bool IsPartyNearCore(
            SimulationWorld world,
            IReadOnlyList<EntityId> partyIds,
            ControlCoreState core,
            float radius = DefaultStandRadius)
        {
            if (world == null || core == null || partyIds == null || partyIds.Count == 0)
                return false;
            if (!world.WorldRegion.TryGet(core.LocationId, out var loc))
                return false;
            var r2 = radius * radius;
            for (var i = 0; i < partyIds.Count; i++)
            {
                if (!world.Entities.TryGet(partyIds[i], out var e))
                    continue;
                // Prefer presentation from location if entity has no fine position: use EntityLocation match.
                if (e.TryGet<EntityLocationComponent>(out var el) &&
                    el.HasLocation &&
                    string.Equals(el.LocationId, core.LocationId, System.StringComparison.Ordinal))
                    return true;
            }

            // Fallback: any party member whose assigned location equals core location.
            return false;
        }

        /// <summary>Host supplies world-space party positions for stand／melee range.</summary>
        public static bool IsAnyPointNearCore(
            SimulationWorld world,
            ControlCoreState core,
            IReadOnlyList<(float X, float Z)> worldPoints,
            float radius = DefaultStandRadius)
        {
            if (world == null || core == null || worldPoints == null || worldPoints.Count == 0)
                return false;
            if (!world.WorldRegion.TryGet(core.LocationId, out var loc))
                return false;
            var r2 = radius * radius;
            for (var i = 0; i < worldPoints.Count; i++)
            {
                var dx = worldPoints[i].X - loc.PresentationX;
                var dz = worldPoints[i].Z - loc.PresentationZ;
                if (dx * dx + dz * dz <= r2)
                    return true;
            }

            return false;
        }

        public static void TickOccupy(
            SimulationWorld world,
            string workAreaId,
            float deltaSeconds,
            bool partyStanding)
        {
            if (world == null || string.IsNullOrEmpty(workAreaId) || !partyStanding)
            {
                if (world != null && !string.IsNullOrEmpty(workAreaId) && !partyStanding)
                    world.ControlCores.ResetOccupyProgress(workAreaId);
                return;
            }

            if (!world.ControlCores.TryGet(workAreaId, out var core) ||
                core.PlayerControlled ||
                !core.CaptureAvailable)
                return;

            world.ControlCores.AddOccupyProgress(workAreaId, deltaSeconds, out core);
            if (core.OccupyProgressSeconds + 0.001f >= core.OccupyHoldSeconds)
                TryCapture(world, workAreaId);
        }
    }
}
