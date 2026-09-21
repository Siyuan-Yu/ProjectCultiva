using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Persistence
{
    /// <summary>
    /// Snapshot Load：由 ActiveControlledCharacter 的正式 WorldLocation 决定 Required LocalMap。
    /// PartyWorld / PreferredMapLayout 仅为 presentation cache，不得覆盖 Active 真源。
    /// </summary>
    public static class SnapshotActiveControlledLocalMapResolver
    {
        public struct Resolved
        {
            public bool HasValue;
            public string LocalMapId;
            public string SiteId;
            public PartyWorldPresenceMode PartyWorldMode;
            public HexCoord WildernessHex;
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

            // SPACE-01：Active Separate Space > Outdoor return／WorldPresence。
            if (TryResolveFromActiveSeparateSpace(world, out resolved))
            {
                _lastResolved = resolved;
                return true;
            }

            var activeId = party.ActiveCharacterId;

            if (TryResolveFromActiveWorldPresence(world, activeId, out resolved))
            {
                _lastResolved = resolved;
                return true;
            }

            if (TryResolveFromPlayerPartyTravel(world, party, activeId, out resolved))
            {
                _lastResolved = resolved;
                return true;
            }

            return false;
        }

        static bool TryResolveFromActiveSeparateSpace(SimulationWorld world, out Resolved resolved)
        {
            resolved = default;
            var session = world?.LocalMap;
            if (session == null || !session.IsActive)
                return false;
            var mapId = session.ActiveMapLayoutId?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(mapId))
                return false;

            resolved = new Resolved
            {
                HasValue = true,
                LocalMapId = mapId,
                SiteId = string.Empty,
                PartyWorldMode = PartyWorldPresenceMode.InSeparateSpace,
                WildernessHex = default,
                WorldLocationLabel = "SeparateSpace(" + mapId + ")",
                Source = "SeparateSpaceSession"
            };
            return true;
        }

        public static void ApplyResolvedPartyWorldFocus(SimulationWorld world, in Resolved resolved)
        {
            if (world?.PartyWorld == null || !resolved.HasValue)
                return;

            world.PartyWorld.EncounterId = string.Empty;

            if (resolved.PartyWorldMode == PartyWorldPresenceMode.AtSite &&
                !string.IsNullOrEmpty(resolved.SiteId))
            {
                world.PartyWorld.SiteId = resolved.SiteId;
                world.PartyWorld.LocalMapId = resolved.LocalMapId ?? string.Empty;
                world.PartyWorld.Mode = PartyWorldPresenceMode.AtSite;
                return;
            }

            if (resolved.PartyWorldMode == PartyWorldPresenceMode.InSeparateSpace)
            {
                world.PartyWorld.ClearSiteFocus();
                world.PartyWorld.LocalMapId = resolved.LocalMapId ?? string.Empty;
                world.PartyWorld.Mode = PartyWorldPresenceMode.InSeparateSpace;
                return;
            }

            world.PartyWorld.ClearSiteFocus();
            world.PartyWorld.LocalMapId = resolved.LocalMapId ?? string.Empty;
            // Only compatibility resolutions reach AtHex here. Confirmed Continuous positions
            // are resolved above as AtWorldPosition; Separate Space and Site have explicit modes.
            world.PartyWorld.Mode = resolved.PartyWorldMode == PartyWorldPresenceMode.AtWorldPosition
                ? PartyWorldPresenceMode.AtWorldPosition
                : PartyWorldPresenceMode.AtHex;
        }

        /// <summary>Active 权威 Required Map 与 target 一致时，允许在尚未 Materialize 前装图。</summary>
        public static bool ActiveAuthorizesMapLoad(
            SimulationWorld world,
            PlayerPartyRuntime party,
            string targetMapLayoutId)
        {
            if (string.IsNullOrWhiteSpace(targetMapLayoutId))
                return false;
            if (!TryResolveRequiredLocalMap(world, party, out var resolved) || !resolved.HasValue)
                return false;
            return string.Equals(
                resolved.LocalMapId?.Trim(),
                targetMapLayoutId.Trim(),
                System.StringComparison.Ordinal);
        }

        static bool TryResolveFromActiveWorldPresence(
            SimulationWorld world,
            EntityId activeId,
            out Resolved resolved)
        {
            resolved = default;
            if (!world.WorldPresence.TryGet(activeId, out var wp) || wp == null)
                return false;

            if (wp.Mode == PartyWorldPresenceMode.AtSite && !string.IsNullOrEmpty(wp.SiteId))
            {
                if (!world.Strategic.Sites.TryGet(wp.SiteId, out var site) || site == null)
                    return false;

                if (ContinuousOutdoorGameplayPolicy.IsNormalContinuousOutdoor(world) &&
                    WorldSiteOutdoorMigrationPolicy.UsesContinuousOutdoorSurface(site))
                {
                    resolved = new Resolved
                    {
                        HasValue = true, LocalMapId = string.Empty, SiteId = site.SiteId,
                        PartyWorldMode = PartyWorldPresenceMode.AtSite,
                        WildernessHex = site.PresenceHex,
                        WorldLocationLabel = "ContinuousSite(" + site.SiteId + ")",
                        Source = "ActiveWorldPresence.ContinuousSite"
                    };
                    return true;
                }

                resolved = new Resolved
                {
                    HasValue = true,
                    LocalMapId = site.LocalMapId ?? string.Empty,
                    SiteId = site.SiteId,
                    PartyWorldMode = PartyWorldPresenceMode.AtSite,
                    WildernessHex = site.PresenceHex,
                    WorldLocationLabel = "AtWorldSite(" + site.SiteId + ")",
                    Source = "ActiveWorldPresence.AtSite"
                };
                return !string.IsNullOrEmpty(resolved.LocalMapId);
            }

            if (ContinuousOutdoorGameplayPolicy.IsNormalContinuousOutdoor(world) &&
                wp.HasContinuousWorldPosition &&
                world.SurfaceGround.TryResolveContaining(wp.ContinuousWorldPosition, out _))
            {
                resolved = new Resolved
                {
                    HasValue = true, LocalMapId = string.Empty,
                    PartyWorldMode = PartyWorldPresenceMode.AtWorldPosition,
                    WildernessHex = wp.ResidualHex,
                    WorldLocationLabel = "ContinuousWorldPosition",
                    Source = "ActiveWorldPresence.ContinuousSurface"
                };
                return true;
            }

            if (wp.Mode == PartyWorldPresenceMode.AtHex && wp.UsesHexPresence)
            {
                if (!LegacyWildernessLocalMapFallback.TryResolve(world, wp.ResidualHex, out var mapId) ||
                    string.IsNullOrEmpty(mapId))
                    return false;

                resolved = new Resolved
                {
                    HasValue = true,
                    LocalMapId = mapId,
                    SiteId = string.Empty,
                    PartyWorldMode = PartyWorldPresenceMode.AtHex,
                    WildernessHex = wp.ResidualHex,
                    WorldLocationLabel = "AtHex(" + wp.ResidualHex + ")",
                    Source = "ActiveWorldPresence.AtHex"
                };
                return true;
            }

            if (wp.Mode == PartyWorldPresenceMode.AtWorldPosition)
            {
                // An exact position outside registered Continuous Surface coverage may still
                // address a legacy Outdoor LocalMap. Quarantine that old map-loading contract as
                // AtHex presentation context; it is not a modern Continuous authority downgrade.
                var hex = wp.ResidualHex;
                if (!LegacyWildernessLocalMapFallback.TryResolve(world, hex, out var mapId) ||
                    string.IsNullOrEmpty(mapId))
                    return false;

                resolved = new Resolved
                {
                    HasValue = true,
                    LocalMapId = mapId,
                    SiteId = string.Empty,
                    PartyWorldMode = PartyWorldPresenceMode.AtHex,
                    WildernessHex = hex,
                    WorldLocationLabel = "AtWorldPosition(hex=" + hex + ")",
                    Source = "ActiveWorldPresence.AtWorldPosition"
                };
                return true;
            }

            return false;
        }

        static bool TryResolveFromPlayerPartyTravel(
            SimulationWorld world,
            PlayerPartyRuntime party,
            EntityId activeId,
            out Resolved resolved)
        {
            resolved = default;
            if (!PlayerPartyWorldLocationQuery.TryResolve(world, party, out var motionResolved) ||
                !motionResolved.HasValue)
                return false;

            if (motionResolved.LocationKind == PlayerPartyLocationKind.AtWorldSite)
            {
                var continuousSite = ContinuousOutdoorGameplayPolicy.IsNormalContinuousOutdoor(world) &&
                                     world.Strategic.Sites.TryGet(motionResolved.SiteId, out var site) &&
                                     WorldSiteOutdoorMigrationPolicy.UsesContinuousOutdoorSurface(site);
                resolved = new Resolved
                {
                    HasValue = true,
                    LocalMapId = continuousSite ? string.Empty : motionResolved.ResolvedLocalMapId ?? string.Empty,
                    SiteId = motionResolved.SiteId ?? string.Empty,
                    PartyWorldMode = PartyWorldPresenceMode.AtSite,
                    WildernessHex = motionResolved.DerivedHex,
                    WorldLocationLabel = "AtWorldSite(" + (motionResolved.SiteId ?? string.Empty) + ")",
                    Source = "PlayerPartyTravel.AtWorldSite(active=" + activeId.Value + ")"
                };
                return continuousSite || !string.IsNullOrEmpty(resolved.LocalMapId);
            }

            if (ContinuousOutdoorGameplayPolicy.IsNormalContinuousOutdoor(world) &&
                world.SurfaceGround.TryResolveContaining(motionResolved.WorldPosition, out _))
            {
                resolved = new Resolved
                {
                    HasValue = true, LocalMapId = string.Empty,
                    PartyWorldMode = PartyWorldPresenceMode.AtWorldPosition,
                    WildernessHex = motionResolved.DerivedHex,
                    WorldLocationLabel = "ContinuousWorldPosition",
                    Source = "PlayerPartyTravel.ContinuousSurface"
                };
                return true;
            }

            resolved = new Resolved
            {
                HasValue = true,
                LocalMapId = motionResolved.ResolvedLocalMapId ?? string.Empty,
                SiteId = string.Empty,
                PartyWorldMode = PartyWorldPresenceMode.AtHex,
                WildernessHex = motionResolved.DerivedHex,
                WorldLocationLabel = "AtWorldPosition(" + motionResolved.WorldPosition + ")",
                Source = "PlayerPartyTravel.AtWorldPosition(active=" + activeId.Value + ")"
            };
            return !string.IsNullOrEmpty(resolved.LocalMapId);
        }
    }
}
