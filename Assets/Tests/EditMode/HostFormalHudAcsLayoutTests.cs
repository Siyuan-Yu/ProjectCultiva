using NUnit.Framework;
using UnityEngine;
using XianXia.Unity.Host;

namespace XianXia.Tests
{
    public sealed class HostFormalHudAcsLayoutTests
    {
        [Test]
        public void FormalHud_BindsAndIssuesLaborFromBridge()
        {
            var camGo = new GameObject("AcsHudCam");
            var cam = camGo.AddComponent<Camera>();
            cam.pixelRect = new Rect(0f, 0f, 800f, 600f);

            var host = new GameObject("AcsHudHost");
            try
            {
                var bootstrap = host.AddComponent<PlayableHostBootstrap>();
                bootstrap.ConfigureOpeningScenario("base:scenario_ch01_reference");
                host.AddComponent<EntityViewSpawner>();
                host.AddComponent<HostSelectionController>();
                host.AddComponent<HostCommandBridge>();
                host.AddComponent<HostMapGraybox>();
                var hud = host.AddComponent<HostFormalHud>();
                Assert.IsTrue(bootstrap.TryInitialize(), bootstrap.StatusLine);
                bootstrap.SelectionController.Bind(bootstrap.ViewSpawner, cam);
                bootstrap.SelectionController.SetPartyFilter(bootstrap.Session.CharacterIds);
                bootstrap.CommandBridge.Bind(bootstrap.Session, bootstrap.SelectionController);
                hud.Bind(bootstrap, bootstrap.SelectionController, null);

                var id = bootstrap.Session.CharacterIds[0];
                Assert.IsTrue(bootstrap.SelectionController.SelectEntity(id, false));
                Assert.AreEqual(1, bootstrap.CommandBridge.IssueSelected(XianXia.Core.Input.PlayerCommandKind.Labor));
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(camGo);
            }
        }
    }
}
