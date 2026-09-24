using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using XianXia.Core.Combat;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Persistence;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;
using XianXia.Core.World.Surface;
using XianXia.Data.Serialization;
using XianXia.Unity.Host;

namespace XianXia.Tests
{
    public sealed class ControlHandoff01Tests
    {
        const string PlayerFaction = "test:player";
        const string SurfaceId = "test:surface";

        [Test]
        public void EmergencyHandoffKeepsLivingOldPartyTogetherAtExactPosition()
        {
            var f = Fixture(withCandidate: true);
            Incapacitate(f.World, f.A);
            Incapacitate(f.World, f.B);
            f.Party.RefreshActiveAfterLifeState(f.World);
            f.World.Entities.TryGet(f.A, out var oldA);
            var caveLocation = new EntityLocationComponent { LocationId = "test:cave" };
            oldA.AddComponent(caveLocation);
            f.World.LocalMap.EstablishSeparateSpace(
                "test:cave-map", "test:places", SeparateSpaceKind.Cave,
                "test:entry", "test:return", "handoff-test");
            f.World.LocalMap.SetOccupants(new[] { f.A, f.B });

            var result = PlayerFactionControlHandoffService.TryResolve(f.World, f.Party);

            Assert.IsTrue(result.IsResolved, result.Error);
            Assert.AreEqual(PlayerFactionControlHandoffKind.EmergencyTakeover, result.Kind);
            Assert.AreEqual(f.C, f.Party.ActiveCharacterId);
            Assert.AreEqual(1, f.Party.Count);
            Assert.AreEqual(new WorldVec2(80f, 70f), f.World.PlayerPartyTravel.WorldPosition);
            Assert.IsTrue(f.World.Strategic.Squads.TryGet(result.RecoverySquadId, out var recovery));
            Assert.AreEqual(SquadCommandKind.None, recovery.CommandKind);
            Assert.IsTrue(recovery.Contains(f.A));
            Assert.IsTrue(recovery.Contains(f.B));
            Assert.AreEqual(PlayerFaction, recovery.FactionId);
            Assert.AreEqual(LifecycleState.Incapacitated, Life(f.World, f.A).State);
            Assert.AreEqual(LifecycleState.Incapacitated, Life(f.World, f.B).State);
            Assert.IsFalse(f.World.LocalMap.IsActive);
            Assert.AreEqual("test:cave", caveLocation.LocationId,
                "External handoff must not perform a normal Separate Space leave.");
            AssertPresence(f.World, f.A, new WorldVec2(10f, 10f));
            AssertPresence(f.World, f.B, new WorldVec2(11f, 10f));

            Assert.IsTrue(f.World.Entities.TryGet(f.A, out var recovered));
            Assert.IsTrue(CombatLifeStateService.TryRecoverFromIncapacitated(
                f.World, recovered, 1));
            f.Party.RefreshActiveAfterLifeState(f.World);
            Assert.AreEqual(f.C, f.Party.ActiveCharacterId,
                "Recovered former members must not reclaim control implicitly.");
        }

        [Test]
        public void ActiveMemberOrNoCandidateDoesNotReplaceOldParty()
        {
            var active = Fixture(withCandidate: true);
            Incapacitate(active.World, active.A);
            active.Party.RefreshActiveAfterLifeState(active.World);
            Assert.AreEqual(active.B, active.Party.ActiveCharacterId);
            Assert.AreEqual(PlayerFactionControlHandoffStatus.NotRequired,
                PlayerFactionControlHandoffService.TryResolve(active.World, active.Party).Status);

            var none = Fixture(withCandidate: false);
            Incapacitate(none.World, none.A);
            Incapacitate(none.World, none.B);
            none.Party.RefreshActiveAfterLifeState(none.World);
            var oldSquadId = none.Party.ControlledSquadId;
            var result = PlayerFactionControlHandoffService.TryResolve(none.World, none.Party);
            Assert.AreEqual(PlayerFactionControlHandoffStatus.NoEligibleCandidate, result.Status);
            Assert.AreEqual(PlayerPartyControlState.TemporarilyUnavailable, none.Party.ControlState);
            Assert.AreEqual(oldSquadId, none.Party.ControlledSquadId);
            Assert.IsTrue(none.Party.IsMember(none.A));
            Assert.IsTrue(none.Party.IsMember(none.B));
        }

