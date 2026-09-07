using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;

namespace XianXia.Core.Social
{
    public static class SocialBondQuery
    {
        public static void GetForSubject(SimulationWorld world, EntityId subject, List<SocialBond> output)
        {
            output?.Clear();
            if (world == null || output == null || subject.IsNone)
                return;
            var all = world.SocialBonds.All;
            for (var i = 0; i < all.Count; i++)
                if (all[i].Involves(subject))
                    output.Add(all[i]);
        }

        public static string GetRoleLabel(SocialBond bond, EntityId subject)
        {
            if (bond == null || !bond.Involves(subject))
                return string.Empty;
            switch (bond.Kind)
            {
                case SocialBondKind.ParentChild: return bond.From == subject ? "子女" : "父母";
                case SocialBondKind.MasterDisciple: return bond.From == subject ? "徒弟" : "师父";
                case SocialBondKind.Sibling: return "兄弟姐妹";
                case SocialBondKind.Spouse: return "配偶";
                case SocialBondKind.SwornSibling: return "结义";
                default: return string.Empty;
            }
        }
    }
}
