using System;
using System.Collections.Generic;
using XianXia.Core.Simulation;

namespace XianXia.Core.Exploration
{
    /// <summary>Enumerates entrances from the currently playable space without copying boards.</summary>
    public static class OpportunityEntranceQuery
    {
        public static IEnumerable<WorldLocationState> EnumerateAvailableEntrances(SimulationWorld world)
        {
            if (world == null) yield break;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (!world.LocalMap.IsInInterior)
                foreach (var pair in world.ContinuousOutdoorMaterialization.PlacesByLocationId)
                    if (pair.Value != null && seen.Add(pair.Key)) yield return pair.Value;
            foreach (var pair in world.LocalPlaces.Locations)
                if (pair.Value != null && seen.Add(pair.Key)) yield return pair.Value;
        }

        public static string Source(SimulationWorld world, string entranceId) =>
            world != null && !world.LocalMap.IsInInterior &&
            world.ContinuousOutdoorMaterialization.TryGetAnyPlace(entranceId, out _)
                ? "ContinuousOutdoor" : "InteriorLegacy";
    }
}
