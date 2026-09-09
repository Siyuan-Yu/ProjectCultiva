using UnityEngine;
using XianXia.Data.Content;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Transient presentation/loading ownership for one loaded surface layout instance.
    /// This is neither a Domain identity nor Save authority.
    /// </summary>
    public sealed class SurfacePresentationInstance
    {
        public SurfacePresentationInstance(
            string instanceKey,
            MapLayoutDefinition sourceLayout,
            Vector2 placementOffset,
            Transform root)
        {
            InstanceKey = instanceKey ?? string.Empty;
            SourceLayout = sourceLayout;
            PlacementOffset = placementOffset;
            Root = root;
            IsLoaded = root != null;
        }

        public string InstanceKey { get; }
        public MapLayoutDefinition SourceLayout { get; }
        public Vector2 PlacementOffset { get; }
        public Transform Root { get; }
        public bool IsLoaded { get; internal set; }
    }
}
