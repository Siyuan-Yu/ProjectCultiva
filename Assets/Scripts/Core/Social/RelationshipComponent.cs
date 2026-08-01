using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;

namespace XianXia.Core.Social
{
    /// <summary>
    /// Runtime cache of directed relationship scores (this entity → peer).
    /// Must only be written by <see cref="RelationshipService"/> after Ledger append.
    /// </summary>
    public sealed class RelationshipComponent : IComponent
    {
        readonly Dictionary<ulong, int> _toward = new Dictionary<ulong, int>();

        public int CachedPeerCount => _toward.Count;

        public int GetCachedToward(EntityId peer)
        {
            if (peer.IsNone)
                return 0;
            return _toward.TryGetValue(peer.Value, out var score) ? score : 0;
        }

        public bool TryGetCachedToward(EntityId peer, out int score)
        {
            score = 0;
            if (peer.IsNone)
                return false;
            return _toward.TryGetValue(peer.Value, out score);
        }

        /// <summary>Service-only cache refresh. Do not call from gameplay／UI to invent scores.</summary>
        public void ReplaceCachedToward(EntityId peer, int score)
        {
            if (peer.IsNone)
                return;
            _toward[peer.Value] = score;
        }

        public void ClearCache()
        {
            _toward.Clear();
        }
    }
}
