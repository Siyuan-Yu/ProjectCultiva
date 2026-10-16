using System;
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
        const string HideMapLabelTag = "hideMapLabel";
        [SerializeField] Transform mapRoot;
        [SerializeField] float tileWorldSize = 1f;
        [SerializeField] int patchRadius = 3;

        readonly List<GameObject> _built = new List<GameObject>();

        public int ZoneCount => _built.Count;

        public void Rebuild(PlayableHostSession session) =>
            Rebuild(session, includeSurfacePresentation: true);

        /// <summary>
        /// Rebuild labels/markers only. Safe while W1B continuous loaded set owns surface tiles.
        /// </summary>
        public void RebuildOverlaysOnly(PlayableHostSession session)
        {
            Rebuild(session, includeSurfacePresentation: false);
        }

        public void Rebuild(PlayableHostSession session, bool includeSurfacePresentation)
        {
            ClearOverlaysOnly();
            if (session == null || !session.IsInitialized || session.World == null)
                return;

            EnsureRoot();

            if (includeSurfacePresentation)
            {
                var demoMap = GetComponent<HostDemoTileMap>() ?? gameObject.AddComponent<HostDemoTileMap>();
                demoMap.Rebuild(session);
            }

            var locations = session.World.LocalPlaces.Locations;
            var continuousPlaces = session.World.ContinuousOutdoorMaterialization.PlacesByLocationId;
            var continuous = GetComponent<ContinuousOutdoorSurfaceRuntime>() ??
                             GetComponentInParent<ContinuousOutdoorSurfaceRuntime>();
            var labeledLocationIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kv in locations)
            {
                // 洞府内室等只属于洞内图：地表不画标签（MapEditor 里也不会出现）
                if (!LocalMapVisibility.IsLocationOnActiveMap(session.World, kv.Value))
                    continue;
                if (continuous != null && continuous.IsActive &&
                    !continuous.IsPresentationPositionLoaded(kv.Value.PresentationX, kv.Value.PresentationZ))
                    continue;
                if (OpportunityEntranceRules.IsHiddenEntrance(kv.Value) &&
                    !OpportunityEntranceRules.IsRevealedToPlayerParty(session.World, kv.Value))
                    continue;
                if (!session.World.LocalMap.IsInInterior && continuousPlaces.ContainsKey(kv.Key))
                    continue;
                if (ShouldHideMapLabel(kv.Value) || !labeledLocationIds.Add(kv.Key))
                    continue;
                BuildZoneLabel(kv.Value);
            }

            foreach (var kv in locations)
            {
                if (continuous != null && continuous.IsActive &&
                    !continuous.IsPresentationPositionLoaded(kv.Value.PresentationX, kv.Value.PresentationZ))
                    continue;
                var marker = new GameObject("ZoneMarker_" + kv.Key);
                marker.transform.SetParent(mapRoot, false);
                _built.Add(marker);
            }

            foreach (var kv in continuousPlaces)
            {
                var loc = kv.Value;
                if (loc == null || (OpportunityEntranceRules.IsHiddenEntrance(loc) &&
                    !OpportunityEntranceRules.IsRevealedToPlayerParty(session.World, loc))) continue;
                if (continuous != null && continuous.IsActive &&
                    !continuous.IsPresentationPositionLoaded(loc.PresentationX, loc.PresentationZ)) continue;
                if (ShouldHideMapLabel(loc) || !labeledLocationIds.Add(kv.Key)) continue;
                BuildZoneLabel(loc);
                var marker = new GameObject("ContinuousSiteMarker_" + kv.Key);
                marker.transform.SetParent(mapRoot, false);
                _built.Add(marker);
            }
        }

        static bool ShouldHideMapLabel(WorldLocationState loc)
        {
            if (loc?.Tags == null) return false;
            for (var i = 0; i < loc.Tags.Count; i++)
                if (string.Equals(loc.Tags[i], HideMapLabelTag, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        public void Clear() => ClearOverlaysOnly();

        void ClearOverlaysOnly()
        {
            for (var i = 0; i < _built.Count; i++)
                DestroyBuilt(_built[i]);

            _built.Clear();
            // This presenter owns only objects it created and recorded in _built. mapRoot is a
            // shared presentation parent in existing scenes; deleting every child here used to
            // remove SurfaceInstance_*, EntityView_* and gateway objects during a handoff.
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
            var marker = HostStepAArt.SiteMarkerSprite();
            if (marker != null)
            {
                var markerObject = new GameObject("SiteMarkerArt");
                markerObject.transform.SetParent(go.transform, false);
                markerObject.transform.localPosition = new Vector3(0f, 0f, 0.02f);
                var spriteRenderer = markerObject.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = marker;
                spriteRenderer.sortingOrder = 498;
                var textRenderer = go.GetComponent<MeshRenderer>();
                if (textRenderer != null) textRenderer.sortingOrder = 499;
            }
            _built.Add(go);
        }
    }
}
