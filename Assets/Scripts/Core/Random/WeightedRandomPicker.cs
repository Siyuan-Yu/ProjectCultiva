using System;

namespace XianXia.Core.Random
{
    public static class WeightedRandomPicker
    {
        public static int PickIndex(int count, Func<int, int> weightAt, IRandomSource random)
        {
            if (count <= 0 || weightAt == null || random == null) return -1;
            var total = 0;
            for (var i = 0; i < count; i++) total += Math.Max(0, weightAt(i));
            if (total <= 0) return -1;
            var roll = random.NextInt(0, total);
            for (var i = 0; i < count; i++)
            {
                roll -= Math.Max(0, weightAt(i));
                if (roll < 0) return i;
            }
            return -1;
        }
    }
}
