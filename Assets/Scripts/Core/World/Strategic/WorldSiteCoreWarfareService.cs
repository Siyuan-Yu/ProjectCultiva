using System;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Npc;
using XianXia.Core.Results;
using XianXia.Core.Settlement;
using XianXia.Core.Simulation;
using XianXia.Core.Social;

namespace XianXia.Core.World.Strategic
{
    public enum WorldSiteCoreKind { FixedControlCore, RemovableFactionFlag }
    public enum SiteCoreObjectiveKind { None, FixedSiteCoreCapture, RemovableFactionFlagDestruction }

    public sealed class SiteCoreEncounterObjective
    {
        public SiteCoreObjectiveKind Kind;
        public string SiteId = "", AssetId = "", AttackerFactionId = "", DefenderFactionId = "";
        public bool Resolved;
    }

    public readonly struct WorldSiteCoreTarget
    {
        public readonly string SiteId, CoreAssetId, OwnerFactionId, SurfaceId;
        public readonly float WorldX, WorldY;
        public readonly bool CoreIsRemovable;
        public WorldSiteCoreKind CoreKind => CoreIsRemovable ? WorldSiteCoreKind.RemovableFactionFlag : WorldSiteCoreKind.FixedControlCore;
        public WorldSiteCoreTarget(WorldSite site)
        {
            SiteId = site.SiteId; CoreAssetId = site.CoreAssetId; OwnerFactionId = site.OwnerFactionId;
            SurfaceId = site.CoreSurfaceId; WorldX = site.CoreWorldX; WorldY = site.CoreWorldY;
            CoreIsRemovable = site.CoreIsRemovable;
        }
    }

    public static class WorldSiteDefenseCharacterQuery
    {
        public static bool IsDefenderSide(SimulationWorld world, string faction, string attacker, string owner)
        {
            foreach (var war in world.Strategic.Wars.EnumerateActive())
            {
                if (war.IsAttacker(attacker) && war.IsDefender(owner) && war.IsDefender(faction)) return true;
                if (war.IsDefender(attacker) && war.IsAttacker(owner) && war.IsAttacker(faction)) return true;
            }
            return faction == owner;
        }

        public static EntityId FindNearest(SimulationWorld world, WorldSiteCoreTarget target, string attackerFaction, bool unjoinedOnly = false)
        {
            var best = EntityId.None; var distance = double.PositiveInfinity;
            var encounter = world.Strategic.CharacterEncounter;
            world.Strategic.Sites.TryGet(target.SiteId, out var site);
            foreach (var squad in world.Strategic.Squads.Squads.Values)
                foreach (var raw in squad.MemberCharacterIds)
                {
                    var id = new EntityId(raw);
                    if (!world.Entities.TryGet(id, out var entity) || !CombatLifeStateService.CanFight(entity) ||
                        !entity.TryGet<FactionMembershipComponent>(out var faction) || !faction.IsAffiliated ||
                        !IsDefenderSide(world, faction.FactionId, attackerFaction, target.OwnerFactionId)) continue;
                    var participant = encounter?.Find(raw);
                    if (unjoinedOnly && encounter != null && encounter.Participants.Exists(p => p.SquadId == squad.SquadId)) continue;
                    float x, y;
                    if (participant != null)
                    {
                        if (encounter.SourceSurfaceId != target.SurfaceId || !participant.Enemy) continue;
                        x = participant.TacticalX; y = participant.TacticalY;
                    }
                    else
                    {
                        if (!CharacterPersonalSpaceQuery.TryResolveContinuous(world, id, target.SurfaceId, out var point, out _)) continue;
                        x = point.X; y = point.Y;
                    }
                    if (!WorldSiteCoreCoverageResolver.Contains(site, target.SurfaceId, x, y) ||
                        (encounter != null && !encounter.Contains(x, y))) continue;
                    var dx = (double)x - target.WorldX; var dy = (double)y - target.WorldY;
                    var d = dx * dx + dy * dy;
                    if (d < distance || (d == distance && (best.IsNone || raw < best.Value))) { best = id; distance = d; }
                }
            return best;
        }

