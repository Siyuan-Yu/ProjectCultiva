using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Data.Bootstrap;
using CoreEventType = XianXia.Core.Events.EventType;

namespace XianXia.Tests
{
    public sealed class RecruitmentPhaseDTests
    {
        [Test]
        public void Recruit_FailsBelowThreshold_SucceedsWhenWilling()
        {
            var world = new SimulationWorld();
            var recruiter = world.Entities.CreateCharacter(new DefinitionId("base", "r"), "招").Value;
            var target = world.Entities.CreateCharacter(new DefinitionId("base", "t"), "应").Value;
            recruiter.Get<FactionMembershipComponent>().Assign(
                SocialAlphaConstants.OpeningFactionId,
                FactionRoleKind.LaborDisciple);

            var recruit = new RecruitService();
            Assert.IsTrue(recruit.TryRecruit(world, recruiter.Id, target.Id).IsFailure);

            Assert.IsTrue(new RelationshipService().Record(
                world, target.Id, recruiter.Id, SocialAlphaConstants.RecruitMinScore, "warmup").IsSuccess);

            Assert.IsTrue(recruit.TryRecruit(world, recruiter.Id, target.Id).IsSuccess);
            Assert.IsTrue(target.Get<FactionMembershipComponent>().IsAffiliated);
            Assert.AreEqual(FactionRoleKind.Member, target.Get<FactionMembershipComponent>().Role);
            Assert.AreEqual(
                SocialAlphaConstants.OpeningFactionId,
                target.Get<FactionMembershipComponent>().FactionId);
            Assert.IsTrue(world.Events.Drain().Exists(e => e.Type == CoreEventType.FactionMembershipChanged));
        }

        [Test]
        public void Leave_ClearsMembership_KeepsLedger()
        {
            var world = new SimulationWorld();
            var a = world.Entities.CreateCharacter(new DefinitionId("base", "a"), "甲").Value;
            var b = world.Entities.CreateCharacter(new DefinitionId("base", "b"), "乙").Value;
            a.Get<FactionMembershipComponent>().Assign(
                SocialAlphaConstants.OpeningFactionId,
                FactionRoleKind.LaborDisciple);
            Assert.IsTrue(new RelationshipService().Record(world, a.Id, b.Id, 12, "bond").IsSuccess);

            Assert.IsTrue(new RecruitService().TryLeave(world, a.Id).IsSuccess);
            Assert.IsFalse(a.Get<FactionMembershipComponent>().IsAffiliated);
            Assert.AreEqual(12, world.Relationships.Score(a.Id, b.Id));
        }

        [Test]
        public void PlayableDay_HasLaborFaction_AndRecruitableNpc()
        {
            var package = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));
            var started = new PlayableDayBootstrap().Start(package);
            Assert.IsTrue(started.IsSuccess, started.IsFailure ? started.Error.ToString() : "");

            foreach (var id in started.Value.CharacterIds)
            {
                Assert.IsTrue(started.Value.World.Entities.TryGet(id, out var e));
                var mem = e.Get<FactionMembershipComponent>();
                Assert.IsTrue(mem.IsAffiliated);
                Assert.AreEqual(FactionRoleKind.LaborDisciple, mem.Role);
            }

            Assert.IsFalse(started.Value.RecruitableNpcId.IsNone);
            Assert.IsTrue(started.Value.World.Entities.TryGet(
                started.Value.RecruitableNpcId, out var npc));
            Assert.IsFalse(npc.Get<FactionMembershipComponent>().IsAffiliated);
        }
    }
}
