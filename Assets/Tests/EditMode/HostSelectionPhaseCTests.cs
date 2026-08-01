using NUnit.Framework;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Unity.Host;

namespace XianXia.Tests
{
    public sealed class HostSelectionPhaseCTests
    {
        [Test]
        public void SelectionState_Replace_Toggle_Clear()
        {
            var state = new HostSelectionState();
            var a = new EntityId(1);
            var b = new EntityId(2);
            var c = new EntityId(3);

            state.Replace(new[] { a, b });
            Assert.AreEqual(2, state.Count);
            Assert.IsTrue(state.Contains(a));
            Assert.IsTrue(state.Contains(b));

            state.ReplaceOne(c);
            Assert.AreEqual(1, state.Count);
            Assert.IsTrue(state.Contains(c));

            state.Toggle(c);
            Assert.AreEqual(0, state.Count);
            state.Toggle(a);
            state.Toggle(b);
            Assert.AreEqual(2, state.Count);
            state.Toggle(a);
            Assert.AreEqual(1, state.Count);
            Assert.IsTrue(state.Contains(b));

            state.Clear();
            Assert.AreEqual(0, state.Count);
        }

        [Test]
        public void SelectionState_Replace_DedupesAndIgnoresNone()
        {
            var state = new HostSelectionState();
            var a = new EntityId(7);
            state.Replace(new[] { a, a, EntityId.None, a });
            Assert.AreEqual(1, state.Count);
            Assert.AreEqual(a, state.SelectedIds[0]);
        }

        [Test]
        public void Controller_ClickReplace_AndShiftToggle()
        {
            var host = CreateHostWithViews(out var bootstrap, out var cam);
            try
            {
                var selection = bootstrap.SelectionController;
                selection.Bind(bootstrap.ViewSpawner, cam);
                var ids = bootstrap.Session.CharacterIds;
                Assert.AreEqual(3, ids.Count);

                Assert.IsTrue(selection.SelectEntity(ids[0], shiftToggle: false));
                Assert.AreEqual(1, selection.State.Count);
                Assert.IsTrue(selection.State.Contains(ids[0]));
                Assert.IsTrue(View(bootstrap, ids[0]).IsHighlightRequested);

                Assert.IsTrue(selection.SelectEntity(ids[1], shiftToggle: true));
                Assert.AreEqual(2, selection.State.Count);
                Assert.IsTrue(View(bootstrap, ids[0]).IsHighlightRequested);
                Assert.IsTrue(View(bootstrap, ids[1]).IsHighlightRequested);

                Assert.IsTrue(selection.SelectEntity(ids[0], shiftToggle: true));
                Assert.AreEqual(1, selection.State.Count);
                Assert.IsFalse(selection.State.Contains(ids[0]));
                Assert.IsFalse(View(bootstrap, ids[0]).IsHighlightRequested);

                // Screen pick path (sphere fallback).
                var screen1 = cam.WorldToScreenPoint(View(bootstrap, ids[1]).transform.position);
                Assert.IsTrue(selection.TryPickEntityAtScreenPoint(screen1, out var picked));
                Assert.AreEqual(ids[1], picked.EntityId);
                Assert.IsTrue(selection.TrySelectAtScreenPoint(screen1, shiftToggle: false));
                Assert.IsTrue(selection.State.Contains(ids[1]));
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(cam.gameObject);
            }
        }

        [Test]
        public void Controller_BoxSelect_AlwaysReplaces()
        {
            var host = CreateHostWithViews(out var bootstrap, out var cam);
            try
            {
                var selection = bootstrap.SelectionController;
                selection.Bind(bootstrap.ViewSpawner, cam);
                var ids = bootstrap.Session.CharacterIds;

                Assert.IsTrue(selection.SelectEntity(ids[2], shiftToggle: false));
                Assert.IsTrue(selection.State.Contains(ids[2]));

                var p0 = cam.WorldToScreenPoint(View(bootstrap, ids[0]).transform.position);
                var p1 = cam.WorldToScreenPoint(View(bootstrap, ids[1]).transform.position);
                var rect = Rect.MinMaxRect(
                    Mathf.Min(p0.x, p1.x) - 5f,
                    Mathf.Min(p0.y, p1.y) - 5f,
                    Mathf.Max(p0.x, p1.x) + 5f,
                    Mathf.Max(p0.y, p1.y) + 5f);
                selection.SelectByBoxScreen(rect);

                Assert.IsTrue(selection.State.Contains(ids[0]));
                Assert.IsTrue(selection.State.Contains(ids[1]));
                Assert.IsFalse(selection.State.Contains(ids[2]));
                Assert.AreEqual(2, selection.State.Count);
                Assert.IsTrue(View(bootstrap, ids[0]).IsHighlightRequested);
                Assert.IsTrue(View(bootstrap, ids[1]).IsHighlightRequested);
                Assert.IsFalse(View(bootstrap, ids[2]).IsHighlightRequested);
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(cam.gameObject);
            }
        }

        [Test]
        public void Bootstrap_Rebuild_ClearsSelectionAndHighlights()
        {
            var host = CreateHostWithViews(out var bootstrap, out var cam);
            try
            {
                var selection = bootstrap.SelectionController;
                selection.Bind(bootstrap.ViewSpawner, cam);
                var id = bootstrap.Session.CharacterIds[0];
                Assert.IsTrue(selection.SelectEntity(id, false));
                Assert.AreEqual(1, selection.State.Count);

                Assert.IsTrue(bootstrap.TryInitialize());
                Assert.AreEqual(0, selection.State.Count);
                foreach (var view in bootstrap.ViewSpawner.Registry.All)
                    Assert.IsFalse(view.IsHighlightRequested);
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(cam.gameObject);
            }
        }

        static EntityView View(PlayableHostBootstrap bootstrap, EntityId id)
        {
            Assert.IsTrue(bootstrap.ViewSpawner.Registry.TryGet(id, out var view));
            return view;
        }

        static GameObject CreateHostWithViews(out PlayableHostBootstrap bootstrap, out Camera cam)
        {
            var camGo = new GameObject("SelCam");
            cam = camGo.AddComponent<Camera>();
            cam.transform.position = new Vector3(0f, 8f, -12f);
            cam.transform.LookAt(Vector3.zero);
            // EditMode cameras often have a zero pixelRect; force a usable viewport for picking.
            cam.pixelRect = new Rect(0f, 0f, 800f, 600f);

            var host = new GameObject("SelHost");
            bootstrap = host.AddComponent<PlayableHostBootstrap>();
            host.AddComponent<EntityViewSpawner>();
            host.AddComponent<HostSelectionController>();
            Assert.IsTrue(bootstrap.TryInitialize());
            bootstrap.SelectionController.Bind(bootstrap.ViewSpawner, cam);
            return host;
        }
    }
}
