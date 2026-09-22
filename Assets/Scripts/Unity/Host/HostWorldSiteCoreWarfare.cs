using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.World.Strategic;
using XianXia.Data.Content;

namespace XianXia.Unity.Host
{
    /// <summary>Presentation handoff after the existing political confirmation.</summary>
    public static class HostWorldSiteCoreWarfare
    {
        public static bool ValidateTarget(PlayableHostBootstrap host, EntityId attacker, string siteId)
        {
            var world = host.Session.World;
            var result = WorldSiteCoreWarfareService.Resolve(world, siteId, out var target);
            if (result.IsSuccess) result = WorldSiteCoreWarfareService.Validate(world, attacker, target, requireWar: false);
            if (result.IsFailure) { Feedback(host, result.Error.Message); return false; }
            return true;
        }

        public static void BeginConfirmed(PlayableHostBootstrap host, EntityId attacker, string siteId)
        {
            var world = host.Session.World;
            var surface = host.ContinuousOutdoorSurfaceRuntime;
            if (world.Strategic.CharacterEncounter == null) surface.CaptureCurrentPersonalPlacements();
            else surface.CaptureIndependentField();
            var result = WorldSiteCoreWarfareService.Resolve(world, siteId, out var target);
            if (result.IsSuccess) result = WorldSiteCoreWarfareService.Validate(world, attacker, target);
            if (result.IsFailure) { Feedback(host, result.Error.Message); return; }
            var defender = WorldSiteDefenseCharacterQuery.ResolveAssaultDefender(world, target);
            var state = world.Strategic.CharacterEncounter;
            if (state != null)
            {
                var previous = state.Participants.Count;
                var oldObjective = state.Objective;
                result = WorldSiteCoreWarfareService.BindObjective(world, attacker, target);
                if (result.IsSuccess && !defender.IsNone)
                    result = CharacterEncounterService.TryJoinObjectiveDefenderSquad(world, defender, surface.PrepareInterventionPlacement);
                if (result.IsFailure) { state.Objective = oldObjective; Feedback(host, result.Error.Message); return; }
                if (state.Participants.Count != previous) surface.PresentJoinedParticipants(previous);
                ContinueAssault(host, siteId);
            }
            else if (!defender.IsNone)
                host.GetComponent<HostCharacterEncounter>().RequestWorldSiteAssault(attacker, defender, siteId,
                    () => ContinueAssault(host, siteId));
            else ContinueAssault(host, siteId);
        }

        public static void ContinueAssault(PlayableHostBootstrap host, string siteId)
        {
            var world = host.Session.World;
            var resolved = WorldSiteCoreWarfareService.Resolve(world, siteId, out var target);
            if (resolved.IsFailure) { Feedback(host, resolved.Error.Message); return; }
            var actor = host.Session.PlayerParty.ActiveCharacterId;
            host.GetComponent<HostCharacterEncounter>()?.Stop(actor);
            MapLayoutDefinition layout = null;
            var surface = host.ContinuousOutdoorSurfaceRuntime;
            if (surface == null || !surface.IsActive) MapLayoutPick.TryGet(host.Session, out layout);
            if (target.CoreIsRemovable)
            {
                host.GetComponent<HostControlCoreAssault>()?.Clear();
                var flag = world.Strategic.FactionFlags.Flags[target.CoreAssetId];
                var point = default(Vector3);
                var hasPoint = surface != null && surface.IsActive &&
                    HostFactionFlagQuery.TryGetApproachPoint(
                        flag, surface, host.MoveController.WalkGrid, out point);
                if (!hasPoint)
                { Feedback(host, "找不到势力旗的合法接近位置。"); return; }
                if (host.MoveController == null || !host.MoveController.OrderPartyToPointPublic(point))
                { Feedback(host, "无法向势力旗下达接近命令。"); return; }
                var assault = host.GetComponent<HostFactionFlagAssault>();
                if (assault == null) { Feedback(host, "势力旗攻击执行器不可用。"); return; }
                assault.Begin(flag.FlagId);
            }
            else if (WorldSiteCoreWarfareService.TryGetFixedCore(world, siteId, out var core))
            {
                host.GetComponent<HostFactionFlagAssault>()?.Clear();
                if (!HostControlCoreQuery.TryGetApproachPoint(world, layout, surface, core, out var point))
                { Feedback(host, "找不到议政厅的合法接近位置。"); return; }
                if (host.MoveController == null || !host.MoveController.OrderPartyToPointPublic(point))
                { Feedback(host, "无法向议政厅下达接近命令。"); return; }
                var assault = host.GetComponent<HostControlCoreAssault>();
                if (assault == null) { Feedback(host, "议政厅攻击执行器不可用。"); return; }
                assault.Begin(core.WorkAreaId);
                host.GetComponent<HostHousingAreaSelection>()?.SelectControlCore(core.WorkAreaId);
            }
            // Movement input can update the combat target; retain the chosen building intent afterwards.
            host.GetComponent<HostCharacterEncounter>()?.Stop(actor);
        }

        public static void Feedback(PlayableHostBootstrap host, string message)
        {
            if (host == null) return;
            host.GetComponent<HostFeedbackOverlay>()?.SpawnAtEntity(host.ViewSpawner,
                host.Session.PlayerParty.ActiveCharacterId, message, new Color(1f, .55f, .3f));
            Debug.LogWarning("[SiteCoreWarfare] " + message);
        }
    }
}
