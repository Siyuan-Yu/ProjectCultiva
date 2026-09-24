using System;
using System.Collections.Generic;
using XianXia.Core.Combat;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;
using XianXia.Core.World.Surface;
using XianXia.Data.Content;

namespace XianXia.Unity.Host
{
    /// <summary>SUCCESSION-01 deterministic LevelTester-only acceptance fixture.</summary>
    public static class LevelTesterSuccessionCheats
    {
        const string AcceptanceSquadId = "squad:acceptance:succession-b";

        public static CharacterCombatCheatResult TryPrepare(PlayableHostBootstrap bootstrap)
        {
            var session = bootstrap?.Session;
            var world = session?.World;
            var party = session?.PlayerParty;
            if (world == null || party == null || !party.HasActive)
                return Failed("当前 PlayerParty 没有可用主控，请先重置 LevelTester 会话。");
            if (!DefinitionId.TryParse(world.PlayerPartyTravel.SurfaceId, out var surfaceId) ||
                session.Registry == null ||
                !session.Registry.TryGetOutdoorSurface(surfaceId, out var surface) || surface == null ||
                !world.SurfaceGround.TryGet(surface.SurfaceId, out var navigation) || navigation == null)
                return Failed("当前 Continuous Surface authority 尚未准备好。");

            var candidates = new List<EntityId>();
            foreach (var entity in world.Entities.All)
            {
                if (entity == null || party.IsMember(entity.Id) ||
                    (entity.Tags & (EntityTag.Character | EntityTag.Npc)) == 0 ||
                    !entity.TryGet<FactionMembershipComponent>(out var faction) ||
                    faction == null || !faction.IsAffiliated ||
                    !string.Equals(faction.FactionId, world.Strategic.PlayerFactionId,
                        StringComparison.Ordinal) ||
                    !entity.TryGet<LifecycleComponent>(out var life) ||
                    life.State != LifecycleState.Alive)
                    continue;
                candidates.Add(entity.Id);
            }
            candidates.Sort((left, right) => left.Value.CompareTo(right.Value));
            if (candidates.Count < 2)
                return Failed("需要至少两名存活的玩家势力非 Party 人物。");

            var candidateA = candidates[0];
            var candidateB = candidates[1];
            if (!CharacterWorldPresenceQuery.TryResolve(world, candidateA, out var aPresence) ||
                !aPresence.HasWorldPosition ||
                !TryFindFarWalkablePoint(surface, navigation,
                    world.PlayerPartyTravel.WorldPosition, out var farPoint))
                return Failed("无法为 A/B 取得两个合法的 Continuous Surface 验收位置。");

            BackgroundCharacterTravelService.CancelTravelIfAny(world, candidateA);
            BackgroundCharacterTravelService.CancelTravelIfAny(world, candidateB);
            SquadMembershipService.LeaveToSingleton(world, candidateA);
            EnsureRealm(world, candidateA, RealmStage.QiRefining);
            EnsureRealm(world, candidateB, RealmStage.Foundation);
            world.WorldPresence.SetAtWorldPosition(
                candidateA, aPresence.WorldPosition, aPresence.SurfaceId);

            var sourceDescription = "singleton";
            if (TryFindEscort(world, party, candidateA, candidateB, out var escort))
            {
                if (world.Strategic.Squads.TryGet(AcceptanceSquadId, out var stale))
                {
                    // A reset normally removes this fixture. Refuse to overwrite a live fixture.
                    return Failed("验收 B 小队已存在（" + stale.SquadId + "），请重置会话后再准备。");
                }
                SquadMembershipService.LeaveToSingleton(world, candidateB);
                SquadMembershipService.LeaveToSingleton(world, escort);
                var created = SquadMembershipService.Create(
                    world, AcceptanceSquadId,
                    new[] { candidateB, escort }, candidateB,
                    SquadCommandKind.SquadWorldMotion,
                    displayName: "继承验收 B 小队",
                    factionId: world.Strategic.PlayerFactionId);
                if (created.IsSuccess)
                {
                    var initialized = SquadWorldMotionService.Initialize(
                        world, AcceptanceSquadId, surface.SurfaceId, farPoint);
                    if (initialized.IsSuccess)
                        sourceDescription = AcceptanceSquadId + "（含护卫 " + escort.Value + "）";
                    else
                    {
                        SquadMembershipService.LeaveToSingleton(world, candidateB);
                        SquadMembershipService.LeaveToSingleton(world, escort);
                        world.WorldPresence.SetAtWorldPosition(candidateB, farPoint, surface.SurfaceId);
                    }
                }
                else
                {
                    world.WorldPresence.SetAtWorldPosition(candidateB, farPoint, surface.SurfaceId);
                }
            }
            else
            {
                SquadMembershipService.LeaveToSingleton(world, candidateB);
                world.WorldPresence.SetAtWorldPosition(candidateB, farPoint, surface.SurfaceId);
            }

            if (!CharacterWorldPresenceQuery.TryResolve(world, candidateA, out aPresence) ||
                !CharacterWorldPresenceQuery.TryResolve(world, candidateB, out var bPresence))
                return Failed("准备后无法重新解析 A/B 位置。");

            return new CharacterCombatCheatResult(true,
                "成功：SUCCESSION-01 候选已准备\n" +
                Describe("A", world, candidateA, aPresence) + "\n" +
                Describe("B", world, candidateB, bPresence) + "\n" +
                "B SourceSquad=" + sourceDescription +
                "；B 应以更高战力胜出并触发远距离重锚。");
        }

