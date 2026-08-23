using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.Social;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// 战略 Encounter / Lingering 场景下的敌对判定：优先 Faction + WarGate，非 Personality 标签。
    /// </summary>
    public static class StrategicEncounterHostilityService
    {
        public static bool IsInStrategicEncounterLocalMap(SimulationWorld world)
        {
            if (world?.Strategic?.Encounter == null)
                return false;
            var rt = world.Strategic.Encounter;
            return !string.IsNullOrEmpty(rt.ActiveBattlefieldId) ||
                   rt.HasEngagedParty ||
                   rt.SpawnOnNextMapLoad ||
                   rt.SpawnedEntityIds.Count > 0;
        }

        public static bool IsStrategicCombatParticipant(SimulationWorld world, EntityId npcId)
        {
            if (world == null || npcId.IsNone)
                return false;

            if (BattlefieldSpawnScope.IsTrackedInCurrentLocalMapScope(world, npcId))
                return true;

            var snap = world.Strategic?.Participants;
            return snap != null && snap.FindByEntity(npcId) != null;
        }

        /// <summary>
        /// 遭遇 LocalMap 上是否应显示该实体（scoped spawn / 已进场 / 正式 Participant）。
        /// 禁止把同 Node 的非参战 AtNode 战略 NPC 画进战场。
        /// </summary>
        public static bool IsVisibleOnEncounterLocalMap(SimulationWorld world, EntityId id)
        {
            if (world == null || id.IsNone)
                return false;

            if (BattlefieldSpawnScope.IsTrackedInCurrentLocalMapScope(world, id))
                return true;

            var rt = world.Strategic?.Encounter;
            if (rt != null && rt.IsEngaged(id))
                return true;

            var rec = world.Strategic?.Participants?.FindByEntity(id);
            if (rec == null)
                return false;

            if (rec.Kind == BattleParticipantKind.OptionalFriendly)
                return rec.Selected;

            return rec.Kind == BattleParticipantKind.MandatoryFriendly ||
                   rec.Kind == BattleParticipantKind.EnemyPrimary ||
                   rec.Kind == BattleParticipantKind.EnemyReinforcement;
        }

        public static bool IsHostileStrategicNpc(SimulationWorld world, Entity entity)
        {
            if (world == null || entity == null)
                return false;

            if (!IsInStrategicEncounterLocalMap(world))
                return false;

            if (!IsStrategicCombatParticipant(world, entity.Id))
                return false;

            if (!entity.TryGet<FactionMembershipComponent>(out var mem) || mem == null)
                return false;

            var playerFaction = world.Strategic?.PlayerFactionId ?? StrategicFactionCatalog.PlayerFactionId;
            return WarGateService.CanAttack(world, playerFaction, mem.FactionId);
        }
    }
}