        [Test]
        public void DeadMembersUseSingletonWhileLivingMembersUseRecoverySquad()
        {
            var f = Fixture(withCandidate: true);
            Incapacitate(f.World, f.A);
            Kill(f.World, f.B);
            f.Party.RefreshActiveAfterLifeState(f.World);

            var result = PlayerFactionControlHandoffService.TryResolve(f.World, f.Party);

            Assert.IsTrue(result.IsResolved, result.Error);
            Assert.IsTrue(f.World.Strategic.Squads.TryGet(result.RecoverySquadId, out var recovery));
            Assert.IsTrue(recovery.Contains(f.A));
            Assert.IsFalse(recovery.Contains(f.B));
            Assert.IsTrue(f.World.Strategic.Squads.TryGet(
                SquadMembershipService.SingletonSquadId(f.B), out var corpse));
            Assert.IsTrue(corpse.Contains(f.B));
        }

        [Test]
        public void GenuineWipeUsesSuccessionAndEncounterDefersBothModes()
        {
            var emergency = Fixture(withCandidate: true);
            Incapacitate(emergency.World, emergency.A);
            Incapacitate(emergency.World, emergency.B);
            emergency.Party.RefreshActiveAfterLifeState(emergency.World);
            emergency.World.Strategic.CharacterEncounter = new CharacterEncounterState
            {
                EncounterId = "test:active",
                Phase = CharacterEncounterPhase.Active
            };
            Assert.AreEqual(PlayerFactionControlHandoffStatus.DeferredByEncounter,
                PlayerFactionControlHandoffService.TryResolve(
                    emergency.World, emergency.Party).Status);

            var succession = Fixture(withCandidate: true);
            Kill(succession.World, succession.A);
            Kill(succession.World, succession.B);
            succession.Party.RefreshActiveAfterLifeState(succession.World);
            succession.World.Strategic.CharacterEncounter = new CharacterEncounterState
            {
                EncounterId = "test:wipe-active",
                Phase = CharacterEncounterPhase.ReadyToEnd
            };
            Assert.AreEqual(PlayerFactionControlHandoffStatus.DeferredByEncounter,
                PlayerFactionControlHandoffService.TryResolve(
                    succession.World, succession.Party).Status);
            succession.World.Strategic.CharacterEncounter = null;
            var result = PlayerFactionControlHandoffService.TryResolve(
                succession.World, succession.Party);
            Assert.IsTrue(result.IsResolved, result.Error);
            Assert.AreEqual(PlayerFactionControlHandoffKind.Succession, result.Kind);
            Assert.IsEmpty(result.RecoverySquadId);
            Assert.IsTrue(succession.World.Strategic.Squads.TryGet(
                SquadMembershipService.SingletonSquadId(succession.A), out _));
            Assert.IsTrue(succession.World.Strategic.Squads.TryGet(
                SquadMembershipService.SingletonSquadId(succession.B), out _));
            Assert.AreEqual(1, succession.World.Strategic.Squads.Squads.Count(pair =>
                pair.Key == SquadMembershipService.SingletonSquadId(succession.A)));
            Assert.AreEqual(1, succession.World.Strategic.Squads.Squads.Count(pair =>
                pair.Key == SquadMembershipService.SingletonSquadId(succession.B)));
            Assert.IsTrue(succession.World.Strategic.Squads.TryGetForCharacter(
                succession.C, out var successorSquad));
            Assert.AreEqual(SquadMembershipService.PlayerSquadId, successorSquad.SquadId);
        }

