using XianXia.Core.Domain.Ids;

namespace XianXia.Core.Social
{
    public sealed class SocialBond
    {
        public SocialBond(SocialBondKind kind, EntityId from, EntityId to)
        {
            Kind = kind;
            if (IsSymmetric(kind) && from.Value > to.Value)
            {
                From = to;
                To = from;
            }
            else
            {
                From = from;
                To = to;
            }
        }

        public SocialBondKind Kind { get; }
        public EntityId From { get; }
        public EntityId To { get; }

        public bool Involves(EntityId id) => From == id || To == id;

        public EntityId Other(EntityId id) => From == id ? To : (To == id ? From : EntityId.None);

        public static bool IsSymmetric(SocialBondKind kind) =>
            kind == SocialBondKind.Sibling ||
            kind == SocialBondKind.Spouse ||
            kind == SocialBondKind.SwornSibling;
    }
}
