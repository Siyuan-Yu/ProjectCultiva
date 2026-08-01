using UnityEngine;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Demo-aligned 2D plane: world XY, camera looks along -Z.
    /// Content PresentationX/Z map to (x, y).
    /// </summary>
    public static class HostPresentationSpace
    {
        public const float EntityZ = 0f;
        public const float GroundZ = 1f;
        public const float BuildingZ = 0.5f;

        public static Vector3 FromPresentation(float presentationX, float presentationZ, float z = EntityZ)
        {
            return new Vector3(presentationX, presentationZ, z);
        }

        public static Vector2 ToPresentation(Vector3 world)
        {
            return new Vector2(world.x, world.y);
        }

        public static bool TryRaycastPlane(Camera cam, Vector3 screen, out Vector3 worldPoint)
        {
            worldPoint = default;
            if (cam == null)
                return false;
            var ray = cam.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.forward, Vector3.zero);
            if (!plane.Raycast(ray, out var enter))
                return false;
            worldPoint = ray.GetPoint(enter);
            worldPoint.z = EntityZ;
            return true;
        }
    }
}
