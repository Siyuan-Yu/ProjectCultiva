using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;

namespace XianXia.Core.Social
{
    /// <summary>
    /// VS0.5 Phase F: low-frequency abstract social drift (no map／AI).
    /// Picks a living pair and may Help or Slight through SocialInteractionService.
    /// </summary>
    public sealed class SocialTickDriver
    {
        readonly SocialInteractionService _interactions;

        public SocialTickDriver(SocialInteractionService interactions = null)
        {
            _interactions = interactions ?? new SocialInteractionService();
        }

        public void Tick(SimulationWorld world)
        {
            if (world == null)
                return;

            var interval = SocialAlphaConstants.SocialTickIntervalTicks;
            if (interval <= 0)
                return;
            if (world.Tick.Value == 0 || world.Tick.Value % (ulong)interval != 0)
                return;

            var candidates = CollectParticipants(world);
            if (candidates.Count < 2)
                return;

            if (world.Random.NextInt(0, 100) >= SocialAlphaConstants.SocialTickInteractChancePercent)
                return;

            var actorIndex = world.Random.NextInt(0, candidates.Count);
            var targetIndex = world.Random.NextInt(0, candidates.Count - 1);
            if (targetIndex >= actorIndex)
                targetIndex++;

            var actor = candidates[actorIndex];
            var target = candidates[targetIndex];

            if (ShouldHelp(world, actor))
                _interactions.Help(world, actor, target);
            else
                _interactions.Slight(world, actor, target);
        }

        static List<EntityId> CollectParticipants(SimulationWorld world)
        {
            var list = new List<EntityId>();
            foreach (var entity in world.Entities.All)
            {
                if ((entity.Tags & (EntityTag.Character | EntityTag.Npc)) == 0)
                    continue;
                if (entity.TryGet<LifecycleComponent>(out var life) &&
                    life.State != LifecycleState.Alive)
                    continue;
                list.Add(entity.Id);
            }

            return list;
        }

        static bool ShouldHelp(SimulationWorld world, EntityId actorId)
        {
            if (!world.Entities.TryGet(actorId, out var entity) ||
                !entity.TryGet<PersonalityProfileComponent>(out var profile))
            {
                return world.Random.NextInt(0, 2) == 0;
            }

            var bold = profile.HasTag(PersonalityScheduleBias.TagBold);
            var cautious = profile.HasTag(PersonalityScheduleBias.TagCautious);
            if (bold && !cautious)
                return world.Random.NextInt(0, 100) < SocialAlphaConstants.SocialTickBoldHelpChancePercent;
            if (cautious && !bold)
                return world.Random.NextInt(0, 100) < SocialAlphaConstants.SocialTickCautiousHelpChancePercent;
            return world.Random.NextInt(0, 2) == 0;
        }
    }
}