        public static EntityId ResolveAssaultDefender(SimulationWorld world, WorldSiteCoreTarget target)
        {
            var state = world.Strategic.CharacterEncounter;
            if (state != null)
                foreach (var p in state.Participants)
                    if (p.Enemy && CharacterEncounterService.IsLiving(world, p.CharacterId) &&
                        world.Entities.TryGet(new EntityId(p.CharacterId), out var entity) &&
                        entity.TryGet<FactionMembershipComponent>(out var faction) && faction.FactionId == target.OwnerFactionId)
                        return EntityId.None;
            return FindNearest(world, target, world.Strategic.PlayerFactionId, unjoinedOnly: state != null);
        }
    }

    /// <summary>Player Site warfare authority. Physical core state and political ownership stay in their existing boards.</summary>
    public static class WorldSiteCoreWarfareService
    {
        public static Result Resolve(SimulationWorld world, string siteId, out WorldSiteCoreTarget target)
        {
            target = default;
            if (world?.Strategic == null || !world.Strategic.Sites.TryGet(siteId ?? "", out var site) ||
                !site.IsCoreActive || !site.HasCoreWorldPosition || string.IsNullOrEmpty(site.CoreAssetId)) return Fail("据点核心不存在或已失效。");
            if (site.CoreIsRemovable)
            {
                if (!world.Strategic.FactionFlags.Flags.TryGetValue(site.CoreAssetId, out var flag) || flag.SiteId != site.SiteId)
                    return Fail("势力旗与核心关联无效。");
            }
            else if (!TryGetFixedCore(world, siteId, out _)) return Fail("固定核心物理状态不存在。");
            target = new WorldSiteCoreTarget(site);
            return Result.Success();
        }

        public static bool TryGetFixedCore(SimulationWorld world, string siteId, out ControlCoreState core)
        {
            core = null;
            return world?.ControlCores != null && world.ControlCores.TryGetByWorldSite(siteId, out core) && core != null;
        }

        public static Result ValidateFixedCoreBinding(SimulationWorld world, string workAreaId, string siteId)
        {
            if (world?.Strategic?.Sites == null || string.IsNullOrWhiteSpace(workAreaId) || string.IsNullOrWhiteSpace(siteId))
                return Result.Failure(ErrorCode.InvalidArgument, "ControlCore binding requires WorkAreaId and SiteId.");
            if (!world.ControlCores.TryGet(workAreaId, out var core) || core == null)
                return Result.Failure(ErrorCode.NotFound, "ControlCore binding target is missing.", workAreaId);
            if (!world.Strategic.Sites.TryGet(siteId, out var site) || site == null)
                return Result.Failure(ErrorCode.NotFound, "ControlCore binding WorldSite is missing.", siteId);
            if (site.CoreIsRemovable)
                return Result.Failure(ErrorCode.InvalidOperation, "Removable WorldSite cannot bind a fixed ControlCore.", siteId);
            if (!string.IsNullOrEmpty(core.BoundWorldSiteId) && core.BoundWorldSiteId != siteId)
                return Result.Failure(ErrorCode.InvalidOperation, "ControlCore is already bound to another WorldSite.", workAreaId);
            if (world.ControlCores.TryGetByWorldSite(siteId, out var existing) && existing.WorkAreaId != workAreaId)
                return Result.Failure(ErrorCode.InvalidOperation, "Fixed WorldSite already has another ControlCore.", siteId);
            return Result.Success();
        }

        public static Result BindFixedCore(SimulationWorld world, string workAreaId, string siteId)
        {
            var valid = ValidateFixedCoreBinding(world, workAreaId, siteId);
            if (valid.IsFailure) return valid;
            var bound = world.ControlCores.BindWorldSite(workAreaId, siteId);
            if (bound.IsSuccess) SettlementAuthoritySync.Rebuild(world);
            return bound;
        }

