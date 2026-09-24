using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;
using XianXia.Data.Bootstrap;
using XianXia.Data.Content;
using XianXia.Data.Serialization;

namespace XianXia.Tests
{
    public sealed class CharacterEncounterReturnAuthorityTests
    {
        sealed class Fixture
        {
            public PlayableDayBootstrapResult Bootstrap;
            public CharacterEncounterState State;
        }

        static Fixture StartEncounter()
        {
            var path = Environment.GetEnvironmentVariable("XIANXIA_BASEGAME") ??
                       Path.GetFullPath("Content/BaseGame");
            var loaded = new ContentPackageLoader().Load(new[] { path });
            Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : "");
            var started = new PlayableDayBootstrap().Start(
                loaded.Value,
                new PlayableDayOptions { OpeningScenarioId = "base:scenario_ch01_reference" });
            Assert.IsTrue(started.IsSuccess, started.IsFailure ? started.Error.ToString() : "");
            var bootstrap = started.Value;
            var world = bootstrap.World;
            var party = new PlayerPartyRuntime();
            party.BindWorld(world);
            Assert.IsTrue(party.TryInitialize(bootstrap.CharacterIds[0], out var failure), failure);
            SquadMembershipService.EnsureSingletonsForUnassignedCharacters(world);

            Assert.IsTrue(WorldSiteCoreWarfareService.Resolve(
                world, "base:site_huangcun", out var target).IsSuccess);
            Assert.IsTrue(StrategicMilitaryAggressionService.TryCommit(
                world, world.Strategic.PlayerFactionId, target.OwnerFactionId, out failure), failure);
            var actor = world.Strategic.PlayerPartyContext.ActiveCharacterId;
            var actorPresence = world.WorldPresence.GetOrCreate(actor);
            actorPresence.Mode = PartyWorldPresenceMode.AtWorldPosition;
            actorPresence.PersonalSurfaceId = target.SurfaceId;
            actorPresence.HasContinuousWorldPosition = true;
            actorPresence.WorldPosX = target.WorldX;
            actorPresence.WorldPosY = target.WorldY;
            var defender = WorldSiteDefenseCharacterQuery.FindNearest(
                world, target, world.Strategic.PlayerFactionId);
            Assert.IsFalse(defender.IsNone);
            var prepared = CharacterEncounterService.PrepareForWorldSiteAssault(
                world, actor, defender, target.SiteId, out var state);
            Assert.IsTrue(prepared.IsSuccess, prepared.IsFailure ? prepared.Error.ToString() : "");
            Assert.IsTrue(CharacterEncounterService.Begin(world, state).IsSuccess);
            return new Fixture { Bootstrap = bootstrap, State = state };
        }

        static EncounterCharacter MoveEnemyTactically(Fixture fixture)
        {
            var participant = fixture.State.Participants.First(p => p.Enemy);
            participant.ReturnX = participant.OriginX;
            participant.ReturnY = participant.OriginY;
            participant.TacticalX = participant.ReturnX + 0.25f;
            participant.TacticalY = participant.ReturnY + 0.25f;
            Assert.IsTrue(fixture.State.Contains(participant.TacticalX, participant.TacticalY));
            CharacterEncounterService.ReconcileRuntimePresence(fixture.Bootstrap.World);
            return participant;
        }

        static void DownRemainingEnemies(Fixture fixture, ulong except)
        {
            var world = fixture.Bootstrap.World;
            foreach (var participant in fixture.State.Participants.Where(p => p.Enemy && p.CharacterId != except))
            {
                Assert.IsTrue(world.Entities.TryGet(new EntityId(participant.CharacterId), out var entity));
                Assert.IsTrue(CombatLifeStateService.TryEnterIncapacitated(world, entity));
            }
            CharacterEncounterService.Advance(world, 0.1f);
            Assert.AreEqual(CharacterEncounterPhase.ReadyToEnd, fixture.State.Phase);
        }

        [Test]
        public void IncapacitatedParticipantStaysTacticalUntilCommitThenReturnsExactly()
        {
            var fixture = StartEncounter();
            var world = fixture.Bootstrap.World;
            var participant = MoveEnemyTactically(fixture);
            var id = new EntityId(participant.CharacterId);
            Assert.IsTrue(world.Entities.TryGet(id, out var entity));

            Assert.IsTrue(CombatLifeStateService.TryEnterIncapacitated(world, entity));
            Assert.IsTrue(CharacterEncounterService.OwnsParticipantSpatialState(world, id));
            var during = world.WorldPresence.All[participant.CharacterId];
            Assert.AreEqual(PartyWorldPresenceMode.InEncounter, during.Mode);
            Assert.AreEqual(participant.TacticalX, during.WorldPosX);
            Assert.AreEqual(participant.TacticalY, during.WorldPosY);
            Assert.AreEqual(0, CharacterEncounterService.ReconcileRuntimePresence(world));

            DownRemainingEnemies(fixture, participant.CharacterId);
            Assert.IsTrue(CharacterEncounterService.CommitAndReturn(world).IsSuccess);
            var after = world.WorldPresence.All[participant.CharacterId];
            Assert.AreEqual(PartyWorldPresenceMode.AtWorldPosition, after.Mode);
            Assert.AreEqual(participant.ReturnX, after.WorldPosX);
            Assert.AreEqual(participant.ReturnY, after.WorldPosY);
            Assert.IsTrue(entity.Get<LifecycleComponent>().IsIncapacitated);
        }

