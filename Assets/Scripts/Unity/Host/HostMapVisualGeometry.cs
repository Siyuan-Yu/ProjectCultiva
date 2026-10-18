using UnityEngine;

namespace XianXia.Unity.Host
{
    public sealed class HostMapVisualGeometry : MonoBehaviour
    {
        Bounds _localBounds;

        public void Configure(Vector3 center, float width, float height)
        {
            var scale = transform.lossyScale;
            _localBounds = new Bounds(transform.InverseTransformPoint(center),
                new Vector3(width / Mathf.Max(.0001f, Mathf.Abs(scale.x)),
                    height / Mathf.Max(.0001f, Mathf.Abs(scale.y)), 0f));
        }

        public Rect WorldRect
        {
            get
            {
                var min = _localBounds.min;
                var max = _localBounds.max;
                var first = transform.TransformPoint(min);
                var bounds = new Bounds(first, Vector3.zero);
                bounds.Encapsulate(transform.TransformPoint(max));
                bounds.Encapsulate(transform.TransformPoint(new Vector3(min.x, max.y)));
                bounds.Encapsulate(transform.TransformPoint(new Vector3(max.x, min.y)));
                return Rect.MinMaxRect(bounds.min.x, bounds.min.y, bounds.max.x, bounds.max.y);
            }
        }
    }

    public static class HostArtDepth
    {
        public static int Order(float footY) => Mathf.Clamp(24000 - Mathf.RoundToInt(footY * 4f), -30000, 30000);

        public static int ObjectOrder(string kind, float centerY, float height, int groundOrder = -25)
        {
            switch (kind)
            {
                case "treeS": case "treeM": case "treeL":
                case "wall": case "wallSegment": case "room":
                case "storageRoom": case "controlCore": case "cave": case "rock":
                    return Order(centerY - height * .5f);
                case "bed": case "ore": case "mine": case "factionControlPost":
                    return Order(centerY);
                default: return groundOrder;
            }
        }
    }
}
