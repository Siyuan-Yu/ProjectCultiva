using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>Local input preferences, never gameplay/Snapshot authority.</summary>
    public static class HostRpgControls
    {
        const string SceneKey = "Cultiva.Controls.SceneRts";
        const string MapKey = "Cultiva.Controls.MapClickTravel";
        public const float InteractionRange = HostNpcInteraction.DefaultMeleeEngageRange;
        public static bool SceneRts => PlayerPrefs.GetInt(SceneKey, 0) != 0;
        public static bool MapClickTravel => PlayerPrefs.GetInt(MapKey, 1) != 0;
        public static bool InCombat(PlayableHostBootstrap host)
        {
            var state = host?.Session?.World?.Strategic?.CharacterEncounter;
            return state != null || host?.GetComponent<HostNpcMeleeAssault>()?.IsFighting == true;
        }
        public static bool ManualActor(PlayableHostBootstrap host, EntityId actor) =>
            !SceneRts && !actor.IsNone && host?.Session?.PlayerParty?.ActiveCharacterId == actor;
        public static void SetSceneRts(PlayableHostBootstrap host, bool enabled)
        {
            if (enabled == SceneRts) return;
            PlayerPrefs.SetInt(SceneKey, enabled ? 1 : 0); PlayerPrefs.Save();
            host?.WorkTargetMode?.Cancel();
            var actor = host?.Session?.PlayerParty?.ActiveCharacterId ?? EntityId.None;
            host?.MoveController?.CancelForControlMode(actor);
            host?.GetComponent<HostNpcMeleeAssault>()?.HoldAttackForControlMode(actor);
            host?.GetComponent<HostDirectionalRangedAttack>()?.RequireFreshPress();
        }
        public static void SetMapClickTravel(PlayableHostBootstrap host, bool enabled)
        {
            if (enabled == MapClickTravel) return;
            PlayerPrefs.SetInt(MapKey, enabled ? 1 : 0); PlayerPrefs.Save();
            if (!enabled) host?.WorldMapPanel?.CancelClickTravel();
        }
        public static bool NearPoint(PlayableHostBootstrap host, EntityId actor, Vector3 point,
            float range = InteractionRange) => host?.ViewSpawner != null &&
            host.ViewSpawner.Registry.TryGet(actor, out var view) && view != null &&
            Vector2.Distance(view.transform.position, point) <= range &&
            host.MoveController != null && host.MoveController.HasProjectileSight(view.transform.position, point);
        public static bool NearNpc(PlayableHostBootstrap host, EntityId actor, EntityId target) =>
            host?.ViewSpawner != null && host.ViewSpawner.Registry.TryGet(target, out var view) && view != null &&
            NearPoint(host, actor, view.transform.position) && host.MoveController.HasCharacterSight(actor, target);
        public static Vector3 InteractionPoint(WorldObjectInteractionTarget target, Vector3 from)
        {
            if (target.Plot != null && target.Plot.HasInteractionBounds)
            {
                var bounds = target.Plot.InteractionBounds;
                return new Vector3(Mathf.Clamp(from.x, bounds.xMin, bounds.xMax),
                    Mathf.Clamp(from.y, bounds.yMin, bounds.yMax), from.z);
            }
            if (target.Destructible != null && target.Destructible.HasInteractionBounds)
            {
                var bounds = target.Destructible.InteractionBounds;
                return new Vector3(Mathf.Clamp(from.x, bounds.xMin, bounds.xMax),
                    Mathf.Clamp(from.y, bounds.yMin, bounds.yMax), from.z);
            }
            return target.ApproachPosition;
        }
        public static bool NearObject(PlayableHostBootstrap host, EntityId actor, WorldObjectInteractionTarget target)
        {
            if (!target.IsValid || !target.HasApproachPosition || host?.ViewSpawner == null ||
                !host.ViewSpawner.Registry.TryGet(actor, out var view) || view == null) return false;
            var from = view.transform.position;
            var point = InteractionPoint(target, from);
            var delta = point - from; delta.z = 0;
            return delta.magnitude <= InteractionRange && host.MoveController != null &&
                host.MoveController.HasProjectileSight(from, point - delta.normalized * Mathf.Min(.25f, delta.magnitude));
        }
        public static bool RequireNear(PlayableHostBootstrap host, EntityId actor, Vector3 point)
        {
            if (SceneRts || NearPoint(host, actor, point)) return true;
            HostWorldSiteCoreWarfare.Feedback(host, "请先走近目标，再按F或右键交互。");
            return false;
        }
    }
}
