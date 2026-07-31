using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Core.World
{
    /// <summary>
    /// VS0.1 placeholder Region layout. No movement / map gameplay.
    /// </summary>
    public sealed class RegionData
    {
        public RegionId Id { get; set; }
        public string Name { get; set; }
    }

    /// <summary>
    /// VS0.1 placeholder LocalMap layout. No instance gameplay.
    /// </summary>
    public sealed class LocalMapData
    {
        public ulong Id { get; set; }
        public RegionId RegionId { get; set; }
        public string Name { get; set; }
    }

    /// <summary>
    /// VS0.1 placeholder Settlement layout. No work / economy gameplay.
    /// </summary>
    public sealed class SettlementData
    {
        public ulong Id { get; set; }
        public RegionId RegionId { get; set; }
        public string Name { get; set; }
    }

    /// <summary>
    /// Minimal world initialization payload for Vertical Slice 0.1 bootstrap.
    /// </summary>
    public sealed class WorldInitData
    {
        public List<RegionData> Regions { get; set; } = new List<RegionData>();
        public List<LocalMapData> LocalMaps { get; set; } = new List<LocalMapData>();
        public List<SettlementData> Settlements { get; set; } = new List<SettlementData>();
    }
}
