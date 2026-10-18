using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Unity.Host;

namespace XianXia.Tests
{
    public sealed class ArtPresentationStateTests
    {
        [Test]
        public void BlockedMovement_ReturnsToIdle()
        {
            var state = new HostCharacterArtState();
            state.Advance(.2f, false, true, false, true, false);
            Assert.AreEqual(HostStepAArt.Action.Walk, state.Action);
            state.Advance(.2f, false, true, false, false, false);
            Assert.AreEqual(HostStepAArt.Action.Idle, state.Action);
        }

        [Test]
        public void Movement_TakesPrecedenceOverWork()
        {
            var state = new HostCharacterArtState();
            state.Advance(.2f, false, true, false, true, true);
            Assert.AreEqual(HostStepAArt.Action.Walk, state.Action);
            state.Advance(.2f, false, true, false, false, true);
            Assert.AreEqual(HostStepAArt.Action.Work, state.Action);
        }

        [Test]
        public void ActualStrike_TakesPrecedenceThenExpires()
        {
            var state = new HostCharacterArtState();
            state.Strike();
            state.Advance(.1f, false, true, false, true, true);
            Assert.AreEqual(HostStepAArt.Action.Attack, state.Action);
            state.Advance(.4f, false, true, false, true, true);
            state.Advance(.01f, false, true, false, true, true);
            Assert.AreEqual(HostStepAArt.Action.Walk, state.Action);
        }

        [Test]
        public void ManualPause_FreezesWalkingFrameAndClock()
        {
            var state = new HostCharacterArtState();
            state.Advance(.1f, false, true, false, true, false);
            state.Advance(.2f, false, true, false, true, false);
            var clock = state.Clock;
            var frame = state.Frame;
            state.Advance(30f, true, true, false, false, false);
            Assert.AreEqual(clock, state.Clock);
            Assert.AreEqual(frame, state.Frame);
            Assert.AreEqual(HostStepAArt.Action.Walk, state.Action);
        }

        [Test]
        public void ModalPause_DoesNotConsumeAttack()
        {
            var session = new PlayableHostSession { ManualPaused = false };
            var state = new HostCharacterArtState();
            state.Strike();
            state.Advance(.1f, session.IsPaused, true, false, false, false);
            session.AcquireModalPause("art-test");
            state.Advance(40f, session.IsPaused, true, false, false, false);
            Assert.AreEqual(0f, state.Clock);
            session.ReleaseModalPause("art-test");
            state.Advance(.1f, session.IsPaused, true, false, false, false);
            Assert.AreEqual(HostStepAArt.Action.Attack, state.Action);
            Assert.AreEqual(.1f, state.Clock, .0001f);
        }

        [Test]
        public void Incapacitation_CancelsAttackAndMovementEvenWhilePaused()
        {
            var state = new HostCharacterArtState();
            state.Strike();
            state.Advance(.1f, true, false, false, true, true);
            Assert.AreEqual(HostStepAArt.Action.Idle, state.Action);
            state.Advance(.1f, false, true, false, false, false);
            Assert.AreEqual(HostStepAArt.Action.Idle, state.Action);
        }

        [Test]
        public void Sleep_CancelsWorkAndAttackWithoutAVisibleBed()
        {
            var state = new HostCharacterArtState();
            state.Strike();
            state.Advance(.1f, false, true, true, true, true);
            Assert.AreEqual(HostStepAArt.Action.Idle, state.Action);
            state.Advance(.1f, false, true, false, false, false);
            Assert.AreEqual(HostStepAArt.Action.Idle, state.Action);
        }

        [Test]
        public void UnknownIdentity_DoesNotAcquireAnotherProfile()
        {
            var store = new EntityStore();
            var entity = store.CreateCharacter(new DefinitionId("base", "character_ch01_ref_herb_b")).Value;
            Assert.AreEqual(HostStepAArt.Profile.None, HostStepAArt.ResolveProfile(entity));
        }

        [Test]
        public void ProtagonistIdentity_RemainsStableAcrossActionChanges()
        {
            var entity = new EntityStore().CreateCharacter(new DefinitionId("base", "character_protagonist")).Value;
            var state = new HostCharacterArtState();
            state.Advance(.1f, false, true, false, true, true);
            state.Strike();
            state.Advance(.1f, false, true, false, true, true);
            Assert.AreEqual(HostStepAArt.Profile.Protagonist, HostStepAArt.ResolveProfile(entity));
        }
    }
}
