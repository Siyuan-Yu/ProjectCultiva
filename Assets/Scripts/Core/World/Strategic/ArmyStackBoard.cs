using System;
using System.Collections.Generic;

namespace XianXia.Core.World.Strategic
{
    public sealed class ArmyStackBoard
    {
        readonly Dictionary<string, ArmyStack> _stacks =
            new Dictionary<string, ArmyStack>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, ArmyStack> Stacks => _stacks;

        public void Clear() => _stacks.Clear();

        public ArmyStack Register(ArmyStack stack)
        {
            if (stack == null || string.IsNullOrEmpty(stack.Id))
                throw new ArgumentException("ArmyStack requires Id.");
            _stacks[stack.Id] = stack;
            return stack;
        }

        public bool TryGet(string id, out ArmyStack stack) =>
            _stacks.TryGetValue(id ?? string.Empty, out stack);

        public void Remove(string id)
        {
            if (!string.IsNullOrEmpty(id))
                _stacks.Remove(id);
        }

        public IEnumerable<ArmyStack> AllOnRoute(string routeId)
        {
            if (string.IsNullOrEmpty(routeId))
                yield break;
            foreach (var kv in _stacks)
            {
                var s = kv.Value;
                if (s == null || !string.Equals(s.RouteId, routeId, StringComparison.Ordinal))
                    continue;
                if (s.IsTraveling || s.IsRouteAnchored)
                    yield return s;
            }
        }
    }
}
