using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using XianXia.Core.Entities;
using XianXia.Unity.Host;

namespace XianXia.Tests
{
    public sealed class NpcViewPhaseATests
    {
        static string BaseGamePath =>
            System.IO.Path.GetFullPath(
                System.IO.Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        [Test]
        public void Spawner_Rebuild_IncludesRecruitableNpc_AndIsSelectable()
        {
            var session = new PlayableHostSession();
            Assert.IsTrue(session.Initialize(BaseGamePath).IsSuccess);
            Assert.AreEqual(3, session.CharacterIds.Count);
            Assert.IsFalse(session.RecruitableNpcId.IsNone);
            Assert.AreEqual(4, session.ViewableEntityIds.Count);

            var host = new GameObject("HostNpcView");
            try
            {
                var spawner = host.AddComponent<EntityViewSpawner>();
                spawner.Rebuild(session);
                Assert.AreEqual(4, spawner.SpawnedCount);

                Assert.IsTrue(spawner.Registry.TryGet(session.RecruitableNpcId, out var npcView));
                Assert.IsTrue(npcView.IsBound);
                Assert.IsNotNull(npcView.GetComponent<CapsuleCollider>());

                Assert.IsTrue(session.World.Entities.TryGet(session.RecruitableNpcId, out var npc));
                Assert.AreEqual(EntityTag.Npc, npc.Tags);

                var selection = host.AddComponent<HostSelectionController>();
                selection.Bind(spawner, null);
                selection.SelectEntity(session.RecruitableNpcId, false);
                Assert.AreEqual(1, selection.State.Count);
                Assert.IsTrue(selection.State.Contains(session.RecruitableNpcId));
            }
            finally
            {
                Object.DestroyImmediate(host);
                session.Clear();
            }
        }
    }
}
