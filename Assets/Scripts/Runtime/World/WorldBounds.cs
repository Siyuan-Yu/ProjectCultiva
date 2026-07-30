using UnityEngine;

namespace XianXia.Unity.World
{
    public sealed class WorldBounds : MonoBehaviour
    {
        [SerializeField] private Rect bounds = new(-40f, -25f, 80f, 50f);

        public Rect Bounds => bounds;

        public Vector2 Min => bounds.min;
        public Vector2 Max => bounds.max;
        public Vector2 Center => bounds.center;
        public Vector2 Size => bounds.size;

        public void Configure(Rect worldRect)
        {
            bounds = worldRect;
        }

        public Vector3 ClampPosition(Vector3 position, float paddingX, float paddingY)
        {
            float minX = bounds.xMin + paddingX;
            float maxX = bounds.xMax - paddingX;
            float minY = bounds.yMin + paddingY;
            float maxY = bounds.yMax - paddingY;

            if (minX > maxX)
            {
                position.x = bounds.center.x;
            }
            else
            {
                position.x = Mathf.Clamp(position.x, minX, maxX);
            }

            if (minY > maxY)
            {
                position.y = bounds.center.y;
            }
            else
            {
                position.y = Mathf.Clamp(position.y, minY, maxY);
            }

            return position;
        }
    }
}
