using System;
using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>战略打断：接战 BattleOffer + 到站 ArrivalNotice。</summary>
    public sealed class HostStrategicInterruptPresenter : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;

        bool _holding;
        bool _pausedBefore;
        bool _executeOnWin;
        string _toast = string.Empty;
        double _toastUntil;
        Texture2D _px;
        GUIStyle _title;
        GUIStyle _body;
        bool _stylesReady;

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
            _holding = false;
            _pausedBefore = false;
            _executeOnWin = false;
            _toast = string.Empty;
            _toastUntil = 0;
        }

        void Update() => SyncPause();

        void SyncPause()
        {
            var session = bootstrap != null ? bootstrap.Session : null;
            if (session == null || !session.IsInitialized)
            {
                ClearSessionState();
                return;
            }

            var world = session.World;
            SyncClockFreezePresentation(session);

            if (HasBlockingInterrupt)
            {
                if (!_holding)
                {
                    _pausedBefore = session.IsPaused;
                    _holding = true;
                }

                // Offer／到站／自动战结算弹窗：强制 UI 暂停
                // 手动 PostBattle（非 AutoSettlement）不挡场景操作
                var autoSettle = world?.Strategic?.Participants != null &&
                                 world.Strategic.Participants.IsAutoSettlement;
                if (world?.Strategic == null ||
                    !world.Strategic.IsModalEncounter ||
                    autoSettle)
                    session.IsPaused = true;
            }
            else if (_holding)
            {
                // 进入手动战后 Offer 已清，但仍在 ClockFreeze → 不得恢复战略时间
                if (world?.Strategic == null || !world.Strategic.IsWorldTickFrozen)
                    session.IsPaused = _pausedBefore;
                _holding = false;
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
                session.IsPaused,
                speed);

            if (freeze.Reason == StrategicClockFreezeReason.BattleOffer)
                session.IsPaused = true;
        }

        /// <summary>解除战略冻结并恢复开战前 pause／倍速。</summary>
        void RestoreHostPresentationAfterFreeze(PlayableHostSession session)
        {
            if (session?.World?.Strategic == null)
                return;
            var freeze = session.World.Strategic.ClockFreeze;
            if (freeze.HasSavedHostPresentation)
            {
                session.IsPaused = freeze.SavedHostPaused;
                if (bootstrap != null)
                    bootstrap.ApplySavedSpeedMultiplier(freeze.SavedSpeedMultiplier);
            }

            StrategicClockFreezeService.EndFreeze(session.World);
        }

        void OnGUI()
        {
            var session = bootstrap != null ? bootstrap.Session : null;
            if (session == null || !session.IsInitialized || session.World?.Strategic == null)
                return;

            EnsureStyles();
            DrawToast();

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
            if (world.Strategic.ClockFreeze.Reason != StrategicClockFreezeReason.PostBattle &&
                !(world.Strategic.ClockFreeze.Reason == StrategicClockFreezeReason.ManualEncounter &&
                  (StrategicEncounterSpawner.IsFieldCleared(world) ||
                   StrategicEncounterResolveService.AreAllEngagedFriendliesDown(world))))
                return;

            if (world.Strategic.ClockFreeze.Reason == StrategicClockFreezeReason.ManualEncounter)
                StrategicEncounterResolveService.TryEnterPostBattleFromManual(world);

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
            var box = new Rect(Screen.width * 0.5f - 240f, Screen.height * 0.5f - 130f, 480f, 260f);
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
                new Rect(box.x + 16f, box.y + 52f, box.width - 32f, 120f),
                summary + "\n\n确认后返回战略层并恢复时间。",
                _body);
            if (GUI.Button(new Rect(box.x + 16f, box.y + box.height - 48f, box.width - 32f, 32f), "确认结算"))
                ConfirmEndBattle(session);
        }

        void DrawManualPostBattleBar(PlayableHostSession session)
        {
            var world = session.World;
            GUI.depth = -40;
            // 非强制：不遮罩、不挡操作；点「结束战斗」才 Resolve
            var barW = 420f;
            var barH = 64f;
            var box = new Rect(Screen.width - barW - 16f, Screen.height - barH - 72f, barW, barH);
            Fill(box, Parchment);
            DrawFrame(box, ParchmentDark);
            var summary = world.Strategic.Participants.LastBattleSummary;
            if (string.IsNullOrEmpty(summary))
                summary = "敌军已清空。可补刀／交互；点结束才结算。";
            GUI.Label(new Rect(box.x + 10f, box.y + 6f, box.width - 140f, 52f), summary, _body);
            if (GUI.Button(new Rect(box.xMax - 128f, box.y + 14f, 116f, 36f), "结束战斗"))
                ConfirmEndBattle(session);
        }

        void ConfirmEndBattle(PlayableHostSession session)
        {
            var world = session.World;
            if (world?.Strategic == null)
                return;
            var freeze = world.Strategic.ClockFreeze;
            var savedPaused = freeze.HasSavedHostPresentation ? freeze.SavedHostPaused : session.IsPaused;
            var savedSpeed = freeze.HasSavedHostPresentation
                ? freeze.SavedSpeedMultiplier
                : (bootstrap != null ? bootstrap.EffectiveSpeedMultiplier() : 1);
            var resolved = StrategicEncounterResolveService.ResolveAndEnd(world);
                if (resolved.IsSuccess)
                {
                    _holding = false;
                    if (!world.Strategic.IsWorldTickFrozen)
                    {
                        session.IsPaused = savedPaused;
                        if (bootstrap != null)
                            bootstrap.ApplySavedSpeedMultiplier(savedSpeed);
                        bootstrap.WorldMapPanel?.Open();
                        if (BattleOfferService.HasLingeringBattlefield(world))
                        {
                            bootstrap.ApplyPartyWorldNodePresentation(closeWorldMap: false);
                            ShowToast("已退出战斗。弥留者仍在接战点，战场未消失。");
                        }
                        else
                            ShowToast("遭遇已结束，返回战略层。");
                    }
                    else
                    {
                        session.IsPaused = true;
                        ShowToast("下一场接战已就绪。");
                    }
                }
            else
                ShowToast(resolved.Error.Message);
        }

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
                if (_holding)
                {
                    session.IsPaused = _pausedBefore;
                    _holding = false;
                }

                if (bootstrap.WorldMapPanel != null)
                {
                    bootstrap.WorldMapPanel.Open();
                    bootstrap.WorldMapPanel.SelectArrivedParty(arrivedCopy);
                    bootstrap.WorldMapPanel.TryOpenPendingLingeringVisitAfterArrival();
                }
            }

            if (GUI.Button(new Rect(box.x + 24f + half, y, half, 32f), "暂不查看"))
            {
                session.World.Strategic.ClearArrivalNotice();
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

        void DrawBattleOffer(PlayableHostSession session, BattleOfferPending offer)
        {
            DrawDim();
            var snap = session.World.Strategic.Participants;
            var optionalCount = 0;
            for (var i = 0; i < snap.Records.Count; i++)
            {
                if (snap.Records[i].Kind == BattleParticipantKind.OptionalFriendly)
                    optionalCount++;
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

            var barY = box.y + 90f;
            var barW = box.width - 32f;
            var total = Mathf.Max(1, offer.PlayerPower + offer.EnemyPower);
            var pw = barW * (offer.PlayerPower / (float)total);
            GUI.color = new Color(0.35f, 0.72f, 0.42f, 0.9f);
            GUI.DrawTexture(new Rect(box.x + 16f, barY, pw, 14f), _px);
            GUI.color = new Color(0.78f, 0.32f, 0.28f, 0.9f);
            GUI.DrawTexture(new Rect(box.x + 16f + pw, barY, barW - pw, 14f), _px);
            GUI.color = Color.white;
            GUI.Label(
                new Rect(box.x + 16f, barY + 18f, box.width - 32f, 22f),
                "自动战胜率约 " + offer.AutoWinPercent + "% · WorldTick 已冻结",
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
                    ? "[强制] "
                    : (r.Kind == BattleParticipantKind.EnemyReinforcement ? "[敌援] " : "[敌军] ");
                GUI.Label(
                    new Rect(box.x + 24f, listY, box.width - 40f, 18f),
                    tag + r.DisplayLabel + "  战力 " + r.CombatPower,
                    _body);
                listY += 18f;
            }

            var anyOptional = false;
            for (var i = 0; i < snap.Records.Count; i++)
            {
                if (snap.Records[i].Kind != BattleParticipantKind.OptionalFriendly)
                    continue;
                if (!anyOptional)
                {
                    listY += 6f;
                    GUI.Label(
                        new Rect(box.x + 16f, listY, box.width - 32f, 20f),
                        "可选支援（勾选加入；战后回原位置）",
                        _body);
                    listY += 20f;
                    anyOptional = true;
                }

                var r = snap.Records[i];
                var next = GUI.Toggle(
                    new Rect(box.x + 24f, listY, box.width - 40f, 20f),
                    r.Selected,
                    r.DisplayLabel + "  战力 " + r.CombatPower);
                if (next != r.Selected)
                    BattleOfferService.SetOptionalSelected(session.World, r.EntityId, next);
                listY += 22f;
            }

            var toggleY = box.y + box.height - 78f;
            _executeOnWin = GUI.Toggle(
                new Rect(box.x + 16f, toggleY, box.width - 32f, 22f),
                _executeOnWin,
                "战胜时直接击杀（不勾选＝敌军全部弥留，可再进补刀）");

            var y = box.y + box.height - 44f;
            var third = (box.width - 40f) / 3f;
            if (GUI.Button(new Rect(box.x + 16f, y, third, 32f), "自动战斗"))
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
                    // 进入自动战结算弹窗（PostBattle + IsAutoSettlement）
                    _holding = true;
                    session.IsPaused = true;
                }
                else
                    ShowToast(resolved.Error.Message);
            }

            if (GUI.Button(new Rect(box.x + 20f + third, y, third, 32f), "手动战斗"))
            {
                EnterManualEncounter(session, offer.EncounterLocalMapId, offer.ArmyStackId);
                session.World.Strategic.ClearBattleOffer();
                StrategicClockFreezeService.BeginOrPromote(
                    session.World,
                    StrategicClockFreezeReason.ManualEncounter);
                session.IsPaused = false;
                _holding = false;
            }

            if (GUI.Button(new Rect(box.x + 24f + third * 2f, y, third, 32f), "撤退"))
            {
                StrategicPursuitService.ClearPursuit(session.World);
                session.World.Strategic.ClearBattleOffer();
                _holding = false;
                var freeze = session.World.Strategic.ClockFreeze;
                var savedPaused = freeze.HasSavedHostPresentation
                    ? freeze.SavedHostPaused
                    : session.IsPaused;
                var savedSpeed = freeze.HasSavedHostPresentation
                    ? freeze.SavedSpeedMultiplier
                    : (bootstrap != null ? bootstrap.EffectiveSpeedMultiplier() : 1);
                BattleOfferService.FinishOfferResolution(session.World);
                if (!session.World.Strategic.IsWorldTickFrozen)
                {
                    session.IsPaused = savedPaused;
                    if (bootstrap != null)
                        bootstrap.ApplySavedSpeedMultiplier(savedSpeed);
                }
                else
                    session.IsPaused = true;
            }
        }

        void EnterManualEncounter(
            PlayableHostSession session,
            string localMapId,
            string armyStackId)
        {
            if (session?.World == null || bootstrap == null)
                return;
            if (string.IsNullOrWhiteSpace(localMapId))
                localMapId = StrategicEncounterCatalog.DefaultEncounterLocalMapId;

            BattleOfferService.RefreshOfferPowerLabels(session.World);
            var engaged = session.World.Strategic.Participants.CollectSelectedFriendly();
            if (engaged.Count == 0)
                engaged = ResolveEngagedPartyForManualEncounter(session.World);

            var memberCount = StrategicEncounterCatalog.DefaultFallbackMemberCount;
            var power = StrategicEncounterCatalog.DefaultFallbackCombatPower;
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

            StrategicEncounterSpawner.PlanManualEncounter(
                session.World,
                armyStackId,
                session.World.PartyWorld.EncounterId,
                engaged,
                memberCount,
                Math.Max(1, power / Math.Max(1, memberCount)));
            StrategicPursuitService.ClearPursuitForEngagedKeepEnRoute(session.World, engaged);
            var map = string.IsNullOrWhiteSpace(localMapId)
                ? BattleOfferService.ResolveActiveEncounterLocalMapId(session.World)
                : localMapId.Trim();
            session.World.PartyWorld.LocalMapId = map;
            if (session != null)
                session.PreferredMapLayoutId = map;

            var closeMap = bootstrap.WorldMapPanel != null && bootstrap.WorldMapPanel.IsOpen;
            bootstrap.ApplyPartyWorldNodePresentation(closeWorldMap: closeMap);
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
