using UnityEngine;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Chapter 01 Reference：2D 俯视正交相机（WASD 平移＋滚轮缩放）。
    /// </summary>
    public sealed class PlayableHostCameraRig : MonoBehaviour
    {
        [SerializeField] Camera targetCamera;
        [SerializeField] Vector3 focus = Vector3.zero;
        [SerializeField] float orthographicSize = 12f;
        [SerializeField] float height = 20f;
        [SerializeField] float panSpeed = 12f;
        [SerializeField] float zoomSpeed = 2f;
        [SerializeField] float minSize = 6f;
        [SerializeField] float maxSize = 24f;
        [SerializeField] bool useOrthographicTopDown = true;

        // Legacy perspective fields retained for older scenes.
        [SerializeField] float distance = 6f;

        Vector3 _focus;

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

            var pan = Vector3.zero;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) pan.x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) pan.x += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) pan.z -= 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) pan.z += 1f;
            if (pan.sqrMagnitude > 0f)
                _focus += pan.normalized * (panSpeed * Time.unscaledDeltaTime);

            var scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
                orthographicSize = Mathf.Clamp(orthographicSize - scroll * zoomSpeed, minSize, maxSize);

            ApplyTransform();
        }

        public void FrameSlots(Vector3 center)
        {
            _focus = center;
            ApplyTransform();
        }

        void ApplyTransform()
        {
            if (targetCamera == null)
                return;

            if (useOrthographicTopDown)
            {
                targetCamera.orthographic = true;
                targetCamera.orthographicSize = orthographicSize;
                targetCamera.transform.position = new Vector3(_focus.x, height, _focus.z);
                targetCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                return;
            }

            targetCamera.orthographic = false;
            var pos = _focus + new Vector3(0f, height, -distance);
            targetCamera.transform.position = pos;
            targetCamera.transform.rotation = Quaternion.LookRotation((_focus - pos).normalized, Vector3.up);
        }
    }
}
