using UnityEngine;

namespace XianXia.Unity.Host
{
    /// <summary>各地可交互点小标记（表现层）。</summary>
    public sealed class HostInteractSpotPresenter : MonoBehaviour
    {
        [SerializeField] bool showLabels = true;
        Transform _root;

        public void Rebuild()
        {
            Clear();
            _root = new GameObject("InteractSpots").transform;
            _root.SetParent(transform, false);
            var spots = HostInteractSpots.Spots;
            for (var i = 0; i < spots.Count; i++)
            {
                var s = spots[i];
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Spot_" + s.Label;
                go.transform.SetParent(_root, false);
                go.transform.position = s.WorldPosition + new Vector3(0f, 0f, -0.05f);
                go.transform.localScale = new Vector3(0.7f, 0.7f, 0.15f);
                var col = go.GetComponent<Collider>();
                if (col != null)
                    Object.Destroy(col);
                var r = go.GetComponent<Renderer>();
                if (r != null)
                {
                    r.material = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color"));
                    r.material.color = s.Kind == HostInteractSpotKind.Work
                        ? new Color(0.95f, 0.82f, 0.28f, 0.85f)
                        : new Color(0.35f, 0.75f, 0.95f, 0.85f);
                }

                if (showLabels)
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
    }
}
