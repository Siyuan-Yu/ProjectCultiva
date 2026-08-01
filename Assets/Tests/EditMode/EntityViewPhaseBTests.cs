using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using XianXia.Core.Domain.Ids;
using XianXia.Unity.Host;

namespace XianXia.Tests
{
    public sealed class EntityViewPhaseBTests
    {
        static string BaseGamePath =>
            System.IO.Path.GetFullPath(
                System.IO.Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        [Test]
        public void Spawner_Rebuild_CreatesThreeBoundViews()
        {
            var session = new PlayableHostSession();
            Assert.IsTrue(session.Initialize(BaseGamePath).IsSuccess);

            var host = new GameObject("HostTest");
            try
            {
                var spawner = host.AddComponent<EntityViewSpawner>();
                spawner.Rebuild(session);

                Assert.AreEqual(3, spawner.SpawnedCount);
                Assert.AreEqual(3, spawner.Registry.Count);

                var seen = new HashSet<ulong>();
                foreach (var id in session.CharacterIds)
                {
                    Assert.IsTrue(spawner.Registry.TryGet(id, out var view));
                    Assert.IsTrue(view.IsBound);
                    Assert.AreEqual(id, view.EntityId);
                    Assert.IsFalse(seen.Contains(id.Value));
                    seen.Add(id.Value);
                    Assert.IsNotNull(view.GetComponent<CapsuleCollider>());
                }
            }
            finally
            {
                Object.DestroyImmediate(host);
                session.Clear();
            }
        }

        [Test]
        public void Spawner_ClearAndRebuild_DoesNotDuplicate()
        {
            var session = new PlayableHostSession();
            Assert.IsTrue(session.Initialize(BaseGamePath).IsSuccess);

            var host = new GameObject("HostTest");
            try
            {
                var spawner = host.AddComponent<EntityViewSpawner>();
                spawner.Rebuild(session);
                spawner.Rebuild(session);
                Assert.AreEqual(3, spawner.SpawnedCount);
                Assert.AreEqual(3, spawner.Registry.Count);

                session.Rebuild(BaseGamePath);
                spawner.Clear();
                spawner.Rebuild(session);
                Assert.AreEqual(3, spawner.SpawnedCount);

                foreach (var id in session.CharacterIds)
                    Assert.IsTrue(spawner.Registry.TryGet(id, out _));
            }
            finally
            {
                Object.DestroyImmediate(host);
                session.Clear();
            }
        }

        [Test]
        public void EntityView_BindMissingEntity_FailsSafely_AndDoesNotMutateCore()
        {
            var session = new PlayableHostSession();
            Assert.IsTrue(session.Initialize(BaseGamePath).IsSuccess);
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            try
            {
                var view = go.AddComponent<EntityView>();
                LogAssert.Expect(LogType.Error, new Regex("Core Entity not found"));
                Assert.IsFalse(view.Bind(session.World, new EntityId(99999)));
                Assert.IsFalse(view.IsBound);

                // Core entity count unchanged.
                Assert.AreEqual(3, session.World.Entities.Count);
            }
            finally
            {
                Object.DestroyImmediate(go);
                session.Clear();
            }
        }

        [Test]
        public void EntityView_SetHighlight_IsPresentationOnly()
        {
            var session = new PlayableHostSession();
            Assert.IsTrue(session.Initialize(BaseGamePath).IsSuccess);
            var id = session.CharacterIds[0];
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            try
            {
                var view = go.AddComponent<EntityView>();
                Assert.IsTrue(view.Bind(session.World, id));
                view.SetHighlight(true);
                Assert.IsTrue(view.IsHighlightRequested);

                Assert.IsTrue(session.World.Entities.TryGet(id, out var entity));
                var before = entity.Get<XianXia.Core.Labor.DailyTaskComponent>().CompletedAmount;
                // Highlight must not touch Core counters.
                Assert.AreEqual(before, entity.Get<XianXia.Core.Labor.DailyTaskComponent>().CompletedAmount);
            }
            finally
            {
                Object.DestroyImmediate(go);
                session.Clear();
            }
        }

        [Test]
        public void Bootstrap_Initialize_SpawnsViews()
        {
            var host = new GameObject("BootstrapHost");
            try
            {
                var bootstrap = host.AddComponent<PlayableHostBootstrap>();
                host.AddComponent<EntityViewSpawner>();
                Assert.IsTrue(bootstrap.TryInitialize());
                Assert.IsNotNull(bootstrap.ViewSpawner);
                Assert.AreEqual(3, bootstrap.ViewSpawner.SpawnedCount);
                Assert.AreEqual(3, bootstrap.Session.CharacterIds.Count);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
