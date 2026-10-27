using System;
using System.Collections.Generic;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Npc;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Surface;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Combat
{
    public sealed class FlightComponent : IComponent
    {
        public bool Airborne;
        public string SurfaceId = "";
        public string ScopeId = "";
        public bool HasGround;
        public float GroundX, GroundY;
        public bool DetachedFollower;
        // Presentation notification, not simulation authority or a second random stream.
        public int DodgeSequence;
    }

    public static class FlightService
    {
        public const int ActivateSpiritCost = 80;
        public const int GroundToAirEvadePercent = 75;
        public static FlightComponent Ensure(Entity entity)
        {
            if (!entity.TryGet<FlightComponent>(out var flight))
            { flight = new FlightComponent(); entity.AddComponent(flight); }
            return flight;
        }
        public static bool IsAirborne(Entity entity) => entity != null &&
            entity.TryGet<FlightComponent>(out var flight) && flight.Airborne;
        public static bool IsAirborne(SimulationWorld world, EntityId id) =>
            world != null && world.Entities.TryGet(id, out var entity) && IsAirborne(entity);
        public static bool Detached(Entity entity) => entity != null &&
            entity.TryGet<FlightComponent>(out var flight) && flight.DetachedFollower;
        public static bool CanSustain(Entity entity) => IsAirborne(entity) && CombatLifeStateService.CanFight(entity) &&
            entity.TryGet<CultivationComponent>(out var cultivation) && cultivation.Realm >= RealmStage.GoldenCore &&
            !PartyRestService.BlocksMovement(entity) && entity.TryGet<CombatVitalsComponent>(out var vitals) && vitals.CurrentSpiritPower > 0;

        public static bool AllAirborne(SimulationWorld world, SquadState squad)
        {
            if (world == null || squad == null || squad.MemberCharacterIds.Count == 0) return false;
            foreach (var member in squad.MemberCharacterIds)
                if (!IsAirborne(world, new EntityId(member))) return false;
            return true;
        }

        public static void PreservePartyPositionsBeforeControlSwitch(SimulationWorld world, PlayerPartyRuntime party, EntityId next)
        {
            if (world == null || party == null || world.Strategic.CharacterEncounter != null || world.LocalMap.IsInInterior) return;
            var needsIndependentPositions = false;
            foreach (var id in party.Members)
                if (world.Entities.TryGet(id, out var entity) && (IsAirborne(entity) || Detached(entity)))
                    needsIndependentPositions = true;
            if (!needsIndependentPositions || !CharacterWorldPresenceQuery.TryResolve(world, next, out var destination) ||
                !destination.HasWorldPosition) return;
            foreach (var id in party.Members)
                if (world.Entities.TryGet(id, out var entity) && CharacterWorldPresenceQuery.TryResolve(world, id, out var at) && at.HasWorldPosition)
                {
                    world.WorldPresence.SetAtWorldPosition(id, at.WorldPosition, at.SurfaceId);
                    Ensure(entity).DetachedFollower = id != next;
                }
            PlayerPartySurfaceTravelService.Cancel(world);
            world.PlayerPartyTravel.SetAtSurfacePosition(destination.SurfaceId, destination.WorldPosition);
        }

        // Offscreen recovery uses the same canonical position and bounded room/terrain geometry.
        public static void MaintainCold(SimulationWorld world)
        {
            foreach (var entity in world.Entities.All)
            {
                if (!IsAirborne(entity) || world.ContinuousOutdoorMaterialization.IsMaterialized(entity.Id) ||
                    !CharacterWorldPresenceQuery.TryResolve(world, entity.Id, out var at) || !at.HasWorldPosition ||
                    !world.SurfaceGround.TryGet(at.SurfaceId, out var navigation)) continue;
                var flight = Ensure(entity);
                if (CanSustain(entity) && (!navigation.IsWalkable(at.WorldPosition.X, at.WorldPosition.Y) ||
                    flight.HasGround && flight.SurfaceId == at.SurfaceId && flight.ScopeId == "" &&
                    flight.GroundX == at.WorldPosition.X && flight.GroundY == at.WorldPosition.Y)) continue;
                bool Allowed(WorldVec2 p) => AllowsAirPosition(world, at.SurfaceId, p);
                var found = NpcLocalNavigation.TryLandingPoint(world, navigation, at.WorldPosition, Allowed, out var landing);
                if (found && landing.Equals(at.WorldPosition)) RememberGround(flight, at.SurfaceId, "", landing);
                if (CanSustain(entity)) continue;
                if (!found && flight.HasGround && flight.SurfaceId == at.SurfaceId && flight.ScopeId == "")
                {
                    var saved = new WorldVec2(flight.GroundX, flight.GroundY);
                    found = NpcLocalNavigation.TryLandingPoint(world, navigation, saved, Allowed, out landing);
                }
                if (!found) continue; // Never invent a walkable point or another Surface.
                BackgroundCharacterTravelService.CancelTravel(world, entity.Id);
                var party = world.Strategic.PlayerPartyContext;
                if (party?.ActiveCharacterId == entity.Id)
                {
                    PlayerPartySurfaceTravelService.Cancel(world);
                    world.PlayerPartyTravel.SetAtSurfacePosition(at.SurfaceId, landing);
                }
                else if (SquadWorldMotionService.OwnsCharacter(world, entity.Id) &&
                    world.Strategic.Squads.TryGetForCharacter(entity.Id, out var squad) &&
                    world.Strategic.SquadWorldMotions.TryGet(squad.SquadId, out var motion))
                    motion.SetAt(at.SurfaceId, landing, motion.SiteId);
                else world.WorldPresence.SetAtWorldPosition(entity.Id, landing, at.SurfaceId);
                Land(entity);
            }
        }

        public static Result Begin(SimulationWorld world, EntityId id, string surfaceId, string scopeId,
            WorldVec2 groundPoint, bool groundClear)
        {
            if (world == null || !world.Entities.TryGet(id, out var entity) || !CombatLifeStateService.CanFight(entity) ||
                !entity.TryGet<CultivationComponent>(out var cultivation) || cultivation.Realm < RealmStage.GoldenCore)
                return Result.Failure(ErrorCode.InvalidOperation, "金丹期才能展开斗气化翼。");
            if (IsAirborne(entity)) return Result.Failure(ErrorCode.InvalidOperation, "已经在空中。");
            if (SeparateSpaceCombatPolicy.IsInPlaceCombatSpace(world) || !groundClear ||
                !world.SurfaceGround.TryGet(surfaceId, out var surface) || !surface.Contains(groundPoint.X, groundPoint.Y) ||
                !HuangcunOpeningAccess.AllowsPosition(world, surfaceId, groundPoint.X, groundPoint.Y))
                return Result.Failure(ErrorCode.InvalidOperation, "请在户外合法落脚点展开化翼。");
            if (PartyRestService.BlocksMovement(entity))
                return Result.Failure(ErrorCode.InvalidOperation, "当前休息状态不允许起飞。");
            CombatDamageRules.EnsureVitals(entity);
            if (!entity.TryGet<CombatVitalsComponent>(out var vitals) || vitals.CurrentSpiritPower <= ActivateSpiritCost)
                return Result.Failure(ErrorCode.InvalidOperation, "化翼需80灵力，且至少保留1点。");
            vitals.CurrentSpiritPower -= ActivateSpiritCost;
            var flight = Ensure(entity);
            flight.Airborne = true;
            RememberGround(flight, surfaceId, scopeId, groundPoint);
            // Preserve grounded followers as real individuals instead of projecting them across an obstacle.
            var party = world.Strategic.PlayerPartyContext;
            if (party?.ActiveCharacterId == id && world.Strategic.CharacterEncounter == null)
                foreach (var member in party.Members)
                    if (member != id && world.Entities.TryGet(member, out var follower) && !IsAirborne(follower))
                    {
                        if (CharacterWorldPresenceQuery.TryResolve(world, member, out var at) && at.HasWorldPosition)
                            world.WorldPresence.SetAtWorldPosition(member, at.WorldPosition, at.SurfaceId);
                        Ensure(follower).DetachedFollower = true;
                    }
            PlayerPartyTransitionMembership.CaptureTravelingMembersForPartyTransition(world, party);
            return Result.Success();
        }

        public static void RememberGround(FlightComponent flight, string surface, string scope, WorldVec2 point)
        {
            flight.SurfaceId = surface; flight.ScopeId = scope ?? "";
            flight.GroundX = point.X; flight.GroundY = point.Y; flight.HasGround = true;
        }
        public static void Land(Entity entity)
        { if (entity.TryGet<FlightComponent>(out var flight)) flight.Airborne = false; }

        public static bool TryEvade(SimulationWorld world, Entity attacker, Entity defender)
        {
            if (world == null || !CanSustain(defender) || IsAirborne(attacker)) return false;
            if (world.Random.NextInt(0, 100) >= GroundToAirEvadePercent) return false;
            Ensure(defender).DodgeSequence++;
            return true;
        }

        public static bool AllowsAirPosition(SimulationWorld world, string surfaceId, WorldVec2 position) =>
            world != null && world.SurfaceGround.TryGet(surfaceId, out var nav) && nav.Contains(position.X, position.Y) &&
            HuangcunOpeningAccess.AllowsPosition(world, surfaceId, position.X, position.Y);

        public static SurfaceGroundRouteStatus Route(SimulationWorld world, EntityId actor,
            SurfaceGroundNavigation nav, WorldVec2 from, WorldVec2 to, List<WorldVec2> route)
        {
            if (!IsAirborne(world, actor)) return nav.TryFindRoute(from, to, route);
            route.Clear();
            if (!AllowsAirPosition(world, nav.SurfaceId, from) || !AllowsAirPosition(world, nav.SurfaceId, to))
                return SurfaceGroundRouteStatus.BlockedGoal;
            route.Add(from); route.Add(to); return SurfaceGroundRouteStatus.Found;
        }
    }
}
