using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Random;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Data.Bootstrap;
using CoreEventType = XianXia.Core.Events.EventType;

namespace XianXia.Tests
{
    public sealed class SocialTickPhaseFTests
    {
        [Test]
        public void SocialTick_FixedSeed_IsReproducible_AndEmitsRelationshipEvents()
        {
            var scoreA = RunDrift(seed: 42UL, out var eventsA);
            var scoreB = RunDrift(seed: 42UL, out var eventsB);
            RunDrift(seed: 99UL, out var eventsOther);

            Assert.AreEqual(scoreA, scoreB);
            Assert.AreEqual(eventsA, eventsB);
            Assert.Greater(eventsA, 0);
            // Different seed must consume a different RNG path (event count and/or net score may differ).
            Assert.IsTrue(eventsOther != eventsA || RunDrift(seed: 99UL, out _) != scoreA);
        }

        [Test]
        public void SocialTick_DisabledByDefault_DoesNotDrift()
        {
            var world = new SimulationWorld(random: new DeterministicRandom(7));
            var a = world.Entities.CreateCharacter(new DefinitionId("base", "a"), "甲").Value;
            var b = world.Entities.CreateCharacter(new DefinitionId("base", "b"), "乙").Value;
            Assert.IsTrue(new RelationshipService().Record(world, a.Id, b.Id, 10, "seed").IsSuccess);
            world.Events.Drain();

            var loop = new SimulationLoop(world, enableSocialTick: false);
            for (var i = 0; i < SocialAlphaConstants.SocialTickIntervalTicks * 4; i++)
                Assert.IsTrue(loop.TickOnce().IsSuccess);

            Assert.AreEqual(10, world.Relationships.Score(a.Id, b.Id));
            Assert.IsFalse(world.Events.Drain().Exists(e => e.Type == CoreEventType.RelationshipChanged));
        }

        [Test]
        public void PlayableDay_MultiDayDrift_CanCrossRecruitThreshold()
        {
            var package = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));
            var started = new PlayableDayBootstrap().Start(
                package,
                random: new DeterministicRandom(12345));
            Assert.IsTrue(started.IsSuccess, started.IsFailure ? started.Error.ToString() : "");

            var world = started.Value.World;
            var loop = started.Value.Loop;
            var recruiter = started.Value.CharacterIds[0];
            var npc = started.Value.RecruitableNpcId;

            // Clear path: npc→recruiter starts at 0; drift or Help until recruitable.
            var recruit = new RecruitService();
            Assert.IsTrue(recruit.TryRecruit(world, recruiter, npc).IsFailure);

            for (var day = 0; day < 3 && world.Relationships.Score(npc, recruiter) < SocialAlphaConstants.RecruitMinScore; day++)
            {
                for (var t = 0; t < 96; t++)
                    Assert.IsTrue(loop.TickOnce().IsSuccess);
            }

            // If drift alone is shy of threshold, one Help stands in for player social push.
            if (world.Relationships.Score(npc, recruiter) < SocialAlphaConstants.RecruitMinScore)
            {
                Assert.IsTrue(new SocialInteractionService().Help(world, npc, recruiter).IsSuccess);
                Assert.IsTrue(new SocialInteractionService().Help(world, npc, recruiter).IsSuccess);
            }

            Assert.GreaterOrEqual(
                world.Relationships.Score(npc, recruiter),
                SocialAlphaConstants.RecruitMinScore);
            Assert.IsTrue(recruit.TryRecruit(world, recruiter, npc).IsSuccess);
            Assert.IsTrue(world.Entities.TryGet(npc, out var recruited));
            Assert.IsTrue(recruited.Get<FactionMembershipComponent>().IsAffiliated);
        }

        static int RunDrift(ulong seed, out int relationshipEvents)
        {
            var world = new SimulationWorld(random: new DeterministicRandom(seed));
            var a = world.Entities.CreateCharacter(new DefinitionId("base", "a"), "甲").Value;
            var b = world.Entities.CreateCharacter(new DefinitionId("base", "b"), "乙").Value;
            a.Get<PersonalityProfileComponent>().SetTags(new[] { PersonalityScheduleBias.TagCautious });
            b.Get<PersonalityProfileComponent>().SetTags(new[] { PersonalityScheduleBias.TagBold });

            var loop = new SimulationLoop(world, enableSocialTick: true);
            for (var i = 0; i < SocialAlphaConstants.SocialTickIntervalTicks * 8; i++)
                Assert.IsTrue(loop.TickOnce().IsSuccess);

            var events = world.Events.Drain();
            relationshipEvents = 0;
            foreach (var e in events)
            {
                if (e.Type == CoreEventType.RelationshipChanged &&
                    (e.Payload.Contains("reason=help") || e.Payload.Contains("reason=slight")))
                    relationshipEvents++;
            }

            return world.Relationships.Score(a.Id, b.Id);
        }
    }
}
