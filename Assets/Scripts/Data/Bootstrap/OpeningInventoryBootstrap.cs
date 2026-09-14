using System.Collections.Generic;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Data.Content;

namespace XianXia.Data.Bootstrap
{
    /// <summary>NewGame only. Restore uses saved PartyInventorySlots.</summary>
    public static class OpeningInventoryBootstrap
    {
        public static Result Apply(SimulationWorld world, OpeningScenarioDefinition scenario)
        {
            if (scenario == null) return Result.Success();
            var added = new List<OpeningStartingInventoryEntry>();
            foreach (var entry in scenario.StartingInventory)
            {
                var count = entry != null && entry.Count > 0 && world.InventoryCatalog.TryGet(entry.ItemId, out _)
                    ? world.Inventory.TryAdd(entry.ItemId, entry.Count) : 0;
                if (count > 0) added.Add(new OpeningStartingInventoryEntry { ItemId = entry.ItemId, Count = count });
                if (entry == null || entry.Count <= 0 || count != entry.Count)
                {
                    foreach (var prior in added) world.Inventory.TryRemoveAll(prior.ItemId, prior.Count);
                    return Result.Failure(ErrorCode.ContentLoadFailed, "开局背包无法完整容纳 startingInventory。", scenario.Id.ToString());
                }
            }
            return Result.Success();
        }
    }
}
