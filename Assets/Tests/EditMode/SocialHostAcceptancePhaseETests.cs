using NUnit.Framework;
using UnityEngine;
using XianXia.Core.Input;
using XianXia.Core.Persistence;
using XianXia.Core.Social;
using XianXia.Unity.Host;
using CoreEventType = XianXia.Core.Events.EventType;

namespace XianXia.Tests
{
    /// <summary>
    /// VS0.6 Phase E: discover → read personality → Help → Recruit closed loop on Host path.
    /// </summary>
    public sealed class SocialHostAcceptancePhaseETests
    {
        [Test]
        public void PlayableSocialHost_Discover_Interact_Recruit_Loop()
        {
            var host = CreateHost(out var bootstrap);
            try
            {
                Assert.AreEqual(3, bootstrap.Session.CharacterIds.Count);
                Assert.GreaterOrEqual(bootstrap.ViewSpawner.SpawnedCount, 4);
                Assert.IsFalse(bootstrap.Session.RecruitableNpcId.IsNone);

                var actor = bootstrap.Session.CharacterIds[0];
                var npc = bootstrap.Session.RecruitableNpcId;

                Assert.IsTrue(bootstrap.SelectionController.SelectEntity(npc, false));
                var hudNpc = bootstrap.DebugHud.Refresh();
                Assert.AreEqual("Npc", hudNpc.FocusKind);
                Assert.IsTrue(hudNpc.PersonalityLine.Length > 0);

                bootstrap.SelectionController.SelectEntity(actor, false);
                bootstrap.SelectionController.SelectEntity(npc, true);
                var hudPair = bootstrap.DebugHud.Refresh();
                Assert.IsTrue(hudPair.RelationLine.Contains("me→peer="));

                bootstrap.EventFeed.Clear();
                bootstrap.Session.World.Events.Drain();
                Assert.IsTrue(bootstrap.CommandBridge.IssueSocial(PlayerCommandKind.Help));
                Assert.IsTrue(bootstrap.CommandBridge.IssueSocial(PlayerCommandKind.Help));
                bootstrap.EventFeed.PullFrom(bootstrap.Session.World.Events);
                Assert.IsTrue(bootstrap.EventFeed.ContainsTypeName("RelationshipChanged"));

                // Warm willingness for recruit (npc→actor).
                Assert.IsTrue(new RelationshipService().Record(
                    bootstrap.Session.World,
                    npc,
                    actor,
                    SocialAlphaConstants.RecruitMinScore,
                    "acceptance_warmup").IsSuccess);
                bootstrap.Session.World.Events.Drain();
                Assert.IsTrue(bootstrap.CommandBridge.IssueSocial(PlayerCommandKind.Recruit));
                Assert.IsTrue(bootstrap.Session.World.Entities.TryGet(npc, out var joined));
                Assert.IsTrue(joined.Get<FactionMembershipComponent>().IsAffiliated);
                Assert.IsTrue(
                    bootstrap.Session.World.Events.Drain()
                        .Exists(e => e.Type == CoreEventType.FactionMembershipChanged));

                Assert.AreEqual(1, WorldSnapshot.CurrentSchemaVersion);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        static GameObject CreateHost(out PlayableHostBootstrap bootstrap)
        {
            var host = new GameObject("SocialHostAcceptance");
            bootstrap = host.AddComponent<PlayableHostBootstrap>();
            host.AddComponent<EntityViewSpawner>();
            host.AddComponent<HostSelectionController>();
            host.AddComponent<HostCommandBridge>();
            host.AddComponent<HostDebugHud>();
            host.AddComponent<HostEventFeed>();
            Assert.IsTrue(bootstrap.TryInitialize());
            return host;
        }
    }
}