        [Test]
        public void IndependentFieldReturnSkipsOldSurfaceOnlyWhileExternalHandoffIsPending()
        {
            var f = Fixture(withCandidate: true);
            Assert.IsTrue(
                ContinuousOutdoorSurfaceRuntime.ShouldRestoreOrdinarySurfaceAfterIndependentField(
                    f.Party));

            Incapacitate(f.World, f.A);
            Incapacitate(f.World, f.B);
            f.Party.RefreshActiveAfterLifeState(f.World);

            Assert.IsTrue(f.Party.NeedsExternalControlHandoff);
            Assert.IsFalse(
                ContinuousOutdoorSurfaceRuntime.ShouldRestoreOrdinarySurfaceAfterIndependentField(
                    f.Party));
        }

        [Test]
        public void CameraHandoffWaitsForDestinationMaterializationAndSuccessorView()
        {
            var successor = new EntityId(42);
            var position = new WorldVec2(80f, 70f);
            Assert.IsTrue(HostPlayerPartyController.ExternalHandoffPresentationPostconditionsMet(
                successor, successor, SurfaceId, SurfaceId, position, position,
                surfaceActive: true,
                motionHasPosition: true,
                motionUsesExpectedSurface: true,
                positionLoaded: true,
                neighborhoodReconciled: true,
                successorMaterialized: true,
                successorViewExists: true,
                surfaceInvariant: true));
            Assert.IsFalse(HostPlayerPartyController.ExternalHandoffPresentationPostconditionsMet(
                successor, successor, SurfaceId, SurfaceId, position, position,
                surfaceActive: true,
                motionHasPosition: true,
                motionUsesExpectedSurface: true,
                positionLoaded: true,
                neighborhoodReconciled: true,
                successorMaterialized: true,
                successorViewExists: false,
                surfaceInvariant: true));
        }

        [Test]
        public void FarDestinationRequiresHardReanchorWhileLoadedDestinationDoesNot()
        {
            Assert.IsTrue(HostPlayerPartyController.RequiresExternalHandoffHardReanchor(
                surfaceActive: true, destinationInLoadedNeighborhood: false));
            Assert.IsFalse(HostPlayerPartyController.RequiresExternalHandoffHardReanchor(
                surfaceActive: true, destinationInLoadedNeighborhood: true));
            Assert.IsFalse(HostPlayerPartyController.RequiresExternalHandoffHardReanchor(
                surfaceActive: false, destinationInLoadedNeighborhood: false));
        }

        [Test]
        public void StrongestCandidateDetachesAloneFromNpcSquadAtCapturedExactPosition()
        {
            var f = Fixture(withCandidate: true);
            var weaker = Spawn(f.World, "D", new WorldVec2(60f, 55f));
            var guard = Spawn(f.World, "Guard", new WorldVec2(81f, 70f));
            Assert.IsTrue(f.World.Entities.TryGet(f.C, out var strongest));
            strongest.Get<CultivationComponent>().Realm = RealmStage.Foundation;
            Assert.IsTrue(f.World.Entities.TryGet(weaker, out var weakerEntity));
            weakerEntity.Get<CultivationComponent>().Realm = RealmStage.QiRefining;
            var source = SquadMembershipService.Create(
                f.World, "squad:test:candidate", new[] { f.C, guard }, f.C,
                SquadCommandKind.FollowLeader, factionId: PlayerFaction);
            Assert.IsTrue(source.IsSuccess, source.IsFailure ? source.Error.ToString() : string.Empty);
            Incapacitate(f.World, f.A);
            Incapacitate(f.World, f.B);
            f.Party.RefreshActiveAfterLifeState(f.World);

            var result = PlayerFactionControlHandoffService.TryResolve(f.World, f.Party);

            Assert.IsTrue(result.IsResolved, result.Error);
            Assert.AreEqual(f.C, result.SuccessorId);
            Assert.AreEqual(new WorldVec2(80f, 70f), result.WorldPosition);
            Assert.AreEqual(result.WorldPosition, f.World.PlayerPartyTravel.WorldPosition);
            Assert.IsTrue(f.World.Strategic.Squads.TryGet("squad:test:candidate", out var remaining));
            Assert.AreEqual(1, remaining.MemberCharacterIds.Count);
            Assert.IsTrue(remaining.Contains(guard));
            Assert.AreEqual(guard, remaining.LeaderCharacterId);
            Assert.IsFalse(remaining.Contains(f.C));
            Assert.AreEqual(f.C, f.Party.ActiveCharacterId);
            Assert.AreEqual(1, f.Party.Count);
        }

