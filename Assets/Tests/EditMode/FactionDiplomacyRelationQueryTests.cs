using NUnit.Framework;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests.EditMode
{
    public sealed class FactionDiplomacyRelationQueryTests
    {
        [Test]
        public void GetRelation_UsesVassalageDirectionAndWarPriority()
        {
            var world = new SimulationWorld();
            const string overlord = "test:overlord";
            const string vassal = "test:vassal";
            Assert.IsTrue(world.Strategic.Vassalages.TryBindVassalage(vassal, overlord));

            Assert.AreEqual(
                FactionDiplomacyRelation.Vassal,
                FactionDiplomacyRelationQuery.GetRelation(world, overlord, vassal));
            Assert.AreEqual(
                FactionDiplomacyRelation.Overlord,
                FactionDiplomacyRelationQuery.GetRelation(world, vassal, overlord));

            Assert.IsTrue(WarGateService.DeclareWar(world, overlord, vassal).IsSuccess);
            Assert.AreEqual(
                FactionDiplomacyRelation.War,
                FactionDiplomacyRelationQuery.GetRelation(world, overlord, vassal));
        }
    }
}
