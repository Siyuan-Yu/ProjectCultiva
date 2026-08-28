using UnityEngine;
using XianXia.Core.Content;

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

        public float Draw(
            PlayableHostBootstrap bootstrap,
            float x,
            float y,
            float width,
            GUIStyle body)
        {
            var lineH = 22f;
            GUI.Label(new Rect(x, y, width, lineH), "时间 / 模拟", body);
            y += lineH + 4f;

            if (GUI.Button(new Rect(x, y, 100f, 24f), "步进 1 Tick"))
            {
                bootstrap?.StepTick();
                _sectionStatus = bootstrap != null && bootstrap.Session.IsInitialized
                    ? "已步进 1 Tick。"
                    : "会话未就绪。";
            }

            y += 28f;
            GUI.Label(new Rect(x, y, 52f, lineH), "Tick 数");
            _advanceTicksText = GUI.TextField(new Rect(x + 56f, y, 56f, 22f), _advanceTicksText);
            if (GUI.Button(new Rect(x + 120f, y, 68f, 24f), "推进"))
                AdvanceTicks(bootstrap);

            y += 28f;
            if (GUI.Button(new Rect(x, y, 120f, 24f), "推进 +1 天"))
                AdvanceOneDay(bootstrap);

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
            if (session == null || !session.IsInitialized || session.Loop == null)
            {
                _sectionStatus = "会话未就绪。";
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
            _sectionStatus = "已推进 " + ticks + " Tick。";
        }

        void AdvanceOneDay(PlayableHostBootstrap bootstrap)
        {
            var session = bootstrap?.Session;
            if (session == null || !session.IsInitialized || session.Loop == null)
            {
                _sectionStatus = "会话未就绪。";
                return;
            }

            var r = _contentDebug.AdvanceDays(session.Loop, 1);
            _sectionStatus = r.IsSuccess ? "已推进 +1 天。" : r.Error.ToString();
        }
    }
}
