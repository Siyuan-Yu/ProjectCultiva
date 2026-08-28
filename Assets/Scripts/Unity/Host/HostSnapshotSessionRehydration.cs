using UnityEngine;
using XianXia.Core.Persistence;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;
using XianXia.Data.Bootstrap;
using XianXia.Data.Content;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Snapshot Restore 后补全 Content Shell（HexWorld／WorldRegion／PartyWorld），不 respawn Character。
    /// </summary>
    public static class HostSnapshotSessionRehydration
    {
        public static void RehydrateAfterRestore(PlayableHostBootstrap bootstrap)
        {
            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return;

            var session = bootstrap.Session;
            var world = session.World;
            var registry = session.Registry;
            if (world == null || registry == null)
                return;

            var scenarioParsed = XianXia.Core.Domain.Ids.DefinitionId.Parse(bootstrap.OpeningScenarioId ?? string.Empty);
            var scenarioId = scenarioParsed.IsSuccess
                ? scenarioParsed.Value
                : PlayableDayBootstrap.DefaultScenarioId;
            if (registry.TryGetOpeningScenario(scenarioId, out var scenario) &&
                scenario != null)
            {
                var hex = HexStrategicMapContentBootstrap.TryApplyToSession(world, registry, scenario);
                if (hex.IsFailure)
                    Debug.LogWarning("[SnapshotRestore] HexWorld rehydrate: " + hex.Error);
            }

            ResolvePartyWorldFromActiveControlledCharacter(world, session.PlayerParty);

            var mapId = world.PartyWorld?.LocalMapId?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(mapId))
            {
                session.PreferredMapLayoutId = mapId;
                bootstrap.ConfigurePreferredMapLayout(mapId);
                var places = WorldRegionBootstrap.ActivatePlacesForMapLayout(world, registry, mapId);
                if (places.IsFailure)
                    Debug.LogWarning("[SnapshotRestore] WorldRegion rehydrate: " + places.Error);
                WorldTravelService.ApplyLocalMapSessionFromFocus(world);
            }

            StrategicSnapshotHelper.FinalizeRuntimeLinks(world);
            session.RefreshViewableEntityIds();
        }

        /// <summary>
        /// ActiveControlledCharacter WorldLocation → Required LocalMap（覆盖 stale PartyWorld cache）。
        /// </summary>
        public static void ResolvePartyWorldFromActiveControlledCharacter(
            SimulationWorld world,
            PlayerPartyRuntime party)
        {
            if (world?.PartyWorld == null)
                return;

            if (!SnapshotActiveControlledLocalMapResolver.TryResolveRequiredLocalMap(
                    world,
                    party,
                    out var resolved) ||
                !resolved.HasValue)
            {
                SyncPartyWorldPresentationCacheFallback(world, party);
                return;
            }

            SnapshotActiveControlledLocalMapResolver.ApplyResolvedPartyWorldFocus(world, in resolved);
        }

        static void SyncPartyWorldPresentationCacheFallback(
            SimulationWorld world,
            PlayerPartyRuntime party)
        {
            if (world?.PartyWorld == null)
                return;

            if (!PlayerPartyWorldLocationQuery.TryResolve(world, party, out var resolved) ||
                !resolved.HasValue)
                return;

            world.PartyWorld.EncounterId = string.Empty;
            world.PartyWorld.FocusFormalArmyId = string.Empty;

            if (resolved.LocationKind == PlayerPartyLocationKind.AtWorldSite &&
                !string.IsNullOrEmpty(resolved.SiteId))
            {
                world.PartyWorld.SiteId = resolved.SiteId;
                world.PartyWorld.LocalMapId = resolved.ResolvedLocalMapId ?? string.Empty;
                world.PartyWorld.Mode = PartyWorldPresenceMode.AtSite;
                return;
            }

            world.PartyWorld.ClearSiteFocus();
            world.PartyWorld.LocalMapId = resolved.ResolvedLocalMapId ?? string.Empty;
            world.PartyWorld.Mode = PartyWorldPresenceMode.AtHex;
        }

        public static void LogDomainTrace(PlayableHostSession session, string phase)
        {
            if (session == null || !session.IsInitialized)
                return;

            var world = session.World;
            var characterCount = 0;
            foreach (var entity in world.Entities.All)
            {
                if (entity != null && (entity.Tags & XianXia.Core.Entities.EntityTag.Character) != 0)
                    characterCount++;
            }

            var partyMembers = session.PlayerParty?.Count ?? 0;
            var active = session.PlayerParty?.ActiveCharacterId.Value ?? 0UL;
            var armyCount = world.Strategic?.FormalArmies?.Armies?.Count ?? 0;

            SnapshotActiveControlledLocalMapResolver.TryResolveRequiredLocalMap(
                world,
                session.PlayerParty,
                out var required);

            var sb = new System.Text.StringBuilder();
            sb.Append("[SnapshotRestore.Domain] phase=").Append(phase);
            sb.Append(" RuntimeCharacterCount=").Append(characterCount);
            sb.Append(" ActiveCharacterId=").Append(active);
            sb.Append(" PlayerPartyMembers=").Append(partyMembers);
            sb.Append(" RequiredLocalMap=").Append(required.LocalMapId ?? string.Empty);
            sb.Append(" RequiredWorldLocation=").Append(required.WorldLocationLabel ?? string.Empty);
            sb.Append(" PartyWorld.Site=").Append(world.PartyWorld?.SiteId ?? string.Empty);
            sb.Append(" PartyWorld.Map=").Append(world.PartyWorld?.LocalMapId ?? string.Empty);
            sb.Append(" ActiveLocalMap=").Append(world.LocalMap?.ActiveMapLayoutId ?? string.Empty);

            Debug.Log(sb.ToString());
        }
    }
}
