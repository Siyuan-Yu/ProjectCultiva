using System.Collections.Generic;
using NUnit.Framework;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Persistence;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests
{
    public sealed class BackgroundCharacterWorldTravelPhase2DTests
    {
        const string FactionA = "test:faction_a";
        const float FloatTol = 0.08f;

        static SimulationWorld BuildTravelWorld(out WorldSite siteA, out WorldSite siteB, out HexCoord midHex)
        {
            var world = new SimulationWorld();
            world.HexWorld.MapId = "test:bg_travel";
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
            EntityId follower,
            WorldSite site)
        {
            world.LocalMap.ActiveMapLayoutId = site.LocalMapId;
            world.LocalMap.AddOccupant(active);
            world.LocalMap.AddOccupant(follower);
            var roster = new List<EntityId> { active, follower };
            var party = new PlayerPartyRuntime();
            Assert.IsTrue(party.TryInitialize(active, out _));
            Assert.IsTrue(party.TryAddMember(world, roster, follower, out _));
            return party;
        }

        static void PlaceAtSite(SimulationWorld world, EntityId id, WorldSite site)
        {
            world.WorldPresence.SetAtSite(id, site.SiteId);
            world.LocalMap.AddOccupant(id);
        }

        [Test]
        public void BackgroundCharacterCanStartWorldTravel()
        {
            var world = BuildTravelWorld(out var siteA, out var siteB, out _);
            var a = Spawn(world, "WangChen");
            PlaceAtSite(world, a, siteA);
            Assert.IsTrue(BackgroundCharacterTravelService.BeginTravelToWorldSite(world, a, siteB.SiteId).IsSuccess);
            Assert.IsTrue(world.BackgroundCharacterTravel.IsTraveling(a));
        }

        [Test]
        public void PlayerPartyMemberCannotUseBackgroundTravelAuthority()
        {
            var world = BuildTravelWorld(out var siteA, out var siteB, out _);
            var active = Spawn(world, "LinQing");
            var follower = Spawn(world, "WangChen");
            PlaceAtSite(world, active, siteA);
            PlaceAtSite(world, follower, siteA);
            var party = BuildParty(world, active, follower, siteA);
            Assert.IsFalse(BackgroundCharacterTravelService.BeginTravelToWorldSite(
                world, follower, siteB.SiteId, party).IsSuccess);
        }

        [Test]
        public void FormalArmyMemberCannotUseBackgroundTravelAuthority()
        {
            var world = BuildTravelWorld(out var siteA, out var siteB, out _);
            var leader = Spawn(world, "Soldier");
            PlaceAtSite(world, leader, siteA);
            var army = ArmyService.CreateArmy(world, FactionA, siteA.SiteId, new[] { leader }).Value;
            ArmyHexTravelService.InitializeArmyAtHex(world, army, siteA.PresenceHex);
            Assert.IsFalse(BackgroundCharacterTravelService.BeginTravelToWorldSite(world, leader, siteB.SiteId).IsSuccess);
        }

        [Test]
        public void LoadedRealtimeCharacterCannotAlsoUseBackgroundTravelAuthority()
        {
            var world = BuildTravelWorld(out var siteA, out var siteB, out _);
            var a = Spawn(world, "Local");
            PlaceAtSite(world, a, siteA);
            world.LocalMap.ActiveMapLayoutId = siteA.LocalMapId;
            Assert.IsFalse(BackgroundCharacterTravelService.BeginTravelToWorldSite(world, a, siteB.SiteId).IsSuccess);
        }

        [Test]
        public void BackgroundTravelUsesExistingHexWorldPathfinder()
        {
            var world = BuildTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "A");
            world.WorldPresence.SetAtHex(a, mid);
            Assert.IsTrue(BackgroundCharacterTravelService.BeginTravelToHex(
                world, a, new HexCoord(mid.Q + 2, mid.R)).IsSuccess);
            Assert.Greater(world.BackgroundCharacterTravel.GetOrCreate(a).HexPathCount, 1);
        }

        [Test]
        public void BackgroundTravelUsesWorldLocationAsPositionTruth()
        {
            var world = BuildTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "A");
            world.WorldPresence.SetAtHex(a, mid);
            BackgroundCharacterTravelService.BeginTravelToHex(world, a, new HexCoord(mid.Q + 3, mid.R));
            Assert.IsTrue(world.WorldPresence.TryGet(a, out var p));
            Assert.IsTrue(p.HasContinuousWorldPosition);
        }

        [Test]
        public void BackgroundTravelCurrentHexIsDerivedFromWorldPosition()
        {
            var world = BuildTravelWorld(out _, out _, out var mid);
            var a = Spawn(world, "A");
            world.WorldPresence.SetAtHex(a, mid);
            BackgroundCharacterTravelService.BeginTravelToHex(world, a, new HexCoord(mid.Q + 4, mid.R));
            BackgroundCharacterTravelService.AdvanceAll(world, 16);
            Assert.IsTrue(world.WorldPresence.TryGet(a, out var p) && p.HasContinuousWorldPosition);
            var derived = HexMath.WorldToHex(p.WorldPosX, p.WorldPosY, world.HexWorld.HexSize);
            Assert.AreEqual(derived.Q, p.HexQ);
            Assert.AreEqual(derived.R, p.HexR);
        }

        [Test]
        public void BackgroundTravelAdvancesUsingSimulationWorldTime()
        {
            var world = BuildTravelWorld(out _, out _, out var mid);
            var a = Spawn(world, "A");
            world.WorldPresence.SetAtHex(a, mid);
            BackgroundCharacterTravelService.BeginTravelToHex(world, a, new HexCoord(mid.Q + 2, mid.R));
            Assert.IsTrue(world.WorldPresence.TryGet(a, out var before));
            var startX = before.WorldPosX;
            BackgroundCharacterTravelService.AdvanceAll(world, 8);
            Assert.IsTrue(world.WorldPresence.TryGet(a, out var after));
            Assert.Greater(System.Math.Abs(after.WorldPosX - startX), 0.01f);
        }

        [Test]
        public void BackgroundTravelStopsWhenSimulationPaused()
        {
            var world = BuildTravelWorld(out _, out _, out var mid);
            var a = Spawn(world, "A");
            world.WorldPresence.SetAtHex(a, mid);
            BackgroundCharacterTravelService.BeginTravelToHex(world, a, new HexCoord(mid.Q + 3, mid.R));
            Assert.IsTrue(world.WorldPresence.TryGet(a, out var before));
            var x = before.WorldPosX;
            // Pause = no AdvanceAll call
            Assert.IsTrue(world.BackgroundCharacterTravel.IsTraveling(a));
            Assert.AreEqual(x, world.WorldPresence.GetOrCreate(a).WorldPosX, FloatTol);
        }

        [Test]
        public void BackgroundTravelScalesWithSimulationSpeed()
        {
            var world = BuildTravelWorld(out _, out _, out var mid);
            var a = Spawn(world, "A");
            world.WorldPresence.SetAtHex(a, mid);
            BackgroundCharacterTravelService.BeginTravelToHex(world, a, new HexCoord(mid.Q + 4, mid.R));
            BackgroundCharacterTravelService.AdvanceAll(world, 4);
            Assert.IsTrue(world.WorldPresence.TryGet(a, out var slow));
            var slowX = slow.WorldPosX;
            var world2 = BuildTravelWorld(out _, out _, out mid);
            var a2 = Spawn(world2, "A2");
            world2.WorldPresence.SetAtHex(a2, mid);
            BackgroundCharacterTravelService.BeginTravelToHex(world2, a2, new HexCoord(mid.Q + 4, mid.R));
            BackgroundCharacterTravelService.AdvanceAll(world2, 16);
            Assert.IsTrue(world2.WorldPresence.TryGet(a2, out var fast));
            Assert.Greater(System.Math.Abs(fast.WorldPosX - mid.Q), System.Math.Abs(slowX - mid.Q));
        }

        [Test]
        public void BackgroundTravelCanAdvancePartwayThroughSegment()
        {
            var world = BuildTravelWorld(out _, out _, out var mid);
            var a = Spawn(world, "A");
            world.WorldPresence.SetAtHex(a, mid);
            BackgroundCharacterTravelService.BeginTravelToHex(world, a, new HexCoord(mid.Q + 2, mid.R));
            BackgroundCharacterTravelService.AdvanceAll(world, 1);
            var motion = world.BackgroundCharacterTravel.GetOrCreate(a);
            Assert.IsTrue(motion.IsMoving);
            Assert.Greater(motion.SegmentProgress, 0f);
            Assert.Less(motion.SegmentProgress, 1f);
        }

        [Test]
        public void BackgroundTravelCanConsumeMultipleSegmentsPerTick()
        {
            var world = BuildTravelWorld(out _, out _, out var mid);
            var a = Spawn(world, "A");
            world.WorldPresence.SetAtHex(a, mid);
            BackgroundCharacterTravelService.BeginTravelToHex(world, a, new HexCoord(mid.Q + 8, mid.R));
            var motion = world.BackgroundCharacterTravel.GetOrCreate(a);
            Assert.Greater(motion.HexPathCount, 3);
            var startSeg = motion.SegmentIndex;
            BackgroundCharacterTravelService.AdvanceDistanceBudget(world, a, 5f);
            Assert.IsTrue(motion.IsMoving);
            Assert.IsTrue(motion.SegmentIndex > startSeg || motion.SegmentProgress > 0.05f);
        }

        [Test]
        public void CancelBackgroundTravelPreservesExactWorldPosition()
        {
            var world = BuildTravelWorld(out _, out _, out var mid);
            var a = Spawn(world, "A");
            world.WorldPresence.SetAtHex(a, mid);
            BackgroundCharacterTravelService.BeginTravelToHex(world, a, new HexCoord(mid.Q + 4, mid.R));
            BackgroundCharacterTravelService.AdvanceAll(world, 3);
            Assert.IsTrue(world.WorldPresence.TryGet(a, out var atCancel));
            var px = atCancel.WorldPosX;
            var py = atCancel.WorldPosY;
            BackgroundCharacterTravelService.CancelTravel(world, a);
            Assert.IsTrue(world.WorldPresence.TryGet(a, out var after));
            Assert.AreEqual(px, after.WorldPosX, FloatTol);
            Assert.AreEqual(py, after.WorldPosY, FloatTol);
            Assert.IsFalse(world.BackgroundCharacterTravel.IsTraveling(a));
        }

        [Test]
        public void BackgroundCharacterCanLeaveWorldSiteWithoutLoadingLocalMap()
        {
            var world = BuildTravelWorld(out var siteA, out var siteB, out _);
            var a = Spawn(world, "A");
            PlaceAtSite(world, a, siteA);
            world.LocalMap.ActiveMapLayoutId = string.Empty;
            Assert.IsTrue(BackgroundCharacterTravelService.BeginTravelToWorldSite(world, a, siteB.SiteId, debugOverrideLocalOccupant: true).IsSuccess);
            Assert.IsTrue(world.WorldPresence.TryGet(a, out var p));
            Assert.AreEqual(PartyWorldPresenceMode.AtWorldPosition, p.Mode);
        }

        [Test]
        public void BackgroundWorldSiteDepartureUsesFullFootprintBoundaryConnections()
        {
            var world = BuildTravelWorld(out var siteA, out var siteB, out _);
            BackgroundCharacterSiteDepartureResolver.CollectTraversableOutsideNeighbors(world, siteA, new List<HexCoord>());
            var outside = new List<HexCoord>();
            BackgroundCharacterSiteDepartureResolver.CollectTraversableOutsideNeighbors(world, siteA, outside);
            Assert.Greater(outside.Count, 1);
            Assert.IsTrue(BackgroundCharacterSiteDepartureResolver.TryResolveDepartureHex(
                world, siteA, siteB.PresenceHex, out var exitHex));
            Assert.IsFalse(siteA.OccupiesHex(exitHex));
        }

        [Test]
        public void BackgroundWorldSiteDepartureDoesNotAssumePresenceHexAsExit()
        {
            var world = BuildTravelWorld(out var siteA, out var siteB, out _);
            var a = Spawn(world, "A");
            PlaceAtSite(world, a, siteA);
            Assert.IsTrue(BackgroundCharacterTravelService.BeginTravelToWorldSite(
                world, a, siteB.SiteId, debugOverrideLocalOccupant: true).IsSuccess);
            var path = world.BackgroundCharacterTravel.GetOrCreate(a).HexPath;
            Assert.Greater(path.Count, 0);
            Assert.AreNotEqual(siteA.PresenceHex, path[0]);
        }

        [Test]
        public void BackgroundTravelCanMoveWildernessToWilderness()
        {
            var world = BuildTravelWorld(out _, out _, out var mid);
            var a = Spawn(world, "A");
            world.WorldPresence.SetAtHex(a, mid);
            var dest = new HexCoord(mid.Q + 3, mid.R);
            Assert.IsTrue(BackgroundCharacterTravelService.BeginTravelToHex(world, a, dest).IsSuccess);
            BackgroundCharacterTravelService.AdvanceAll(world, 128);
            Assert.IsTrue(BackgroundCharacterTravelService.TryResolveCharacterWorldLocation(
                world, a, out _, out _, out _, out var hex));
            Assert.AreEqual(dest, hex);
        }

        [Test]
        public void BackgroundCharacterCanArriveAtWorldSite()
        {
            var world = BuildTravelWorld(out var siteA, out var siteB, out _);
            var a = Spawn(world, "A");
            PlaceAtSite(world, a, siteA);
            Assert.IsTrue(BackgroundCharacterTravelService.BeginTravelToWorldSite(
                world, a, siteB.SiteId, debugOverrideLocalOccupant: true).IsSuccess);
            BackgroundCharacterTravelService.AdvanceAll(world, 256);
            Assert.IsTrue(world.WorldPresence.TryGet(a, out var p));
            Assert.AreEqual(PartyWorldPresenceMode.AtSite, p.Mode);
            Assert.AreEqual(siteB.SiteId, p.SiteId);
        }

        [Test]
        public void BeginTravelToHexInsideFootprintCanonicalizesToAtWorldSite()
        {
            var world = BuildTravelWorld(out var siteA, out var siteB, out var mid);
            var a = Spawn(world, "A");
            world.WorldPresence.SetAtHex(a, mid);
            Assert.IsTrue(BackgroundCharacterTravelService.BeginTravelToHex(
                world, a, siteB.PresenceHex).IsSuccess);
            BackgroundCharacterTravelService.AdvanceAll(world, 256);
            Assert.IsFalse(world.BackgroundCharacterTravel.IsTraveling(a));
            Assert.IsTrue(world.WorldPresence.TryGet(a, out var presence));
            Assert.AreEqual(PartyWorldPresenceMode.AtSite, presence.Mode);
            Assert.AreEqual(siteB.SiteId, presence.SiteId);
            Assert.IsTrue(BackgroundCharacterTravelService.TryResolveCharacterWorldLocation(
                world, a, out var kind, out var siteId, out _, out var derivedHex));
            Assert.AreEqual(BackgroundCharacterLocationKind.AtWorldSite, kind);
            Assert.AreEqual(siteB.SiteId, siteId);
            Assert.AreEqual(siteB.PresenceHex, derivedHex);
        }

        [Test]
        public void BackgroundTravelDestinationHexUsesCanonicalHexDestination()
        {
            var world = BuildTravelWorld(out _, out _, out var mid);
            var a = Spawn(world, "A");
            world.WorldPresence.SetAtHex(a, mid);
            var dest = new HexCoord(mid.Q + 2, mid.R);
            BackgroundCharacterTravelService.BeginTravelToHex(world, a, dest);
            BackgroundCharacterTravelService.AdvanceAll(world, 256);
            HexMath.ToWorldPosition(dest, world.HexWorld.HexSize, out var cx, out var cy);
            Assert.IsTrue(world.WorldPresence.TryGet(a, out var p));
            Assert.AreEqual(cx, p.WorldPosX, FloatTol);
            Assert.AreEqual(cy, p.WorldPosY, FloatTol);
        }

        [Test]
        public void JoiningPlayerPartyCancelsBackgroundTravel()
        {
            var world = BuildTravelWorld(out var siteA, out var siteB, out _);
            var active = Spawn(world, "Active");
            var bg = Spawn(world, "Bg");
            PlaceAtSite(world, active, siteA);
            world.WorldPresence.SetAtHex(bg, siteA.PresenceHex);
            BackgroundCharacterTravelService.BeginTravelToHex(world, bg, siteB.PresenceHex);
            var party = new PlayerPartyRuntime();
            party.TryInitialize(active, out _);
            world.LocalMap.ClearOccupants();
            world.LocalMap.AddOccupant(active);
            world.LocalMap.AddOccupant(bg);
            party.TryAddMember(world, new List<EntityId> { active, bg }, bg, out _);
            BackgroundCharacterTravelService.CancelTravelIfAny(world, bg);
            Assert.IsFalse(world.BackgroundCharacterTravel.IsTraveling(bg));
        }

        [Test]
        public void JoiningFormalArmyCancelsBackgroundTravel()
        {
            var world = BuildTravelWorld(out var siteA, out var siteB, out _);
            var a = Spawn(world, "A");
            PlaceAtSite(world, a, siteA);
            BackgroundCharacterTravelService.BeginTravelToHex(world, a, siteB.PresenceHex, debugOverrideLocalOccupant: true);
            BackgroundCharacterTravelService.CancelTravelIfAny(world, a);
            Assert.IsFalse(world.BackgroundCharacterTravel.IsTraveling(a));
        }

        [Test]
        public void DeadDyingOrCapturedCharacterCannotContinueBackgroundTravel()
        {
            var world = BuildTravelWorld(out _, out _, out var mid);
            var a = Spawn(world, "A");
            world.WorldPresence.SetAtHex(a, mid);
            BackgroundCharacterTravelService.BeginTravelToHex(world, a, new HexCoord(mid.Q + 3, mid.R));
            Assert.IsTrue(world.Entities.TryGet(a, out var ent));
            ent.Get<LifecycleComponent>().State = LifecycleState.Incapacitated;
            BackgroundCharacterTravelService.AdvanceAll(world, 4);
            Assert.IsFalse(world.BackgroundCharacterTravel.IsTraveling(a));
        }

        [Test]
        public void BackgroundCharacterDoesNotCreateWorldMapPersonalMarker()
        {
            var world = BuildTravelWorld(out _, out _, out var mid);
            var a = Spawn(world, "A");
            world.WorldPresence.SetAtHex(a, mid);
            BackgroundCharacterTravelService.BeginTravelToHex(world, a, new HexCoord(mid.Q + 2, mid.R));
            Assert.IsFalse(ArmyWorldMapPresentation.ShouldDrawIndependentCharacterPortrait(world, a));
        }

        [Test]
        public void BackgroundTravelSaveLoadPreservesMidSegmentWorldPosition()
        {
            var world = BuildTravelWorld(out _, out _, out var mid);
            var a = Spawn(world, "A");
            world.WorldPresence.SetAtHex(a, mid);
            BackgroundCharacterTravelService.BeginTravelToHex(world, a, new HexCoord(mid.Q + 4, mid.R));
            BackgroundCharacterTravelService.AdvanceAll(world, 5);
            Assert.IsTrue(world.WorldPresence.TryGet(a, out var before));
            var dto = StrategicSnapshotHelper.Capture(world);
            world.BackgroundCharacterTravel.Clear();
            world.WorldPresence.Clear();
            StrategicSnapshotHelper.Restore(world, dto);
            Assert.IsTrue(world.WorldPresence.TryGet(a, out var after));
            Assert.AreEqual(before.WorldPosX, after.WorldPosX, FloatTol);
            Assert.AreEqual(before.WorldPosY, after.WorldPosY, FloatTol);
            Assert.IsTrue(world.BackgroundCharacterTravel.IsTraveling(a));
        }

        [Test]
        public void BackgroundTravelSaveLoadRestoresDestinationAndContinues()
        {
            var world = BuildTravelWorld(out _, out _, out var mid);
            var a = Spawn(world, "A");
            world.WorldPresence.SetAtHex(a, mid);
            var dest = new HexCoord(mid.Q + 4, mid.R);
            BackgroundCharacterTravelService.BeginTravelToHex(world, a, dest);
            BackgroundCharacterTravelService.AdvanceAll(world, 2);
            var dto = StrategicSnapshotHelper.Capture(world);
            world.BackgroundCharacterTravel.Clear();
            StrategicSnapshotHelper.Restore(world, dto);
            Assert.IsTrue(world.BackgroundCharacterTravel.IsTraveling(a));
            BackgroundCharacterTravelService.AdvanceAll(world, 256);
            Assert.IsFalse(world.BackgroundCharacterTravel.IsTraveling(a));
            Assert.IsTrue(BackgroundCharacterTravelService.TryResolveCharacterWorldLocation(
                world, a, out _, out _, out _, out var hex));
            Assert.AreEqual(dest, hex);
        }

        [Test]
        public void BackgroundTravelDoesNotUseLegacyNodeOrRoute()
        {
            var world = BuildTravelWorld(out _, out _, out var mid);
            var a = Spawn(world, "A");
            world.WorldPresence.SetAtHex(a, mid);
            BackgroundCharacterTravelService.BeginTravelToHex(world, a, new HexCoord(mid.Q + 2, mid.R));
            var motion = world.BackgroundCharacterTravel.GetOrCreate(a);
            Assert.Greater(motion.HexPathCount, 0);
            Assert.IsTrue(motion.IsMoving);
        }

        static EntityId SpawnWithBucketZero(SimulationWorld world, HexCoord at, HexCoord dest)
        {
            EntityId id = EntityId.None;
            for (var i = 0; i < 16; i++)
                id = Spawn(world, "bucket0_" + i);
            Assert.AreEqual(0, BackgroundSimulationScheduler.ResolveTravelBucket(id));
            world.WorldPresence.SetAtHex(id, at);
            BackgroundCharacterTravelService.BeginTravelToHex(world, id, dest);
            return id;
        }

        [Test]
        public void BackgroundSimulationSchedulerUsesElapsedWorldTimeNotUpdateFrequency()
        {
            var world = BuildTravelWorld(out _, out _, out var mid);
            var dest = new HexCoord(mid.Q + 6, mid.R);
            var id = SpawnWithBucketZero(world, mid, dest);
            var motion = world.BackgroundCharacterTravel.GetOrCreate(id);
            motion.LastProcessedWorldTick = 0;
            world.Tick = new WorldTick(16);

            BackgroundSimulationScheduler.AfterSimulationTick(world, 1);

            Assert.IsTrue(world.WorldPresence.TryGet(id, out var staggered));
            var staggeredX = staggered.WorldPosX;

            var worldBatch = BuildTravelWorld(out _, out _, out mid);
            var idBatch = SpawnWithBucketZero(worldBatch, mid, dest);
            BackgroundSimulationScheduler.AdvanceTravelBatch(worldBatch, 16);
            Assert.IsTrue(worldBatch.WorldPresence.TryGet(idBatch, out var batched));

            Assert.AreEqual(batched.WorldPosX, staggeredX, FloatTol);
            Assert.AreEqual(motion.LastProcessedWorldTick, 16UL);
        }

        [Test]
        public void BackgroundSimulationFiveHundredCharacterBatchBenchmark()
        {
            var world = BuildTravelWorld(out _, out _, out var mid);
            var dest = new HexCoord(mid.Q + 10, mid.R);
            world.LocalMap.ClearOccupants();

            var traveling = 0;
            for (var i = 0; i < 500; i++)
            {
                var id = Spawn(world, "bg500_" + i);
                world.WorldPresence.SetAtHex(id, mid);
                if (i % 2 == 0)
                {
                    Assert.IsTrue(
                        BackgroundCharacterTravelService.BeginTravelToHex(world, id, dest).IsSuccess);
                    traveling++;
                }
            }

            Assert.AreEqual(250, traveling);
            Assert.AreEqual(0, world.LocalMap.OccupantIds.Count);

            for (ulong t = 1; t <= 32; t++)
            {
                world.Tick = new WorldTick(t);
                BackgroundSimulationScheduler.AfterSimulationTick(world, 1);
            }

            var moved = 0;
            foreach (var kv in world.BackgroundCharacterTravel.All)
            {
                if (kv.Value == null || !kv.Value.IsMoving)
                    continue;
                var cid = new EntityId(kv.Key);
                if (!world.WorldPresence.TryGet(cid, out var p) || !p.HasContinuousWorldPosition)
                    continue;
                if (System.Math.Abs(p.WorldPosX - mid.Q) > 0.05f)
                    moved++;
            }

            Assert.Greater(moved, 50);
            Assert.AreEqual(0, world.LocalMap.OccupantIds.Count);
        }
    }
}