        [Test]
        public void OrdinaryContinuousSurfaceIsBlockedOnlyByPreCommitEncounterPhases()
        {
            var world = new SimulationWorld();
            Assert.IsFalse(CharacterEncounterService.BlocksOrdinaryContinuousSurface(world));
            foreach (var phase in new[]
                     {
                         CharacterEncounterPhase.Preparing,
                         CharacterEncounterPhase.Active,
                         CharacterEncounterPhase.ReadyToEnd
                     })
            {
                world.Strategic.CharacterEncounter = new CharacterEncounterState { Phase = phase };
                Assert.IsTrue(CharacterEncounterService.BlocksOrdinaryContinuousSurface(world),
                    phase.ToString());
            }
            world.Strategic.CharacterEncounter = new CharacterEncounterState
            {
                Phase = CharacterEncounterPhase.Committed
            };
            Assert.IsFalse(CharacterEncounterService.BlocksOrdinaryContinuousSurface(world));
        }

        [Test]
        public void DeadParticipantStaysTacticalUntilCommitThenReturnsExactly()
        {
            var fixture = StartEncounter();
            var world = fixture.Bootstrap.World;
            var participant = MoveEnemyTactically(fixture);
            var id = new EntityId(participant.CharacterId);
            Assert.IsTrue(world.Entities.TryGet(id, out var entity));
            Assert.IsTrue(CombatLifeStateService.TryEnterIncapacitated(world, entity));
            Assert.IsTrue(CombatLifeStateService.TryConfirmDeath(
                world, EntityId.None, entity, out var confirmed));
            Assert.IsTrue(confirmed);
            var during = world.WorldPresence.All[participant.CharacterId];
            Assert.AreEqual(PartyWorldPresenceMode.InEncounter, during.Mode);
            Assert.AreEqual(participant.TacticalX, during.WorldPosX);
            Assert.AreEqual(participant.TacticalY, during.WorldPosY);

            DownRemainingEnemies(fixture, participant.CharacterId);
            Assert.IsTrue(CharacterEncounterService.CommitAndReturn(world).IsSuccess);
            var after = world.WorldPresence.All[participant.CharacterId];
            Assert.AreEqual(PartyWorldPresenceMode.AtWorldPosition, after.Mode);
            Assert.AreEqual(participant.ReturnX, after.WorldPosX);
            Assert.AreEqual(participant.ReturnY, after.WorldPosY);
            Assert.IsTrue(entity.Get<LifecycleComponent>().IsDead);
        }

        [Test]
        public void FormatFourRoundTripsReturnAndFormatThreeFallsBackToOrigin()
        {
            var state = new CharacterEncounterState
            {
                EncounterId = "encounter:return",
                SourceSurfaceId = "test:surface",
                SourceSiteId = string.Empty,
                CenterX = 10,
                CenterY = 10,
                Width = 20,
                Height = 20,
                Phase = CharacterEncounterPhase.Active,
                DecisionAt = 1,
                ArrivalDelay = 1,
                RelationThreshold = 1,
                ChanceBasisPoints = 1
            };
            state.Participants.Add(new EncounterCharacter
            {
                CharacterId = 1,
                SquadId = "squad:test",
                OriginX = 2,
                OriginY = 3,
                ReturnX = 4,
                ReturnY = 5,
                TacticalX = 8,
                TacticalY = 9
            });
            var current = CharacterEncounterJson.Read(CharacterEncounterJson.Write(state));
            Assert.AreEqual(CharacterEncounterState.Format, current.Version);
            Assert.AreEqual(4, current.Participants[0].ReturnX);
            Assert.AreEqual(5, current.Participants[0].ReturnY);
            Assert.AreEqual(8, current.Participants[0].TacticalX);
            Assert.AreEqual(9, current.Participants[0].TacticalY);

            var old = CharacterEncounterJson.Write(state);
            old.Object["version"] = JsonValue.FromNumber(3);
            foreach (var row in old.Object["participants"].Array)
            {
                row.Object.Remove("returnX");
                row.Object.Remove("returnY");
            }
            var migrated = CharacterEncounterJson.Read(old);
            Assert.AreEqual(CharacterEncounterState.Format, migrated.Version);
            Assert.AreEqual(migrated.Participants[0].OriginX, migrated.Participants[0].ReturnX);
            Assert.AreEqual(migrated.Participants[0].OriginY, migrated.Participants[0].ReturnY);
            Assert.AreEqual(8, migrated.Participants[0].TacticalX);
            Assert.AreEqual(9, migrated.Participants[0].TacticalY);
        }
    }
}
