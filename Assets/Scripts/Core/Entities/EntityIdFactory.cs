using XianXia.Core.Domain.Ids;

namespace XianXia.Core.Entities
{
    public sealed class EntityIdFactory
    {
        ulong _next = 1;

        public EntityId Next()
        {
            var id = new EntityId(_next);
            _next++;
            return id;
        }

        public void Reset(ulong nextValue)
        {
            _next = nextValue == 0 ? 1UL : nextValue;
        }

        public ulong PeekNext => _next;
    }
}
