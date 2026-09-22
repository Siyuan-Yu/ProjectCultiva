using System;
using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Serialized compatibility name. Modern runtime responsibility is CharacterEncounter
    /// presentation only.
    /// </summary>
    public sealed class HostStrategicInterruptPresenter : MonoBehaviour
    {
        const string ManualBattleReportPauseOwner = "ManualBattleReport";
        [SerializeField] PlayableHostBootstrap bootstrap;

        readonly List<EntityId> _friendlyPreview = new List<EntityId>(8);
        readonly List<EntityId> _enemyPreview = new List<EntityId>(8);
        readonly List<ButtonSpec> _actions = new List<ButtonSpec>(2);
        ManualBattleReport _manualBattleReport;
        PlayableHostSession _reportOwnerSession;
        bool _reportHolding;
        Vector2 _reportScroll;
        Vector2 _offerScroll;
        string _toast = string.Empty;
        float _toastUntil;
        Texture2D _pixel;
        GUIStyle _title;
        GUIStyle _body;
        bool _stylesReady;

        static readonly Color Parchment = new Color(0.90f, 0.84f, 0.72f, 0.96f);
        static readonly Color ParchmentDark = new Color(0.72f, 0.62f, 0.48f, 1f);

        public bool HasBlockingInterrupt
        {
            get
            {
                if (_manualBattleReport != null || _reportHolding)
                    return true;
                var encounter = ResolveCoordinator();
                if (encounter == null)
                    return false;
                switch (encounter.Phase)
                {
                    case HostCharacterEncounter.PresentationPhase.Pending:
                    case HostCharacterEncounter.PresentationPhase.Preparing:
                    case HostCharacterEncounter.PresentationPhase.ReadyToCommit:
                    case HostCharacterEncounter.PresentationPhase.ReadyToStart:
                    case HostCharacterEncounter.PresentationPhase.Report:
                    case HostCharacterEncounter.PresentationPhase.Failed:
                        return true;
                    default:
                        return false;
                }
            }
        }

        public void Bind(PlayableHostBootstrap host) => bootstrap = host;

        public void ClearSessionState()
        {
            ReleaseManualBattleReportPause();
            _manualBattleReport = null;
            _reportScroll = Vector2.zero;
            _offerScroll = Vector2.zero;
            _toast = string.Empty;
            _toastUntil = 0f;
        }

        public void ShowTransientToast(string message)
        {
            _toast = message ?? string.Empty;
            _toastUntil = Time.unscaledTime + 4f;
        }

        void OnDisable() => ReleaseManualBattleReportPause();

        void OnGUI()
        {
            var session = bootstrap != null ? bootstrap.Session : null;
            if (session == null || !session.IsInitialized || session.World?.Strategic == null)
                return;
            EnsureStyles();
            DrawToast();

            var coordinator = ResolveCoordinator();
            SyncCommittedReport(session);
            if (_manualBattleReport != null)
            {
                DrawManualBattleReport(session, coordinator);
                return;
            }
            if (coordinator == null)
                return;
            switch (coordinator.Phase)
            {
                case HostCharacterEncounter.PresentationPhase.Pending:
                case HostCharacterEncounter.PresentationPhase.Preparing:
                case HostCharacterEncounter.PresentationPhase.ReadyToCommit:
                case HostCharacterEncounter.PresentationPhase.Failed:
                    DrawCharacterEncounterOffer(session, coordinator);
                    break;
                case HostCharacterEncounter.PresentationPhase.ReadyToStart:
                    DrawCharacterEncounterStartBar(coordinator);
                    break;
                case HostCharacterEncounter.PresentationPhase.ReadyToEnd:
                    DrawCharacterEncounterEndBar(coordinator);
                    break;
            }
        }

        HostCharacterEncounter ResolveCoordinator() =>
            bootstrap != null ? bootstrap.GetComponent<HostCharacterEncounter>() : null;

        void SyncCommittedReport(PlayableHostSession session)
        {
            var state = session.World.Strategic.CharacterEncounter;
            if (state == null || state.Phase != CharacterEncounterPhase.Committed)
                return;
            var report = session.World.Strategic.ManualBattleSettlement.CommittedReport;
            if (report == null)
                return;
            if (!ReferenceEquals(_manualBattleReport, report))
            {
                _manualBattleReport = report;
                _reportScroll = Vector2.zero;
            }
            AcquireManualBattleReportPause(session);
        }

        void AcquireManualBattleReportPause(PlayableHostSession session)
        {
            if (_reportHolding || session == null)
                return;
            session.AcquireModalPause(ManualBattleReportPauseOwner);
            HostInputGate.Acquire(ManualBattleReportPauseOwner);
            _reportOwnerSession = session;
            _reportHolding = true;
        }

        void ReleaseManualBattleReportPause()
        {
            _reportOwnerSession?.ReleaseModalPause(ManualBattleReportPauseOwner);
            HostInputGate.Release(ManualBattleReportPauseOwner);
            _reportOwnerSession = null;
            _reportHolding = false;
        }

        void DrawCharacterEncounterOffer(PlayableHostSession session, HostCharacterEncounter coordinator)
        {
            GUI.depth = -100;
            var box = new Rect(Screen.width * .5f - 260f, Screen.height * .5f - 235f, 520f, 470f);
            DrawDim();
            Fill(box, Parchment);
            DrawFrame(box, ParchmentDark);
            HostUiHitTest.Block(box);
            GUI.Label(new Rect(box.x + 16f, box.y + 12f, box.width - 32f, 26f), "人物遭遇", _title);

            var phase = coordinator.Phase;
            var preparing = phase == HostCharacterEncounter.PresentationPhase.Preparing ||
                            phase == HostCharacterEncounter.PresentationPhase.ReadyToCommit;
            GUI.Label(new Rect(box.x + 16f, box.y + 42f, box.width - 32f, 22f),
                preparing ? coordinator.Progress : "初始双方仅为当前两支小队中的存活成员。", _body);

            _friendlyPreview.Clear();
            _enemyPreview.Clear();
            coordinator.CopyPreviewTo(_friendlyPreview, _enemyPreview);
            var friendlyPower = SumCharacterPower(session.World, _friendlyPreview);
            var enemyPower = SumCharacterPower(session.World, _enemyPreview);
            GUI.Label(new Rect(box.x + 16f, box.y + 68f, box.width - 32f, 22f),
                "我方小队 " + _friendlyPreview.Count + " 人 · 战力 " + friendlyPower, _body);
            GUI.Label(new Rect(box.x + 16f, box.y + 90f, box.width - 32f, 22f),
                "敌方小队 " + _enemyPreview.Count + " 人 · 战力 " + enemyPower, _body);
            DrawPowerBar(new Rect(box.x + 16f, box.y + 118f, box.width - 32f, 14f),
                friendlyPower, enemyPower);

            var viewport = new Rect(box.x + 16f, box.y + 142f, box.width - 32f, box.height - 254f);
            var content = new Rect(0f, 0f, viewport.width - 20f,
                Mathf.Max(viewport.height, (_friendlyPreview.Count + _enemyPreview.Count) * 20f + 24f));
            _offerScroll = GUI.BeginScrollView(viewport, _offerScroll, content);
            var y = 4f;
            GUI.Label(new Rect(4f, y, content.width, 20f), "参战人物", _body);
            y += 20f;
            DrawCharacterPreviewRows(session.World, _friendlyPreview, "我方", 8f, ref y, content.width - 8f);
            DrawCharacterPreviewRows(session.World, _enemyPreview, "敌方", 8f, ref y, content.width - 8f);
            GUI.EndScrollView();

            if (!string.IsNullOrEmpty(coordinator.Failure))
                GUI.Label(new Rect(box.x + 16f, box.yMax - 104f, box.width - 32f, 38f),
                    "准备失败：" + coordinator.Failure, _body);
            else if (preparing)
                GUI.Label(new Rect(box.x + 16f, box.yMax - 104f, box.width - 32f, 38f),
                    "正在按冻结范围准备真实地形与导航；WorldTick 保持冻结。", _body);

            _actions.Clear();
            if (phase == HostCharacterEncounter.PresentationPhase.Pending)
                _actions.Add(new ButtonSpec("手动战斗", coordinator.BeginConfirmed));
            else if (phase == HostCharacterEncounter.PresentationPhase.Failed)
                _actions.Add(new ButtonSpec(coordinator.IsRestoring ? "重试恢复" : "重试", coordinator.BeginConfirmed));
            if (coordinator.CanCancel)
                _actions.Add(new ButtonSpec("取消主动攻击", coordinator.CancelPending));
            DrawActions(box, box.yMax - 48f, _actions);
        }

        void DrawCharacterEncounterStartBar(HostCharacterEncounter coordinator)
        {
            GUI.depth = -95;
            const float width = 420f;
            const float height = 92f;
            var box = new Rect((Screen.width - width) * .5f, 82f, width, height);
            Fill(box, Parchment);
            DrawFrame(box, ParchmentDark);
            HostUiHitTest.Block(box);
            GUI.Label(new Rect(box.x + 14f, box.y + 10f, box.width - 28f, 26f),
                coordinator.ReadyToStartIsRestore
                    ? "战场已恢复 · 当前全场暂停"
                    : "战场已就绪 · 当前全场暂停", _title);
            if (!string.IsNullOrEmpty(coordinator.Failure))
                GUI.Label(new Rect(box.x + 14f, box.y + 36f, box.width - 152f, 42f),
                    coordinator.Failure, _body);
            if (GUI.Button(new Rect(box.xMax - 132f, box.yMax - 42f, 116f, 32f), "开始战斗") &&
                !coordinator.StartBattle())
                ShowTransientToast(coordinator.Failure);
        }

        void DrawCharacterEncounterEndBar(HostCharacterEncounter coordinator)
        {
            GUI.depth = -40;
            const float width = 420f;
            const float height = 116f;
            var box = new Rect(Screen.width - width - 16f, Screen.height - height - 72f, width, height);
            Fill(box, Parchment);
            DrawFrame(box, ParchmentDark);
            HostUiHitTest.Block(box);
            GUI.Label(new Rect(box.x + 10f, box.y + 6f, box.width - 140f, 52f),
                "本场已可结束。结束后生成战报并返回原位置。\n仍可补刀、查看角色或进行战场交互。", _body);
            if (GUI.Button(new Rect(box.xMax - 128f, box.yMax - 40f, 116f, 32f), "结束战斗") &&
                !coordinator.TryFinishBattle())
                ShowTransientToast("无法结束战斗：" + coordinator.Failure);
        }

        void DrawManualBattleReport(PlayableHostSession session, HostCharacterEncounter coordinator)
        {
            var report = _manualBattleReport;
            if (report == null)
                return;
            GUI.depth = -100;
            DrawDim();
            var width = Mathf.Min(680f, Screen.width - 40f);
            var height = Mathf.Min(560f, Screen.height - 40f);
            var box = new Rect((Screen.width - width) * .5f, (Screen.height - height) * .5f, width, height);
            Fill(box, Parchment);
            DrawFrame(box, ParchmentDark);
            HostUiHitTest.Block(box);
            GUI.Label(new Rect(box.x + 18f, box.y + 14f, box.width - 36f, 30f),
                report.PlayerWon ? "战斗胜利" : "战斗失败", _title);
            GUI.Label(new Rect(box.x + 18f, box.y + 48f, box.width - 36f, 42f),
                string.IsNullOrWhiteSpace(report.ResultReason)
                    ? report.PlayerWon ? "本次有效敌方已失去战斗能力。" : "我方参战者已失去战斗能力。"
                    : report.ResultReason, _body);
            GUI.Label(new Rect(box.x + 18f, box.y + 92f, box.width - 36f, 46f),
                BuildReportSummary(report), _body);

            var viewport = new Rect(box.x + 18f, box.y + 142f, box.width - 36f, box.height - 202f);
            var content = new Rect(0f, 0f, viewport.width - 20f,
                Mathf.Max(viewport.height, report.Participants.Count * 54f + 8f));
            _reportScroll = GUI.BeginScrollView(viewport, _reportScroll, content);
            var y = 4f;
            for (var i = 0; i < report.Participants.Count; i++)
            {
                var row = report.Participants[i];
                var side = row.Side == ActualBattleParticipantSide.Friendly ? "我方" : "敌方";
                GUI.Label(new Rect(4f, y, content.width - 8f, 22f), side + " · " + row.Name, _body);
                GUI.Label(new Rect(18f, y + 23f, content.width - 22f, 24f),
                    FormatReportState(row.EntryCondition, row.EntryHpAvailable, row.EntryHp, row.EntryMaxHp) +
                    "  →  " +
                    FormatReportState(row.FinalCondition, row.FinalHpAvailable, row.FinalHp, row.FinalMaxHp) +
                    (row.ChangedDuringBattle ? "（本场发生变化）" : "（进场状态未变）"), _body);
                y += 54f;
            }
            GUI.EndScrollView();
            if (GUI.Button(new Rect(box.x + 18f, box.yMax - 46f, box.width - 36f, 32f), "继续"))
            {
                if (session.World.Strategic.CharacterEncounter?.Phase == CharacterEncounterPhase.Committed)
                {
                    if (coordinator == null)
                    {
                        ShowTransientToast("无法关闭战报：人物遭遇协调器不可用。");
                        return;
                    }
                    coordinator.CloseReport();
                    if (session.World.Strategic.CharacterEncounter?.Phase == CharacterEncounterPhase.Committed)
                    {
                        ShowTransientToast("无法关闭战报，请保留现场并联系开发者。");
                        return;
                    }
                }
                _manualBattleReport = null;
                _reportScroll = Vector2.zero;
                ReleaseManualBattleReportPause();
            }
        }

        void DrawToast()
        {
            if (string.IsNullOrEmpty(_toast) || Time.unscaledTime > _toastUntil)
                return;
            var rect = new Rect(Screen.width * .5f - 220f, 72f, 440f, 32f);
            var previous = GUI.color;
            GUI.color = new Color(0.1f, 0.12f, 0.14f, 0.92f);
            GUI.DrawTexture(rect, _pixel);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 12f, rect.y + 6f, rect.width - 24f, 22f), _toast, _body);
            GUI.color = previous;
            HostUiHitTest.Block(rect);
        }

        static int SumCharacterPower(SimulationWorld world, List<EntityId> ids)
        {
            var sum = 0;
            for (var i = 0; i < ids.Count; i++)
                sum += CombatPowerCalculator.ForEntity(world, ids[i]);
            return sum;
        }

        void DrawCharacterPreviewRows(SimulationWorld world, List<EntityId> ids,
            string side, float x, ref float y, float width)
        {
            for (var i = 0; i < ids.Count; i++)
            {
                GUI.Label(new Rect(x, y, width, 18f),
                    side + " · " + ResolveCharacterLabel(world, ids[i]) +
                    "  战力 " + CombatPowerCalculator.ForEntity(world, ids[i]), _body);
                y += 18f;
            }
        }

        static string ResolveCharacterLabel(SimulationWorld world, EntityId id)
        {
            if (id.IsNone || world?.Entities == null ||
                !world.Entities.TryGet(id, out var entity) || entity == null ||
                string.IsNullOrWhiteSpace(entity.DisplayName))
                return id.ToString();
            return entity.DisplayName;
        }

        void DrawPowerBar(Rect bar, int friendly, int enemy)
        {
            var previous = GUI.color;
            var width = bar.width * friendly / Mathf.Max(1f, friendly + enemy);
            GUI.color = new Color(.35f, .72f, .42f, .9f);
            GUI.DrawTexture(new Rect(bar.x, bar.y, width, bar.height), _pixel);
            GUI.color = new Color(.78f, .32f, .28f, .9f);
            GUI.DrawTexture(new Rect(bar.x + width, bar.y, bar.width - width, bar.height), _pixel);
            GUI.color = previous;
        }

        static string BuildReportSummary(ManualBattleReport report) =>
            BuildSideReportSummary(report, ActualBattleParticipantSide.Friendly, "我方") + "\n" +
            BuildSideReportSummary(report, ActualBattleParticipantSide.Enemy, "敌方");

        static string BuildSideReportSummary(ManualBattleReport report,
            ActualBattleParticipantSide side, string label)
        {
            var unavailable = report.Count(side, ManualBattleReportCondition.Removed) +
                              report.Count(side, ManualBattleReportCondition.MissingOrUnavailable);
            return label + " " + report.CountSide(side) + " 人：完好 " +
                   report.Count(side, ManualBattleReportCondition.Intact) + " / 负伤 " +
                   report.Count(side, ManualBattleReportCondition.Injured) + " / 重伤 " +
                   report.Count(side, ManualBattleReportCondition.SeriouslyInjured) + " / 弥留 " +
                   report.Count(side, ManualBattleReportCondition.Incapacitated) + " / 阵亡 " +
                   report.Count(side, ManualBattleReportCondition.Dead) + " / 被俘 " +
                   report.Count(side, ManualBattleReportCondition.Captured) + " / 不可用 " + unavailable;
        }

        static string FormatReportState(ManualBattleReportCondition condition,
            bool hpAvailable, int hp, int maxHp)
        {
            string label;
            switch (condition)
            {
                case ManualBattleReportCondition.Intact: label = "完好"; break;
                case ManualBattleReportCondition.Injured: label = "负伤"; break;
                case ManualBattleReportCondition.SeriouslyInjured: label = "重伤"; break;
                case ManualBattleReportCondition.Incapacitated: label = "弥留"; break;
                case ManualBattleReportCondition.Dead: label = "阵亡"; break;
                case ManualBattleReportCondition.Captured: label = "被俘"; break;
                case ManualBattleReportCondition.Removed: label = "已移除"; break;
                default: label = "状态不可用"; break;
            }
            return hpAvailable ? label + " HP " + hp + "/" + maxHp : label;
        }

        void DrawActions(Rect box, float y, List<ButtonSpec> specs)
        {
            if (specs.Count == 0)
                return;
            var slotWidth = (box.width - 40f) / specs.Count;
            for (var i = 0; i < specs.Count; i++)
                if (GUI.Button(new Rect(box.x + 16f + slotWidth * i, y, slotWidth, 32f), specs[i].Label))
                    specs[i].Invoke();
        }

        void DrawDim()
        {
            var dim = new Rect(0f, 0f, Screen.width, Screen.height);
            var previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(dim, _pixel);
            GUI.color = previous;
            HostUiHitTest.Block(dim);
        }

        void Fill(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _pixel);
            GUI.color = previous;
        }

        void DrawFrame(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 2f), _pixel);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), _pixel);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 2f, rect.height), _pixel);
            GUI.DrawTexture(new Rect(rect.xMax - 2f, rect.y, 2f, rect.height), _pixel);
            GUI.color = previous;
        }

        void EnsureStyles()
        {
            if (_stylesReady)
                return;
            _pixel = new Texture2D(1, 1);
            _pixel.SetPixel(0, 0, Color.white);
            _pixel.Apply();
            _title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft
            };
            _body = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft
            };
            _stylesReady = true;
        }

        readonly struct ButtonSpec
        {
            public ButtonSpec(string label, Action action)
            {
                Label = label;
                Action = action;
            }
            public string Label { get; }
            readonly Action Action;
            public void Invoke() => Action?.Invoke();
        }
    }
}
