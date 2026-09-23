using XianXia.Core.Random;
using XianXia.Data.Content;

namespace XianXia.Data.Bootstrap
{
    public static class SpawnTableWeightedPicker
    {
        public static string PickDefinitionId(SpawnTableDefinition table, IRandomSource random)
        {
            if (table?.Entries == null || table.Entries.Count == 0 || random == null) return string.Empty;
            var index = WeightedRandomPicker.PickIndex(
                table.Entries.Count,
                i => table.Entries[i]?.Weight ?? 0,
                random);
            return index >= 0 ? table.Entries[index]?.DefinitionId?.Trim() ?? string.Empty : string.Empty;
        }
    }
}
