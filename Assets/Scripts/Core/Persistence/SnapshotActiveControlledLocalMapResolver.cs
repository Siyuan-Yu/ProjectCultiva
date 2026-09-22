using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Persistence
{
    /// <summary>
    /// Resolves the presentation space required after a current snapshot restore.
    /// Only an active Separate Space requires a LocalMap; normal Outdoor remains on its Surface.
    /// </summary>
    public static class SnapshotActiveControlledLocalMapResolver
    {
        public struct Resolved
        {
            public bool HasValue;
            public string LocalMapId;
            public string SiteId;
            public PartyWorldPresenceMode PartyWorldMode;
            public string WorldLocationLabel;
            public string Source;
        }

        static Resolved _lastResolved;
        public static Resolved LastResolved => _lastResolved;

        public static bool TryResolveRequiredLocalMap(
            SimulationWorld world,
            PlayerPartyRuntime party,
            out Resolved resolved)
        {
            resolved = default;
            if (world == null || party == null || !party.HasActive)
                return false;
            var session = world.LocalMap;
            if (session != null && session.IsActive &&
                !string.IsNullOrWhiteSpace(session.ActiveMapLayoutId))
            {
                resolved = new Resolved
                {
                    HasValue = true,
                    LocalMapId = session.ActiveMapLayoutId.Trim(),
                    SiteId = string.Empty,
                    PartyWorldMode = PartyWorldPresenceMode.InSeparateSpace,
                    WorldLocationLabel = "SeparateSpace(" + session.ActiveMapLayoutId.Trim() + ")",
                    Source = "SeparateSpaceSession"
                };
                _lastResolved = resolved;
                return true;
            }

            if (!PlayerPartyWorldLocationQuery.TryResolve(world, party, out var outdoor) ||
                !outdoor.HasValue)
                return false;
            resolved = new Resolved
            {
                HasValue = true,
                LocalMapId = string.Empty,
                SiteId = outdoor.SiteId,
                PartyWorldMode = PartyWorldPresenceMode.AtWorldPosition,
                WorldLocationLabel = "ContinuousSurface(" + outdoor.SurfaceId + ")",
                Source = "PlayerPartyTravel"
            };
            _lastResolved = resolved;
            return true;
        }

        public static void ApplyResolvedPartyWorldFocus(
            SimulationWorld world,
            in Resolved resolved)
        {
            if (world?.PartyWorld == null || !resolved.HasValue)
                return;
            world.PartyWorld.EncounterId = string.Empty;
            world.PartyWorld.ClearSiteFocus();
            world.PartyWorld.LocalMapId = resolved.LocalMapId ?? string.Empty;
            world.PartyWorld.SiteId = resolved.SiteId ?? string.Empty;
            world.PartyWorld.Mode = resolved.PartyWorldMode;
        }

    }
}
