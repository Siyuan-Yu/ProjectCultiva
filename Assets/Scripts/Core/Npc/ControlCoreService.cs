using System;
using System.Collections.Generic;
using XianXia.Core.Attributes;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Events;
using XianXia.Core.Exploration;
using XianXia.Core.Results;
using XianXia.Core.Settlement;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Npc
{
    /// <summary>Damage／occupy／capture for settlement control cores（伤害对齐近战属性公式）。</summary>
    public static class ControlCoreService
    {
        public const float DefaultStandRadius = 8f;

        /// <summary>与近战普攻同式：max(1, 攻击 − 建筑防御/2)。</summary>
        public static int ComputeAssaultDamage(Entity attacker, ControlCoreState core)
        {
            var attack = 1;
            if (attacker != null && attacker.TryGet<AttributesComponent>(out var attrs))
                attack = Math.Max(1, attrs.GetFinal(AttributeId.Attack));
            var defense = core != null ? Math.Max(0, core.Defense) : 0;
            return Math.Max(1, attack - defense / 2);
        }

        /// <summary>
        /// 攻方实体对主管府一击（正式近战伤害）；无攻方时失败。
        /// </summary>
        public static Result ApplyStrikeFromAttacker(
            SimulationWorld world,
            string workAreaId,
            EntityId attackerId,
            out int damageApplied)
        {
            damageApplied = 0;
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "world null");
            if (string.IsNullOrEmpty(workAreaId))
                return Result.Failure(ErrorCode.InvalidArgument, "workAreaId empty");
            if (attackerId.IsNone || !world.Entities.TryGet(attackerId, out var attacker))
                return Result.Failure(ErrorCode.EntityNotFound, "Attacker missing.");
            if (!world.ControlCores.TryGet(workAreaId, out var core))
                return Result.Failure(ErrorCode.NotFound, "No control core for work area.");
            if (core.CurrentDurability <= 0)
                return Result.Failure(ErrorCode.InvalidOperation, "Already breached; stand to occupy.");

            var attackerFactionId = attacker.TryGet<FactionMembershipComponent>(out var membership) &&
                                    membership != null && membership.IsAffiliated
                ? membership.FactionId
                : string.Empty;
            var assault = CaptureObjectiveService.TryBeginMilitaryAssault(
                world, attackerFactionId, workAreaId);
            if (assault.IsFailure)
                return assault;

            damageApplied = ComputeAssaultDamage(attacker, core);
            return ApplyDamageInternal(world, workAreaId, damageApplied, attackerId, defenseAlreadyApplied: true);
        }

        /// <summary>显式伤害（测试／脚本）；建筑 Defense 仍会在 ApplyDamage 中扣除。</summary>
        public static Result ApplyStrike(SimulationWorld world, string workAreaId, int damage)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "world null");
            if (string.IsNullOrEmpty(workAreaId))
                return Result.Failure(ErrorCode.InvalidArgument, "workAreaId empty");
            if (!world.ControlCores.TryGet(workAreaId, out var core))
                return Result.Failure(ErrorCode.NotFound, "No control core for work area.");
            if (core.CurrentDurability <= 0)
                return Result.Failure(ErrorCode.InvalidOperation, "Already breached; stand to occupy.");

            var attackerFactionId = world.Strategic?.PlayerFactionId ?? string.Empty;
            var assault = CaptureObjectiveService.TryBeginMilitaryAssault(world, attackerFactionId, workAreaId);
            if (assault.IsFailure)
                return assault;

            return ApplyDamageInternal(
                world, workAreaId, Math.Max(1, damage), EntityId.None, defenseAlreadyApplied: false);
        }

        static Result ApplyDamageInternal(
            SimulationWorld world,
            string workAreaId,
            int damage,
            EntityId attackerId,
            bool defenseAlreadyApplied)
        {
            var breached = world.ControlCores.ApplyDamage(
                workAreaId, damage, out var core, defenseAlreadyApplied);
            world.Events.Publish(
                EventType.ControlCoreDamaged,
                world.Tick,
                actor: attackerId,
                payload: workAreaId + ":" + core.CurrentDurability + "/" + core.MaxDurability +
                         ";dmg=" + damage);
            if (breached)
            {
                world.Flags.Set("control_core_breach:" + workAreaId);
                world.Flags.Set("control_core_capture_available");
            }

            return Result.Success();
        }

        /// <summary>Capture after breach + occupy hold completed; grants content privileges.</summary>
        public static Result TryCapture(
            SimulationWorld world,
            string workAreaId,
            string attackerFactionId = null)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "world null");
            if (!world.ControlCores.TryGet(workAreaId, out var core))
                return Result.Failure(ErrorCode.NotFound, "No control core.");
            if (!core.CaptureAvailable)
                return Result.Failure(ErrorCode.InvalidOperation, "Occupy hold not finished.");

            attackerFactionId ??= world.Strategic?.PlayerFactionId ?? StrategicFactionCatalog.PlayerFactionId;
            var assault = CaptureObjectiveService.TryBeginMilitaryAssault(world, attackerFactionId, workAreaId);
            if (assault.IsFailure)
                return assault;

            var complete = CaptureObjectiveService.TryCompleteWorldSiteCapture(world, attackerFactionId, workAreaId);
            if (complete.IsFailure)
                return complete;

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
            if (!world.WorldRegion.TryGet(core.LocationId, out _))
                return false;
            for (var i = 0; i < partyIds.Count; i++)
            {
                if (!world.Entities.TryGet(partyIds[i], out var e))
                    continue;
                if (e.TryGet<EntityLocationComponent>(out var el) &&
                    el.HasLocation &&
                    string.Equals(el.LocationId, core.LocationId, StringComparison.Ordinal))
                    return true;
            }

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

            if (!world.ControlCores.TryGet(workAreaId, out var core) || !core.CaptureAvailable)
                return;

            world.ControlCores.AddOccupyProgress(workAreaId, deltaSeconds, out core);
            if (core.OccupyProgressSeconds + 0.001f >= core.OccupyHoldSeconds)
                TryCapture(world, workAreaId);
        }
    }
}
