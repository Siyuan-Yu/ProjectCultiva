using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;
using XianXia.Unity.Host;

namespace XianXia.Tests
{
    public sealed class CharacterEncounterStartGateTests
    {
        static CharacterEncounterState ActiveState(string id = "encounter:test")
        {
            var state = new CharacterEncounterState
            {
                EncounterId = id,
                Phase = CharacterEncounterPhase.Active
            };
            state.Participants.Add(new EncounterCharacter { CharacterId = 1, Enemy = false });
            state.Participants.Add(new EncounterCharacter { CharacterId = 2, Enemy = true });
            return state;
        }

        [Test]
        public void MaterializedFieldWaitsWithNoTargetAndConsumesStartActionOnce()
        {
            var world = new SimulationWorld();
            var state = ActiveState();
            var invoked = 0;
            var gate = new CharacterEncounterStartGate();
            gate.Stage(world, state.EncounterId, new EntityId(1), new EntityId(2),
                () => invoked++, isRestore: false, recordCharacterAttack: true);

            Assert.AreEqual(0UL, state.Find(1).TargetId, "ReadyToStart must not assign tactical target.");
            Assert.IsTrue(gate.ValidateStart(world, state, state.EncounterId,
                pauseOwned: true, inputLocked: true).IsSuccess);
            var action = gate.TakeStartAction();
            Assert.NotNull(action);
            action();
            Assert.AreEqual(1, invoked);
            Assert.IsNull(gate.TakeStartAction(), "Start callback must be one-shot.");
        }

        [Test]
        public void StartRequiresSameWorldActiveDomainFieldPauseAndInputLock()
        {
            var world = new SimulationWorld();
            var state = ActiveState();
            var gate = new CharacterEncounterStartGate();
            gate.Stage(world, state.EncounterId, EntityId.None, EntityId.None, null,
                isRestore: true, recordCharacterAttack: false);

            Assert.IsTrue(gate.ValidateStart(new SimulationWorld(), state, state.EncounterId, true, true).IsFailure);
            Assert.IsTrue(gate.ValidateStart(world, null, state.EncounterId, true, true).IsFailure);
            state.Phase = CharacterEncounterPhase.ReadyToEnd;
            Assert.IsTrue(gate.ValidateStart(world, state, state.EncounterId, true, true).IsFailure);
            state.Phase = CharacterEncounterPhase.Active;
            Assert.IsTrue(gate.ValidateStart(world, state, "encounter:other", true, true).IsFailure);
            Assert.IsTrue(gate.ValidateStart(world, state, state.EncounterId, false, true).IsFailure);
            Assert.IsTrue(gate.ValidateStart(world, state, state.EncounterId, true, false).IsFailure);
        }

        [Test]
        public void ClearDropsRestoredObjectiveCallbackAndIdentity()
        {
            var gate = new CharacterEncounterStartGate();
            gate.Stage(new SimulationWorld(), "encounter:restore", EntityId.None, EntityId.None,
                () => { }, isRestore: true, recordCharacterAttack: false);
            gate.Clear();

            Assert.IsFalse(gate.IsStaged);
            Assert.IsFalse(gate.IsRestore);
            Assert.IsTrue(gate.Attacker.IsNone);
            Assert.IsTrue(gate.Target.IsNone);
            Assert.IsNull(gate.TakeStartAction());
        }

        [Test]
        public void CharacterEncounterPauseOwnerRemainsUntilExplicitRelease()
        {
            var session = new PlayableHostSession { ManualPaused = true };
            session.AcquireModalPause("CharacterEncounterUI");
            session.AcquireModalPause("CharacterEncounterUI");

            Assert.IsTrue(session.HasModalPauseOwner("CharacterEncounterUI"));
            Assert.IsTrue(session.ModalHardPaused);
            Assert.IsTrue(session.IsPaused);
            session.ManualPaused = false;
            Assert.IsTrue(session.ModalHardPaused, "Start gate owner must dominate manual pause.");
            Assert.IsTrue(session.IsPaused);
            session.ReleaseModalPause("CharacterEncounterUI");
            Assert.IsFalse(session.HasModalPauseOwner("CharacterEncounterUI"));
            Assert.IsFalse(session.ModalHardPaused);
            Assert.IsFalse(session.IsPaused);
            session.ReleaseModalPause("CharacterEncounterUI");
            Assert.IsFalse(session.ModalHardPaused, "Repeated start/release must have no effect.");
        }

        [Test]
        public void ReadyToStartNeverAdvancesTacticalPresentation()
        {
            Assert.IsFalse(HostCharacterEncounter.CanAdvanceTacticalPresentation(
                HostCharacterEncounter.PresentationPhase.ReadyToStart, isPaused: true));
            Assert.IsFalse(HostCharacterEncounter.CanAdvanceTacticalPresentation(
                HostCharacterEncounter.PresentationPhase.ReadyToStart, isPaused: false));
            Assert.IsTrue(HostCharacterEncounter.CanAdvanceTacticalPresentation(
                HostCharacterEncounter.PresentationPhase.Active, isPaused: false));
        }
    }
}
