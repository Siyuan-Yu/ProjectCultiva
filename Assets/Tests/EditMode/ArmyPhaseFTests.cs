using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World.Strategic;
using XianXia.Data.Bootstrap;

namespace XianXia.Tests
{
    public sealed class ArmyPhaseFTests
    {
        static string BaseGamePath =>
            System.IO.Path.GetFullPath(
                System.IO.Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        static SimulationWorld StartWorld()
        {
            var started = new PlayableDayBootstrap().Start(
                BaseGamePath,
                new PlayableDayOptions { OpeningScenarioId = "base:scenario_ch01_reference" });
            Assert.IsTrue(started.IsSuccess);
            return started.Value.World;
        }

        [Test]
        public void AutoBattle_RealMembers_CasualtiesOnEntities()
        {
            var world = StartWorld();
            var party = new System.Collections.Generic.List<EntityId>(4);
            foreach (var entity in world.Entities.All)
            {
                if (entity.TryGet<FactionMembershipComponent>(out var mem) &&
                    mem.FactionId == StrategicFactionCatalog.HuangcunLaborId &&
                    entity.TryGet<LifecycleComponent>(out var life) &&
                    life.State == LifecycleState.Alive)
                {
                    party.Add(entity.Id);
                    if (party.Count >= 2)
                        break;
                }
            }

            Assert.IsTrue(world.Strategic.Armies.TryGet(ArmyStackAdapter.BanditPatrolStackId, out var enemy));
            world.Strategic.BattleOffer.AutoWinPercent = 100;
            Assert.IsTrue(BattleOfferService.TryBuildOfferForArmy(world, party, enemy, "Auto"));
            Assert.IsTrue(BattleOfferService.ResolveAuto(world, true, out var won, out var report).IsSuccess);
            Assert.IsTrue(won);

            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(
                ArmyStackAdapter.BanditPatrolFormalArmyId,
                out var banditArmy));
            var dead = 0;
            for (var i = 0; i < banditArmy.MemberCharacterIds.Count; i++)
            {
                var id = new EntityId(banditArmy.MemberCharacterIds[i]);
                if (world.Entities.TryGet(id, out var ent) &&
                    ent.TryGet<LifecycleComponent>(out var life) &&
                    life.IsDead)
                    dead++;
            }

            Assert.GreaterOrEqual(dead, 1);
            ArmyStackAdapter.SyncDownedCountsFromMembers(world, enemy);
            Assert.GreaterOrEqual(enemy.CorpseMemberCount, 1);
        }

        [Test]
        public void ArmyStackAdapter_DownedCountsDerivedFromMembers()
        {
            var world = StartWorld();
            Assert.IsTrue(world.Strategic.Armies.TryGet(ArmyStackAdapter.BanditPatrolStackId, out var stack));
            ArmyStackAdapter.SyncDownedCountsFromMembers(world, stack);
            Assert.AreEqual(ArmyStackAdapter.GetMemberCount(world, stack), stack.MemberCount);
        }
    }
}
