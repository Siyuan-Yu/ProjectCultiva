using XianXia.Core.World;
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
        public EncounterSpatialOwnerKind SourceSpatialOwnerKind;
        public string SourceSquadId = "";
        /// <summary>Legacy snapshot input only.</summary>
        public string LegacySourceFormalArmyId = "";
        public float OriginX, OriginY;
        /// <summary>Immutable pre-battle physical world position used only for return.</summary>
        public float ReturnX, ReturnY;
        public float TacticalX, TacticalY;
        public ulong TargetId;
        public float Cooldown;
        public float[] ArtCooldowns = new float[CombatArtsComponent.MaxEquippedSlots];
        public float JoinedAt;
        public ManualBattleReportCondition EntryCondition;
        public bool EntryHpAvailable;
        public int EntryHp, EntryMaxHp;
    }

    public enum EncounterCandidatePhase { Undecided, Declined, Announced, Joined, Closed }
    public sealed class EncounterCandidate
    {
        public string SquadId = "";
        public EncounterCandidatePhase Phase;
        public bool Enemy;
        public int Roll = -1;
        public int AffinityDifference;
        public float ArriveAt;
        public readonly List<EncounterCharacter> Members = new List<EncounterCharacter>();
    }

    public sealed class CharacterEncounterState
    {
        public const int Format = 4;
        public int Version = Format;
        public string EncounterId = "";
        public string SourceSurfaceId = "";
        public string SourceSiteId = "";
        public float CenterX, CenterY, Width, Height;
        public float ElapsedSeconds, DecayAccumulator;
        public CharacterEncounterPhase Phase;
        public bool PlayerWon;
        public SiteCoreEncounterObjective Objective;
        public readonly List<string> ObjectiveDefenderSquads = new List<string>();
        public int RosterVersion = 1;
        public bool ContinuationUsed;
        public float DecisionAt, ArrivalDelay;
        public int RelationThreshold, ChanceBasisPoints;
        public readonly List<EncounterCandidate> Candidates = new List<EncounterCandidate>();
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
            // SPACE-01：双方均在 active Separate Space → 原地战斗，不进 Independent Encounter。
            if (SeparateSpaceCombatPolicy.AreBothInActiveSeparateSpace(world, attacker, target))
                return false;

            var board = world?.Strategic;
            if (board == null) return false;
            var state = board.CharacterEncounter;
            if (state != null)
                return (state.Phase != CharacterEncounterPhase.Active &&
                        state.Phase != CharacterEncounterPhase.ReadyToEnd) ||
                       !state.Opposing(attacker.Value, target.Value);
            var party = board.PlayerPartyContext;
            if (party == null || (!party.IsMember(attacker) && !party.IsMember(target))) return false;
            if (board.ContinuousManualCombat.IsActive) return false; // Explicit objective encounter compatibility only.
            if (IsContactSuppressed(world, attacker, target)) return true;
            if (!world.WorldPresence.TryGet(attacker, out var a) || !world.WorldPresence.TryGet(target, out var b) ||
                string.IsNullOrEmpty(a.PersonalSurfaceId) || a.PersonalSurfaceId != b.PersonalSurfaceId ||
                !a.HasContinuousWorldPosition || !b.HasContinuousWorldPosition) return true;
            var dx = a.WorldPosX - b.WorldPosX; var dy = a.WorldPosY - b.WorldPosY;
            if (dx * dx + dy * dy > MeleeCombatService.DefaultMeleeRange * MeleeCombatService.DefaultMeleeRange) return true;
            board.PendingCharacterAttacker = attacker;
            board.PendingCharacterTarget = target;
            return true;
        }

        public static string ContactKey(EntityId a, EntityId b) => a.Value < b.Value
            ? a.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + b.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : b.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + a.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        public static bool IsContactSuppressed(SimulationWorld world, EntityId a, EntityId b)
        {
            var key = ContactKey(a, b);
            if (!world.Strategic.SuppressedCharacterContacts.Contains(key)) return false;
            if (world.WorldPresence.TryGet(a, out var pa) && world.WorldPresence.TryGet(b, out var pb))
            {
                var dx = pa.WorldPosX - pb.WorldPosX; var dy = pa.WorldPosY - pb.WorldPosY;
                if (pa.PersonalSurfaceId != pb.PersonalSurfaceId || dx * dx + dy * dy >
                    4f * MeleeCombatService.DefaultMeleeRange * MeleeCombatService.DefaultMeleeRange)
                { world.Strategic.SuppressedCharacterContacts.Remove(key); return false; }
            }
            return true;
        }

        public static void AbortEntry(SimulationWorld world)
        {
            var state = world.Strategic.CharacterEncounter;
            if (state == null || state.ElapsedSeconds != 0 || state.Phase != CharacterEncounterPhase.Active) return;
            RestoreAnchors(world, state);
            world.Strategic.ManualBattleSettlement.ClearOwned(state.EncounterId);
            world.Strategic.ContinuousManualCombat.ClearOwned(state.EncounterId);
            world.Strategic.Participants.Clear(); world.Strategic.ClockFreeze.Clear();
            world.Strategic.CharacterEncounter = null;
            RestorePlayerPartyMembersFromGroup(world, state);
        }

        public static Result Prepare(SimulationWorld world, EntityId attacker, EntityId target,
            string surfaceId, out CharacterEncounterState prepared)
            => PrepareInternal(world, attacker, target, surfaceId, null, out prepared);

        public static Result PrepareForWorldSiteAssault(SimulationWorld world, EntityId attacker, EntityId defender,
            string targetSiteId, out CharacterEncounterState prepared)
        {
            prepared = null;
            var resolved = WorldSiteCoreWarfareService.Resolve(world, targetSiteId, out var target);
            if (resolved.IsFailure) return resolved;
            var valid = WorldSiteCoreWarfareService.Validate(world, attacker, target);
            if (valid.IsFailure) return valid;
            var result = PrepareInternal(world, attacker, defender, target.SurfaceId, targetSiteId, out prepared);
            if (result.IsSuccess) prepared.Objective = WorldSiteCoreWarfareService.DescribeObjective(world, target);
            return result;
        }

        static Result PrepareInternal(SimulationWorld world, EntityId attacker, EntityId target,
            string surfaceId, string explicitSiteId, out CharacterEncounterState prepared)
        {
            prepared = null;
            if (world?.Strategic?.SpatialRules == null || world.Strategic.CharacterEncounter != null ||
                StrategicClockFreezeService.IsWorldTickFrozen(world))
                return Fail("World is already owned by another encounter or lacks spatial configuration.");
            if (!CharacterEncounterSpatialAuthorityResolver.TryResolveEncounterWorldPosition(
                    world, target, surfaceId, out var contact, out var targetOwner,
                    out _, out var failure))
            {
                world.Strategic.Squads.TryGetForCharacter(target, out var targetSquad);
                return Fail("Target encounter space: " +
                    CharacterEncounterSpatialAuthorityResolver.DescribeFailure(world, target,
                        targetSquad?.SquadId, surfaceId, targetOwner, failure));
            }
            if (!world.Strategic.Squads.TryGetForCharacter(attacker, out var attackers) ||
                !world.Strategic.Squads.TryGetForCharacter(target, out var targets) || attackers == targets)
                return Fail("Encounter requires two different current squads.");
            if (!IsLiving(world, attacker.Value))
                return Fail("Encounter attacker is unavailable: " + attacker.Value);
            if (!IsLiving(world, target.Value))
                return Fail("Encounter target is unavailable: " + target.Value);
            ResolvedWorldSpatialRange wildernessRange;
            try { wildernessRange = world.Strategic.SpatialRules.ResolveWildernessEncounter(world, surfaceId); }
            catch (InvalidOperationException ex) { return Fail(ex.Message); }
            var state = new CharacterEncounterState
            {
                SourceSurfaceId = surfaceId, CenterX = contact.X, CenterY = contact.Y,
                Width = wildernessRange.WidthWorld,
                Height = wildernessRange.HeightWorld,
                Phase = CharacterEncounterPhase.Preparing
            };
            WorldSite site = null;
            if (explicitSiteId != null) world.Strategic.Sites.TryGet(explicitSiteId, out site);
            else WorldSiteAdministrativeControlResolver.TryResolve(world, surfaceId, contact.X, contact.Y, out site, out _);
            if (site != null)
            {
                state.SourceSiteId = site.SiteId;
                state.CenterX = site.CoreWorldX; state.CenterY = site.CoreWorldY;
                state.Width = site.CoreRangeWidth; state.Height = site.CoreRangeHeight;
            }
            var player = world.Strategic.PlayerPartyContext;
            var attackerFriendly = player != null && player.IsMember(attacker);
            if (!attackerFriendly && (player == null || !player.IsMember(target)))
                return Fail("Player encounter requires a physically involved player squad.");
            foreach (var squad in new[] { attackers, targets })
                foreach (var raw in squad.MemberCharacterIds)
                {
                    var id = new EntityId(raw);
                    // Squad identity survives incapacitation/death, but execution eligibility does not.
                    // Only living members join a new encounter; downed/corpse/removed members remain
                    // in their authoritative Squad and retain their existing residual position.
                    if (!IsLiving(world, raw))
                        continue;
                    if (!CharacterEncounterSpatialAuthorityResolver.TryResolveEncounterWorldPosition(
                            world, id, surfaceId, out var point, out var owner,
                            out var ownerId, out failure))
                        return Fail("Necessary squad member encounter space: " +
                            CharacterEncounterSpatialAuthorityResolver.DescribeFailure(world, id,
                                squad.SquadId, surfaceId, owner, failure));
                    if (!state.Contains(point.X, point.Y))
                        return Fail("Necessary squad member outside frozen range: " +
                            CharacterEncounterSpatialAuthorityResolver.DescribeFailure(world, id,
                                squad.SquadId, surfaceId, owner, "OutsideFrozenRange"));
                    if (world.Strategic.Participants.FindByEntity(id) != null) return Fail("Squad member already locked: " + raw);
                    state.Participants.Add(CreateEntry(world, id, squad.SquadId,
                        (squad == targets) == attackerFriendly, point, owner, ownerId));
                }
            // Reserve identity only after full read-only qualification. Character allocation is not used.
            state.EncounterId = "encounter:" + world.Entities.Ids.Next().Value;
            state.DecisionAt = world.Strategic.SpatialRules.InterventionDecisionSeconds;
            state.ArrivalDelay = world.Strategic.SpatialRules.InterventionArrivalSeconds;
            state.RelationThreshold = world.Strategic.SpatialRules.InterventionRelationThreshold;
            state.ChanceBasisPoints = world.Strategic.SpatialRules.InterventionChanceBasisPoints;
            FreezeCandidates(world, state);
            prepared = state;
            return Result.Success();
        }

        public static bool IsLiving(SimulationWorld world, ulong id) =>
            world.Entities.TryGet(new EntityId(id), out var entity) &&
            entity.TryGet<LifecycleComponent>(out var life) && life.State == LifecycleState.Alive;

        static EncounterCharacter CreateEntry(SimulationWorld world, EntityId id, string squadId,
            bool enemy, WorldVec2 point, EncounterSpatialOwnerKind owner, string ownerId)
        {
            world.WorldPresence.TryGet(id, out var presence);
            var sourceMode = presence?.Mode ?? PartyWorldPresenceMode.AtWorldPosition;
            var sourceSiteId = presence?.SiteId ?? string.Empty;
            if (owner == EncounterSpatialOwnerKind.PlayerParty)
            {
                var motion = world.PlayerPartyTravel;
                sourceMode = motion.LocationKind == PlayerPartyLocationKind.AtWorldSite
                    ? PartyWorldPresenceMode.AtSite : PartyWorldPresenceMode.AtWorldPosition;
                sourceSiteId = motion.LocationKind == PlayerPartyLocationKind.AtWorldSite
                    ? motion.SiteId : motion.CurrentOutdoorWorldSiteId;
            }
            return new EncounterCharacter
            {
                CharacterId = id.Value, SquadId = squadId, Enemy = enemy,
                SourceMode = (int)sourceMode,
                SourceSiteId = sourceSiteId ?? string.Empty,
                SourceSpatialOwnerKind = owner,
                SourceSquadId = owner == EncounterSpatialOwnerKind.Squad ? ownerId ?? string.Empty : string.Empty,
                LegacySourceFormalArmyId = string.Empty,
                OriginX = point.X, OriginY = point.Y,
                ReturnX = point.X, ReturnY = point.Y,
                TacticalX = point.X, TacticalY = point.Y
            };
        }

        /// <summary>
        /// Active and post-battle CharacterEncounter phases exclusively own participant spatial
        /// state. Lifecycle transitions must not hand the character to ordinary world placement.
        /// </summary>
        public static bool OwnsParticipantSpatialState(SimulationWorld world, EntityId id)
        {
            var state = world?.Strategic?.CharacterEncounter;
            return state != null && !id.IsNone &&
                   (state.Phase == CharacterEncounterPhase.Active ||
                    state.Phase == CharacterEncounterPhase.ReadyToEnd) &&
                   state.Find(id.Value) != null;
        }

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
            MigrateLegacySpatialOwners(world, state);
            if (state.Version != CharacterEncounterState.Format || string.IsNullOrWhiteSpace(state.EncounterId) ||
                string.IsNullOrWhiteSpace(state.SourceSurfaceId) || !Finite(state.CenterX) || !Finite(state.CenterY) ||
                !Finite(state.Width) || !Finite(state.Height) || state.Width <= 0f || state.Height <= 0f ||
                !Finite(state.ElapsedSeconds) || state.ElapsedSeconds < 0f ||
                (state.Phase != CharacterEncounterPhase.Active && state.Phase != CharacterEncounterPhase.ReadyToEnd)) return Fail("Invalid independent encounter snapshot.");
            if (!Finite(state.DecisionAt) || state.DecisionAt < 0 || !Finite(state.ArrivalDelay) || state.ArrivalDelay < 0 ||
                state.RelationThreshold < 1 || state.RelationThreshold > 100 || state.ChanceBasisPoints < 0 || state.ChanceBasisPoints > 10000 ||
                !Finite(state.DecayAccumulator) || state.DecayAccumulator < 0 || state.DecayAccumulator >= 1 || state.RosterVersion < 1)
                return Fail("Invalid encounter timing or intervention rules.");
            var seen = new HashSet<ulong>(); var friendly = 0; var enemy = 0;
            foreach (var p in state.Participants)
            {
                if (p == null || p.CharacterId == 0 || !seen.Add(p.CharacterId) ||
                    !world.Entities.TryGet(new EntityId(p.CharacterId), out _) ||
                    !Finite(p.OriginX) || !Finite(p.OriginY) || !Finite(p.ReturnX) || !Finite(p.ReturnY) ||
                    !Finite(p.TacticalX) || !Finite(p.TacticalY) ||
                    !state.Contains(p.OriginX, p.OriginY) || !state.Contains(p.ReturnX, p.ReturnY) ||
                    !state.Contains(p.TacticalX, p.TacticalY) ||
                    !Finite(p.Cooldown) || p.Cooldown < 0f || !Finite(p.JoinedAt) || p.JoinedAt < 0 ||
                    p.JoinedAt > state.ElapsedSeconds || string.IsNullOrEmpty(p.SquadId) ||
                    !Enum.IsDefined(typeof(PartyWorldPresenceMode), p.SourceMode) || p.SourceMode == (int)PartyWorldPresenceMode.InEncounter ||
                    !Enum.IsDefined(typeof(EncounterSpatialOwnerKind), p.SourceSpatialOwnerKind) ||
                    (p.SourceSpatialOwnerKind == EncounterSpatialOwnerKind.Squad &&
                     string.IsNullOrEmpty(p.SourceSquadId)) ||
                    !Enum.IsDefined(typeof(ManualBattleReportCondition), p.EntryCondition) ||
                    (p.EntryHpAvailable && (p.EntryHp < 0 || p.EntryMaxHp <= 0 || p.EntryHp > p.EntryMaxHp))) return Fail("Invalid encounter participant snapshot.");
                if (p.ArtCooldowns == null || p.ArtCooldowns.Length != CombatArtsComponent.MaxEquippedSlots)
                    return Fail("Invalid encounter skill cooldown slots.");
                foreach (var cooldown in p.ArtCooldowns)
                    if (!Finite(cooldown) || cooldown < 0) return Fail("Invalid skill cooldown.");
                if (p.Enemy) enemy++; else friendly++;
            }
            var candidateIds = new HashSet<ulong>(); var squadIds = new HashSet<string>(); var joined = 0;
            foreach (var c in state.Candidates)
            {
                if (c == null || string.IsNullOrEmpty(c.SquadId) || !squadIds.Add(c.SquadId) || c.Members.Count == 0 ||
                    !Enum.IsDefined(typeof(EncounterCandidatePhase), c.Phase) || c.Roll < -1 || c.Roll >= 10000 ||
                    !Finite(c.ArriveAt) || c.ArriveAt < 0 ||
                    (c.Phase == EncounterCandidatePhase.Undecided && c.Roll != -1) ||
                    ((c.Phase == EncounterCandidatePhase.Announced || c.Phase == EncounterCandidatePhase.Joined) && c.Roll < 0))
                    return Fail("Invalid frozen candidate state.");
                if (c.Phase == EncounterCandidatePhase.Joined) joined++;
                foreach (var m in c.Members)
                {
                    if (m == null || m.CharacterId == 0 || !candidateIds.Add(m.CharacterId) || m.SquadId != c.SquadId ||
                        !world.Entities.TryGet(new EntityId(m.CharacterId), out _) ||
                        !Finite(m.OriginX) || !Finite(m.OriginY) || !Finite(m.ReturnX) || !Finite(m.ReturnY) ||
                        !Finite(m.TacticalX) || !Finite(m.TacticalY) ||
                        !Enum.IsDefined(typeof(EncounterSpatialOwnerKind), m.SourceSpatialOwnerKind) ||
                        (m.SourceSpatialOwnerKind == EncounterSpatialOwnerKind.Squad &&
                         string.IsNullOrEmpty(m.SourceSquadId)) ||
                        !state.Contains(m.OriginX, m.OriginY) || !state.Contains(m.ReturnX, m.ReturnY) ||
                        !state.Contains(m.TacticalX, m.TacticalY))
                        return Fail("Invalid candidate personal anchor.");
                    var actual = state.Find(m.CharacterId);
                    if (c.Phase == EncounterCandidatePhase.Joined)
                    {
                        if (actual == null || actual.SquadId != c.SquadId || actual.Enemy != c.Enemy ||
                            actual.OriginX != m.OriginX || actual.OriginY != m.OriginY || actual.JoinedAt <= 0)
                            return Fail("Joined candidate/roster mismatch.");
                    }
                    else if (actual != null && !state.ObjectiveDefenderSquads.Contains(c.SquadId)) return Fail("Unjoined candidate in actual roster.");
                }
            }
            if (state.RosterVersion != 1 + joined + state.ObjectiveDefenderSquads.Count) return Fail("Encounter roster version mismatch.");
            var objectiveSquads = new HashSet<string>();
            if (state.Objective == null && state.ObjectiveDefenderSquads.Count > 0) return Fail("Objective defender history lacks objective.");
            foreach (var squad in state.ObjectiveDefenderSquads)
                if (string.IsNullOrEmpty(squad) || !objectiveSquads.Add(squad) || !state.Participants.Exists(p => p.SquadId == squad && p.Enemy))
                    return Fail("Invalid objective defender squad.");
            if (state.Objective != null)
            {
                var o = state.Objective;
                if ((o.Kind != SiteCoreObjectiveKind.FixedSiteCoreCapture && o.Kind != SiteCoreObjectiveKind.RemovableFactionFlagDestruction) ||
                    string.IsNullOrEmpty(o.AssetId) || string.IsNullOrEmpty(o.AttackerFactionId) || string.IsNullOrEmpty(o.DefenderFactionId) ||
                    o.AttackerFactionId == o.DefenderFactionId || string.IsNullOrWhiteSpace(o.SiteId) ||
                    (o.Resolved && state.Phase != CharacterEncounterPhase.ReadyToEnd)) return Fail("Invalid SiteCore objective snapshot.");
            }
            return friendly > 0 && enemy > 0 ? Result.Success() : Fail("Encounter lacks two sides.");
        }

        // Political Sites are restored after the static Content shell, not during entity RestoreJson.
        public static Result ValidateObjectiveWorldState(SimulationWorld world)
        {
            var state = world.Strategic.CharacterEncounter;
            var o = state?.Objective;
            if (o == null) return Result.Success();
            if (!world.Strategic.Sites.TryGet(o.SiteId, out var site) || site.CoreAssetId != o.AssetId ||
                site.CoreIsRemovable != (o.Kind == SiteCoreObjectiveKind.RemovableFactionFlagDestruction) ||
                site.CoreSurfaceId != state.SourceSurfaceId || !state.Contains(site.CoreWorldX, site.CoreWorldY) ||
                (!o.Resolved && (!site.IsCoreActive || site.OwnerFactionId != o.DefenderFactionId)) ||
                (o.Resolved && (site.CoreIsRemovable ? site.IsCoreActive || site.OwnerFactionId != o.DefenderFactionId : site.OwnerFactionId != o.AttackerFactionId)))
                return Fail("Invalid SiteCore objective world link.");
            return Result.Success();
        }

        public static Result TryJoinObjectiveDefenderSquad(SimulationWorld world, EntityId defender,
            Func<EncounterCandidate, bool> preparePlacement = null)
        {
            var state = world?.Strategic?.CharacterEncounter;
            if (state == null || (state.Phase != CharacterEncounterPhase.Active && state.Phase != CharacterEncounterPhase.ReadyToEnd) ||
                state.Objective == null || state.Objective.Resolved ||
                !world.Strategic.Squads.TryGetForCharacter(defender, out var squad)) return Fail("Objective defender cannot join.");
            if (!world.Entities.TryGet(defender, out var defendingEntity) || !IsLiving(world, defender.Value) ||
                !defendingEntity.TryGet<XianXia.Core.Social.FactionMembershipComponent>(out var membership) || !membership.IsAffiliated ||
                !WorldSiteDefenseCharacterQuery.IsDefenderSide(world, membership.FactionId, state.Objective.AttackerFactionId, state.Objective.DefenderFactionId))
                return Fail("人物不属于目标战争防守侧。");
            if (state.Participants.Exists(p => p.SquadId == squad.SquadId))
                return state.Participants.Exists(p => p.SquadId == squad.SquadId && !p.Enemy) ? Fail("该小队已经在本场友方参战。") : Result.Success();
            var joining = new EncounterCandidate { SquadId = squad.SquadId, Enemy = true };
            foreach (var raw in squad.MemberCharacterIds)
            {
                var id = new EntityId(raw);
                if (!IsLiving(world, raw)) continue;
                if (state.Find(raw) != null || world.Strategic.Participants.FindByEntity(id) != null)
                    return Fail("守军小队成员已被当前战场占用。CharacterId=" + raw);
                if (!CharacterEncounterSpatialAuthorityResolver.TryResolveEncounterWorldPosition(
                        world, id, state.SourceSurfaceId, out var point, out var owner,
                        out var ownerId, out var failure))
                    return Fail("守军小队成员空间校验失败：" +
                        CharacterEncounterSpatialAuthorityResolver.DescribeFailure(world, id,
                            squad.SquadId, state.SourceSurfaceId, owner, failure));
                if (!state.Contains(point.X, point.Y))
                    return Fail("守军小队成员不在当前战场范围内。CharacterId=" + raw);
                var entry = CreateEntry(world, id, squad.SquadId, true, point, owner, ownerId);
                entry.JoinedAt = state.ElapsedSeconds;
                joining.Members.Add(entry);
            }
            if (joining.Members.Count == 0 || (preparePlacement != null && !preparePlacement(joining))) return Fail("守军无法进入当前战场。");
            foreach (var member in joining.Members)
            {
                world.Entities.TryGet(new EntityId(member.CharacterId), out var entity);
                CombatDamageRules.EnsureVitals(entity);
                ManualBattleReportBuilder.CaptureState(world, new EntityId(member.CharacterId), out member.EntryCondition,
                    out member.EntryHpAvailable, out member.EntryHp, out member.EntryMaxHp);
            }
            state.Participants.AddRange(joining.Members);
            state.ObjectiveDefenderSquads.Add(squad.SquadId);
            state.RosterVersion++;
            state.Phase = CharacterEncounterPhase.Active;
            BindRuntime(world);
            return Result.Success();
        }

        public static void NotifyStrategicObjectiveResolved(SimulationWorld world, string siteId, string assetId)
        {
            var state = world?.Strategic?.CharacterEncounter;
            if (state?.Objective == null || state.Objective.SiteId != siteId || state.Objective.AssetId != assetId ||
                (state.Phase != CharacterEncounterPhase.Active && state.Phase != CharacterEncounterPhase.ReadyToEnd)) return;
            if (!world.Strategic.Sites.TryGet(siteId, out var site) || site.CoreAssetId != assetId) return;
            if (site.CoreIsRemovable)
            {
                if (site.IsCoreActive || world.Strategic.FactionFlags.Flags.ContainsKey(assetId) ||
                    state.Objective.Kind != SiteCoreObjectiveKind.RemovableFactionFlagDestruction) return;
            }
            else if (site.OwnerFactionId != state.Objective.AttackerFactionId || state.Objective.Kind != SiteCoreObjectiveKind.FixedSiteCoreCapture) return;
            state.Objective.Resolved = true;
            state.PlayerWon = true;
            state.Phase = CharacterEncounterPhase.ReadyToEnd;
            world.Strategic.ClockFreeze.Reason = StrategicClockFreezeReason.PostBattle;
        }

        public static void BindRuntime(SimulationWorld world)
        {
            var state = world.Strategic.CharacterEncounter;
            if (state == null || state.Phase == CharacterEncounterPhase.Committed) return;
            ReconcileRuntimePresence(world);
            var snapshot = world.Strategic.Participants;
            snapshot.Clear(); snapshot.OfferId = state.EncounterId;
            var draft = new ManualBattleReportDraft { OfferId = state.EncounterId };
            foreach (var p in state.Participants)
            {
                var id = new EntityId(p.CharacterId);
                snapshot.Add(new BattleParticipantRecord { EntityId = id, SquadId = p.SquadId, Selected = true,
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

        /// <summary>Reasserts only the spatial runtime projection of the authoritative encounter
        /// roster. It does not rebuild participants, settlement, reports, or combat events.</summary>
        public static int ReconcileRuntimePresence(SimulationWorld world)
        {
            var state = world?.Strategic?.CharacterEncounter;
            if (state == null ||
                (state.Phase != CharacterEncounterPhase.Active &&
                 state.Phase != CharacterEncounterPhase.ReadyToEnd))
                return 0;
            var repaired = 0;
            foreach (var participant in state.Participants)
            {
                var id = new EntityId(participant.CharacterId);
                if (!world.Entities.TryGet(id, out var entity) || entity == null ||
                    entity.TryGet<LifecycleComponent>(out var life) && life.IsRemoved)
                    continue;
                var presence = world.WorldPresence.GetOrCreate(id);
                if (presence.Mode != PartyWorldPresenceMode.InEncounter ||
                    !string.Equals(presence.PersonalSurfaceId, state.SourceSurfaceId,
                        StringComparison.Ordinal) ||
                    !presence.HasContinuousWorldPosition ||
                    presence.WorldPosX != participant.TacticalX ||
                    presence.WorldPosY != participant.TacticalY)
                    repaired++;
                presence.Mode = PartyWorldPresenceMode.InEncounter;
                presence.PersonalSurfaceId = state.SourceSurfaceId;
                presence.WorldPosX = participant.TacticalX;
                presence.WorldPosY = participant.TacticalY;
                presence.HasContinuousWorldPosition = true;
            }
            return repaired;
        }

        public static void Advance(SimulationWorld world, float seconds, Func<EncounterCandidate, bool> preparePlacement = null)
        {
            var state = world.Strategic.CharacterEncounter;
            if (state == null || state.Phase != CharacterEncounterPhase.Active || !Finite(seconds) || seconds <= 0f) return;
            AdvanceTacticalTime(world, state, seconds);
            AdvanceCandidates(world, state, preparePlacement);
            var friendly = false; var enemy = false;
            foreach (var p in state.Participants)
                if (IsLiving(world, p.CharacterId)) { if (p.Enemy) enemy = true; else friendly = true; }
            if (!friendly || !enemy)
            {
                var pendingArrival = state.Candidates.Exists(c => c.Phase == EncounterCandidatePhase.Announced &&
                    !state.ObjectiveDefenderSquads.Contains(c.SquadId));
                if (pendingArrival)
                {
                    // One bounded continuation; announced arrivals all expire at their saved deadline.
                    state.ContinuationUsed = true;
                    return;
                }
                foreach (var c in state.Candidates)
                    if (c.Phase == EncounterCandidatePhase.Undecided) c.Phase = EncounterCandidatePhase.Closed;
                state.PlayerWon = friendly;
                state.Phase = CharacterEncounterPhase.ReadyToEnd;
                world.Strategic.ClockFreeze.Reason = StrategicClockFreezeReason.PostBattle;
            }
        }

        /// <summary>Post-battle tactical time while the player remains on the frozen field.
        /// Strategic ticks, candidates, schedules, travel and production are intentionally absent.</summary>
        public static void AdvanceReadyToEnd(SimulationWorld world, float seconds)
        {
            var state = world?.Strategic?.CharacterEncounter;
            if (state == null || state.Phase != CharacterEncounterPhase.ReadyToEnd ||
                !Finite(seconds) || seconds <= 0f)
                return;
            AdvanceTacticalTime(world, state, seconds);
            foreach (var participant in state.Participants)
                participant.Cooldown = Math.Max(0, participant.Cooldown - seconds);
        }

        static void AdvanceTacticalTime(
            SimulationWorld world,
            CharacterEncounterState state,
            float seconds)
        {
            state.ElapsedSeconds += seconds;
            state.DecayAccumulator += seconds;
            foreach (var participant in state.Participants)
                for (var i = 0; i < participant.ArtCooldowns.Length; i++)
                    participant.ArtCooldowns[i] = Math.Max(0, participant.ArtCooldowns[i] - seconds);
            while (state.DecayAccumulator >= 1f)
            {
                state.DecayAccumulator -= 1f;
                CombatLifeStateService.TickEncounterLifeDecay(world, state.Participants);
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
            RestoreAnchors(world, state);
            foreach (var a in state.Participants)
                foreach (var b in state.Participants)
                    if (a.Enemy != b.Enemy)
                        world.Strategic.SuppressedCharacterContacts.Add(ContactKey(new EntityId(a.CharacterId), new EntityId(b.CharacterId)));
            world.Strategic.PendingCharacterAttacker = world.Strategic.PendingCharacterTarget = EntityId.None;
            foreach (var c in state.Candidates)
                if (c.Phase != EncounterCandidatePhase.Joined) c.Phase = EncounterCandidatePhase.Closed;
            state.Phase = CharacterEncounterPhase.Committed;
            world.Strategic.ContinuousManualCombat.ClearOwned(state.EncounterId);
            world.Strategic.Participants.Clear();
            world.Strategic.ClockFreeze.Clear();
            RestorePlayerPartyMembersFromGroup(world, state);
            return Result.Success();
        }
        static void RestoreAnchors(SimulationWorld world, CharacterEncounterState state)
        {
            foreach (var p in state.Participants)
            {
                var id = new EntityId(p.CharacterId);
                if (!world.Entities.TryGet(id, out var entity) || CombatLifeStateService.ShouldHideFromSpawn(entity)) continue;
                var presence = world.WorldPresence.GetOrCreate(id);
                if (p.SourceSpatialOwnerKind == EncounterSpatialOwnerKind.Squad ||
                    p.SourceSpatialOwnerKind == EncounterSpatialOwnerKind.PlayerParty)
                {
                    if (IsLiving(world, p.CharacterId))
                    {
                        if (p.SourceSpatialOwnerKind == EncounterSpatialOwnerKind.Squad &&
                            world.Strategic.Squads.TryGetForCharacter(id, out var currentSquad) &&
                            string.Equals(currentSquad.SquadId, p.SourceSquadId, StringComparison.Ordinal) &&
                            world.Strategic.SquadWorldMotions.TryGet(currentSquad.SquadId, out var currentMotion) &&
                            SquadWorldMotionService.IsActiveNpcSquadAuthority(
                            world, currentSquad, currentMotion))
                        {
                            continue;
                        }
                        if (p.SourceSpatialOwnerKind == EncounterSpatialOwnerKind.PlayerParty &&
                            world.Strategic.PlayerPartyContext?.IsMember(id) == true)
                        {
                            continue;
                        }
                    }
                    // Group projection is restored after the encounter lock is released.
                    // A downed, dead or detached participant returns to its immutable pre-battle
                    // physical point; tactical battlefield movement never leaks into world state.
                    presence.Mode = PartyWorldPresenceMode.AtWorldPosition;
                    presence.SiteId = string.Empty;
                    presence.WorldPosX = p.ReturnX; presence.WorldPosY = p.ReturnY;
                    presence.HasContinuousWorldPosition = true;
                    presence.PersonalSurfaceId = state.SourceSurfaceId;
                    presence.ClearHexPresence();
                    continue;
                }
                var normalContinuous = ContinuousOutdoorGameplayPolicy.IsNormalContinuousOutdoor(world);
                presence.Mode = normalContinuous
                    ? PartyWorldPresenceMode.AtWorldPosition
                    : (PartyWorldPresenceMode)p.SourceMode;
                presence.SiteId = normalContinuous ? string.Empty : p.SourceSiteId;
                presence.WorldPosX = p.ReturnX; presence.WorldPosY = p.ReturnY;
                presence.HasContinuousWorldPosition = true; presence.PersonalSurfaceId = state.SourceSurfaceId;
                if (normalContinuous)
                    presence.ClearHexPresence();
                else if (world.LegacyHexWorld != null)
                {
                    var hex = HexMath.WorldToHex(p.ReturnX, p.ReturnY, world.LegacyHexWorld.HexSize);
                    presence.HexQ = hex.Q; presence.HexR = hex.R;
                }
            }
        }

        static void MigrateLegacySpatialOwners(SimulationWorld world, CharacterEncounterState state)
        {
            Action<EncounterCharacter> migrate = participant =>
            {
                if (participant == null ||
                    participant.SourceSpatialOwnerKind != EncounterSpatialOwnerKind.LegacyFormalArmy)
                    return;
                var id = new EntityId(participant.CharacterId);
                if (world.Strategic.Squads.TryGetForCharacter(id, out var squad) && squad != null)
                {
                    participant.SourceSpatialOwnerKind = EncounterSpatialOwnerKind.Squad;
                    participant.SourceSquadId = squad.SquadId;
                    participant.SquadId = squad.SquadId;
                }
                else
                {
                    participant.SourceSpatialOwnerKind = EncounterSpatialOwnerKind.Personal;
                    participant.SourceSquadId = string.Empty;
                }
                participant.LegacySourceFormalArmyId = string.Empty;
            };
            foreach (var participant in state.Participants) migrate(participant);
            foreach (var candidate in state.Candidates)
                if (candidate != null) foreach (var member in candidate.Members) migrate(member);
        }

        static void RestorePlayerPartyMembersFromGroup(SimulationWorld world,
            CharacterEncounterState state)
        {
            var party = world.Strategic.PlayerPartyContext;
            if (party == null) return;
            foreach (var participant in state.Participants)
                if (participant.SourceSpatialOwnerKind == EncounterSpatialOwnerKind.PlayerParty &&
                    IsLiving(world, participant.CharacterId) &&
                    party.IsMember(new EntityId(participant.CharacterId)))
                {
                    PlayerPartyTransitionMembership.ReconcilePlayerPartyMemberWorldPresenceFromMotion(
                        world, party, "CharacterEncounterReturn");
                    return;
                }
        }
        public static void CloseReport(SimulationWorld world)
        {
            var state = world.Strategic.CharacterEncounter;
            if (state == null || state.Phase != CharacterEncounterPhase.Committed) return;
            world.Strategic.ManualBattleSettlement.ClearOwned(state.EncounterId);
            world.Strategic.CharacterEncounter = null;
        }
        static void FreezeCandidates(SimulationWorld world, CharacterEncounterState state)
        {
            var squads = new List<SquadState>(world.Strategic.Squads.Squads.Values);
            squads.Sort((a, b) => string.CompareOrdinal(a.SquadId, b.SquadId));
            foreach (var squad in squads)
            {
                if (state.Participants.Exists(p => p.SquadId == squad.SquadId)) continue;
                var candidate = new EncounterCandidate { SquadId = squad.SquadId };
                foreach (var raw in squad.MemberCharacterIds)
                {
                    var id = new EntityId(raw);
                    if (!IsLiving(world, raw) || world.Strategic.Participants.FindByEntity(id) != null ||
                        !CharacterEncounterSpatialAuthorityResolver.TryResolveEncounterWorldPosition(
                            world, id, state.SourceSurfaceId, out var point, out var owner,
                            out var ownerId, out _) ||
                        !state.Contains(point.X, point.Y)) continue;
                    candidate.Members.Add(CreateEntry(world, id, squad.SquadId,
                        false, point, owner, ownerId));
                }
                if (candidate.Members.Count > 0) state.Candidates.Add(candidate);
            }
        }

        static bool QualifyCandidate(SimulationWorld world, CharacterEncounterState state, EncounterCandidate candidate)
        {
            if (candidate.Members.Count == 0) return false;
            foreach (var member in candidate.Members)
            {
                var id = new EntityId(member.CharacterId);
                if (state.Find(member.CharacterId) != null || !IsLiving(world, member.CharacterId) ||
                    !world.Strategic.Squads.TryGetForCharacter(id, out var squad) ||
                    squad.SquadId != candidate.SquadId || !state.Contains(member.OriginX, member.OriginY) ||
                    !CharacterEncounterSpatialAuthorityResolver.TryResolveEncounterWorldPosition(
                        world, id, state.SourceSurfaceId, out var current, out var owner,
                        out var ownerId, out _) ||
                    owner != member.SourceSpatialOwnerKind ||
                    !string.Equals(owner == EncounterSpatialOwnerKind.Squad ? ownerId : string.Empty,
                        member.SourceSquadId, StringComparison.Ordinal) ||
                    Math.Abs(current.X - member.OriginX) > .0001f ||
                    Math.Abs(current.Y - member.OriginY) > .0001f) return false;
            }
            return true;
        }

        public static bool DecideCandidate(SimulationWorld world, EncounterCandidate candidate, bool manualAccept = false)
        {
            var state = world?.Strategic?.CharacterEncounter;
            if (state == null || state.Phase != CharacterEncounterPhase.Active || state.ContinuationUsed ||
                !state.Candidates.Contains(candidate) || candidate.Phase != EncounterCandidatePhase.Undecided) return false;
            if (!QualifyCandidate(world, state, candidate)) { candidate.Phase = EncounterCandidatePhase.Closed; return false; }
            long difference = 0;
            foreach (var member in candidate.Members)
            {
                var friendly = 0; var enemy = 0;
                foreach (var initial in state.Participants)
                {
                    if (initial.JoinedAt > 0f || state.ObjectiveDefenderSquads.Contains(initial.SquadId)) continue; // No recursive relationship cascade.
                    var affinity = world.Relationships.Score(new EntityId(member.CharacterId), new EntityId(initial.CharacterId));
                    if (initial.Enemy) enemy = Math.Max(enemy, affinity); else friendly = Math.Max(friendly, affinity);
                }
                difference += friendly - enemy;
            }
            candidate.AffinityDifference = (int)(difference / candidate.Members.Count);
            candidate.Enemy = candidate.AffinityDifference < 0;
            candidate.Roll = world.Random.NextInt(0, 10000); // One group draw, persisted even when declining.
            if (Math.Abs(candidate.AffinityDifference) < state.RelationThreshold ||
                (!manualAccept && candidate.Roll >= state.ChanceBasisPoints))
            { candidate.Phase = EncounterCandidatePhase.Declined; return false; }
            candidate.Phase = EncounterCandidatePhase.Announced;
            candidate.ArriveAt = state.ElapsedSeconds + state.ArrivalDelay;
            return true;
        }

        static void AdvanceCandidates(SimulationWorld world, CharacterEncounterState state, Func<EncounterCandidate, bool> preparePlacement)
        {
            foreach (var candidate in state.Candidates)
            {
                if (state.ObjectiveDefenderSquads.Contains(candidate.SquadId)) continue;
                if (candidate.Phase == EncounterCandidatePhase.Undecided && state.ElapsedSeconds >= state.DecisionAt)
                    DecideCandidate(world, candidate);
                if (candidate.Phase != EncounterCandidatePhase.Announced || state.ElapsedSeconds < candidate.ArriveAt) continue;
                if (!QualifyCandidate(world, state, candidate) || preparePlacement == null || !preparePlacement(candidate))
                { candidate.Phase = EncounterCandidatePhase.Closed; continue; }
                // All qualification and placement checks precede the single roster/report transaction.
                foreach (var member in candidate.Members)
                {
                    member.Enemy = candidate.Enemy;
                    member.JoinedAt = Math.Max(float.Epsilon, state.ElapsedSeconds);
                    world.Entities.TryGet(new EntityId(member.CharacterId), out var entity);
                    CombatDamageRules.EnsureVitals(entity);
                    ManualBattleReportBuilder.CaptureState(world, new EntityId(member.CharacterId), out member.EntryCondition,
                        out member.EntryHpAvailable, out member.EntryHp, out member.EntryMaxHp);
                }
                state.Participants.AddRange(candidate.Members);
                candidate.Phase = EncounterCandidatePhase.Joined;
                state.RosterVersion++;
                BindRuntime(world); // Rebuild projections from preserved entry baselines, never current HP of earlier members.
            }
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static Result Fail(string message) => Result.Failure(ErrorCode.InvalidOperation, message);
    }
}
