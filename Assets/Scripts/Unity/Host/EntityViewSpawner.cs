using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Domain.Ids;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Instantiates／rebuilds EntityViews for playable-host characters. Presentation slots only.
    /// </summary>
    public sealed class EntityViewSpawner : MonoBehaviour
    {
        static readonly Color[] DefaultSlotColors =
        {
            new Color(0.25f, 0.55f, 0.95f),
            new Color(0.30f, 0.75f, 0.40f),
            new Color(0.95f, 0.55f, 0.20f)
        };

        [SerializeField] Transform viewsRoot;
        [SerializeField] Vector3[] slotPositions =
        {
            new Vector3(-2.5f, 1f, 0f),
            new Vector3(0f, 1f, 0f),
            new Vector3(2.5f, 1f, 0f)
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

            var ids = session.CharacterIds;
            for (var i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                var position = i < slotPositions.Length
                    ? slotPositions[i]
                    : new Vector3(i * 2.5f, 1f, 0f);
                var color = DefaultSlotColors[i % DefaultSlotColors.Length];

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

        EntityView CreateCapsuleView(EntityId id, Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "EntityView_" + id.Value;
            go.transform.SetParent(viewsRoot, worldPositionStays: true);
            go.transform.position = position;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            // CapsuleCollider already present for V4-C picking.
            var view = go.AddComponent<EntityView>();

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            var text = labelGo.AddComponent<TextMesh>();
            text.characterSize = 0.12f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 32;
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
