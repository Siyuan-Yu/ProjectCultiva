using System;
using XianXia.Core.Attributes;
using XianXia.Core.Construction;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;

namespace XianXia.Core.Combat
{
    /// <summary>One fixed-point authority for dying reserve, herb treatment and HP recovery.</summary>
    public static class MedicalRecoveryService
    {
        public const int BaseDyingRateMilli = -1000;
        public const int HerbRateMilli = 400;
        public const int MedicalBedRateMilli = 200;
        public const int TreatmentSeconds = 5;

        public static bool TryGetPatientBed(SimulationWorld world, Entity patient,
            out OutdoorConstructedAssetState bed)
        {
            bed = null;
            if (patient == null || world == null) return false;
            return RoomBedFacilityService.IsValidOccupiedBed(world, patient.Id, RoomUsage.Medical, out bed) ||
                   RoomBedFacilityService.IsValidOccupiedBed(world, patient.Id, RoomUsage.Residential, out bed) ||
                   RoomBedFacilityService.IsValidOccupiedBed(world, patient.Id, RoomUsage.Prison, out bed);
        }

        public static int ResolveRateMilli(SimulationWorld world, Entity entity)
        {
            if (!entity.TryGet<LifecycleComponent>(out var life)) return 0;
            long rate = entity.TryGet<AttributesComponent>(out var attrs)
                ? attrs.GetFinal(AttributeId.HealthRecoveryRateMilli) : 0;
            if (life.IsIncapacitated || life.HerbDoses > 0)
                rate += BaseDyingRateMilli + (long)life.HerbDoses * HerbRateMilli;
            if (TryGetPatientBed(world, entity, out var bed) &&
                RoomBedFacilityService.BedHasUsage(world, bed, RoomUsage.Medical))
                rate += MedicalBedRateMilli;
            return (int)Math.Max(-1000000L, Math.Min(1000000L, rate));
        }

        // Called once per existing world second, or once per encounter real second while WorldTick is frozen.
        public static void AdvanceSecond(SimulationWorld world, Entity entity)
        {
            if (!entity.TryGet<LifecycleComponent>(out var life) ||
                !(life.IsIncapacitated || life.State == LifecycleState.Alive || life.State == LifecycleState.Captured)) return;
            if (!life.HasMedicalProgress && life.IsIncapacitated)
            {
                life.HasMedicalProgress = true;
                var remaining = life.BleedOutAfterTick > world.Tick.Value
                    ? Math.Min(200UL, life.BleedOutAfterTick - world.Tick.Value) : 0UL;
                life.DyingReserveMilli = (int)remaining * 1000;
            }
            // Most living characters are already at full health. Bed validation walks the
            // world's bed inventory; it cannot change a full-health character with no
            // pending medical progress or herb treatment, regardless of recovery rate.
            if (life.State == LifecycleState.Alive && !life.HasMedicalProgress && life.HerbDoses == 0 &&
                entity.TryGet<CombatVitalsComponent>(out var currentVitals) &&
                entity.TryGet<AttributesComponent>(out var currentAttributes) &&
                currentVitals.CurrentHp >= Math.Max(1, currentAttributes.GetFinal(AttributeId.MaxHp)))
                return;
            var rate = ResolveRateMilli(world, entity);
            if (life.IsDying)
            {
                if (rate > 0)
                {
                    life.Convalescing = true;
                    life.BleedOutAfterTick = 0;
                    return; // HP remains zero on the stabilization second.
                }
                life.DyingReserveMilli = Math.Max(0, life.DyingReserveMilli + rate);
                // Compatibility projection only: the fixed-point reserve is now authoritative.
                life.BleedOutAfterTick = rate < 0
                    ? world.Tick.Value + (ulong)Math.Max(1, (life.DyingReserveMilli - rate - 1) / -rate)
                    : ulong.MaxValue;
                if (life.DyingReserveMilli == 0)
                    CombatLifeStateService.TryConfirmDeath(world, XianXia.Core.Domain.Ids.EntityId.None,
                        entity, DeathConfirmationReason.BleedOut, out _);
                return;
            }
            if (rate <= 0 || !entity.TryGet<AttributesComponent>(out var attributes)) return;
            CombatDamageRules.EnsureVitals(entity);
            if (!entity.TryGet<CombatVitalsComponent>(out var vitals)) return;
            var maxHp = Math.Max(1, attributes.GetFinal(AttributeId.MaxHp));
            life.HasMedicalProgress = true;
            var total = (long)life.RecoveryHpMilli + rate;
            vitals.CurrentHp = (int)Math.Min(maxHp, (long)vitals.CurrentHp + total / 1000);
            life.RecoveryHpMilli = (int)(total % 1000);
            if (life.IsIncapacitated && (long)vitals.CurrentHp * 2 > maxHp)
            {
                var doses = life.HerbDoses;
                var fraction = life.RecoveryHpMilli;
                CombatLifeStateService.TryRecoverFromIncapacitated(world, entity, vitals.CurrentHp);
                life.HasMedicalProgress = true;
                life.HerbDoses = doses;
                life.RecoveryHpMilli = fraction;
            }
            if (vitals.CurrentHp >= maxHp) life.ResetMedicalProgress();
        }
    }
}
