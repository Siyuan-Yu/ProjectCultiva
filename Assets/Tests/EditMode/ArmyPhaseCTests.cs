using NUnit.Framework;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;
using XianXia.Data.Bootstrap;

namespace XianXia.Tests
{
    public sealed class ArmyPhaseCTests
    {
        static string BaseGamePath =>
            System.IO.Path.GetFullPath(
                System.IO.Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        [Test]
        public void ArmyStackAdapter_MemberCountDerived()
        {
            var world = StartCh01().World;
            Assert.IsTrue(world.Strategic.Armies.TryGet(ArmyStackAdapter.BanditPatrolStackId, out var stack));
            Assert.IsTrue(ArmyStackAdapter.HasFormalArmyLink(stack));
            ArmyStackAdapter.RefreshDerivedPresentation(world, stack);
            Assert.AreEqual(4, ArmyStackAdapter.GetMemberCount(world, stack));
            Assert.AreEqual(4, stack.MemberCount);
        }

        [Test]
        public void ArmyStackAdapter_CombatPowerFromMembers()
        {
            var world = StartCh01().World;
            Assert.IsTrue(world.Strategic.Armies.TryGet(ArmyStackAdapter.BanditPatrolStackId, out var stack));
            ArmyStackAdapter.RefreshDerivedPresentation(world, stack);
            var derived = ArmyStackAdapter.GetCombatPower(world, stack);
            Assert.GreaterOrEqual(derived, 4);
            Assert.AreEqual(derived, CombatPowerCalculator.ForArmyStack(world, stack));
        }

        [Test]
        public void TestBanditArmy_FourRealCharacters()
        {
            var world = StartCh01().World;
            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(
                ArmyStackAdapter.BanditPatrolFormalArmyId,
                out var army));
            Assert.AreEqual(4, army.MemberCharacterIds.Count);
            for (var i = 0; i < army.MemberCharacterIds.Count; i++)
            {
                var id = new XianXia.Core.Domain.Ids.EntityId(army.MemberCharacterIds[i]);
                Assert.IsTrue(world.Entities.TryGet(id, out _));
            }
        }

        static PlayableDayBootstrapResult StartCh01()
        {
            var started = new PlayableDayBootstrap().Start(
                BaseGamePath,
                new PlayableDayOptions { OpeningScenarioId = "base:scenario_ch01_reference" });
            Assert.IsTrue(started.IsSuccess, started.IsFailure ? started.Error.ToString() : "");
            return started.Value;
        }
    }
}
