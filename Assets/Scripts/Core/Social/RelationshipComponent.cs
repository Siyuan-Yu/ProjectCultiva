using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;

namespace XianXia.Core.Social
{
    /// <summary>
    /// 五维定向态度 cache（this entity → peer）。
    /// Must only be written by <see cref="RelationshipService"/> after Ledger append.
    /// </summary>
    public sealed class RelationshipComponent : IComponent
    {
        readonly Dictionary<ulong, SocialAttitude> _toward = new Dictionary<ulong, SocialAttitude>();

        public int CachedPeerCount => _toward.Count;

        public int GetCachedToward(EntityId peer)
        {
            if (peer.IsNone)
                return 0;
            return _toward.TryGetValue(peer.Value, out var attitude) ? attitude.Affection : 0;
        }

        public bool TryGetCachedToward(EntityId peer, out int score)
        {
            score = 0;
            if (peer.IsNone)
                return false;
            if (!_toward.TryGetValue(peer.Value, out var attitude))
                return false;
            score = attitude.Affection;
            return true;
        }

        public SocialAttitude GetCachedAttitude(EntityId peer) =>
            !peer.IsNone && _toward.TryGetValue(peer.Value, out var attitude)
                ? attitude
                : SocialAttitude.Zero;

        public bool TryGetCachedAttitude(EntityId peer, out SocialAttitude attitude)
        {
            attitude = SocialAttitude.Zero;
            return !peer.IsNone && _toward.TryGetValue(peer.Value, out attitude);
        }

        /// <summary>Service-only cache refresh. Do not call from gameplay／UI to invent scores.</summary>
        public void ReplaceCachedToward(EntityId peer, int score)
            => ReplaceCachedAttitude(peer, new SocialAttitude(score, 0, 0, 0, 0));

        public void ReplaceCachedAttitude(EntityId peer, SocialAttitude attitude)
        {
            if (peer.IsNone || attitude == null)
                return;
            _toward[peer.Value] = attitude;
        }

        public void ClearCache()
        {
            _toward.Clear();
        }
    }
}
