using System;
using System.Collections.Generic;
using UnityEngine;

namespace XianXia.Unity.Host
{
    public sealed class HostDynamicWorldObjectEntry
    {
        public string WorldObjectInstanceId = string.Empty;
        public string OpportunityInstanceId = string.Empty;
        public string OpportunityDefinitionId = string.Empty;
        public string SurfaceId = string.Empty;
        public Bounds PresentationBounds;
        public Vector3 ApproachPosition;
        public string DisplayLabel = string.Empty;
        public GameObject View;
    }

    /// <summary>Transient picker registry for non-authoritative dynamic opportunity visuals.</summary>
    public static class HostDynamicWorldObjectRegistry
    {
        static readonly Dictionary<string, HostDynamicWorldObjectEntry> Entries =
            new Dictionary<string, HostDynamicWorldObjectEntry>(StringComparer.Ordinal);

        public static IReadOnlyDictionary<string, HostDynamicWorldObjectEntry> All => Entries;
        public static void Register(HostDynamicWorldObjectEntry entry)
        {
            if (entry != null && !string.IsNullOrWhiteSpace(entry.WorldObjectInstanceId))
                Entries[entry.WorldObjectInstanceId] = entry;
        }
        public static bool TryResolve(string id, out HostDynamicWorldObjectEntry entry) =>
            Entries.TryGetValue(id ?? string.Empty, out entry) && entry?.View != null;
        public static bool Remove(string id) => Entries.Remove(id ?? string.Empty);
        public static void Clear() => Entries.Clear();
        public static bool TryPick(Vector3 point, out HostDynamicWorldObjectEntry picked)
        {
            picked = null; var best = float.MaxValue;
            foreach (var entry in Entries.Values)
            {
                if (entry?.View == null || !entry.PresentationBounds.Contains(point)) continue;
                var distance = (entry.PresentationBounds.center - point).sqrMagnitude;
                if (distance >= best) continue;
                best = distance; picked = entry;
            }
            return picked != null;
        }
    }
}
