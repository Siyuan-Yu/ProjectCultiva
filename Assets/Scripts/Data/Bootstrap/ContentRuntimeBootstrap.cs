using System.Collections.Generic;
using XianXia.Core.Content;
using XianXia.Core.Inventory;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Data.Content;

namespace XianXia.Data.Bootstrap
{
    /// <summary>Maps quest／content-event definitions onto session boards.</summary>
    public static class ContentRuntimeBootstrap
    {
        public static Result Apply(SimulationWorld world, DefinitionRegistry registry)
        {
            if (world == null || registry == null)
                return Result.Failure(ErrorCode.InvalidArgument, "ContentRuntime bootstrap args null.");

            RehydrateInventoryCatalog(world, registry);

            foreach (var kv in registry.Quests)
            {
                var def = kv.Value;
                var spec = new QuestSpec
                {
                    Id = def.Id.ToString(),
                    Name = def.Name ?? string.Empty,
                    Description = def.Description ?? string.Empty,
                    AutoOffer = def.AutoOffer,
                    Abandonable = def.Abandonable,
                    DeadlineDays = def.DeadlineDays
                };
                spec.OfferConditions.AddRange(def.OfferConditions);
                spec.CompleteConditions.AddRange(def.CompleteConditions);
                spec.FailConditions.AddRange(def.FailConditions);
                spec.Rewards.AddRange(def.Rewards);
                spec.FailResults.AddRange(def.FailResults);
                world.Quests.Register(spec);
            }

            foreach (var kv in registry.ContentEvents)
            {
                var def = kv.Value;
                var spec = new ContentEventSpec
                {
                    Id = def.Id.ToString(),
                    Name = def.Name ?? string.Empty,
                    Body = def.Body ?? string.Empty,
                    Trigger = def.Trigger ?? string.Empty,
                    LocationId = def.LocationId ?? string.Empty,
                    QuestId = def.QuestId ?? string.Empty,
                    NpcDefinitionId = def.NpcDefinitionId ?? string.Empty,
                    Once = def.Once
                };
                spec.Conditions.AddRange(def.Conditions);
                for (var i = 0; i < def.Choices.Count; i++)
                {
                    var c = def.Choices[i];
                    var choice = new ContentEventChoiceSpec
                    {
                        Id = c.Id ?? string.Empty,
                        Text = c.Text ?? string.Empty
                    };
                    choice.Conditions.AddRange(c.Conditions);
                    choice.Outcomes.AddRange(c.Outcomes);
                    spec.Choices.Add(choice);
                }

                world.ContentEvents.Register(spec);
            }

            return Result.Success();
        }

        /// <summary>
        /// 从静态内容恢复物品目录。背包槽位与数量不属于此处，不会被清空或重发。
        /// </summary>
        internal static void RehydrateInventoryCatalog(SimulationWorld world, DefinitionRegistry registry)
        {
            var catalog = world.InventoryCatalog;
            catalog.Clear();
            foreach (var kv in registry.Resources)
            {
                var r = kv.Value;
                var id = r.Id.ToString();
                var tags = new List<string> { "resource" };
                AppendHeuristicTags(id, tags);
                catalog.Register(id, r.Name ?? id, 99, tags);
            }

            foreach (var kv in registry.Items)
            {
                var item = kv.Value;
                var id = item.Id.ToString();
                catalog.Register(
                    id, item.Name ?? id, item.MaxStack, item.Tags,
                    item.TeachesManualId, item.TeachesArtId);
            }

            world.Inventory.SetSlotCapacity(PartyInventory.DefaultSlotCapacity);
        }

        static void AppendHeuristicTags(string id, List<string> tags)
        {
            if (string.IsNullOrEmpty(id))
                return;
            if (id.IndexOf("wood", System.StringComparison.OrdinalIgnoreCase) >= 0)
                tags.Add("wood");
            if (id.IndexOf("herb", System.StringComparison.OrdinalIgnoreCase) >= 0)
                tags.Add("herb");
            if (id.IndexOf("grain", System.StringComparison.OrdinalIgnoreCase) >= 0)
                tags.Add("grain");
            if (id.IndexOf("conceal", System.StringComparison.OrdinalIgnoreCase) >= 0)
                tags.Add("consumable");
        }
    }
}
