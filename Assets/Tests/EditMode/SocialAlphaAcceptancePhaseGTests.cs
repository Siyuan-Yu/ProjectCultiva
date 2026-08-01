using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Input;
using XianXia.Core.Orders;
using XianXia.Core.Persistence;
using XianXia.Core.Random;
using XianXia.Core.Schedule;
using XianXia.Core.Social;
using XianXia.Data.Bootstrap;
using XianXia.Data.Serialization;
using CoreEventType = XianXia.Core.Events.EventType;

namespace XianXia.Tests
{
    /// <summary>
    /// VS0.5 Phase G: closed-loop Alpha acceptance (personality → relations → recruit → bias → drift).
    /// </summary>
    public sealed class SocialAlphaAcceptancePhaseGTests
    {
        static string BaseGamePath => System.IO.Path.GetFullPath(
            System.IO.Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        [Test]
        public void SocialAlpha_ClosedLoop_Personality_Relations_Recruit_Bias_Drift()
        {
            var started = new PlayableDayBootstrap().Start(
                BaseGamePath,
                random: new DeterministicRandom(20260801));
            Assert.IsTrue(started.IsSuccess, started.IsFailure ? started.Error.ToString() : "");

            var world = started.Value.World;
            var loop = started.Value.Loop;
            var ids = started.Value.CharacterIds;
            Assert.AreEqual(3, ids.Count);
            Assert.IsFalse(started.Value.RecruitableNpcId.IsNone);

            // A — distinct personality tags from Content
            Assert.IsTrue(world.Entities.TryGet(ids[0], out var protagonist));
            Assert.IsTrue(world.Entities.TryGet(ids[2], out var companionB));
            Assert.IsTrue(protagonist.Get<PersonalityProfileComponent>().HasTag("personality_cautious"));
            Assert.IsTrue(companionB.Get<PersonalityProfileComponent>().HasTag("personality_bold"));

            // C — opening relations seeded
            Assert.AreEqual(
                SocialAlphaConstants.OpeningCompanionFavor,
                world.Relationships.Score(ids[0], ids[1]));

            // C — Help／Slight write Ledger
            var social = new SocialInteractionService();
            Assert.IsTrue(social.Help(world, ids[0], ids[1]).IsSuccess);
            Assert.Greater(
                world.Relationships.Score(ids[0], ids[1]),
                SocialAlphaConstants.OpeningCompanionFavor);

            // E — bold vs cautious schedule duration bias (isolated micro-world)
            AssertBoldLongerThanCautious();

            // D — recruit gated by npc→recruiter score
            var npcId = started.Value.RecruitableNpcId;
            var recruit = new RecruitService();
            Assert.IsTrue(recruit.TryRecruit(world, ids[0], npcId).IsFailure);
            Assert.IsTrue(social.Help(world, npcId, ids[0]).IsSuccess);
            Assert.IsTrue(social.Help(world, npcId, ids[0]).IsSuccess);
            Assert.GreaterOrEqual(
                world.Relationships.Score(npcId, ids[0]),
                SocialAlphaConstants.RecruitMinScore);
            Assert.IsTrue(recruit.TryRecruit(world, ids[0], npcId).IsSuccess);
            Assert.IsTrue(world.Entities.TryGet(npcId, out var npc));
            Assert.AreEqual(EntityTag.Npc, npc.Tags);
            Assert.IsTrue(npc.Get<FactionMembershipComponent>().IsAffiliated);

            // F — social tick drift emits relationship events (PlayableDay loop enables it)
            world.Events.Drain();
            for (var i = 0; i < SocialAlphaConstants.SocialTickIntervalTicks * 6; i++)
                Assert.IsTrue(loop.TickOnce().IsSuccess);
            var drifted = world.Events.Drain().Exists(e =>
                e.Type == CoreEventType.RelationshipChanged &&
                (e.Payload.Contains("reason=help") || e.Payload.Contains("reason=slight")));
            Assert.IsTrue(drifted, "Expected social-tick Help/Slight events over several intervals.");

            // Player override still wins over schedule
            Assert.IsTrue(world.Entities.TryGet(ids[0], out var controllable));
            if (!controllable.Get<ActionStateComponent>().HasActiveAction)
                loop.TickOnce();
            var port = new PlayerInputPort(loop);
            Assert.IsTrue(port.Submit(new PlayerCommandRequest(ids[0], PlayerCommandKind.Rest, 2)).IsSuccess);
            Assert.AreEqual(OrderSource.Player, controllable.Get<ActionStateComponent>().ActiveOrderSource);

            // Snapshot schema unchanged; social state intentionally not persisted yet
            Assert.AreEqual(1, WorldSnapshot.CurrentSchemaVersion);
            var snap = new SnapshotService(new JsonSnapshotSerializer()).Capture(world, loop);
            Assert.AreEqual(1, snap.SchemaVersion);
            Assert.IsNull(snap.GetType().GetProperty("Relationships"));
        }

        static void AssertBoldLongerThanCautious()
        {
            var block = new ScheduleBlock(0, 96, ScheduleActivity.Labor, 4);
            var bold = new PersonalityProfileComponent();
            bold.SetTags(new[] { PersonalityScheduleBias.TagBold });
            var cautious = new PersonalityProfileComponent();
            cautious.SetTags(new[] { PersonalityScheduleBias.TagCautious });
            Assert.Greater(
                PersonalityScheduleBias.Apply(block, bold).DurationTicks,
                PersonalityScheduleBias.Apply(block, cautious).DurationTicks);
        }
    }
}
