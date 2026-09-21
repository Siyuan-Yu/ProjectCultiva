using UnityEngine;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;

namespace XianXia.Unity.Host
{
    /// <summary>Refreshes an existing Character view after an in-place life-state transition.</summary>
    public static class HostLifeStatePresentationSync
    {
        public static bool RefreshDownedOrDead(
            SimulationWorld world,
            EntityViewSpawner viewSpawner,
            HostMoveController moveController,
            EntityId id,
            bool captureOrdinaryPlacement)
        {
            if (world == null || viewSpawner == null || id.IsNone ||
                !world.Entities.TryGet(id, out var entity) || entity == null ||
                !entity.TryGet<LifecycleComponent>(out var life) ||
                (!life.IsIncapacitated && !life.IsDead))
                return false;

            if (captureOrdinaryPlacement)
                HostSnapshotLocalPlacementCaptureSync.TryCaptureCharacterPlacementFromView(
                    world, viewSpawner, id);

            moveController?.CancelPresentationMovementPublic(id);
            if (!viewSpawner.Registry.TryGet(id, out var view) || view == null)
                return false;

            view.SetActivityText(
                CombatLifeStateService.FormatLifeStateWithCountdown(world, entity));
            view.SetBaseColor(life.IsDead
                ? new Color(0.35f, 0.32f, 0.30f, 0.85f)
                : new Color(0.72f, 0.45f, 0.42f, 0.92f));
            return true;
        }
    }
}
