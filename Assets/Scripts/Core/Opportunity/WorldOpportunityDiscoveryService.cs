using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Events;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Opportunity
{
    /// <summary>Domain-only proximity discovery for hidden dynamic opportunity objects.</summary>
    public sealed class WorldOpportunityDiscoveryService
    {
        public int Tick(SimulationWorld world)
        {
            var party = world?.Strategic?.PlayerPartyContext;
            if (party == null) return 0;
            var discovered = new List<WorldOpportunityInstance>();
            foreach (var instance in world.WorldOpportunities.ActiveInstances.Values)
            {
                if (instance.SpawnKind != WorldOpportunitySpawnKind.WorldObject || instance.IsDiscovered ||
                    instance.DiscoveryMode != WorldOpportunityDiscoveryMode.HiddenUntilDiscovered ||
                    !world.WorldOpportunities.TryGetSpec(instance.OpportunityDefinitionId, out var spec) ||
                    spec.DiscoveryRadiusWorld <= 0f) continue;
                if (AnyLivingPartyMemberInRange(world, party, instance, spec.DiscoveryRadiusWorld, out _))
                    discovered.Add(instance);
            }
            for (var i = 0; i < discovered.Count; i++) Discover(world, discovered[i]);
            return discovered.Count;
        }

        static bool AnyLivingPartyMemberInRange(SimulationWorld world, PlayerPartyRuntime party,
            WorldOpportunityInstance instance, float radius, out EntityId discoverer)
        {
            discoverer = EntityId.None;
            var target = new WorldVec2(instance.WorldX, instance.WorldY);
            foreach (var memberId in party.Members)
            {
                if (!world.Entities.TryGet(memberId, out var member) ||
                    (member.Tags & EntityTag.Character) == 0 ||
                    (member.TryGet<LifecycleComponent>(out var life) && life.State != LifecycleState.Alive) ||
                    !CharacterWorldPresenceQuery.TryResolve(world, memberId, out var presence) ||
                    !presence.HasWorldPosition ||
                    !string.Equals(presence.SurfaceId, instance.SurfaceId, StringComparison.Ordinal) ||
                    WorldVec2.Distance(presence.WorldPosition, target) > radius) continue;
                discoverer = memberId;
                return true;
            }
            return false;
        }

        static void Discover(SimulationWorld world, WorldOpportunityInstance instance)
        {
            if (instance == null || instance.IsDiscovered ||
                !world.WorldOpportunities.TryGetSpec(instance.OpportunityDefinitionId, out var spec)) return;
            instance.IsDiscovered = true;
            var title = string.IsNullOrWhiteSpace(spec.DiscoveryNoticeTitle) ? spec.Name : spec.DiscoveryNoticeTitle;
            var body = string.IsNullOrWhiteSpace(spec.DiscoveryNoticeText) ? "你发现了：" + spec.Name + "。" : spec.DiscoveryNoticeText;
            world.WorldActivities.CreateActive(WorldActivitySourceKind.WorldOpportunity, instance.InstanceId,
                title, body, DayClock.FromWorldTick(world.Tick).DayIndex);
            world.Events.Publish(EventType.WorldOpportunityDiscovered, world.Tick, payload: instance.InstanceId);
            world.Events.Publish(EventType.WorldOpportunityNotice, world.Tick, payload: body);
        }
    }
}
