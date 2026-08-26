using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Background Character 旅行会话（仅 Traveling 时持有 route 状态）。</summary>
    public sealed class BackgroundCharacterTravelBoard
    {
        readonly Dictionary<ulong, BackgroundCharacterTravelMotion> _byEntity =
            new Dictionary<ulong, BackgroundCharacterTravelMotion>();

        public IReadOnlyDictionary<ulong, BackgroundCharacterTravelMotion> All => _byEntity;

        public bool IsTraveling(EntityId id) =>
            !id.IsNone && _byEntity.TryGetValue(id.Value, out var motion) && motion != null && motion.IsMoving;

        public bool TryGet(EntityId id, out BackgroundCharacterTravelMotion motion)
        {
            motion = null;
            if (id.IsNone)
                return false;
            return _byEntity.TryGetValue(id.Value, out motion) && motion != null;
        }

        public BackgroundCharacterTravelMotion GetOrCreate(EntityId id)
        {
            if (id.IsNone)
                return null;
            if (_byEntity.TryGetValue(id.Value, out var existing) && existing != null)
                return existing;
            var created = new BackgroundCharacterTravelMotion();
            _byEntity[id.Value] = created;
            return created;
        }

        public void Remove(EntityId id)
        {
            if (id.IsNone)
                return;
            _byEntity.Remove(id.Value);
        }

        public void Clear() => _byEntity.Clear();
    }
}
