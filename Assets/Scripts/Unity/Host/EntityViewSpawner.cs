using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Spawns 2D Sprite EntityViews (Demo-aligned XY plane). No Capsule／3D mesh.
    /// </summary>
    public sealed class EntityViewSpawner : MonoBehaviour
    {
        static readonly Color[] CharacterSlotColors =
        {
            new Color(0.35f, 0.65f, 1f),
            new Color(0.35f, 0.85f, 0.45f),
            new Color(1f, 0.65f, 0.30f)
        };

        static readonly Color NpcSlotColor = new Color(0.85f, 0.78f, 0.40f);
        static readonly Color SupervisorColor = new Color(0.95f, 0.35f, 0.35f);

        [SerializeField] Transform viewsRoot;
        [SerializeField] Vector3[] slotPositions =
        {
            new Vector3(-2.5f, 0f, 0f),
            new Vector3(0f, 0f, 0f),
            new Vector3(2.5f, 0f, 0f),
            new Vector3(5f, 0f, 0f)
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
                var position = ResolvePresentationPosition(session, id, i, stackAtLocation, slotPositions);

                var isNpc = session.World.Entities.TryGet(id, out var entity) &&
                             (entity.Tags & EntityTag.Npc) != 0;
                var color = CharacterSlotColors[i % CharacterSlotColors.Length];
                if (isNpc)
                {
                    color = NpcSlotColor;
                    if (entity.TryGet<XianXia.Core.Social.NpcAiRoleComponent>(out var ai) &&
                        ai.Role == XianXia.Core.Social.NpcAiRoleKind.Supervisor)
                        color = SupervisorColor;
                }

                var view = CreateSpriteView(id, position);
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
            Dictionary<string, int> stackAtLocation,
            Vector3[] slots)
        {
            if (session.World.Entities.TryGet(id, out var entity) &&
                entity.TryGet<EntityLocationComponent>(out var loc) &&
                loc.HasLocation &&
                session.World.WorldRegion.TryGet(loc.LocationId, out var location))
            {
                stackAtLocation.TryGetValue(loc.LocationId, out var stack);
                stackAtLocation[loc.LocationId] = stack + 1;
                var ox = (stack % 3) * 0.85f - 0.85f;
                var oy = (stack / 3) * 0.85f;
                return HostPresentationSpace.FromPresentation(
                    location.PresentationX + ox,
                    location.PresentationZ + oy);
            }

            if (slots != null && fallbackIndex < slots.Length)
                return slots[fallbackIndex];
            return new Vector3(fallbackIndex * 2.5f, 0f, HostPresentationSpace.EntityZ);
        }

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
                view.transform.position = ResolvePresentationPosition(
                    session, id, i, stackAtLocation, slotPositions);
            }
        }

        EntityView CreateSpriteView(EntityId id, Vector3 position)
        {
            var go = new GameObject("EntityView_" + id.Value);
            go.transform.SetParent(viewsRoot, worldPositionStays: true);
            go.transform.position = position;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * 0.9f;

            var body = go.AddComponent<SpriteRenderer>();
            body.sprite = HostSpriteFactory.UnitSprite();
            body.sortingOrder = 10;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.9f, 1.1f);

            var ringGo = new GameObject("SelectionRing");
            ringGo.transform.SetParent(go.transform, false);
            ringGo.transform.localPosition = new Vector3(0f, -0.15f, 0.1f);
            ringGo.transform.localScale = Vector3.one * 1.35f;
            var ring = ringGo.AddComponent<SpriteRenderer>();
            ring.sprite = HostSpriteFactory.SelectionRingSprite();
            ring.sortingOrder = 9;
            ring.enabled = false;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 0.85f, -0.1f);
            var text = labelGo.AddComponent<TextMesh>();
            text.characterSize = 0.12f;
            text.anchor = TextAnchor.LowerCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 28;
            text.color = Color.white;
            text.text = id.ToString();

            var view = go.AddComponent<EntityView>();
            return view;
        }

        void EnsureRoot()
        {
            if (viewsRoot != null)
                return;

            var rootGo = new GameObject("EntityViews");
            rootGo.transform.SetParent(transform, false);
            viewsRoot = rootGo.transform;
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
