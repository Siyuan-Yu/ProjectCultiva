using XianXia.Core.Domain.Ids;

namespace XianXia.Core.Attributes
{
    public sealed class ModifierIdFactory
    {
        ulong _next = 1;

        public ModifierId Next()
        {
            var id = new ModifierId(_next);
            _next++;
            return id;
        }

        public void Reset(ulong nextValue) => _next = nextValue == 0 ? 1UL : nextValue;

        public ulong PeekNext => _next;
    }
}
