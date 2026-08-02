using NUnit.Framework;
using UnityEngine;
using XianXia.Core.Content;
using XianXia.Unity.Host;

namespace XianXia.Tests
{
    /// <summary>RTS：首次走进区域应自动勘察，避免「只移动不勘察、任务永远不完成」。</summary>
    public sealed class HostRtsFirstVisitSurveyTests
    {
        [Test]
        public void FirstVisit_AutoExplores_AndSecondVisitDoesNotRepeatStock()
        {
            var host = new GameObject("RtsSurveyHost");
            try
            {
                var bootstrap = host.AddComponent<PlayableHostBootstrap>();
                bootstrap.ConfigureOpeningScenario("base:scenario_ch01_reference");
                host.AddComponent<EntityViewSpawner>();
                host.AddComponent<HostSelectionController>();
                host.AddComponent<HostCommandBridge>();
                host.AddComponent<HostMapGraybox>();
                Assert.IsTrue(bootstrap.TryInitialize(), bootstrap.StatusLine);

                var session = bootstrap.Session;
                var subject = session.CharacterIds[0];
                Assert.IsFalse(session.World.Flags.Has(
                    ContentConditionEvaluator.ExploredFlag("base:loc_ref_forest")));

                session.World.Settlements.TryGetPrimary(out var settlement);
                var woodBefore = settlement.GetStock("base:resource_rough_wood");

                HostMoveController.ApplyPresentationArrival(
                    session, subject, "base:loc_ref_forest", bootstrap);

                Assert.IsTrue(session.World.Flags.Has(
                    ContentConditionEvaluator.ExploredFlag("base:loc_ref_forest")));
                Assert.Greater(settlement.GetStock("base:resource_rough_wood"), woodBefore);

                var woodAfterFirst = settlement.GetStock("base:resource_rough_wood");
                HostMoveController.ApplyPresentationArrival(
                    session, subject, "base:loc_ref_labor_yard", bootstrap);
                HostMoveController.ApplyPresentationArrival(
                    session, subject, "base:loc_ref_forest", bootstrap);
                Assert.AreEqual(woodAfterFirst, settlement.GetStock("base:resource_rough_wood"));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RtsPath_WalkForestAfterSense_PresentsWoodcutter()
        {
            var host = new GameObject("RtsElderHost");
            try
            {
                var bootstrap = host.AddComponent<PlayableHostBootstrap>();
                bootstrap.ConfigureOpeningScenario("base:scenario_ch01_reference");
                host.AddComponent<EntityViewSpawner>();
                host.AddComponent<HostSelectionController>();
                host.AddComponent<HostCommandBridge>();
                host.AddComponent<HostMapGraybox>();
                Assert.IsTrue(bootstrap.TryInitialize(), bootstrap.StatusLine);

                var session = bootstrap.Session;
                var subject = session.CharacterIds[0];
                while (session.World.ContentEvents.HasActive)
                {
                    Assert.IsTrue(session.World.ContentEvents.TryGet(
                        session.World.ContentEvents.ActiveEventId, out var open));
                    Assert.IsTrue(bootstrap.CommandBridge.ResolveContentChoice(open.Choices[0].Id));
                }

                StoryFlagService.Set(session.World, "quest:ch01_ref_sense_done", subject);
                HostMoveController.ApplyPresentationArrival(
                    session, subject, "base:loc_ref_forest", bootstrap);

                Assert.IsTrue(session.World.ContentEvents.HasActive);
                Assert.AreEqual("base:event_ch01_ref_woodcutter", session.World.ContentEvents.ActiveEventId);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
