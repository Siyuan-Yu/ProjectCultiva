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

        public string SectionStatus => _sectionStatus;

        public void ResetSessionState() => _sectionStatus = string.Empty;

        public float Draw(
            PlayableHostBootstrap bootstrap,
            float x,
            float y,
            float width,
            GUIStyle body)
        {
            var lineH = 22f;
            GUI.Label(new Rect(x, y, width, lineH), "高级诊断 · Core 时间（不模拟逐帧 Host 移动）", body);
            y += lineH + 4f;

            var world = bootstrap?.Session?.World;
            var currentTick = world?.Tick.Value ?? 0UL;
            GUI.Label(new Rect(x, y, width, lineH),
                "当前：" + FormatTime(currentTick) + "　次日目标：" +
                FormatTime(NextMidnight(currentTick)) + "　实际将推进：" +
                FormatHours(NextMidnight(currentTick) - currentTick), body);
            y += lineH;
            GUI.Label(new Rect(x, y, width, lineH * 2f),
                "不能用 Core 跳时证明取食、回床、搬运、追击或完整照料日；行为验收请使用 1x / 2x / 5x / 20x 正常帧循环。",
                body);
            y += lineH * 2f + 2f;
            if (HasHostDependentWork(world))
            {
                GUI.Label(new Rect(x, y, width, lineH * 2f),
                    "警告：当前存在依赖 Host 表现移动的搬运／押送／建设，Core 跳时不能完成其路径。", body);
                y += lineH * 2f;
            }

            if (GUI.Button(new Rect(x, y, 150f, 24f), "Core 步进 1 Tick"))
            {
                if (CanAdvanceCore(bootstrap, out var reason))
                {
                    var before = bootstrap.Session.World.Tick.Value;
                    bootstrap.StepTick();
                    _sectionStatus = bootstrap.Session.World.Tick.Value > before
                        ? "成功：Core 步进 1 Tick。"
                        : "失败：Core Tick 未推进。";
                }
                else _sectionStatus = "失败：" + reason;
            }

            y += 28f;
            GUI.Label(new Rect(x, y, 52f, lineH), "Tick 数");
            GUI.SetNextControlName("LevelTester.TickCount");
            _advanceTicksText = GUI.TextField(new Rect(x + 56f, y, 56f, 22f), _advanceTicksText);
            if (GUI.Button(new Rect(x + 120f, y, 110f, 24f), "Core 推进"))
                AdvanceTicks(bootstrap);

            y += 28f;
            if (GUI.Button(new Rect(x, y, 190f, 24f), "推进至次日零点〔仅 Core 数据〕"))
                AdvanceDays(bootstrap, 1);
            if (GUI.Button(new Rect(x + 196f, y, 120f, 24f), "至第 3 日零点"))
                AdvanceDays(bootstrap, 3);
            if (GUI.Button(new Rect(x + 322f, y, 120f, 24f), "至第 7 日零点"))
                AdvanceDays(bootstrap, 7);

            y += 28f;
            GUI.Label(new Rect(x, y, width, lineH), "速度（使用 HostDebugHud 状态）：");
            y += lineH;
            var speed = bootstrap != null ? bootstrap.EffectiveSpeedMultiplier() : 1;
            var bx = x;
            if (GUI.Button(new Rect(bx, y, 40f, 24f), "1x"))
            {
                bootstrap?.SetSpeedMultiplier(1);
                _sectionStatus = "速度 1x";
            }

            bx += 44f;
            if (GUI.Button(new Rect(bx, y, 40f, 24f), "2x"))
            {
                bootstrap?.SetSpeedMultiplier(2);
                _sectionStatus = "速度 2x";
            }

            bx += 44f;
            if (GUI.Button(new Rect(bx, y, 40f, 24f), "5x"))
            {
                bootstrap?.SetSpeedMultiplier(5);
                _sectionStatus = "速度 5x";
            }

            bx += 44f;
            if (GUI.Button(new Rect(bx, y, 44f, 24f), "20x"))
            {
                bootstrap?.SetSpeedMultiplier(20);
                _sectionStatus = "速度 20x";
            }

            bx += 52f;
            GUI.Label(new Rect(bx, y + 2f, 80f, lineH), "当前 " + speed + "x");

            y += 28f;
            if (!string.IsNullOrEmpty(_sectionStatus))
            {
                GUI.Label(new Rect(x, y, width, lineH), _sectionStatus, body);
                y += lineH;
            }

            return y;
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
