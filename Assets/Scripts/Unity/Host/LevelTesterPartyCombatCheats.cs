using System.Collections.Generic;
using XianXia.Core.Attributes;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;

namespace XianXia.Unity.Host
{
    public readonly struct PartyCombatCheatResult
    {
        public PartyCombatCheatResult(int succeeded, int skipped)
        { Succeeded = succeeded; Skipped = skipped; }
        public int Succeeded { get; }
        public int Skipped { get; }
    }

    /// <summary>仅供 LevelTester Host 使用；调用者必须显式传入 PlayerParty.Members。</summary>
    public static class LevelTesterPartyCombatCheats
    {
        public static PartyCombatCheatResult AddAttack(
            SimulationWorld world, IReadOnlyList<EntityId> partyMembers) =>
            AddAttribute(world, partyMembers, AttributeId.Attack, 10, false);

        public static PartyCombatCheatResult AddMaxHpAndRefill(
            SimulationWorld world, IReadOnlyList<EntityId> partyMembers) =>
            AddAttribute(world, partyMembers, AttributeId.MaxHp, 50, true);

        public static PartyCombatCheatResult Refill(
            SimulationWorld world, IReadOnlyList<EntityId> partyMembers)
        {
            var succeeded = 0;
            var skipped = 0;
            if (world == null || partyMembers == null) return new PartyCombatCheatResult(0, 0);
            for (var i = 0; i < partyMembers.Count; i++)
            {
                if (!world.Entities.TryGet(partyMembers[i], out var entity) ||
                    CombatRecoveryService.RestoreToMaximum(entity).IsFailure)
                    skipped++;
                else
                    succeeded++;
            }
            return new PartyCombatCheatResult(succeeded, skipped);
        }

        static PartyCombatCheatResult AddAttribute(
            SimulationWorld world, IReadOnlyList<EntityId> partyMembers,
            AttributeId attribute, int delta, bool refill)
        {
            var succeeded = 0;
            var skipped = 0;
            if (world == null || partyMembers == null) return new PartyCombatCheatResult(0, 0);
            for (var i = 0; i < partyMembers.Count; i++)
            {
                if (!world.Entities.TryGet(partyMembers[i], out var entity) ||
                    !entity.TryGet<LifecycleComponent>(out var life) || life.IsDead || life.IsRemoved || life.IsIncapacitated ||
                    !entity.TryGet<AttributesComponent>(out var attrs))
                { skipped++; continue; }
                var next = (long)attrs.GetBase(attribute) + delta;
                attrs.SetBase(attribute, next > int.MaxValue ? int.MaxValue : next < int.MinValue ? int.MinValue : (int)next);
                if (refill)
                {
                    CombatDamageRules.EnsureVitals(entity);
                    if (!entity.TryGet<CombatVitalsComponent>(out var vitals))
                    { skipped++; continue; }
                    vitals.CurrentHp = System.Math.Max(1, attrs.GetFinal(AttributeId.MaxHp));
                    vitals.CurrentSpiritPower = System.Math.Max(0, attrs.GetFinal(AttributeId.SpiritPower));
                    vitals.PoolsInitialized = true;
                }
                succeeded++;
            }
            return new PartyCombatCheatResult(succeeded, skipped);
        }
    }
}