        public static CharacterCombatCheatResult TryKillCurrentParty(
            PlayableHostBootstrap bootstrap)
        {
            var world = bootstrap?.Session?.World;
            var party = bootstrap?.Session?.PlayerParty;
            if (world == null || party == null || party.Count == 0)
                return Failed("当前 PlayerParty 不可用。");
            if (world.Strategic.CharacterEncounter != null)
                return Failed("请先完成当前 CharacterEncounter，再执行全灭验收。");

            var members = new List<EntityId>(party.Members);
            if (world.LocalMap.IsActive)
                HostSnapshotLocalPlacementCaptureSync
                    .FlushActiveSeparateSpaceCharacterPlacementsFromViews(bootstrap);
            var killed = 0;
            for (var i = 0; i < members.Count; i++)
            {
                if (!world.Entities.TryGet(members[i], out var entity) || entity == null ||
                    !entity.TryGet<LifecycleComponent>(out var life) || life == null)
                    continue;
                if (life.State == LifecycleState.Alive &&
                    !CombatLifeStateService.TryEnterIncapacitated(world, entity))
                    continue;
                if (life.IsIncapacitated &&
                    CombatLifeStateService.TryConfirmDeath(
                        world, EntityId.None, entity, out var confirmed) && confirmed)
                    killed++;
                else if (life.IsDead || life.IsRemoved)
                    killed++;
            }

            bootstrap.PlayerPartyController?.RefreshActiveControlAfterLifeStateChange();
            return new CharacterCombatCheatResult(
                killed == members.Count,
                (killed == members.Count ? "成功：" : "失败：") +
                "已通过正式 Lifecycle API 确认阵亡 " + killed + "/" + members.Count +
                "；继承检查已触发。");
        }

