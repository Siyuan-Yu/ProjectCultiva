using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Content
{
    public enum QuestCompanionState { Active = 0, PendingDeparture = 1 }
    public sealed class QuestCompanionBinding
    {
        public EntityId CompanionEntityId { get; internal set; }
        public string QuestInstanceId { get; internal set; } = "";
        public string OriginalSquadId { get; internal set; } = "";
        public QuestCompanionState State { get; internal set; }
        internal QuestCompanionBinding Clone() => (QuestCompanionBinding)MemberwiseClone();
    }
    public sealed class QuestCompanionBoard
    {
        readonly Dictionary<EntityId, QuestCompanionBinding> _bindings = new Dictionary<EntityId, QuestCompanionBinding>();
        public IReadOnlyDictionary<EntityId, QuestCompanionBinding> Bindings => _bindings;
        public bool TryGet(EntityId id, out QuestCompanionBinding binding) => _bindings.TryGetValue(id, out binding);
        internal void Add(QuestCompanionBinding binding) => _bindings.Add(binding.CompanionEntityId, binding);
        internal void Remove(EntityId id) => _bindings.Remove(id);
        internal List<QuestCompanionBinding> Capture()
        {
            var result = new List<QuestCompanionBinding>();
            foreach (var b in _bindings.Values) result.Add(b.Clone());
            result.Sort((a,b) => a.CompanionEntityId.Value.CompareTo(b.CompanionEntityId.Value));
            return result;
        }
        internal void Restore(IEnumerable<QuestCompanionBinding> bindings)
        { _bindings.Clear(); foreach (var b in bindings) Add(b.Clone()); }
    }
    public sealed class SecretRealmQuestSocialTopic
    {
        public string QuestInstanceId { get; internal set; }
        public string QuestDefinitionId { get; internal set; }
        public string QuestName { get; internal set; }
        public string DisplayTopic => "关于【" + QuestName + "】";
    }
    public static class SecretRealmQuestSocialTopicQuery
    {
        public const int Priority = 0;
        public static List<SecretRealmQuestSocialTopic> Query(SimulationWorld world, EntityId actor, EntityId target)
        {
            var result = new List<SecretRealmQuestSocialTopic>();
            if (world == null || !world.Entities.TryGet(target, out var entity) ||
                (entity.Tags & (EntityTag.Character | EntityTag.Npc)) == 0 ||
                world.Strategic.PlayerPartyContext?.IsMember(actor) != true) return result;
            foreach (var q in world.Quests.Runtime.Values)
                if (q.Status == QuestStatus.Active && world.Quests.TryGetSpec(q.QuestId, out var spec) && spec.IsSecretRealm)
                    result.Add(new SecretRealmQuestSocialTopic { QuestInstanceId = q.QuestInstanceId,
                        QuestDefinitionId = q.QuestId, QuestName = string.IsNullOrEmpty(spec.Name) ? spec.Id : spec.Name });
            result.Sort((a,b) => StringComparer.Ordinal.Compare(a.QuestInstanceId,b.QuestInstanceId));
            return result;
        }
    }
    public sealed class QuestCompanionInviteResult
    {
        public bool CanInvite { get; internal set; }
        public string Reason { get; internal set; }
        public int RelationshipScore { get; internal set; }
        public int RequiredScore => QuestCompanionService.SecretRealmQuestInviteMinScore;
    }
    public static class QuestCompanionService
    {
        public const int SecretRealmQuestInviteMinScore = 20;
        public static QuestCompanionInviteResult EvaluateInvite(SimulationWorld world, EntityId actor, EntityId target, string questInstanceId)
        {
            var result = new QuestCompanionInviteResult { RelationshipScore = world?.Relationships.Score(target, actor) ?? 0 };
            string reason = null;
            var party = world?.Strategic?.PlayerPartyContext;
            if (world == null || !world.Quests.TryGet(questInstanceId, out var q) || q.Status != QuestStatus.Active ||
                !world.Quests.TryGetSpec(q.QuestId, out var spec) || !spec.IsSecretRealm) reason = "这件事已经不需要同行了。";
            else if (party == null || !party.IsMember(actor) || !PlayerPartyRuntime.CanActAsActive(world, actor, out _) ||
                !party.HasActive || !PlayerPartyRuntime.CanActAsActive(world, party.ActiveCharacterId, out _)) reason = "现在还无法一起出发。";
            else if (!world.Entities.TryGet(target, out var npc) || (npc.Tags & (EntityTag.Character | EntityTag.Npc)) == 0 ||
                !PlayerPartyRuntime.CanActAsActive(world, target, out _)) reason = "我现在无法随你行动。";
            else if (IsHostile(world, actor, target)) reason = "我们之间的冲突还没有解决。";
            else if (BattleBlocked(world) || IsMemberBattleLocked(world, target) || IsMemberBattleLocked(world, actor)) reason = "眼下战事未了，不能动身。";
            else if (party.IsMember(target) || world.QuestCompanions.TryGet(target, out _)) reason = "我已经在同行了。";
            else if (party.Count >= PlayerPartyRuntime.MaxMembers) reason = "你们同行的人已经够多了。";
            else if (world.WorldOpportunities.TryGetByEntity(target, out _)) reason = "我目前的身份无法作为秘境同行者。";
            else if (!PlayerPartyLocalCoPresenceQuery.Evaluate(world, party, actor).IsCoPresent ||
                !PlayerPartyLocalCoPresenceQuery.Evaluate(world, party, target).IsCoPresent) reason = "先到同一处地方再谈同行吧。";
            else if (result.RelationshipScore < SecretRealmQuestInviteMinScore) reason = "我们还没熟到这个程度。";
            result.CanInvite = reason == null; result.Reason = reason ?? "好，我与你同去。";
            return result;
        }
        static bool IsHostile(SimulationWorld world, EntityId actor, EntityId target)
        {
            world.Entities.TryGet(target, out var npc);
            if (npc != null && npc.TryGet<PersonalityProfileComponent>(out var profile) && profile.HasTag("hostile")) return true;
            var a = CharacterStrategicQuery.ResolveFactionId(world, actor);
            var b = CharacterStrategicQuery.ResolveFactionId(world, target);
            return WarGateService.IsAtWar(world,a,b) || (!string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b) && world.Strategic.Diplomacy.IsHostile(a,b));
        }
        static bool IsMemberBattleLocked(SimulationWorld world, EntityId id)
        {
            world.Strategic.Squads.TryGetForCharacter(id, out var squad);
            return SquadMembershipService.IsBattleLocked(world, squad);
        }
        public static bool BattleBlocked(SimulationWorld world) =>
            CharacterEncounterService.BlocksOrdinaryContinuousSurface(world) || StrategicClockFreezeService.IsWorldTickFrozen(world) ||
            world?.Strategic?.ContinuousManualCombat?.IsActive == true;

        public static QuestCompanionInviteResult TryInvite(SimulationWorld world, EntityId actor, EntityId target, string questInstanceId, bool hostCanJoin = true)
        {
            var result = EvaluateInvite(world, actor, target, questInstanceId);
            if (!result.CanInvite) return result;
            if (!hostCanJoin) { result.CanInvite = false; result.Reason = "眼下暂时无法动身，稍后再谈吧。"; return result; }
            world.Strategic.Squads.TryGetForCharacter(target, out var source);
            var original = source?.SquadId ?? SquadMembershipService.SingletonSquadId(target);
            var party = world.Strategic.PlayerPartyContext;
            if (!party.TryAddQuestCompanion(world, target, out _))
            { result.CanInvite = false; result.Reason = "眼下暂时无法动身，稍后再谈吧。"; return result; }
            // Keep the source squad and its remaining members; retire only orphaned motion.
            if (!world.Strategic.Squads.TryGet(original, out var remaining)) world.Strategic.SquadWorldMotions.Remove(original);
            else if (remaining.CommandTargetCharacterId == target)
                SquadCommandService.SetExecution(world, original, remaining.CommandKind, remaining.LeaderCharacterId);
            world.QuestCompanions.Add(new QuestCompanionBinding { CompanionEntityId = target,
                QuestInstanceId = questInstanceId, OriginalSquadId = original, State = QuestCompanionState.Active });
            BackgroundCharacterTravelService.CancelTravelIfAny(world, target);
            if (PlayerPartyLocalCoPresenceQuery.IsContinuousOutdoorPresentationScope(world))
            {
                PlayerPartyTransitionMembership.SyncMemberPresenceFromMotion(world,target);
                PlayerPartyTransitionMembership.CaptureTravelingMembersForPartyTransition(world,party);
            }
            else world.LocalMap.AddOccupant(target);
            return result;
        }
        public static void QuestStatusChanged(SimulationWorld world, QuestRuntime quest)
        {
            if (quest.Status == QuestStatus.Active) return;
            foreach (var b in world.QuestCompanions.Bindings.Values)
                if (b.QuestInstanceId == quest.QuestInstanceId) b.State = QuestCompanionState.PendingDeparture;
        }
        public static void Reconcile(SimulationWorld world)
        {
            if (world == null) return;
            foreach (var b in world.QuestCompanions.Capture())
            {
                if (!world.Entities.TryGet(b.CompanionEntityId, out var entity) ||
                    (entity.TryGet<LifecycleComponent>(out var life) && (life.IsDead || life.IsRemoved)) ||
                    (world.Strategic.PlayerPartyContext != null && !world.Strategic.PlayerPartyContext.IsMember(b.CompanionEntityId)))
                { world.QuestCompanions.Remove(b.CompanionEntityId); continue; }
                if (!world.Quests.TryGet(b.QuestInstanceId, out var q) || q.Status != QuestStatus.Active)
                    if (world.QuestCompanions.TryGet(b.CompanionEntityId, out var current)) current.State = QuestCompanionState.PendingDeparture;
            }
        }
        public static EntityId DepartureSuccessor(SimulationWorld world, EntityId departing)
        {
            var party = world?.Strategic?.PlayerPartyContext;
            if (party != null) foreach (var id in party.Members)
                if (id != departing && party.IsPlayerControllableMember(id) && PlayerPartyRuntime.CanActAsActive(world,id,out _)) return id;
            return EntityId.None;
        }
        public static bool CanDepart(SimulationWorld world, EntityId id)
        {
            var party = world?.Strategic?.PlayerPartyContext;
            return party != null && party.IsMember(id) && world.QuestCompanions.TryGet(id,out var b) &&
                b.State == QuestCompanionState.PendingDeparture && PlayerPartyRuntime.CanActAsActive(world,id,out _) && !world.LocalMap.IsActive &&
                PlayerPartyLocalCoPresenceQuery.IsContinuousOutdoorPresentationScope(world) &&
                !BattleBlocked(world) && !IsMemberBattleLocked(world,id) && !world.ContentEvents.HasActive &&
                !DepartureSuccessor(world,id).IsNone;
        }
        // Called only after the ordinary stop-follow path preserved the actual departure position.
        public static void FinishDeparture(SimulationWorld world, EntityId id)
        {
            if (!world.QuestCompanions.TryGet(id,out var binding) || world.Strategic.PlayerPartyContext.IsMember(id)) return;
            var party = world.Strategic.PlayerPartyContext;
            if (world.Strategic.Squads.TryGet(party.ControlledSquadId,out var controlled) && controlled.CommandTargetCharacterId == id)
                SquadCommandService.SetExecution(world,controlled.SquadId,controlled.CommandKind,party.ActiveCharacterId);
            if (world.Strategic.Squads.TryGet(binding.OriginalSquadId,out var original) &&
                original.SquadId != world.Strategic.PlayerPartyContext.ControlledSquadId &&
                original.MemberCharacterIds.Count < PlayerPartyRuntime.MaxMembers &&
                PlayerPartyRuntime.CanActAsActive(world,original.LeaderCharacterId,out _) && !SquadMembershipService.IsBattleLocked(world,original) &&
                world.WorldPresence.TryGet(id,out var here) && here.HasContinuousWorldPosition &&
                OriginalSquadAtPosition(world,original,here.PersonalSurfaceId,here.ContinuousWorldPosition))
                SquadMembershipService.Transfer(world,id,original.SquadId);
            world.QuestCompanions.Remove(id);
        }
        static bool OriginalSquadAtPosition(SimulationWorld world, SquadState squad, string surface, WorldVec2 position)
        {
            if (SquadWorldMotionService.TryGetActiveNpcSquadAuthority(world,squad.SquadId,out _,out var motion))
                return motion.SurfaceId == surface && Near(motion.WorldPosition,position);
            foreach (var value in squad.MemberCharacterIds)
                if (!world.WorldPresence.TryGet(new EntityId(value),out var p) || !p.HasContinuousWorldPosition ||
                    p.PersonalSurfaceId != surface || !Near(p.ContinuousWorldPosition,position)) return false;
            return true;
        }
        static bool Near(WorldVec2 a, WorldVec2 b) => Math.Abs(a.X-b.X) < .01f && Math.Abs(a.Y-b.Y) < .01f;
        public static Result ValidateRestored(SimulationWorld world,string controlledSquadId,bool definitions)
        {
            foreach (var b in world.QuestCompanions.Bindings.Values)
            {
                if (b.CompanionEntityId.IsNone || !world.Entities.TryGet(b.CompanionEntityId,out var entity) ||
                    (entity.Tags & (EntityTag.Character | EntityTag.Npc)) == 0 ||
                    (entity.TryGet<LifecycleComponent>(out var life) && (life.IsDead || life.IsRemoved)) ||
                    string.IsNullOrWhiteSpace(b.QuestInstanceId) || !world.Quests.TryGet(b.QuestInstanceId,out var q) || string.IsNullOrWhiteSpace(b.OriginalSquadId) ||
                    b.OriginalSquadId == controlledSquadId || !Enum.IsDefined(typeof(QuestCompanionState),b.State) ||
                    !world.Strategic.Squads.TryGet(controlledSquadId,out var squad) || !squad.Contains(b.CompanionEntityId) ||
                    (b.State == QuestCompanionState.Active && q.Status != QuestStatus.Active) ||
                    (definitions && (!world.Quests.TryGetSpec(q.QuestId,out var spec) || !spec.IsSecretRealm)))
                    return Result.Failure(ErrorCode.SnapshotInvalid,"Invalid temporary Quest companion identity/membership.",b.CompanionEntityId.ToString());
            }
            return Result.Success();
        }
    }
}
