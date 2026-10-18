using System;
using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Actions;
using XianXia.Core.Attributes;
using XianXia.Core.Combat;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Navigation;
using XianXia.Core.Npc;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// 砍树／拆墙：靠近后按攻击力拆血量；树归零后粗木进队伍背包。
    /// 支持多名攻击者（PlayerParty Follower 可各自找附近树）。
    /// </summary>
    public sealed class HostDestructibleAssault : MonoBehaviour
    {
        sealed class Session
        {
            public EntityId Attacker;
            public HostMapDestructible Target;
            public float Cooldown;
            public float NextRepath;
            public bool FromPartyFollow;
            public bool FromCivilianWork;
            public string WorkAreaId;
            public string WorkLocationId;
            public string TargetAdministrativeSiteId;
            public string StockSiteId;
            public Vector3 ApproachPoint;
            public uint OwnedMovementRevision;
            public string LatestOutput;
            public int CandidateCount;
            public int LegalCount;
            public int PreferredCount;
            public int UnclaimedCount;
            public int OtherFactionCount;
        }

        readonly struct CivilianTreeCandidate
        {
            public CivilianTreeCandidate(
                HostMapDestructible tree, WorldSite administrativeSite, bool preferredWorkLocation)
            {
                Tree = tree;
                AdministrativeSite = administrativeSite;
                PreferredWorkLocation = preferredWorkLocation;
            }

            public HostMapDestructible Tree { get; }
            public WorldSite AdministrativeSite { get; }
            public bool PreferredWorkLocation { get; }
        }

        enum LoggingControlKind
        {
            OwnFaction,
            OtherFaction,
            Unclaimed
        }

        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] EntityViewSpawner viewSpawner;
        [SerializeField] HostMoveController moveController;
        [SerializeField] HostMeleeStrikeVfx strikeVfx;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] float engageRange = 1.85f;
        [SerializeField] float repathInterval = 0.25f;
        [SerializeField] float civilianRepathInterval = 1.25f;
        [SerializeField] float unreachableRetrySeconds = 8f;

        readonly List<Session> _sessions = new List<Session>(4);
        readonly List<Session> _updateSnapshot = new List<Session>(4);
        readonly List<CivilianTreeCandidate> _candidateTrees = new List<CivilianTreeCandidate>(64);
        readonly Dictionary<string, float> _retryAfterByLoggerAndTree = new Dictionary<string, float>();
        readonly Dictionary<ulong, string> _civilianStatus = new Dictionary<ulong, string>();
        readonly Dictionary<ulong, string> _lastDiagnostic = new Dictionary<ulong, string>();
        readonly Dictionary<ulong, string> _lastCivilianOutput = new Dictionary<ulong, string>();

        public bool IsAssaulting => _sessions.Count > 0;
        public HostMapDestructible Target => _sessions.Count > 0 ? _sessions[0].Target : null;

        public bool TryGetWorkVisual(EntityId id, out HostCharacterWorkVisual visual)
        {
            visual = default;
            if (viewSpawner == null || !viewSpawner.Registry.TryGet(id, out var view) || view == null)
                return false;
            for (var i = 0; i < _sessions.Count; i++)
            {
                var session = _sessions[i];
                var target = session.Target;
                if (session.Attacker != id || target == null || target.IsDestroyed || !target.IsTree) continue;
                var distance = session.FromCivilianWork ? DistanceToTargetFootprint(target, view.transform.position) :
                    Vector2.Distance(view.transform.position, target.transform.position);
                if (distance > engageRange) continue;
                visual = HostCharacterWorkVisual.For(HumanWorkMotion.Chop, HumanCommonRigView.Equipment.Axe);
                visual.HasTarget = true;
                visual.Target = target.transform.position;
                return true;
            }
            return false;
        }

        public void Bind(PlayableHostBootstrap host)
        {
            bootstrap = host;
            if (host == null)
                return;
            viewSpawner = host.ViewSpawner;
            moveController = host.GetComponent<HostMoveController>();
            strikeVfx = host.GetComponent<HostMeleeStrikeVfx>() ??
                        host.gameObject.AddComponent<HostMeleeStrikeVfx>();
            selectionController = host.GetComponent<HostSelectionController>();
        }

        public bool IsAttacker(EntityId id)
        {
            if (id.IsNone)
                return false;
            for (var i = 0; i < _sessions.Count; i++)
            {
                if (_sessions[i].Attacker == id)
                    return true;
            }

            return false;
        }

        public bool IsPartyDerivedAttacker(EntityId id)
        {
            if (id.IsNone)
                return false;
            for (var i = 0; i < _sessions.Count; i++)
            {
                if (_sessions[i].Attacker == id && _sessions[i].FromPartyFollow)
                    return true;
            }

            return false;
        }

        public HostMapDestructible GetTargetForAttacker(EntityId id)
        {
            for (var i = 0; i < _sessions.Count; i++)
            {
                if (_sessions[i].Attacker == id)
                    return _sessions[i].Target;
            }

            return null;
        }

        /// <summary>True only while a valid civilian logging session owns tree approach/chopping.</summary>
        public bool IsCivilianLoggingOwner(EntityId id)
        {
            var session = FindSession(id);
            return session != null && session.FromCivilianWork &&
                   IsCivilianSessionStillValid(session, out _, out _, out _);
        }

        public bool TryGetCivilianLoggingStatus(EntityId id, out string status)
        {
            status = string.Empty;
            var world = bootstrap?.Session?.World;
            return !id.IsNone && world != null &&
                   world.Civilians.TryGet(id, out var civilian) &&
                   civilian.Activity == MortalActivity.Logging &&
                   _civilianStatus.TryGetValue(id.Value, out status) &&
                   !string.IsNullOrEmpty(status);
        }

        public void Begin(EntityId attacker, HostMapDestructible target, bool fromPartyFollow = false, bool fromCivilianWork = false)
        {
            if (attacker.IsNone || target == null || target.IsDestroyed)
                return;
            if (MortalTransportConstraintService.ValidateUnburdenedAction(
                    bootstrap?.Session?.World, attacker).IsFailure)
                return;
            if (!AutonomousActionContinuationService.CanContinue(bootstrap?.Session?.World, attacker))
                return;

            for (var i = 0; i < _sessions.Count; i++)
            {
                if (_sessions[i].Attacker != attacker)
                    continue;
                _sessions[i].Target = target;
                _sessions[i].Cooldown = 0f;
                _sessions[i].NextRepath = 0f;
                _sessions[i].FromPartyFollow = fromPartyFollow || _sessions[i].FromPartyFollow;
                _sessions[i].FromCivilianWork = fromCivilianWork || _sessions[i].FromCivilianWork;
                if (!fromPartyFollow && !fromCivilianWork)
                    bootstrap?.GetComponent<HostHousingAreaSelection>()?.SelectDestructible(target);
                return;
            }

            _sessions.Add(new Session
            {
                Attacker = attacker,
                Target = target,
                Cooldown = 0f,
                NextRepath = 0f,
                FromPartyFollow = fromPartyFollow,
                FromCivilianWork = fromCivilianWork
            });

            if (!fromPartyFollow && !fromCivilianWork)
                bootstrap?.GetComponent<HostHousingAreaSelection>()?.SelectDestructible(target);
        }

        public void Clear() => ClearAllSessions(clearInspect: true);

        public void DisengageIfAttacker(EntityId id)
        {
            if (id.IsNone)
                return;
            RemoveSession(id, clearInspect: true);
        }

        public void StopPartyDerived(EntityId id)
        {
            if (id.IsNone)
                return;
            for (var i = _sessions.Count - 1; i >= 0; i--)
            {
                if (_sessions[i].Attacker != id || !_sessions[i].FromPartyFollow)
                    continue;
                _sessions.RemoveAt(i);
            }
        }

        void Update()
        {
            if (bootstrap?.Session?.World == null)
                return;
            if (bootstrap.Session.IsPaused)
                return;

            SyncCivilianLoggers();
            if (_sessions.Count == 0)
                return;

            // TickSession may destroy a target and remove every session sharing it.
            // Iterate a stable snapshot so those removals cannot invalidate this loop's index.
            _updateSnapshot.Clear();
            _updateSnapshot.AddRange(_sessions);
            for (var i = _updateSnapshot.Count - 1; i >= 0; i--)
            {
                var session = _updateSnapshot[i];
                if (!_sessions.Contains(session))
                    continue;
                if (!TickSession(session))
                    _sessions.Remove(session);
            }
            _updateSnapshot.Clear();
        }

        bool TickSession(Session session)
        {
            if (session == null)
                return false;

            var target = session.Target;
            if (target == null || target.IsDestroyed)
            {
                if (session.FromCivilianWork)
                    ReleaseOwnedMovement(session);
                return false;
            }

            var world = bootstrap.Session.World;
            var attacker = session.Attacker;
            if (session.FromCivilianWork &&
                !IsCivilianSessionStillValid(session, out _, out _, out var invalidReason))
            {
                ReleaseOwnedMovement(session);
                SetCivilianStatus(attacker, "伐木已停止：" + invalidReason, session);
                return false;
            }
            if (!AutonomousActionContinuationService.CanContinue(world, attacker) ||
                !world.Entities.TryGet(attacker, out var atk) ||
                MortalTransportConstraintService.ValidateUnburdenedAction(world, attacker).IsFailure)
            {
                if (session.FromCivilianWork) ReleaseOwnedMovement(session);
                else moveController?.CancelPresentationMovementPublic(attacker);
                return false;
            }

            if (!TryGetPresentation(attacker, out var ax, out var ay))
                return false;

            var attackerPoint = new Vector3(ax, ay, HostPresentationSpace.EntityZ);
            var targetPoint = HostPresentationSpace.ToPresentation(target.transform.position);
            var dist = session.FromCivilianWork
                ? DistanceToTargetFootprint(target, attackerPoint)
                : Vector2.Distance(new Vector2(ax, ay), new Vector2(targetPoint.x, targetPoint.y));
            // Preserve the original cooldown clock: it advances while in work range,
            // including a short reposition, and pauses while travelling from farther away.
            if (dist <= engageRange)
                session.Cooldown -= bootstrap.PresentationDeltaTime;
            if (dist > engageRange || moveController != null && !moveController.IsCharacterStandClear(attacker))
            {
                if (session.FromCivilianWork)
                {
                    SetCivilianStatus(attacker, "前往树木", session);
                    return ChaseCivilian(session);
                }
                ChaseExisting(session);
                return true;
            }

            if (session.FromCivilianWork)
                ReleaseOwnedMovement(session);
            else
                moveController?.CancelPresentationMovementPublic(attacker);
            if (session.FromCivilianWork)
                SetCivilianStatus(attacker, "砍伐中", session);

            if (session.Cooldown > 0f)
                return true;
            session.Cooldown = MeleeCombatService.DefaultMeleeIntervalSeconds;

            var damage = 1;
            if (atk.TryGet<AttributesComponent>(out var attrs))
                damage = Mathf.Max(1, attrs.GetFinal(AttributeId.Attack));

            var isTree = target.IsTree;
            var kind = target.Kind;
            var expectedYield = target.ResolveWoodYield();
            var hitPos = target.transform.position;
            var dealt = target.ApplyDamage(damage, out var destroyed);
            if (dealt > 0)
            {
                if (viewSpawner != null && viewSpawner.Registry.TryGet(attacker, out var artView) && artView != null)
                {
                    if (isTree)
                        artView.PlayArtWorkContact(hitPos, HumanWorkMotion.Chop, HumanCommonRigView.Equipment.Axe);
                    else if (!session.FromCivilianWork)
                        artView.PlayArtAttack(hitPos);
                }
                strikeVfx?.Play(hitPos + Vector3.up * 0.4f, hitPos);
                Toast(attacker, "-" + dealt, new Color(0.75f, 0.9f, 0.45f));
            }

            if (!destroyed)
                return true;

            bootstrap.ContinuousOutdoorSurfaceRuntime?
                .NotifyOutdoorDestructibleStateChanged(target.PlacementId);

            var wood = 0;
            var civilianLogging = false;
            if (isTree && expectedYield > 0)
            {
                if (world.Civilians.TryGet(attacker, out var civilian) && civilian.Activity == MortalActivity.Logging &&
                    session.FromCivilianWork && !string.IsNullOrEmpty(session.StockSiteId))
                {
                    civilianLogging = true;
                    var deposited = WorldSitePublicStockService.TryAdd(world, session.StockSiteId,
                        MortalCivilianService.WoodResourceId, expectedYield, attacker);
                    wood = deposited.IsSuccess ? expectedYield : 0;
                    if (deposited.IsSuccess) civilian.LastWorkOutputTick = world.Tick.Value;
                }
                else wood = GrantRoughWood(world, expectedYield);
            }

            string msg;
            if (isTree)
            {
                if (civilianLogging)
                    msg = wood > 0 ? "伐倒 · 公库粗木 ×" + wood : "伐倒 · 公库无法接收粗木";
                else
                if (wood <= 0 && expectedYield > 0)
                    msg = "伐倒 · 背包已满，粗木未入包";
                else if (wood < expectedYield && expectedYield > 0)
                    msg = "伐倒 · 获粗木 ×" + wood + "（背包将满）";
                else if (wood > 0)
                    msg = "伐倒 · 获粗木 ×" + wood;
                else
                    msg = "伐倒 · 无木材产量（kind=" + kind + "）";
                Debug.Log("[Host] 砍树结算 kind=" + kind + " yield=" + expectedYield +
                          " added=" + wood + " destination=" + (civilianLogging ? "SitePublicStock" : "PartyInventory"));
                if (session.FromCivilianWork)
                {
                    session.LatestOutput = msg;
                    _lastCivilianOutput[attacker.Value] = msg;
                }
            }
            else
                msg = "摧毁";

            Toast(attacker, msg, new Color(0.55f, 1f, 0.55f));
            if (!session.FromPartyFollow && !session.FromCivilianWork)
                bootstrap.GetComponent<HostHousingAreaSelection>()?.Clear();
            bootstrap.DispatchDrainedEvents();
            // 当前目标的全部攻击会话在此批量移除；Update 遍历稳定快照并跳过已移除项。
            RemoveSessionsOnTarget(target, clearInspect: !session.FromPartyFollow && !session.FromCivilianWork);
            return true;
        }

        void SyncCivilianLoggers()
        {
            var world = bootstrap.Session.World;
            // End invalid sessions before scanning.  This lets a new food/sleep/combat owner take
            // over in the same frame and prevents a stale logger session from swallowing it.
            for (var i = _sessions.Count - 1; i >= 0; i--)
            {
                var session = _sessions[i];
                if (!session.FromCivilianWork)
                    continue;
                if (IsCivilianSessionStillValid(session, out _, out _, out var invalidReason))
                    continue;
                ReleaseOwnedMovement(session);
                SetCivilianStatus(session.Attacker, "伐木已停止：" + invalidReason, session);
                _sessions.RemoveAt(i);
            }

            foreach (var entity in world.Entities.All)
            {
                if (!world.Civilians.TryGet(entity.Id, out var civilian) ||
                    civilian.Profession != MortalProfession.Logger ||
                    civilian.Activity != MortalActivity.Logging ||
                    !AutonomousActionContinuationService.CanContinue(world, entity.Id))
                    continue;
                if (!MortalCivilianMovementAuthority.CanCivilianOwnLocalMovement(world, entity.Id, civilian))
                {
                    SetCivilianStatus(entity.Id, "伐木受阻：移动权不属于民生日程", null);
                    continue;
                }
                if (IsAttacker(entity.Id))
                    continue;
                if (!MortalCivilianQuery.TryFindProfessionArea(world, entity, "wood", out var area))
                {
                    SetCivilianStatus(entity.Id, "伐木受阻：缺少合法伐木工区", null);
                    continue;
                }
                if (string.IsNullOrEmpty(area.LocationId))
                {
                    SetCivilianStatus(entity.Id, "伐木受阻：工区缺少地点绑定", null);
                    continue;
                }
                if (!viewSpawner.Registry.TryGet(entity.Id, out var view) || view == null)
                {
                    SetCivilianStatus(entity.Id, "前往伐木区：人物尚未加载", null);
                    continue;
                }

                var candidates = 0;
                var legal = 0;
                var preferred = 0;
                var unclaimed = 0;
                var otherFaction = 0;
                _candidateTrees.Clear();
                var trees = HostMapObjectRegistry.AllDestructibles;
                for (var i = 0; i < trees.Count; i++)
                {
                    var tree = trees[i];
                    if (tree == null || tree.IsDestroyed || !tree.IsTree)
                        continue;
                    candidates++;
                    var plot = tree.GetComponent<HostMapPlotCell>();
                    if (!TryResolveCivilianLoggingControl(
                            world, entity, tree, out var administrativeSite, out var controlKind))
                    {
                        if (controlKind == LoggingControlKind.OtherFaction)
                            otherFaction++;
                        else
                            unclaimed++;
                        continue;
                    }
                    legal++;
                    var preferredWorkLocation = plot != null &&
                        !string.IsNullOrEmpty(plot.LocationId) &&
                        string.Equals(plot.LocationId, area.LocationId, StringComparison.Ordinal);
                    if (preferredWorkLocation)
                        preferred++;
                    if (Time.unscaledTime < RetryAfter(entity.Id, tree))
                        continue;
                    _candidateTrees.Add(new CivilianTreeCandidate(
                        tree, administrativeSite, preferredWorkLocation));
                }

                _candidateTrees.Sort((left, right) =>
                {
                    if (left.PreferredWorkLocation != right.PreferredWorkLocation)
                        return left.PreferredWorkLocation ? -1 : 1;
                    return (left.Tree.transform.position - view.transform.position).sqrMagnitude.CompareTo(
                        (right.Tree.transform.position - view.transform.position).sqrMagnitude);
                });
                var started = false;
                for (var i = 0; i < _candidateTrees.Count; i++)
                {
                    var candidate = _candidateTrees[i];
                    if (!TryStartCivilianLogging(
                            entity, civilian, area, view, candidate.Tree,
                            candidate.AdministrativeSite, candidates, legal,
                            preferred, unclaimed, otherFaction))
                        continue;
                    started = true;
                    break;
                }
                if (started)
                    continue;

                var materialized = world.ContinuousOutdoorMaterialization.TryGetPlace(
                    civilian.CurrentSiteId, area.LocationId, out _);
                var reason = !materialized
                    ? "前往伐木区：目标地点尚未加载"
                    : candidates == 0
                        ? "伐木待命：本区没有已加载树木"
                        : legal > 0
                            ? "伐木受阻：己方控制范围内树木不可达"
                            : unclaimed > 0 && otherFaction == 0
                                ? "伐木受阻：树木处于无主控制范围"
                                : otherFaction > 0 && unclaimed == 0
                                    ? "伐木受阻：树木属于其他势力控制范围"
                                    : "伐木受阻：无主树木 " + unclaimed +
                                      "，其他势力树木 " + otherFaction;
                SetCivilianStatus(entity.Id, reason, null,
                    area.Id, area.LocationId, candidates, legal, null,
                    preferred, unclaimed, otherFaction);
            }
        }

        bool TryResolveCivilianLoggingControl(
            XianXia.Core.Simulation.SimulationWorld world,
            Entity logger,
            HostMapDestructible tree,
            out WorldSite administrativeSite,
            out LoggingControlKind controlKind)
        {
            administrativeSite = null;
            controlKind = LoggingControlKind.Unclaimed;
            if (world == null || logger == null || tree == null ||
                !logger.TryGet<XianXia.Core.Social.FactionMembershipComponent>(out var membership) ||
                !membership.IsAffiliated)
                return false;
            var continuous = bootstrap?.ContinuousOutdoorSurfaceRuntime;
            if (continuous == null || !continuous.IsActive ||
                !continuous.PresentationToWorld(
                    tree.transform.position.x, tree.transform.position.y, out var wx, out var wy) ||
                !WorldSiteAdministrativeControlResolver.TryResolve(
                    world, continuous.ActiveSurfaceId, wx, wy, out administrativeSite, out _) ||
                administrativeSite == null || string.IsNullOrEmpty(administrativeSite.OwnerFactionId))
                return false;
            if (!string.Equals(administrativeSite.OwnerFactionId, membership.FactionId, StringComparison.Ordinal))
            {
                controlKind = LoggingControlKind.OtherFaction;
                return false;
            }
            controlKind = LoggingControlKind.OwnFaction;
            return true;
        }

        bool TryStartCivilianLogging(
            Entity logger,
            MortalCivilianState civilian,
            WorkAreaDefinition area,
            EntityView view,
            HostMapDestructible tree,
            WorldSite administrativeSite,
            int candidates,
            int legal,
            int preferred,
            int unclaimed,
            int otherFaction)
        {
            if (logger == null || civilian == null || area == null || view == null || tree == null ||
                administrativeSite == null || string.IsNullOrEmpty(administrativeSite.SiteId))
                return false;

            var inRange = DistanceToTargetFootprint(tree, view.transform.position) <= engageRange &&
                          moveController != null && moveController.IsCharacterStandClear(logger.Id);
            var approach = view.transform.position;
            uint ownedRevision = 0u;
            var scheduleMover = bootstrap.GetComponent<HostNpcScheduleMover>();
            if (inRange)
            {
                // Clear only the old schedule path bookkeeping/path before standing to chop.
                scheduleMover?.RelinquishCivilianIntentPathForLogging(logger.Id, cancelCurrentPath: true);
            }
            else
            {
                if (!TryResolveTreeApproach(logger.Id, view.transform.position, tree, out approach))
                {
                    ExcludeTemporarily(logger.Id, tree);
                    return false;
                }
                var accepted = moveController != null && moveController.OrderEntityToWorldPoint(
                    logger.Id, approach, arriveCommand: null, issueStop: false, exactGoal: true);
                if (!accepted)
                {
                    ExcludeTemporarily(logger.Id, tree);
                    return false;
                }
                // The logging path has replaced the old center path; forget the stale Core
                // revision without cancelling the new path we just accepted.
                scheduleMover?.RelinquishCivilianIntentPathForLogging(logger.Id, cancelCurrentPath: false);
                ownedRevision = moveController.GetPresentationMovementRevision(logger.Id);
            }

            var session = new Session
            {
                Attacker = logger.Id,
                Target = tree,
                Cooldown = 0f,
                NextRepath = Time.unscaledTime + Mathf.Max(0.75f, civilianRepathInterval),
                FromCivilianWork = true,
                WorkAreaId = area.Id,
                WorkLocationId = area.LocationId,
                TargetAdministrativeSiteId = administrativeSite.SiteId,
                // Production is deposited where the tree is actually administered.  This keeps
                // same-faction cross-Site permission from becoming an arbitrary CurrentSite deposit.
                StockSiteId = administrativeSite.SiteId,
                ApproachPoint = approach,
                OwnedMovementRevision = ownedRevision,
                LatestOutput = _lastCivilianOutput.TryGetValue(logger.Id.Value, out var output)
                    ? output : string.Empty,
                CandidateCount = candidates,
                LegalCount = legal,
                PreferredCount = preferred,
                UnclaimedCount = unclaimed,
                OtherFactionCount = otherFaction
            };
            _sessions.Add(session);
            SetCivilianStatus(logger.Id, inRange ? "砍伐中" : "前往树木", session,
                area.Id, area.LocationId, candidates, legal, tree,
                preferred, unclaimed, otherFaction);
            return true;
        }

        bool IsCivilianSessionStillValid(
            Session session,
            out Entity logger,
            out MortalCivilianState civilian,
            out string reason)
        {
            logger = null;
            civilian = null;
            reason = string.Empty;
            var world = bootstrap?.Session?.World;
            if (session == null || world == null || session.Attacker.IsNone ||
                !world.Entities.TryGet(session.Attacker, out logger))
            { reason = "人物不存在"; return false; }
            if (!world.Civilians.TryGet(session.Attacker, out civilian))
            { reason = "不再由民生系统管理"; return false; }
            if (civilian.Profession != MortalProfession.Logger)
            { reason = "职业已变更"; return false; }
            if (civilian.Activity != MortalActivity.Logging)
            { reason = "日程已切换"; return false; }
            if (!AutonomousActionContinuationService.CanContinue(world, session.Attacker))
            { reason = "生命或行动状态不允许"; return false; }
            if (!MortalCivilianMovementAuthority.CanCivilianOwnLocalMovement(world, session.Attacker, civilian))
            { reason = "移动权已移交"; return false; }
            if (!world.TryGetWorkArea(session.WorkAreaId, out var area) ||
                !string.Equals(area.LocationId, session.WorkLocationId, StringComparison.Ordinal))
            { reason = "伐木工区已失效"; return false; }
            if (session.Target == null || session.Target.IsDestroyed ||
                !TryResolveCivilianLoggingControl(
                    world, logger, session.Target, out var administrativeSite, out _))
            { reason = "树木不再是合法目标"; return false; }
            session.TargetAdministrativeSiteId = administrativeSite.SiteId;
            session.StockSiteId = administrativeSite.SiteId;
            return true;
        }

        bool TryResolveTreeApproach(EntityId actor, Vector3 from, HostMapDestructible tree, out Vector3 approach)
        {
            approach = tree != null ? tree.transform.position : default;
            if (tree == null || moveController?.WalkGrid == null)
                return false;
            var bounds = tree.HasInteractionBounds
                ? tree.InteractionBounds
                : new Rect(tree.transform.position.x - .25f, tree.transform.position.y - .25f, .5f, .5f);
            var center = bounds.center;
            var direction = new Vector2(from.x - center.x, from.y - center.y);
            if (direction.sqrMagnitude < .001f)
                direction = Vector2.down;
            direction.Normalize();
            var grid = moveController.WalkGrid;
            var angle = Mathf.Atan2(direction.y, direction.x);
            // Reserve approach positions around the tree, not the whole tree. Several
            // workers can cooperate without sharing the same snapped cell center.
            for (var i = 0; i < 16; i++)
            {
                var turn = i == 0 ? 0f : ((i + 1) / 2) * Mathf.PI / 8f * (i % 2 == 1 ? 1f : -1f);
                var radial = new Vector2(Mathf.Cos(angle + turn), Mathf.Sin(angle + turn));
                var edge = ClosestPoint(bounds, center + radial * (bounds.size.magnitude + engageRange));
                var desired = edge + radial * Mathf.Min(.45f, engageRange * .3f);
                if (!grid.TryWorldToCell(desired.x, desired.y, out var cx, out var cy) ||
                    !grid.IsWalkable(cx, cy)) continue;
                var preferred = new Vector3(desired.x, desired.y, HostPresentationSpace.EntityZ);
                if (!moveController.TryResolveFreeStandPoint(actor, preferred, .75f, out var candidate) ||
                    DistanceToTargetFootprint(tree, candidate) > engageRange) continue;
                approach = candidate;
                return true;
            }
            return false;
        }

        static float DistanceToTargetFootprint(HostMapDestructible target, Vector3 point)
        {
            if (target == null)
                return float.MaxValue;
            if (!target.HasInteractionBounds)
                return Vector2.Distance(point, target.transform.position);
            var nearest = ClosestPoint(target.InteractionBounds, new Vector2(point.x, point.y));
            return Vector2.Distance(new Vector2(point.x, point.y), nearest);
        }

        static Vector2 ClosestPoint(Rect bounds, Vector2 point) => new Vector2(
            Mathf.Clamp(point.x, bounds.xMin, bounds.xMax),
            Mathf.Clamp(point.y, bounds.yMin, bounds.yMax));

        void ExcludeTemporarily(EntityId logger, HostMapDestructible tree)
        {
            if (logger.IsNone || tree == null)
                return;
            _retryAfterByLoggerAndTree[RetryKey(logger, tree)] =
                Time.unscaledTime + Mathf.Max(2f, unreachableRetrySeconds);
        }

        float RetryAfter(EntityId logger, HostMapDestructible tree) =>
            _retryAfterByLoggerAndTree.TryGetValue(RetryKey(logger, tree), out var retryAt) ? retryAt : 0f;

        static string RetryKey(EntityId logger, HostMapDestructible tree) =>
            logger.Value + "|" + (!string.IsNullOrEmpty(tree?.PlacementId)
                ? tree.PlacementId : tree != null ? tree.GetInstanceID().ToString() : string.Empty);

        Session FindSession(EntityId id)
        {
            if (id.IsNone)
                return null;
            for (var i = 0; i < _sessions.Count; i++)
                if (_sessions[i].Attacker == id)
                    return _sessions[i];
            return null;
        }

        void ReleaseOwnedMovement(Session session)
        {
            if (session == null || session.OwnedMovementRevision == 0u)
                return;
            moveController?.CancelPresentationMovementIfRevision(
                session.Attacker, session.OwnedMovementRevision);
            session.OwnedMovementRevision = 0u;
        }

        void SetCivilianStatus(
            EntityId id,
            string status,
            Session session,
            string workAreaId = null,
            string workLocationId = null,
            int candidates = 0,
            int legal = 0,
            HostMapDestructible target = null,
            int preferred = 0,
            int unclaimed = 0,
            int otherFaction = 0)
        {
            if (id.IsNone)
                return;
            _civilianStatus[id.Value] = status ?? string.Empty;
            target = target != null ? target : session?.Target;
            workAreaId = workAreaId ?? session?.WorkAreaId ?? string.Empty;
            workLocationId = workLocationId ?? session?.WorkLocationId ?? string.Empty;
            if (session != null && candidates == 0 && legal == 0 && preferred == 0 &&
                unclaimed == 0 && otherFaction == 0)
            {
                candidates = session.CandidateCount;
                legal = session.LegalCount;
                preferred = session.PreferredCount;
                unclaimed = session.UnclaimedCount;
                otherFaction = session.OtherFactionCount;
            }
            var key = (status ?? string.Empty) + "|" +
                      (target != null ? target.PlacementId : string.Empty) + "|" +
                      (session?.TargetAdministrativeSiteId ?? string.Empty);
            if (_lastDiagnostic.TryGetValue(id.Value, out var previous) &&
                string.Equals(previous, key, StringComparison.Ordinal))
                return;
            _lastDiagnostic[id.Value] = key;

            var world = bootstrap?.Session?.World;
            Entity entity = null;
            MortalCivilianState civilian = null;
            if (world != null)
            {
                world.Entities.TryGet(id, out entity);
                world.Civilians.TryGet(id, out civilian);
            }
            var name = entity != null && entity.TryGet<IdentityComponent>(out var identity)
                ? identity.DisplayName : id.ToString();
            var life = entity != null && entity.TryGet<LifecycleComponent>(out var lifecycle)
                ? lifecycle.State.ToString() : "Missing";
            var owner = civilian != null
                ? MortalCivilianMovementAuthority.Resolve(world, id, civilian).ToString() : "None";
            var output = _lastCivilianOutput.TryGetValue(id.Value, out var latest)
                ? latest : "无";
            Debug.Log("[CivilianLogging] EntityId=" + id.Value + " Name=" + name +
                      " Life=" + life + " Profession=" + (civilian?.Profession.ToString() ?? "None") +
                      " Activity=" + (civilian?.Activity.ToString() ?? "None") +
                      " MovementOwner=" + owner + " WorkArea=" + workAreaId +
                      " WorkLocation=" + workLocationId + " Candidates=" + candidates +
                      " Preferred=" + preferred + " Legal=" + legal +
                      " Unclaimed=" + unclaimed + " OtherFaction=" + otherFaction +
                      " Target=" + (target != null ? target.PlacementId : "None") +
                      " AdministrativeSite=" + (session?.TargetAdministrativeSiteId ?? "None") +
                      " StockSite=" + (session?.StockSiteId ?? "None") +
                      " Path=" + (moveController?.IsMoving(id) == true ? "Moving" : "Idle") +
                      " Approach=(" + (session != null ? session.ApproachPoint.x.ToString("0.###") : "—") +
                      "," + (session != null ? session.ApproachPoint.y.ToString("0.###") : "—") + ")" +
                      " PathRevision=" + (session?.OwnedMovementRevision ?? 0u) +
                      " TargetHp=" + (target != null ? target.CurrentHp + "/" + target.MaxHp : "—") +
                      " LatestOutput=" + output + " CurrentSite=" + (civilian?.CurrentSiteId ?? string.Empty) +
                      " Status=" + status, this);
        }

        void RemoveSessionsOnTarget(HostMapDestructible target, bool clearInspect)
        {
            for (var i = _sessions.Count - 1; i >= 0; i--)
            {
                if (_sessions[i].Target != target)
                    continue;
                if (_sessions[i].FromCivilianWork)
                    ReleaseOwnedMovement(_sessions[i]);
                _sessions.RemoveAt(i);
            }

            if (clearInspect)
                bootstrap?.GetComponent<HostHousingAreaSelection>()?.Clear();
        }

        void RemoveSession(EntityId attacker, bool clearInspect)
        {
            for (var i = _sessions.Count - 1; i >= 0; i--)
            {
                if (_sessions[i].Attacker != attacker)
                    continue;
                _sessions.RemoveAt(i);
            }

            if (clearInspect && _sessions.Count == 0)
                bootstrap?.GetComponent<HostHousingAreaSelection>()?.Clear();
        }

        void ClearAllSessions(bool clearInspect)
        {
            _sessions.Clear();
            if (clearInspect)
                bootstrap?.GetComponent<HostHousingAreaSelection>()?.Clear();
        }

        static int GrantRoughWood(XianXia.Core.Simulation.SimulationWorld world, int yield)
        {
            if (world?.Inventory == null || yield <= 0)
                return 0;

            var id = HostMapDestructible.RoughWoodItemId;
            if (!world.InventoryCatalog.TryGet(id, out _))
                world.InventoryCatalog.Register(id, "粗木", 99, new[] { "resource", "wood" });

            var added = world.Inventory.TryAdd(id, yield);
            if (added > 0)
            {
                world.Events.Publish(
                    XianXia.Core.Events.EventType.PartyInventoryChanged,
                    world.Tick,
                    payload: "bag:" + id + ":" + added + ":chopTree");
                QuestProgressRefresh.AfterWorldChange(world, EntityId.None);
            }
            else
            {
                Debug.LogWarning(
                    "[Host] 砍树粗木未入包：TryAdd 返回 0（背包可能已满）。yield=" + yield +
                    " usedSlots=" + world.Inventory.UsedSlotCount +
                    "/" + world.Inventory.SlotCapacity);
            }

            return added;
        }

        bool ChaseCivilian(Session session)
        {
            if (moveController == null || session?.Target == null)
                return false;
            if (Time.unscaledTime < session.NextRepath)
                return true;
            session.NextRepath = Time.unscaledTime + Mathf.Max(.75f, civilianRepathInterval);
            if (moveController.IsMoving(session.Attacker) &&
                moveController.TryResolveFreeStandPoint(
                    session.Attacker, session.ApproachPoint, 0f, out _))
                return true;
            // A precise approach can become occupied after it was issued. Reconsider it
            // on the existing throttle instead of waiting forever on IsMoving alone.
            ReleaseOwnedMovement(session);
            if (!viewSpawner.Registry.TryGet(session.Attacker, out var atkView) || atkView == null ||
                !TryResolveTreeApproach(session.Attacker, atkView.transform.position, session.Target, out var dest))
            {
                ExcludeTemporarily(session.Attacker, session.Target);
                SetCivilianStatus(session.Attacker, "伐木受阻：目标没有可达接近点", session);
                ReleaseOwnedMovement(session);
                return false;
            }
            if (!moveController.OrderEntityToWorldPoint(
                    session.Attacker, dest, arriveCommand: null, issueStop: false, exactGoal: true))
            {
                ExcludeTemporarily(session.Attacker, session.Target);
                SetCivilianStatus(session.Attacker, "伐木受阻：目标路径不连通", session);
                ReleaseOwnedMovement(session);
                return false;
            }
            session.ApproachPoint = dest;
            session.OwnedMovementRevision = moveController.GetPresentationMovementRevision(session.Attacker);
            return true;
        }

        // Player/follower tree and wall pursuit retains its original center-based range,
        // but chooses a free point inside that range instead of a shared point on one side.
        void ChaseExisting(Session session)
        {
            if (moveController == null || session?.Target == null)
                return;
            if (Time.unscaledTime < session.NextRepath)
                return;
            session.NextRepath = Time.unscaledTime + repathInterval;
            if (viewSpawner == null ||
                !viewSpawner.Registry.TryGet(session.Attacker, out var atkView) || atkView == null)
                return;
            var center = session.Target.transform.position;
            var delta = atkView.transform.position - center;
            var angle = delta.sqrMagnitude > .001f ? Mathf.Atan2(delta.y, delta.x) : -Mathf.PI * .5f;
            for (var i = 0; i < 16; i++)
            {
                var turn = i == 0 ? 0f : ((i + 1) / 2) * Mathf.PI / 8f * (i % 2 == 1 ? 1f : -1f);
                var preferred = center + new Vector3(Mathf.Cos(angle + turn), Mathf.Sin(angle + turn), 0f) *
                    (engageRange * .75f);
                preferred.z = HostPresentationSpace.EntityZ;
                if (!moveController.TryResolveFreeStandPoint(session.Attacker, preferred, .2f, out var dest) ||
                    Vector2.Distance(dest, center) > engageRange)
                    continue;
                if (moveController.OrderEntityToWorldPoint(
                        session.Attacker, dest, arriveCommand: null, issueStop: false, exactGoal: true))
                    return;
            }
        }

        bool TryGetPresentation(EntityId id, out float x, out float y)
        {
            x = y = 0f;
            if (viewSpawner != null &&
                viewSpawner.Registry.TryGet(id, out var view) &&
                view != null)
            {
                var p = HostPresentationSpace.ToPresentation(view.transform.position);
                x = p.x;
                y = p.y;
                return true;
            }

            return false;
        }

        void Toast(EntityId id, string text, Color color)
        {
            var overlay = bootstrap != null ? bootstrap.GetComponent<HostFeedbackOverlay>() : null;
            if (overlay == null || viewSpawner == null)
                return;
            overlay.SpawnAtEntity(viewSpawner, id, text, color);
        }
    }
}
