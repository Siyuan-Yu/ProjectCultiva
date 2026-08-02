using NUnit.Framework;
using UnityEngine;
using XianXia.Core.Actions;
using XianXia.Core.Input;
using XianXia.Unity.Host;

namespace XianXia.Tests
{
    public sealed class HostFormalHudFocusCommandTests
    {
        [Test]
        public void IssueOne_OnlyAffectsFocus_NotWholeSelection()
        {
            var camGo = new GameObject("FocusCmdCam");
            var cam = camGo.AddComponent<Camera>();
            cam.pixelRect = new Rect(0f, 0f, 800f, 600f);
            var host = new GameObject("FocusCmdHost");
            try
            {
                var bootstrap = host.AddComponent<PlayableHostBootstrap>();
                bootstrap.ConfigureOpeningScenario("base:scenario_ch01_reference");
                host.AddComponent<EntityViewSpawner>();
                host.AddComponent<HostSelectionController>();
                host.AddComponent<HostCommandBridge>();
                host.AddComponent<HostMapGraybox>();
                Assert.IsTrue(bootstrap.TryInitialize(), bootstrap.StatusLine);
                bootstrap.SelectionController.Bind(bootstrap.ViewSpawner, cam);
                bootstrap.SelectionController.SetPartyFilter(bootstrap.Session.CharacterIds);
                bootstrap.CommandBridge.Bind(bootstrap.Session, bootstrap.SelectionController);

                var a = bootstrap.Session.CharacterIds[0];
                var b = bootstrap.Session.CharacterIds[1];
                bootstrap.SelectionController.State.Replace(new[] { a, b });

                Assert.AreEqual(1, bootstrap.CommandBridge.IssueOne(a, PlayerCommandKind.Labor));
                Assert.IsInstanceOf<LaborAction>(ActiveOf(bootstrap, a));
                Assert.IsNull(ActiveOf(bootstrap, b));
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(camGo);
            }
        }

        static IAction ActiveOf(PlayableHostBootstrap bootstrap, XianXia.Core.Domain.Ids.EntityId id)
        {
            if (!bootstrap.Session.World.Entities.TryGet(id, out var entity))
                return null;
            var actionId = entity.Get<XianXia.Core.Entities.ActionStateComponent>().ActiveActionId;
            if (actionId.IsNone)
                return null;
            return bootstrap.Session.World.ActiveActions.TryGetValue(actionId, out var action) ? action : null;
        }
    }
}
