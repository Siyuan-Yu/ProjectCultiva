using UnityEngine;
using XianXia.Core.Exploration;
using XianXia.Core.Simulation;
using XianXia.Data.Content;

namespace XianXia.Unity.Host
{
    /// <summary>洞府入口／洞内出口的点选（mapLayout kind=cave）。</summary>
    public static class HostCaveEntranceQuery
    {
        public const float NearbyPartyRadius = 14f;
        public const float ApproachDistance = 2.5f;

        public static bool TryPickAtMouse(
            Camera camera,
            SimulationWorld world,
            MapLayoutDefinition layout,
            ContinuousOutdoorSurfaceRuntime continuous,
            out string entranceLocationId)
        {
            entranceLocationId = string.Empty;
            if (camera == null ||
                !HostPresentationSpace.TryRaycastPlane(camera, Input.mousePosition, out var worldPoint))
                return false;
            return TryPickAtWorld(world, layout, continuous, worldPoint, out entranceLocationId);
        }

        public static bool TryPickAtWorld(
            SimulationWorld world,
            MapLayoutDefinition layout,
            ContinuousOutdoorSurfaceRuntime continuous,
            Vector3 worldPoint,
            out string entranceLocationId)
        {
            entranceLocationId = string.Empty;
            if (continuous != null && continuous.IsActive)
            {
                var point = HostPresentationSpace.ToPresentation(worldPoint);
                if (!continuous.TryPickBakedPlacement("cave", point.x, point.y, out var id) ||
                    !world.ContinuousOutdoorMaterialization.TryGetAnyPlace(id, out var outdoorEntrance) ||
                    !OpportunityEntranceRules.IsHiddenEntrance(outdoorEntrance) ||
                    !OpportunityEntranceRules.IsRevealedToPlayerParty(world, outdoorEntrance)) return false;
                entranceLocationId = id;
                return true;
            }
            if (world?.LocalPlaces?.Locations == null || layout?.Placements == null)
                return false;

            var p = HostPresentationSpace.ToPresentation(worldPoint);
            var cs = layout.CellSize > 0f ? layout.CellSize : 1f;
            var bestArea = float.MaxValue;
            string bestId = null;

            for (var i = 0; i < layout.Placements.Count; i++)
            {
                var pl = layout.Placements[i];
                if (pl == null || string.IsNullOrWhiteSpace(pl.BoundLocationId))
                    continue;
                if (!string.Equals(pl.Kind, "cave", System.StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!world.LocalPlaces.TryGet(pl.BoundLocationId, out var loc))
                    continue;
                if (!OpportunityEntranceRules.IsHiddenEntrance(loc))
                    continue;
                if (!OpportunityEntranceRules.IsRevealedToPlayerParty(world, loc))
                    continue;
                if (!ContainsPresentation(layout, pl, cs, p.x, p.y))
                    continue;

                var area = FootprintArea(pl, cs);
                if (area >= bestArea)
                    continue;
                bestArea = area;
                bestId = loc.Id;
            }

            if (string.IsNullOrEmpty(bestId))
                return false;
            entranceLocationId = bestId;
            return true;
        }

        /// <summary>
        /// 洞内：以 presentation 点判断是否在 Exit Trigger geometry 内。
        /// </summary>
        public static bool TryResolveInteriorExitAtPoint(
            MapLayoutDefinition layout,
            float presentationX,
            float presentationZ,
            out string exitId,
            out string exitLabel)
        {
            exitId = string.Empty;
            exitLabel = string.Empty;
            if (layout?.Placements == null)
                return false;

            var cs = layout.CellSize > 0f ? layout.CellSize : 1f;
            var bestArea = float.MaxValue;
            string bestId = null;
            string bestLabel = null;

            for (var i = 0; i < layout.Placements.Count; i++)
            {
                var pl = layout.Placements[i];
                if (pl == null)
                    continue;
                if (!string.Equals(pl.Kind, "cave", System.StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!IsInteriorExitStamp(pl))
                    continue;
                if (!ContainsPresentation(layout, pl, cs, presentationX, presentationZ))
                    continue;

                var area = FootprintArea(pl, cs);
                if (area >= bestArea)
                    continue;
                bestArea = area;
                bestId = pl.Id ?? string.Empty;
                bestLabel = string.IsNullOrEmpty(pl.Label) ? "洞口" : pl.Label;
            }

            if (string.IsNullOrEmpty(bestLabel) && string.IsNullOrEmpty(bestId))
                return false;
            exitId = bestId ?? string.Empty;
            exitLabel = bestLabel ?? "洞口";
            return true;
        }

        public static bool IsPointInsideInteriorExit(
            MapLayoutDefinition layout,
            float presentationX,
            float presentationZ) =>
            TryResolveInteriorExitAtPoint(layout, presentationX, presentationZ, out _, out _);

        public static bool IsInteriorExitStamp(MapPlacement pl)
        {
            if (pl == null)
                return false;
            var id = pl.Id ?? string.Empty;
            var label = pl.Label ?? string.Empty;
            if (id.IndexOf("exit", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (label.IndexOf("离开", System.StringComparison.Ordinal) >= 0)
                return true;
            if (label.IndexOf("出口", System.StringComparison.Ordinal) >= 0)
                return true;
            var w = pl.W < 1 ? 1 : pl.W;
            var h = pl.H < 1 ? 1 : pl.H;
            return w * h <= 20;
        }

        public static bool TryGetCenter(
            SimulationWorld world,
            string entranceLocationId,
            out Vector3 worldCenter)
        {
            worldCenter = default;
            if (world == null ||
                string.IsNullOrWhiteSpace(entranceLocationId) ||
                (!world.ContinuousOutdoorMaterialization.TryGetAnyPlace(entranceLocationId, out var loc) &&
                 !world.LocalPlaces.TryGet(entranceLocationId, out loc)))
                return false;
            worldCenter = HostPresentationSpace.FromPresentation(loc.PresentationX, loc.PresentationZ);
            return true;
        }

        public static bool IsNearEntrance(
            float presentationX,
            float presentationZ,
            WorldLocationState entrance,
            float distance = ApproachDistance)
        {
            if (entrance == null)
                return false;
            var dx = entrance.PresentationX - presentationX;
            var dz = entrance.PresentationZ - presentationZ;
            return dx * dx + dz * dz <= distance * distance;
        }

        public static bool IsNearEntrance(
            PlayableHostBootstrap bootstrap,
            XianXia.Core.Domain.Ids.EntityId actor,
            WorldLocationState entrance,
            out float distance)
        {
            distance = float.PositiveInfinity;
            if (bootstrap?.Session?.World == null || entrance == null || actor.IsNone)
                return false;
            float x, z;
            if (bootstrap.ViewSpawner?.Registry != null &&
                bootstrap.ViewSpawner.Registry.TryGet(actor, out var view) && view != null)
            {
                var point = HostPresentationSpace.ToPresentation(view.transform.position);
                x = point.x; z = point.y;
            }
            else if (bootstrap.Session.PlayerParty?.IsMember(actor) == true &&
                     bootstrap.ContinuousOutdoorSurfaceRuntime?.IsActive == true &&
                     bootstrap.ContinuousOutdoorSurfaceRuntime.Mapper != null)
            {
                var motion = bootstrap.Session.World.PlayerPartyTravel.WorldPosition;
                bootstrap.ContinuousOutdoorSurfaceRuntime.Mapper.WorldToPresentation(
                    motion.X, motion.Y, out x, out z);
            }
            else
                return false;
            var dx = entrance.PresentationX - x;
            var dz = entrance.PresentationZ - z;
            distance = Mathf.Sqrt(dx * dx + dz * dz);
            return distance <= ApproachDistance;
        }

        static bool ContainsPresentation(
            MapLayoutDefinition layout,
            MapPlacement pl,
            float cs,
            float px,
            float pz)
        {
            var pw = pl.W < 1 ? 1 : pl.W;
            var ph = pl.H < 1 ? 1 : pl.H;
            var minX = layout.OriginX + pl.X * cs;
            var maxX = layout.OriginX + (pl.X + pw) * cs;
            var minZ = layout.OriginY + pl.Y * cs;
            var maxZ = layout.OriginY + (pl.Y + ph) * cs;
            return px >= minX && px <= maxX && pz >= minZ && pz <= maxZ;
        }

        static float FootprintArea(MapPlacement pl, float cs)
        {
            var pw = pl.W < 1 ? 1 : pl.W;
            var ph = pl.H < 1 ? 1 : pl.H;
            return pw * ph * cs * cs;
        }
    }
}
