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
            GUI.depth = -40;
            var barW = 420f;
            var barH = 210f;
            var box = new Rect(Screen.width - barW - 16f, Screen.height - barH - 72f, barW, barH);
            Fill(box, Parchment);
            DrawFrame(box, ParchmentDark);
            var summary = world.Strategic.Participants.LastBattleSummary;
            if (string.IsNullOrEmpty(summary))
                summary = "敌军已清空。可补刀／交互；点结束才结算。";
            GUI.Label(new Rect(box.x + 10f, box.y + 6f, box.width - 140f, 52f), summary, _body);
            DrawBattleAftermathSection(world, new Rect(box.x + 10f, box.y + 58f, box.width - 20f, 110f));
            if (GUI.Button(new Rect(box.xMax - 128f, box.y + box.height - 40f, 116f, 32f), "结束战斗"))
                ConfirmEndBattle(session);
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
            if (world?.Strategic == null)
                return;
            var freeze = world.Strategic.ClockFreeze;
            // 有开战前快照则恢复；否则默认走时（勿把结算弹窗造成的暂停当成用户本意）
            var savedPaused = freeze.HasSavedHostPresentation && freeze.SavedHostPaused;
            var savedSpeed = freeze.HasSavedHostPresentation
                ? freeze.SavedSpeedMultiplier
                : (bootstrap != null ? bootstrap.EffectiveSpeedMultiplier() : 1);
            // Phase 5S：Resolve 前 capture（FinishOfferResolution 会清 Participants / IsAutoSettlement）。
            var completionKind = world.Strategic.Participants.LocalMapResolutionKind;
            var completeInPlace =
                completionKind == BattleLocalMapResolutionKind.WorldSite ||
                completionKind == BattleLocalMapResolutionKind.Wilderness;
            // Phase 5S-B2-3.3：Auto settlement 必须在 Resolve 前 capture —— Auto 从未进入 Battle
            // LocalMap，确认结算后需要走正式 Apply 链切到 BattleHex surface；Manual 原地保留。
            var autoSettlement = world.Strategic.Participants.IsAutoSettlement;
            var resolved = StrategicEncounterResolveService.ResolveAndEnd(world);
                if (resolved.IsSuccess)
                {
                    _holding = false;
                    if (!world.Strategic.IsWorldTickFrozen)
                    {
                        // 恢复开战前 pause；开大地图不再二次强制暂停
                        session.IsPaused = savedPaused;
                        if (bootstrap != null)
                            bootstrap.ApplySavedSpeedMultiplier(savedSpeed);
                        bootstrap.WorldMapPanel?.NotifyAfterBattleResolved(world);
                        if (completeInPlace)
                        {
                            if (autoSettlement)
                            {
                                // Phase 5S-B2-3.3：real WORLD_COMBAT + Auto —— 结算确认后把 LocalMap
                                // 从 canonical PartyWorld 展开/切到 BattleHex authoritative surface。
                                // WorldMap 保持打开（后台准备 LocalMap）；禁止 WorldMap.Open /
                                // ReloadLocalMap / Rebuild 整图。Auto 此前未进入 Battle LocalMap，
                                // 因此不适用 Manual 的“原地保留”分支。
                                bootstrap.ApplyPartyWorldSitePresentation(closeWorldMap: false);
                                ShowToast("战斗已结束，世界时间已恢复。");
                            }
                            else
                            {
                                // Phase 5S：普通真实 LocalMap 手动战 —— 原地结束：清除
                                // Combat/PostBattle 状态、EndFreeze、恢复 pause/speed、
                                // 保留当前 LocalMap session 与现场位置。禁止 Open WorldMap /
                                // ApplyPartyWorldSitePresentation / ReloadLocalMap / Rebuild。
                                // Phase 5S-B2-3.1：battle context 已释放 → 立即把参战
                                // FormalArmy / Residual 转成普通 LocalMap population
                                // （保留战斗落点、不 teleport），并轻量刷新视图。
                                bootstrap?.RefreshLoadedStrategicPopulation();
                                ShowToast("战斗结束，世界时间已恢复。");
                            }
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

            var barY = box.y + 90f;
            var barW = box.width - 32f;
            var total = Mathf.Max(1, offer.PlayerPower + offer.EnemyPower);
            var pw = barW * (offer.PlayerPower / (float)total);
            GUI.color = new Color(0.35f, 0.72f, 0.42f, 0.9f);
            GUI.DrawTexture(new Rect(box.x + 16f, barY, pw, 14f), _px);
            GUI.color = new Color(0.78f, 0.32f, 0.28f, 0.9f);
            GUI.DrawTexture(new Rect(box.x + 16f + pw, barY, barW - pw, 14f), _px);
            GUI.color = Color.white;
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
                        _holding = true;
                        session.IsPaused = true;
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
                    var retreat = BattleRetreatService.ExecuteRetreat(session.World, session.PlayerParty);
                    _holding = false;
                    if (retreat.IsFailure)
                    {
                        ShowToast(retreat.Error.Message);
                    }
                    else
                    {
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
                }));

            var count = Mathf.Max(1, specs.Count);
            var slotW = (box.width - 40f) / count;
            for (var i = 0; i < specs.Count; i++)
            {
                var rect = new Rect(box.x + 16f + slotW * i, y, slotW, 32f);
                if (GUI.Button(rect, specs[i].Label))
                    specs[i].Invoke();
            }
        }

        /// <summary>BattleOffer 底部动作按钮描述：动作列表驱动布局，隐藏按钮后不残留槽位。</summary>
        readonly struct ButtonSpec
        {
            public ButtonSpec(string label, Action action)
            {
                Label = label;
                Action = action;
            }

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
            var gate = BattleManualEntryPolicy.ValidateManualEntry(world);
            if (gate.IsFailure)
            {
                ShowToast(gate.Error.Message);
                return;
            }

            // Local-origin 的可见目标与当前物理 surface 必须同一；此预检在任何 DeclareWar
            // side effect 前执行，解析分叉必须保留 Offer 让用户看见，而不是静默切图。
            if (offer.Origin == BattleOfferOrigin.LocalMapHostileAction)
            {
                if (!LoadedLocalMapBelongingQuery.TryResolveLoadedLocalMap(world, out var previousLoaded))
                {
                    ShowToast("无法解析当前已加载的 LocalMap surface。");
                    return;
                }
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

            if (offer.RequiresWarDeclaration)
            {
                var engagement = world.Strategic.PendingEngagement;
                if (engagement == null || !engagement.IsActive)
                {
                    ShowToast("接战状态已失效，无法宣战。");
                    return;
                }
                if (!world.Strategic.FormalArmies.TryGet(offer.DefenderArmyId, out var defender) ||
                    defender == null)
                {
                    ShowToast("目标军团已不存在，无法宣战。");
                    return;
                }

                var currentPlayerFaction = world.Strategic.PlayerFactionId ?? string.Empty;
                var defenderFaction = defender.FactionId ?? string.Empty;
                if (!string.Equals(
                        currentPlayerFaction,
                        offer.PendingWarAttackerFactionId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        defenderFaction,
                        offer.PendingWarDefenderFactionId,
                        StringComparison.Ordinal))
                {
                    ShowToast("宣战方/目标阵营已变化，请重新发起。");
                    return;
                }
                if (string.Equals(currentPlayerFaction, defenderFaction, StringComparison.Ordinal))
                {
                    ShowToast("不能攻击同阵营单位。");
                    return;
                }
                var stance = world.Strategic.Diplomacy?.GetStance(currentPlayerFaction, defenderFaction) ??
                             FactionStance.Neutral;
                if (stance == FactionStance.Friendly)
                {
                    ShowToast("该阵营为友好关系，不能宣战。");
                    return;
                }

                if (!StrategicMilitaryAggressionService.TryEscalateToWar(
                        world,
                        currentPlayerFaction,
                        defenderFaction,
                        out var warReason))
                {
                    ShowToast("宣战失败：" + warReason);
                    return;
                }
            }

            var entered = EnterManualEncounter(session, offer.EncounterLocalMapId, offer.ArmyStackId);
            if (entered.IsFailure)
            {
                ShowToast(entered.Error.Message);
                return;
            }
            session.World.Strategic.ClearBattleOffer();
            session.World.Strategic.PendingEngagement.Clear();
            StrategicClockFreezeService.BeginOrPromote(
                session.World,
                StrategicClockFreezeReason.ManualEncounter);
            session.IsPaused = false;
            _holding = false;
        }

        Result EnterManualEncounter(
            PlayableHostSession session,
            string localMapId,
            string armyStackId)
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
            if (worldCombat)
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

            BattleOfferService.RefreshOfferPowerLabels(session.World);
            // 进场前再钉一次：追击接战窗也可能漏掉半径内已倒下的同伴
            BattleOfferService.PromoteInRangeIncapacitatedToMandatory(
                session.World, session.World.Strategic.Participants);
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
            StrategicPursuitService.ClearPursuitForEngagedKeepEnRoute(session.World, engaged);
            // Phase 5S：冻结本场 Manual Battle 的地点解析类别（真实 LocalMap 或 ExplicitEncounterMap）。
            session.World.Strategic.Participants.LocalMapResolutionKind = worldCombat
                ? worldResolution.Kind
                : BattleLocalMapResolutionKind.ExplicitEncounterMap;
            if (worldCombat)
                session.World.Strategic.Participants.EncounterLocalMapId = worldResolution.LocalMapId;
            // Phase 5S-B2-3.2：Manual Battle 入场 = 所有实际参战战略单位（PlayerParty + 全部
            // 参战 FormalArmy）正式 commit 到 BattleAnchorHex。这是正式改变旧的
            // 「Active 不 teleport / PlayerParty 保持 SupportArea」policy —— 选择 Manual Battle
            // 本身就代表 PlayerParty 从 SupportArea 正式加入 BattleHex。
            // Friendly battle presentation 不再在此处提前执行，移入 map-loaded assembly 阶段
            // （PlayableHostBootstrap.ApplyPartyWorldSitePresentation 的 PlayerParty materialize
            // 之后、enemy ApplyPending 之前）。
            if (worldCombat)
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
            session.World.PartyWorld.LocalMapId = map;
            if (worldCombat)
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
            if (session != null)
                session.PreferredMapLayoutId = map;

            // 进战场：大地图必须关（与 Open 门禁一致）
            bootstrap.WorldMapPanel?.Close();
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
