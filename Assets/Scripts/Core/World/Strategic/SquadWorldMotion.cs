using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Surface;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Normal NPC group position and route authority, keyed only by SquadId.</summary>
    public sealed class SquadWorldMotionState
    {
        readonly List<WorldVec2> _route = new List<WorldVec2>(64);
        ReadOnlyCollection<WorldVec2> _routeView;

        public string SquadId { get; internal set; } = string.Empty;
        public string SurfaceId { get; private set; } = string.Empty;
        public string SiteId { get; private set; } = string.Empty;
        public WorldVec2 WorldPosition { get; private set; }
        public WorldVec2 Destination { get; private set; }
        public bool HasPosition { get; private set; }
        public bool IsMoving { get; private set; }
        public int WaypointIndex { get; private set; }
        public float SegmentProgress { get; private set; }
        public string SourceRevision { get; private set; } = string.Empty;
        public string SourceHash { get; private set; } = string.Empty;
        public IReadOnlyList<WorldVec2> Route => _routeView ?? (_routeView = _route.AsReadOnly());

        public void SetAt(string surfaceId, WorldVec2 position, string siteId = "")
        {
            SurfaceId = surfaceId ?? string.Empty;
            SiteId = siteId ?? string.Empty;
            WorldPosition = position;
            Destination = position;
            HasPosition = true;
            IsMoving = false;
            WaypointIndex = 0;
            SegmentProgress = 0f;
            SourceRevision = string.Empty;
            SourceHash = string.Empty;
            _route.Clear();
        }

        public void BeginRoute(IReadOnlyList<WorldVec2> route, WorldVec2 destination,
            string surfaceId, string revision, string hash)
        {
            _route.Clear();
            if (route != null)
                for (var i = 0; i < route.Count; i++) _route.Add(route[i]);
            Destination = destination;
            SurfaceId = surfaceId ?? string.Empty;
            SiteId = string.Empty;
            SourceRevision = revision ?? string.Empty;
            SourceHash = hash ?? string.Empty;
            WaypointIndex = 0;
            SegmentProgress = 0f;
            HasPosition = true;
            IsMoving = _route.Count > 0;
            if (!IsMoving) WorldPosition = destination;
        }

        internal void AdvanceTo(WorldVec2 position, int waypointIndex, float progress)
        {
            WorldPosition = position;
            WaypointIndex = waypointIndex;
            SegmentProgress = progress;
        }

        internal void Finish()
        {
            WorldPosition = Destination;
            IsMoving = false;
            SegmentProgress = 0f;
            _route.Clear();
            WaypointIndex = 0;
        }

        internal void Restore(string surfaceId, string siteId, WorldVec2 position,
            bool moving, WorldVec2 destination, IReadOnlyList<WorldVec2> route,
            int waypointIndex, float progress, string revision, string hash)
        {
            SurfaceId = surfaceId ?? string.Empty;
            SiteId = siteId ?? string.Empty;
            WorldPosition = position;
            Destination = destination;
            HasPosition = true;
            IsMoving = moving;
            WaypointIndex = Math.Max(0, waypointIndex);
            SegmentProgress = progress;
            SourceRevision = revision ?? string.Empty;
            SourceHash = hash ?? string.Empty;
            _route.Clear();
            if (route != null)
                for (var i = 0; i < route.Count; i++) _route.Add(route[i]);
            if (WaypointIndex > _route.Count) WaypointIndex = _route.Count;
        }
    }

    public sealed class SquadWorldMotionBoard
    {
        readonly Dictionary<string, SquadWorldMotionState> _motions =
            new Dictionary<string, SquadWorldMotionState>(StringComparer.Ordinal);
        public IReadOnlyDictionary<string, SquadWorldMotionState> Motions => _motions;
        public bool TryGet(string squadId, out SquadWorldMotionState motion) =>
            _motions.TryGetValue(squadId ?? string.Empty, out motion);
        public bool Register(SquadWorldMotionState motion)
        {
            if (motion == null || string.IsNullOrWhiteSpace(motion.SquadId) ||
                _motions.ContainsKey(motion.SquadId)) return false;
            _motions.Add(motion.SquadId, motion);
            return true;
        }
        public void Remove(string squadId) => _motions.Remove(squadId ?? string.Empty);
        public void Clear() => _motions.Clear();
    }

    public static class SquadWorldMotionService
    {
        static readonly List<WorldVec2> RouteScratch = new List<WorldVec2>(256);
        static readonly List<string> MotionScratch = new List<string>(32);

        public static Result<SquadWorldMotionState> Initialize(SimulationWorld world,
            string squadId, string surfaceId, WorldVec2 position, string siteId = "")
        {
            if (world?.Strategic?.Squads == null ||
                !world.Strategic.Squads.TryGet(squadId, out var squad) || squad == null)
                return Result.Fail<SquadWorldMotionState>(ErrorCode.InvalidArgument,
                    "Squad world-motion target is invalid.", squadId ?? string.Empty);
            if (IsPlayerPartySquad(world, squad))
                return Result.Fail<SquadWorldMotionState>(ErrorCode.InvalidOperation,
                    "PlayerParty cannot use NPC SquadWorldMotion.", squadId ?? string.Empty);
            if (
                !world.SurfaceGround.TryGet(surfaceId, out var navigation) ||
                !navigation.Contains(position.X, position.Y) || !navigation.IsWalkable(position.X, position.Y))
                return Result.Fail<SquadWorldMotionState>(ErrorCode.InvalidArgument,
                    "Squad world-motion initial position is invalid.", squadId ?? string.Empty);
            world.Strategic.SquadWorldMotions.Remove(squadId);
            var motion = new SquadWorldMotionState { SquadId = squadId };
            motion.SetAt(surfaceId, position, siteId);
            world.Strategic.SquadWorldMotions.Register(motion);
            squad.LegacyArmyId = string.Empty;
            squad.SetCommand(SquadCommandKind.SquadWorldMotion);
            return Result.Ok(motion);
        }

        /// <summary>Initializes a stationary NPC Squad at the canonical baked Site arrival.</summary>
        public static Result<SquadWorldMotionState> InitializeAtSite(
            SimulationWorld world, string squadId, string siteId)
        {
            if (world == null || string.IsNullOrWhiteSpace(siteId) ||
                !world.SurfaceGround.TryResolveSiteArrival(siteId, out var surfaceId, out var arrival) ||
                !world.SurfaceGround.TryGet(surfaceId, out var navigation) ||
                !navigation.Contains(arrival.X, arrival.Y) || !navigation.IsWalkable(arrival.X, arrival.Y))
                return Result.Fail<SquadWorldMotionState>(ErrorCode.InvalidOperation,
                    "NPC Squad SiteArrival is unavailable or not walkable.", siteId ?? string.Empty);
            return Initialize(world, squadId, surfaceId, arrival, siteId);
        }

        public static Result MoveToWorldPosition(SimulationWorld world, string squadId, WorldVec2 destination)
        {
            if (world?.Strategic?.Squads == null ||
                !world.Strategic.Squads.TryGet(squadId, out var squad) || squad == null)
                return Result.Failure(ErrorCode.InvalidArgument, "NPC Squad has no world-motion authority.", squadId ?? string.Empty);
            if (IsPlayerPartySquad(world, squad))
                return Result.Failure(ErrorCode.InvalidOperation,
                    "PlayerParty cannot use NPC SquadWorldMotion.", squadId ?? string.Empty);
            if (
                !world.Strategic.SquadWorldMotions.TryGet(squadId, out var motion) ||
                !motion.HasPosition)
                return Result.Failure(ErrorCode.InvalidArgument, "NPC Squad has no world-motion authority.", squadId ?? string.Empty);
            if (string.IsNullOrEmpty(motion.SurfaceId) ||
                !world.SurfaceGround.TryGet(motion.SurfaceId, out var navigation) || navigation == null ||
                !navigation.Contains(motion.WorldPosition.X, motion.WorldPosition.Y) ||
                !navigation.Contains(destination.X, destination.Y))
                return Result.Failure(ErrorCode.InvalidOperation, "NPC Squad destination is not on the same Surface.");
            RouteScratch.Clear();
            var status = navigation.TryFindRoute(motion.WorldPosition, destination, RouteScratch);
            if (status != SurfaceGroundRouteStatus.Found)
                return Result.Failure(ErrorCode.InvalidOperation, "NPC Squad route unavailable: " + status + ".");
            motion.BeginRoute(RouteScratch, destination, navigation.SurfaceId,
                navigation.SourceRevision, navigation.SourceHash);
            squad.SetCommand(SquadCommandKind.SquadWorldMotion);
            return Result.Success();
        }

        public static bool OwnsCharacter(SimulationWorld world, EntityId id)
        {
            if (world?.Strategic?.Squads == null || id.IsNone ||
                world.Strategic.PlayerPartyContext?.IsMember(id) == true ||
                !world.Strategic.Squads.TryGetForCharacter(id, out var squad) || squad == null ||
                !world.Strategic.SquadWorldMotions.TryGet(squad.SquadId, out var motion) ||
                !IsActiveNpcSquadAuthority(world, squad, motion) ||
                !CharacterLifeStateQuery.IsLivingForMacroOrder(world, id))
                return false;
            return squad.MemberCharacterIds.Count > 0;
        }

        public static bool IsPlayerPartySquad(SimulationWorld world, SquadState squad)
        {
            if (world?.Strategic == null || squad == null) return false;
            if (string.Equals(squad.SquadId, SquadMembershipService.PlayerSquadId,
                    StringComparison.Ordinal)) return true;
            var party = world.Strategic.PlayerPartyContext;
            if (party == null) return false;
            if (!string.IsNullOrEmpty(party.ControlledSquadId) &&
                string.Equals(squad.SquadId, party.ControlledSquadId, StringComparison.Ordinal))
                return true;
            for (var i = 0; i < squad.MemberCharacterIds.Count; i++)
                if (party.IsMember(new EntityId(squad.MemberCharacterIds[i]))) return true;
            return false;
        }

        public static bool IsActiveNpcSquadAuthority(SimulationWorld world,
            SquadState squad, SquadWorldMotionState motion) =>
            world?.Strategic != null && squad != null && motion != null && motion.HasPosition &&
            squad.CommandKind == SquadCommandKind.SquadWorldMotion &&
            string.Equals(squad.SquadId, motion.SquadId, StringComparison.Ordinal) &&
            !IsPlayerPartySquad(world, squad);

        public static bool TryGetActiveNpcSquadAuthority(SimulationWorld world, string squadId,
            out SquadState squad, out SquadWorldMotionState motion)
        {
            squad = null;
            motion = null;
            return world?.Strategic?.Squads != null &&
                   world.Strategic.Squads.TryGet(squadId, out squad) &&
                   world.Strategic.SquadWorldMotions.TryGet(squadId, out motion) &&
                   IsActiveNpcSquadAuthority(world, squad, motion);
        }

        /// <summary>Idempotently removes an invalid NPC motion from the controlled PlayerParty Squad.</summary>
        public static bool ReconcilePlayerPartyAuthority(SimulationWorld world, PlayerPartyRuntime party)
        {
            if (world?.Strategic?.Squads == null || party == null ||
                string.IsNullOrEmpty(party.ControlledSquadId) ||
                !world.Strategic.Squads.TryGet(party.ControlledSquadId, out var squad)) return false;
            var changed = world.Strategic.SquadWorldMotions.TryGet(squad.SquadId, out _);
            world.Strategic.SquadWorldMotions.Remove(squad.SquadId);
            if (squad.CommandKind == SquadCommandKind.SquadWorldMotion ||
                squad.CommandKind == SquadCommandKind.FormalArmyWorldMotion)
            {
                var target = party.HasActive ? party.ActiveCharacterId : squad.LeaderCharacterId;
                changed |= SquadCommandService.SetExecution(world, squad.SquadId,
                    SquadCommandKind.FollowLeader, target);
            }
            return changed;
        }

        public static void AdvanceAll(SimulationWorld world, int ticks)
        {
            if (world?.Strategic?.SquadWorldMotions == null || ticks < 1) return;
            var hexSize = world.HexWorld != null && world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            var budget = PlayerPartyTravelRuntimeService.WorldUnitsPerTick(hexSize) * ticks;
            MotionScratch.Clear();
            foreach (var pair in world.Strategic.SquadWorldMotions.Motions)
                if (pair.Value?.IsMoving == true &&
                    world.Strategic.Squads.TryGet(pair.Key, out var squad) &&
                    IsActiveNpcSquadAuthority(world, squad, pair.Value)) MotionScratch.Add(pair.Key);
            for (var i = 0; i < MotionScratch.Count; i++)
                if (world.Strategic.SquadWorldMotions.TryGet(MotionScratch[i], out var motion))
                    Advance(world, motion, budget);
        }

        static void Advance(SimulationWorld world, SquadWorldMotionState motion, float budget)
        {
            if (motion == null || !motion.IsMoving || budget <= 0f ||
                !world.Strategic.Squads.TryGet(motion.SquadId, out var squad) ||
                !IsActiveNpcSquadAuthority(world, squad, motion)) return;
            var hasLiving = false;
            for (var i = 0; i < squad.MemberCharacterIds.Count; i++)
                if (CharacterLifeStateQuery.IsLivingForMacroOrder(world,
                    new EntityId(squad.MemberCharacterIds[i]))) { hasLiving = true; break; }
            if (!hasLiving) return;
            if (!world.SurfaceGround.TryGet(motion.SurfaceId, out var navigation)) return;
            if (!string.Equals(navigation.SourceHash, motion.SourceHash, StringComparison.Ordinal))
            {
                RouteScratch.Clear();
                if (navigation.TryFindRoute(motion.WorldPosition, motion.Destination, RouteScratch) !=
                    SurfaceGroundRouteStatus.Found) return;
                motion.BeginRoute(RouteScratch, motion.Destination, navigation.SurfaceId,
                    navigation.SourceRevision, navigation.SourceHash);
            }
            var position = motion.WorldPosition;
            var index = motion.WaypointIndex;
            var remaining = budget;
            var route = motion.Route;
            var guard = Math.Max(2, route.Count + 1);
            while (remaining > .0001f && index < route.Count && guard-- > 0)
            {
                var target = route[index];
                var distance = WorldVec2.Distance(position, target);
                if (distance <= .0001f) { position = target; index++; continue; }
                if (distance <= remaining + .0001f) { position = target; remaining -= distance; index++; }
                else
                {
                    var ratio = remaining / distance;
                    position = new WorldVec2(position.X + (target.X - position.X) * ratio,
                        position.Y + (target.Y - position.Y) * ratio);
                    remaining = 0f;
                }
                motion.AdvanceTo(position, index, 0f);
            }
            if (index >= route.Count) motion.Finish();
        }
    }
}
