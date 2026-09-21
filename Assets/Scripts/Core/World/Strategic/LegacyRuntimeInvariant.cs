using XianXia.Core.Simulation;
using XianXia.Core.Persistence;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Development-only proof that a modern Continuous session owns no retired runtime authority.</summary>
    public static class LegacyRuntimeInvariant
    {
        public static void AssertModernOpeningScenario(int initialFormalArmyCount)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (initialFormalArmyCount > 0)
                System.Diagnostics.Debug.Fail(
                    "[LegacyRuntimeInvariant] Current opening scenario still declares InitialFormalArmyIds: " +
                    initialFormalArmyCount);
#endif
        }

        public static void AssertModernNewGame(SimulationWorld world)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!ContinuousOutdoorGameplayPolicy.IsNormalContinuousOutdoor(world))
                return;
            foreach (var pair in world.WorldPresence.All)
            {
                var presence = pair.Value;
                if (presence != null && presence.Mode == PartyWorldPresenceMode.AtHex)
                    System.Diagnostics.Debug.Fail(
                        "[LegacyRuntimeInvariant] Modern Character has AtHex presence: " + pair.Key);
            }
            var travel = world.PlayerPartyTravel;
            if (travel != null && travel.HasPosition)
            {
                if (travel.LocationKind != PlayerPartyLocationKind.AtWorldPosition)
                    System.Diagnostics.Debug.Fail(
                        "[LegacyRuntimeInvariant] Modern PlayerParty is not AtWorldPosition: " +
                        travel.LocationKind);
                if (string.IsNullOrWhiteSpace(travel.SurfaceId))
                    System.Diagnostics.Debug.Fail(
                        "[LegacyRuntimeInvariant] Modern PlayerParty SurfaceId is empty.");
                else if (!world.SurfaceGround.TryGet(travel.SurfaceId, out var navigation))
                    System.Diagnostics.Debug.Fail(
                        "[LegacyRuntimeInvariant] Modern PlayerParty Surface is not registered: " +
                        travel.SurfaceId);
                else if (!navigation.Contains(travel.WorldPosition.X, travel.WorldPosition.Y))
                    System.Diagnostics.Debug.Fail(
                        "[LegacyRuntimeInvariant] Modern PlayerParty WorldPosition is outside its Surface: " +
                        travel.SurfaceId);

                if (travel.IsMoving)
                {
                    if (travel.ExecutionMode != PlayerPartyTravelExecutionMode.SurfaceVisible)
                        System.Diagnostics.Debug.Fail(
                            "[LegacyRuntimeInvariant] Moving modern PlayerParty does not use SurfaceVisible: " +
                            travel.ExecutionMode);
                    if (!travel.HasContinuousPhysicalDestination)
                        System.Diagnostics.Debug.Fail(
                            "[LegacyRuntimeInvariant] Moving modern PlayerParty has no continuous physical destination.");
                }
                else if (travel.ExecutionMode != PlayerPartyTravelExecutionMode.None)
                {
                    System.Diagnostics.Debug.Fail(
                        "[LegacyRuntimeInvariant] Idle modern PlayerParty execution is not None: " +
                        travel.ExecutionMode);
                }

                var hexSize = world.HexWorld != null && world.HexWorld.HexSize > 0f
                    ? world.HexWorld.HexSize
                    : 1f;
                var derivedHex = HexMath.WorldToHex(
                    travel.WorldPosition.X, travel.WorldPosition.Y, hexSize);
                if (travel.CurrentHex != derivedHex)
                    System.Diagnostics.Debug.Fail(
                        "[LegacyRuntimeInvariant] Modern PlayerParty CurrentHex is not derived from WorldPosition.");
            }
            if (travel != null && travel.HasPosition &&
                travel.LocationKind == PlayerPartyLocationKind.AtWorldSite &&
                !string.IsNullOrEmpty(travel.SiteId) &&
                world.Strategic.Sites.TryGet(travel.SiteId, out var site) &&
                WorldSiteOutdoorMigrationPolicy.UsesContinuousOutdoorSurface(site))
                System.Diagnostics.Debug.Fail(
                    "[LegacyRuntimeInvariant] Modern Continuous Site uses AtWorldSite: " +
                    travel.SiteId);
            if (world.LocalMap != null && !world.LocalMap.IsInInterior &&
                !string.IsNullOrEmpty(world.LocalMap.ActiveMapLayoutId))
                System.Diagnostics.Debug.Fail(
                    "[LegacyRuntimeInvariant] Outdoor LocalMap is active in a modern Continuous session: " +
                    world.LocalMap.ActiveMapLayoutId);
#endif
        }

        /// <summary>Development-only proof that a newly captured snapshot does not emit retired authorities.</summary>
        public static void AssertModernSnapshot(StrategicSnapshotDto snapshot)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (snapshot == null)
            {
                System.Diagnostics.Debug.Fail("[LegacyRuntimeInvariant] Modern strategic snapshot is null.");
                return;
            }
            if (snapshot.TerritoryRegionControllers != null && snapshot.TerritoryRegionControllers.Count != 0)
                System.Diagnostics.Debug.Fail("[LegacyRuntimeInvariant] Modern snapshot emitted TerritoryRegion authority.");
            if (snapshot.ResidualCharacterPresences != null && snapshot.ResidualCharacterPresences.Count != 0)
                System.Diagnostics.Debug.Fail("[LegacyRuntimeInvariant] Modern snapshot emitted legacy residual AtHex authority.");
            if (snapshot.RetreatingArmies != null && snapshot.RetreatingArmies.Count != 0)
                System.Diagnostics.Debug.Fail("[LegacyRuntimeInvariant] Modern snapshot emitted RetreatingArmy authority.");
            if (snapshot.PendingEngagement != null)
                System.Diagnostics.Debug.Fail("[LegacyRuntimeInvariant] Modern snapshot emitted legacy StrategicEncounter authority.");
            if (snapshot.Ch01FormationScenarioCompat)
                System.Diagnostics.Debug.Fail("[LegacyRuntimeInvariant] Modern snapshot emitted Ch01 formation compatibility state.");
            if (snapshot.CharacterWorldPresences != null)
                for (var i = 0; i < snapshot.CharacterWorldPresences.Count; i++)
                    if (snapshot.CharacterWorldPresences[i] != null &&
                        snapshot.CharacterWorldPresences[i].Mode == (int)PartyWorldPresenceMode.AtHex)
                        System.Diagnostics.Debug.Fail(
                            "[LegacyRuntimeInvariant] Modern snapshot emitted AtHex Character presence: " +
                            snapshot.CharacterWorldPresences[i].CharacterId);
#endif
        }
    }
}