        static bool TryFindEscort(
            XianXia.Core.Simulation.SimulationWorld world,
            XianXia.Core.World.PlayerPartyRuntime party,
            EntityId candidateA,
            EntityId candidateB,
            out EntityId escort)
        {
            escort = EntityId.None;
            foreach (var entity in world.Entities.All)
            {
                if (entity == null || entity.Id == candidateA || entity.Id == candidateB ||
                    party.IsMember(entity.Id) ||
                    (entity.Tags & (EntityTag.Character | EntityTag.Npc)) == 0 ||
                    !entity.TryGet<FactionMembershipComponent>(out var faction) ||
                    faction == null || !faction.IsAffiliated ||
                    !string.Equals(faction.FactionId, world.Strategic.PlayerFactionId,
                        StringComparison.Ordinal) ||
                    !entity.TryGet<LifecycleComponent>(out var life) ||
                    life.State != LifecycleState.Alive)
                    continue;
                if (!world.Strategic.Squads.TryGetForCharacter(entity.Id, out var squad) ||
                    squad.MemberCharacterIds.Count != 1 ||
                    SquadWorldMotionService.OwnsCharacter(world, entity.Id))
                    continue;
                escort = entity.Id;
                return true;
            }
            return false;
        }

        static bool TryFindFarWalkablePoint(
            OutdoorWorldSurfaceDefinition surface,
            SurfaceGroundNavigation navigation,
            WorldVec2 origin,
            out WorldVec2 point)
        {
            point = default;
            var found = false;
            var bestDistance = 0f;
            for (var i = 0; i < surface.Chunks.Count; i++)
            {
                var chunk = surface.Chunks[i];
                if (chunk == null)
                    continue;
                var center = new WorldVec2(
                    surface.OriginWorldX + (chunk.Coord.X + .5f) * surface.ChunkWidth,
                    surface.OriginWorldY + (chunk.Coord.Y + .5f) * surface.ChunkHeight);
                var distance = (center.X - origin.X) * (center.X - origin.X) +
                               (center.Y - origin.Y) * (center.Y - origin.Y);
                if (found && distance <= bestDistance)
                    continue;
                if (!TryFindWalkableNear(navigation, center, surface.ChunkWidth,
                        surface.ChunkHeight, out var walkable))
                    continue;
                point = walkable;
                bestDistance = distance;
                found = true;
            }
            return found;
        }

        static bool TryFindWalkableNear(
            SurfaceGroundNavigation navigation,
            WorldVec2 center,
            float width,
            float height,
            out WorldVec2 point)
        {
            point = default;
            var maxRadius = Math.Max(1,
                (int)Math.Ceiling(Math.Max(width, height) / navigation.CellSize * .5f));
            for (var radius = 0; radius <= maxRadius; radius++)
            {
                for (var y = -radius; y <= radius; y++)
                for (var x = -radius; x <= radius; x++)
                {
                    if (radius > 0 && Math.Abs(x) != radius && Math.Abs(y) != radius)
                        continue;
                    var candidate = new WorldVec2(
                        center.X + x * navigation.CellSize,
                        center.Y + y * navigation.CellSize);
                    if (!navigation.IsWalkable(candidate.X, candidate.Y))
                        continue;
                    point = candidate;
                    return true;
                }
            }
            return false;
        }

        static void EnsureRealm(
            XianXia.Core.Simulation.SimulationWorld world,
            EntityId id,
            RealmStage realm)
        {
            if (!world.Entities.TryGet(id, out var entity) || entity == null)
                return;
            if (!entity.TryGet<CultivationComponent>(out var cultivation))
            {
                cultivation = new CultivationComponent();
                entity.AddComponent(cultivation);
            }
            cultivation.Realm = realm;
            cultivation.MinorStage = 0;
        }

        static string Describe(
            string label,
            XianXia.Core.Simulation.SimulationWorld world,
            EntityId id,
            CharacterWorldPresenceQuery.ResolvedPresence presence) =>
            label + " EntityId=" + id.Value +
            " Power=" + CombatPowerCalculator.ForEntity(world, id) +
            " Surface=" + presence.SurfaceId +
            " Position=(" + presence.WorldPosition.X.ToString("0.###") + "," +
            presence.WorldPosition.Y.ToString("0.###") + ")";

        static CharacterCombatCheatResult Failed(string reason) =>
            new CharacterCombatCheatResult(false, "失败：" + reason);
    }
}
