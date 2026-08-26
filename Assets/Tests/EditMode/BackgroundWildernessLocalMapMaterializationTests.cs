using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Exploration;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests
{
    public sealed class BackgroundWildernessLocalMapMaterializationTests
    {
        const string FactionA = "test:faction_a";
        const float FloatTol = 0.12f;

        static WildernessLocalWorldProjection.WildernessLocalMapBounds DefaultBounds =>
            WildernessLocalWorldProjection.WildernessLocalMapBounds.FromOriginSize(0f, 0f, 1f, 16, 16);

        static SimulationWorld BuildWildernessWorld(out HexCoord hexA, out HexCoord hexB)
        {
            var world = new SimulationWorld();
            world.HexWorld.MapId = "test:bg_wilderness";
            world.HexWorld.HexSize = 1f;
            world.HexWorld.FillRectangle(20, 12, HexTerrainType.Plain);
            for (var r = 0; r < 12; r++)
            for (var q = 0; q < 20; q++)
            {
                if (!world.HexWorld.TryGetCell(new HexCoord(q, r), out var cell) || cell == null)
                    continue;
                cell.IsPassable = true;
            }

            hexA = new HexCoord(5, 4);
            hexB = new HexCoord(9, 4);
            return world;
        }

        static EntityId Spawn(SimulationWorld world, string name)
        {
            var created = world.Entities.CreateCharacter(new DefinitionId("test", name), name);
            Assert.IsTrue(created.IsSuccess);
            created.Value.Get<FactionMembershipComponent>().Assign(FactionA, FactionRoleKind.Member);
            return created.Value.Id;
        }

        static void SetupLoadedWildernessLocalMap(
            SimulationWorld world,
            HexCoord hex,
            PlayerPartyRuntime party,
            EntityId active)
        {
            Assert.IsTrue(WildernessLocalMapFallback.TryResolve(world, hex, out var mapId));
            world.LocalMap.ActiveMapLayoutId = mapId;
            world.LocalMap.OverworldMapLayoutId = mapId;
            world.LocalMap.SetPlayableBounds(0f, 0f, 1f, 16, 16);
            world.LocalMap.ExitTriggerDepth = SurfaceExitZoneCalculator.DefaultExitTriggerDepth;
            world.PartyWorld.ClearSiteFocus();
            world.PartyWorld.SiteId = string.Empty;
            world.PartyWorld.LocalMapId = mapId;
            world.PartyWorld.Mode = PartyWorldPresenceMode.AtHex;
            HexMath.ToWorldPosition(hex, world.HexWorld.HexSize, out var x, out var y);
            world.PlayerPartyTravel.SetAtWorldPosition(new WorldVec2(x, y), hex);
            world.LocalMap.AddOccupant(active);
            if (party != null)
            {
                for (var i = 0; i < party.Members.Count; i++)
                    world.LocalMap.AddOccupant(party.Members[i]);
            }
        }

        static void AdvanceUntilArrival(SimulationWorld world, EntityId traveler)
        {
            for (var guard = 0; guard < 512 && world.BackgroundCharacterTravel.IsTraveling(traveler); guard++)
                BackgroundCharacterTravelService.AdvanceAll(world, 4);
            Assert.IsFalse(world.BackgroundCharacterTravel.IsTraveling(traveler));
        }

        static WorldVec2 OffsetWorldPosition(HexCoord hex, float dx, float dy)
        {
            HexMath.ToWorldPosition(hex, 1f, out var x, out var y);
            return new WorldVec2(x + dx, y + dy);
        }

        [Test]
        public void BackgroundCharacterAtWildernessHexMaterializesWhenPlayerEntersLocalMap()
        {
            var world = BuildWildernessWorld(out var hexA, out _);
            var active = Spawn(world, "Hero");
            var bg = Spawn(world, "Companion");
            world.WorldPresence.SetAtWorldPosition(bg, OffsetWorldPosition(hexA, 0.15f, -0.1f), hexA);

            var party = new PlayerPartyRuntime();
            Assert.IsTrue(party.TryInitialize(active, out _));
            SetupLoadedWildernessLocalMap(world, hexA, party, active);

            var count = LoadedDestinationArrivalMaterializer.MaterializeEligibleWildernessCharactersOnLocalMap(
                world, party, DefaultBounds);

            Assert.AreEqual(1, count);
            Assert.IsTrue(world.LocalMap.ContainsOccupant(bg));
            Assert.IsTrue(world.Entities.TryGet(bg, out var ent));
            Assert.IsTrue(ent.TryGet<EntityLocationComponent>(out var loc));
            Assert.IsTrue(loc.HasPresentationOverride);
        }

        [Test]
        public void BackgroundWildernessMaterializeUsesWorldPositionProjection()
        {
            var world = BuildWildernessWorld(out var hexA, out _);
            var active = Spawn(world, "Hero");
            var bg = Spawn(world, "Companion");
            var worldPos = OffsetWorldPosition(hexA, 0.2f, 0.15f);
            world.WorldPresence.SetAtWorldPosition(bg, worldPos, hexA);

            var party = new PlayerPartyRuntime();
            Assert.IsTrue(party.TryInitialize(active, out _));
            SetupLoadedWildernessLocalMap(world, hexA, party, active);

            var bounds = DefaultBounds;
            Assert.IsTrue(WildernessLocalWorldProjection.TryProjectWorldToLocal(
                worldPos, bounds, 1f, out var expectedX, out var expectedY));
            LoadedDestinationArrivalMaterializer.MaterializeEligibleWildernessCharactersOnLocalMap(
                world, party, bounds);

            Assert.IsTrue(world.Entities.TryGet(bg, out var ent));
            Assert.IsTrue(ent.TryGet<EntityLocationComponent>(out var loc));
            Assert.AreEqual(expectedX, loc.PresentationOverrideX, FloatTol);
            Assert.AreEqual(expectedY, loc.PresentationOverrideZ, FloatTol);

            var centerDist = System.Math.Sqrt(
                System.Math.Pow(loc.PresentationOverrideX - bounds.CenterX, 2) +
                System.Math.Pow(loc.PresentationOverrideZ - bounds.CenterY, 2));
            Assert.Less(centerDist, bounds.HalfWidth * 0.45f);
        }

        [Test]
        public void BackgroundWildernessReenterMaterializesAfterLeave()
        {
            var world = BuildWildernessWorld(out var hexA, out _);
            var active = Spawn(world, "Hero");
            var bg = Spawn(world, "Companion");
            world.WorldPresence.SetAtWorldPosition(bg, OffsetWorldPosition(hexA, 0.1f, 0.05f), hexA);

            var party = new PlayerPartyRuntime();
            Assert.IsTrue(party.TryInitialize(active, out _));
            SetupLoadedWildernessLocalMap(world, hexA, party, active);
            LoadedDestinationArrivalMaterializer.MaterializeEligibleWildernessCharactersOnLocalMap(
                world, party, DefaultBounds);
            Assert.IsTrue(world.LocalMap.ContainsOccupant(bg));

            LoadedDestinationArrivalMaterializer.ReleaseEligibleOccupantsOnLocalMapUnload(world, party);
            Assert.IsFalse(world.LocalMap.ContainsOccupant(bg));
            Assert.IsTrue(CharacterWorldMovementAuthorityQuery.TryGetAuthority(
                world, bg, party, out var authority));
            Assert.AreNotEqual(CharacterWorldMovementAuthority.LoadedLocalRealtime, authority);

            LoadedDestinationArrivalMaterializer.MaterializeEligibleWildernessCharactersOnLocalMap(
                world, party, DefaultBounds);
            Assert.IsTrue(world.LocalMap.ContainsOccupant(bg));
        }

        [Test]
        public void BackgroundCharacterEnteringLoadedWildernessMaterializesImmediately()
        {
            var world = BuildWildernessWorld(out var hexA, out var hexB);
            var active = Spawn(world, "Hero");
            var bg = Spawn(world, "Companion");
            world.WorldPresence.SetAtHex(bg, hexB);

            var party = new PlayerPartyRuntime();
            Assert.IsTrue(party.TryInitialize(active, out _));
            SetupLoadedWildernessLocalMap(world, hexA, party, active);

            Assert.IsTrue(BackgroundCharacterTravelService.BeginTravelToHex(
                world, bg, hexA, party, debugOverrideLocalOccupant: true).IsSuccess);
            AdvanceUntilArrival(world, bg);

            Assert.IsTrue(world.LocalMap.ContainsOccupant(bg));
            Assert.IsFalse(world.BackgroundCharacterTravel.IsTraveling(bg));
            Assert.IsTrue(CharacterWorldMovementAuthorityQuery.TryGetAuthority(
                world, bg, party, out var authority));
            Assert.AreEqual(CharacterWorldMovementAuthority.LoadedLocalRealtime, authority);
            Assert.Greater(LoadedDestinationArrivalMaterializer.PendingPresentationFlush.Count, 0);
        }

        [Test]
        public void BackgroundCharacterCrossingLoadedWildernessHexMaterializesWithoutFinishArrival()
        {
            var world = BuildWildernessWorld(out var hexA, out var hexB);
            var hexC = new HexCoord(hexA.Q + 2, hexA.R);
            var active = Spawn(world, "Hero");
            var bg = Spawn(world, "Companion");
            world.WorldPresence.SetAtHex(bg, hexB);

            var party = new PlayerPartyRuntime();
            Assert.IsTrue(party.TryInitialize(active, out _));
            SetupLoadedWildernessLocalMap(world, hexA, party, active);

            Assert.IsTrue(BackgroundCharacterTravelService.BeginTravelToHex(
                world, bg, hexC, party, debugOverrideLocalOccupant: true).IsSuccess);
            BackgroundCharacterTravelService.AdvanceAll(world, 64);

            Assert.IsTrue(world.LocalMap.ContainsOccupant(bg));
            Assert.IsFalse(world.BackgroundCharacterTravel.IsTraveling(bg));
            Assert.IsTrue(CharacterWorldMovementAuthorityQuery.TryGetAuthority(
                world, bg, party, out var authority));
            Assert.AreEqual(CharacterWorldMovementAuthority.LoadedLocalRealtime, authority);
        }

        [Test]
        public void BackgroundSiteDepartureToAdjacentOutsideHexMaterializesAfterBoundaryCrossing()
        {
            var world = BuildTravelWorldWithSite(out var site, out var outsideHex);
            var active = Spawn(world, "Hero");
            var bg = Spawn(world, "Companion");
            world.WorldPresence.SetAtSite(bg, site.SiteId);

            var party = new PlayerPartyRuntime();
            Assert.IsTrue(party.TryInitialize(active, out _));
            SetupLoadedWildernessLocalMap(world, outsideHex, party, active);

            Assert.IsTrue(BackgroundCharacterTravelService.BeginTravelToHex(
                world, bg, outsideHex, party, debugOverrideLocalOccupant: true).IsSuccess);
            Assert.IsTrue(world.WorldPresence.TryGet(bg, out var atStart));
            Assert.AreEqual(PartyWorldPresenceMode.AtSite, atStart.Mode);
            Assert.IsTrue(world.BackgroundCharacterTravel.IsTraveling(bg));
            Assert.IsFalse(world.LocalMap.ContainsOccupant(bg));

            AdvanceUntilArrival(world, bg);

            Assert.IsTrue(world.LocalMap.ContainsOccupant(bg));
            Assert.IsFalse(world.BackgroundCharacterTravel.IsTraveling(bg));
            Assert.IsTrue(CharacterWorldMovementAuthorityQuery.TryGetAuthority(
                world, bg, party, out var authority));
            Assert.AreEqual(CharacterWorldMovementAuthority.LoadedLocalRealtime, authority);
        }

        [Test]
        public void SiteDepartureAdjacentTargetKeepsAtWorldSiteUntilSchedulerAdvances()
        {
            var world = BuildTravelWorldWithSite(out var site, out var outsideHex);
            var bg = Spawn(world, "Companion");
            world.WorldPresence.SetAtSite(bg, site.SiteId);

            Assert.IsTrue(BackgroundCharacterTravelService.BeginTravelToHex(
                world, bg, outsideHex, debugOverrideLocalOccupant: true).IsSuccess);
            Assert.IsTrue(world.WorldPresence.TryGet(bg, out var presence));
            Assert.AreEqual(PartyWorldPresenceMode.AtSite, presence.Mode);
            Assert.IsTrue(world.BackgroundCharacterTravel.TryGet(bg, out var motion));
            Assert.IsTrue(motion.IsSiteDeparturePending);
            Assert.IsTrue(motion.IsMoving);
        }

        [Test]
        public void SiteDepartureAdjacentTargetBuildsFootprintToOutsideRoute()
        {
            var world = BuildTravelWorldWithSite(out var site, out var outsideHex);
            var active = Spawn(world, "Hero");
            var bg = Spawn(world, "Companion");
            world.WorldPresence.SetAtSite(bg, site.SiteId);

            var party = new PlayerPartyRuntime();
            Assert.IsTrue(party.TryInitialize(active, out _));
            var farHex = new HexCoord(outsideHex.Q + 4, outsideHex.R);
            SetupLoadedWildernessLocalMap(world, farHex, party, active);

            Assert.IsTrue(BackgroundCharacterTravelService.BeginTravelToHex(
                world, bg, outsideHex, party, debugOverrideLocalOccupant: true).IsSuccess);
            Assert.IsTrue(world.BackgroundCharacterTravel.TryGet(bg, out var motion));
            Assert.GreaterOrEqual(motion.HexPathCount, 2);
            Assert.IsTrue(motion.IsMoving);
            Assert.IsTrue(motion.IsSiteDeparturePending);
        }

        [Test]
        public void BackgroundSiteDepartureIntoLoadedWildernessHexMaterializesOnBoundaryCrossing()
        {
            var world = BuildTravelWorldWithSite(out var site, out var outsideHex);
            var active = Spawn(world, "Hero");
            var bg = Spawn(world, "Companion");
            world.WorldPresence.SetAtSite(bg, site.SiteId);

            var party = new PlayerPartyRuntime();
            Assert.IsTrue(party.TryInitialize(active, out _));
            SetupLoadedWildernessLocalMap(world, outsideHex, party, active);

            Assert.IsTrue(BackgroundCharacterTravelService.BeginTravelToHex(
                world, bg, new HexCoord(outsideHex.Q + 2, outsideHex.R), party, debugOverrideLocalOccupant: true).IsSuccess);
            Assert.IsFalse(world.LocalMap.ContainsOccupant(bg));
            Assert.IsTrue(world.BackgroundCharacterTravel.IsTraveling(bg));

            BackgroundCharacterTravelService.AdvanceAll(world, 64);

            Assert.IsTrue(world.LocalMap.ContainsOccupant(bg));
            Assert.IsFalse(world.BackgroundCharacterTravel.IsTraveling(bg));
        }

        [Test]
        public void BackgroundRuntimeArrivalDoesNotMutateActiveWorldOrLocalPosition()
        {
            var world = BuildWildernessWorld(out var hexA, out var hexB);
            var active = Spawn(world, "Hero");
            var bg = Spawn(world, "Companion");
            world.WorldPresence.SetAtHex(bg, hexB);

            var party = new PlayerPartyRuntime();
            Assert.IsTrue(party.TryInitialize(active, out _));
            SetupLoadedWildernessLocalMap(world, hexA, party, active);

            var before = BackgroundLoadedLocalMapArrivalDebug.CaptureActiveSideEffectTrace(
                world,
                bg,
                party,
                nameof(LoadedDestinationArrivalMaterializer.TryMaterializeCharacterIntoLoadedLocalMap));

            Assert.IsTrue(BackgroundCharacterTravelService.BeginTravelToHex(
                world, bg, hexA, party, debugOverrideLocalOccupant: true).IsSuccess);
            AdvanceUntilArrival(world, bg);

            var after = BackgroundLoadedLocalMapArrivalDebug.FinishActiveSideEffectTrace(in before, world);

            Assert.IsTrue(world.LocalMap.ContainsOccupant(bg));
            Assert.AreEqual(before.ActiveWorldBefore, after.ActiveWorldAfter);
            Assert.AreEqual(before.ActiveLocalBefore, after.ActiveLocalAfter);
        }

        static SimulationWorld BuildTravelWorldWithSite(out WorldSite site, out HexCoord outsideHex)
        {
            var world = new SimulationWorld();
            world.HexWorld.HexSize = 1f;
            world.HexWorld.FillRectangle(20, 12, HexTerrainType.Plain);
            for (var r = 0; r < 12; r++)
            for (var q = 0; q < 20; q++)
            {
                if (!world.HexWorld.TryGetCell(new HexCoord(q, r), out var cell) || cell == null)
                    continue;
                cell.IsPassable = true;
            }

            var anchor = new HexCoord(2, 6);
            var presence = new HexCoord(3, 6);
            outsideHex = new HexCoord(4, 6);
            site = new WorldSite
            {
                SiteId = "test:site_huangcun",
                DisplayName = "青石荒村",
                AnchorHex = anchor,
                PresenceHex = presence,
                LocalMapId = "base:map_ch01_reference",
            };
            site.SetFootprint(new[] { anchor, presence, new HexCoord(2, 5), new HexCoord(3, 5) });
            WorldSiteRegistrationService.RegisterSiteOnGrid(world, site);
            return world;
        }

        [Test]
        public void BackgroundWildernessDoesNotMaterializeOnDifferentLoadedHex()
        {
            var world = BuildWildernessWorld(out var hexA, out var hexB);
            var active = Spawn(world, "Hero");
            var bg = Spawn(world, "Companion");
            world.WorldPresence.SetAtWorldPosition(bg, OffsetWorldPosition(hexB, 0.1f, 0f), hexB);

            var party = new PlayerPartyRuntime();
            Assert.IsTrue(party.TryInitialize(active, out _));
            SetupLoadedWildernessLocalMap(world, hexA, party, active);

            var count = LoadedDestinationArrivalMaterializer.MaterializeEligibleWildernessCharactersOnLocalMap(
                world, party, DefaultBounds);
            Assert.AreEqual(0, count);
            Assert.IsFalse(world.LocalMap.ContainsOccupant(bg));
        }
    }
}
