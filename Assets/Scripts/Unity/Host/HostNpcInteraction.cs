using UnityEngine;
using XianXia.Core.Domain.Ids;

namespace XianXia.Unity.Host
{
    public enum HostNpcArriveAction
    {
        Talk = 0,
        Attack = 1
    }

    /// <summary>Party member walks to an NPC then performs Talk or Attack.</summary>
    public readonly struct HostNpcArriveIntent
    {
        public HostNpcArriveIntent(EntityId npcId, HostNpcArriveAction action)
        {
            NpcId = npcId;
            Action = action;
        }

        public EntityId NpcId { get; }
        public HostNpcArriveAction Action { get; }
    }

    public static class HostNpcPicker
    {
        public static bool TryPickAtMouse(
            Camera camera,
            EntityViewSpawner spawner,
            out EntityId id,
            out EntityView view)
        {
            id = EntityId.None;
            view = null;
            if (spawner == null || camera == null)
                return false;

            var ray = camera.ScreenPointToRay(Input.mousePosition);
            EntityView best = null;
            var bestDist = float.MaxValue;
            foreach (var candidate in spawner.Registry.All)
            {
                if (candidate == null || !candidate.IsBound)
                    continue;

                float dist = 0f;
                var rend = candidate.GetComponentInChildren<Renderer>();
                var hit = rend != null && rend.bounds.extents.sqrMagnitude > 0.0001f &&
                          rend.bounds.IntersectRay(ray, out dist);
                if (!hit)
                {
                    var radius = 0.75f * Mathf.Max(
                        candidate.transform.lossyScale.x,
                        candidate.transform.lossyScale.y);
                    hit = RayHitsSphere(ray, candidate.transform.position, radius, out dist);
                }

                if (!hit || dist < 0f || dist >= bestDist)
                    continue;

                bestDist = dist;
                best = candidate;
            }

            if (best == null)
                return false;

            id = best.EntityId;
            view = best;
            return !id.IsNone;
        }

        static bool RayHitsSphere(Ray ray, Vector3 center, float radius, out float distance)
        {
            var oc = ray.origin - center;
            var b = Vector3.Dot(oc, ray.direction);
            var c = Vector3.Dot(oc, oc) - radius * radius;
            var discriminant = b * b - c;
            if (discriminant < 0f)
            {
                distance = 0f;
                return false;
            }

            var sqrt = Mathf.Sqrt(discriminant);
            var t = -b - sqrt;
            if (t < 0f)
                t = -b + sqrt;
            distance = t;
            return t >= 0f;
        }
    }

    public static class HostNpcInteraction
    {
        /// <summary>Non-party NPCs are non-hostile until combat slice marks them otherwise.</summary>
        public static bool IsNonHostileNpc(EntityViewSpawner spawner, EntityId npcId)
        {
            if (npcId.IsNone || spawner == null)
                return false;
            return true;
        }

        public static bool TryResolveDefinitionId(
            PlayableHostSession session,
            EntityId entityId,
            out string definitionId)
        {
            definitionId = string.Empty;
            if (session == null || !session.IsInitialized || entityId.IsNone)
                return false;
            if (!session.World.Entities.TryGet(entityId, out var entity))
                return false;
            definitionId = entity.DefinitionId.ToString();
            return !string.IsNullOrEmpty(definitionId);
        }

        public static EntityId ResolvePartyActor(HostSelectionController selection)
        {
            if (selection == null || selection.State.Count == 0)
                return EntityId.None;
            for (var i = 0; i < selection.State.Count; i++)
            {
                var id = selection.State.SelectedIds[i];
                if (selection.IsPartyUnit(id))
                    return id;
            }

            return EntityId.None;
        }
    }
}
