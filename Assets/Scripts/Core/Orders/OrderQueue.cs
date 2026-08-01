using System;
using System.Collections.Generic;

namespace XianXia.Core.Orders
{
    /// <summary>
    /// Pending orders. Player-sourced orders are always ahead of Schedule-sourced ones.
    /// </summary>
    public sealed class OrderQueue
    {
        readonly List<Order> _orders = new List<Order>();

        public int Count => _orders.Count;

        public void Enqueue(Order order)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            if (order.Source == OrderSource.Player)
            {
                var insertAt = 0;
                while (insertAt < _orders.Count && _orders[insertAt].Source == OrderSource.Player)
                    insertAt++;
                _orders.Insert(insertAt, order);
                return;
            }

            _orders.Add(order);
        }

        public bool TryPeek(out Order order)
        {
            if (_orders.Count == 0)
            {
                order = null;
                return false;
            }

            order = _orders[0];
            return true;
        }

        public bool TryDequeue(out Order order)
        {
            if (_orders.Count == 0)
            {
                order = null;
                return false;
            }

            order = _orders[0];
            _orders.RemoveAt(0);
            return true;
        }

        public bool HasSource(OrderSource source)
        {
            for (var i = 0; i < _orders.Count; i++)
            {
                if (_orders[i].Source == source)
                    return true;
            }

            return false;
        }

        public bool HasMatching(OrderSource source, OrderType type)
        {
            for (var i = 0; i < _orders.Count; i++)
            {
                var o = _orders[i];
                if (o.Source == source && o.Type == type)
                    return true;
            }

            return false;
        }

        public void RemoveWhere(Func<Order, bool> predicate)
        {
            if (predicate == null)
                return;
            _orders.RemoveAll(o => predicate(o));
        }

        public List<Order> Snapshot() => new List<Order>(_orders);

        public void Clear() => _orders.Clear();

        public void ReplaceAll(IEnumerable<Order> orders)
        {
            _orders.Clear();
            if (orders == null) return;
            foreach (var o in orders)
                Enqueue(o);
        }
    }
}
