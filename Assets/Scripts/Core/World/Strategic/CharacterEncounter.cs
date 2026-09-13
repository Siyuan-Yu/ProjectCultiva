using System;
using System.Collections.Generic;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    public enum CharacterEncounterPhase { Preparing, Active, ReadyToEnd, Committed }

    public sealed class EncounterCharacter
    {
        public ulong CharacterId;
        public string SquadId = "";
        public bool Enemy;
        public string SourceSiteId = "";
        public int SourceMode;
        public float OriginX, OriginY;
        public float TacticalX, TacticalY;
        public ulong TargetId;
        public float Cooldown;
        public float JoinedAt;
        public ManualBattleReportCondition EntryCondition;
        public bool EntryHpAvailable;
        public int EntryHp, EntryMaxHp;
    }

    public sealed class CharacterEncounterState
    {
        public const int Format = 1;
        public int Version = Format;
        public string EncounterId = "";
        public string SourceSurfaceId = "";
        public string SourceSiteId = "";
        public float CenterX, CenterY, Width, Height;
        public float ElapsedSeconds, DecayAccumulator;
        public CharacterEncounterPhase Phase;
        public bool PlayerWon;
        public int RosterVersion = 1;
        public bool ContinuationUsed;
        public readonly List<EncounterCharacter> Participants = new List<EncounterCharacter>();
        public bool Contains(float x, float y) => Math.Abs(x - CenterX) <= Width * .5f && Math.Abs(y - CenterY) <= Height * .5f;
        public EncounterCharacter Find(ulong id) => Participants.Find(p => p.CharacterId == id);
        public bool Opposing(ulong a, ulong b) => Find(a) != null && Find(b) != null && Find(a).Enemy != Find(b).Enemy;
    }

    /// <summary>One real Character per participant. Only space is restored on return.</summary>
    public static class CharacterEncounterService
    {
        public static bool RequiresEntry(SimulationWorld world, EntityId attacker, EntityId target)
        {
            var board = world?.Strategic;
            if (board == null) return false;
            var state = board.CharacterEncounter;
            if (state != null) return state.Phase != CharacterEncounterPhase.Active || !state.Opposing(attacker.Value, target.Value);
            var party = board.PlayerPartyContext;
            if (party == null || (!party.IsMember(attacker) && !party.IsMember(target))) return false;
            if (board.ContinuousManualCombat.IsActive) return false; // Explicit legacy encounter compatibility only.
            board.PendingCharacterAttacker = attacker;
            board.PendingCharacterTarget = target;
            return true;
        }

        public static Result Prepare(SimulationWorld world, EntityId attacker, EntityId target,
            string surfaceId, out CharacterEncounterState prepared)
        {
            prepared = null;
            if (world?.Strategic?.SpatialRules == null || world.Strategic.CharacterEncounter != null ||
                StrategicClockFreezeService.IsWorldTickFrozen(world))
                return Fail("World is already owned by another encounter or lacks spatial configuration.");
            if (!CharacterPersonalSpaceQuery.TryResolveContinuous(world, target, surfaceId, out var contact, out var failure))
                return Fail("Target personal space: " + failure);
            if (!world.Strategic.Squads.TryGetForCharacter(attacker, out var attackers) ||
                !world.Strategic.Squads.TryGetForCharacter(target, out var targets) || attackers == targets)
                return Fail("Encounter requires two different current squads.");
            var state = new CharacterEncounterState
            {
                SourceSurfaceId = surfaceId, CenterX = contact.X, CenterY = contact.Y,
                Width = world.Strategic.SpatialRules.WildernessEncounterWidthWorld,
                Height = world.Strategic.SpatialRules.WildernessEncounterHeightWorld,
                Phase = CharacterEncounterPhase.Preparing
            };
            if (WorldSiteCoreCoverageResolver.TryResolve(world, surfaceId, contact.X, contact.Y, out var site))
            {
                var range = world.Strategic.SpatialRules.RequireLevel(site.CoreLevel);
                state.SourceSiteId = site.SiteId;
                state.CenterX = site.CoreWorldX; state.CenterY = site.CoreWorldY;
                state.Width = range.WidthWorld; state.Height = range.HeightWorld;
            }
            var player = world.Strategic.PlayerPartyContext;
            var attackerFriendly = player != null && player.IsMember(attacker);
            if (!attackerFriendly && (player == null || !player.IsMember(target)))
                return Fail("Player encounter requires a physically involved player squad.");
            foreach (var squad in new[] { attackers, targets })
                foreach (var raw in squad.MemberCharacterIds)
                {
                    var id = new EntityId(raw);
                    if (!world.Entities.TryGet(id, out var entity) || !IsLiving(world, raw))
                        return Fail("Necessary squad member unavailable: " + raw);
                    if (!CharacterPersonalSpaceQuery.TryResolveContinuous(world, id, surfaceId, out var point, out failure))
                        return Fail("Necessary squad member personal space: " + raw + " " + failure);
                    if (!state.Contains(point.X, point.Y)) return Fail("Necessary squad member outside frozen range: " + raw);
                    if (world.Strategic.Participants.FindByEntity(id) != null) return Fail("Squad member already locked: " + raw);
                    world.WorldPresence.TryGet(id, out var presence);
                    state.Participants.Add(new EncounterCharacter
                    {
                        CharacterId = raw, SquadId = squad.SquadId,
                        Enemy = (squad == targets) == attackerFriendly,
                        SourceMode = (int)presence.Mode, SourceSiteId = presence.SiteId,
                        OriginX = point.X, OriginY = point.Y, TacticalX = point.X, TacticalY = point.Y
                    });
                }
            // Reserve identity only after full read-only qualification. Character allocation is not used.
            state.EncounterId = "encounter:" + world.Entities.Ids.Next().Value;
            prepared = state;
            return Result.Success();
        }

        public static bool IsLiving(SimulationWorld world, ulong id) =>
            world.Entities.TryGet(new EntityId(id), out var entity) &&
            entity.TryGet<LifecycleComponent>(out var life) && life.State == LifecycleState.Alive;

        public static Result Begin(SimulationWorld world, CharacterEncounterState state)
        {
            if (world.Strategic.CharacterEncounter != null || state == null || state.Phase != CharacterEncounterPhase.Preparing)
                return Fail("Encounter preparation expired.");
            foreach (var p in state.Participants)
            {
                if (!IsLiving(world, p.CharacterId) || !world.Strategic.Squads.TryGetForCharacter(new EntityId(p.CharacterId), out var squad) ||
                    squad.SquadId != p.SquadId) return Fail("Encounter membership changed during preparation.");
            }
            foreach (var p in state.Participants)
            {
                world.Entities.TryGet(new EntityId(p.CharacterId), out var entity);
                CombatDamageRules.EnsureVitals(entity);
                ManualBattleReportBuilder.CaptureState(world, new EntityId(p.CharacterId), out p.EntryCondition,
                    out p.EntryHpAvailable, out p.EntryHp, out p.EntryMaxHp);
            }
            state.Phase = CharacterEncounterPhase.Active;
            world.Strategic.CharacterEncounter = state;
            BindRuntime(world);
            return Result.Success();
        }

        public static Result ValidateRestored(SimulationWorld world, CharacterEncounterState state)
        {
            if (state == null) return Result.Success();
            if (state.Version != CharacterEncounterState.Format || string.IsNullOrWhiteSpace(state.EncounterId) ||
                string.IsNullOrWhiteSpace(state.SourceSurfaceId) || !Finite(state.CenterX) || !Finite(state.CenterY) ||
                !Finite(state.Width) || !Finite(state.Height) || state.Width <= 0f || state.Height <= 0f ||
                !Finite(state.ElapsedSeconds) || state.ElapsedSeconds < 0f ||
                (state.Phase != CharacterEncounterPhase.Active && state.Phase != CharacterEncounterPhase.ReadyToEnd &&
                 state.Phase != CharacterEncounterPhase.Committed)) return Fail("Invalid independent encounter snapshot.");
            var seen = new HashSet<ulong>(); var friendly = 0; var enemy = 0;
            foreach (var p in state.Participants)
            {
                if (p == null || p.CharacterId == 0 || !seen.Add(p.CharacterId) ||
                    !world.Entities.TryGet(new EntityId(p.CharacterId), out _) ||
                    !Finite(p.OriginX) || !Finite(p.OriginY) || !Finite(p.TacticalX) || !Finite(p.TacticalY) ||
                    !state.Contains(p.OriginX, p.OriginY) || !state.Contains(p.TacticalX, p.TacticalY) ||
                    !Finite(p.Cooldown) || p.Cooldown < 0f) return Fail("Invalid encounter participant snapshot.");
                if (p.Enemy) enemy++; else friendly++;
            }
            return friendly > 0 && enemy > 0 ? Result.Success() : Fail("Encounter lacks two sides.");
        }

        public static void BindRuntime(SimulationWorld world)
        {
            var state = world.Strategic.CharacterEncounter;
            if (state == null || state.Phase == CharacterEncounterPhase.Committed) return;
            var snapshot = world.Strategic.Participants;
            snapshot.Clear(); snapshot.OfferId = state.EncounterId;
            snapshot.EncounterLocalMapId = state.EncounterId;
            var draft = new ManualBattleReportDraft { OfferId = state.EncounterId };
            foreach (var p in state.Participants)
            {
                var id = new EntityId(p.CharacterId);
                var presence = world.WorldPresence.GetOrCreate(id);
                presence.Mode = PartyWorldPresenceMode.InEncounter;
                presence.PersonalSurfaceId = state.SourceSurfaceId;
                presence.WorldPosX = p.TacticalX; presence.WorldPosY = p.TacticalY;
                presence.HasContinuousWorldPosition = true;
                snapshot.Add(new BattleParticipantRecord { EntityId = id, Selected = true,
                    Kind = p.Enemy ? BattleParticipantKind.EnemyPrimary : BattleParticipantKind.MandatoryFriendly,
                    IncludedReason = p.JoinedAt > 0f ? "FrozenRangeIntervention" : "InitiatingSquads" });
                world.Entities.TryGet(id, out var entity);
                draft.Add(new ManualBattleReportDraft.Entry { Id = id, Name = entity.DisplayName,
                    Side = p.Enemy ? ActualBattleParticipantSide.Enemy : ActualBattleParticipantSide.Friendly,
                    Condition = p.EntryCondition, HpAvailable = p.EntryHpAvailable, Hp = p.EntryHp, MaxHp = p.EntryMaxHp });
            }
            world.Strategic.ManualBattleSettlement.Begin(draft);
            world.Strategic.ContinuousManualCombat.Begin(state.EncounterId, state.SourceSurfaceId,
                new WorldVec2(state.CenterX, state.CenterY), ActualBattleParticipantQuery.Collect(snapshot));
            world.Strategic.ClockFreeze.Reason = state.Phase == CharacterEncounterPhase.Active
                ? StrategicClockFreezeReason.ManualEncounter : StrategicClockFreezeReason.PostBattle;
        }

        public static void Advance(SimulationWorld world, float seconds)
        {
            var state = world.Strategic.CharacterEncounter;
            if (state == null || state.Phase != CharacterEncounterPhase.Active || !Finite(seconds) || seconds <= 0f) return;
            state.ElapsedSeconds += seconds; state.DecayAccumulator += seconds;
            while (state.DecayAccumulator >= 1f)
            {
                state.DecayAccumulator -= 1f;
                CombatLifeStateService.TickEncounterLifeDecay(world, state.Participants);
            }
            var friendly = false; var enemy = false;
            foreach (var p in state.Participants)
                if (IsLiving(world, p.CharacterId)) { if (p.Enemy) enemy = true; else friendly = true; }
            if (!friendly || !enemy)
            {
                state.PlayerWon = friendly;
                state.Phase = CharacterEncounterPhase.ReadyToEnd;
                world.Strategic.ClockFreeze.Reason = StrategicClockFreezeReason.PostBattle;
            }
        }

        public static Result CommitAndReturn(SimulationWorld world)
        {
            var state = world.Strategic.CharacterEncounter;
            if (state == null || state.Phase != CharacterEncounterPhase.ReadyToEnd) return Fail("Encounter cannot finish yet.");
            var settlement = world.Strategic.ManualBattleSettlement;
            if (settlement.IsCommitted || !settlement.Matches(world.Strategic.Participants)) return Fail("Encounter settlement identity mismatch.");
            var report = ManualBattleReportBuilder.CaptureFinal(world, settlement.Draft, state.PlayerWon, "");
            if (!settlement.Commit(report)) return Fail("Encounter report was already committed.");
            foreach (var p in state.Participants)
            {
                var id = new EntityId(p.CharacterId);
                if (!world.Entities.TryGet(id, out var entity) || CombatLifeStateService.ShouldHideFromSpawn(entity)) continue;
                var presence = world.WorldPresence.GetOrCreate(id);
                presence.Mode = (PartyWorldPresenceMode)p.SourceMode; presence.SiteId = p.SourceSiteId;
                presence.WorldPosX = p.OriginX; presence.WorldPosY = p.OriginY;
                presence.HasContinuousWorldPosition = true; presence.PersonalSurfaceId = state.SourceSurfaceId;
                var hex = HexMath.WorldToHex(p.OriginX, p.OriginY, world.HexWorld.HexSize);
                presence.HexQ = hex.Q; presence.HexR = hex.R;
                presence.ClearCombatPursuit();
            }
            state.Phase = CharacterEncounterPhase.Committed;
            world.Strategic.ContinuousManualCombat.ClearOwned(state.EncounterId);
            world.Strategic.Participants.Clear();
            world.Strategic.Encounter.ClearCompletedWorldCombatSession();
            world.Strategic.ClockFreeze.Clear();
            return Result.Success();
        }
        public static void CloseReport(SimulationWorld world)
        {
            var state = world.Strategic.CharacterEncounter;
            if (state == null || state.Phase != CharacterEncounterPhase.Committed) return;
            world.Strategic.ManualBattleSettlement.ClearOwned(state.EncounterId);
            world.Strategic.CharacterEncounter = null;
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static Result Fail(string message) => Result.Failure(ErrorCode.InvalidOperation, message);
    }
}
