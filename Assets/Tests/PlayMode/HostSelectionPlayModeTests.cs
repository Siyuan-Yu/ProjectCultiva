using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using XianXia.Unity.Host;

namespace XianXia.Tests.PlayMode
{
    public sealed class HostSelectionPlayModeTests
    {
        [UnityTest]
        public IEnumerator Click_SelectsCorrectEntityView_AndHighlight()
        {
            var setup = CreateHost();
            yield return null;

            var bootstrap = setup.bootstrap;
            var cam = setup.cam;
            var selection = bootstrap.SelectionController;
            var id = bootstrap.Session.CharacterIds[1];
            Assert.IsTrue(bootstrap.ViewSpawner.Registry.TryGet(id, out var view));

            selection.Bind(bootstrap.ViewSpawner, cam);
            var screen = cam.WorldToScreenPoint(view.transform.position);
            Assert.IsTrue(selection.TrySelectAtScreenPoint(screen, shiftToggle: false) ||
                          selection.SelectEntity(id, false));
            Assert.AreEqual(1, selection.State.Count);
            Assert.IsTrue(selection.State.Contains(id));
            Assert.IsTrue(view.IsHighlightRequested);

            foreach (var other in bootstrap.ViewSpawner.Registry.All)
            {
                if (other.EntityId != id)
                    Assert.IsFalse(other.IsHighlightRequested);
            }

            Object.Destroy(setup.host);
            Object.Destroy(setup.camGo);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BoxSelect_ReplacesSelection()
        {
            var setup = CreateHost();
            yield return null;

            var bootstrap = setup.bootstrap;
            var cam = setup.cam;
            var selection = bootstrap.SelectionController;
            var ids = bootstrap.Session.CharacterIds;

            Assert.IsTrue(bootstrap.ViewSpawner.Registry.TryGet(ids[0], out var v0));
            Assert.IsTrue(bootstrap.ViewSpawner.Registry.TryGet(ids[1], out var v1));
            Assert.IsTrue(bootstrap.ViewSpawner.Registry.TryGet(ids[2], out var v2));

            selection.Bind(bootstrap.ViewSpawner, cam);
            Assert.IsTrue(selection.SelectEntity(ids[2], false));
            Assert.IsTrue(selection.State.Contains(ids[2]));

            var p0 = cam.WorldToScreenPoint(v0.transform.position);
            var p1 = cam.WorldToScreenPoint(v1.transform.position);
            var rect = Rect.MinMaxRect(
                Mathf.Min(p0.x, p1.x) - 8f,
                Mathf.Min(p0.y, p1.y) - 8f,
                Mathf.Max(p0.x, p1.x) + 8f,
                Mathf.Max(p0.y, p1.y) + 8f);
            selection.SelectByBoxScreen(rect);

            Assert.AreEqual(2, selection.State.Count);
            Assert.IsTrue(v0.IsHighlightRequested);
            Assert.IsTrue(v1.IsHighlightRequested);
            Assert.IsFalse(v2.IsHighlightRequested);

            Object.Destroy(setup.host);
            Object.Destroy(setup.camGo);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Rebuild_ClearsSelection()
        {
            var setup = CreateHost();
            yield return null;

            var bootstrap = setup.bootstrap;
            var cam = setup.cam;
            var selection = bootstrap.SelectionController;
            var id = bootstrap.Session.CharacterIds[0];
            Assert.IsTrue(bootstrap.ViewSpawner.Registry.TryGet(id, out var view));
            selection.Bind(bootstrap.ViewSpawner, cam);
            Assert.IsTrue(selection.SelectEntity(id, false));
            Assert.AreEqual(1, selection.State.Count);

            Assert.IsTrue(bootstrap.TryInitialize());
            Assert.AreEqual(0, selection.State.Count);
            foreach (var v in bootstrap.ViewSpawner.Registry.All)
                Assert.IsFalse(v.IsHighlightRequested);

            Object.Destroy(setup.host);
            Object.Destroy(setup.camGo);
            yield return null;
        }

        static (GameObject host, GameObject camGo, PlayableHostBootstrap bootstrap, Camera cam) CreateHost()
        {
            var camGo = new GameObject("PlayModeSelCam");
            var cam = camGo.AddComponent<Camera>();
            cam.transform.position = new Vector3(0f, 8f, -12f);
            cam.transform.LookAt(Vector3.zero);

            var host = new GameObject("PlayModeSelHost");
            var bootstrap = host.AddComponent<PlayableHostBootstrap>();
            host.AddComponent<EntityViewSpawner>();
            host.AddComponent<HostSelectionController>();
            Assert.IsTrue(bootstrap.TryInitialize(), bootstrap.StatusLine);
            bootstrap.SelectionController.Bind(bootstrap.ViewSpawner, cam);
            return (host, camGo, bootstrap, cam);
        }
    }
}
