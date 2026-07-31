using System;
using XianXia.Core;

namespace XianXia.Data
{
    /// <summary>
    /// Phase-1 scaffold marker. ContentPackage / JSON snapshot land in later phases.
    /// typeof(CoreAssemblyMarker) keeps a real assembly reference to XianXia.Core.
    /// </summary>
    public static class DataAssemblyMarker
    {
        public const string MilestoneId = "CoreMilestone1";
        public const int Phase = 1;

        public static Type CoreMarkerType => typeof(CoreAssemblyMarker);
    }
}
