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
            BuildAmbientGrass();

            var locations = session.World.WorldRegion.Locations;
            foreach (var kv in locations)
                BuildZonePatch(kv.Value);

            foreach (var kv in locations)
                BuildRoads(kv.Value, locations);

            foreach (var kv in locations)
                BuildZoneLabel(kv.Value);
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

        void BuildAmbientGrass()
        {
            // Wide grass bed approximating Demo village footprint (not full 80×50 yet).
            for (var y = -12; y <= 12; y++)
            for (var x = -16; x <= 16; x++)
            {
                if ((x + y) % 2 != 0)
                    continue;
                StampTile(
                    "Grass_" + x + "_" + y,
                    x * tileWorldSize,
                    y * tileWorldSize,
                    new Color(0.30f, 0.42f, 0.26f),
                    -25,
                    1f);
            }
        }

        void BuildZonePatch(WorldLocationState loc)
        {
            var color = ColorForKind(loc);
            var radius = patchRadius;
            if (loc.Kind == LocationKind.Opportunity)
                radius = 2;
            for (var dy = -radius; dy <= radius; dy++)
            for (var dx = -radius; dx <= radius; dx++)
            {
                if (dx * dx + dy * dy > radius * radius)
                    continue;
                StampTile(
                    "ZoneTile_" + loc.Id + "_" + dx + "_" + dy,
                    loc.PresentationX + dx * tileWorldSize,
                    loc.PresentationZ + dy * tileWorldSize,
                    color,
                    -10,
                    1f);
            }

            _built.Add(new GameObject("ZoneMarker_" + loc.Id)); // count zones for tests
            var marker = _built[_built.Count - 1];
            marker.transform.SetParent(mapRoot, false);
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

        void StampTile(string name, float x, float y, Color color, int sorting, float scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(mapRoot, false);
            go.transform.position = HostPresentationSpace.FromPresentation(x, y, HostPresentationSpace.GroundZ);
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = HostSpriteFactory.TileSprite();
            sr.color = color;
            sr.sortingOrder = sorting;
            _built.Add(go);
        }

        static Color ColorForKind(WorldLocationState loc)
        {
            if (!string.IsNullOrEmpty(loc.OpportunitySiteId))
                return new Color(0.35f, 0.70f, 0.85f);
            if (!string.IsNullOrEmpty(loc.ResourceOnExploreId))
            {
                if (loc.ResourceOnExploreId.Contains("herb"))
                    return new Color(0.35f, 0.65f, 0.40f);
                if (loc.ResourceOnExploreId.Contains("grain"))
                    return new Color(0.70f, 0.62f, 0.30f);
                return new Color(0.25f, 0.48f, 0.28f);
            }

            switch (loc.Kind)
            {
                case LocationKind.Settlement: return new Color(0.55f, 0.45f, 0.35f);
                case LocationKind.Village: return new Color(0.50f, 0.42f, 0.32f);
                case LocationKind.Wild: return new Color(0.28f, 0.50f, 0.30f);
                case LocationKind.Opportunity: return new Color(0.40f, 0.60f, 0.80f);
                default: return new Color(0.40f, 0.40f, 0.40f);
            }
        }
    }
}
