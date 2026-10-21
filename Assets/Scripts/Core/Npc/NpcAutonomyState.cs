using System;
using System.Collections.Generic;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;
using XianXia.Core.World.Surface;

namespace XianXia.Core.Npc
{
    /// <summary>Entity-owned state survives presentation unload and save/load.</summary>
    public sealed class NpcAutonomyState : IComponent
    {
        public float StrikeCooldown;
        public float Art0, Art1, Art2, Art3, Art4, Art5;
        public bool Retreating;
        public string RetreatSurface = string.Empty;
        public float RetreatX, RetreatY;
        public ulong LastMoveTick;
        public bool Exhausted;
        public bool DetachedByExhaustion;
        public static NpcAutonomyState Ensure(Entity e)
        {
            if (!e.TryGet<NpcAutonomyState>(out var state))
            { state = new NpcAutonomyState(); e.AddComponent(state); }
            return state;
        }
        public float GetArt(int i) => i == 0 ? Art0 : i == 1 ? Art1 : i == 2 ? Art2 : i == 3 ? Art3 : i == 4 ? Art4 : Art5;
        public void SetArt(int i, float value)
        { if (i == 0) Art0 = value; else if (i == 1) Art1 = value; else if (i == 2) Art2 = value; else if (i == 3) Art3 = value; else if (i == 4) Art4 = value; else Art5 = value; }
        public void AdvanceCooldowns(float seconds)
        {
            StrikeCooldown = Math.Max(0, StrikeCooldown - seconds);
            for (int i = 0; i < XianXia.Core.Combat.CombatArtsComponent.MaxEquippedSlots; i++) SetArt(i, Math.Max(0, GetArt(i) - seconds));
        }
    }

    public static class NpcColdMovement
    {
        public static bool Advance(SimulationWorld world, Entity actor, string surfaceId,
            WorldVec2 goal, float radius, out bool arrived)
        {
            arrived = false;
            if (!CharacterWorldPresenceQuery.TryResolve(world, actor.Id, out var at) ||
                !at.HasWorldPosition || at.SurfaceId != surfaceId ||
                !world.SurfaceGround.TryGet(surfaceId, out var nav) || nav == null) return false;
            arrived = WorldVec2.Distance(at.WorldPosition, goal) <= radius;
            if (arrived) return true;
            var state = NpcAutonomyState.Ensure(actor);
            if (state.LastMoveTick == world.Tick.Value) return true;
            // Drive is called twice per Tick. Spend the movement budget exactly once.
            var elapsed = state.LastMoveTick == 0 ? 1UL : Math.Min(8UL, world.Tick.Value - state.LastMoveTick);
            state.LastMoveTick = world.Tick.Value;
            var route = new List<WorldVec2>();
            if (!NpcLocalNavigation.TryRoute(world, nav, at.WorldPosition, goal, route)) return false;
            var position = at.WorldPosition;
            var budget = BackgroundSimulationScheduler.DistanceBudgetFromElapsedSimulationTicks(
                ContinuousWorldMovementScale.Resolve(world), elapsed);
            for (int i = 1; i < route.Count && budget > 0; i++)
            {
                var d = WorldVec2.Distance(position, route[i]);
                if (d <= budget) { position = route[i]; budget -= d; }
                else { var t = budget / d; position = new WorldVec2(position.X + (route[i].X-position.X)*t,
                    position.Y + (route[i].Y-position.Y)*t); budget = 0; }
            }
            PlayerPartyTransitionMembership.SyncIndependentCharacterPresenceFromPosition(world, actor.Id, position, surfaceId);
            arrived = WorldVec2.Distance(position, goal) <= radius;
            return true;
        }
    }
}
