using System.Collections.Generic;

namespace XianXia.Core.Orders
{
    public sealed class OrderQueue
    {
        readonly Queue<Order> _queue = new Queue<Order>();

        public int Count => _queue.Count;

        public void Enqueue(Order order) => _queue.Enqueue(order);

        public bool TryPeek(out Order order)
        {
            if (_queue.Count == 0)
            {
                order = null;
                return false;
            }

            order = _queue.Peek();
            return true;
        }

        public bool TryDequeue(out Order order)
        {
            if (_queue.Count == 0)
            {
                order = null;
                return false;
            }

            order = _queue.Dequeue();
            return true;
        }

        public List<Order> Snapshot() => new List<Order>(_queue);

        public void Clear() => _queue.Clear();

        public void ReplaceAll(IEnumerable<Order> orders)
        {
            _queue.Clear();
            if (orders == null) return;
            foreach (var o in orders)
                _queue.Enqueue(o);
        }
    }
}
