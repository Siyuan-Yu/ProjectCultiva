using XianXia.Core.Domain.Ids;
using XianXia.Core.Events;
using XianXia.Core.Simulation;

namespace XianXia.Core.Content
{
    /// <summary>Story／content flags on WorldFlagBoard with history + domain events.</summary>
    public static class StoryFlagService
    {
        public static bool Set(SimulationWorld world, string flag, EntityId subject = default)
        {
            if (world == null || string.IsNullOrEmpty(flag))
                return false;
            if (!world.Flags.Set(flag))
                return false;
            world.Flags.RecordHistory("set:" + flag);
            world.Events.Publish(
                EventType.StoryFlagChanged,
                world.Tick,
                target: subject,
                payload: "set:" + flag);
            return true;
        }

        public static bool Clear(SimulationWorld world, string flag, EntityId subject = default)
        {
            if (world == null || string.IsNullOrEmpty(flag))
                return false;
            if (!world.Flags.Clear(flag))
                return false;
            world.Flags.RecordHistory("clear:" + flag);
            world.Events.Publish(
                EventType.StoryFlagChanged,
                world.Tick,
                target: subject,
                payload: "clear:" + flag);
            return true;
        }

        public static bool Has(SimulationWorld world, string flag) =>
            world != null && world.Flags.Has(flag);
    }
}
