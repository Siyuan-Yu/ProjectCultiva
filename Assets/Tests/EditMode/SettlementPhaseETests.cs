using System.IO;
using NUnit.Framework;
using XianXia.Core.Content;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Input;
using XianXia.Core.Settlement;
using XianXia.Core.Social;
using XianXia.Data.Bootstrap;
using XianXia.Data.Content;
using CoreEventType = XianXia.Core.Events.EventType;

namespace XianXia.Tests
{
    public sealed class SettlementPhaseETests
    {
        static string BaseGamePath =>
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        [Test]
        public void Loader_LoadsResourcesFacilitiesSettlement()
        {
            var loaded = new ContentPackageLoader().Load(new[] { BaseGamePath });
            Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : "");
            Assert.IsTrue(loaded.Value.Registry.TryGetResource(
                new DefinitionId("base", "resource_rough_wood"), out _));
            Assert.IsTrue(loaded.Value.Registry.TryGetFacility(
                new DefinitionId("base", "facility_meditation_mat"), out _));
            Assert.IsTrue(loaded.Value.Registry.TryGetSettlement(
                new DefinitionId("base", "settlement_qingshi_cave"), out var settlement));
            Assert.AreEqual(1, settlement.FacilityIds.Count);
        }

        [Test]
        public void PlayableDay_HasOpeningSettlement_AndDistinctWorkRoles()
        {
            var started = new PlayableDayBootstrap().Start(BaseGamePath);
            Assert.IsTrue(started.IsSuccess, started.IsFailure ? started.Error.ToString() : "");
            Assert.IsTrue(started.Value.World.Settlements.TryGetPrimary(out var settlement));
            Assert.AreEqual("青石洞府", settlement.Name);
            Assert.AreEqual(10, settlement.GetStock("base:resource_rough_wood"));
            Assert.AreEqual(2, settlement.GetStock("base:resource_spirit_herb"));
            Assert.AreEqual(1, settlement.Facilities.Count);

            var roles = new System.Collections.Generic.HashSet<WorkRoleKind>();
            foreach (var id in started.Value.CharacterIds)
            {
                Assert.IsTrue(started.Value.World.Entities.TryGet(id, out var e));
                Assert.IsTrue(e.TryGet<WorkAssignmentComponent>(out var work));
                Assert.IsTrue(work.IsAssigned);
                roles.Add(work.Role);
            }

            Assert.IsTrue(roles.Contains(WorkRoleKind.Labor));
            Assert.IsTrue(roles.Contains(WorkRoleKind.Gather));
            Assert.IsTrue(roles.Contains(WorkRoleKind.Cultivate));
        }

        [Test]
        public void DayEnd_ProducesResources_AndCultivateProgress()
        {
            var started = new PlayableDayBootstrap().Start(BaseGamePath);
            Assert.IsTrue(started.IsSuccess, started.IsFailure ? started.Error.ToString() : "");

            var world = started.Value.World;
            Assert.IsTrue(world.Settlements.TryGetPrimary(out var settlement));
            var woodBefore = settlement.GetStock("base:resource_rough_wood");
            var herbBefore = settlement.GetStock("base:resource_spirit_herb");

            EntityId cultivateId = EntityId.None;
            foreach (var id in started.Value.CharacterIds)
            {
                if (world.Entities.TryGet(id, out var e) &&
                    e.TryGet<WorkAssignmentComponent>(out var w) &&
                    w.Role == WorkRoleKind.Cultivate)
                {
                    cultivateId = id;
                    break;
                }
            }

            Assert.IsFalse(cultivateId.IsNone);
            Assert.IsTrue(world.Entities.TryGet(cultivateId, out var cultivator));
            var progressBefore = cultivator.Get<CultivationComponent>().Progress;
            var talentBonus = cultivator.TryGet<PersonalityProfileComponent>(out var profile)
                ? TalentGrowthRules.ExtraCultivateProgress(profile)
                : 0;

            world.Tick = new WorldTick(95);
            world.Events.Drain();
            Assert.IsTrue(started.Value.Loop.TickOnce().IsSuccess);

            Assert.AreEqual(woodBefore + 2, settlement.GetStock("base:resource_rough_wood"));
            Assert.AreEqual(herbBefore + 1, settlement.GetStock("base:resource_spirit_herb"));
            Assert.AreEqual(progressBefore + 8 + talentBonus, cultivator.Get<CultivationComponent>().Progress);
            Assert.IsTrue(world.Events.Drain().Exists(e => e.Type == CoreEventType.SettlementProductionResolved));
        }

        [Test]
        public void AssignWork_Command_ChangesRole()
        {
            var started = new PlayableDayBootstrap().Start(BaseGamePath);
            Assert.IsTrue(started.IsSuccess);
            var subject = started.Value.CharacterIds[0];
            var result = started.Value.Port.Submit(
                new PlayerCommandRequest(
                    subject,
                    PlayerCommandKind.AssignWork,
                    1,
                    EntityId.None,
                    WorkRoleKind.Cultivate));
            Assert.IsTrue(result.IsSuccess, result.IsFailure ? result.Error.ToString() : "");
            Assert.IsTrue(started.Value.World.Entities.TryGet(subject, out var entity));
            Assert.AreEqual(WorkRoleKind.Cultivate, entity.Get<WorkAssignmentComponent>().Role);
        }
    }
}
