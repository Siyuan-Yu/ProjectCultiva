using NUnit.Framework;
using UnityEngine;
using XianXia.Core.Input;
using XianXia.Core.Social;
using XianXia.Unity.Host;
using CoreEventType = XianXia.Core.Events.EventType;

namespace XianXia.Tests
{
    public sealed class SocialCommandPhaseCTests
    {
        [Test]
        public void Help_ViaPort_RaisesRelationship_AndRecruitGateWorks()
        {
            var host = CreateHost(out var bootstrap);
            try
            {
                var actor = bootstrap.Session.CharacterIds[0];
                var npc = bootstrap.Session.RecruitableNpcId;
                var world = bootstrap.Session.World;

                bootstrap.SelectionController.SelectEntity(actor, false);
                bootstrap.SelectionController.SelectEntity(npc, true);

                world.Events.Drain();
                Assert.IsTrue(bootstrap.CommandBridge.IssueSocial(PlayerCommandKind.Help));
                Assert.AreEqual(SocialAlphaConstants.HelpDelta, world.Relationships.Score(actor, npc));
                Assert.IsTrue(world.Events.Drain().Exists(e => e.Type == CoreEventType.RelationshipChanged));

                // Recruit needs npc→actor score; Help was actor→npc. Warm npc→actor then Recruit.
                Assert.IsFalse(bootstrap.CommandBridge.IssueSocial(PlayerCommandKind.Recruit));
                Assert.IsTrue(bootstrap.CommandBridge.LastStatus.Contains("FAIL"));

                Assert.IsTrue(new RelationshipService().Record(
                    world, npc, actor, SocialAlphaConstants.RecruitMinScore, "warmup").IsSuccess);
                world.Events.Drain();
                Assert.IsTrue(bootstrap.CommandBridge.IssueSocial(PlayerCommandKind.Recruit));
                Assert.IsTrue(world.Entities.TryGet(npc, out var recruited));
                Assert.IsTrue(recruited.Get<FactionMembershipComponent>().IsAffiliated);
                Assert.IsTrue(world.Events.Drain().Exists(e => e.Type == CoreEventType.FactionMembershipChanged));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Slight_ViaPort_LowersScore()
        {
            var host = CreateHost(out var bootstrap);
            try
            {
                var a = bootstrap.Session.CharacterIds[0];
                var b = bootstrap.Session.CharacterIds[1];
                bootstrap.SelectionController.SelectEntity(a, false);
                bootstrap.SelectionController.SelectEntity(b, true);

                var before = bootstrap.Session.World.Relationships.Score(a, b);
                Assert.IsTrue(bootstrap.CommandBridge.IssueSocial(PlayerCommandKind.Slight));
                Assert.AreEqual(before + SocialAlphaConstants.SlightDelta,
                    bootstrap.Session.World.Relationships.Score(a, b));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        static GameObject CreateHost(out PlayableHostBootstrap bootstrap)
        {
            var host = new GameObject("SocialCmdHost");
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