        [Test]
        public void EqualPowerCandidatesUseStableLowestEntityIdTieBreak()
        {
            var f = Fixture(withCandidate: true);
            var later = Spawn(f.World, "D", new WorldVec2(60f, 55f));
            Assert.Less(f.C.Value, later.Value);
            Incapacitate(f.World, f.A);
            Incapacitate(f.World, f.B);
            f.Party.RefreshActiveAfterLifeState(f.World);

            var result = PlayerFactionControlHandoffService.TryResolve(f.World, f.Party);

            Assert.IsTrue(result.IsResolved, result.Error);
            Assert.AreEqual(f.C, result.SuccessorId);
        }

        [Test]
        public void ExistingV8SquadSnapshotRoundTripPreservesEmergencyHandoff()
        {
            var source = Fixture(withCandidate: true);
            Incapacitate(source.World, source.A);
            Incapacitate(source.World, source.B);
            source.Party.RefreshActiveAfterLifeState(source.World);
            var handoff = PlayerFactionControlHandoffService.TryResolve(
                source.World, source.Party);
            Assert.IsTrue(handoff.IsResolved, handoff.Error);
            var dto = StrategicSnapshotHelper.Capture(source.World, source.Party);
            var serializer = new JsonSnapshotSerializer();
            var json = serializer.Serialize(new WorldSnapshot { Strategic = dto });
            Assert.IsTrue(json.IsSuccess, json.IsFailure ? json.Error.ToString() : string.Empty);
            var parsed = serializer.Deserialize(json.Value);
            Assert.IsTrue(parsed.IsSuccess, parsed.IsFailure ? parsed.Error.ToString() : string.Empty);
            dto = parsed.Value.Strategic;

            var restoredWorld = CreateWorld();
            var a = Spawn(restoredWorld, "A", new WorldVec2(10f, 10f));
            var b = Spawn(restoredWorld, "B", new WorldVec2(11f, 10f));
            var c = Spawn(restoredWorld, "C", new WorldVec2(80f, 70f));
            Assert.AreEqual(source.A, a);
            Assert.AreEqual(source.B, b);
            Assert.AreEqual(source.C, c);
            Incapacitate(restoredWorld, a);
            Incapacitate(restoredWorld, b);

            var restore = StrategicSnapshotHelper.Restore(restoredWorld, dto);
            Assert.IsTrue(restore.IsSuccess, restore.IsFailure ? restore.Error.ToString() : string.Empty);
            var restoredParty = new PlayerPartyRuntime();
            PlayerPartySnapshotRestore.Apply(
                restoredWorld, restoredParty, dto.PlayerParty, dto.ControlledSquadId);

            Assert.AreEqual(c, restoredParty.ActiveCharacterId);
            Assert.AreEqual(1, restoredParty.Count);
            Assert.IsTrue(restoredWorld.Strategic.Squads.TryGet(
                handoff.RecoverySquadId, out var recovery));
            Assert.IsTrue(recovery.Contains(a));
            Assert.IsTrue(recovery.Contains(b));
            Assert.AreEqual(SquadCommandKind.None, recovery.CommandKind);
            Assert.AreEqual(WorldSnapshot.CurrentSchemaVersion, 8);
        }

