using UnityEngine;
using XianXia.Core.Construction;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Navigation;
using XianXia.Core.Npc;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    public static class HostPrisonWallCombat
    {
        public static void BeginConfirmed(PlayableHostBootstrap host, EntityId actor, string wallId)
        {
            var world = host?.Session?.World;
            if (!PrisonWallBreachService.TryResolveWall(world, wallId, out var room, out _, out _) ||
                !PrisonWallBreachService.TryOwner(world, room, out var site)) return;
            if (world.Strategic.CharacterEncounter != null)
            {
                if (!PrisonWallBreachService.CanDamage(world, actor, wallId))
                { HostWorldSiteCoreWarfare.Feedback(host, "监狱墙不在当前参战范围内，或双方尚未宣战。"); return; }
                BeginMaterializedAttack(host, actor, wallId);
                return;
            }
            var defender = WorldSiteDefenseCharacterQuery.FindNearest(world,
                new WorldSiteCoreTarget(site), world.Strategic.PlayerFactionId);
            host.GetComponent<HostCharacterEncounter>()?.RequestPrisonAssault(actor, defender, room.StableAssetId,
                () => BeginMaterializedAttack(host, actor, wallId));
        }

        static void BeginMaterializedAttack(PlayableHostBootstrap host, EntityId actor, string wallId)
        {
            // Entering an independent field rebuilds views; never retain the old Unity object.
            foreach (var wall in HostMapObjectRegistry.AllDestructibles)
            {
                if (wall == null || wall.IsDestroyed || wall.PlacementId != wallId) continue;
                host.GetComponent<HostDestructibleAssault>()?.Begin(actor, wall);
                return;
            }
            HostWorldSiteCoreWarfare.Feedback(host, "目标墙体已不在当前战场。");
        }

        public static void ReconcileBreaches(PlayableHostBootstrap host)
        {
            var world = host?.Session?.World;
            var surface = host?.ContinuousOutdoorSurfaceRuntime;
            if (world == null || surface == null || !surface.IsDestructibleNavigationCurrent ||
                !surface.TryGetCompositeWalkGrid(out var grid) || world.LocalMap.IsInInterior) return;
            var field = world.Strategic.CharacterEncounter;
            if (field != null && field.Phase != CharacterEncounterPhase.Active && field.Phase != CharacterEncounterPhase.ReadyToEnd) return;
            foreach (var room in world.OutdoorConstructedAssets.Assets.Values)
            {
                if (!RoomBedGeometry.IsRoom(room) || room.RoomUsage != RoomUsage.Prison || room.SurfaceId != surface.ActiveSurfaceId) continue;
                var hasCaptive = false;
                foreach (var entity in world.Entities.All)
                    if (PrisonConfinementService.TryGetRoom(world, entity.Id, out var held) && held.StableAssetId == room.StableAssetId)
                    { hasCaptive = true; break; }
                if (!hasCaptive || !PrisonWallBreachService.TryFindPassableBreach(world, room, CanCross, out var exit)) continue;
                if (PrisonWallBreachService.ReleaseRoom(world, room, host.Session.PlayerParty.ActiveCharacterId, exit) > 0)
                {
                    surface.ReconcileOutdoorEntityMaterializationForScopeChange();
                    host.DispatchDrainedEvents();
                    HostWorldSiteCoreWarfare.Feedback(host, "墙体出现可通行缺口，囚犯已解除拘押；伤势与原位置保留。");
                }
            }

            bool CanCross(WorldVec2 inside, WorldVec2 outside)
            {
                if (!surface.TryWorldToPresentation(inside, out var a) || !surface.TryWorldToPresentation(outside, out var b)) return false;
                var normal = (b - a).normalized;
                var tangent = new Vector3(-normal.y, normal.x, 0) * (grid.CellSize * .45f);
                // Check a character-width corridor, not just an infinitesimal center ray.
                return Clear(a, b) && Clear(a + tangent, b + tangent) && Clear(a - tangent, b - tangent);
            }
            bool Clear(Vector3 a, Vector3 b) => GridPathfinder.IsWorldSegmentWalkable(grid, a.x, a.y, b.x, b.y);
        }
    }
}
