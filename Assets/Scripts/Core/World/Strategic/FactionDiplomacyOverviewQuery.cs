using System;
using System.Collections.Generic;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// 势力／外交总览的运行时只读汇总。
    /// 势力集合仅来自当前战略世界已引用的正式 authority，不把开局内容当作当前外交状态。
    /// </summary>
    public static class FactionDiplomacyOverviewQuery
    {
        public static void CollectRuntimeFactionIds(SimulationWorld world, List<string> into)
        {
            if (into == null)
                return;

            into.Clear();
            if (world?.Strategic == null)
                return;

            var ids = new HashSet<string>(StringComparer.Ordinal);
            Add(ids, world.Strategic.PlayerFactionId);

            foreach (var war in world.Strategic.Wars.EnumerateActive())
            {
                foreach (var factionId in war.Attackers)
                    Add(ids, factionId);
                foreach (var factionId in war.Defenders)
                    Add(ids, factionId);
            }

            foreach (var pair in world.Strategic.Vassalages.All)
            {
                Add(ids, pair.Key);
                Add(ids, pair.Value);
            }

            foreach (var alliance in world.Strategic.Alliances.All)
            {
                foreach (var factionId in alliance.Value)
                    Add(ids, factionId);
            }

            foreach (var pair in world.Strategic.FormalArmies.Armies)
                Add(ids, pair.Value?.FactionId);
            foreach (var pair in world.Strategic.Sites.Sites)
                Add(ids, pair.Value?.OwnerFactionId);
            foreach (var pair in world.Strategic.TerritoryRegions.Regions)
                Add(ids, pair.Value?.ControlFactionId);

            into.AddRange(ids);
            into.Sort(CompareFactionIds);
        }

        public static int CountControlledTerritoryRegions(SimulationWorld world, string factionId)
        {
            if (world?.Strategic?.TerritoryRegions == null || string.IsNullOrEmpty(factionId))
                return 0;

            var count = 0;
            foreach (var pair in world.Strategic.TerritoryRegions.Regions)
            {
                if (string.Equals(pair.Value?.ControlFactionId, factionId, StringComparison.Ordinal))
                    count++;
            }

            return count;
        }

        public static int CountFormalArmies(SimulationWorld world, string factionId)
        {
            if (world?.Strategic?.FormalArmies == null || string.IsNullOrEmpty(factionId))
                return 0;

            var count = 0;
            foreach (var pair in world.Strategic.FormalArmies.Armies)
            {
                if (string.Equals(pair.Value?.FactionId, factionId, StringComparison.Ordinal))
                    count++;
            }

            return count;
        }

        public static void CollectVassalIds(SimulationWorld world, string overlordFactionId, List<string> into)
        {
            if (into == null)
                return;

            into.Clear();
            if (world?.Strategic?.Vassalages == null || string.IsNullOrEmpty(overlordFactionId))
                return;

            foreach (var pair in world.Strategic.Vassalages.All)
            {
                if (string.Equals(pair.Value, overlordFactionId, StringComparison.Ordinal))
                    into.Add(pair.Key);
            }

            into.Sort(CompareFactionIds);
        }

        static void Add(HashSet<string> ids, string factionId)
        {
            if (!string.IsNullOrEmpty(factionId))
                ids.Add(factionId);
        }

        static int CompareFactionIds(string left, string right)
        {
            var leftSort = GetSortOrder(left);
            var rightSort = GetSortOrder(right);
            var bySort = leftSort.CompareTo(rightSort);
            return bySort != 0 ? bySort : string.CompareOrdinal(left, right);
        }

        static int GetSortOrder(string factionId)
        {
            return StrategicFactionCatalog.TryGetInstalled(factionId, out var presentation)
                ? presentation.SortOrder
                : int.MaxValue;
        }
    }
}
