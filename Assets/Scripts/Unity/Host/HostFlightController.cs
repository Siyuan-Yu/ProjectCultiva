using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Combat;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>Input and visible placement adapter; realm, cost and evasion belong to Core.</summary>
    public sealed class HostFlightController : MonoBehaviour
    {
        PlayableHostBootstrap _host;
        object _world;
        readonly Dictionary<ulong, int> _dodges = new Dictionary<ulong, int>();
        void Awake() { _host = GetComponent<PlayableHostBootstrap>(); }

        public void TryAutoBegin(EntityId id)
        {
            var world = _host?.Session?.World;
            if (world == null || id == _host.Session.PlayerParty.ActiveCharacterId ||
                !world.Entities.TryGet(id, out var entity) || FlightService.IsAirborne(entity) ||
                !entity.TryGet<CultivationComponent>(out var cultivation) || cultivation.Realm < RealmStage.GoldenCore ||
                !SpiritVeilService.IsActive(entity) || !_host.ViewSpawner.Registry.TryGet(id, out var view) || view == null) return;
            TryBegin(view, out _);
        }

        public void ToggleActive() => Toggle(_host?.Session?.PlayerParty?.ActiveCharacterId ?? EntityId.None);

        public void Toggle(EntityId id)
        {
            var world = _host?.Session?.World;
            if (world == null || id.IsNone || !_host.ViewSpawner.Registry.TryGet(id, out var view) || view == null ||
                !world.Entities.TryGet(id, out var entity)) return;
            if (!PlayerPartyRuntime.CanPlayerControlCharacter(world, id))
            { Toast(id, "仅可控制的己方人物可以切换化翼"); return; }
            var move = _host.MoveController;
            if (FlightService.IsAirborne(entity))
            {
                if (!move.IsLandingPointClear(id, view.transform.position))
                { Toast(id, "下方无法落地，请先飞到空地"); return; }
                move.CancelPresentationMovementPublic(id);
                if (_host.Session.PlayerParty.IsActive(id)) PlayerPartySurfaceTravelService.Cancel(world);
                FlightService.Land(entity);
                Toast(id, "收翼落地");
            }
            else if (TryBegin(view, out var message)) Toast(id, "斗气化翼 · F3落地 · F2远程");
            else Toast(id, message);
        }

        bool TryBegin(EntityView view, out string message)
        {
            message = "当前场景不能飞行";
            var surface = _host.ContinuousOutdoorSurfaceRuntime;
            var world = _host.Session.World;
            if (surface?.Mapper == null || !surface.IsActive) return false;
            surface.Mapper.PresentationToWorld(view.transform.position.x, view.transform.position.y, out var x, out var y);
            var result = FlightService.Begin(world, view.EntityId, surface.ActiveSurfaceId,
                world.Strategic.CharacterEncounter?.EncounterId ?? "", new WorldVec2(x, y),
                _host.MoveController.IsLandingPointClear(view.EntityId, view.transform.position));
            message = result.IsFailure ? result.Error.Message : "";
            if (result.IsFailure) return false;
            if (_host.Session.PlayerParty.IsActive(view.EntityId) && world.Strategic.CharacterEncounter == null)
                foreach (var member in _host.Session.PlayerParty.Members)
                    if (member != view.EntityId && world.Entities.TryGet(member, out var follower) && FlightService.Detached(follower) &&
                        _host.ViewSpawner.Registry.TryGet(member, out var followerView) && followerView != null)
                    {
                        surface.Mapper.PresentationToWorld(followerView.transform.position.x, followerView.transform.position.y, out var fx, out var fy);
                        world.WorldPresence.SetAtWorldPosition(member, new WorldVec2(fx, fy), surface.ActiveSurfaceId);
                    }
            _host.Session.Loop.StopSubject(view.EntityId);
            _host.MoveController.CancelPresentationMovementPublic(view.EntityId);
            if (_host.Session.PlayerParty.IsActive(view.EntityId)) PlayerPartySurfaceTravelService.Cancel(world);
            return true;
        }

        void Update()
        {
            using var timing = HostDebugHud.FlightUpdate.Auto();
            var world = _host?.Session?.World;
            if (world == null || _host.ViewSpawner == null) return;
            if (!ReferenceEquals(world, _world)) { _world = world; _dodges.Clear(); }
            if (_host.Session.IsPaused) return;
            var surface = _host.ContinuousOutdoorSurfaceRuntime;
            foreach (var view in _host.ViewSpawner.Registry.All)
            {
                if (view == null || !view.gameObject.activeInHierarchy || !world.Entities.TryGet(view.EntityId, out var entity)) continue;
                var encounter = world.Strategic.CharacterEncounter;
                // Non-controlled Golden Core combatants use the same activation cost and gate.
                if (view.EntityId != _host.Session.PlayerParty.ActiveCharacterId && encounter?.Find(view.EntityId.Value) != null &&
                    !FlightService.IsAirborne(entity) && entity.TryGet<CultivationComponent>(out var cultivation) &&
                    cultivation.Realm >= RealmStage.GoldenCore && SpiritVeilService.IsActive(entity)) TryBegin(view, out _);
                if (!entity.TryGet<FlightComponent>(out var flight)) continue;
                _dodges.TryGetValue(view.EntityId.Value, out var seen);
                if (flight.DodgeSequence != seen) { _dodges[view.EntityId.Value] = flight.DodgeSequence; Toast(view.EntityId, "闪避"); }
                if (!flight.Airborne || surface?.Mapper == null || !surface.IsActive) continue;
                var position = view.transform.position;
                surface.Mapper.PresentationToWorld(position.x, position.y, out var wx, out var wy);
                var scope = encounter?.EncounterId ?? "";
                if (_host.MoveController.IsLandingPointClear(view.EntityId, position))
                    FlightService.RememberGround(flight, surface.ActiveSurfaceId, scope, new WorldVec2(wx, wy));
                if (FlightService.CanSustain(entity))
                {
                    // Autonomous cultivators return to ground work/rest after dismissing combat mode.
                    if (view.EntityId != _host.Session.PlayerParty.ActiveCharacterId && encounter == null &&
                        !SpiritVeilService.IsActive(entity) && _host.MoveController.IsLandingPointClear(view.EntityId, position))
                        FlightService.Land(entity);
                    continue;
                }
                if (!_host.MoveController.TryFindLandingPoint(view.EntityId, position, out var landing))
                {
                    if (!flight.HasGround || flight.SurfaceId != surface.ActiveSurfaceId || flight.ScopeId != scope ||
                        !surface.TryWorldToPresentation(new WorldVec2(flight.GroundX, flight.GroundY), out landing) ||
                        !_host.MoveController.IsLandingPointClear(view.EntityId, landing)) continue;
                }
                _host.MoveController.CancelPresentationMovementPublic(view.EntityId);
                view.transform.position = landing;
                // Capture while still airborne: the old point may be over blocked terrain.
                if (encounter != null) surface.TryCaptureIndependentParticipantPosition(view.EntityId, landing);
                else if (_host.Session.PlayerParty.IsActive(view.EntityId))
                {
                    surface.Mapper.PresentationToWorld(landing.x, landing.y, out wx, out wy);
                    PlayerPartySurfaceTravelService.Cancel(world);
                    world.PlayerPartyTravel.SetAtSurfacePosition(surface.ActiveSurfaceId, new WorldVec2(wx, wy));
                }
                else if (_host.Session.PlayerParty.IsMember(view.EntityId))
                {
                    surface.Mapper.PresentationToWorld(landing.x, landing.y, out wx, out wy);
                    flight.DetachedFollower = true;
                    world.WorldPresence.SetAtWorldPosition(view.EntityId, new WorldVec2(wx, wy), surface.ActiveSurfaceId);
                    PlayerPartyTransitionMembership.CaptureTravelingMembersForPartyTransition(world, _host.Session.PlayerParty);
                }
                else if (!surface.TryCaptureAtSiteAnchor(view.EntityId, landing))
                {
                    surface.Mapper.PresentationToWorld(landing.x, landing.y, out wx, out wy);
                    var point = new WorldVec2(wx, wy);
                    if (SquadWorldMotionService.OwnsCharacter(world, view.EntityId) &&
                        world.Strategic.Squads.TryGetForCharacter(view.EntityId, out var squad) &&
                        world.Strategic.SquadWorldMotions.TryGet(squad.SquadId, out var motion))
                        motion.SetAt(surface.ActiveSurfaceId, point, motion.SiteId);
                    else world.WorldPresence.SetAtWorldPosition(view.EntityId, point, surface.ActiveSurfaceId);
                }
                FlightService.Land(entity);
                Toast(view.EntityId, "化翼消散，紧急落地");
            }
        }

        void Toast(EntityId id, string message) => _host.GetComponent<HostFeedbackOverlay>()?.SpawnAtEntity(
            _host.ViewSpawner, id, message, new Color(.55f, .92f, 1f));
    }
}
