using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Input;
using XianXia.Core.Results;
using XianXia.Core.Exploration;
using XianXia.Core.Settlement;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Selection → PlayerCommandRequest → IPlayerInputPort.
    /// VS0.6: Help／Slight／Recruit resolve Actor＋Target from selection.
    /// </summary>
    public sealed class HostCommandBridge : MonoBehaviour
    {
        public const ulong DefaultDurationTicks = 1;

        /// <summary>1x 下采集／劳动一轮的目标现实秒数（倍速会按同一 tick 节奏加快）。</summary>
        public const float GatherWallSecondsAt1x = 10f;

        [SerializeField] HostSelectionController selectionController;
        [SerializeField] PlayableHostBootstrap hostBootstrap;
        [SerializeField] HostWorkLoop workLoop;
        [SerializeField] bool enableDebugKeys = true;
        [SerializeField] bool showDebugButtons = false;
        // 数字键 1–6 专供斗技栏（HostCombatSkillBar）；以下行动改走 V／菜单／底部按钮，不再绑数字。
        [SerializeField] KeyCode laborKey = KeyCode.None;
        [SerializeField] KeyCode restKey = KeyCode.None;
        [SerializeField] KeyCode observeKey = KeyCode.None;
        [SerializeField] KeyCode cultivateKey = KeyCode.None;
        [SerializeField] KeyCode helpKey = KeyCode.None;
        [SerializeField] KeyCode slightKey = KeyCode.None;
        [SerializeField] KeyCode recruitKey = KeyCode.None;
        [SerializeField] KeyCode assignLaborKey = KeyCode.None;
        [SerializeField] KeyCode assignGatherKey = KeyCode.None;
        [SerializeField] KeyCode assignCultivateKey = KeyCode.None;
        [SerializeField] KeyCode exploreKey = KeyCode.T;
        [SerializeField] KeyCode travelKey = KeyCode.None;
        [SerializeField] EntityViewSpawner viewSpawner;
        [SerializeField] HostFeedbackOverlay feedbackOverlay;

        PlayableHostSession _session;
        string _lastStatus = "No command yet";
        int _lastSuccessCount;
        int _lastFailureCount;

        public string LastStatus => _lastStatus;

        public int LastSuccessCount => _lastSuccessCount;

        public int LastFailureCount => _lastFailureCount;

        public void Bind(
            PlayableHostSession session,
            HostSelectionController selection,
            HostFeedbackOverlay feedback = null)
        {
            _session = session;
            selectionController = selection;
            if (feedback != null)
                feedbackOverlay = feedback;
            if (hostBootstrap == null)
                hostBootstrap = GetComponent<PlayableHostBootstrap>();
            if (workLoop == null)
                workLoop = GetComponent<HostWorkLoop>();
            _lastStatus = "Bound";
            _lastSuccessCount = 0;
            _lastFailureCount = 0;
        }

        public ulong GatherDurationTicks()
        {
            var secPer = hostBootstrap != null
                ? hostBootstrap.SecondsPerAutoTickAt1x
                : SimulationTickPacing.SecondsPerTickAt1x;
            if (secPer < 0.01f)
                secPer = SimulationTickPacing.SecondsPerTickAt1x;
            return (ulong)Mathf.Max(1, Mathf.CeilToInt(GatherWallSecondsAt1x / secPer));
        }

        ulong ResolveDuration(PlayerCommandKind kind, ulong durationTicks)
        {
            if (kind == PlayerCommandKind.Labor && durationTicks == DefaultDurationTicks)
                return GatherDurationTicks();
            return durationTicks;
        }

        void Update()
        {
            if (!enableDebugKeys || _session == null || !_session.IsInitialized)
                return;

            // Demo letter keys [49] (W work-target mode is HostWorkTargetMode; camera: arrows / Alt+WASD).
            if (Input.GetKeyDown(KeyCode.S))
                IssueSelected(PlayerCommandKind.Stop, 0);
            else if (Input.GetKeyDown(KeyCode.C))
            {
                if (hostBootstrap != null &&
                    hostBootstrap.CultivateConfirm != null &&
                    selectionController != null &&
                    selectionController.State.Count > 0)
                    hostBootstrap.CultivateConfirm.OpenFor(selectionController.State.SelectedIds[0]);
                else
                    IssueSelected(PlayerCommandKind.Cultivate);
            }
            else if (Input.GetKeyDown(KeyCode.X))
                IssueSelected(PlayerCommandKind.Stop, 0);
            else if (Input.GetKeyDown(KeyCode.G))
                IssueSelected(PlayerCommandKind.UseConcealGrass, 0);
            else if (IsBoundKeyDown(laborKey))
                IssueSelected(PlayerCommandKind.Labor);
            else if (IsBoundKeyDown(restKey))
                IssueSelected(PlayerCommandKind.Rest);
            else if (IsBoundKeyDown(observeKey))
                IssueSelected(PlayerCommandKind.Observe);
            else if (IsBoundKeyDown(cultivateKey))
            {
                if (hostBootstrap != null &&
                    hostBootstrap.CultivateConfirm != null &&
                    selectionController != null &&
                    selectionController.State.Count > 0)
                    hostBootstrap.CultivateConfirm.OpenFor(selectionController.State.SelectedIds[0]);
                else
                    IssueSelected(PlayerCommandKind.Cultivate);
            }
            else if (IsBoundKeyDown(helpKey))
                IssueSocial(PlayerCommandKind.Help);
            else if (IsBoundKeyDown(slightKey))
                IssueSocial(PlayerCommandKind.Slight);
            else if (IsBoundKeyDown(recruitKey))
                IssueSocial(PlayerCommandKind.Recruit);
            else if (IsBoundKeyDown(assignLaborKey))
                IssueAssignWork(WorkRoleKind.Labor);
            else if (IsBoundKeyDown(assignGatherKey))
                IssueAssignWork(WorkRoleKind.Gather);
            else if (IsBoundKeyDown(assignCultivateKey))
                IssueAssignWork(WorkRoleKind.Cultivate);
            else if (IsBoundKeyDown(exploreKey))
                IssueExplore();
            else if (IsBoundKeyDown(travelKey))
                IssueTravelNextAdjacent();
        }

        static bool IsBoundKeyDown(KeyCode key) =>
            key != KeyCode.None && Input.GetKeyDown(key);

        void OnGUI()
        {
            if (!showDebugButtons || _session == null || !_session.IsInitialized)
                return;

            const float w = 88f;
            const float h = 28f;
            var y = 8f;
            if (GUI.Button(new Rect(8f, y, w, h), "劳动"))
                IssueSelected(PlayerCommandKind.Labor);
            if (GUI.Button(new Rect(8f + (w + 6f), y, w, h), "休息"))
                IssueSelected(PlayerCommandKind.Rest);
            if (GUI.Button(new Rect(8f + 2f * (w + 6f), y, w, h), "观察"))
                IssueSelected(PlayerCommandKind.Observe);
            if (GUI.Button(new Rect(8f + 3f * (w + 6f), y, w, h), "修炼"))
                IssueSelected(PlayerCommandKind.Cultivate);

            y += h + 6f;
            if (GUI.Button(new Rect(8f, y, w, h), "帮助"))
                IssueSocial(PlayerCommandKind.Help);
            if (GUI.Button(new Rect(8f + (w + 6f), y, w, h), "轻慢"))
                IssueSocial(PlayerCommandKind.Slight);
            if (GUI.Button(new Rect(8f + 2f * (w + 6f), y, w, h), "招募"))
                IssueSocial(PlayerCommandKind.Recruit);

            y += h + 6f;
            if (GUI.Button(new Rect(8f, y, w, h), "分工劳"))
                IssueAssignWork(WorkRoleKind.Labor);
            if (GUI.Button(new Rect(8f + (w + 6f), y, w, h), "分工采"))
                IssueAssignWork(WorkRoleKind.Gather);
            if (GUI.Button(new Rect(8f + 2f * (w + 6f), y, w, h), "分工修"))
                IssueAssignWork(WorkRoleKind.Cultivate);

            y += h + 6f;
            if (GUI.Button(new Rect(8f, y, w, h), "探索(T)"))
                IssueExplore();
            if (GUI.Button(new Rect(8f + (w + 6f), y, w, h), "本地邻接"))
                IssueTravelNextAdjacent();
        }

        public int IssueExplore()
        {
            if (selectionController == null || selectionController.State.Count == 0)
            {
                _lastStatus = "Empty selection";
                return 0;
            }

            _lastSuccessCount = 0;
            _lastFailureCount = 0;
            if (_session?.Port == null)
                return 0;

            var id = selectionController.State.SelectedIds[0];
            var result = _session.Port.Submit(
                new PlayerCommandRequest(id, PlayerCommandKind.Explore, 1));
            if (result.IsSuccess)
            {
                _lastSuccessCount = 1;
                _lastStatus = "Explore ok entity=" + id.Value;
            }
            else
            {
                _lastFailureCount = 1;
                _lastStatus = "Explore FAIL " + FormatError(result);
            }

            return _lastSuccessCount;
        }

        public int IssueTravelNextAdjacent()
        {
            if (selectionController == null || selectionController.State.Count == 0 || _session?.Port == null)
            {
                _lastStatus = "Cannot travel";
                return 0;
            }

            var id = selectionController.State.SelectedIds[0];
            if (!_session.World.Entities.TryGet(id, out var entity) ||
                !entity.TryGet<EntityLocationComponent>(out var loc) ||
                !loc.HasLocation ||
                !_session.World.WorldRegion.TryGet(loc.LocationId, out var location) ||
                location.AdjacentIds.Count == 0)
            {
                _lastStatus = "No adjacent location";
                _lastFailureCount = 1;
                return 0;
            }

            var target = location.AdjacentIds[0];
            var result = _session.Port.Submit(
                new PlayerCommandRequest(
                    id,
                    PlayerCommandKind.Travel,
                    1,
                    EntityId.None,
                    WorkRoleKind.None,
                    target));
            if (result.IsSuccess)
            {
                _lastSuccessCount = 1;
                _lastFailureCount = 0;
                _lastStatus = "Travel → " + target;
                if (viewSpawner != null)
                    viewSpawner.SyncLocations(_session);
            }
            else
            {
                _lastSuccessCount = 0;
                _lastFailureCount = 1;
                _lastStatus = "Travel FAIL " + FormatError(result);
            }

            return _lastSuccessCount;
        }

        public int IssueEnterLocalMap()
        {
            if (selectionController == null || selectionController.State.Count == 0)
            {
                _lastStatus = "Cannot enter LocalMap";
                return 0;
            }

            var id = selectionController.State.SelectedIds[0];
            return IssueEnterLocalMapWithParty(id, null, new[] { id });
        }

        /// <summary>
        /// 入洞：先把随行者 Location 写到洞口，再 Enter（Core 只带走站在洞口的己方）。
        /// </summary>
        public int IssueEnterLocalMapWithParty(
            EntityId leader,
            string entranceLocationId,
            EntityId[] party)
        {
            if (_session?.Port == null || leader.IsNone || party == null || party.Length == 0)
            {
                _lastStatus = "Cannot enter LocalMap";
                return 0;
            }

            if (string.IsNullOrWhiteSpace(entranceLocationId))
            {
                if (!_session.World.Entities.TryGet(leader, out var ent) ||
                    !ent.TryGet<XianXia.Core.Exploration.EntityLocationComponent>(out var loc) ||
                    !loc.HasLocation)
                {
                    _lastStatus = "No entrance location";
                    return 0;
                }

                entranceLocationId = loc.LocationId;
            }

            entranceLocationId = entranceLocationId.Trim();
            if (!_session.World.WorldRegion.TryGet(entranceLocationId, out _))
            {
                _lastStatus = "Entrance missing";
                return 0;
            }

            for (var i = 0; i < party.Length; i++)
            {
                var pid = party[i];
                if (pid.IsNone || !_session.World.Entities.TryGet(pid, out var pe))
                    continue;
                if (!pe.TryGet<XianXia.Core.Exploration.EntityLocationComponent>(out var plc))
                {
                    plc = new XianXia.Core.Exploration.EntityLocationComponent();
                    pe.AddComponent(plc);
                }

                // 已在目标洞府内室的人不必先拽到洞口（再进救人时保留洞内站位）。
                if (_session.World.WorldRegion.TryGet(plc.LocationId, out var cur) &&
                    !string.IsNullOrEmpty(cur.LocalMapId) &&
                    _session.World.WorldRegion.TryGet(entranceLocationId, out var entLoc) &&
                    string.Equals(cur.LocalMapId, entLoc.EnterLocalMapId, System.StringComparison.Ordinal))
                    continue;

                plc.LocationId = entranceLocationId;
            }

            CancelPartyPresentationMovement();

            var result = _session.Port.Submit(
                new PlayerCommandRequest(
                    leader,
                    PlayerCommandKind.EnterLocalMap,
                    1,
                    EntityId.None,
                    WorkRoleKind.None,
                    entranceLocationId));
            if (result.IsSuccess)
            {
                // 双保险：把勾选随行登记进 session（Core 也会按 Location 刷新）。
                for (var i = 0; i < party.Length; i++)
                    _session.World.LocalMap.AddOccupant(party[i]);

                _lastSuccessCount = 1;
                _lastFailureCount = 0;
                _lastStatus = "EnterLocalMap ok → " + entranceLocationId;
                hostBootstrap?.ReloadLocalMapPresentation(frameCamera: true);
            }
            else
            {
                _lastSuccessCount = 0;
                _lastFailureCount = 1;
                _lastStatus = "EnterLocalMap FAIL " + FormatError(result);
            }

            return _lastSuccessCount;
        }

        public int IssueLeaveLocalMap()
        {
            if (selectionController == null || _session?.Port == null)
            {
                _lastStatus = "Cannot leave LocalMap";
                return 0;
            }

            EntityId id = EntityId.None;
            if (selectionController.State.Count > 0)
                id = selectionController.State.SelectedIds[0];
            if (id.IsNone)
            {
                var occupants = _session.World.LocalMap.OccupantIds;
                if (occupants.Count > 0)
                    id = occupants[0];
            }

            if (id.IsNone)
            {
                _lastStatus = "Cannot leave LocalMap";
                return 0;
            }

            // 离开前停掉洞内走位，避免 Location 再被表现层吸附错乱。
            CancelPartyPresentationMovement();

            var result = _session.Port.Submit(
                new PlayerCommandRequest(id, PlayerCommandKind.LeaveLocalMap, 1));
            if (result.IsSuccess)
            {
                _lastSuccessCount = 1;
                _lastFailureCount = 0;
                _lastStatus = "LeaveLocalMap ok";
                hostBootstrap?.ReloadLocalMapPresentation();
            }
            else
            {
                _lastSuccessCount = 0;
                _lastFailureCount = 1;
                _lastStatus = "LeaveLocalMap FAIL " + FormatError(result);
            }

            return _lastSuccessCount;
        }

        void CancelPartyPresentationMovement()
        {
            var move = hostBootstrap != null ? hostBootstrap.MoveController : null;
            if (move == null || _session?.World == null)
                return;
            var occupants = _session.World.LocalMap.OccupantIds;
            for (var i = 0; i < occupants.Count; i++)
                move.CancelPresentationMovementPublic(occupants[i]);
            if (selectionController == null)
                return;
            for (var i = 0; i < selectionController.State.Count; i++)
                move.CancelPresentationMovementPublic(selectionController.State.SelectedIds[i]);
        }

        /// <summary>以角色表现为圆心勘查。<paramref name="presentationHint"/> 形如 "x,z,r;x2,z2,r2"。</summary>
        public int IssueSurveyAround(string presentationHint = null)
        {
            if (selectionController == null || selectionController.State.Count == 0 || _session?.Port == null)
            {
                _lastStatus = "Cannot survey";
                return 0;
            }

            var id = selectionController.State.SelectedIds[0];
            var result = _session.Port.Submit(
                new PlayerCommandRequest(
                    id,
                    PlayerCommandKind.SurveyEntrance,
                    1,
                    EntityId.None,
                    WorkRoleKind.None,
                    presentationHint ?? string.Empty));
            if (result.IsSuccess)
            {
                _lastSuccessCount = 1;
                _lastFailureCount = 0;
                _lastStatus = "SurveyAround ok";
            }
            else
            {
                _lastSuccessCount = 0;
                _lastFailureCount = 1;
                _lastStatus = "SurveyAround FAIL " + FormatError(result);
            }

            return _lastSuccessCount;
        }

        /// <summary>拾取地上物进背包（一次）。</summary>
        public int IssuePickupLoot(EntityId subject, string lootSpotId, string itemId)
        {
            if (_session?.World == null || subject.IsNone)
            {
                _lastStatus = "Cannot pickup";
                return 0;
            }

            var result = new XianXia.Core.Content.WorldLootPickupService().TryPickup(
                _session.World, subject, lootSpotId, itemId);
            if (result.IsSuccess)
            {
                _lastSuccessCount = 1;
                _lastFailureCount = 0;
                _lastStatus = "Pickup ok → " + itemId;
                return 1;
            }

            _lastSuccessCount = 0;
            _lastFailureCount = 1;
            _lastStatus = "Pickup FAIL " + FormatError(result);
            return 0;
        }

        public int IssueAssignWork(WorkRoleKind role)
        {
            if (selectionController == null)
            {
                _lastStatus = "No selection controller";
                _lastSuccessCount = 0;
                _lastFailureCount = 0;
                return 0;
            }

            return IssueAssignWorkTo(selectionController.State.SelectedIds, role);
        }

        public int IssueAssignWorkTo(IReadOnlyList<EntityId> targets, WorkRoleKind role)
        {
            _lastSuccessCount = 0;
            _lastFailureCount = 0;

            if (_session == null || !_session.IsInitialized || _session.Port == null)
            {
                _lastStatus = "Session／Port not ready";
                return 0;
            }

            if (targets == null || targets.Count == 0)
            {
                _lastStatus = "Empty selection";
                return 0;
            }

            var allowed = BuildAllowedSet(_session.CharacterIds);
            for (var i = 0; i < targets.Count; i++)
            {
                var id = targets[i];
                if (id.IsNone || !allowed.Contains(id.Value))
                {
                    _lastFailureCount++;
                    continue;
                }

                var result = _session.Port.Submit(
                    new PlayerCommandRequest(id, PlayerCommandKind.AssignWork, 1, EntityId.None, role));
                if (result.IsSuccess)
                    _lastSuccessCount++;
                else
                    _lastFailureCount++;
            }

            _lastStatus = "AssignWork=" + role +
                          " ok=" + _lastSuccessCount +
                          " fail=" + _lastFailureCount;
            return _lastSuccessCount;
        }

        /// <summary>Issue labor-style command to selected Characters only.</summary>
        public int IssueSelected(PlayerCommandKind kind, ulong durationTicks = DefaultDurationTicks)
        {
            if (kind == PlayerCommandKind.Help ||
                kind == PlayerCommandKind.Slight ||
                kind == PlayerCommandKind.Recruit)
                return IssueSocial(kind) ? 1 : 0;

            if (selectionController == null)
            {
                _lastStatus = "No selection controller";
                _lastSuccessCount = 0;
                _lastFailureCount = 0;
                return 0;
            }

            return IssueTo(selectionController.State.SelectedIds, kind, ResolveDuration(kind, durationTicks));
        }

        /// <summary>ACS 角色面板：只对单个焦点角色下令（不是全选）。</summary>
        public int IssueOne(EntityId subject, PlayerCommandKind kind, ulong durationTicks = DefaultDurationTicks)
        {
            if (subject.IsNone)
            {
                _lastStatus = "IssueOne: empty subject";
                _lastSuccessCount = 0;
                _lastFailureCount = 1;
                return 0;
            }

            if (kind == PlayerCommandKind.Help ||
                kind == PlayerCommandKind.Slight ||
                kind == PlayerCommandKind.Recruit)
            {
                // 社交仍需二人：subject 为行动者，目标优先选中里的另一个。
                return IssueSocialAs(subject, kind) ? 1 : 0;
            }

            return IssueTo(new[] { subject }, kind, ResolveDuration(kind, durationTicks));
        }

        bool IssueSocialAs(EntityId actor, PlayerCommandKind kind)
        {
            _lastSuccessCount = 0;
            _lastFailureCount = 0;
            if (_session == null || !_session.IsInitialized || _session.Port == null || actor.IsNone)
            {
                _lastStatus = "Session／Port not ready";
                return false;
            }

            EntityId target = EntityId.None;
            if (selectionController != null)
            {
                var selected = selectionController.State.SelectedIds;
                for (var i = 0; i < selected.Count; i++)
                {
                    if (!selected[i].IsNone && selected[i] != actor)
                    {
                        target = selected[i];
                        break;
                    }
                }
            }

            if (target.IsNone)
            {
                _lastFailureCount = 1;
                _lastStatus = "帮助需要再点选一个目标（当前面板角色为行动者）";
                return false;
            }

            var result = _session.Port.Submit(new PlayerCommandRequest(actor, kind, 1, target));
            if (result.IsSuccess)
            {
                _lastSuccessCount = 1;
                _lastStatus = "kind=" + kind + " actor=" + actor.Value + " target=" + target.Value + " ok";
                return true;
            }

            _lastFailureCount = 1;
            _lastStatus = "kind=" + kind + " FAIL " + FormatError(result);
            return false;
        }

        public int IssueExploreOne(EntityId subject)
        {
            _lastSuccessCount = 0;
            _lastFailureCount = 0;
            if (_session?.Port == null || subject.IsNone)
            {
                _lastStatus = "IssueExploreOne: not ready";
                return 0;
            }

            _session.Loop.StopSubject(subject);
            var result = _session.Port.Submit(
                new PlayerCommandRequest(subject, PlayerCommandKind.Explore, 1));
            if (result.IsSuccess)
            {
                _lastSuccessCount = 1;
                _lastStatus = "Explore ok entity=" + subject.Value;
            }
            else
            {
                _lastFailureCount = 1;
                _lastStatus = "Explore FAIL " + FormatError(result);
            }

            return _lastSuccessCount;
        }

        /// <summary>
        /// Resolve active ContentEvent choice for the focus party member (or first character).
        /// </summary>
        public bool ResolveContentChoice(string choiceId)
        {
            _lastSuccessCount = 0;
            _lastFailureCount = 0;

            if (_session == null || !_session.IsInitialized || _session.Port == null)
            {
                _lastStatus = "Session／Port not ready";
                return false;
            }

            if (string.IsNullOrWhiteSpace(choiceId))
            {
                _lastStatus = "ChoiceId empty";
                _lastFailureCount = 1;
                return false;
            }

            if (!_session.World.ContentEvents.HasActive)
            {
                _lastStatus = "No active content event";
                _lastFailureCount = 1;
                return false;
            }

            var subject = ResolveContentSubject();
            if (subject.IsNone)
            {
                _lastStatus = "No subject for content choice";
                _lastFailureCount = 1;
                return false;
            }

            var result = _session.Port.Submit(
                new PlayerCommandRequest(
                    subject,
                    PlayerCommandKind.ResolveContentChoice,
                    1,
                    EntityId.None,
                    WorkRoleKind.None,
                    null,
                    choiceId.Trim(),
                    null));
            if (result.IsSuccess)
            {
                _lastSuccessCount = 1;
                _lastStatus = "ResolveContentChoice ok choice=" + choiceId + " subject=" + subject.Value;
                return true;
            }

            _lastFailureCount = 1;
            _lastStatus = "ResolveContentChoice FAIL " + FormatError(result);
            Debug.LogWarning("[HostCommand] " + _lastStatus, this);
            return false;
        }

        /// <summary>任务日志：接取／领奖／放弃。</summary>
        public bool SubmitQuestCommand(PlayerCommandKind kind, string questId)
        {
            _lastSuccessCount = 0;
            _lastFailureCount = 0;

            if (kind != PlayerCommandKind.StartQuest &&
                kind != PlayerCommandKind.ClaimQuestRewards &&
                kind != PlayerCommandKind.AbandonQuest)
            {
                _lastStatus = "Not a quest command: " + kind;
                _lastFailureCount = 1;
                return false;
            }

            if (_session == null || !_session.IsInitialized || _session.Port == null)
            {
                _lastStatus = "Session／Port not ready";
                _lastFailureCount = 1;
                return false;
            }

            if (string.IsNullOrWhiteSpace(questId))
            {
                _lastStatus = "QuestId empty";
                _lastFailureCount = 1;
                return false;
            }

            var subject = ResolveContentSubject();
            if (subject.IsNone)
            {
                _lastStatus = "No subject for quest command";
                _lastFailureCount = 1;
                return false;
            }

            var result = _session.Port.Submit(
                new PlayerCommandRequest(
                    subject,
                    kind,
                    1,
                    EntityId.None,
                    WorkRoleKind.None,
                    null,
                    null,
                    questId.Trim()));
            if (result.IsSuccess)
            {
                _lastSuccessCount = 1;
                _lastStatus = kind + " ok quest=" + questId + " subject=" + subject.Value;
                return true;
            }

            _lastFailureCount = 1;
            _lastStatus = kind + " FAIL " + FormatError(result);
            Debug.LogWarning("[HostCommand] " + _lastStatus, this);
            return false;
        }

        EntityId ResolveContentSubject()
        {
            if (selectionController != null)
            {
                var selected = selectionController.State.SelectedIds;
                for (var i = 0; i < selected.Count; i++)
                {
                    var id = selected[i];
                    if (id.IsNone)
                        continue;
                    for (var c = 0; c < _session.CharacterIds.Count; c++)
                    {
                        if (_session.CharacterIds[c] == id)
                            return id;
                    }
                }
            }

            return _session.CharacterIds.Count > 0 ? _session.CharacterIds[0] : EntityId.None;
        }

        /// <summary>
        /// Social: Actor = first Character in selection; Target = first non-Actor.
        /// </summary>
        public bool IssueSocial(PlayerCommandKind kind)
        {
            _lastSuccessCount = 0;
            _lastFailureCount = 0;

            if (_session == null || !_session.IsInitialized || _session.Port == null)
            {
                _lastStatus = "Session／Port not ready";
                return false;
            }

            if (kind != PlayerCommandKind.Help &&
                kind != PlayerCommandKind.Slight &&
                kind != PlayerCommandKind.Recruit)
            {
                _lastStatus = "Not a social kind: " + kind;
                return false;
            }

            if (selectionController == null)
            {
                _lastStatus = "No selection controller";
                return false;
            }

            if (!TryResolveSocialPair(
                    _session,
                    selectionController.State.SelectedIds,
                    out var actor,
                    out var target,
                    out var resolveError))
            {
                _lastFailureCount = 1;
                _lastStatus = resolveError;
                return false;
            }

            var result = _session.Port.Submit(new PlayerCommandRequest(actor, kind, 1, target));
            if (result.IsSuccess)
            {
                _lastSuccessCount = 1;
                _lastStatus = "kind=" + kind + " actor=" + actor.Value + " target=" + target.Value + " ok";
                return true;
            }

            _lastFailureCount = 1;
            _lastStatus = "kind=" + kind + " actor=" + actor.Value + " target=" + target.Value +
                          " FAIL " + FormatError(result);
            Debug.LogWarning("[HostCommand] Social failed: " + _lastStatus, this);
            return false;
        }

        public int IssueTo(
            IReadOnlyList<EntityId> targets,
            PlayerCommandKind kind,
            ulong durationTicks = DefaultDurationTicks)
        {
            _lastSuccessCount = 0;
            _lastFailureCount = 0;

            if (_session == null || !_session.IsInitialized || _session.Port == null)
            {
                _lastStatus = "Session／Port not ready";
                return 0;
            }

            if (targets == null || targets.Count == 0)
            {
                _lastStatus = "Empty selection";
                return 0;
            }

            var utility = kind == PlayerCommandKind.Stop || kind == PlayerCommandKind.UseConcealGrass;
            if (!utility && durationTicks == 0)
            {
                _lastStatus = "DurationTicks must be > 0";
                return 0;
            }

            var allowed = BuildAllowedSet(_session.CharacterIds);
            for (var i = 0; i < targets.Count; i++)
            {
                var id = targets[i];
                if (id.IsNone || !allowed.Contains(id.Value))
                {
                    _lastFailureCount++;
                    Debug.LogWarning("[HostCommand] Skip non-controllable entity: " + id, this);
                    continue;
                }

                // 新指令打断待命 Wait／旧行动，避免「点了劳动却排在 Wait 后面」
                if (!utility)
                    _session.Loop.StopSubject(id);

                // 玩家改令：取消尚未走出场景的宏观「未出行」
                hostBootstrap?.WorldTravelDeparture?.NotifyPlayerOverride(id);

                var ritual = hostBootstrap != null ? hostBootstrap.BreakthroughRitual : null;
                if (ritual != null && ritual.TryHandleCommandDuringChannel(id, kind))
                {
                    // Stop＝取消蓄势；其他指令＝打断失败。Stop 仍继续清行动；非 Stop 则不再下发新指令。
                    if (kind != PlayerCommandKind.Stop)
                    {
                        _lastFailureCount++;
                        continue;
                    }
                }

                if (kind == PlayerCommandKind.Stop)
                    workLoop?.StopLoop(id);
                else if (kind != PlayerCommandKind.Labor && kind != PlayerCommandKind.Cultivate)
                    workLoop?.StopLoop(id);

                if (kind == PlayerCommandKind.Stop)
                    NotifyMeleeDisengage(id);

                var result = _session.Port.Submit(new PlayerCommandRequest(id, kind, durationTicks));
                if (result.IsSuccess)
                {
                    _lastSuccessCount++;
                    NotifyFeedback(id, kind);
                    if (kind == PlayerCommandKind.Labor || kind == PlayerCommandKind.Cultivate)
                        workLoop?.StartLoop(id, kind);
                }
                else
                {
                    _lastFailureCount++;
                    Debug.LogWarning(
                        "[HostCommand] Submit failed kind=" + kind + " entity=" + id + " err=" + FormatError(result),
                        this);
                }
            }

            _lastStatus = "kind=" + kind +
                          " ok=" + _lastSuccessCount +
                          " fail=" + _lastFailureCount;
            return _lastSuccessCount;
        }

        void NotifyMeleeDisengage(EntityId id)
        {
            var melee = hostBootstrap != null
                ? hostBootstrap.GetComponent<HostNpcMeleeAssault>()
                : GetComponent<HostNpcMeleeAssault>();
            melee?.DisengageIfInvolved(id);
        }

        public static bool TryResolveSocialPair(
            PlayableHostSession session,
            IReadOnlyList<EntityId> selection,
            out EntityId actor,
            out EntityId target,
            out string error)
        {
            actor = EntityId.None;
            target = EntityId.None;
            error = null;

            if (session == null || !session.IsInitialized || session.World == null)
            {
                error = "Session not ready";
                return false;
            }

            if (selection == null || selection.Count == 0)
            {
                error = "Select Character (actor) + target";
                return false;
            }

            var controllable = BuildAllowedSet(session.CharacterIds);
            for (var i = 0; i < selection.Count; i++)
            {
                var id = selection[i];
                if (!id.IsNone && controllable.Contains(id.Value))
                {
                    actor = id;
                    break;
                }
            }

            if (actor.IsNone)
            {
                error = "Need a Character actor in selection";
                return false;
            }

            for (var i = 0; i < selection.Count; i++)
            {
                var id = selection[i];
                if (id.IsNone || id == actor)
                    continue;
                if (!session.World.Entities.TryGet(id, out _))
                    continue;
                target = id;
                break;
            }

            if (target.IsNone)
            {
                error = "Need a distinct target (Npc or other) in selection";
                return false;
            }

            return true;
        }

        void NotifyFeedback(EntityId id, PlayerCommandKind kind)
        {
            if (feedbackOverlay == null)
                return;
            string text = null;
            var color = Color.white;
            switch (kind)
            {
                case PlayerCommandKind.Stop:
                    text = "已停下";
                    color = new Color(1f, 0.85f, 0.4f);
                    break;
                case PlayerCommandKind.Labor:
                    text = "开工";
                    color = new Color(0.7f, 1f, 0.5f);
                    break;
                case PlayerCommandKind.Cultivate:
                    text = "入定";
                    color = new Color(0.5f, 0.9f, 1f);
                    break;
                case PlayerCommandKind.UseConcealGrass:
                    text = "敛息";
                    color = new Color(0.6f, 1f, 0.8f);
                    break;
            }

            if (text != null)
                feedbackOverlay.SpawnAtEntity(viewSpawner, id, text, color);
        }

        static HashSet<ulong> BuildAllowedSet(IReadOnlyList<EntityId> characterIds)
        {
            var set = new HashSet<ulong>();
            if (characterIds == null)
                return set;
            for (var i = 0; i < characterIds.Count; i++)
            {
                if (!characterIds[i].IsNone)
                    set.Add(characterIds[i].Value);
            }

            return set;
        }

        static string FormatError(Result result) =>
            result.IsFailure ? result.Error.ToString() : "ok";
    }
}
