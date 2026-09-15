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
            => StrategicBuildingDamage.Compute(attacker, core != null ? core.Defense : 0);

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
            var assault = WorldSiteCoreWarfareService.ValidateFixedCoreAssault(
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
            var assault = WorldSiteCoreWarfareService.ValidateFixedCoreAssault(world, attackerFactionId, workAreaId);
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
            var assault = WorldSiteCoreWarfareService.ValidateFixedCoreAssault(world, attackerFactionId, workAreaId);
            if (assault.IsFailure)
                return assault;

            var complete = WorldSiteCoreWarfareService.TryCompleteFixedSiteCapture(world, attackerFactionId, workAreaId);
            if (complete.IsFailure)
                return complete;

            world.Events.Publish(
                EventType.ControlCoreCaptured,
                world.Tick,
                payload: workAreaId);
            return Result.Success();
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
