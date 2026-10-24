using UnityEngine;

namespace XianXia.Unity.Host
{
    /// <summary>Shared low-poly spirit feathers; no textures, lights or per-frame allocations.</summary>
    public sealed class HostFlightVisual : MonoBehaviour
    {
        static Mesh _wingMesh, _shadowMesh;
        static Material _material;
        Transform _left, _right;
        MeshRenderer _leftRenderer, _rightRenderer, _shadow;
        float _clock, _blend;
        public float Height => .65f * _blend;

        public float Present(bool airborne, bool paused, int order)
        {
            if (!airborne && _blend <= 0f) { gameObject.SetActive(false); return 0f; }
            gameObject.SetActive(true);
            // Loading a paused airborne save must already display the saved height.
            if (_left == null && paused && airborne) _blend = 1f;
            Ensure();
            if (!paused) { _clock += Time.unscaledDeltaTime; _blend = Mathf.MoveTowards(_blend, airborne ? 1f : 0f, Time.unscaledDeltaTime * 4f); }
            var flap = Mathf.Sin(_clock * 3.8f);
            var spread = _blend * (.9f + .1f * flap);
            _left.localPosition = _right.localPosition = new Vector3(0, Height + .65f, 0);
            _left.localScale = new Vector3(-spread, _blend, 1);
            _right.localScale = new Vector3(spread, _blend, 1);
            _left.localRotation = Quaternion.Euler(0, 0, -flap * 7f);
            _right.localRotation = Quaternion.Euler(0, 0, flap * 7f);
            _leftRenderer.sortingOrder = _rightRenderer.sortingOrder = order - 1;
            _shadow.sortingOrder = order - 2;
            _shadow.transform.localScale = new Vector3(.65f, .18f, 1) * _blend;
            return Height;
        }

        void Ensure()
        {
            if (_left != null) return;
            if (_material == null) _material = new Material(Shader.Find("Sprites/Default")) { name = "SharedSpiritWings" };
            if (_wingMesh == null)
            {
                var vertices = new Vector3[24]; var colors = new Color[24]; var indices = new int[24];
                for (var i = 0; i < 8; i++)
                {
                    var n = i * 3;
                    vertices[n] = new Vector3(.08f, -.08f - i * .014f);
                    vertices[n + 1] = new Vector3(.48f + i * .045f, .6f - i * .11f);
                    vertices[n + 2] = new Vector3(1.36f - i * .075f, 1.0f - i * .20f);
                    colors[n] = new Color(.45f, .95f, 1f, .85f);
                    colors[n + 1] = new Color(.5f, .85f, 1f, .68f);
                    colors[n + 2] = new Color(.65f, .92f, 1f, .12f);
                    indices[n] = n; indices[n + 1] = n + 1; indices[n + 2] = n + 2;
                }
                _wingMesh = new Mesh { name = "SpiritFeathers8", vertices = vertices, colors = colors, triangles = indices };
                _wingMesh.RecalculateBounds();
                var shadowVertices = new Vector3[13]; var shadowColors = new Color[13]; var shadowIndices = new int[36];
                for (var i = 0; i < 13; i++)
                {
                    shadowColors[i] = new Color(.07f, .10f, .13f, i == 0 ? .3f : .04f);
                    if (i > 0) shadowVertices[i] = new Vector3(Mathf.Cos((i - 1) * Mathf.PI / 6), Mathf.Sin((i - 1) * Mathf.PI / 6));
                    if (i < 12) { shadowIndices[i * 3] = 0; shadowIndices[i * 3 + 1] = i + 1; shadowIndices[i * 3 + 2] = (i + 1) % 12 + 1; }
                }
                _shadowMesh = new Mesh { name = "FlightGroundShadow12", vertices = shadowVertices, colors = shadowColors, triangles = shadowIndices };
                _shadowMesh.RecalculateBounds();
            }
            _leftRenderer = Create("LeftWing", _wingMesh); _left = _leftRenderer.transform;
            _rightRenderer = Create("RightWing", _wingMesh); _right = _rightRenderer.transform;
            _shadow = Create("GroundShadow", _shadowMesh);
        }

        MeshRenderer Create(string name, Mesh mesh)
        {
            var child = new GameObject(name); child.transform.SetParent(transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = child.AddComponent<MeshRenderer>(); renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }
    }
}
