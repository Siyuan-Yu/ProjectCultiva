using UnityEngine;
using XianXia.Unity.World;

namespace XianXia.Unity.CameraControl
{
    /// <summary>
    /// 中键拖拽平移、滚轮缩放，并限制镜头不超出地图边界。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraController : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private WorldBounds worldBounds;
        [SerializeField] private float minOrthographicSize = 6f;
        [SerializeField] private float maxOrthographicSize = 22f;
        [SerializeField] private float zoomSpeed = 3f;

        private bool _panning;
        private Vector3 _lastPanScreenPosition;

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

            HandleZoom();
            HandlePan();
            ClampToBounds();
        }

        private void HandleZoom()
        {
            float scroll = UnityEngine.Input.mouseScrollDelta.y;
            if (Mathf.Approximately(scroll, 0f))
            {
                return;
            }

            targetCamera.orthographicSize = Mathf.Clamp(
                targetCamera.orthographicSize - scroll * zoomSpeed,
                minOrthographicSize,
                maxOrthographicSize);
        }

        private void HandlePan()
        {
            if (UnityEngine.Input.GetMouseButtonDown(2))
            {
                _panning = true;
                _lastPanScreenPosition = UnityEngine.Input.mousePosition;
            }

            if (UnityEngine.Input.GetMouseButtonUp(2))
            {
                _panning = false;
            }

            if (!_panning || !UnityEngine.Input.GetMouseButton(2))
            {
                return;
            }

            Vector3 currentScreen = UnityEngine.Input.mousePosition;
            Vector3 lastWorld = targetCamera.ScreenToWorldPoint(_lastPanScreenPosition);
            Vector3 currentWorld = targetCamera.ScreenToWorldPoint(currentScreen);
            // 抓取地图：鼠标往哪拖，地图跟着往哪走。
            transform.position += lastWorld - currentWorld;
            _lastPanScreenPosition = currentScreen;
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
