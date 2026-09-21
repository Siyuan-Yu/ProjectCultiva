using XianXia.Core.World;
using System;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Events;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    public enum DefeatSpatialTransitionKind
    {
        InitialIncapacitation = 0,
        DeathConfirmation = 1,
        LegacyRepair = 2,
    }

    /// <summary>Central compatibility decoder for the two lifecycle meanings carried by CombatantDefeated.</summary>
    public static class DefeatSpatialTransitionResolver
    {
        const string DeathConfirmationPayload = "lethal";

        public static DefeatSpatialTransitionKind Resolve(DomainEvent evt, Entity entity)
        {
            if (evt != null && string.Equals(
                    evt.Payload, DeathConfirmationPayload, StringComparison.Ordinal))
                return DefeatSpatialTransitionKind.DeathConfirmation;

            if (entity != null && entity.TryGet<LifecycleComponent>(out var life) && life != null)
            {
                // A delayed or duplicated initial event must never relocate an already dead body.
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
            bool hasPrecisePosition,
            WorldVec2 worldPosition,
            bool hasResidualHex,
            HexCoord residualHex,
            string owner)
        {
            Mode = mode;
            SiteId = siteId ?? string.Empty;
            SurfaceId = surfaceId ?? string.Empty;
            HasPrecisePosition = hasPrecisePosition;
            WorldPosition = worldPosition;
            HasResidualHex = hasResidualHex;
            ResidualHex = residualHex;
            Owner = owner ?? string.Empty;
        }

        public PartyWorldPresenceMode Mode { get; }
        public string SiteId { get; }
        public string SurfaceId { get; }
        public bool HasPrecisePosition { get; }
        public WorldVec2 WorldPosition { get; }
        public bool HasResidualHex { get; }
        public HexCoord ResidualHex { get; }
        public string Owner { get; }
    }

    /// <summary>
    /// Stable personal spatial authority for incapacitated characters and visible corpses.
    /// Lifecycle transitions may query or freeze it, but DeathConfirmation must never replace it.
    /// </summary>
    public static class ResidualSpatialAuthorityService
    {
        public static bool TryResolveStableResidualSpatialAuthority(
            SimulationWorld world,
            EntityId characterId,
            out StableResidualSpatialAuthority authority)
        {
            authority = default;
            if (world == null || characterId.IsNone ||
                !world.Entities.TryGet(characterId, out var entity) || entity == null ||
                !world.WorldPresence.TryGet(characterId, out var presence) || presence == null)
                return false;

            var hasPrecise = IsFinitePrecisePosition(presence);
            var hasHex = presence.UsesHexPresence &&
                         (world.LegacyHexWorld == null || !world.LegacyHexWorld.HasGrid ||
                          world.LegacyHexWorld.Contains(presence.ResidualHex));

            switch (presence.Mode)
            {
                case PartyWorldPresenceMode.AtSite:
                    if (string.IsNullOrEmpty(presence.SiteId) || !hasPrecise)
                        return false;
                    authority = Build(presence, hasPrecise, hasHex, "PersonalAtSite");
                    return true;

                case PartyWorldPresenceMode.AtWorldPosition:
                    if (!hasPrecise)
                        return false;
                    authority = Build(presence, true, hasHex, "PersonalWorldPosition");
                    return true;

                case PartyWorldPresenceMode.AtHex:
                    if (!hasHex && !hasPrecise)
                        return false;
                    authority = Build(presence, hasPrecise, hasHex,
                        hasPrecise ? "PreciseResidualHex" : "LegacyResidualHex");
                    return true;

                case PartyWorldPresenceMode.InEncounter:
                    var encounter = world.Strategic?.CharacterEncounter;
                    var participant = encounter?.Find(characterId.Value);
                    if (participant == null)
                        return false;
                    authority = new StableResidualSpatialAuthority(
                        presence.Mode,
                        participant.SourceSiteId,
                        encounter.SourceSurfaceId,
                        true,
                        new WorldVec2(participant.TacticalX, participant.TacticalY),
                        false,
                        default,
                        "CharacterEncounterTactical");
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Initial downing on a Continuous Surface: freeze the character's own current point while
        /// normalizing spatial authority to AtWorldPosition without changing organization membership.
        /// </summary>
        public static bool TryFreezeAtPreciseWorldPosition(
            SimulationWorld world,
            EntityId characterId,
            WorldVec2 worldPosition,
            string surfaceId)
        {
            if (world == null || characterId.IsNone ||
                !IsFinite(worldPosition.X) || !IsFinite(worldPosition.Y) ||
                !ResidualCharacterPresenceService.IsResidualLifeCandidate(world, characterId))
                return false;

            if (string.IsNullOrWhiteSpace(surfaceId) &&
                world.SurfaceGround.TryResolveContaining(worldPosition, out var containingSurface))
                surfaceId = containingSurface.SurfaceId;
            if (ContinuousOutdoorGameplayPolicy.IsNormalContinuousOutdoor(world) &&
                string.IsNullOrWhiteSpace(surfaceId))
                return false;

            var hexSize = world.LegacyHexWorld != null && world.LegacyHexWorld.HexSize > 0f
                ? world.LegacyHexWorld.HexSize
                : 1f;
            var derived = HexMath.WorldToHex(worldPosition.X, worldPosition.Y, hexSize);
            world.WorldPresence.SetAtWorldPosition(
                characterId, worldPosition, derived, surfaceId);
            return true;
        }

        /// <summary>Initial incapacitation ends movement execution without changing spatial fields or membership.</summary>
        public static void StopResidualMovementAuthority(SimulationWorld world, EntityId characterId)
        {
            if (world?.WorldPresence == null || characterId.IsNone ||
                !world.WorldPresence.TryGet(characterId, out var presence) || presence == null)
                return;
        }

        static StableResidualSpatialAuthority Build(
            WorldAgentPresence presence,
            bool hasPrecise,
            bool hasHex,
            string owner) =>
            new StableResidualSpatialAuthority(
                presence.Mode,
                presence.SiteId,
                presence.PersonalSurfaceId,
                hasPrecise,
                hasPrecise ? presence.ContinuousWorldPosition : default,
                hasHex,
                hasHex ? presence.ResidualHex : default,
                owner);

        static bool IsFinitePrecisePosition(WorldAgentPresence presence) =>
            presence != null && presence.HasContinuousWorldPosition &&
            IsFinite(presence.WorldPosX) && IsFinite(presence.WorldPosY);

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