        static TestFixture Fixture(bool withCandidate)
        {
            var world = CreateWorld();
            var a = Spawn(world, "A", new WorldVec2(10f, 10f));
            var b = Spawn(world, "B", new WorldVec2(11f, 10f));
            var c = withCandidate
                ? Spawn(world, "C", new WorldVec2(80f, 70f))
                : EntityId.None;
            var created = SquadMembershipService.Create(
                world, SquadMembershipService.PlayerSquadId,
                new[] { a, b }, a, SquadCommandKind.FollowLeader,
                factionId: PlayerFaction);
            Assert.IsTrue(created.IsSuccess, created.IsFailure ? created.Error.ToString() : string.Empty);
            var party = new PlayerPartyRuntime();
            party.BindWorld(world);
            Assert.IsTrue(party.TryBindControlledSquad(
                SquadMembershipService.PlayerSquadId, a, out var error), error);
            world.PlayerPartyTravel.SetAtSurfacePosition(SurfaceId, new WorldVec2(10f, 10f));
            return new TestFixture(world, party, a, b, c);
        }

        static SimulationWorld CreateWorld()
        {
            var world = new SimulationWorld();
            world.Strategic.PlayerFactionId = PlayerFaction;
            var cells = new List<SurfaceGroundCellKind>(100 * 100);
            for (var i = 0; i < 100 * 100; i++)
                cells.Add(SurfaceGroundCellKind.Ground);
            world.SurfaceGround.Register(new SurfaceGroundNavigation(
                SurfaceId, "test", "test", 0f, 0f, 1f, 100, 100, cells));
            return world;
        }

        static EntityId Spawn(SimulationWorld world, string name, WorldVec2 position)
        {
            var created = world.Entities.CreateCharacter(
                new DefinitionId("test", name.ToLowerInvariant()), name);
            Assert.IsTrue(created.IsSuccess, created.IsFailure ? created.Error.ToString() : string.Empty);
            created.Value.Get<FactionMembershipComponent>().Assign(
                PlayerFaction, FactionRoleKind.Member);
            world.WorldPresence.SetAtWorldPosition(created.Value.Id, position, SurfaceId);
            return created.Value.Id;
        }

        static void Incapacitate(SimulationWorld world, EntityId id)
        {
            Assert.IsTrue(world.Entities.TryGet(id, out var entity));
            Assert.IsTrue(CombatLifeStateService.TryEnterIncapacitated(world, entity));
        }

        static void Kill(SimulationWorld world, EntityId id)
        {
            Incapacitate(world, id);
            Assert.IsTrue(world.Entities.TryGet(id, out var entity));
            Assert.IsTrue(CombatLifeStateService.TryConfirmDeath(
                world, EntityId.None, entity, out var confirmed));
            Assert.IsTrue(confirmed);
        }

        static LifecycleComponent Life(SimulationWorld world, EntityId id)
        {
            Assert.IsTrue(world.Entities.TryGet(id, out var entity));
            return entity.Get<LifecycleComponent>();
        }

        static void AssertPresence(SimulationWorld world, EntityId id, WorldVec2 expected)
        {
            Assert.IsTrue(world.WorldPresence.TryGet(id, out var presence));
            Assert.AreEqual(expected.X, presence.WorldPosX, 0.0001f);
            Assert.AreEqual(expected.Y, presence.WorldPosY, 0.0001f);
        }

        readonly struct TestFixture
        {
            public TestFixture(
                SimulationWorld world, PlayerPartyRuntime party,
                EntityId a, EntityId b, EntityId c)
            {
                World = world;
                Party = party;
                A = a;
                B = b;
                C = c;
            }

            public SimulationWorld World { get; }
            public PlayerPartyRuntime Party { get; }
            public EntityId A { get; }
            public EntityId B { get; }
            public EntityId C { get; }
        }
    }
}
