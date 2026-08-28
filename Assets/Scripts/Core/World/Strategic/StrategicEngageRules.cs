using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>?????????Pure Hex ??????</summary>
    public static class StrategicEngageRules
    {
        public static bool IsAgentColocatedWithStack(
            SimulationWorld world,
            WorldAgentPresence p,
            ArmyStack stack)
        {
            if (world == null || p == null || stack == null)
                return false;

            if (!ArmyStackAdapter.TryGetFormalArmy(world, stack, out var defender) ||
                defender == null ||
                !defender.UsesHexStrategicPosition)
                return false;

            var agentId = p.EntityId;
            if (agentId.IsNone)
                return false;

            if (ArmyService.TryGetArmyForCharacter(world, agentId, out var attacker) &&
                attacker != null &&
                attacker.UsesHexStrategicPosition)
                return BattleEngagementTriggerService.TryDetectEngagementContact(
                    world, attacker, defender);

            if (BattleEngagementSpatialQuery.TryGetCommittedCharacterHex(
                    world, agentId, out var agentHex))
            {
                var supportArea = BattleEngagementSupportArea.ResolveAndFreeze(
                    world, defender.ArmyId);
                return supportArea.HasValue && supportArea.Contains(agentHex);
            }

            if (p.UsesHexPresence)
            {
                var supportArea = BattleEngagementSupportArea.ResolveAndFreeze(
                    world, defender.ArmyId);
                return supportArea.HasValue && supportArea.Contains(p.ResidualHex);
            }

            if (p.Mode == PartyWorldPresenceMode.AtSite &&
                !string.IsNullOrEmpty(p.SiteId) &&
                world.Strategic.Sites.TryGet(p.SiteId, out var site) &&
                site != null &&
                site.OccupiesHex(defender.CurrentHex))
                return true;

            return false;
        }

        public static bool CanEngageStackNow(
            SimulationWorld world,
            IReadOnlyList<EntityId> party,
            ArmyStack stack)
        {
            if (world == null || stack == null || party == null || party.Count == 0)
                return false;

            for (var i = 0; i < party.Count; i++)
            {
                if (party[i].IsNone ||
                    !world.WorldPresence.TryGet(party[i], out var p) ||
                    p == null)
                    continue;
                if (IsAgentColocatedWithStack(world, p, stack))
                    return true;
            }

            return false;
        }

        public static void CollectPartyReadyToEngageStack(
            SimulationWorld world,
            IReadOnlyList<EntityId> party,
            ArmyStack stack,
            List<EntityId> into)
        {
            into.Clear();
            if (world == null || stack == null || party == null || into == null)
                return;

            for (var i = 0; i < party.Count; i++)
            {
                if (party[i].IsNone)
                    continue;
                if (!world.WorldPresence.TryGet(party[i], out var p) || p == null)
                    continue;
                if (!IsAgentColocatedWithStack(world, p, stack))
                    continue;
                into.Add(party[i]);
            }
        }
    }
}
