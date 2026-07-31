using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;

namespace XianXia.Core.Events
{
    public sealed class DomainEventQueue
    {
        readonly Queue<DomainEvent> _queue = new Queue<DomainEvent>();
        ulong _nextId = 1;
        int _cursor;

        public int Count => _queue.Count;

        public int Cursor => _cursor;

        public ulong PeekNextId => _nextId;

        public DomainEvent Publish(
            EventType type,
            WorldTick tick,
            EntityId? actor = null,
            EntityId? target = null,
            string payload = null,
            EventId? causeEventId = null)
        {
            var evt = new DomainEvent(new EventId(_nextId++), type, tick, actor, target, payload, causeEventId);
            _queue.Enqueue(evt);
            return evt;
        }

        public bool TryPeek(out DomainEvent evt)
        {
            if (_queue.Count == 0)
            {
                evt = null;
                return false;
            }

            evt = _queue.Peek();
            return true;
        }

        public List<DomainEvent> Drain()
        {
            var list = new List<DomainEvent>(_queue.Count);
            while (_queue.Count > 0)
            {
                list.Add(_queue.Dequeue());
                _cursor++;
            }
            return list;
        }

        public void RestoreCursor(int cursor, ulong nextId)
        {
            _cursor = cursor;
            _nextId = nextId == 0 ? 1UL : nextId;
        }

        public void Clear()
        {
            _queue.Clear();
        }
    }
}
