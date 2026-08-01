using UnityEngine;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Minimal observer camera for Playable Host. Not a Demo CameraController port.
    /// </summary>
    public sealed class PlayableHostCameraRig : MonoBehaviour
    {
        [SerializeField] Camera targetCamera;
        [SerializeField] Vector3 focus = Vector3.zero;
        [SerializeField] float distance = 6f;
        [SerializeField] float height = 14f;
        [SerializeField] float panSpeed = 10f;
        [SerializeField] float zoomSpeed = 4f;
        [SerializeField] float minDistance = 5f;
        [SerializeField] float maxDistance = 28f;

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
                distance = Mathf.Clamp(distance - scroll * zoomSpeed, minDistance, maxDistance);

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

            var pos = _focus + new Vector3(0f, height, -distance);
            targetCamera.transform.position = pos;
            targetCamera.transform.rotation = Quaternion.LookRotation((_focus - pos).normalized, Vector3.up);
        }
    }
}
