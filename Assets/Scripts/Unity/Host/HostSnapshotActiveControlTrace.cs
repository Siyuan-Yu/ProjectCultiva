using UnityEngine;
using XianXia.Core.Persistence;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>Snapshot Load 后一次性 Active Control / LocalMap 诊断。</summary>
    public static class HostSnapshotActiveControlTrace
    {
        public static void LogAfterPresentationRebuild(PlayableHostBootstrap bootstrap)
        {
            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return;

            var session = bootstrap.Session;
            var world = session.World;
            var party = session.PlayerParty;
            if (party == null || !party.HasActive)
                return;

            var activeId = party.ActiveCharacterId;
            SnapshotActiveControlledLocalMapResolver.TryResolveRequiredLocalMap(
                world,
                party,
                out var required);

            Debug.Log(
                "[SnapshotRestore.Active] ActiveCharacterId=" + activeId.Value +
                " ActiveWorldLocation=" + (required.WorldLocationLabel ?? "?") +
                " ResolvedRequiredLocalMap=" + (required.LocalMapId ?? string.Empty) +
                " LoadedLocalMap=" + (world.LocalMap?.ActiveMapLayoutId ?? string.Empty) +
                " Source=" + (required.Source ?? string.Empty));

            var presentationFound = false;
            var presentationInstanceId = 0;
            var registryId = 0UL;
            if (bootstrap.ViewSpawner != null &&
                bootstrap.ViewSpawner.Registry.TryGet(activeId, out var view) &&
                view != null)
            {
                presentationFound = true;
                presentationInstanceId = view.GetInstanceID();
                registryId = view.EntityId.Value;
            }

            Debug.Log(
                "[Presentation] DomainCharacterId=" + activeId.Value +
                " PresentationFound=" + presentationFound +
                " PresentationInstanceId=" + presentationInstanceId +
                " PresentationRegistryCharacterId=" + registryId);

            var inputTarget = activeId;
            var inputPresentationId = 0;
            if (bootstrap.ViewSpawner != null &&
                bootstrap.ViewSpawner.Registry.TryGet(inputTarget, out var inputView) &&
                inputView != null)
                inputPresentationId = inputView.GetInstanceID();

            var move = bootstrap.MoveController;
            var selectedId = 0UL;
            if (bootstrap.SelectionController != null)
            {
                var selected = bootstrap.SelectionController.State.SelectedIds;
                if (selected.Count > 0)
                    selectedId = selected[0].Value;
            }
            var cameraTargetId = 0UL;
            if (presentationFound && bootstrap.PlayerPartyController != null)
                cameraTargetId = bootstrap.PlayerPartyController.ActiveCharacterId.Value;

            Debug.Log(
                "[Control] InputTargetCharacterId=" + inputTarget.Value +
                " InputTargetPresentationInstanceId=" + inputPresentationId +
                " MovementControllerBound=" + (move != null && move.BoundLocalMapId != null) +
                " MovementLocalMapId=" + (move != null ? move.BoundLocalMapId : string.Empty) +
                " WalkGridReady=" + (move != null && move.WalkGrid != null) +
                " SelectedCharacterId=" + selectedId +
                " CameraTargetCharacterId=" + cameraTargetId +
                " SessionPaused=" + session.IsPaused +
                " InputGateBlock=" + HostInputGate.BlockWorldInteraction);
        }
    }
}
