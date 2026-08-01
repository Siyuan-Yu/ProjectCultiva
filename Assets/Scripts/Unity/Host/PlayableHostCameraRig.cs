using UnityEngine;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Demo-aligned 2D ortho camera on XY: middle-mouse pan, scroll zoom, WASD optional.
    /// </summary>
    public sealed class PlayableHostCameraRig : MonoBehaviour
    {
        [SerializeField] Camera targetCamera;
        [SerializeField] Vector3 focus = new Vector3(-4f, 2f, 0f);
        [SerializeField] float orthographicSize = 12f;
        [SerializeField] float cameraZ = -10f;
        [SerializeField] float panSpeed = 12f;
        [SerializeField] float zoomSpeed = 2f;
        [SerializeField] float minSize = 6f;
        [SerializeField] float maxSize = 22f;
        [SerializeField] bool enableKeyboardPan = true;
        [SerializeField] bool enableMiddleMousePan = true;

        Vector3 _focus;
        bool _panning;
        Vector3 _lastPanScreen;

        void Awake()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;
            _focus = focus;
            ApplyTransform();
        }

        void Update()
        {
            if (targetCamera == null)
                return;

            if (enableKeyboardPan)
            {
                var pan = Vector3.zero;
                // Avoid stealing Demo W/A/S command keys: arrows + optional hold Alt+WASD.
                if (Input.GetKey(KeyCode.LeftArrow)) pan.x -= 1f;
                if (Input.GetKey(KeyCode.RightArrow)) pan.x += 1f;
                if (Input.GetKey(KeyCode.DownArrow)) pan.y -= 1f;
                if (Input.GetKey(KeyCode.UpArrow)) pan.y += 1f;
                if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
                {
                    if (Input.GetKey(KeyCode.A)) pan.x -= 1f;
                    if (Input.GetKey(KeyCode.D)) pan.x += 1f;
                    if (Input.GetKey(KeyCode.S)) pan.y -= 1f;
                    if (Input.GetKey(KeyCode.W)) pan.y += 1f;
                }

                if (pan.sqrMagnitude > 0f)
                    _focus += pan.normalized * (panSpeed * Time.unscaledDeltaTime);
            }

            if (enableMiddleMousePan)
                HandleMiddlePan();

            var scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
                orthographicSize = Mathf.Clamp(orthographicSize - scroll * zoomSpeed, minSize, maxSize);

            ApplyTransform();
        }

        void HandleMiddlePan()
        {
            if (Input.GetMouseButtonDown(2))
            {
                _panning = true;
                _lastPanScreen = Input.mousePosition;
            }

            if (Input.GetMouseButtonUp(2))
                _panning = false;

            if (!_panning || !Input.GetMouseButton(2) || targetCamera == null)
                return;

            var current = Input.mousePosition;
            var prev = targetCamera.ScreenToWorldPoint(new Vector3(_lastPanScreen.x, _lastPanScreen.y, -cameraZ));
            var now = targetCamera.ScreenToWorldPoint(new Vector3(current.x, current.y, -cameraZ));
            var delta = prev - now;
            _focus.x += delta.x;
            _focus.y += delta.y;
            _lastPanScreen = current;
        }

        public void FrameSlots(Vector3 center)
        {
            _focus = new Vector3(center.x, center.y, 0f);
            ApplyTransform();
        }

        void ApplyTransform()
        {
            if (targetCamera == null)
                return;

            targetCamera.orthographic = true;
            targetCamera.orthographicSize = orthographicSize;
            targetCamera.transform.position = new Vector3(_focus.x, _focus.y, cameraZ);
            targetCamera.transform.rotation = Quaternion.identity;
        }
    }
}
