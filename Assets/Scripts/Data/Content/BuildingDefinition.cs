using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Data.Content
{
    public sealed class BuildingMaterialCostDefinition
    {
        public string ItemId { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    /// <summary>Content-only construction template. Buildings are not inventory items.</summary>
    public sealed class BuildingDefinition
    {
        public DefinitionId Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool UnlockedByDefault { get; set; }
        public string PlacementKind { get; set; } = string.Empty;
        public List<BuildingMaterialCostDefinition> Costs { get; set; } =
            new List<BuildingMaterialCostDefinition>();
        public float DismantleRefundRate { get; set; }
    }
}
