using NUnit.Framework;
using UnityEngine;
using XianXia.Core.Input;
using XianXia.Unity.Host;

namespace XianXia.Tests
{
    public sealed class SocialEventFeedPhaseDTests
    {
        [Test]
        public void EventFeed_PrioritizesRelationshipChanged()
        {
            var host = CreateHost(out var bootstrap);
            try
            {
                var a = bootstrap.Session.CharacterIds[0];
                var b = bootstrap.Session.CharacterIds[1];
                bootstrap.SelectionController.SelectEntity(a, false);
                bootstrap.SelectionController.SelectEntity(b, true);
                bootstrap.EventFeed.Clear();
                Assert.IsTrue(bootstrap.CommandBridge.IssueSocial(PlayerCommandKind.Help));
                bootstrap.EventFeed.PullFrom(bootstrap.Session.World.Events);
                Assert.IsTrue(bootstrap.EventFeed.ContainsTypeName("RelationshipChanged"));
                Assert.IsTrue(bootstrap.EventFeed.ToDebugText().Contains("*"));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        static GameObject CreateHost(out PlayableHostBootstrap bootstrap)
        {
            var host = new GameObject("SocialFeedHost");
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
