using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using XianXia.Core.Actions;
using XianXia.Core.Entities;
using XianXia.Core.Input;
using XianXia.Unity.Host;

namespace XianXia.Tests.PlayMode
{
    public sealed class HostCommandPlayModeTests
    {
        [UnityTest]
        public IEnumerator Select_Then_Labor_SetsPlayerLaborAction()
        {
            var setup = CreateHost();
            yield return null;

            var bootstrap = setup.bootstrap;
            var id = bootstrap.Session.CharacterIds[0];
            bootstrap.SelectionController.SelectEntity(id, false);
            Assert.AreEqual(1, bootstrap.CommandBridge.IssueSelected(PlayerCommandKind.Labor));

            Assert.IsTrue(bootstrap.Session.World.Entities.TryGet(id, out var entity));
            Assert.IsTrue(entity.Get<ActionStateComponent>().HasActiveAction);
            Assert.IsTrue(bootstrap.Session.World.ActiveActions.TryGetValue(
                entity.Get<ActionStateComponent>().ActiveActionId, out var action));
            Assert.IsInstanceOf<LaborAction>(action);

            Object.Destroy(setup.host);
            Object.Destroy(setup.camGo);
            yield return null;
        }

        static (GameObject host, GameObject camGo, PlayableHostBootstrap bootstrap) CreateHost()
        {
            var camGo = new GameObject("PlayModeCmdCam");
            var cam = camGo.AddComponent<Camera>();
            cam.transform.position = new Vector3(0f, 8f, -12f);
            cam.transform.LookAt(Vector3.zero);

            var host = new GameObject("PlayModeCmdHost");
            var bootstrap = host.AddComponent<PlayableHostBootstrap>();
            host.AddComponent<EntityViewSpawner>();
            host.AddComponent<HostSelectionController>();
            host.AddComponent<HostCommandBridge>();
            Assert.IsTrue(bootstrap.TryInitialize(), bootstrap.StatusLine);
            bootstrap.SelectionController.Bind(bootstrap.ViewSpawner, cam);
            bootstrap.CommandBridge.Bind(bootstrap.Session, bootstrap.SelectionController);
            return (host, camGo, bootstrap);
        }
    }
}
