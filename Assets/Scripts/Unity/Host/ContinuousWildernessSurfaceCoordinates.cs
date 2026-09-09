using UnityEngine;
using XianXia.Core.World.Hex;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Pure Presentation ↔ Surface-local conversion for W1B loaded pairs.
    /// When inactive, presentation == legacy local (identity passthrough).
    /// </summary>
    public static class ContinuousWildernessSurfaceCoordinates
    {
        public static bool PresentationToSurfaceLocal(
            bool loadedSetActive,
            Vector2 placementOffset,
            float presentationX,
            float presentationY,
            out float surfaceLocalX,
            out float surfaceLocalY)
        {
            if (!loadedSetActive)
            {
                surfaceLocalX = presentationX;
                surfaceLocalY = presentationY;
                return true;
            }

            surfaceLocalX = presentationX - placementOffset.x;
            surfaceLocalY = presentationY - placementOffset.y;
            return true;
        }

        public static bool SurfaceLocalToPresentation(
            bool loadedSetActive,
            Vector2 placementOffset,
            float surfaceLocalX,
            float surfaceLocalY,
            out float presentationX,
            out float presentationY)
        {
            if (!loadedSetActive)
            {
                presentationX = surfaceLocalX;
                presentationY = surfaceLocalY;
                return true;
            }

            presentationX = surfaceLocalX + placementOffset.x;
            presentationY = surfaceLocalY + placementOffset.y;
            return true;
        }

        public static Vector2 OffsetForHex(
            HexCoord hex,
            HexCoord hexA,
            HexCoord hexB,
            Vector2 offsetA,
            Vector2 offsetB)
        {
            if (hex.Equals(hexA))
                return offsetA;
            if (hex.Equals(hexB))
                return offsetB;
            return Vector2.zero;
        }
    }
}
