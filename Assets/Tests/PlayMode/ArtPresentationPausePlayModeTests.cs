using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Unity.Host;

namespace XianXia.Tests.PlayMode
{
    public sealed class ArtPresentationPausePlayModeTests
    {
        [UnityTest]
        public IEnumerator ActualEntityView_ManualAndModalPauseFreezeAttackClock()
        {
            var root = new GameObject("ArtPauseTest");
            try
            {
                root.AddComponent<SpriteRenderer>();
                var world = new SimulationWorld();
                var entity = world.Entities.CreateCharacter(new DefinitionId("base", "character_protagonist")).Value;
                var session = new PlayableHostSession { ManualPaused = false };
                var view = root.AddComponent<EntityView>();
                Assert.IsTrue(view.Bind(world, entity.Id));
                view.ConfigureRuntimeArt(HostStepAArt.Profile.Protagonist, null, null, null, session);
                view.PlayArtAttack(Vector3.right);
                yield return null;
                session.ManualPaused = true;
                yield return null;
                var state = (HostCharacterArtState)typeof(EntityView).GetField("_artState",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                var clock = state.Clock;
                yield return null;
                yield return null;
                Assert.AreEqual(clock, state.Clock);
                session.ManualPaused = false;
                session.AcquireModalPause("art-test");
                yield return null;
                Assert.AreEqual(clock, state.Clock);
                session.ReleaseModalPause("art-test");
                yield return null;
                yield return null;
                Assert.Greater(state.Clock, clock);
            }
            finally { Object.Destroy(root); }
        }
    }
}
