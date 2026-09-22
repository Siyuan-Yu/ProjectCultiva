using System;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Events;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    public enum DefeatSpatialTransitionKind
    {
        InitialIncapacitation = 0,
        DeathConfirmation = 1,
        LegacyRepair = 2,
    }

    public static class DefeatSpatialTransitionResolver
    {
        public static DefeatSpatialTransitionKind Resolve(DomainEvent evt, Entity entity)
        {
            if (evt != null && string.Equals(evt.Payload, "lethal", StringComparison.Ordinal))
                return DefeatSpatialTransitionKind.DeathConfirmation;
            if (entity != null &&
                entity.TryGet<LifecycleComponent>(out var life) && life != null)
            {
                if (life.IsDead)
                    return DefeatSpatialTransitionKind.DeathConfirmation;
                if (life.IsIncapacitated)
                    return DefeatSpatialTransitionKind.InitialIncapacitation;
            }
            return DefeatSpatialTransitionKind.LegacyRepair;
        }
    }

    public readonly struct StableResidualSpatialAuthority
    {
        public StableResidualSpatialAuthority(
            PartyWorldPresenceMode mode,
            string siteId,
            string surfaceId,
            WorldVec2 worldPosition,
            string owner)
        {
            Mode = mode;
            SiteId = siteId ?? string.Empty;
            SurfaceId = surfaceId ?? string.Empty;
            WorldPosition = worldPosition;
            Owner = owner ?? string.Empty;
        }

        public PartyWorldPresenceMode Mode { get; }
        public string SiteId { get; }
        public string SurfaceId { get; }
        public bool HasPrecisePosition => true;
        public WorldVec2 WorldPosition { get; }
        public string Owner { get; }
    }

    public static class ResidualSpatialAuthorityService
    {
        public static bool TryResolveStableResidualSpatialAuthority(
            SimulationWorld world,
            EntityId characterId,
            out StableResidualSpatialAuthority authority)
        {
            authority = default;
            if (world == null || characterId.IsNone ||
                !world.WorldPresence.TryGet(characterId, out var presence) ||
                presence == null)
                return false;

            if (presence.Mode == PartyWorldPresenceMode.InEncounter)
            {
                var encounter = world.Strategic?.CharacterEncounter;
                var participant = encounter?.Find(characterId.Value);
                if (participant == null)
                    return false;
                authority = new StableResidualSpatialAuthority(
                    presence.Mode,
                    participant.SourceSiteId,
                    encounter.SourceSurfaceId,
                    new WorldVec2(participant.TacticalX, participant.TacticalY),
                    "CharacterEncounterTactical");
                return true;
            }

            if ((presence.Mode != PartyWorldPresenceMode.AtSite &&
                 presence.Mode != PartyWorldPresenceMode.AtWorldPosition) ||
                !presence.HasContinuousWorldPosition ||
                !Finite(presence.WorldPosX) || !Finite(presence.WorldPosY) ||
                string.IsNullOrEmpty(presence.PersonalSurfaceId))
                return false;
            authority = new StableResidualSpatialAuthority(
                presence.Mode,
                presence.SiteId,
                presence.PersonalSurfaceId,
                presence.ContinuousWorldPosition,
                presence.Mode == PartyWorldPresenceMode.AtSite
                    ? "PersonalAtSite"
                    : "PersonalWorldPosition");
            return true;
        }

        public static bool TryFreezeAtPreciseWorldPosition(
            SimulationWorld world,
            EntityId characterId,
            WorldVec2 worldPosition,
            string surfaceId)
        {
            if (world == null || characterId.IsNone ||
                !Finite(worldPosition.X) || !Finite(worldPosition.Y) ||
                !ResidualCharacterPresenceService.IsResidualLifeCandidate(world, characterId))
                return false;
            if (string.IsNullOrWhiteSpace(surfaceId) &&
                world.SurfaceGround.TryResolveContaining(worldPosition, out var surface))
                surfaceId = surface.SurfaceId;
            if (string.IsNullOrWhiteSpace(surfaceId))
                return false;
            world.WorldPresence.SetAtWorldPosition(
                characterId, worldPosition, surfaceId);
            return true;
        }

        static bool Finite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
