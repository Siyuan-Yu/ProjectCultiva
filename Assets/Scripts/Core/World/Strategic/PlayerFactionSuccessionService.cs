using System;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Events;
using XianXia.Core.Exploration;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    public enum PlayerFactionSuccessionStatus
    {
        NotAwaitingSuccession = 0,
        DeferredByEncounter = 1,
        NoEligibleCandidate = 2,
        Resolved = 3,
        Failed = 4
    }

    public readonly struct PlayerFactionSuccessionResult
    {
        public PlayerFactionSuccessionResult(
            PlayerFactionSuccessionStatus status,
            EntityId successorId,
            int combatPower,
            string surfaceId,
            WorldVec2 worldPosition,
            string siteId,
            string sourceSquadId,
            string error)
        {
            Status = status;
            SuccessorId = successorId;
            CombatPower = combatPower;
            SurfaceId = surfaceId ?? string.Empty;
            WorldPosition = worldPosition;
            SiteId = siteId ?? string.Empty;
            SourceSquadId = sourceSquadId ?? string.Empty;
            Error = error ?? string.Empty;
        }

        public PlayerFactionSuccessionStatus Status { get; }
        public EntityId SuccessorId { get; }
        public int CombatPower { get; }
        public string SurfaceId { get; }
        public WorldVec2 WorldPosition { get; }
        public string SiteId { get; }
        public string SourceSquadId { get; }
        public string Error { get; }
        public bool IsResolved => Status == PlayerFactionSuccessionStatus.Resolved;
    }

    /// <summary>
    /// Core authority for choosing and installing a successor after a genuine PlayerParty wipe.
    /// Ordinary in-party Active replacement remains owned by PlayerPartyRuntime and never enters
    /// this service.
    /// </summary>
    public static class PlayerFactionSuccessionService
    {
        public static PlayerFactionSuccessionResult TryResolve(
            SimulationWorld world,
            PlayerPartyRuntime party)
        {
            if (world == null || party == null || !party.IsAwaitingSuccession)
                return Status(PlayerFactionSuccessionStatus.NotAwaitingSuccession);

            var encounter = world.Strategic?.CharacterEncounter;
            if (encounter != null &&
                (encounter.Phase == CharacterEncounterPhase.Preparing ||
                 encounter.Phase == CharacterEncounterPhase.Active ||
                 encounter.Phase == CharacterEncounterPhase.ReadyToEnd))
                return Status(PlayerFactionSuccessionStatus.DeferredByEncounter);
            if (world.Strategic?.ContinuousManualCombat?.IsActive == true)
                return Status(PlayerFactionSuccessionStatus.DeferredByEncounter);

            var playerFactionId = world.Strategic?.PlayerFactionId ?? string.Empty;
            Candidate best = default;
            var hasBest = false;
            foreach (var entity in world.Entities.All)
            {
                if (!TryBuildCandidate(world, party, playerFactionId, entity, out var candidate))
                    continue;
                if (!hasBest || candidate.Power > best.Power ||
                    (candidate.Power == best.Power && candidate.Id.Value < best.Id.Value))
                {
                    best = candidate;
                    hasBest = true;
                }
            }

            if (!hasBest)
                return Status(PlayerFactionSuccessionStatus.NoEligibleCandidate);

            // Candidate presence is captured above, before any membership mutation. The helper
            // then retires the old squad and detaches only this character from its source squad.
            var replaced = SquadMembershipService.ReplacePlayerSquadForSuccession(
                world, party, best.Id);
            if (replaced.IsFailure)
                return new PlayerFactionSuccessionResult(
                    PlayerFactionSuccessionStatus.Failed, best.Id, best.Power,
                    best.Presence.SurfaceId, best.Presence.WorldPosition,
                    best.SiteId, best.SourceSquadId, replaced.Error.ToString());

            if (!party.TryBindControlledSquad(
                    SquadMembershipService.PlayerSquadId, best.Id, out var bindError))
                return new PlayerFactionSuccessionResult(
                    PlayerFactionSuccessionStatus.Failed, best.Id, best.Power,
                    best.Presence.SurfaceId, best.Presence.WorldPosition,
                    best.SiteId, best.SourceSquadId, bindError);

            SeparateSpaceTransitionService.ReleasePlayerControlAfterPartyWipe(world);
            world.PlayerPartyTravel.SetAtSurfacePosition(
                best.Presence.SurfaceId, best.Presence.WorldPosition);
            world.PlayerPartyTravel.SetCurrentOutdoorWorldSiteContext(best.SiteId);
            PlayerPartyTransitionMembership.CaptureTravelingMembersForPartyTransition(world, party);
            world.WorldPresence.SetAtWorldPosition(
                best.Id, best.Presence.WorldPosition, best.Presence.SurfaceId);
            world.PartyWorld.Mode = PartyWorldPresenceMode.AtWorldPosition;
            world.PartyWorld.LocalMapId = string.Empty;
            world.PartyWorld.SiteId = string.Empty;
            world.PartyWorld.EncounterId = string.Empty;
            SquadWorldMotionService.ReconcilePlayerPartyAuthority(world, party);

            var displayName = world.Entities.TryGet(best.Id, out var successor) &&
                              !string.IsNullOrWhiteSpace(successor.DisplayName)
                ? successor.DisplayName
                : "人物 " + best.Id.Value;
            world.Events.Publish(
                EventType.PlayerSuccessionResolved,
                world.Tick,
                target: best.Id,
                payload: displayName + " 接替了玩家势力主控。");

            return new PlayerFactionSuccessionResult(
                PlayerFactionSuccessionStatus.Resolved, best.Id, best.Power,
                best.Presence.SurfaceId, best.Presence.WorldPosition,
                best.SiteId, best.SourceSquadId, string.Empty);
        }

        static bool TryBuildCandidate(
            SimulationWorld world,
            PlayerPartyRuntime party,
            string playerFactionId,
            Entity entity,
            out Candidate candidate)
        {
            candidate = default;
            if (entity == null || entity.Id.IsNone ||
                (entity.Tags & (EntityTag.Character | EntityTag.Npc)) == 0 ||
                party.IsMember(entity.Id) ||
                !entity.TryGet<FactionMembershipComponent>(out var faction) ||
                faction == null || !faction.IsAffiliated ||
                !string.Equals(faction.FactionId, playerFactionId, StringComparison.Ordinal) ||
                !entity.TryGet<LifecycleComponent>(out var life) || life == null ||
                life.State != LifecycleState.Alive ||
                !PlayerPartyRuntime.CanActAsActive(world, entity.Id, out _) ||
                CharacterEncounterService.OwnsParticipantSpatialState(world, entity.Id) ||
                !CharacterWorldPresenceQuery.TryResolve(world, entity.Id, out var presence) ||
                !presence.HasWorldPosition || string.IsNullOrWhiteSpace(presence.SurfaceId) ||
                !Finite(presence.WorldPosition.X) || !Finite(presence.WorldPosition.Y))
                return false;

            if (!world.SurfaceGround.TryGet(presence.SurfaceId, out var surface) ||
                surface == null ||
                !surface.Contains(presence.WorldPosition.X, presence.WorldPosition.Y))
                return false;

            var siteId = presence.SiteId;
            if (string.IsNullOrEmpty(siteId) &&
                WorldSiteAdministrativeControlResolver.TryResolve(
                    world, presence.SurfaceId,
                    presence.WorldPosition.X, presence.WorldPosition.Y,
                    out var site, out _) && site != null)
                siteId = site.SiteId;
            var sourceSquadId = world.Strategic.Squads.TryGetForCharacter(
                entity.Id, out var sourceSquad)
                ? sourceSquad.SquadId
                : string.Empty;
            candidate = new Candidate(
                entity.Id,
                CombatPowerCalculator.ForEntity(world, entity.Id),
                presence,
                siteId,
                sourceSquadId);
            return true;
        }

        static PlayerFactionSuccessionResult Status(PlayerFactionSuccessionStatus status) =>
            new PlayerFactionSuccessionResult(
                status, EntityId.None, 0, string.Empty, default,
                string.Empty, string.Empty, string.Empty);

        static bool Finite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        readonly struct Candidate
        {
            public Candidate(
                EntityId id,
                int power,
                CharacterWorldPresenceQuery.ResolvedPresence presence,
                string siteId,
                string sourceSquadId)
            {
                Id = id;
                Power = power;
                Presence = presence;
                SiteId = siteId ?? string.Empty;
                SourceSquadId = sourceSquadId ?? string.Empty;
            }

            public EntityId Id { get; }
            public int Power { get; }
            public CharacterWorldPresenceQuery.ResolvedPresence Presence { get; }
            public string SiteId { get; }
            public string SourceSquadId { get; }
        }
    }
}
