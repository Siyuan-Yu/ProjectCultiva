using System;
using System.Collections.Generic;

namespace XianXia.Core.Construction
{
    public enum ConstructionPlacementKind
    {
        FactionFlag = 0
    }

    public sealed class ConstructionMaterialCost
    {
        public string ItemId { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public sealed class BuildingConstructionSpec
    {
        public string BuildingId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool UnlockedByDefault { get; set; }
        public ConstructionPlacementKind PlacementKind { get; set; }
        public float DismantleRefundRate { get; set; }
        public List<ConstructionMaterialCost> Costs { get; } = new List<ConstructionMaterialCost>();
    }

    /// <summary>Static runtime content shell; it owns no player progression or save authority.</summary>
    public sealed class ConstructionCatalog
    {
        readonly Dictionary<string, BuildingConstructionSpec> _buildings =
            new Dictionary<string, BuildingConstructionSpec>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, BuildingConstructionSpec> Buildings => _buildings;

        public void Clear() => _buildings.Clear();

        public bool Register(BuildingConstructionSpec spec)
        {
            if (spec == null || string.IsNullOrEmpty(spec.BuildingId))
                return false;
            _buildings[spec.BuildingId] = spec;
            return true;
        }

        public bool TryGet(string buildingId, out BuildingConstructionSpec spec)
        {
            spec = null;
            return !string.IsNullOrEmpty(buildingId) && _buildings.TryGetValue(buildingId, out spec);
        }
    }
}
