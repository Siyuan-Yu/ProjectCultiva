using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Instantiates／rebuilds EntityViews for playable-host characters and NPCs.
    /// Presentation slots only.
    /// </summary>
    public sealed class EntityViewSpawner : MonoBehaviour
    {
        static readonly Color[] CharacterSlotColors =
        {
            new Color(0.25f, 0.55f, 0.95f),
            new Color(0.30f, 0.75f, 0.40f),
            new Color(0.95f, 0.55f, 0.20f)
        };

        static readonly Color NpcSlotColor = new Color(0.75f, 0.70f, 0.35f);

        [SerializeField] Transform viewsRoot;
        [SerializeField] Vector3[] slotPositions =
        {
            new Vector3(-2.5f, 1f, 0f),
            new Vector3(0f, 1f, 0f),
            new Vector3(2.5f, 1f, 0f),
            new Vector3(5f, 1f, 0f)
        };

        readonly EntityViewRegistry _registry = new EntityViewRegistry();
        readonly List<EntityView> _spawned = new List<EntityView>();

        public EntityViewRegistry Registry => _registry;

        public int SpawnedCount => _spawned.Count;

        public IReadOnlyList<Vector3> SlotPositions => slotPositions;

        public void Rebuild(PlayableHostSession session)
        {
            Clear();

            if (session == null || !session.IsInitialized)
            {
                Debug.LogError("[EntityViewSpawner] Cannot rebuild: session not initialized.", this);
                return;
            }

            EnsureRoot();

            var ids = session.ViewableEntityIds;
            var stackAtLocation = new Dictionary<string, int>(System.StringComparer.Ordinal);
            for (var i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                var position = ResolvePresentationPosition(session, id, i, stackAtLocation);

                var isNpc = session.World.Entities.TryGet(id, out var entity) &&
                             (entity.Tags & EntityTag.Npc) != 0;
                var color = isNpc
                    ? NpcSlotColor
                    : CharacterSlotColors[i % CharacterSlotColors.Length];

                var view = CreateCapsuleView(id, position);
                if (!view.Bind(session.World, id))
                {
                    DestroyView(view);
                    continue;
                }

                view.SetBaseColor(color);
                _registry.Register(id, view);
                _spawned.Add(view);
            }
        }

        public void Clear()
        {
            for (var i = 0; i < _spawned.Count; i++)
                DestroyView(_spawned[i]);

            _spawned.Clear();
            _registry.Clear();

            if (viewsRoot != null)
            {
                for (var i = viewsRoot.childCount - 1; i >= 0; i--)
                    DestroyViewObject(viewsRoot.GetChild(i).gameObject);
            }
        }

        static Vector3 ResolvePresentationPosition(
            PlayableHostSession session,
            EntityId id,
            int fallbackIndex,
            Dictionary<string, int> stackAtLocation)
        {
            if (session.World.Entities.TryGet(id, out var entity) &&
                entity.TryGet<EntityLocationComponent>(out var loc) &&
                loc.HasLocation &&
                session.World.WorldRegion.TryGet(loc.LocationId, out var location))
            {
                stackAtLocation.TryGetValue(loc.LocationId, out var stack);
                stackAtLocation[loc.LocationId] = stack + 1;
                var ox = (stack % 3) * 0.85f - 0.85f;
                var oz = (stack / 3) * 0.85f;
                return new Vector3(location.PresentationX + ox, 0.5f, location.PresentationZ + oz);
            }

            return fallbackIndex < 4
                ? new Vector3(fallbackIndex * 2.5f - 2.5f, 0.5f, 0f)
                : new Vector3(fallbackIndex * 2.5f, 0.5f, 0f);
        }

        /// <summary>VS0.9: move views after travel without full rebuild.</summary>
        public void SyncLocations(PlayableHostSession session)
        {
            if (session == null || !session.IsInitialized)
                return;
            var stackAtLocation = new Dictionary<string, int>(System.StringComparer.Ordinal);
            var ids = session.ViewableEntityIds;
            for (var i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                if (!_registry.TryGet(id, out var view) || view == null)
                    continue;
                view.transform.position = ResolvePresentationPosition(session, id, i, stackAtLocation);
            }
        }

        EntityView CreateCapsuleView(EntityId id, Vector3 position)
        {
            // Reference Level：矮胶囊作 2D 俯视棋子（非 Demo Sprite 管线）。
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "EntityView_" + id.Value;
            go.transform.SetParent(viewsRoot, worldPositionStays: true);
            go.transform.position = position;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = new Vector3(0.7f, 0.35f, 0.7f);

            var view = go.AddComponent<EntityView>();

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            labelGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var text = labelGo.AddComponent<TextMesh>();
            text.characterSize = 0.14f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 28;
            text.color = Color.white;
            text.text = id.ToString();

            return view;
        }

        void EnsureRoot()
        {
            if (viewsRoot != null)
                return;

            var rootGo = new GameObject("EntityViews");
            rootGo.transform.SetParent(transform, false);
            viewsRoot = rootGo.transform;

            EnsureGroundPlane();
        }

        void EnsureGroundPlane()
        {
            if (transform.Find("GroundPlane") != null)
                return;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "GroundPlane";
            ground.transform.SetParent(transform, false);
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(4f, 1f, 4f);
            var rend = ground.GetComponent<Renderer>();
            if (rend != null)
                rend.sharedMaterial.color = new Color(0.22f, 0.28f, 0.20f);
        }

        static void DestroyView(EntityView view)
        {
            if (view == null)
                return;
            view.Unbind();
            DestroyViewObject(view.gameObject);
        }

        static void DestroyViewObject(GameObject go)
        {
            if (go == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(go);
            else
                Object.DestroyImmediate(go);
        }
    }
}
