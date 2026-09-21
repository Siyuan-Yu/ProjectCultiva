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
    /// Minimal world initialization payload. Region identity is required by runtime/save scope.
    /// </summary>
    public sealed class WorldInitData
    {
        public List<RegionData> Regions { get; set; } = new List<RegionData>();
    }
}
