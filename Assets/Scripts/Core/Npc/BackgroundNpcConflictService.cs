using System;
using System.Collections.Generic;
using XianXia.Core.Attributes;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;
using XianXia.Core.World.Surface;

namespace XianXia.Core.Npc
{
    /// <summary>Cold-only, deterministic real-Character conflict. A hot participant is never damaged here.</summary>
    public static class BackgroundNpcConflictService
    {
        const ulong IntervalTicks = 8UL;
        const float ContactRange = 3f;
        static readonly MeleeCombatService Combat = new MeleeCombatService();
        static readonly List<Entity> Actors = new List<Entity>(128);
        static readonly HashSet<ulong> Engaged = new HashSet<ulong>();
        static readonly List<WorldVec2> Route = new List<WorldVec2>(16);

        public static void Tick(SimulationWorld world)
        {
            if (world?.Entities == null || world.Tick.Value % IntervalTicks != 0 ||
                world.Strategic.CharacterEncounter != null) return;
            Actors.Clear(); Engaged.Clear();
            foreach (var e in world.Entities.All)
                if (e != null && (e.Tags & (EntityTag.Character | EntityTag.Npc)) != 0 &&
                    CombatLifeStateService.CanFight(e) &&
                    world.Strategic.PlayerPartyContext?.IsMember(e.Id) != true &&
                    !world.ContinuousOutdoorMaterialization.IsMaterialized(e.Id) &&
                    !SeparateSpaceCombatPolicy.IsEntityInActiveSpace(world, e.Id))
                    Actors.Add(e);
            Actors.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value));
            for (var i = 0; i < Actors.Count; i++)
            {
                var attacker = Actors[i];
                if (Engaged.Contains(attacker.Id.Value) || !CombatLifeStateService.CanFight(attacker) ||
                    !CharacterWorldPresenceQuery.TryResolve(world, attacker.Id, out var from) ||
                    !from.HasWorldPosition || string.IsNullOrEmpty(from.SurfaceId)) continue;
                Entity defender = null;
                CharacterWorldPresenceQuery.ResolvedPresence to = default;
                var closest = ContactRange * ContactRange;
                for (var j = 0; j < Actors.Count; j++)
                {
                    var candidate = Actors[j];
                    if (candidate == attacker || Engaged.Contains(candidate.Id.Value) ||
                        !NpcHostilityService.CanAutoAttack(world, attacker.Id, candidate.Id) ||
                        CharacterEncounterService.IsContactSuppressed(world, attacker.Id, candidate.Id) ||
                        !CharacterWorldPresenceQuery.TryResolve(world, candidate.Id, out var place) ||
                        !place.HasWorldPosition || place.SurfaceId != from.SurfaceId) continue;
                    var dx = place.WorldPosition.X - from.WorldPosition.X;
                    var dy = place.WorldPosition.Y - from.WorldPosition.Y;
                    var distance = dx * dx + dy * dy;
                    if (distance > closest) continue;
                    closest = distance;
                    defender = candidate; to = place;
                }
                if (defender == null) continue;
                Engaged.Add(attacker.Id.Value); Engaged.Add(defender.Id.Value);
                if (MortalCivilianService.ReactToHostileContact(world, defender.Id)) continue;
                if (TryRetreat(world, attacker, defender, from, to) ||
                    TryRetreat(world, defender, attacker, to, from)) continue;
                Combat.ApplyStrike(world, attacker.Id, defender.Id, out _, out _);
                if (CombatLifeStateService.CanFight(defender))
                    Combat.ApplyStrike(world, defender.Id, attacker.Id, out _, out _);
            }
            Actors.Clear(); Engaged.Clear(); Route.Clear();
        }

        static bool TryRetreat(SimulationWorld world, Entity actor, Entity enemy,
            CharacterWorldPresenceQuery.ResolvedPresence from,
            CharacterWorldPresenceQuery.ResolvedPresence threat)
        {
            if (actor.TryGet<XianXia.Core.Social.PersonalityProfileComponent>(out var profile) &&
                (profile.HasTag("fanatic") || profile.HasTag("fight_to_death"))) return false;
            if (SquadWorldMotionService.OwnsCharacter(world, actor.Id) ||
                !actor.TryGet<AttributesComponent>(out var attrs)) return false;
            CombatDamageRules.EnsureVitals(actor);
            CombatDamageRules.EnsureVitals(enemy);
            if (!actor.TryGet<CombatVitalsComponent>(out var hp) ||
                !enemy.TryGet<CombatVitalsComponent>(out var enemyHp) ||
                !enemy.TryGet<AttributesComponent>(out var enemyAttrs)) return false;
            var max = Math.Max(1, attrs.GetFinal(AttributeId.MaxHp));
            var enemyMax = Math.Max(1, enemyAttrs.GetFinal(AttributeId.MaxHp));
            if (hp.CurrentHp * 100 > max * 30 ||
                hp.CurrentHp * enemyMax >= enemyHp.CurrentHp * max) return false;
            if (!world.SurfaceGround.TryGet(from.SurfaceId, out var navigation) || navigation == null)
                return false;
            var vx = from.WorldPosition.X - threat.WorldPosition.X;
            var vy = from.WorldPosition.Y - threat.WorldPosition.Y;
            var length = Math.Sqrt(vx * vx + vy * vy);
            if (length < .001) { vx = actor.Id.Value < enemy.Id.Value ? -1 : 1; vy = 0; length = 1; }
            var destination = new WorldVec2((float)(from.WorldPosition.X + vx / length * 4.0),
                (float)(from.WorldPosition.Y + vy / length * 4.0));
            Route.Clear();
            if (navigation.TryFindRoute(from.WorldPosition, destination, Route) != SurfaceGroundRouteStatus.Found)
                return false;
            world.WorldPresence.SetAtWorldPosition(actor.Id, destination, from.SurfaceId);
            world.Strategic.SuppressedCharacterContacts.Add(CharacterEncounterService.ContactKey(actor.Id, enemy.Id));
            if (actor.TryGet<PersonalLifeComponent>(out var personal))
            { personal.Activity = PersonalLifeActivity.None; personal.Status = "战败撤离"; }
            return true;
        }
    }
}
