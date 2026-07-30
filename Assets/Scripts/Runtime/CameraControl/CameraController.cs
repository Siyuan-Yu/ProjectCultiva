using UnityEngine;
using XianXia.Unity.World;

namespace XianXia.Unity.CameraControl
{
    /// <summary>
    /// 鼠标滚轮缩放，并限制镜头不超出地图边界。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraController : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private WorldBounds worldBounds;
        [SerializeField] private float minOrthographicSize = 6f;
        [SerializeField] private float maxOrthographicSize = 22f;
        [SerializeField] private float zoomSpeed = 3f;

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = GetComponent<Camera>();
            }
        }

        public void Configure(Camera camera, WorldBounds bounds, float startSize)
        {
            targetCamera = camera;
            worldBounds = bounds;
            if (targetCamera != null)
            {
                targetCamera.orthographicSize = Mathf.Clamp(startSize, minOrthographicSize, maxOrthographicSize);
            }

            ClampToBounds();
        }

        private void LateUpdate()
        {
            if (targetCamera == null)
            {
                return;
            }

            float scroll = UnityEngine.Input.mouseScrollDelta.y;
            if (!Mathf.Approximately(scroll, 0f))
            {
                targetCamera.orthographicSize = Mathf.Clamp(
                    targetCamera.orthographicSize - scroll * zoomSpeed,
                    minOrthographicSize,
                    maxOrthographicSize);
            }

            ClampToBounds();
        }

        private void ClampToBounds()
        {
            if (worldBounds == null || targetCamera == null)
            {
                return;
            }

            float halfHeight = targetCamera.orthographicSize;
            float halfWidth = halfHeight * targetCamera.aspect;
            Vector3 position = transform.position;
            position = worldBounds.ClampPosition(position, halfWidth, halfHeight);
            transform.position = position;
        }
    }
}
