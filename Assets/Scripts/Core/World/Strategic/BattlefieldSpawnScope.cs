using System.Collections.Generic;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Encounter-scoped SpawnedEntityIds 访问层（ENCOUNTER-INV-03）。
    /// Active session 用 Encounter._activeSpawns；Lingering 用 Registry 内 scoped list。
    /// </summary>
    public static class BattlefieldSpawnScope
    {
        public static IList<ulong> GetMutableSpawnList(SimulationWorld world)
        {
            if (world?.Strategic?.Encounter == null)
                return null;

            var rt = world.Strategic.Encounter;
            if (!string.IsNullOrEmpty(rt.ActiveBattlefieldId) &&
                world.Strategic.LingeringBattlefields.TryGetById(rt.ActiveBattlefieldId, out var battlefield) &&
                battlefield != null)
                return battlefield.MutableSpawnedEntityIds;

            return rt.MutableActiveSpawnedIds;
        }

        public static IReadOnlyList<ulong> GetSpawnList(SimulationWorld world)
        {
            var list = GetMutableSpawnList(world);
            return list as IReadOnlyList<ulong>;
        }

        public static void TrackSpawn(SimulationWorld world, ulong entityId)
        {
            var list = GetMutableSpawnList(world);
            if (list == null || entityId == 0 || list.Contains(entityId))
                return;
            list.Add(entityId);
        }

        public static void RemoveTrackedSpawnAt(SimulationWorld world, int index)
        {
            var list = GetMutableSpawnList(world);
            if (list == null || index < 0 || index >= list.Count)
                return;
            list.RemoveAt(index);
        }

        public static void ClearScopedSpawns(SimulationWorld world)
        {
            GetMutableSpawnList(world)?.Clear();
        }

        public static bool IsTrackedInCurrentScope(SimulationWorld world, EntityId id)
        {
            if (world == null || id.IsNone)
                return false;
            var list = GetMutableSpawnList(world);
            if (list == null)
                return false;
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] == id.Value)
                    return true;
            }

            return false;
        }

        /// <summary>LocalMap 可见性：entity 必须属于当前 LocalMap Encounter scope。</summary>
        public static bool IsTrackedInCurrentLocalMapScope(SimulationWorld world, EntityId id)
        {
            if (world?.Strategic?.Encounter == null || id.IsNone)
                return false;

            var rt = world.Strategic.Encounter;
            if (!string.IsNullOrEmpty(rt.ActiveBattlefieldId))
                return IsTrackedInCurrentScope(world, id);

            return IsTrackedInCurrentScope(world, id);
        }

        public static bool TryFindOwningBattlefieldId(
            SimulationWorld world,
            EntityId id,
            out string battlefieldId)
        {
            battlefieldId = string.Empty;
            if (world == null || id.IsNone)
                return false;

            foreach (var battlefield in world.Strategic.LingeringBattlefields.Enumerate())
            {
                if (battlefield == null || !battlefield.ContainsSpawn(id.Value))
                    continue;
                battlefieldId = battlefield.BattlefieldId;
                return true;
            }

            return false;
        }

        public static void AssertNotCrossBattlefieldFinalize(
            SimulationWorld world,
            EntityId id,
            string operation)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (world == null || id.IsNone)
                return;

            var rt = world.Strategic?.Encounter;
            if (rt == null || string.IsNullOrEmpty(rt.ActiveBattlefieldId))
                return;

            if (!TryFindOwningBattlefieldId(world, id, out var ownerId))
                return;

            if (!string.Equals(ownerId, rt.ActiveBattlefieldId, System.StringComparison.Ordinal))
            {
                System.Diagnostics.Debug.Fail(
                    "[BattlefieldSpawnScope] Cross-battlefield " + operation +
                    ": active=" + rt.ActiveBattlefieldId + " owner=" + ownerId +
                    " entity=" + id);
            }
#endif
        }

        public static bool ShouldProtectFromScopedRemoval(
            SimulationWorld world,
            EntityId id,
            IList<ulong> activeScopeList)
        {
            if (world == null || id.IsNone || activeScopeList == null)
                return false;

            // 已登记在 Registry 某 battlefield 下的 entity：当前 scope 不是其 owner 时禁止 FinalizeRemoval
            if (TryFindOwningBattlefieldId(world, id, out var ownerId))
            {
                var rt = world.Strategic?.Encounter;
                if (rt == null || string.IsNullOrEmpty(rt.ActiveBattlefieldId))
                    return true;
                if (!string.Equals(ownerId, rt.ActiveBattlefieldId, System.StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}
