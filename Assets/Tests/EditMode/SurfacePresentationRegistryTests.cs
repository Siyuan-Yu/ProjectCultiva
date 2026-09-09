using NUnit.Framework;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Data.Content;
using XianXia.Unity.Host;

namespace XianXia.Tests.EditMode
{
    public sealed class SurfacePresentationRegistryTests
    {
        [TearDown]
        public void TearDown()
        {
            HostInteractSpots.ClearAll();
            HostMapObjectRegistry.ClearAll();
            HostFarmFieldRegistry.ClearAll();
        }

        [Test]
        public void OwnerScopedRegistries_RemoveOnlyRequestedOwner()
        {
            var a = new GameObject("surface-a").AddComponent<HostMapDestructible>();
            var b = new GameObject("surface-b").AddComponent<HostMapDestructible>();
            HostMapObjectRegistry.Register("A", a);
            HostMapObjectRegistry.Register("B", b);
            HostInteractSpots.RegisterPlot("A", new HostInteractSpot("a", HostInteractSpotKind.Work, 0f, 0f, "A"));
            HostInteractSpots.RegisterPlot("B", new HostInteractSpot("b", HostInteractSpotKind.Work, 10f, 0f, "B"));

            HostMapObjectRegistry.RemoveOwner("A");
            HostInteractSpots.RemoveOwner("A");

            Assert.AreEqual(1, HostMapObjectRegistry.AllDestructibles.Count);
            Assert.AreSame(b, HostMapObjectRegistry.AllDestructibles[0]);
            Assert.IsTrue(HostInteractSpots.TryFindNearest(
                HostPresentationSpace.FromPresentation(10f, 0f), HostInteractSpotKind.Work, out _, 0.1f));
            Object.DestroyImmediate(a.gameObject);
            Object.DestroyImmediate(b.gameObject);
        }

        [Test]
        public void DuplicateRegistrationDoesNotDuplicateAndDestroyedReferencesPrune()
        {
            var go = new GameObject("surface-b");
            var destructible = go.AddComponent<HostMapDestructible>();
            HostMapObjectRegistry.Register("B", destructible);
            HostMapObjectRegistry.Register("B", destructible);
            Assert.AreEqual(1, HostMapObjectRegistry.AllDestructibles.Count);

            Object.DestroyImmediate(go);
            Assert.IsFalse(HostMapObjectRegistry.TryFindNearestDestructible(Vector3.zero, 5f, out _));
            Assert.AreEqual(0, HostMapObjectRegistry.AllDestructibles.Count);
        }

        [Test]
        public void SameLayoutCanHaveTwoIndependentPresentationInstances()
        {
            var host = new GameObject("tile-host");
            var tileMap = host.AddComponent<HostDemoTileMap>();
            var layout = new MapLayoutDefinition
            {
                Id = new DefinitionId("test", "surface"),
                OriginX = 0f,
                OriginY = 0f,
                CellSize = 1f,
                Width = 1,
                Height = 1,
            };

            var a = tileMap.BuildLayoutInstance("A", layout, Vector2.zero);
            var b = tileMap.BuildLayoutInstance("B", layout, new Vector2(12f, 0f));

            Assert.AreSame(layout, a.SourceLayout);
            Assert.AreSame(layout, b.SourceLayout);
            Assert.AreNotSame(a.Root, b.Root);
            Assert.AreEqual(2, tileMap.LoadedInstances.Count);
            Assert.IsTrue(tileMap.RemoveLayoutInstance("A"));
            Assert.IsTrue(tileMap.LoadedInstances.ContainsKey("B"));
            Object.DestroyImmediate(host);
        }
    }
}