        public static bool TryGetBoundSiteForFixedCore(SimulationWorld world, string workAreaId, out WorldSite site)
        {
            site = null;
            return world?.Strategic?.Sites != null &&
                   world.ControlCores.TryGetBoundSiteId(workAreaId, out var siteId) &&
                   world.Strategic.Sites.TryGet(siteId, out site) && site != null && !site.CoreIsRemovable;
        }

        public static Result ValidateFixedCoreAssault(SimulationWorld world, string attackerFactionId, string workAreaId)
        {
            if (world == null || string.IsNullOrEmpty(workAreaId))
                return Result.Failure(ErrorCode.InvalidArgument, "Invalid assault request.");
            if (!world.ControlCores.TryGet(workAreaId, out _))
                return Result.Failure(ErrorCode.NotFound, "Control core not found.", workAreaId);
            if (!TryGetBoundSiteForFixedCore(world, workAreaId, out var site))
                return Result.Failure(ErrorCode.NotFound, "Control core canonical WorldSite binding missing.", workAreaId);
            var owner = site.OwnerFactionId ?? string.Empty;
            if (!string.IsNullOrEmpty(owner) && string.Equals(attackerFactionId, owner, StringComparison.Ordinal))
                return Result.Failure(ErrorCode.InvalidOperation, "Already controlled by attacker faction.");
            if (!string.IsNullOrEmpty(owner) && !WarGateService.CanMilitaryCapture(world, attackerFactionId, owner))
                return Result.Failure(ErrorCode.InvalidOperation, "Military capture requires active war.", attackerFactionId + "->" + owner);
            return Result.Success();
        }

        public static Result TryCompleteFixedSiteCapture(SimulationWorld world, string attackerFactionId, string workAreaId)
        {
            var assault = ValidateFixedCoreAssault(world, attackerFactionId, workAreaId);
            if (assault.IsFailure) return assault;
            if (!TryGetBoundSiteForFixedCore(world, workAreaId, out var site))
                return Result.Failure(ErrorCode.NotFound, "Control core canonical WorldSite binding missing.", workAreaId);
            if (site.CoreIsRemovable)
                return Result.Failure(ErrorCode.InvalidOperation, "可拆核心只能摧毁，不能占领。");
            if (!world.ControlCores.TryCapture(workAreaId, out _))
                return Result.Failure(ErrorCode.InvalidOperation, "Occupy hold not finished.");

            var oldOwner = site.OwnerFactionId ?? string.Empty;
            var transfer = WorldSiteTerritoryTransferService.Transfer(world, site.SiteId, attackerFactionId);
            if (transfer.IsFailure) return transfer;
            world.ControlCores.ResetAfterCapture(workAreaId, out _);
            SettlementAuthoritySync.Rebuild(world);
            ScenarioProgressionHooks.NotifyWorldSiteCaptured(world, site.SiteId, oldOwner, attackerFactionId, workAreaId);
            CharacterEncounterService.NotifyStrategicObjectiveResolved(world, site.SiteId, site.CoreAssetId);
            return Result.Success();
        }

        public static Result Validate(SimulationWorld world, EntityId attacker, WorldSiteCoreTarget target, bool requireWar = true)
        {
            if (world?.Strategic?.PlayerPartyContext == null || !world.Strategic.PlayerPartyContext.IsMember(attacker) ||
                !world.Entities.TryGet(attacker, out var entity) || !CombatLifeStateService.CanFight(entity) ||
                !entity.TryGet<FactionMembershipComponent>(out var member) || member.FactionId != world.Strategic.PlayerFactionId)
                return Fail("攻击者必须是可战的玩家小队人物。");
            if (Resolve(world, target.SiteId, out var current).IsFailure || current.CoreAssetId != target.CoreAssetId)
                return Fail("据点核心已发生变化。");
            if (current.OwnerFactionId == member.FactionId) return Fail("不能攻击己方据点核心。");
            var state = world.Strategic.CharacterEncounter;
            if (state != null)
            {
                if (state.Phase != CharacterEncounterPhase.Active && state.Phase != CharacterEncounterPhase.ReadyToEnd)
                    return Fail("当前遭遇尚不能攻击建筑。");
                if (state.SourceSurfaceId != target.SurfaceId || !state.Contains(target.WorldX, target.WorldY))
                    return Fail("该建筑不在当前战场范围内。");
                if (state.Objective != null && !state.Objective.Resolved && state.Objective.SiteId != target.SiteId)
                    return Fail("当前战场已有攻城目标。");
                if (state.Find(attacker.Value) == null) return Fail("攻击者不在当前战场。");
            }
            else if (!CharacterPersonalSpaceQuery.TryResolveContinuous(world, attacker, target.SurfaceId, out _, out _))
                return Fail("攻击者不在目标连续世界表面。");
            if (requireWar && !WarGateService.CanAttack(world, member.FactionId, current.OwnerFactionId))
                return Fail("攻击据点核心需要有效战争状态。");
            return Result.Success();
        }

