using NUnit.Framework;
using UnityEngine;
using XianXia.Core.Social;
using XianXia.Unity.Host;

namespace XianXia.Tests
{
    public sealed class SocialHudPhaseBTests
    {
        [Test]
        public void Hud_ShowsPersonality_Faction_AndRelationWhenPeerSelected()
        {
            var host = CreateHost(out var bootstrap);
            try
            {
                var actor = bootstrap.Session.CharacterIds[0];
                var npc = bootstrap.Session.RecruitableNpcId;
                Assert.IsFalse(npc.IsNone);

                bootstrap.SelectionController.SelectEntity(actor, false);
                bootstrap.SelectionController.SelectEntity(npc, true);

                var snap = bootstrap.DebugHud.Refresh();
                Assert.IsTrue(snap.Ready);
                Assert.IsTrue(snap.FocusName.Length > 0);
                Assert.IsTrue(snap.PersonalityLine.Contains("personality_") || snap.PersonalityLine.Contains("(none)") ||
                              snap.FocusKind == "Npc");
                Assert.IsTrue(snap.ToDebugText().Contains("Personality:"));
                Assert.IsTrue(snap.ToDebugText().Contains("Relation:"));
                Assert.IsFalse(snap.RelationLine.Contains("select peer"));

                // Opening companions have favor; actor→npc may be 0 until Help.
                Assert.IsTrue(snap.RelationLine.Contains("me→peer="));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        static GameObject CreateHost(out PlayableHostBootstrap bootstrap)
        {
            var host = new GameObject("SocialHudHost");
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
