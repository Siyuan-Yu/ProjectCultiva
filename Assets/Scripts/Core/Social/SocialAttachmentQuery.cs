using System;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;

namespace XianXia.Core.Social
{
    public static class SocialAttachmentQuery
    {
        public static int Get(SimulationWorld world, EntityId reactor, EntityId subject)
        {
            if (world == null || reactor.IsNone || subject.IsNone || reactor == subject)
                return 0;
            var strongestBondBase = 0;
            var bonds = world.SocialBonds.All;
            for (var i = 0; i < bonds.Count; i++)
            {
                var bond = bonds[i];
                if (!bond.Involves(reactor) || !bond.Involves(subject))
                    continue;
                strongestBondBase = Math.Max(strongestBondBase, BondBase(bond, reactor));
            }

            var attitude = world.Relationships.GetAttitude(reactor, subject);
            return Math.Max(-100, Math.Min(100,
                strongestBondBase + attitude.Affection / 2 - attitude.Grudge / 2));
        }

        static int BondBase(SocialBond bond, EntityId reactor)
        {
            switch (bond.Kind)
            {
                case SocialBondKind.ParentChild: return bond.From == reactor ? 80 : 70;
                case SocialBondKind.Spouse: return 80;
                case SocialBondKind.Sibling: return 60;
                case SocialBondKind.MasterDisciple: return bond.From == reactor ? 50 : 65;
                case SocialBondKind.SwornSibling: return 65;
                default: return 0;
            }
        }
    }
}
