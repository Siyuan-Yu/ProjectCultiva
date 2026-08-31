using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// FormalArmy StrategicPosition ? ?? WorldAgentPresence ?????Pure Hex??
    /// </summary>
    public static class ArmyPresenceAdapter
    {
        public static void SyncFromArmy(SimulationWorld world, FormalArmy army)
        {
            if (world == null || army == null)
                return;

            for (var i = 0; i < army.MemberCharacterIds.Count; i++)
            {
                var memberId = new EntityId(army.MemberCharacterIds[i]);
                if (memberId.IsNone)
                    continue;
                if (!LingeringBattlefieldPartyService.IsLivingForMacroOrder(world, memberId))
                    continue;
                if (!world.WorldPresence.TryGet(memberId, out var presence) || presence == null)
                    continue;

                ProjectMemberPresence(world, army, presence, memberId);
            }
        }

        public static void SyncAll(SimulationWorld world)
        {
            if (world?.Strategic?.FormalArmies == null)
                return;
            foreach (var kv in world.Strategic.FormalArmies.Armies)
                SyncFromArmy(world, kv.Value);
        }

        static void ProjectMemberPresence(
            SimulationWorld world,
            FormalArmy army,
            WorldAgentPresence presence,
            EntityId memberId)
        {
            // Phase 5S-B2-3.1：Physical Presence = FormalArmy.WorldMotion 派生（单一 authority）。
            // 必须先做 physical sync —— SetAtSite / SetAtWorldPosition 会 ClearCombatPursuit，
            // 因此再附加 pursuit metadata。legacy AtSite/SetAtHex（基于 army.CurrentHex）不再
            // 覆盖连续 WorldMotion 语义（CommitArmyAtExactBattleHex 之后成员不得被降回错误 hex）。
            FormalArmyMemberPresenceSync.SyncMember(world, army, memberId);

            var pursueStackId = ResolvePursuitStackId(world, army);
            if (string.IsNullOrEmpty(pursueStackId))
                presence.ClearCombatPursuit();
            else
                presence.CombatPursuitStackId = pursueStackId;
        }

        static string ResolvePursuitStackId(SimulationWorld world, FormalArmy army)
        {
            var rt = world?.Strategic?.Encounter;
            if (rt == null || army == null || string.IsNullOrEmpty(army.ArmyId))
                return string.Empty;
            if (!string.Equals(rt.PursueAttackerArmyId, army.ArmyId, System.StringComparison.Ordinal))
                return string.Empty;
            return rt.PursueStackId ?? string.Empty;
        }
    }
}
