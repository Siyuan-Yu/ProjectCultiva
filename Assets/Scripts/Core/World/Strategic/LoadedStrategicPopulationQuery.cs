using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// 只读：FormalArmy living member / Strategic Residual 是否已作为「普通战略人口」
    /// materialize 到当前 Loaded Real LocalMap。
    /// 这是 World → LocalMap 的正常人口桥，不依赖 Battle Encounter / ParticipantSnapshot /
    /// BattlefieldSpawnScope —— 战斗结束后实体仍物理在场即应继续可见。
    /// </summary>
    public static class LoadedStrategicPopulationQuery
    {
        public static bool IsMaterializedStrategicCharacterOnLoadedMap(
            SimulationWorld world,
            EntityId id)
        {
            if (world == null || id.IsNone)
                return false;
            if (world.LocalMap == null || !world.LocalMap.ContainsOccupant(id))
                return false;
            if (!LoadedLocalMapBelongingQuery.TryResolveLoadedLocalMap(world, out var context))
                return false;

            // FormalArmy living member：位置真源 = army.WorldMotion。
            if (ArmyService.TryGetArmyForCharacter(world, id, out var army) && army != null)
            {
                if (!LingeringBattlefieldPartyService.IsLivingForMacroOrder(world, id))
                    return false; // 阵亡 member（未 detach 前）不在此列；退役后走 residual。
                return LoadedStrategicPopulationMaterializer.BelongsArmyToLoadedMap(
                    world, context, army);
            }

            // Strategic Residual（Incapacitated / visible Corpse，非 FormalArmy）。
            if (!StrategicResidualPresenceService.IsStrategicResidualCandidate(world, id))
                return false;
            return LoadedStrategicPopulationMaterializer.BelongsResidualToLoadedMap(
                world, context, id);
        }
    }
}
