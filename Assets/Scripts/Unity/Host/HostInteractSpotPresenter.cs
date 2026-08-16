using UnityEngine;

namespace XianXia.Unity.Host
{
    /// <summary>各地可交互点小标记（表现层）。不依赖 Physics 模块。</summary>
    public sealed class HostInteractSpotPresenter : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] bool showLabels = true;
        Transform _root;
        static Mesh _discMesh;

        public void Bind(PlayableHostBootstrap host) => bootstrap = host;

        public void Rebuild()
        {
            Clear();
            _root = new GameObject("InteractSpots").transform;
            _root.SetParent(transform, false);
            var world = bootstrap != null && bootstrap.Session != null && bootstrap.Session.IsInitialized
                ? bootstrap.Session.World
                : null;
            var spots = HostInteractSpots.GetSpots(world);
            var dynamic = HostInteractSpots.HasDynamicPlots;
            var labels = showLabels && !dynamic;
            for (var i = 0; i < spots.Count; i++)
            {
                var s = spots[i];
                var go = new GameObject("Spot_" + s.Label);
                go.transform.SetParent(_root, false);
                go.transform.position = s.WorldPosition + new Vector3(0f, 0f, -0.05f);
                go.transform.localScale = dynamic
                    ? new Vector3(0.45f, 0.45f, 1f)
                    : new Vector3(0.7f, 0.7f, 1f);

                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = DiscMesh();
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color"));
                mr.sharedMaterial.color = s.Kind == HostInteractSpotKind.Work
                    ? new Color(0.95f, 0.82f, 0.28f, 0.85f)
                    : (s.Kind == HostInteractSpotKind.Explore
                        ? new Color(0.72f, 0.55f, 0.90f, 0.85f)
                        : (s.Kind == HostInteractSpotKind.Loot
                            ? new Color(0.95f, 0.78f, 0.25f, 0.9f)
                            : new Color(0.35f, 0.75f, 0.95f, 0.85f)));

                if (labels)
                {
                    var label = new GameObject("Label");
                    label.transform.SetParent(go.transform, false);
                    label.transform.localPosition = new Vector3(0f, 1.1f, -0.2f);
                    var tm = label.AddComponent<TextMesh>();
                    tm.text = s.Label;
                    tm.characterSize = 0.12f;
                    tm.fontSize = 32;
                    tm.anchor = TextAnchor.MiddleCenter;
                    tm.alignment = TextAlignment.Center;
                    tm.color = new Color(0.15f, 0.12f, 0.08f, 0.95f);
                }
            }
        }

        public void Clear()
        {
            if (_root != null)
            {
                Object.Destroy(_root.gameObject);
                _root = null;
            }
        }

        static Mesh DiscMesh()
        {
            if (_discMesh != null)
                return _discMesh;

            const int segments = 16;
            var verts = new Vector3[segments + 1];
            var tris = new int[segments * 3];
            verts[0] = Vector3.zero;
            for (var i = 0; i < segments; i++)
            {
                var a = i * Mathf.PI * 2f / segments;
                verts[i + 1] = new Vector3(Mathf.Cos(a) * 0.5f, Mathf.Sin(a) * 0.5f, 0f);
                tris[i * 3] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = i + 1 < segments ? i + 2 : 1;
            }

            _discMesh = new Mesh { name = "InteractSpotDisc" };
            _discMesh.vertices = verts;
            _discMesh.triangles = tris;
            _discMesh.RecalculateNormals();
            _discMesh.RecalculateBounds();
            return _discMesh;
        }
    }
}
