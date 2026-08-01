using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Exploration;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Chapter 01 Reference：2D 俯视灰盒地图（色块＋道路连线）。非正式美术。
    /// </summary>
    public sealed class HostMapGraybox : MonoBehaviour
    {
        [SerializeField] Transform mapRoot;
        [SerializeField] float zoneScale = 2.4f;
        [SerializeField] float roadWidth = 0.35f;

        readonly List<GameObject> _built = new List<GameObject>();

        public int ZoneCount => _built.Count;

        public void Rebuild(PlayableHostSession session)
        {
            Clear();
            if (session == null || !session.IsInitialized || session.World == null)
                return;

            EnsureRoot();
            var locations = session.World.WorldRegion.Locations;
            foreach (var kv in locations)
                BuildZone(kv.Value);

            foreach (var kv in locations)
                BuildRoads(kv.Value, locations);
        }

        public void Clear()
        {
            for (var i = 0; i < _built.Count; i++)
                DestroyBuilt(_built[i]);

            _built.Clear();
            if (mapRoot == null)
                return;
            for (var i = mapRoot.childCount - 1; i >= 0; i--)
                DestroyBuilt(mapRoot.GetChild(i).gameObject);
        }

        static void DestroyBuilt(GameObject go)
        {
            if (go == null)
                return;
            if (Application.isPlaying)
                Destroy(go);
            else
                DestroyImmediate(go);
        }

        void EnsureRoot()
        {
            if (mapRoot != null)
                return;
            var go = new GameObject("MapGraybox");
            go.transform.SetParent(transform, false);
            mapRoot = go.transform;
        }

        void BuildZone(WorldLocationState loc)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Zone_" + loc.Id;
            go.transform.SetParent(mapRoot, false);
            go.transform.position = new Vector3(loc.PresentationX, 0.02f, loc.PresentationZ);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(zoneScale, zoneScale, 1f);
            var rend = go.GetComponent<Renderer>();
            if (rend != null)
                rend.sharedMaterial.color = ColorForKind(loc.Kind);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 0f, -0.1f);
            labelGo.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            var tm = labelGo.AddComponent<TextMesh>();
            tm.text = string.IsNullOrEmpty(loc.Name) ? loc.Id : loc.Name;
            tm.characterSize = 0.18f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = Color.white;
            tm.fontSize = 32;

            // Remove collider on label parent zone — keep for raycast move ground
            _built.Add(go);
        }

        void BuildRoads(
            WorldLocationState loc,
            System.Collections.Generic.IReadOnlyDictionary<string, WorldLocationState> all)
        {
            if (loc.AdjacentIds == null)
                return;
            for (var i = 0; i < loc.AdjacentIds.Count; i++)
            {
                var otherId = loc.AdjacentIds[i];
                if (string.CompareOrdinal(loc.Id, otherId) >= 0)
                    continue;
                if (!all.TryGetValue(otherId, out var other))
                    continue;

                var a = new Vector3(loc.PresentationX, 0.03f, loc.PresentationZ);
                var b = new Vector3(other.PresentationX, 0.03f, other.PresentationZ);
                var mid = (a + b) * 0.5f;
                var dir = b - a;
                var len = dir.magnitude;
                if (len < 0.01f)
                    continue;

                var road = GameObject.CreatePrimitive(PrimitiveType.Cube);
                road.name = "Road_" + loc.Id + "_" + otherId;
                road.transform.SetParent(mapRoot, false);
                road.transform.position = mid;
                road.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                road.transform.localScale = new Vector3(roadWidth, 0.02f, len);
                var rend = road.GetComponent<Renderer>();
                if (rend != null)
                    rend.sharedMaterial.color = new Color(0.45f, 0.38f, 0.28f);
                _built.Add(road);
            }
        }

        static Color ColorForKind(LocationKind kind)
        {
            switch (kind)
            {
                case LocationKind.Settlement:
                    return new Color(0.55f, 0.45f, 0.30f);
                case LocationKind.Village:
                    return new Color(0.50f, 0.55f, 0.40f);
                case LocationKind.Opportunity:
                    return new Color(0.35f, 0.45f, 0.70f);
                default:
                    return new Color(0.30f, 0.55f, 0.35f);
            }
        }
    }
}
