using System;
using System.Collections.Generic;
using XianXia.Core.Bootstrap;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Events;
using XianXia.Core.Exploration;
using XianXia.Core.Random;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Opportunity
{
    public sealed class WorldOpportunityDriver
    {
        const int PositionAttempts = 64;
        const int TemplateAttempts = 64;
        const float MinimumOpportunitySpacing = 1.5f;
        ulong _lastExpiryDay = ulong.MaxValue;

        /// <summary>Explicit LevelTester acceptance entry; does not alter normal director density.</summary>
        public static Result SpawnAcceptanceInstances(SimulationWorld world, string definitionId, int count,
            out string summary)
        {
            summary = string.Empty;
            if (world == null || count <= 0 || !world.WorldOpportunities.TryGetSpec(definitionId, out var spec))
                return Result.Failure(ErrorCode.InvalidArgument, "Acceptance opportunity request invalid.", definitionId);
            var party = world.Strategic?.PlayerPartyContext;
            if (!PlayerPartyWorldLocationQuery.TryResolve(world, party, out var location) ||
                !string.Equals(location.SurfaceId, spec.SurfaceId, StringComparison.Ordinal))
                return Result.Failure(ErrorCode.InvalidOperation, "Player is not on the opportunity surface.", spec.SurfaceId);
            var day = DayClock.FromWorldTick(world.Tick).DayIndex;
            var existingCount = world.WorldOpportunities.CountActive(definitionId);
            if (existingCount > count)
                return Result.Failure(ErrorCode.InvalidOperation, "More acceptance instances are already active.", definitionId);
            for (var i = existingCount; i < count; i++)
            {
                if (!TrySpawn(world, spec, location.WorldPosition, day))
                    return Result.Failure(ErrorCode.InvalidOperation, "Could not place acceptance opportunity.", definitionId);
            }
            var created = new List<string>();
            foreach (var pair in world.WorldOpportunities.ActiveInstances)
                if (string.Equals(pair.Value.OpportunityDefinitionId, definitionId, StringComparison.Ordinal))
                {
                    var value = pair.Value;
                    if (value.SpawnKind == WorldOpportunitySpawnKind.WorldObject)
                        created.Add(value.InstanceId + " / " + value.WorldObjectInstanceId + " / " + value.SurfaceId +
                                    " / (" + value.WorldX.ToString("0.00") + ", " + value.WorldY.ToString("0.00") +
                                    ") / " + value.DiscoveryMode + " / Discovered=" + value.IsDiscovered);
                    else if (world.WorldPresence.TryGet(value.SpawnedEntityId, out var presence))
                        created.Add(value.InstanceId + " / Entity " + value.SpawnedEntityId.Value +
                                    " / (" + presence.ContinuousWorldPosition.X.ToString("0.00") + ", " +
                                    presence.ContinuousWorldPosition.Y.ToString("0.00") + ")");
                }
            created.Sort(StringComparer.Ordinal);
            summary = string.Join("；", created);
            return Result.Success();
        }

        public static int ClearAcceptanceInstances(SimulationWorld world, params string[] definitionIds)
        {
            if (world == null || definitionIds == null) return 0;
            var wanted = new HashSet<string>(definitionIds, StringComparer.Ordinal);
            var ids = new List<string>();
            foreach (var instance in world.WorldOpportunities.ActiveInstances.Values)
                if (wanted.Contains(instance.OpportunityDefinitionId)) ids.Add(instance.InstanceId);
            for (var i = 0; i < ids.Count; i++) ResolveCurrent(world, ids[i]);
            return ids.Count;
        }

        public void Tick(SimulationWorld world)
        {
            if (world?.WorldOpportunities == null) return;
            var day = DayClock.FromWorldTick(world.Tick).DayIndex;
            if (_lastExpiryDay != day)
            {
                _lastExpiryDay = day;
                Expire(world, day);
            }
            CleanupRemoved(world, day);

            var party = world.Strategic?.PlayerPartyContext;
            if (!PlayerPartyWorldLocationQuery.TryResolve(world, party, out var location)) return;
            var surfaceId = location.SurfaceId;
            if (world.WorldOpportunities.WasRefreshedToday(surfaceId, day)) return;
            world.WorldOpportunities.MarkRefreshed(surfaceId, day);
            Refill(world, party, surfaceId, location.WorldPosition, day);
        }

        void Refill(SimulationWorld world, PlayerPartyRuntime party, string surfaceId, WorldVec2 playerPosition, ulong day)
        {
            var board = world.WorldOpportunities;
            if (!board.TryGetDirectorForSurface(surfaceId, out var director)) return;
            var active = board.CountActiveOnSurface(surfaceId);
            if (active >= director.TargetActiveMin) return;
            var target = director.TargetActiveMin;
            if (director.TargetActiveMax > director.TargetActiveMin)
                target = world.Random.NextInt(director.TargetActiveMin, director.TargetActiveMax + 1);
            var actor = party != null && party.HasActive ? party.ActiveCharacterId : EntityId.None;
            var attempts = 0;
            while (active < target && attempts++ < TemplateAttempts)
            {
                var eligible = EligibleSpecs(world, surfaceId, actor);
                if (eligible.Count == 0) break;
                var spec = PickSpec(world, eligible);
                if (spec == null || !TrySpawn(world, spec, playerPosition, day)) continue;
                active++;
            }
        }

        static List<WorldOpportunitySpec> EligibleSpecs(SimulationWorld world, string surfaceId, EntityId actor)
        {
            var result = new List<WorldOpportunitySpec>();
            foreach (var spec in world.WorldOpportunities.Specs.Values)
            {
                if (spec == null || spec.Weight <= 0 || spec.MaxActive <= 0 ||
                    !string.Equals(spec.SurfaceId, surfaceId, StringComparison.Ordinal) ||
                    world.WorldOpportunities.CountActive(spec.Id) >= spec.MaxActive ||
                    !ContentConditionEvaluator.AllPass(world, actor, spec.Conditions)) continue;
                result.Add(spec);
            }
            return result;
        }

        static WorldOpportunitySpec PickSpec(SimulationWorld world, List<WorldOpportunitySpec> specs)
        {
            var index = WeightedRandomPicker.PickIndex(specs.Count, i => specs[i]?.Weight ?? 0, world.Random);
            return index >= 0 ? specs[index] : null;
        }

        static WorldOpportunityNpcCandidate PickNpc(SimulationWorld world, WorldOpportunitySpec spec)
        {
            var index = WeightedRandomPicker.PickIndex(
                spec.NpcCandidates.Count,
                i => spec.NpcCandidates[i]?.Weight ?? 0,
                world.Random);
            return index >= 0 ? spec.NpcCandidates[index] : null;
        }

        static bool TrySpawn(SimulationWorld world, WorldOpportunitySpec spec, WorldVec2 playerPosition, ulong day)
        {
            if (!TryFindPosition(world, spec, playerPosition, out var position)) return false;
            if (string.Equals(spec.SpawnKind, WorldOpportunitySpawnKind.WorldObject, StringComparison.Ordinal))
                return TrySpawnWorldObject(world, spec, position, day);
            var npc = PickNpc(world, spec);
            if (npc?.Spawn == null) return false;
            var spawned = GameStartBootstrap.SpawnIntoWorld(world, npc.Spawn);
            if (spawned.IsFailure) return false;
            var entity = spawned.Value;
            if (!entity.TryGet<EntityLocationComponent>(out var location))
            {
                location = new EntityLocationComponent();
                if (entity.AddComponent(location).IsFailure)
                {
                    world.Entities.MarkRemoved(entity.Id);
                    return false;
                }
            }
            location.ClearPresence();
            world.WorldPresence.SetAtWorldPosition(entity.Id, position, spec.SurfaceId);
            var instance = new WorldOpportunityInstance
            {
                InstanceId = world.WorldOpportunities.AllocateInstanceId(),
                OpportunityDefinitionId = spec.Id,
                SurfaceId = spec.SurfaceId,
                SpawnKind = WorldOpportunitySpawnKind.Npc,
                SpawnedEntityId = entity.Id,
                CreatedDayIndex = day,
                ExpireDayIndexExclusive = day + (ulong)Math.Max(1, spec.DurationDays),
                DiscoveryMode = spec.DiscoveryMode,
                IsDiscovered = true
            };
            if (!world.WorldOpportunities.AddInstance(instance))
            {
                RemoveEntityFromWorld(world, entity.Id);
                return false;
            }
            if (string.Equals(spec.DiscoveryMode, WorldOpportunityDiscoveryMode.PublicNotice, StringComparison.Ordinal))
            {
                world.WorldActivities.CreateActive(
                    WorldActivitySourceKind.WorldOpportunity,
                    instance.InstanceId,
                    string.IsNullOrWhiteSpace(spec.PublicNoticeTitle) ? spec.Name : spec.PublicNoticeTitle,
                    spec.PublicNoticeText,
                    day);
                world.Events.Publish(EventType.WorldOpportunityNotice, world.Tick, target: entity.Id, payload: spec.PublicNoticeText);
            }
            return true;
        }

        static bool TrySpawnWorldObject(SimulationWorld world, WorldOpportunitySpec spec, WorldVec2 position, ulong day)
        {
            var instanceId = world.WorldOpportunities.AllocateInstanceId();
            var instance = new WorldOpportunityInstance
            {
                InstanceId = instanceId,
                OpportunityDefinitionId = spec.Id,
                SurfaceId = spec.SurfaceId,
                SpawnKind = WorldOpportunitySpawnKind.WorldObject,
                SpawnedEntityId = EntityId.None,
                WorldObjectInstanceId = WorldOpportunityBoard.WorldObjectIdFor(instanceId),
                WorldX = position.X,
                WorldY = position.Y,
                CreatedDayIndex = day,
                ExpireDayIndexExclusive = day + (ulong)Math.Max(1, spec.DurationDays),
                DiscoveryMode = spec.DiscoveryMode,
                IsDiscovered = !string.Equals(spec.DiscoveryMode, WorldOpportunityDiscoveryMode.HiddenUntilDiscovered, StringComparison.Ordinal)
            };
            if (!world.WorldOpportunities.AddInstance(instance)) return false;
            if (string.Equals(spec.DiscoveryMode, WorldOpportunityDiscoveryMode.PublicNotice, StringComparison.Ordinal))
            {
                world.WorldActivities.CreateActive(WorldActivitySourceKind.WorldOpportunity, instance.InstanceId,
                    string.IsNullOrWhiteSpace(spec.PublicNoticeTitle) ? spec.Name : spec.PublicNoticeTitle,
                    spec.PublicNoticeText, day);
                world.Events.Publish(EventType.WorldOpportunityNotice, world.Tick, payload: spec.PublicNoticeText);
            }
            return true;
        }

        static bool TryFindPosition(SimulationWorld world, WorldOpportunitySpec spec, WorldVec2 player, out WorldVec2 position)
        {
            position = default;
            if (!world.SurfaceGround.TryGet(spec.SurfaceId, out var ground) || ground == null) return false;
            var min = Math.Max(0f, spec.MinPlayerDistanceWorld);
            var max = Math.Max(min, spec.MaxPlayerDistanceWorld);
            for (var attempt = 0; attempt < PositionAttempts; attempt++)
            {
                var distance = min + (float)world.Random.NextDouble() * (max - min);
                var angle = world.Random.NextDouble() * Math.PI * 2.0;
                var candidate = new WorldVec2(
                    player.X + (float)Math.Cos(angle) * distance,
                    player.Y + (float)Math.Sin(angle) * distance);
                var actualDistance = WorldVec2.Distance(player, candidate);
                if (actualDistance < min || actualDistance > max || !ground.Contains(candidate.X, candidate.Y) ||
                    !ground.IsWalkable(candidate.X, candidate.Y)) continue;
                if (!spec.AllowInsideWorldSite && !string.IsNullOrEmpty(WorldSitePhysicalRegionQuery.ResolveSiteIdOrEmpty(world, candidate))) continue;
                var separated = true;
                foreach (var other in world.WorldOpportunities.ActiveInstances.Values)
                {
                    if (!string.Equals(other.SurfaceId, spec.SurfaceId, StringComparison.Ordinal)) continue;
                    WorldVec2 otherPosition;
                    if (other.SpawnKind == WorldOpportunitySpawnKind.WorldObject)
                        otherPosition = new WorldVec2(other.WorldX, other.WorldY);
                    else if (world.WorldPresence.TryGet(other.SpawnedEntityId, out var presence) && presence != null &&
                             presence.HasContinuousWorldPosition) otherPosition = presence.ContinuousWorldPosition;
                    else continue;
                    if (WorldVec2.Distance(candidate, otherPosition) < MinimumOpportunitySpacing)
                    {
                        separated = false;
                        break;
                    }
                }
                if (!separated) continue;
                position = candidate;
                return true;
            }
            return false;
        }

        static void Expire(SimulationWorld world, ulong day)
        {
            var expired = new List<string>();
            foreach (var instance in world.WorldOpportunities.ActiveInstances.Values)
                if (instance.ExpireDayIndexExclusive <= day) expired.Add(instance.InstanceId);
            for (var i = 0; i < expired.Count; i++)
            {
                if (!world.WorldOpportunities.ActiveInstances.TryGetValue(expired[i], out var instance)) continue;
                if (instance.SpawnKind == WorldOpportunitySpawnKind.Npc)
                    new QuestService().FailIssuerCommissions(
                        world, instance.SpawnedEntityId, instance.InstanceId, "issuer_opportunity_expired");
                if (world.WorldOpportunities.TryGetSpec(instance.OpportunityDefinitionId, out var spec))
                    ContentOutcomeApplier.ApplyAll(world, instance.SpawnedEntityId, spec.ExpireOutcomes);
                if (instance.SpawnKind == WorldOpportunitySpawnKind.Npc)
                    RemoveEntityFromWorld(world, instance.SpawnedEntityId);
                world.WorldActivities.ResolveSource(
                    WorldActivitySourceKind.WorldOpportunity, instance.InstanceId, day);
                world.WorldOpportunities.RemoveInstance(instance.InstanceId);
            }
        }

        static void CleanupRemoved(SimulationWorld world, ulong day)
        {
            var stale = new List<string>();
            foreach (var instance in world.WorldOpportunities.ActiveInstances.Values)
                if (instance.SpawnKind == WorldOpportunitySpawnKind.Npc &&
                    (!world.Entities.TryGet(instance.SpawnedEntityId, out var entity) ||
                    (entity.TryGet<LifecycleComponent>(out var life) &&
                     (life.State == LifecycleState.Dead || life.State == LifecycleState.Removed))))
                    stale.Add(instance.InstanceId);
            for (var i = 0; i < stale.Count; i++)
            {
                if (!world.WorldOpportunities.ActiveInstances.TryGetValue(stale[i], out var instance)) continue;
                new QuestService().FailIssuerCommissions(
                    world, instance.SpawnedEntityId, instance.InstanceId, "issuer_dead_or_removed");
                world.WorldActivities.ResolveSource(
                    WorldActivitySourceKind.WorldOpportunity, stale[i], day);
                world.WorldOpportunities.RemoveInstance(stale[i]);
            }
        }

        public static Result ResolveCurrent(SimulationWorld world, string opportunityInstanceId)
        {
            if (world == null || !world.WorldOpportunities.TryGetInstance(opportunityInstanceId, out var instance))
                return Result.Failure(ErrorCode.NotFound, "WorldOpportunity instance is no longer active.", opportunityInstanceId);
            var day = DayClock.FromWorldTick(world.Tick).DayIndex;
            if (instance.SpawnKind == WorldOpportunitySpawnKind.Npc)
                RemoveEntityFromWorld(world, instance.SpawnedEntityId);
            world.WorldActivities.ResolveSource(WorldActivitySourceKind.WorldOpportunity, instance.InstanceId, day);
            world.WorldOpportunities.RemoveInstance(instance.InstanceId);
            world.Events.Publish(EventType.WorldOpportunityResolved, world.Tick,
                target: instance.SpawnedEntityId, payload: instance.InstanceId);
            return Result.Success();
        }

        static void RemoveEntityFromWorld(SimulationWorld world, EntityId entityId)
        {
            world.BackgroundCharacterTravel.Remove(entityId);
            world.WorldPresence.Remove(entityId);
            if (world.Entities.TryGet(entityId, out var entity) && entity.TryGet<EntityLocationComponent>(out var location))
                location.ClearPresence();
            if (world.Entities.TryGet(entityId, out var existing) &&
                existing.TryGet<LifecycleComponent>(out var life) && life.State != LifecycleState.Removed)
                world.Entities.MarkRemoved(entityId);
        }
    }
}
