using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Exploration;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// 2D Sprite zone map from WorldRegion locations (Demo-aligned XY). Replaces 3D Quad graybox.
    /// </summary>
    public sealed class HostMapGraybox : MonoBehaviour
    {
        [SerializeField] Transform mapRoot;
        [SerializeField] float zoneScale = 2.8f;
        [SerializeField] float roadWidth = 0.25f;

        readonly List<GameObject> _built = new List<GameObject>();

        public int ZoneCount => _built.Count;

        public void Rebuild(PlayableHostSession session)
        {
            Clear();
            if (session == null || !session.IsInitialized || session.World == null)
                return;

            EnsureRoot();
            BuildGround();

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
            var go = new GameObject("MapSprites");
            go.transform.SetParent(transform, false);
            mapRoot = go.transform;
        }

        void BuildGround()
        {
            var go = new GameObject("GroundTiles");
            go.transform.SetParent(mapRoot, false);
            go.transform.position = HostPresentationSpace.FromPresentation(0f, 0f, HostPresentationSpace.GroundZ);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = HostSpriteFactory.TileSprite();
            sr.color = new Color(0.28f, 0.38f, 0.24f, 1f);
            sr.sortingOrder = -20;
            go.transform.localScale = new Vector3(48f, 30f, 1f);
            _built.Add(go);
        }

        void BuildZone(WorldLocationState loc)
        {
            var go = new GameObject("Zone_" + loc.Id);
            go.transform.SetParent(mapRoot, false);
            go.transform.position = HostPresentationSpace.FromPresentation(
                loc.PresentationX, loc.PresentationZ, HostPresentationSpace.GroundZ - 0.1f);
            go.transform.localScale = Vector3.one * zoneScale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = HostSpriteFactory.TileSprite();
            sr.color = ColorForKind(loc.Kind);
            sr.sortingOrder = -10;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 0.55f, -0.05f);
            var text = labelGo.AddComponent<TextMesh>();
            text.text = string.IsNullOrEmpty(loc.Name) ? loc.Id : loc.Name;
            text.characterSize = 0.08f;
            text.anchor = TextAnchor.MiddleCenter;
            text.fontSize = 36;
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

        static Color ColorForKind(LocationKind kind)
        {
            switch (kind)
            {
                case LocationKind.Settlement: return new Color(0.55f, 0.45f, 0.35f);
                case LocationKind.Village: return new Color(0.50f, 0.42f, 0.32f);
                case LocationKind.Wild: return new Color(0.25f, 0.45f, 0.28f);
                case LocationKind.Opportunity: return new Color(0.35f, 0.55f, 0.75f);
                default: return new Color(0.40f, 0.40f, 0.40f);
            }
        }
    }
}
