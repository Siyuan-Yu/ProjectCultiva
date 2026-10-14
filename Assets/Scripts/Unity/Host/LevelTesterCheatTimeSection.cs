using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Content;
using XianXia.Core.Domain.Time;
using XianXia.Core.Npc;

namespace XianXia.Unity.Host
{
    /// <summary>LevelTester Cheat · Time / Simulation。</summary>
    public sealed class LevelTesterCheatTimeSection
    {
        public const int MaxAdvanceTicks = 500;

        readonly ContentDebugService _contentDebug = new ContentDebugService();
        string _advanceTicksText = "16";
        string _sectionStatus = string.Empty;
        readonly Dictionary<string, string> _toolStatuses = new Dictionary<string, string>();
        string _statusToolId = string.Empty;

        public string SectionStatus => _sectionStatus;

        public void ResetSessionState()
        {
            _sectionStatus = string.Empty;
            _toolStatuses.Clear();
            _statusToolId = string.Empty;
        }

        public float DrawTool(string toolId, PlayableHostBootstrap bootstrap,
            float x, float y, float width, GUIStyle body)
        {
            ActivateToolStatus(toolId);
            var world = bootstrap?.Session?.World;
            var currentTick = world?.Tick.Value ?? 0UL;
            if (toolId == "time.speed")
            {
                GUI.Label(new Rect(x, y, width, 38f),
                    "使用正常 Host 帧循环；不会暂停 NPC 或世界模拟。当前 " +
                    (bootstrap != null ? bootstrap.EffectiveSpeedMultiplier() : 1) + "x", body);
                y += 44f;
                var values = new[] { 1, 2, 5, 20 };
                for (var i = 0; i < values.Length; i++)
                    if (GUI.Button(new Rect(x + i * 58f, y, 52f, 26f), values[i] + "x"))
                    { bootstrap?.SetSpeedMultiplier(values[i]); _sectionStatus = "速度已设为 " + values[i] + "x。"; }
            }
            else if (toolId == "time.core-ticks")
            {
                GUI.Label(new Rect(x, y, width, 38f),
                    "仅推进 Core 数据，不能证明取食、回床、搬运、追击等 Host 路径。", body);
                y += 44f;
                if (HasHostDependentWork(world))
                {
                    GUI.Label(new Rect(x, y, width, 38f), "警告：当前存在依赖 Host 表现移动的搬运／押送／建设，Core 跳时不能完成其路径。", body);
                    y += 42f;
                }
                if (GUI.Button(new Rect(x, y, 150f, 26f), "Core 步进 1 Tick"))
                {
                    if (CanAdvanceCore(bootstrap, out var reason))
                    { var before = currentTick; bootstrap.StepTick(); _sectionStatus = bootstrap.Session.World.Tick.Value > before ? "成功：Core 步进 1 Tick。" : "失败：Tick 未推进。"; }
                    else _sectionStatus = "失败：" + reason;
                }
                GUI.Label(new Rect(x + 160f, y + 3f, 52f, 20f), "Tick 数");
                GUI.SetNextControlName("LevelTester.TickCount");
                _advanceTicksText = GUI.TextField(new Rect(x + 214f, y, 60f, 24f), _advanceTicksText);
                if (GUI.Button(new Rect(x + 280f, y, 100f, 26f), "批量推进")) AdvanceTicks(bootstrap);
            }
            else
            {
                GUI.Label(new Rect(x, y, width, 42f),
                    "当前：" + FormatTime(currentTick) + "　次日目标：" + FormatTime(NextMidnight(currentTick)) +
                    "　实际将推进：" + FormatHours(NextMidnight(currentTick) - currentTick), body);
                y += 48f;
                if (HasHostDependentWork(world))
                {
                    GUI.Label(new Rect(x, y, width, 38f), "警告：当前存在依赖 Host 表现移动的搬运／押送／建设，Core 跳时不能完成其路径。", body);
                    y += 42f;
                }
                if (GUI.Button(new Rect(x, y, 190f, 26f), "推进至次日零点")) AdvanceDays(bootstrap, 1);
                if (GUI.Button(new Rect(x + 196f, y, 120f, 26f), "至第 3 日")) AdvanceDays(bootstrap, 3);
                if (GUI.Button(new Rect(x + 322f, y, 120f, 26f), "至第 7 日")) AdvanceDays(bootstrap, 7);
            }
            y += 34f;
            GUI.Label(new Rect(x, y, width, 58f), string.IsNullOrEmpty(_sectionStatus) ? "尚未执行。" : _sectionStatus, body);
            _toolStatuses[toolId] = _sectionStatus ?? string.Empty;
            return y + 62f;
        }

