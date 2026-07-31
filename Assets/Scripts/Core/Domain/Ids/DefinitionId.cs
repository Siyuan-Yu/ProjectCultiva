using System;
using XianXia.Core.Results;

namespace XianXia.Core.Domain.Ids
{
    /// <summary>
    /// Stable content definition id in the form <c>namespace:local_id</c> (ADR-0015).
    /// </summary>
    public readonly struct DefinitionId : IEquatable<DefinitionId>
    {
        public DefinitionId(string ns, string localId)
        {
            if (string.IsNullOrEmpty(ns))
                throw new ArgumentException("DefinitionId namespace must be non-empty.", nameof(ns));
            if (string.IsNullOrEmpty(localId))
                throw new ArgumentException("DefinitionId local_id must be non-empty.", nameof(localId));

            Namespace = ns;
            LocalId = localId;
        }

        public string Namespace { get; }

        public string LocalId { get; }

        /// <summary>
        /// Parse without throwing for ordinary failures. Prefer <see cref="Parse"/> when Result is needed.
        /// </summary>
        public static bool TryParse(string text, out DefinitionId id)
        {
            var parsed = Parse(text);
            if (parsed.IsSuccess)
            {
                id = parsed.Value;
                return true;
            }

            id = default;
            return false;
        }

        /// <summary>
        /// Result-based parse for business paths. Does not throw on ordinary invalid input.
        /// </summary>
        public static Result<DefinitionId> Parse(string text)
        {
            if (string.IsNullOrEmpty(text))
                return Result.Fail<DefinitionId>(ErrorCode.InvalidDefinitionId, "DefinitionId text is empty.");

            var separator = text.IndexOf(':');
            if (separator <= 0 || separator >= text.Length - 1)
                return Result.Fail<DefinitionId>(ErrorCode.InvalidDefinitionId, "DefinitionId must be namespace:local_id.", text);

            var ns = text.Substring(0, separator);
            var local = text.Substring(separator + 1);
            if (string.IsNullOrEmpty(ns) || string.IsNullOrEmpty(local))
                return Result.Fail<DefinitionId>(ErrorCode.InvalidDefinitionId, "DefinitionId namespace and local_id must be non-empty.", text);

            return Result.Ok(new DefinitionId(ns, local));
        }

        public override string ToString() => Namespace + ":" + LocalId;

        public bool Equals(DefinitionId other) =>
            string.Equals(Namespace, other.Namespace, StringComparison.Ordinal) &&
            string.Equals(LocalId, other.LocalId, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is DefinitionId other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + (Namespace != null ? StringComparer.Ordinal.GetHashCode(Namespace) : 0);
                hash = hash * 31 + (LocalId != null ? StringComparer.Ordinal.GetHashCode(LocalId) : 0);
                return hash;
            }
        }

        public static bool operator ==(DefinitionId left, DefinitionId right) => left.Equals(right);

        public static bool operator !=(DefinitionId left, DefinitionId right) => !left.Equals(right);
    }
}
