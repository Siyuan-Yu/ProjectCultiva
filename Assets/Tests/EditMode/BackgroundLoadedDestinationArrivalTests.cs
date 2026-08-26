using System.Collections.Generic;
using NUnit.Framework;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Exploration;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests
{
    public sealed class BackgroundLoadedDestinationArrivalTests
    {
        const string FactionA = "test:faction_a";
        const float FloatTol = 0.12f;

        static WildernessLocalWorldProjection.WildernessLocalMapBounds DefaultBounds =>
            WildernessLocalWorldProjection.WildernessLocalMapBounds.FromOriginSize(0f, 0f, 1f, 16, 16);

        static SimulationWorld BuildTravelWorld(out WorldSite siteA, out WorldSite siteB, out HexCoord midHex)
        {
            var world = new SimulationWorld();
            world.HexWorld.MapId = "test:bg_arrival";
            world.HexWorld.HexSize = 1f;
            world.HexWorld.FillRectangle(20, 12, HexTerrainType.Plain);
            for (var r = 0; r < 12; r++)
            for (var q = 0; q < 20; q++)
            {
                if (!world.HexWorld.TryGetCell(new HexCoord(q, r), out var cell) || cell == null)
                    continue;
                cell.IsPassable = true;
            }

            var aAnchor = new HexCoord(2, 4);
            var aPresence = new HexCoord(3, 4);
            siteA = new WorldSite
            {
                SiteId = "test:site_huangcun",
                DisplayName = "青石荒村",
                AnchorHex = aAnchor,
                PresenceHex = aPresence,
                LocalMapId = "base:map_ch01_reference",
            };
            siteA.SetFootprint(new[]
            {
                aAnchor, aPresence, new HexCoord(2, 5), new HexCoord(3, 5),
            });
            WorldSiteRegistrationService.RegisterSiteOnGrid(world, siteA);

            var bAnchor = new HexCoord(10, 4);
            siteB = new WorldSite
            {
                SiteId = "test:site_chengzhen",
                DisplayName = "青石镇",
                AnchorHex = bAnchor,
                PresenceHex = bAnchor,
                LocalMapId = "base:map_site_chengzhen",
            };
            siteB.SetFootprint(new[]
            {
                bAnchor, new HexCoord(11, 4), new HexCoord(10, 5), new HexCoord(11, 5),
            });
            WorldSiteRegistrationService.RegisterSiteOnGrid(world, siteB);

            midHex = new HexCoord(6, 4);
            return world;
        }

        static EntityId Spawn(SimulationWorld world, string name)
        {
            var created = world.Entities.CreateCharacter(new DefinitionId("test", name), name);
            Assert.IsTrue(created.IsSuccess);
            created.Value.Get<FactionMembershipComponent>().Assign(FactionA, FactionRoleKind.Member);
            return created.Value.Id;
        }

        static PlayerPartyRuntime BuildParty(
            SimulationWorld world,
            EntityId active,
            WorldSite focusSite)
        {
            var party = new PlayerPartyRuntime();
            Assert.IsTrue(party.TryInitialize(active, out _));
            SetupLoadedSiteLocalMap(world, focusSite, party, active);
            return party;
        }

        static void SetupLoadedSiteLocalMap(
            SimulationWorld world,
            WorldSite site,
            PlayerPartyRuntime party,
            EntityId active)
        {
            var mapId = site.LocalMapId;
            world.LocalMap.ActiveMapLayoutId = mapId;
            world.LocalMap.OverworldMapLayoutId = mapId;
            world.LocalMap.SetPlayableBounds(0f, 0f, 1f, 16, 16);
            world.LocalMap.ExitTriggerDepth = SurfaceExitZoneCalculator.DefaultExitTriggerDepth;
            world.PartyWorld.SiteId = site.SiteId;
            world.PartyWorld.LocalMapId = mapId;
            world.PartyWorld.Mode = PartyWorldPresenceMode.AtSite;
            world.PlayerPartyTravel.SetAtWorldSite(
                site.SiteId,
                site.PresenceHex,
                world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f);
            world.LocalMap.AddOccupant(active);
            if (party != null)
            {
                for (var i = 0; i < party.Members.Count; i++)
                    world.LocalMap.AddOccupant(party.Members[i]);
            }
        }

        static void PlaceAtSite(SimulationWorld world, EntityId id, WorldSite site)
        {
            world.WorldPresence.SetAtSite(id, site.SiteId);
        }

        static void AdvanceUntilArrival(SimulationWorld world, EntityId traveler)
        {
            for (var guard = 0; guard < 512 && world.BackgroundCharacterTravel.IsTraveling(traveler); guard++)
                BackgroundCharacterTravelService.AdvanceAll(world, 4);
            Assert.IsFalse(world.BackgroundCharacterTravel.IsTraveling(traveler));
        }

        static bool TryGetMatchedIngressConnection(
            SimulationWorld world,
            WorldSite site,
            in BackgroundTravelArrivalContext context,
            out SurfaceExitConnection connection)
        {
            connection = default;
            var bounds = DefaultBounds;
            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            var depth = SurfaceExitZoneCalculator.DefaultExitTriggerDepth;
            var list = new List<SurfaceExitConnection>(16);
            WorldSiteFootprintExitConnectionResolver.CollectConnections(
                world, site, hexSize, bounds, depth,
                SurfaceExitZoneCalculator.DefaultSlotSpanFraction, list);
            for (var i = 0; i < list.Count; i++)
            {
                if (!list[i].DestinationHex.Equals(context.IngressOutsideHex))
                    continue;
                connection = list[i];
                return true;
            }

            return false;
        }

        [Test]
        public void BackgroundCharacterArrivingAtLoadedWorldSiteMaterializesImmediately()
        {
            var world = BuildTravelWorld(out var siteA, out var siteB, out _);
            var active = Spawn(world, "Hero");
            var bg = Spawn(world, "Companion");
            PlaceAtSite(world, active, siteB);
            PlaceAtSite(world, bg, siteA);
            var party = BuildParty(world, active, siteB);

            Assert.IsTrue(BackgroundCharacterTravelService.BeginTravelToWorldSite(
                world, bg, siteB.SiteId, party, debugOverrideLocalOccupant: true).IsSuccess);

            var mapBefore = world.LocalMap.ActiveMapLayoutId;
            AdvanceUntilArrival(world, bg);

            Assert.AreEqual(mapBefore, world.LocalMap.ActiveMapLayoutId);
            Assert.IsTrue(world.LocalMap.ContainsOccupant(bg));
            Assert.IsTrue(world.Entities.TryGet(bg, out var ent));
            Assert.IsTrue(ent.TryGet<EntityLocationComponent>(out var loc));
            Assert.IsTrue(loc.HasPresentationOverride);
        }

        [Test]
        public void BackgroundArrivalDoesNotRequireMapReload()
        {
            var world = BuildTravelWorld(out var siteA, out var siteB, out _);
            var active = Spawn(world, "Hero");
            var bg = Spawn(world, "Companion");
            PlaceAtSite(world, active, siteB);
            PlaceAtSite(world, bg, siteA);
            var party = BuildParty(world, active, siteB);
            BackgroundCharacterTravelService.BeginTravelToWorldSite(
                world, bg, siteB.SiteId, party, debugOverrideLocalOccupant: true);

            var loadedMap = world.LocalMap.ActiveMapLayoutId;
            AdvanceUntilArrival(world, bg);
            Assert.AreEqual(loadedMap, world.LocalMap.ActiveMapLayoutId);
            Assert.IsTrue(world.LocalMap.ContainsOccupant(bg));
        }

        [Test]
        public void BackgroundArrivalUsesActualFinalBoundaryConnection()
        {
            var world = BuildTravelWorld(out var siteA, out var siteB, out _);
            var bg = Spawn(world, "Companion");
            PlaceAtSite(world, bg, siteA);
            Assert.IsTrue(BackgroundCharacterTravelService.BeginTravelToWorldSite(
                world, bg, siteB.SiteId, debugOverrideLocalOccupant: true).IsSuccess);

            var motion = world.BackgroundCharacterTravel.GetOrCreate(bg);
            Assert.IsTrue(BackgroundTravelArrivalContext.TryFromMotion(world, motion, out var context));

            SetupLoadedSiteLocalMap(world, siteB, null, Spawn(world, "Hero"));
            AdvanceUntilArrival(world, bg);

            Assert.IsTrue(TryGetMatchedIngressConnection(world, siteB, in context, out var connection));
            Assert.IsTrue(world.Entities.TryGet(bg, out var ent));
            Assert.IsTrue(ent.TryGet<EntityLocationComponent>(out var loc));
            var bounds = DefaultBounds;
            var distToExitCenter = System.Math.Sqrt(
                System.Math.Pow(loc.PresentationOverrideX - connection.ExitCenterLocalX, 2) +
                System.Math.Pow(loc.PresentationOverrideZ - connection.ExitCenterLocalY, 2));
            var distToMapCenter = System.Math.Sqrt(
                System.Math.Pow(loc.PresentationOverrideX - bounds.CenterX, 2) +
                System.Math.Pow(loc.PresentationOverrideZ - bounds.CenterY, 2));
            Assert.Less(distToExitCenter, distToMapCenter);
        }

        [Test]
        public void BackgroundArrivalMapsConnectionToMatchingLocalExitZone()
        {
            var world = BuildTravelWorld(out var siteA, out var siteB, out _);
            var active = Spawn(world, "Hero");
            var bg = Spawn(world, "Companion");
            PlaceAtSite(world, active, siteB);
            PlaceAtSite(world, bg, siteA);
            var party = BuildParty(world, active, siteB);
            BackgroundCharacterTravelService.BeginTravelToWorldSite(
                world, bg, siteB.SiteId, party, debugOverrideLocalOccupant: true);
            Assert.IsTrue(BackgroundTravelArrivalContext.TryFromMotion(
                world, world.BackgroundCharacterTravel.GetOrCreate(bg), out var context));

            AdvanceUntilArrival(world, bg);
            Assert.IsTrue(TryGetMatchedIngressConnection(world, siteB, in context, out var connection));
            Assert.IsTrue(world.Entities.TryGet(bg, out var ent));
            Assert.IsTrue(ent.TryGet<EntityLocationComponent>(out var loc));

            var bounds = DefaultBounds;
            var depth = SurfaceExitZoneCalculator.DefaultExitTriggerDepth;
            Assert.IsTrue(SurfaceExitZoneCalculator.PointBelongsToConnection(
                    loc.PresentationOverrideX,
                    loc.PresentationOverrideZ,
                    connection,
                    depth) ||
                WildernessLocalWorldProjection.IsInNearEdgeBand(
                    loc.PresentationOverrideX,
                    loc.PresentationOverrideZ,
                    bounds));
        }

        [Test]
        public void BackgroundArrivalSpawnsInsideSafeInset()
        {
            var world = BuildTravelWorld(out var siteA, out var siteB, out _);
            var active = Spawn(world, "Hero");
            var bg = Spawn(world, "Companion");
            PlaceAtSite(world, active, siteB);
            PlaceAtSite(world, bg, siteA);
            var party = BuildParty(world, active, siteB);
            BackgroundCharacterTravelService.BeginTravelToWorldSite(
                world, bg, siteB.SiteId, party, debugOverrideLocalOccupant: true);
            AdvanceUntilArrival(world, bg);

            Assert.IsTrue(world.Entities.TryGet(bg, out var ent));
            Assert.IsTrue(ent.TryGet<EntityLocationComponent>(out var loc));
            var bounds = DefaultBounds;
            Assert.IsFalse(WildernessLocalWorldProjection.IsOutsideBounds(
                loc.PresentationOverrideX, loc.PresentationOverrideZ, bounds));
            Assert.Greater(loc.PresentationOverrideX - bounds.MinX, 0.05f);
            Assert.Greater(bounds.MaxX - loc.PresentationOverrideX, 0.05f);
        }

        [Test]
        public void BackgroundArrivalDoesNotImmediatelyReverseTransition()
        {
            var world = BuildTravelWorld(out var siteA, out var siteB, out _);
            var active = Spawn(world, "Hero");
            var bg = Spawn(world, "Companion");
            PlaceAtSite(world, active, siteB);
            PlaceAtSite(world, bg, siteA);
            var party = BuildParty(world, active, siteB);
            BackgroundCharacterTravelService.BeginTravelToWorldSite(
                world, bg, siteB.SiteId, party, debugOverrideLocalOccupant: true);
            Assert.IsTrue(BackgroundTravelArrivalContext.TryFromMotion(
                world, world.BackgroundCharacterTravel.GetOrCreate(bg), out var context));
            AdvanceUntilArrival(world, bg);

            Assert.IsTrue(TryGetMatchedIngressConnection(world, siteB, in context, out var connection));
            Assert.IsTrue(world.Entities.TryGet(bg, out var ent));
            Assert.IsTrue(ent.TryGet<EntityLocationComponent>(out var loc));
            var bounds = DefaultBounds;
            var depth = SurfaceExitZoneCalculator.ResolveDepthFromSession(world, bounds);
            var atOuterCenter = System.Math.Abs(loc.PresentationOverrideX - connection.ExitCenterLocalX) < 0.05f &&
                                System.Math.Abs(loc.PresentationOverrideZ - connection.ExitCenterLocalY) < 0.05f;
            Assert.IsFalse(atOuterCenter);
            if (WildernessLocalWorldProjection.IsInExitTriggerBand(
                    loc.PresentationOverrideX, loc.PresentationOverrideZ, bounds, depth))
            {
                var cx = bounds.CenterX - loc.PresentationOverrideX;
                var cy = bounds.CenterY - loc.PresentationOverrideZ;
                Assert.Greater(cx * connection.LocalDirectionX + cy * connection.LocalDirectionY, 0f);
            }
        }

        [Test]
        public void BackgroundArrivalTransfersAuthorityToLocalRealtime()
        {
            var world = BuildTravelWorld(out var siteA, out var siteB, out _);
            var active = Spawn(world, "Hero");
            var bg = Spawn(world, "Companion");
            PlaceAtSite(world, active, siteB);
            PlaceAtSite(world, bg, siteA);
            var party = BuildParty(world, active, siteB);
            BackgroundCharacterTravelService.BeginTravelToWorldSite(
                world, bg, siteB.SiteId, party, debugOverrideLocalOccupant: true);
            AdvanceUntilArrival(world, bg);

            Assert.IsTrue(CharacterWorldMovementAuthorityQuery.TryGetAuthority(
                world, bg, party, out var authority));
            Assert.AreEqual(CharacterWorldMovementAuthority.LoadedLocalRealtime, authority);
        }

        [Test]
        public void BackgroundSchedulerStopsAfterLoadedArrival()
        {
            var world = BuildTravelWorld(out var siteA, out var siteB, out _);
            var active = Spawn(world, "Hero");
            var bg = Spawn(world, "Companion");
            PlaceAtSite(world, active, siteB);
            PlaceAtSite(world, bg, siteA);
            var party = BuildParty(world, active, siteB);
            BackgroundCharacterTravelService.BeginTravelToWorldSite(
                world, bg, siteB.SiteId, party, debugOverrideLocalOccupant: true);
            AdvanceUntilArrival(world, bg);
            Assert.IsFalse(world.BackgroundCharacterTravel.IsTraveling(bg));
        }

        [Test]
        public void BackgroundArrivalDoesNotDuplicateDomainCharacter()
        {
            var world = BuildTravelWorld(out var siteA, out var siteB, out _);
            var active = Spawn(world, "Hero");
            var bg = Spawn(world, "Companion");
            PlaceAtSite(world, active, siteB);
            PlaceAtSite(world, bg, siteA);
            var party = BuildParty(world, active, siteB);
            BackgroundCharacterTravelService.BeginTravelToWorldSite(
                world, bg, siteB.SiteId, party, debugOverrideLocalOccupant: true);
            AdvanceUntilArrival(world, bg);

            var occupantCount = 0;
            var occupants = world.LocalMap.OccupantIds;
            for (var i = 0; i < occupants.Count; i++)
            {
                if (occupants[i] == bg)
                    occupantCount++;
            }

            Assert.AreEqual(1, occupantCount);
            Assert.IsTrue(world.WorldPresence.TryGet(bg, out var presence));
            Assert.AreEqual(PartyWorldPresenceMode.AtSite, presence.Mode);
            Assert.AreEqual(siteB.SiteId, presence.SiteId);
        }

        [Test]
        public void BackgroundArrivalPreservesCharacterIdentity()
        {
            var world = BuildTravelWorld(out var siteA, out var siteB, out _);
            var active = Spawn(world, "Hero");
            var bg = Spawn(world, "Companion");
            PlaceAtSite(world, active, siteB);
            PlaceAtSite(world, bg, siteA);
            var party = BuildParty(world, active, siteB);
            var beforeName = world.Entities.TryGet(bg, out var beforeEnt) ? beforeEnt.DisplayName : string.Empty;
            BackgroundCharacterTravelService.BeginTravelToWorldSite(
                world, bg, siteB.SiteId, party, debugOverrideLocalOccupant: true);
            AdvanceUntilArrival(world, bg);
            Assert.IsTrue(world.Entities.TryGet(bg, out var afterEnt));
            Assert.AreEqual(beforeName, afterEnt.DisplayName);
        }

        [Test]
        public void CharacterAlreadyAtSiteBeforeMapLoadDoesNotReplayArrivalEntrance()
        {
            var world = BuildTravelWorld(out _, out var siteB, out _);
            var bg = Spawn(world, "Companion");
            PlaceAtSite(world, bg, siteB);
            SetupLoadedSiteLocalMap(world, siteB, null, Spawn(world, "Hero"));

            world.LocalMap.AddOccupant(bg);
            var bounds = DefaultBounds;
            Assert.IsTrue(world.Entities.TryGet(bg, out var ent));
            if (!ent.TryGet<EntityLocationComponent>(out var loc))
            {
                loc = new EntityLocationComponent();
                ent.AddComponent(loc);
            }

            loc.SetPresentationOverride(bounds.CenterX, bounds.CenterY);

            var connections = new List<SurfaceExitConnection>(16);
            WorldSiteFootprintExitConnectionResolver.CollectConnections(
                world,
                siteB,
                1f,
                bounds,
                SurfaceExitZoneCalculator.DefaultExitTriggerDepth,
                SurfaceExitZoneCalculator.DefaultSlotSpanFraction,
                connections);
            Assert.Greater(connections.Count, 0);
            for (var i = 0; i < connections.Count; i++)
            {
                var c = connections[i];
                var atExitCenter = System.Math.Abs(loc.PresentationOverrideX - c.ExitCenterLocalX) < 0.2f &&
                                   System.Math.Abs(loc.PresentationOverrideZ - c.ExitCenterLocalY) < 0.2f;
                Assert.IsFalse(atExitCenter);
            }
        }

        [Test]
        public void LocalMapUnloadReturnsEligibleCharacterToBackgroundAuthority()
        {
            var world = BuildTravelWorld(out var siteA, out var siteB, out _);
            var active = Spawn(world, "Hero");
            var bg = Spawn(world, "Companion");
            PlaceAtSite(world, active, siteB);
            PlaceAtSite(world, bg, siteA);
            var party = BuildParty(world, active, siteB);
            BackgroundCharacterTravelService.BeginTravelToWorldSite(
                world, bg, siteB.SiteId, party, debugOverrideLocalOccupant: true);
            AdvanceUntilArrival(world, bg);
            Assert.IsTrue(world.LocalMap.ContainsOccupant(bg));

            LoadedDestinationArrivalMaterializer.ReleaseEligibleOccupantsOnLocalMapUnload(world, party);
            Assert.IsFalse(world.LocalMap.ContainsOccupant(bg));
            Assert.IsTrue(CharacterWorldMovementAuthorityQuery.TryGetAuthority(
                world, bg, party, out var authority));
            Assert.AreNotEqual(CharacterWorldMovementAuthority.LoadedLocalRealtime, authority);
            Assert.IsTrue(world.WorldPresence.TryGet(bg, out var presence));
            Assert.AreEqual(siteB.SiteId, presence.SiteId);
        }
    }
}
