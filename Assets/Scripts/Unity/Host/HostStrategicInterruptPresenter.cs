using System;
using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>战略打断：接战 BattleOffer + 到站 ArrivalNotice。</summary>
    public sealed class HostStrategicInterruptPresenter : MonoBehaviour
    {
        const string InterruptPauseOwner = "StrategicInterrupt";
        const string ManualBattleReportPauseOwner = "ManualBattleReport";
        [SerializeField] PlayableHostBootstrap bootstrap;

        bool _holding;
        bool _executeOnWin;
        string _toast = string.Empty;
        double _toastUntil;
        Texture2D _px;
        GUIStyle _title;
        GUIStyle _body;
        bool _stylesReady;
        bool _reportHolding;
        PlayableHostSession _reportOwnerSession;
        bool _settlementInProgress;
        Vector2 _reportScroll;
        Vector2 _offerScroll;
        ManualBattleReport _manualBattleReport;
        string _lastSettlementFailureKey = string.Empty;

        public bool HasManualBattleReport => _manualBattleReport != null;

        static readonly Color Parchment = new Color(0.90f, 0.84f, 0.72f, 0.96f);
        static readonly Color ParchmentDark = new Color(0.72f, 0.62f, 0.48f, 1f);

        public bool HasBlockingInterrupt
        {
            get
            {
                var session = bootstrap != null ? bootstrap.Session : null;
                if (session == null || !session.IsInitialized || session.World?.Strategic == null)
                    return false;
                return session.World.Strategic.HasBlockingInterrupt;
            }
        }

        public void Bind(PlayableHostBootstrap host) => bootstrap = host;

        public void ClearSessionState()
        {
            ReleaseInterruptPause();
            ReleaseManualBattleReportPause();
            _holding = false;
            _executeOnWin = false;
            _settlementInProgress = false;
            _manualBattleReport = null;
            var world = bootstrap != null ? bootstrap.Session?.World : null;
            world?.Strategic?.ManualBattleSettlement?.Clear();
            _lastSettlementFailureKey = string.Empty;
            _reportScroll = Vector2.zero;
            _toast = string.Empty;
            _toastUntil = 0;
        }

        void Update() => SyncPause();

        void OnDisable()
        {
            ReleaseInterruptPause();
            ReleaseManualBattleReportPause();
        }

        void SyncPause()
        {
            var session = bootstrap != null ? bootstrap.Session : null;
            if (session == null || !session.IsInitialized)
            {
                ClearSessionState();
                return;
            }

            var world = session.World;
            // CharacterEncounter uses the same presenter and parchment workflow. Its own
            // coordinator owns only the request/build modal while this presenter owns drawing.
            if (world.Strategic.CharacterEncounter != null)
            {
                ReleaseInterruptPause();
                SyncClockFreezePresentation(session);
                return;
            }
            SyncClockFreezePresentation(session);

            if (HasBlockingInterrupt)
            {
                if (!_holding)
                {
                    session.AcquireModalPause(InterruptPauseOwner);
                    _holding = true;
                }
            }
            else if (_holding)
            {
                ReleaseInterruptPause();
            }

            // 清场或我方全倒 → PostBattle（事件漏同步时每帧兜底）
            if (world?.Strategic != null &&
                world.Strategic.ClockFreeze.Reason == StrategicClockFreezeReason.ManualEncounter)
            {
                StrategicEncounterSpawner.TryMarkFieldCleared(world);
                StrategicEncounterResolveService.TryEnterPostBattleFromManual(world);
            }
        }

        void SyncClockFreezePresentation(PlayableHostSession session)
        {
            var world = session.World;
            if (world?.Strategic == null)
                return;

            var freeze = world.Strategic.ClockFreeze;
            if (!freeze.IsWorldTickFrozen)
                return;

            var speed = bootstrap != null ? bootstrap.EffectiveSpeedMultiplier() : 1;
            StrategicClockFreezeService.CaptureHostPresentationIfNeeded(
                world,
                session.ManualPaused,
                speed);
        }

        void ReleaseInterruptPause()
        {
            var session = bootstrap != null ? bootstrap.Session : null;
            session?.ReleaseModalPause(InterruptPauseOwner);
            _holding = false;
        }

        void AcquireManualBattleReportPause(PlayableHostSession session)
        {
            if (_reportHolding || session == null)
                return;
            session.AcquireModalPause(ManualBattleReportPauseOwner);
            HostInputGate.Acquire(ManualBattleReportPauseOwner);
            _reportHolding = true;
            _reportOwnerSession = session;
        }

        void ReleaseManualBattleReportPause()
        {
            _reportOwnerSession?.ReleaseModalPause(ManualBattleReportPauseOwner);
            HostInputGate.Release(ManualBattleReportPauseOwner);
            _reportHolding = false;
            _reportOwnerSession = null;
        }

        void OnGUI()
        {
            var session = bootstrap != null ? bootstrap.Session : null;
            if (session == null || !session.IsInitialized || session.World?.Strategic == null)
                return;

            EnsureStyles();
            DrawToast();

            var character = bootstrap.GetComponent<HostCharacterEncounter>();
            var characterState = session.World.Strategic.CharacterEncounter;
            if ((character != null && character.Phase != HostCharacterEncounter.PresentationPhase.None) || characterState != null)
            {
                if (characterState != null && characterState.Phase == CharacterEncounterPhase.Committed && _manualBattleReport == null)
                {
                    _manualBattleReport = session.World.Strategic.ManualBattleSettlement.CommittedReport;
                    if (_manualBattleReport != null) AcquireManualBattleReportPause(session);
                }
                if (_manualBattleReport != null) { DrawManualBattleReport(session); return; }
                if (character != null && character.Phase == HostCharacterEncounter.PresentationPhase.ReadyToStart)
                    DrawCharacterEncounterStartBar(session, character);
                else if (characterState != null && characterState.Phase == CharacterEncounterPhase.ReadyToEnd &&
                    character != null && !character.IsRestoring)
                    DrawManualPostBattleBar(session);
                else if (character != null && character.Phase != HostCharacterEncounter.PresentationPhase.Active)
                    DrawCharacterEncounterOffer(session, character);
                return;
            }

            if (_manualBattleReport != null)
            {
                DrawManualBattleReport(session);
                return;
            }

            // Offer／到站优先；战后只画非强制「结束战斗」条（可继续在场景里玩）
            if (HasBlockingInterrupt)
                GUI.depth = -90;

            var offer = session.World.Strategic.BattleOffer;
            if (offer != null && !offer.Resolved && !string.IsNullOrEmpty(offer.OfferId))
            {
                DrawBattleOffer(session, offer);
                return;
            }

            var arrival = session.World.Strategic.ArrivalNotice;
            if (arrival != null && !arrival.Resolved && !string.IsNullOrEmpty(arrival.NoticeId))
            {
                DrawArrivalNotice(session, arrival);
                return;
            }

            DrawPostBattleEndIfNeeded(session);
        }

        void DrawPostBattleEndIfNeeded(PlayableHostSession session)
        {
            var world = session.World;
            if (world?.Strategic == null)
                return;
            if (world.Strategic.ClockFreeze.Reason == StrategicClockFreezeReason.ManualEncounter)
                StrategicEncounterResolveService.TryEnterPostBattleFromManual(world);

            if (world.Strategic.ClockFreeze.Reason != StrategicClockFreezeReason.PostBattle)
                return;

            EnsureStyles();
            var auto = world.Strategic.Participants.IsAutoSettlement;
            if (auto)
                DrawAutoSettlementModal(session);
            else
                DrawManualPostBattleBar(session);
        }

        void DrawAutoSettlementModal(PlayableHostSession session)
        {
            var world = session.World;
            GUI.depth = -90;
            DrawDim();
            var box = new Rect(Screen.width * 0.5f - 240f, Screen.height * 0.5f - 170f, 480f, 340f);
            Fill(box, Parchment);
            DrawFrame(box, ParchmentDark);
            GUI.Label(
                new Rect(box.x + 16f, box.y + 16f, box.width - 32f, 28f),
                world.Strategic.Participants.PlayerWon ? "自动战斗 · 胜利" : "自动战斗 · 失利",
                _title);
            var summary = world.Strategic.Participants.LastBattleSummary;
            if (string.IsNullOrEmpty(summary))
                summary = world.Strategic.Participants.PlayerWon
                    ? "自动战斗胜利。"
                    : "自动战斗失利。";
            GUI.Label(
                new Rect(box.x + 16f, box.y + 52f, box.width - 32f, 72f),
                summary,
                _body);
            DrawBattleAftermathSection(world, new Rect(box.x + 16f, box.y + 128f, box.width - 32f, 140f));
            GUI.Label(
                new Rect(box.x + 16f, box.y + 272f, box.width - 32f, 24f),
                "确认后返回战略层并恢复时间。",
                _body);
            if (GUI.Button(new Rect(box.x + 16f, box.y + box.height - 48f, box.width - 32f, 32f), "确认结算"))
                ConfirmEndBattle(session);
        }

        void DrawManualPostBattleBar(PlayableHostSession session)
        {
            var world = session.World;
            if (_manualBattleReport != null) AcquireManualBattleReportPause(session);
            var character = world.Strategic.CharacterEncounter != null
                ? bootstrap.GetComponent<HostCharacterEncounter>() : null;
            GUI.depth = -40;
            var barW = 420f;
            var barH = 116f;
            var box = new Rect(Screen.width - barW - 16f, Screen.height - barH - 72f, barW, barH);
            Fill(box, Parchment);
            DrawFrame(box, ParchmentDark);
            var summary = character != null ? "本场已可结束。结束后生成战报并返回原位置。" : world.Strategic.Participants.LastBattleSummary;
            if (string.IsNullOrEmpty(summary))
                summary = "敌军已清空。可补刀／交互；点结束才结算。";
            GUI.Label(new Rect(box.x + 10f, box.y + 6f, box.width - 140f, 52f), summary, _body);
            if (GUI.Button(new Rect(box.xMax - 128f, box.y + box.height - 40f, 116f, 32f), "结束战斗"))
            {
                if (character == null) ConfirmEndBattle(session);
                else if (!character.TryFinishBattle()) ShowToast("无法结束战斗：" + character.Failure);
            }
        }

        void DrawManualBattleReport(PlayableHostSession session)
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
            GUI.Label(new Rect(box.x + 18f, box.y + 14f, box.width - 36f, 30f),
                report.PlayerWon ? "战斗胜利" : "战斗失败", _title);
            var reason = string.IsNullOrWhiteSpace(report.ResultReason)
                ? (report.PlayerWon ? "本次有效敌方已失去战斗能力。" : "我方参战者已失去战斗能力。")
                : report.ResultReason;
            GUI.Label(new Rect(box.x + 18f, box.y + 48f, box.width - 36f, 42f), reason, _body);
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
                GUI.Label(new Rect(4f, y, content.width - 8f, 22f),
                    side + " · " + row.Name, _body);
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
                var character = bootstrap != null ? bootstrap.GetComponent<HostCharacterEncounter>() : null;
                if (session.World?.Strategic?.CharacterEncounter?.Phase == CharacterEncounterPhase.Committed)
                    character?.CloseReport();
                else
                    session.World?.Strategic?.ManualBattleSettlement?.ClearOwned(report.OfferId);
                _manualBattleReport = null;
                _reportScroll = Vector2.zero;
                ReleaseManualBattleReportPause();
            }
        }

        static string BuildReportSummary(ManualBattleReport report)
        {
            return BuildSideReportSummary(report, ActualBattleParticipantSide.Friendly, "我方") + "\n" +
                   BuildSideReportSummary(report, ActualBattleParticipantSide.Enemy, "敌方");
        }

        static string BuildSideReportSummary(
            ManualBattleReport report, ActualBattleParticipantSide side, string label)
        {
            var unavailable = report.Count(side, ManualBattleReportCondition.Removed) +
                              report.Count(side, ManualBattleReportCondition.MissingOrUnavailable);
            return label + " " + report.CountSide(side) + " 人：完整 " +
                   report.Count(side, ManualBattleReportCondition.Intact) + " / 负伤 " +
                   report.Count(side, ManualBattleReportCondition.Injured) + " / 重伤 " +
                   report.Count(side, ManualBattleReportCondition.SeriouslyInjured) + " / 弥留 " +
                   report.Count(side, ManualBattleReportCondition.Incapacitated) + " / 阵亡 " +
                   report.Count(side, ManualBattleReportCondition.Dead) + " / 被俘 " +
                   report.Count(side, ManualBattleReportCondition.Captured) + " / 不可用 " + unavailable;
        }

        static string FormatReportState(
            ManualBattleReportCondition condition, bool hpAvailable, int hp, int maxHp)
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

        void DrawBattleAftermathSection(SimulationWorld world, Rect rect)
        {
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 20f), "Battle Aftermath [ACCEPTANCE]", _title);
            var report = StrategicAcceptanceInspector.BuildAftermathReport(world);
            var y = rect.y + 22f;
            y = DrawAftermathList(rect, y, "Captured:", report.Captured, world);
            y = DrawAftermathList(rect, y, "Escaped:", report.Escaped, world);
            y = DrawRetreatingList(rect, y, report.RetreatingArmies, world);
        }

        float DrawAftermathList(Rect rect, float y, string header, List<EntityId> ids, SimulationWorld world)
        {
            GUI.Label(new Rect(rect.x, y, rect.width, 18f), header, _body);
            y += 18f;
            if (ids == null || ids.Count == 0)
            {
                GUI.Label(new Rect(rect.x + 8f, y, rect.width - 8f, 16f), "None", _body);
                return y + 18f;
            }

            for (var i = 0; i < ids.Count; i++)
            {
                var label = ResolveCharacterLabel(world, ids[i]);
                GUI.Label(new Rect(rect.x + 8f, y, rect.width - 8f, 16f), "- " + label, _body);
                y += 16f;
            }

            return y + 2f;
        }

        float DrawRetreatingList(Rect rect, float y, List<RetreatingArmy> armies, SimulationWorld world)
        {
            GUI.Label(new Rect(rect.x, y, rect.width, 18f), "Retreating Army:", _body);
            y += 18f;
            if (armies == null || armies.Count == 0)
            {
                GUI.Label(new Rect(rect.x + 8f, y, rect.width - 8f, 16f), "None", _body);
                return y + 18f;
            }

            for (var i = 0; i < armies.Count; i++)
            {
                var army = armies[i];
                if (army == null)
                    continue;
                GUI.Label(new Rect(rect.x + 8f, y, rect.width - 8f, 16f),
                    "- " + army.RetreatingArmyId + " (" + army.FactionId + ", members=" +
                    army.MemberCharacterIds.Count + ")",
                    _body);
                y += 16f;
            }

            return y + 2f;
        }

        static string ResolveCharacterLabel(SimulationWorld world, EntityId id)
        {
            if (id.IsNone || world?.Entities == null || !world.Entities.TryGet(id, out var entity) || entity == null)
                return id.ToString();
            if (!string.IsNullOrWhiteSpace(entity.DisplayName))
                return entity.DisplayName;
            return id.ToString();
        }

        void ConfirmEndBattle(PlayableHostSession session)
        {
            var world = session.World;
            if (world?.Strategic == null || _settlementInProgress || _manualBattleReport != null)
                return;
            var settlement = world.Strategic.ManualBattleSettlement;
            if (settlement != null && settlement.IsCommitted)
            {
                _manualBattleReport = settlement.CommittedReport;
                AcquireManualBattleReportPause(session);
                return;
            }
            if (!TryEnsureManualSettlementIdentity(world, out var identityFailure))
            {
                LogManualSettlementFailureOnce(world, "Preflight", identityFailure);
                ShowToast("无法结束战斗：本场结算身份不完整，请保留现场并查看诊断日志。");
                return;
            }
            _settlementInProgress = true;
            var freeze = world.Strategic.ClockFreeze;
            var savedSpeed = freeze.HasSavedHostPresentation
                ? freeze.SavedSpeedMultiplier
                : (bootstrap != null ? bootstrap.EffectiveSpeedMultiplier() : 1);
            // Phase 5S：Resolve 前 capture（FinishOfferResolution 会清 Participants / IsAutoSettlement）。
            var completionKind = world.Strategic.Participants.LocalMapResolutionKind;
            var completeInPlace =
                completionKind == BattleLocalMapResolutionKind.WorldSite ||
                completionKind == BattleLocalMapResolutionKind.Wilderness;
            var partyMembers = world.Strategic.PlayerPartyContext?.Members;
            var playerPartyParticipated = ManualBattleWorldCommitService
                .HasActualPlayerPartyParticipant(world.Strategic.Participants, partyMembers);
            var battleHex = "(none)";
            if (ArmyHexBattleAnchorService.TryGetBattleAnchorHex(
                    world.Strategic.Participants,
                    out var frozenBattleHex))
                battleHex = frozenBattleHex.ToString();
            var beforeSurface = DescribeCurrentSurface(world);
            // Phase 5S-B2-3.3：Auto settlement 必须在 Resolve 前 capture —— Auto 从未进入 Battle
            // LocalMap，确认结算后需要走正式 Apply 链切到 BattleHex surface；Manual 原地保留。
            var autoSettlement = world.Strategic.Participants.IsAutoSettlement;
            var continuousCombatOfferId = world.Strategic.ContinuousManualCombat.IsActive
                ? world.Strategic.ContinuousManualCombat.OfferId
                : string.Empty;
            ManualBattleReport pendingReport = null;
            if (!autoSettlement)
            {
                pendingReport = ManualBattleReportBuilder.CaptureFinal(
                    world,
                    world.Strategic.ManualBattleSettlement.Draft,
                    world.Strategic.Participants.PlayerWon,
                    world.Strategic.Participants.LastBattleSummary);
                if (pendingReport != null)
                    AcquireManualBattleReportPause(session);
            }
            var resolved = StrategicEncounterResolveService.ResolveAndEnd(world);
            if (resolved.IsSuccess)
            {
                // The completed offer owns this presentation isolation. Clear it even when
                // FinishOfferResolution has already promoted and frozen a queued offer.
                if (!string.IsNullOrEmpty(continuousCombatOfferId))
                    bootstrap?.CompleteContinuousManualCombat(continuousCombatOfferId);
                else if (!autoSettlement && completeInPlace)
                    bootstrap?.RefreshLoadedStrategicPopulation();
                if (!autoSettlement)
                {
                    if (world.Strategic.ManualBattleSettlement.Commit(pendingReport))
                        _manualBattleReport = world.Strategic.ManualBattleSettlement.CommittedReport;
                    if (_manualBattleReport == null)
                        ReleaseManualBattleReportPause();
                }
                ReleaseInterruptPause();
                if (!world.Strategic.IsWorldTickFrozen)
                {
                    if (bootstrap != null)
                        bootstrap.ApplySavedSpeedMultiplier(savedSpeed);
                    bootstrap.WorldMapPanel?.NotifyAfterBattleResolved(world);
                    if (completeInPlace)
                    {
                        if (autoSettlement)
                        {
                            if (playerPartyParticipated)
                                world.PlayerPartyTravel?.SurfaceEdgeGate?.ClearEdgeState();
                            bootstrap.ApplyPartyWorldSitePresentation(closeWorldMap: false);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            LogAutoBattleSurfaceRelease(world, beforeSurface, battleHex, playerPartyParticipated);
#endif
                            ShowToast("战斗已结束，世界时间已恢复。");
                        }
                        else
                            ShowToast("战斗结束，世界时间已恢复。");
                    }
                    else
                    {
                        bootstrap.WorldMapPanel?.Open();
                        bootstrap.WorldMapPanel?.RefreshStrategicPresentation(world);
                        if (BattleOfferService.HasLingeringBattlefield(world))
                        {
                            bootstrap.ApplyPartyWorldSitePresentation(closeWorldMap: false);
                            ShowToast("已退出战斗。弥留者仍在接战点，战场未消失。");
                        }
                        else
                            ShowToast("遭遇已结束，返回战略层。");
                    }
                }
                else
                    ShowToast(_manualBattleReport != null ? "战报已生成；继续后处理下一场接战。" : "下一场接战已就绪。");
            }
            else
            {
                if (!autoSettlement)
                {
                    ReleaseManualBattleReportPause();
                    LogManualSettlementFailureOnce(world, "Resolve", resolved.Error.Message);
                    ShowToast("结束战斗失败，本场状态已保留；请查看诊断日志后重试。");
                }
                else
                    ShowToast(resolved.Error.Message);
            }
            _settlementInProgress = false;
        }

        bool TryEnsureManualSettlementIdentity(SimulationWorld world, out string failure)
        {
            failure = string.Empty;
            if (world?.Strategic == null)
            {
                failure = "WorldOrStrategicMissing";
                return false;
            }
            var snapshot = world.Strategic.Participants;
            if (snapshot == null)
            {
                failure = "ParticipantsMissing";
                return false;
            }
            if (snapshot.IsAutoSettlement)
                return true;

            var actual = ActualBattleParticipantQuery.Collect(snapshot);
            if (actual.Count == 0)
            {
                failure = "ActualParticipantCount=0";
                return false;
            }

            var settlement = world.Strategic.ManualBattleSettlement;
            var continuous = world.Strategic.ContinuousManualCombat;
            if (settlement != null && settlement.IsInitialized)
            {
                if (!settlement.Matches(snapshot))
                {
                    failure = "SettlementParticipantCopyMismatch";
                    return false;
                }
                if (!string.IsNullOrEmpty(snapshot.OfferId) &&
                    !string.Equals(snapshot.OfferId, settlement.OfferId, StringComparison.Ordinal))
                {
                    failure = "ParticipantsOfferIdMismatch";
                    return false;
                }
                if (continuous != null && continuous.IsActive &&
                    !string.Equals(continuous.OfferId, settlement.OfferId, StringComparison.Ordinal))
                {
                    failure = "ContinuousOfferIdMismatch";
                    return false;
                }
                // Limited repair for a session created while BuildSnapshotFromEngagement still
                // erased snapshot identity. The independent settlement copy is authoritative.
                if (string.IsNullOrEmpty(snapshot.OfferId))
                    snapshot.OfferId = settlement.OfferId;
                return true;
            }

            // Compatibility for an already-running battle created before settlement state was
            // introduced. Use only a reliable frozen id plus the existing actual participant
            // records; entry HP/state stays Unknown and is never reconstructed from final HP.
            var reliableId = snapshot.OfferId ?? string.Empty;
            if (continuous != null && continuous.IsActive)
            {
                if (!string.IsNullOrEmpty(reliableId) &&
                    !string.Equals(reliableId, continuous.OfferId, StringComparison.Ordinal))
                {
                    failure = "LegacyContinuousOfferIdMismatch";
                    return false;
                }
                reliableId = continuous.OfferId;
                if (string.IsNullOrEmpty(reliableId) ||
                    continuous.ParticipantIds.Count != actual.Count)
                {
                    failure = "LegacyContinuousIdentityOrCountMissing";
                    return false;
                }
                for (var i = 0; i < actual.Count; i++)
                {
                    var participant = actual[i];
                    if (!continuous.Contains(participant.EntityId) ||
                        (participant.IsFriendly && !continuous.IsFriendly(participant.EntityId)) ||
                        (participant.IsEnemy && !continuous.IsEnemy(participant.EntityId)))
                    {
                        failure = "LegacyContinuousParticipantMismatch";
                        return false;
                    }
                }
            }
            if (string.IsNullOrWhiteSpace(reliableId))
            {
                failure = "NoReliableBattleId";
                return false;
            }
            if (settlement == null || !settlement.Begin(
                    world, snapshot, reliableId, entryStateKnown: false))
            {
                failure = "LegacySettlementCopyFailed";
                return false;
            }
            snapshot.OfferId = reliableId;
            return true;
        }

        void LogManualSettlementFailureOnce(SimulationWorld world, string stage, string condition)
        {
            var snapshot = world?.Strategic?.Participants;
            var continuous = world?.Strategic?.ContinuousManualCombat;
            var settlement = world?.Strategic?.ManualBattleSettlement;
            var actual = ActualBattleParticipantQuery.Collect(snapshot);
            var friendly = 0;
            var enemy = 0;
            for (var i = 0; i < actual.Count; i++)
                if (actual[i].IsFriendly) friendly++; else enemy++;
            var key = stage + "|" + condition + "|" + (snapshot?.OfferId ?? string.Empty) + "|" +
                      (continuous?.OfferId ?? string.Empty) + "|" + (settlement?.OfferId ?? string.Empty);
            if (string.Equals(key, _lastSettlementFailureKey, StringComparison.Ordinal))
                return;
            _lastSettlementFailureKey = key;
            Debug.LogError(
                "[ManualBattleSettlementFailure] Stage=" + stage +
                " Condition=" + condition +
                " WorldSessionMatch=" + ReferenceEquals(world, bootstrap?.Session?.World) +
                " Freeze=" + (world?.Strategic?.ClockFreeze?.Reason.ToString() ?? "Missing") +
                " PendingEngagementId=" + (world?.Strategic?.PendingEngagement?.EngagementId ?? string.Empty) +
                " EncounterLinkId=" + (world?.Strategic?.Encounter?.EncounterLinkId ?? string.Empty) +
                " ActiveBattlefieldId=" + (world?.Strategic?.Encounter?.ActiveBattlefieldId ?? string.Empty) +
                " ParticipantsOfferId=" + (snapshot?.OfferId ?? string.Empty) +
                " ContinuousOfferId=" + (continuous?.OfferId ?? string.Empty) +
                " SettlementOfferId=" + (settlement?.OfferId ?? string.Empty) +
                " ReportOfferId=" + (_manualBattleReport?.OfferId ?? string.Empty) +
                " SettlementInitialized=" + (settlement != null && settlement.IsInitialized) +
                " ActualParticipants=" + actual.Count +
                " Friendly=" + friendly +
                " Enemy=" + enemy +
                " SettlementCommitted=" + (settlement != null && settlement.IsCommitted),
                this);
        }

        static string DescribeCurrentSurface(SimulationWorld world)
        {
            if (!LoadedLocalMapBelongingQuery.TryResolveLoadedLocalMap(world, out var loaded))
                return "None";
            return loaded.Kind == LoadedLocalMapBelongingQuery.LoadedLocalMapKind.WorldSite
                ? "WorldSite(" + (loaded.Site?.SiteId ?? string.Empty) + ")"
                : "Wilderness(" + loaded.WildernessHex + ")";
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static void LogAutoBattleSurfaceRelease(
            SimulationWorld world,
            string beforeSurface,
            string battleHex,
            bool playerPartyParticipated)
        {
            var motion = world?.PlayerPartyTravel;
            var gate = motion?.SurfaceEdgeGate;
            Debug.Log(
                "[AutoBattleSurfaceRelease]" +
                " BeforeSurface=" + beforeSurface +
                " BattleHex=" + battleHex +
                " PlayerPartyParticipated=" + playerPartyParticipated +
                " AfterLocation=" + (motion != null ? motion.LocationKind.ToString() : "(none)") +
                " CurrentHex=" + (motion != null ? motion.CurrentHex.ToString() : "(none)") +
                " Freeze=" + (world?.Strategic?.ClockFreeze != null
                    ? world.Strategic.ClockFreeze.Reason.ToString()
                    : "(none)") +
                " GateTransitionInProgress=" + (gate != null && gate.TransitionInProgress) +
                " GateEdgeArmed=" + (gate != null && gate.EdgeArmed));
        }
#endif

        void DrawArrivalNotice(PlayableHostSession session, ArrivalNoticePending notice)
        {
            DrawDim();
            var box = new Rect(Screen.width * 0.5f - 240f, Screen.height * 0.5f - 140f, 480f, 280f);
            Fill(box, Parchment);
            DrawFrame(box, ParchmentDark);

            GUI.Label(new Rect(box.x + 16f, box.y + 12f, box.width - 32f, 26f), "到站提示", _title);
            GUI.Label(
                new Rect(box.x + 16f, box.y + 48f, box.width - 32f, 120f),
                (string.IsNullOrEmpty(notice.Summary) ? "有人抵达目的地" : notice.Summary) +
                "\n\n是否打开大地图查看？",
                _body);

            var y = box.y + box.height - 44f;
            var half = (box.width - 40f) * 0.5f;
            if (GUI.Button(new Rect(box.x + 16f, y, half, 32f), "去查看"))
            {
                var arrivedCopy = new List<ulong>(notice.ArrivedIds.Count);
                for (var i = 0; i < notice.ArrivedIds.Count; i++)
                    arrivedCopy.Add(notice.ArrivedIds[i]);

                session.World.Strategic.ClearArrivalNotice();
                ReleaseInterruptPause();

                if (bootstrap.WorldMapPanel != null)
                {
                    bootstrap.WorldMapPanel.Open();
                    bootstrap.WorldMapPanel.SelectArrivedParty(arrivedCopy);
                }
            }

            if (GUI.Button(new Rect(box.x + 24f + half, y, half, 32f), "暂不查看"))
            {
                session.World.Strategic.ClearArrivalNotice();
                ReleaseInterruptPause();
            }
        }

        void DrawToast()
        {
            if (string.IsNullOrEmpty(_toast) || Time.unscaledTime > _toastUntil)
                return;
            EnsureStyles();
            var rect = new Rect(Screen.width * 0.5f - 220f, 72f, 440f, 32f);
            var prev = GUI.color;
            GUI.color = new Color(0.1f, 0.12f, 0.14f, 0.92f);
            GUI.DrawTexture(rect, _px);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 12f, rect.y + 6f, rect.width - 24f, 22f), _toast, _body);
            GUI.color = prev;
        }

        void ShowToast(string message)
        {
            _toast = message ?? string.Empty;
            _toastUntil = Time.unscaledTime + 4f;
        }

        public void ShowTransientToast(string message) => ShowToast(message);

        void DrawCharacterEncounterOffer(PlayableHostSession session, HostCharacterEncounter coordinator)
        {
            var world = session.World;
            var box = new Rect(Screen.width * .5f - 260f, Screen.height * .5f - 235f, 520f, 470f);
            DrawDim(); Fill(box, Parchment); DrawFrame(box, ParchmentDark);
            GUI.Label(new Rect(box.x + 16f, box.y + 12f, box.width - 32f, 26f), "人物遭遇", _title);
            var phase = coordinator.Phase;
            GUI.Label(new Rect(box.x + 16f, box.y + 42f, box.width - 32f, 22f),
                phase == HostCharacterEncounter.PresentationPhase.Preparing || phase == HostCharacterEncounter.PresentationPhase.ReadyToCommit
                    ? coordinator.Progress : "初始双方仅为当前两支小队中的存活成员。", _body);
            var friendlies = new List<EntityId>(); var enemies = new List<EntityId>();
            coordinator.CopyPreviewTo(friendlies, enemies);
            var friendlyPower = SumCharacterPower(world, friendlies);
            var enemyPower = SumCharacterPower(world, enemies);
            GUI.Label(new Rect(box.x + 16f, box.y + 68f, box.width - 32f, 22f),
                "我方小队 " + friendlies.Count + " 人 · 战力 " + friendlyPower, _body);
            GUI.Label(new Rect(box.x + 16f, box.y + 90f, box.width - 32f, 22f),
                "敌方小队 " + enemies.Count + " 人 · 战力 " + enemyPower, _body);
            DrawPowerBar(new Rect(box.x + 16f, box.y + 118f, box.width - 32f, 14f), friendlyPower, enemyPower);
            var viewport = new Rect(box.x + 16f, box.y + 142f, box.width - 32f, box.height - 254f);
            var content = new Rect(0, 0, viewport.width - 20f,
                Mathf.Max(viewport.height, (friendlies.Count + enemies.Count) * 20f + 24f));
            _offerScroll = GUI.BeginScrollView(viewport, _offerScroll, content);
            var y = 4f;
            GUI.Label(new Rect(4f, y, content.width, 20f), "参战人物", _body); y += 20f;
            DrawCharacterPreviewRows(world, friendlies, "我方", 8f, ref y, content.width - 8f);
            DrawCharacterPreviewRows(world, enemies, "敌方", 8f, ref y, content.width - 8f);
            GUI.EndScrollView();
            if (!string.IsNullOrEmpty(coordinator.Failure))
                GUI.Label(new Rect(box.x + 16f, box.yMax - 104f, box.width - 32f, 38f), coordinator.Failure, _body);
            else if (phase == HostCharacterEncounter.PresentationPhase.Preparing || phase == HostCharacterEncounter.PresentationPhase.ReadyToCommit)
                GUI.Label(new Rect(box.x + 16f, box.yMax - 104f, box.width - 32f, 38f),
                    "正在按冻结范围准备真实地形与导航；WorldTick 保持冻结。", _body);
            var specs = new List<ButtonSpec> {
                new ButtonSpec(coordinator.IsRestoring ? "重试恢复" : "手动战斗", coordinator.BeginConfirmed,
                    phase == HostCharacterEncounter.PresentationPhase.Pending || phase == HostCharacterEncounter.PresentationPhase.Failed)
            };
            if (coordinator.CanCancel) specs.Add(new ButtonSpec("取消主动攻击", coordinator.CancelPending));
            DrawOfferActions(box, box.yMax - 48f, specs);
        }

        void DrawCharacterEncounterStartBar(
            PlayableHostSession session,
            HostCharacterEncounter coordinator)
        {
            GUI.depth = -95;
            const float width = 420f;
            const float height = 92f;
            var box = new Rect((Screen.width - width) * .5f, 82f, width, height);
            Fill(box, Parchment);
            DrawFrame(box, ParchmentDark);
            HostUiHitTest.Block(box);
            var message = coordinator.ReadyToStartIsRestore
                ? "战场已恢复 · 当前全场暂停"
                : "战场已就绪 · 当前全场暂停";
            GUI.Label(new Rect(box.x + 14f, box.y + 10f, box.width - 28f, 26f), message, _title);
            if (!string.IsNullOrEmpty(coordinator.Failure))
                GUI.Label(new Rect(box.x + 14f, box.y + 36f, box.width - 152f, 42f), coordinator.Failure, _body);
            if (GUI.Button(new Rect(box.xMax - 132f, box.yMax - 42f, 116f, 32f), "开始战斗") &&
                !coordinator.StartBattle())
                ShowToast(coordinator.Failure);
        }

        static int SumCharacterPower(SimulationWorld world, List<EntityId> ids)
        {
            var sum = 0;
            for (var i = 0; i < ids.Count; i++) sum += CombatPowerCalculator.ForEntity(world, ids[i]);
            return sum;
        }

        void DrawCharacterPreviewRows(SimulationWorld world, List<EntityId> ids, string side, float x, ref float y, float width)
        {
            for (var i = 0; i < ids.Count; i++)
            {
                GUI.Label(new Rect(x, y, width, 18f), side + " · " + ResolveCharacterLabel(world, ids[i]) +
                    "  战力 " + CombatPowerCalculator.ForEntity(world, ids[i]), _body);
                y += 18f;
            }
        }

        void DrawPowerBar(Rect bar, int friendly, int enemy)
        {
            var previous = GUI.color;
            var width = bar.width * friendly / Mathf.Max(1f, friendly + enemy);
            GUI.color = new Color(.35f, .72f, .42f, .9f);
            GUI.DrawTexture(new Rect(bar.x, bar.y, width, bar.height), _px);
            GUI.color = new Color(.78f, .32f, .28f, .9f);
            GUI.DrawTexture(new Rect(bar.x + width, bar.y, bar.width - width, bar.height), _px);
            GUI.color = previous;
        }

        void DrawBattleOffer(PlayableHostSession session, BattleOfferPending offer)
        {
            DrawDim();
            var snap = session.World.Strategic.Participants;
            var decision = BattleDecisionPolicy.ResolveDecisionOptions(session.World);
            var hideOptionalPickers = session.World.Strategic.PendingEngagement.IsActive;
            var optionalCount = 0;
            if (!hideOptionalPickers)
            {
                for (var i = 0; i < snap.Records.Count; i++)
                {
                    if (snap.Records[i].Kind == BattleParticipantKind.OptionalFriendly)
                        optionalCount++;
                }
            }

            var extra = Mathf.Min(optionalCount, 6) * 22f + 72f;
            var box = new Rect(
                Screen.width * 0.5f - 260f,
                Screen.height * 0.5f - (200f + extra * 0.5f),
                520f,
                390f + extra);
            Fill(box, Parchment);
            DrawFrame(box, ParchmentDark);

            var title = string.IsNullOrEmpty(offer.Title) ? "遭遇接战" : offer.Title;
            GUI.Label(new Rect(box.x + 16f, box.y + 12f, box.width - 32f, 26f), title, _title);
            GUI.Label(
                new Rect(box.x + 16f, box.y + 40f, box.width - 32f, 22f),
                offer.PlayerLabel + "  战力 " + offer.PlayerPower,
                _body);
            GUI.Label(
                new Rect(box.x + 16f, box.y + 62f, box.width - 32f, 22f),
                offer.EnemyLabel + "  战力 " + offer.EnemyPower,
                _body);
            if (string.Equals(offer.StrategicObjectiveKind, StrategicObjectiveKind.ControlCore, System.StringComparison.Ordinal))
                GUI.Label(
                    new Rect(box.x + 16f, box.y + 82f, box.width - 32f, 22f),
                    "战略目标：议政厅（不计入战斗单位与结束条件）",
                    _body);
            else if (string.Equals(offer.StrategicObjectiveKind, StrategicObjectiveKind.FactionFlag, System.StringComparison.Ordinal))
                GUI.Label(
                    new Rect(box.x + 16f, box.y + 82f, box.width - 32f, 22f),
                    "战略目标：阵营旗（不计入战斗单位与结束条件）",
                    _body);

            var barY = box.y + (string.IsNullOrEmpty(offer.StrategicObjectiveKind) ? 90f : 110f);
            DrawPowerBar(new Rect(box.x + 16f, barY, box.width - 32f, 14f), offer.PlayerPower, offer.EnemyPower);
            // CORRECTION V1: Auto 专属文案（自动战胜率 / 处决 toggle）只在 Auto 可用时显示。
            if (decision.Auto)
                GUI.Label(
                    new Rect(box.x + 16f, barY + 18f, box.width - 32f, 22f),
                    "自动战胜率约 " + offer.AutoWinPercent + "% · WorldTick 已冻结",
                    _body);
            else
                GUI.Label(
                    new Rect(box.x + 16f, barY + 18f, box.width - 32f, 22f),
                    "WorldTick 已冻结",
                    _body);

            var listY = barY + 44f;
            GUI.Label(new Rect(box.x + 16f, listY, box.width - 32f, 20f), "强制参战／敌军", _body);
            listY += 20f;
            for (var i = 0; i < snap.Records.Count; i++)
            {
                var r = snap.Records[i];
                if (r.Kind == BattleParticipantKind.OptionalFriendly)
                    continue;
                var tag = r.Kind == BattleParticipantKind.MandatoryFriendly
                    ? (LingeringBattlefieldPartyService.IsIncapacitated(session.World, r.EntityId)
                        ? "[强制·弥留] "
                        : "[强制] ")
                    : (r.Kind == BattleParticipantKind.EnemyReinforcement ? "[敌援] " : "[敌军] ");
                GUI.Label(
                    new Rect(box.x + 24f, listY, box.width - 40f, 18f),
                    tag + r.DisplayLabel + "  战力 " + r.CombatPower +
                    FormatParticipantLifeStamp(session.World, r.EntityId),
                    _body);
                listY += 18f;
            }

            var anyOptional = false;
            var drawnArmies = new HashSet<string>(StringComparer.Ordinal);
            if (!hideOptionalPickers)
            for (var i = 0; i < snap.Records.Count; i++)
            {
                if (snap.Records[i].Kind != BattleParticipantKind.OptionalFriendly)
                    continue;
                var r = snap.Records[i];
                if (!string.IsNullOrEmpty(r.FormalArmyId))
                {
                    if (drawnArmies.Contains(r.FormalArmyId))
                        continue;
                    drawnArmies.Add(r.FormalArmyId);
                    if (!anyOptional)
                    {
                        listY += 6f;
                        GUI.Label(
                            new Rect(box.x + 16f, listY, box.width - 32f, 20f),
                            "可选支援军团（勾选加入；参战后进入战场格）",
                            _body);
                        listY += 20f;
                        anyOptional = true;
                    }

                    var armyLabel = r.FormalArmyId;
                    var armyPower = 0;
                    var armySelected = true;
                    if (session.World.Strategic.FormalArmies.TryGet(r.FormalArmyId, out var army) &&
                        army != null &&
                        !string.IsNullOrEmpty(army.ArmyId))
                        armyLabel = army.ArmyId;
                    for (var j = 0; j < snap.Records.Count; j++)
                    {
                        var member = snap.Records[j];
                        if (member.Kind != BattleParticipantKind.OptionalFriendly)
                            continue;
                        if (!string.Equals(member.FormalArmyId, r.FormalArmyId, StringComparison.Ordinal))
                            continue;
                        armyPower += member.CombatPower;
                        if (!member.Selected)
                            armySelected = false;
                    }

                    var nextArmy = GUI.Toggle(
                        new Rect(box.x + 24f, listY, box.width - 40f, 20f),
                        armySelected,
                        "军团 " + armyLabel + "  战力 " + armyPower);
                    if (nextArmy != armySelected)
                        BattleOfferService.SetOptionalFormalArmySelected(
                            session.World, r.FormalArmyId, nextArmy);
                    listY += 22f;
                    continue;
                }

                if (!anyOptional)
                {
                    listY += 6f;
                    GUI.Label(
                        new Rect(box.x + 16f, listY, box.width - 32f, 20f),
                        "可选支援（勾选加入；参战后进入战场格）",
                        _body);
                    listY += 20f;
                    anyOptional = true;
                }

                var next = GUI.Toggle(
                    new Rect(box.x + 24f, listY, box.width - 40f, 20f),
                    r.Selected,
                    r.DisplayLabel + "  战力 " + r.CombatPower + FormatParticipantLifeStamp(session.World, r.EntityId));
                if (next != r.Selected)
                    BattleOfferService.SetOptionalSelected(session.World, r.EntityId, next);
                listY += 22f;
            }

            var noticeY = box.y + box.height - 78f;
            if (decision.Auto)
            {
                _executeOnWin = GUI.Toggle(
                    new Rect(box.x + 16f, noticeY, box.width - 32f, 22f),
                    _executeOnWin,
                    "战胜时处决（敌军阵亡留尸体；不勾选＝全部弥留，可再进补刀）");
            }
            else if (offer.RequiresWarDeclaration)
            {
                // CORRECTION V1: Neutral 宣战 warning 放在同一个 BattleOffer，不再弹第二个 modal。
                GUI.Label(
                    new Rect(box.x + 16f, noticeY, box.width - 32f, 22f),
                    "确认手动战斗将向【" +
                    StrategicFactionCatalog.DisplayName(offer.PendingWarDefenderFactionId) +
                    "】宣战。",
                    _body);
            }

            var y = box.y + box.height - 44f;
            // FIX: 按钮布局改为「动作列表驱动」。
            // 旧实现把 btnIndex++ 放在 GUI.Button 点击条件体内：未点击帧 btnIndex 恒为 0，
            // LocalMap-origin（Auto=false）时 Manual 与 Retreat 全部落在同一槽位 → 视觉重叠且点击命中同一 rect。
            // 现在按 decision 生成有序动作列表，x 只由列表下标决定，与点击状态无关。
            var specs = new List<ButtonSpec>(3);
            if (decision.Auto)
                specs.Add(new ButtonSpec("自动战斗", () =>
                {
                    offer.ExecuteOnWin = _executeOnWin;
                    var resolved = BattleOfferService.ResolveAuto(
                        session.World,
                        _executeOnWin,
                        out _,
                        out _);
                    _executeOnWin = false;
                    if (resolved.IsSuccess)
                    {
                        session.World.Strategic.PendingEngagement.Clear();
                        bootstrap.WorldMapPanel?.Open();
                        bootstrap.WorldMapPanel?.RefreshStrategicPresentation(session.World);
                    }
                    else
                        ShowToast(resolved.Error.Message);
                }));
            if (decision.Manual)
                specs.Add(new ButtonSpec("手动战斗", () => CommitManualWithWarDeclarationIfNeeded(session, offer)));
            if (decision.Retreat)
                specs.Add(new ButtonSpec("撤退", () =>
                {
                    var freeze = session.World.Strategic.ClockFreeze;
                    var savedSpeed = freeze.HasSavedHostPresentation
                        ? freeze.SavedSpeedMultiplier
                        : (bootstrap != null ? bootstrap.EffectiveSpeedMultiplier() : 1);
                    var retreat = BattleRetreatService.ExecuteRetreat(session.World, session.PlayerParty);
                    if (retreat.IsFailure)
                    {
                        ShowToast(retreat.Error.Message);
                    }
                    else
                    {
                        ReleaseInterruptPause();
                        if (!session.World.Strategic.IsWorldTickFrozen)
                        {
                            if (bootstrap != null)
                                bootstrap.ApplySavedSpeedMultiplier(savedSpeed);
                        }
                    }
                }));

            DrawOfferActions(box, y, specs);
        }

        void DrawOfferActions(Rect box, float y, List<ButtonSpec> specs)
        {
            var count = Mathf.Max(1, specs.Count);
            var slotW = (box.width - 40f) / count;
            for (var i = 0; i < specs.Count; i++)
            {
                var rect = new Rect(box.x + 16f + slotW * i, y, slotW, 32f);
                var previous = GUI.enabled;
                GUI.enabled = previous && specs[i].Enabled;
                if (GUI.Button(rect, specs[i].Label))
                    specs[i].Invoke();
                GUI.enabled = previous;
            }
        }

        /// <summary>BattleOffer 底部动作按钮描述：动作列表驱动布局，隐藏按钮后不残留槽位。</summary>
        readonly struct ButtonSpec
        {
            public ButtonSpec(string label, Action action, bool enabled = true)
            {
                Enabled = enabled;
                Label = label;
                Action = action;
            }
            public bool Enabled { get; }

            public string Label { get; }

            public Action Action { get; }

            public void Invoke() => Action?.Invoke();
        }

        /// <summary>
        /// CORRECTION V1: Manual 点击的 DeclareWar commitment point。
        /// 先 ValidateManualEntry；若 Offer 带 pending 宣战 metadata，则 defensive re-validate
        /// （engagement 仍 active / defender 存在 / faction 一致 / 非 same faction / 非 Friendly）
        /// 后调 StrategicMilitaryAggressionService.TryEscalateToWar；失败则保持 Offer 不进入战斗。
        /// 已 War 的 Offer（RequiresWarDeclaration=false）直接进入 Manual，无宣战步骤。
        /// </summary>
        void CommitManualWithWarDeclarationIfNeeded(PlayableHostSession session, BattleOfferPending offer)
        {
            var world = session.World;
            var freeze = world.Strategic.ClockFreeze;
            var manualPausedAfterEntry = freeze.HasSavedHostPresentation
                ? freeze.SavedHostPaused
                : session.ManualPaused;
            var gate = BattleManualEntryPolicy.ValidateManualEntry(world);
            if (gate.IsFailure)
            {
                ShowToast(gate.Error.Message);
                return;
            }

            BattleOfferService.RefreshOfferPowerLabels(world);
            BattleOfferService.PromoteInRangeIncapacitatedToMandatory(
                world, world.Strategic.Participants);
            var entryReportDraft = ManualBattleReportBuilder.CaptureEntry(
                world, world.Strategic.Participants, offer.OfferId);
            if (entryReportDraft == null)
            {
                LogManualSettlementFailureOnce(world, "EntryPreflight", "OfferIdOrActualParticipantsMissing");
                ShowToast("无法进入战斗：本场参战身份不完整。");
                return;
            }
            ContinuousOutdoorSurfaceRuntime.ManualCombatPreparation continuousPreparation = null;

            // Local-origin 的可见目标与当前物理 surface 必须同一；此预检在任何 DeclareWar
            // side effect 前执行，解析分叉必须保留 Offer 让用户看见，而不是静默切图。
            if (offer.Origin == BattleOfferOrigin.LocalMapHostileAction)
            {
                var continuous = bootstrap.ContinuousOutdoorSurfaceRuntime;
                if (continuous != null && continuous.IsActive &&
                    !world.LocalMap.IsInInterior)
                {
                    var prepared = continuous.TryPrepareManualCombatEntry(
                        world, offer, out continuousPreparation);
                    if (prepared.IsFailure)
                    {
                        WriteContinuousManualEntrySummary(
                            world, offer, continuousPreparation, "Preflight", prepared.Error.Message, 0);
                        ShowToast(prepared.Error.Message);
                        return;
                    }
                }
                else if (!LoadedLocalMapBelongingQuery.TryResolveLoadedLocalMap(world, out var previousLoaded))
                {
                    ShowToast("无法解析当前已加载的 LocalMap surface。");
                    return;
                }
                else
                {
                    var resolution = BattleLocalMapResolver.ResolvePendingEngagement(world);
                    if (!resolution.Success)
                    {
                        ShowToast("无法解析本地发起战斗地点：" + resolution.FailureReason);
                        return;
                    }
                    if (!ArmyHexBattleAnchorService.TryGetBattleAnchorHex(world.Strategic.Participants, out _) ||
                        world.HexWorld == null || !world.HexWorld.Contains(resolution.BattleHex))
                    {
                        ShowToast("本地发起战斗缺少有效的冻结战斗锚点。");
                        return;
                    }
                    if (ManualBattleWorldCommitService.PhysicalSurfaceChanged(previousLoaded, resolution))
                    {
                        ShowToast("本地发起战斗解析到了不同的物理场景。");
                        return;
                    }
                    if (!string.Equals(world.LocalMap.ActiveMapLayoutId, resolution.LocalMapId, StringComparison.Ordinal))
                    {
                        ShowToast("本地发起战斗的地图标识不一致。");
                        return;
                    }
                    WriteManualBattleSurfaceTrace(world, offer, previousLoaded, resolution, false);
                }
            }

            // Legacy LocalMap keeps its established declaration point. Continuous first
            // completes reversible presentation assembly, then commits diplomacy synchronously;
            // a failed declaration rolls that local assembly back and leaves the Offer intact.
            if (offer.RequiresWarDeclaration && continuousPreparation == null &&
                !TryCommitManualWarDeclaration(world, offer, out var preEntryWarFailure))
            {
                ShowToast(preEntryWarFailure);
                return;
            }

            var entered = EnterManualEncounter(
                session, offer.EncounterLocalMapId, offer.ArmyStackId, continuousPreparation);
            if (entered.IsFailure)
            {
                if (continuousPreparation != null)
                    WriteContinuousManualEntrySummary(
                        world, offer, continuousPreparation, "Assembly", entered.Error.Message, 0);
                ShowToast(entered.Error.Message);
                return;
            }
            if (offer.RequiresWarDeclaration && continuousPreparation != null &&
                !TryCommitManualWarDeclaration(world, offer, out var postAssemblyWarFailure))
            {
                bootstrap.ContinuousOutdoorSurfaceRuntime.AbortPreparedManualCombat(
                    continuousPreparation, postAssemblyWarFailure);
                WriteContinuousManualEntrySummary(
                    world, offer, continuousPreparation, "WarCommit", postAssemblyWarFailure, 0);
                ShowToast(postAssemblyWarFailure);
                return;
            }
            if (continuousPreparation != null)
                StrategicPursuitService.ClearPursuitForEngagedKeepEnRoute(
                    world, world.Strategic.Participants.CollectSelectedFriendly());
            if (continuousPreparation != null)
                WriteContinuousManualEntrySummary(
                    world, offer, continuousPreparation, "Completed", string.Empty,
                    continuousPreparation.ExpectedCount);
            if (!session.World.Strategic.ManualBattleSettlement.Begin(entryReportDraft))
            {
                if (continuousPreparation != null)
                    bootstrap.ContinuousOutdoorSurfaceRuntime.AbortPreparedManualCombat(
                        continuousPreparation, "本场结算身份初始化失败。");
                ShowToast("无法进入战斗：本场结算身份初始化失败。");
                return;
            }
            session.World.Strategic.ClearBattleOffer();
            session.World.Strategic.PendingEngagement.Clear();
            _manualBattleReport = null;
            _lastSettlementFailureKey = string.Empty;
            StrategicClockFreezeService.BeginOrPromote(
                session.World,
                StrategicClockFreezeReason.ManualEncounter);
            session.ManualPaused = manualPausedAfterEntry;
            ReleaseInterruptPause();
        }

        static bool TryCommitManualWarDeclaration(
            SimulationWorld world,
            BattleOfferPending offer,
            out string failure)
        {
            failure = string.Empty;
            var engagement = world?.Strategic?.PendingEngagement;
            if (engagement == null || !engagement.IsActive)
            {
                failure = "接战状态已失效，无法宣战。";
                return false;
            }
            if (!world.Strategic.FormalArmies.TryGet(offer.DefenderArmyId, out var defender) ||
                defender == null)
            {
                failure = "目标军团已不存在，无法宣战。";
                return false;
            }

            var currentPlayerFaction = world.Strategic.PlayerFactionId ?? string.Empty;
            var defenderFaction = defender.FactionId ?? string.Empty;
            if (!string.Equals(currentPlayerFaction, offer.PendingWarAttackerFactionId, StringComparison.Ordinal) ||
                !string.Equals(defenderFaction, offer.PendingWarDefenderFactionId, StringComparison.Ordinal))
            {
                failure = "宣战方/目标阵营已变化，请重新发起。";
                return false;
            }
            if (string.Equals(currentPlayerFaction, defenderFaction, StringComparison.Ordinal))
            {
                failure = "不能攻击同阵营单位。";
                return false;
            }
            var stance = world.Strategic.Diplomacy?.GetStance(currentPlayerFaction, defenderFaction) ??
                         FactionStance.Neutral;
            if (stance == FactionStance.Friendly)
            {
                failure = "该阵营为友好关系，不能宣战。";
                return false;
            }
            if (!StrategicMilitaryAggressionService.TryEscalateToWar(
                    world, currentPlayerFaction, defenderFaction, out var warReason))
            {
                failure = "宣战失败：" + warReason;
                return false;
            }
            return true;
        }

        Result EnterManualEncounter(
            PlayableHostSession session,
            string localMapId,
            string armyStackId,
            ContinuousOutdoorSurfaceRuntime.ManualCombatPreparation continuousPreparation = null)
        {
            if (session?.World == null || bootstrap == null)
                return Result.Failure(ErrorCode.InvalidOperation, "手动战斗 Host 尚未就绪。");

            var gate = BattleManualEntryPolicy.ValidateManualEntry(session.World);
            if (gate.IsFailure)
                return gate;

            // 普通世界接战直接消费 Phase 4 冻结的 PendingEngagement 地点；显式 Encounter
            // 才保留旧的专用地图／默认地图兼容路径。
            var pending = session.World.Strategic?.PendingEngagement;
            var worldCombat = pending != null && pending.IsActive;
            BattleLocalMapResolution worldResolution = null;
            var samePhysicalSurface = false;
            var continuousWorldCombat = worldCombat && continuousPreparation != null;
            if (worldCombat && !continuousWorldCombat)
            {
                worldResolution = BattleLocalMapResolver.ResolvePendingEngagement(session.World);
                if (!worldResolution.Success)
                    return Result.Failure(ErrorCode.InvalidOperation, "无法解析世界战斗地点：" + worldResolution.FailureReason);

                // Phase 5S-B2-3.2：Snapshot BattleAnchorHex 是 frozen authority，缺它不能入场。
                if (!ArmyHexBattleAnchorService.TryGetBattleAnchorHex(
                        session.World.Strategic.Participants, out _))
                    return Result.Failure(ErrorCode.InvalidOperation, "手动战斗缺少冻结的 BattleAnchorHex。");

                localMapId = worldResolution.LocalMapId;
            }

            if (!worldCombat && string.IsNullOrWhiteSpace(localMapId))
                localMapId = StrategicEncounterCatalog.DefaultEncounterLocalMapId;

            var engaged = session.World.Strategic.Participants.CollectSelectedFriendly();
            if (engaged.Count == 0)
                engaged = ResolveEngagedPartyForManualEncounter(session.World);

            var memberCount = StrategicEncounterCatalog.DefaultFallbackMemberCount;
            var power = StrategicEncounterCatalog.DefaultFallbackCombatPower;
            if (!string.IsNullOrEmpty(armyStackId) &&
                session.World.Strategic.Armies.TryGet(armyStackId, out var primaryStack) &&
                primaryStack != null)
            {
                if (primaryStack.HasDownedRemnant)
                {
                    memberCount = Math.Max(
                        1,
                        Math.Max(primaryStack.IncapacitatedMemberCount, primaryStack.CorpseMemberCount));
                }
                else
                {
                    memberCount = Math.Max(1, primaryStack.MemberCount);
                }

                power = Math.Max(1, primaryStack.CombatPower);
            }
            else
            {
                var enemyIds = session.World.Strategic.Participants.CollectEnemyStackIds();
                if (enemyIds.Count > 0)
                {
                    memberCount = 0;
                    power = 0;
                    for (var i = 0; i < enemyIds.Count; i++)
                    {
                        if (!session.World.Strategic.Armies.TryGet(enemyIds[i], out var st) || st == null)
                            continue;
                        memberCount += Math.Max(1, st.MemberCount);
                        power += Math.Max(1, st.CombatPower);
                    }

                    if (memberCount <= 0)
                        memberCount = StrategicEncounterCatalog.DefaultFallbackMemberCount;
                    if (power <= 0)
                        power = StrategicEncounterCatalog.DefaultFallbackCombatPower;
                }
            }

            var rt = session.World.Strategic.Encounter;
            var encounterLink = session.World.PartyWorld.EncounterId;
            if (string.IsNullOrEmpty(encounterLink) && rt != null)
                encounterLink = string.IsNullOrEmpty(rt.EncounterLinkId) ? "linger" : rt.EncounterLinkId;

            HexCoord? lingerHex = null;
            if (ArmyHexBattleAnchorService.TryGetBattleAnchorHex(
                    session.World.Strategic.Participants, out var anchorHex))
                lingerHex = anchorHex;

            if (worldCombat)
            {
                // Phase 5S：普通 WORLD_COMBAT 走 fresh planning path —— 不绑定旧 Lingering
                // Registry（ActiveBattlefieldId / stored participants / stack.HasDownedRemnant
                // reuse 全部绕过）。新 living Army 战斗绝不会因同 Hex 有历史 casualty 变成
                // residual re-entry。markPartyInEncounter 保持 false：真实 Character 的存在由
                // PlayerPartyWorldMotion / FormalArmy.WorldMotion / StrategicResidualPresence 负责。
                StrategicEncounterSpawner.PlanFreshWorldCombatManualEncounter(
                    session.World,
                    armyStackId,
                    encounterLink,
                    engaged,
                    memberCount,
                    Math.Max(1, power / Math.Max(1, memberCount)));
            }
            else
            {
                // legacy ExplicitEncounter / 旧 Lingering compatibility 保持原路径。
                StrategicEncounterSpawner.TryPrepareLingeringLocalMapSession(session.World, lingerHex);
                StrategicEncounterSpawner.PlanManualEncounter(
                    session.World,
                    armyStackId,
                    encounterLink,
                    engaged,
                    memberCount,
                    Math.Max(1, power / Math.Max(1, memberCount)),
                    markPartyInEncounter: true);
            }
            if (!continuousWorldCombat)
                StrategicPursuitService.ClearPursuitForEngagedKeepEnRoute(session.World, engaged);
            // Phase 5S：冻结本场 Manual Battle 的地点解析类别（真实 LocalMap 或 ExplicitEncounterMap）。
            session.World.Strategic.Participants.LocalMapResolutionKind = worldCombat
                ? continuousWorldCombat
                    ? ResolveContinuousStrategicLocationKind(session.World)
                    : worldResolution.Kind
                : BattleLocalMapResolutionKind.ExplicitEncounterMap;
            if (worldCombat && !continuousWorldCombat)
                session.World.Strategic.Participants.EncounterLocalMapId = worldResolution.LocalMapId;
            // Phase 5S-B2-3.2：Manual Battle 入场 = 所有实际参战战略单位（PlayerParty + 全部
            // 参战 FormalArmy）正式 commit 到 BattleAnchorHex。这是正式改变旧的
            // 「Active 不 teleport / PlayerParty 保持 SupportArea」policy —— 选择 Manual Battle
            // 本身就代表 PlayerParty 从 SupportArea 正式加入 BattleHex。
            // Friendly battle presentation 不再在此处提前执行，移入 map-loaded assembly 阶段
            // （PlayableHostBootstrap.ApplyPartyWorldSitePresentation 的 PlayerParty materialize
            // 之后、enemy ApplyPending 之前）。
            if (worldCombat && !continuousWorldCombat)
            {
                // 必须在 commit 前 capture 旧 physical loaded surface（Wilderness context 读
                // PlayerPartyTravel.CurrentHex，commit 后已指向 BattleHex）。
                LoadedLocalMapBelongingQuery.TryResolveLoadedLocalMap(
                    session.World, out var previousLoaded);

                var commitResult = ManualBattleWorldCommitService.CommitWorldCombatParticipants(
                    session.World,
                    session.PlayerParty != null ? session.PlayerParty.Members : null,
                    session.World.Strategic.Participants,
                    worldResolution);
                if (commitResult.IsFailure)
                    return Result.Failure(ErrorCode.InvalidOperation, "战斗入场 commit 失败：" + commitResult.Error.Message);

                // physical surface 变化（S→B 即使共用同一 MapLayoutId 也按 Hex/Site 语义判定）
                // 时重置旧 LocalMap domain session（background occupant / army presentation /
                // residual / stale local override 清掉），再进入 Battle surface。
                samePhysicalSurface = !ManualBattleWorldCommitService.PhysicalSurfaceChanged(
                    previousLoaded, worldResolution);
                if (!samePhysicalSurface)
                    WorldTravelService.ApplyLocalMapSessionFromFocus(session.World);
            }
            var map = string.IsNullOrWhiteSpace(localMapId)
                ? BattleOfferService.ResolveActiveEncounterLocalMapId(session.World)
                : localMapId.Trim();
            if (!worldCombat)
                session.World.PartyWorld.ClearSiteFocus();
            if (!continuousWorldCombat)
                session.World.PartyWorld.LocalMapId = map;
            if (worldCombat && !continuousWorldCombat)
            {
                // Bootstrap 现有 active-encounter targetMap authority 读取此字段；这里只记录
                // 已解析的真实地图，不创建第二套位置状态。
                session.World.Strategic.Encounter.LingeringLocalMapId = map;
                var currentMap = session.World.LocalMap?.ActiveMapLayoutId ?? string.Empty;
                var reuse = string.Equals(currentMap, map, StringComparison.Ordinal);
                UnityEngine.Debug.Log("[WorldCombatManualEntry] Kind=" + worldResolution.Kind +
                    " SiteId=" + (worldResolution.SiteId ?? string.Empty) +
                    " BattleHex=" + worldResolution.BattleHex +
                    " ResolvedLocalMapId=" + map +
                    " CurrentLocalMapId=" + currentMap +
                    " ReuseCurrentLocalMap=" + reuse +
                    " PlayerPartyIncluded=" + pending.PlayerPartyIncluded +
                    " ParticipantCount=" + engaged.Count);
            }
            if (session != null && !continuousWorldCombat)
                session.PreferredMapLayoutId = map;

            // 进战场：大地图必须关（与 Open 门禁一致）
            bootstrap.WorldMapPanel?.Close();
            if (continuousWorldCombat)
                return bootstrap.ActivateRealWorldCombatOnCurrentLoadedSurface(continuousPreparation);
            if (worldCombat &&
                samePhysicalSurface &&
                session.World.Strategic.BattleOffer.Origin == BattleOfferOrigin.LocalMapHostileAction)
            {
                bootstrap.ActivateRealWorldCombatOnCurrentLoadedSurface();
                return Result.Success();
            }
            bootstrap.ApplyPartyWorldSitePresentation(closeWorldMap: true);
            return Result.Success();
        }

        static BattleLocalMapResolutionKind ResolveContinuousStrategicLocationKind(SimulationWorld world)
        {
            var pending = world?.Strategic?.PendingEngagement;
            return pending != null && pending.HasSupportArea &&
                   !string.IsNullOrEmpty(pending.SupportArea.BattleSiteId)
                ? BattleLocalMapResolutionKind.WorldSite
                : BattleLocalMapResolutionKind.Wilderness;
        }

        void WriteContinuousManualEntrySummary(
            SimulationWorld world,
            BattleOfferPending offer,
            ContinuousOutdoorSurfaceRuntime.ManualCombatPreparation preparation,
            string stage,
            string failure,
            int actual)
        {
            var anchorHex = ArmyHexBattleAnchorService.TryGetBattleAnchorHex(
                world?.Strategic?.Participants, out var hex) ? hex.ToString() : "<missing>";
            Debug.Log("[ContinuousManualBattleEntry] OfferId=" + (offer?.OfferId ?? string.Empty) +
                      " Origin=" + (offer != null ? offer.Origin.ToString() : string.Empty) +
                      " PresentationSpace=ContinuousOutdoor" +
                      " SourceSurfaceId=" + (preparation?.SurfaceId ??
                          bootstrap?.ContinuousOutdoorSurfaceRuntime?.ActiveSurfaceId ?? string.Empty) +
                      " ActiveMapLayoutId=" + (world?.LocalMap?.ActiveMapLayoutId ?? string.Empty) +
                      " BattleAnchorHex=" + anchorHex +
                      " ContinuousAnchor=" + (preparation != null ? preparation.BattleWorldAnchor.ToString() : "<unresolved>") +
                      " RequiredChunkReady=" + (preparation != null) +
                      " Expected=" + (preparation?.ExpectedCount ?? 0) +
                      " Actual=" + actual +
                      " Stage=" + stage +
                      " Failure=" + (failure ?? string.Empty));
        }

        static void WriteManualBattleSurfaceTrace(
            SimulationWorld world,
            BattleOfferPending offer,
            LoadedLocalMapBelongingQuery.LoadedLocalMapContext previousLoaded,
            BattleLocalMapResolution resolution,
            bool physicalSurfaceChanged)
        {
            var engagement = world.Strategic.PendingEngagement;
            var support = engagement != null && engagement.HasSupportArea ? engagement.SupportArea : null;
            var defender = engagement != null && !string.IsNullOrEmpty(engagement.DefenderFormalArmyId) &&
                           world.Strategic.FormalArmies.TryGet(engagement.DefenderFormalArmyId, out var army)
                ? army : null;
            var motion = defender?.WorldMotion;
            Debug.Log("=== Manual Battle Surface ===\n" +
                      "OfferOrigin=" + offer.Origin + "\n" +
                      "CurrentActiveMap=" + (world.LocalMap?.ActiveMapLayoutId ?? string.Empty) + "\n" +
                      "CurrentLoadedKind=" + previousLoaded.Kind + " CurrentLoadedSiteId=" + (previousLoaded.Site?.SiteId ?? string.Empty) +
                      " CurrentLoadedWildernessHex=" + previousLoaded.WildernessHex + "\n" +
                      "FrozenBattleSiteId=" + (support?.BattleSiteId ?? string.Empty) +
                      " FrozenSiteSource=" + (support?.BattleSiteResolutionSource ?? string.Empty) +
                      " BattleAreaHexes=" + (support?.BattleAreaHexes.Count ?? 0) +
                      " BattleLocation=" + engagement.BattleLocation + "\n" +
                      "DefenderMotionKind=" + (motion?.LocationKind.ToString() ?? string.Empty) +
                      " DefenderMotionSiteId=" + (motion?.SiteId ?? string.Empty) +
                      " DefenderCurrentHex=" + (motion?.CurrentHex.ToString() ?? string.Empty) + "\n" +
                      "ResolvedKind=" + resolution.Kind + " ResolvedSiteId=" + resolution.SiteId +
                      " ResolvedBattleHex=" + resolution.BattleHex + " ResolvedLocalMapId=" + resolution.LocalMapId + "\n" +
                      "PhysicalSurfaceChanged=" + physicalSurfaceChanged +
                      " EntryMode=" + (physicalSurfaceChanged ? "LoadBattleSurface" : "InPlaceCurrentSurface"));
        }

        static List<EntityId> ResolveEngagedPartyForManualEncounter(SimulationWorld world)
        {
            var list = new List<EntityId>(4);
            if (world?.Strategic == null)
                return list;

            var offer = world.Strategic.BattleOffer;
            if (offer.PlayerPartyIds.Count > 0)
                list.AddRange(StrategicPursuitService.CollectEngagedPartyFromOffer(offer));

            if (list.Count == 0 && world.Strategic.Encounter.HasEngagedParty)
                list.AddRange(StrategicPursuitService.CollectEngagedParty(world, world.Strategic.Encounter));

            return list;
        }

        void DrawDim()
        {
            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _px);
            GUI.color = prev;
        }

        static string FormatParticipantLifeStamp(SimulationWorld world, EntityId id)
        {
            if (world == null || id.IsNone || !world.Entities.TryGet(id, out var entity) || entity == null)
                return string.Empty;
            var stamped = CombatLifeStateService.FormatLifeStateWithCountdown(world, entity);
            if (string.IsNullOrEmpty(stamped) || stamped == "存活")
                return string.Empty;
            return " · " + stamped;
        }

        void Fill(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _px);
            GUI.color = prev;
        }

        void DrawFrame(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2f), _px);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 2f, r.width, 2f), _px);
            GUI.DrawTexture(new Rect(r.x, r.y, 2f, r.height), _px);
            GUI.DrawTexture(new Rect(r.xMax - 2f, r.y, 2f, r.height), _px);
            GUI.color = prev;
        }

        void EnsureStyles()
        {
            if (_stylesReady)
                return;
            _px = new Texture2D(1, 1);
            _px.SetPixel(0, 0, Color.white);
            _px.Apply();
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
    }
}
