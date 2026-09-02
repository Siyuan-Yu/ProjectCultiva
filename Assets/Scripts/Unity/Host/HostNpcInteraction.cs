using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Social;
using XianXia.Core.World.Strategic;

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
        /// <summary>Local/content hostility posture; not StrategicMilitary identity or faction-war authority.</summary>
        public const string HostileTag = "hostile";
        public const float DefaultMeleeEngageRange = 1.85f;

        public static bool IsHostileNpc(PlayableHostSession session, EntityId npcId)
        {
            if (session?.World == null || npcId.IsNone)
                return false;
            if (!session.World.Entities.TryGet(npcId, out var entity))
                return false;
            if (StrategicEncounterHostilityService.IsHostileStrategicNpc(session.World, entity))
                return true;

            if (!HostileActionClassificationService.TryClassifyTarget(
                    session.World, npcId, out var classification, out _))
                return false;
            if (classification.Scope == HostileActionScope.StrategicMilitary)
            {
                var playerFaction = session.World.Strategic?.PlayerFactionId;
                if (string.IsNullOrEmpty(playerFaction))
                    playerFaction = StrategicFactionCatalog.PlayerFactionId;
                return WarGateService.CanAttack(session.World, playerFaction, classification.TargetFactionId);
            }

            return IsHostileEntity(entity);
        }

        public static bool IsHostileEntity(XianXia.Core.Entities.Entity entity)
        {
            if (entity == null)
                return false;
            return entity.TryGet<PersonalityProfileComponent>(out var profile) &&
                   profile.HasTag(HostileTag);
        }

        /// <summary>中立／未宣战单位（主管、凡人等）攻击前需二次确认。</summary>
        public static bool IsNonHostileNpc(PlayableHostSession session, EntityId npcId)
        {
            if (npcId.IsNone || session == null)
                return false;
            return !IsHostileNpc(session, npcId);
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

        public static EntityId ResolveActiveCommandAuthority(PlayableHostSession session) =>
            session?.PlayerParty?.ActiveCharacterId ?? EntityId.None;

        /// <summary>World ground move / work-target move: respects view-selection command context.</summary>
        public static EntityId ResolvePartyActor(HostSelectionController selection, PlayableHostSession session = null) =>
            HostPlayerMoveCommandGate.ResolveActiveForWorldMove(selection, session);
    }
}
