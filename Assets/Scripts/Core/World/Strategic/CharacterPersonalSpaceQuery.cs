using System;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Personal physical authority, independent of View materialization and legacy organization.</summary>
    public static class CharacterPersonalSpaceQuery
    {
        public static bool TryResolveContinuous(SimulationWorld world, EntityId id, string surfaceId,
            out WorldVec2 position, out string reason)
        {
            position = default;
            reason = "MissingPersonalSpace";
            if (world == null || id.IsNone || string.IsNullOrWhiteSpace(surfaceId) ||
                !world.Entities.TryGet(id, out _) || !world.WorldPresence.TryGet(id, out var presence))
                return false;
            if (!string.Equals(presence.PersonalSurfaceId, surfaceId, StringComparison.Ordinal))
            {
                reason = string.IsNullOrEmpty(presence.PersonalSurfaceId)
                    ? "LegacyUnqualifiedPosition" : "DifferentSurface";
                return false;
            }
            if (presence.Mode == PartyWorldPresenceMode.InEncounter || !presence.HasContinuousWorldPosition ||
                float.IsNaN(presence.WorldPosX) || float.IsInfinity(presence.WorldPosX) ||
                float.IsNaN(presence.WorldPosY) || float.IsInfinity(presence.WorldPosY))
            {
                reason = "InvalidPersonalSpace";
                return false;
            }
            position = presence.ContinuousWorldPosition;
            reason = "PersonalWorldPresence";
            return true;
        }
    }
}
