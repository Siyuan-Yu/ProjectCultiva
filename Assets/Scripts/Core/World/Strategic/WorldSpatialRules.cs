using System;
using System.Collections.Generic;

namespace XianXia.Core.World.Strategic
{
    public sealed class CoreLevelControlRange
    {
        public int Level { get; set; }
        public float WidthWorld { get; set; }
        public float HeightWorld { get; set; }
    }

    /// <summary>Content-owned metrics. Building collision/placement never consumes this catalog.</summary>
    public sealed class WorldSpatialRules
    {
        public string Id { get; set; } = string.Empty;
        public List<CoreLevelControlRange> CoreLevels { get; } = new List<CoreLevelControlRange>();
        public float WildernessEncounterWidthWorld { get; set; }
        public float WildernessEncounterHeightWorld { get; set; }
        public float InterventionDecisionSeconds { get; set; }
        public float InterventionArrivalSeconds { get; set; }
        public int InterventionRelationThreshold { get; set; }
        public int InterventionChanceBasisPoints { get; set; }

        public CoreLevelControlRange RequireLevel(int level)
        {
            for (var i = 0; i < CoreLevels.Count; i++)
                if (CoreLevels[i].Level == level) return CoreLevels[i];
            throw new InvalidOperationException("Missing core control range for level " + level);
        }

        public void Bind(WorldSite site)
        {
            var range = RequireLevel(site.CoreLevel);
            site.CoreRangeWidth = range.WidthWorld;
            site.CoreRangeHeight = range.HeightWorld;
        }
    }
}
