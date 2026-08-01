using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Exploration;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Demo-style 2D Sprite zone map: tiled patches + roads on XY (no 3D mesh).
    /// </summary>
    public sealed class HostMapGraybox : MonoBehaviour
    {
        [SerializeField] Transform mapRoot;
        [SerializeField] float tileWorldSize = 1f;
        [SerializeField] int patchRadius = 3;
        [SerializeField] float roadWidth = 0.35f;

        readonly List<GameObject> _built = new List<GameObject>();

        public int ZoneCount => _built.Count;

        public void Rebuild(PlayableHostSession session)
        {
            Clear();
            if (session == null || !session.IsInitialized || session.World == null)
                return;

            EnsureRoot();

            var demoMap = GetComponent<HostDemoTileMap>() ?? gameObject.AddComponent<HostDemoTileMap>();
            demoMap.Rebuild();

            var locations = session.World.WorldRegion.Locations;
            foreach (var kv in locations)
                BuildZoneLabel(kv.Value);

            foreach (var kv in locations)
                BuildRoads(kv.Value, locations);

            // Zone markers for tests／selection helpers (labels already added).
            foreach (var kv in locations)
            {
                var marker = new GameObject("ZoneMarker_" + kv.Key);
                marker.transform.SetParent(mapRoot, false);
                _built.Add(marker);
            }
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
            var go = new GameObject("MapSprites");
            go.transform.SetParent(transform, false);
            mapRoot = go.transform;
        }

        void BuildZoneLabel(WorldLocationState loc)
        {
            var go = new GameObject("Label_" + loc.Id);
            go.transform.SetParent(mapRoot, false);
            go.transform.position = HostPresentationSpace.FromPresentation(
                loc.PresentationX, loc.PresentationZ + patchRadius * tileWorldSize * 0.55f, -0.2f);
            var text = go.AddComponent<TextMesh>();
            text.text = string.IsNullOrEmpty(loc.Name) ? loc.Id : loc.Name;
            text.characterSize = 0.12f;
            text.anchor = TextAnchor.MiddleCenter;
            text.fontSize = 40;
            text.color = Color.white;
            _built.Add(go);
        }

        void BuildRoads(WorldLocationState loc, IReadOnlyDictionary<string, WorldLocationState> all)
        {
            if (loc.AdjacentIds == null)
                return;
            foreach (var adjId in loc.AdjacentIds)
            {
                if (string.CompareOrdinal(loc.Id, adjId) >= 0)
                    continue;
                if (!all.TryGetValue(adjId, out var other))
                    continue;

                var a = HostPresentationSpace.FromPresentation(loc.PresentationX, loc.PresentationZ);
                var b = HostPresentationSpace.FromPresentation(other.PresentationX, other.PresentationZ);
                var mid = (a + b) * 0.5f;
                mid.z = HostPresentationSpace.GroundZ - 0.05f;
                var dir = b - a;
                var len = dir.magnitude;
                if (len < 0.01f)
                    continue;

                var go = new GameObject("Road_" + loc.Id + "_" + adjId);
                go.transform.SetParent(mapRoot, false);
                go.transform.position = mid;
                go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                go.transform.localScale = new Vector3(len / 0.32f, roadWidth / 0.32f, 1f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = HostSpriteFactory.TileSprite();
                sr.color = new Color(0.55f, 0.45f, 0.32f, 1f);
                sr.sortingOrder = -15;
                _built.Add(go);
            }
        }
    }
}
