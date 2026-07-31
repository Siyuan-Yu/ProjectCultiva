using System;

namespace XianXia.Core.Domain.Ids
{
    /// <summary>
    /// Origin of a modifier / effect. Carries kind plus optional definition, entity, or modifier handles.
    /// </summary>
    public readonly struct SourceRef : IEquatable<SourceRef>
    {
        public SourceRef(
            SourceKind kind,
            DefinitionId? definitionId = null,
            EntityId? entityId = null,
            ModifierId? modifierId = null)
        {
            Kind = kind;
            DefinitionId = definitionId;
            EntityId = entityId;
            ModifierId = modifierId;
        }

        public SourceKind Kind { get; }

        public DefinitionId? DefinitionId { get; }

        public EntityId? EntityId { get; }

        public ModifierId? ModifierId { get; }

        public bool Equals(SourceRef other) =>
            Kind == other.Kind &&
            Nullable.Equals(DefinitionId, other.DefinitionId) &&
            Nullable.Equals(EntityId, other.EntityId) &&
            Nullable.Equals(ModifierId, other.ModifierId);

        public override bool Equals(object obj) => obj is SourceRef other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Kind;
                hash = hash * 31 + (DefinitionId.HasValue ? DefinitionId.Value.GetHashCode() : 0);
                hash = hash * 31 + (EntityId.HasValue ? EntityId.Value.GetHashCode() : 0);
                hash = hash * 31 + (ModifierId.HasValue ? ModifierId.Value.GetHashCode() : 0);
                return hash;
            }
        }

        public static bool operator ==(SourceRef left, SourceRef right) => left.Equals(right);

        public static bool operator !=(SourceRef left, SourceRef right) => !left.Equals(right);
    }
}
