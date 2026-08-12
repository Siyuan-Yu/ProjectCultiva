using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Exploration;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Host map labels／markers on top of <see cref="HostDemoTileMap"/>.
    /// Location labels use presentation coords (synced from mapLayout when bound).
    /// </summary>
    public sealed class HostMapGraybox : MonoBehaviour
    {
        [SerializeField] Transform mapRoot;
        [SerializeField] float tileWorldSize = 1f;
        [SerializeField] int patchRadius = 3;

        readonly List<GameObject> _built = new List<GameObject>();

        public int ZoneCount => _built.Count;

        public void Rebuild(PlayableHostSession session)
        {
            Clear();
            if (session == null || !session.IsInitialized || session.World == null)
                return;

            EnsureRoot();

            var demoMap = GetComponent<HostDemoTileMap>() ?? gameObject.AddComponent<HostDemoTileMap>();
            demoMap.Rebuild(session);

            var locations = session.World.WorldRegion.Locations;
            foreach (var kv in locations)
                BuildZoneLabel(kv.Value);

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
    }
}