        public static SiteCoreEncounterObjective DescribeObjective(SimulationWorld world, WorldSiteCoreTarget target) =>
            new SiteCoreEncounterObjective { Kind = target.CoreIsRemovable ? SiteCoreObjectiveKind.RemovableFactionFlagDestruction : SiteCoreObjectiveKind.FixedSiteCoreCapture,
                SiteId = target.SiteId, AssetId = target.CoreAssetId, AttackerFactionId = world.Strategic.PlayerFactionId,
                DefenderFactionId = target.OwnerFactionId };

        public static Result BindObjective(SimulationWorld world, EntityId attacker, WorldSiteCoreTarget target)
        {
            var valid = Validate(world, attacker, target);
            if (valid.IsFailure) return valid;
            var state = world.Strategic.CharacterEncounter;
            if (state != null && (state.Objective == null || state.Objective.SiteId != target.SiteId))
                state.Objective = DescribeObjective(world, target);
            return Result.Success();
        }

        public static bool TickOccupation(SimulationWorld world, string workAreaId, float seconds, out bool contested,
            Func<float, float, bool> physicalOccupyArea = null)
        {
            contested = false;
            if (!world.ControlCores.TryGet(workAreaId, out var core) || !core.CaptureAvailable ||
                !TryGetBoundSiteForFixedCore(world, workAreaId, out var site) ||
                Resolve(world, site.SiteId, out var target).IsFailure || target.CoreIsRemovable) return false;
            var state = world.Strategic.CharacterEncounter;
            if (state != null && (state.SourceSurfaceId != target.SurfaceId || state.Objective == null ||
                state.Objective.SiteId != target.SiteId || state.Objective.Resolved ||
                (state.Phase != CharacterEncounterPhase.Active && state.Phase != CharacterEncounterPhase.ReadyToEnd)))
            { world.ControlCores.ResetOccupyProgress(workAreaId); return false; }
            bool InArea(float x, float y) => physicalOccupyArea != null ? physicalOccupyArea(x, y) : Near(target, x, y);
            var standing = false;
            if (state != null)
            {
                foreach (var p in state.Participants)
                    if (CharacterEncounterService.IsLiving(world, p.CharacterId) && InArea(p.TacticalX, p.TacticalY))
                    { if (p.Enemy) contested = true; else standing = true; }
            }
            else if (world.Strategic.PlayerPartyContext != null)
                foreach (var id in world.Strategic.PlayerPartyContext.Members)
                    if (CharacterEncounterService.IsLiving(world, id.Value) &&
                        CharacterPersonalSpaceQuery.TryResolveContinuous(world, id, target.SurfaceId, out var point, out _) && InArea(point.X, point.Y)) standing = true;
            ControlCoreService.TickOccupy(world, workAreaId, seconds, standing && !contested);
            return !core.CaptureAvailable;
        }

        static bool Near(WorldSiteCoreTarget target, float x, float y)
        { var dx = x - target.WorldX; var dy = y - target.WorldY; return dx * dx + dy * dy <= ControlCoreService.DefaultStandRadius * ControlCoreService.DefaultStandRadius; }
        static Result Fail(string message) => Result.Failure(ErrorCode.InvalidOperation, message);
    }
}
