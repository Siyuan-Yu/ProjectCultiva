using System;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Npc
{
    /// <summary>Shared first-strike gate. Opinion axes alone never authorize attack.</summary>
    public static class NpcHostilityService
    {
        const string StartTag = "private_hostility_start";
        const string EndTag = "private_hostility_end";

        public static void SetPrivateHostility(SimulationWorld world, EntityId from, EntityId to, bool hostile)
        {
            if (world == null || from.IsNone || to.IsNone || from == to ||
                !world.Entities.TryGet(from, out _) || !world.Entities.TryGet(to, out _)) return;
            world.Relationships.Append(new RelationshipEvent(world.Tick, from, to,
                SocialAttitudeAxis.Grudge, 0, hostile ? StartTag : EndTag));
        }

        public static bool HasPrivateHostility(SimulationWorld world, EntityId from, EntityId to)
        {
            return world?.Relationships?.HasPrivateHostility(from, to) == true;
        }

        public static bool CanAutoAttack(SimulationWorld world, EntityId attacker, EntityId target)
        {
            if (world == null || attacker.IsNone || target.IsNone || attacker == target ||
                !world.Entities.TryGet(attacker, out var a) ||
                !world.Entities.TryGet(target, out var b)) return false;
            if (!XianXia.Core.Combat.CombatLifeStateService.CanFight(a) ||
                !XianXia.Core.Combat.CombatLifeStateService.CanFight(b)) return false;
            if (world.Civilians.TryGet(target, out var civilian) &&
                civilian.Disposition == CivilianDisposition.SurrenderWaiting) return false;
            a.TryGet<FactionMembershipComponent>(out var am);
            b.TryGet<FactionMembershipComponent>(out var bm);
            var af = am?.FactionId ?? string.Empty;
            var bf = bm?.FactionId ?? string.Empty;
            if (!string.IsNullOrEmpty(af) && string.Equals(af, bf, StringComparison.Ordinal)) return false;
            var contentHostile = a.TryGet<PersonalityProfileComponent>(out var ap) && ap.HasTag("hostile");
            if (MortalCivilianQuery.IsManagedCivilian(world, a) && !contentHostile) return false;
            if (HasPrivateHostility(world, attacker, target)) return true;
            if (contentHostile) return true;
            if (b.TryGet<PersonalityProfileComponent>(out var bp) && bp.HasTag("hostile")) return true;
            // Unarmed, managed mortals obey their civilian flee/custody loop, not an assault order.
            return WarGateService.IsAtWar(world, af, bf);
        }
    }
}