        void ActivateToolStatus(string toolId)
        {
            if (string.Equals(_statusToolId, toolId, System.StringComparison.Ordinal)) return;
            if (!string.IsNullOrEmpty(_statusToolId)) _toolStatuses[_statusToolId] = _sectionStatus ?? string.Empty;
            _statusToolId = toolId ?? string.Empty;
            _sectionStatus = _toolStatuses.TryGetValue(_statusToolId, out var status) ? status : string.Empty;
        }

        void AdvanceTicks(PlayableHostBootstrap bootstrap)
        {
            var session = bootstrap?.Session;
            if (!CanAdvanceCore(bootstrap, out var reason))
            {
                _sectionStatus = "失败：" + reason;
                return;
            }

            if (!int.TryParse(_advanceTicksText, out var ticks) || ticks <= 0)
            {
                _sectionStatus = "Tick 数须为正整数。";
                return;
            }

            ticks = Mathf.Clamp(ticks, 1, MaxAdvanceTicks);
            _advanceTicksText = ticks.ToString();
            for (var i = 0; i < ticks; i++)
                bootstrap.StepTick();
            bootstrap.FlushLoadedDestinationArrivals();
            _sectionStatus = "成功：Core 已推进 " + ticks + " Tick。";
        }

        void AdvanceDays(PlayableHostBootstrap bootstrap, int days)
        {
            var session = bootstrap?.Session;
            if (!CanAdvanceCore(bootstrap, out var reason))
            {
                _sectionStatus = "失败：" + reason;
                return;
            }

            var before = session.World.Tick.Value;
            var target = ((before / (ulong)WorldTick.TicksPerDay) + (ulong)days) *
                         (ulong)WorldTick.TicksPerDay;
            var r = _contentDebug.AdvanceDays(session.Loop, days);
            var advanced = session.World.Tick.Value - before;
            _sectionStatus = r.IsSuccess
                ? "成功：已推进至 " + FormatTime(target) + "，实际 " + FormatHours(advanced) + "〔仅 Core 数据〕。"
                : "失败：" + r.Error;
        }

        static bool CanAdvanceCore(PlayableHostBootstrap bootstrap, out string reason)
        {
            reason = string.Empty;
            var session = bootstrap?.Session;
            if (session == null || !session.IsInitialized || session.Loop == null)
            { reason = "会话未就绪。"; return false; }
            if (session.ModalHardPaused)
            { reason = "正式 Modal 正在冻结世界。"; return false; }
            if (session.World.ContentEvents.HasActive ||
                XianXia.Core.World.Strategic.StrategicClockFreezeService.IsWorldTickFrozen(session.World))
            { reason = "正式事件/遭遇正在冻结世界。"; return false; }
            if (session.InitialBootstrapPending || session.PendingRestoredStrategicSnapshot != null)
            { reason = "场景／存档过渡尚未完成。"; return false; }
            return true;
        }

        static bool HasHostDependentWork(XianXia.Core.Simulation.SimulationWorld world)
        {
            if (world == null) return false;
            foreach (var pair in world.Civilians.All)
            {
                var state = pair.Value;
                if (state != null && (state.RescueCarrying || !state.RescueTargetId.IsNone ||
                                      state.Disposition == CivilianDisposition.CapturedEscorted))
                    return true;
            }
            return world.CivilianConstructionJobs.Jobs.Count > 0;
        }

        static ulong NextMidnight(ulong tick) =>
            ((tick / (ulong)WorldTick.TicksPerDay) + 1UL) * (ulong)WorldTick.TicksPerDay;

        static string FormatTime(ulong tick)
        {
            var day = tick / (ulong)WorldTick.TicksPerDay;
            var minute = (tick % (ulong)WorldTick.TicksPerDay) * (ulong)WorldTick.GameMinutesPerTick;
            return "Day " + day + " " + (minute / 60UL).ToString("00") + ":" + (minute % 60UL).ToString("00");
        }

        static string FormatHours(ulong ticks) =>
            (ticks * (ulong)WorldTick.GameMinutesPerTick / 60d).ToString("0.##") + " 小时";
    }
}
