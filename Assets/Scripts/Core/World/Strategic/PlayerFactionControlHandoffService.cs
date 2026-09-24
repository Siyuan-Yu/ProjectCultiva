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
    public enum PlayerFactionControlHandoffStatus
    {
        NotRequired = 0,
        DeferredByEncounter = 1,
        NoEligibleCandidate = 2,
        Resolved = 3,
        Failed = 4
    }

    public enum PlayerFactionControlHandoffKind
    {
        None = 0,
        EmergencyTakeover = 1,
        Succession = 2
    }

    public readonly struct PlayerFactionControlHandoffResult
    {
        public PlayerFactionControlHandoffResult(
            PlayerFactionControlHandoffStatus status,
            PlayerFactionControlHandoffKind kind,
            EntityId successorId,
            int combatPower,
            string surfaceId,
            WorldVec2 worldPosition,
            string siteId,
            string sourceSquadId,
            string recoverySquadId,
            string error)
        {
            Status = status;
            Kind = kind;
            SuccessorId = successorId;
            CombatPower = combatPower;
            SurfaceId = surfaceId ?? string.Empty;
            WorldPosition = worldPosition;
            SiteId = siteId ?? string.Empty;
            SourceSquadId = sourceSquadId ?? string.Empty;
            RecoverySquadId = recoverySquadId ?? string.Empty;
            Error = error ?? string.Empty;
        }

        public PlayerFactionControlHandoffStatus Status { get; }
        public PlayerFactionControlHandoffKind Kind { get; }
        public EntityId SuccessorId { get; }
        public int CombatPower { get; }
        public string SurfaceId { get; }
        public WorldVec2 WorldPosition { get; }
        public string SiteId { get; }
        public string SourceSquadId { get; }
        public string RecoverySquadId { get; }
        public string Error { get; }
        public bool IsResolved => Status == PlayerFactionControlHandoffStatus.Resolved;
    }

    /// <summary>
    /// Core authority for choosing and installing an external player-faction controller after the
    /// current PlayerParty has no eligible Active. Ordinary in-party replacement remains owned by
    /// PlayerPartyRuntime and never enters this service.
    /// </summary>
    public static class PlayerFactionControlHandoffService
    {
        public static PlayerFactionControlHandoffResult TryResolve(
            SimulationWorld world,
            PlayerPartyRuntime party)
        {
            if (world == null || party == null || !party.NeedsExternalControlHandoff)
                return Status(PlayerFactionControlHandoffStatus.NotRequired);
            var kind = party.IsAwaitingSuccession
                ? PlayerFactionControlHandoffKind.Succession
                : PlayerFactionControlHandoffKind.EmergencyTakeover;

            var encounter = world.Strategic?.CharacterEncounter;
            if (encounter != null &&
                (encounter.Phase == CharacterEncounterPhase.Preparing ||
                 encounter.Phase == CharacterEncounterPhase.Active ||
                 encounter.Phase == CharacterEncounterPhase.ReadyToEnd))
                return Status(PlayerFactionControlHandoffStatus.DeferredByEncounter, kind);
            if (world.Strategic?.ContinuousManualCombat?.IsActive == true)
                return Status(PlayerFactionControlHandoffStatus.DeferredByEncounter, kind);
            var oldMembers = party.Members;
            for (var i = 0; i < oldMembers.Count; i++)
                if (CharacterEncounterService.OwnsParticipantSpatialState(world, oldMembers[i]) ||
                    ActualBattleParticipantQuery.TryFind(world.Strategic.Participants, oldMembers[i], out _))
                    return Status(PlayerFactionControlHandoffStatus.DeferredByEncounter, kind);

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
                return Status(PlayerFactionControlHandoffStatus.NoEligibleCandidate, kind);

            // Candidate presence is captured above, before any membership mutation. The helper
            // then retires the old squad and detaches only this character from its source squad.
            var replaced = SquadMembershipService.ReplacePlayerSquadForExternalHandoff(
                world, party, best.Id, kind, out var recoverySquadId);
            if (replaced.IsFailure)
                return new PlayerFactionControlHandoffResult(
                    PlayerFactionControlHandoffStatus.Failed, kind, best.Id, best.Power,
                    best.Presence.SurfaceId, best.Presence.WorldPosition,
                    best.SiteId, best.SourceSquadId, recoverySquadId, replaced.Error.ToString());

            if (!party.TryBindControlledSquad(
                    SquadMembershipService.PlayerSquadId, best.Id, out var bindError))
                return new PlayerFactionControlHandoffResult(
                    PlayerFactionControlHandoffStatus.Failed, kind, best.Id, best.Power,
                    best.Presence.SurfaceId, best.Presence.WorldPosition,
                    best.SiteId, best.SourceSquadId, recoverySquadId, bindError);

            SeparateSpaceTransitionService.ReleasePlayerControlForExternalHandoff(world);
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
            var eventType = kind == PlayerFactionControlHandoffKind.EmergencyTakeover
                ? EventType.PlayerEmergencyControlTransferred
                : EventType.PlayerSuccessionResolved;
            var message = kind == PlayerFactionControlHandoffKind.EmergencyTakeover
                ? "当前小队已失去行动能力，" + displayName + " 接管了主控。"
                : displayName + " 接替了玩家势力主控。";
            world.Events.Publish(
                eventType,
                world.Tick,
                target: best.Id,
                payload: message);

            return new PlayerFactionControlHandoffResult(
                PlayerFactionControlHandoffStatus.Resolved, kind, best.Id, best.Power,
                best.Presence.SurfaceId, best.Presence.WorldPosition,
                best.SiteId, best.SourceSquadId, recoverySquadId, string.Empty);
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

        static PlayerFactionControlHandoffResult Status(
            PlayerFactionControlHandoffStatus status,
            PlayerFactionControlHandoffKind kind = PlayerFactionControlHandoffKind.None) =>
            new PlayerFactionControlHandoffResult(
                status, kind, EntityId.None, 0, string.Empty, default,
                string.Empty, string.Empty, string.Empty, string.Empty);

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
