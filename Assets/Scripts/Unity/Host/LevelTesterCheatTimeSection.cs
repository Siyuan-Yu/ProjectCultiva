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
            GUI.Label(new Rect(x, y, width, lineH), "Time / Simulation", body);
            y += lineH + 4f;

            if (GUI.Button(new Rect(x, y, 100f, 24f), "Step 1 Tick"))
            {
                bootstrap?.StepTick();
                _sectionStatus = bootstrap != null && bootstrap.Session.IsInitialized
                    ? "Stepped 1 tick."
                    : "Session not ready.";
            }

            y += 28f;
            GUI.Label(new Rect(x, y, 40f, lineH), "Ticks");
            _advanceTicksText = GUI.TextField(new Rect(x + 44f, y, 56f, 22f), _advanceTicksText);
            if (GUI.Button(new Rect(x + 108f, y, 80f, 24f), "Advance"))
                AdvanceTicks(bootstrap);

            y += 28f;
            if (GUI.Button(new Rect(x, y, 120f, 24f), "Advance +1 Day"))
                AdvanceOneDay(bootstrap);

            y += 28f;
            GUI.Label(new Rect(x, y, width, lineH), "Speed (uses HostDebugHud state):");
            y += lineH;
            var speed = bootstrap != null ? bootstrap.EffectiveSpeedMultiplier() : 1;
            var bx = x;
            if (GUI.Button(new Rect(bx, y, 40f, 24f), "1x"))
            {
                bootstrap?.SetSpeedMultiplier(1);
                _sectionStatus = "Speed 1x";
            }

            bx += 44f;
            if (GUI.Button(new Rect(bx, y, 40f, 24f), "2x"))
            {
                bootstrap?.SetSpeedMultiplier(2);
                _sectionStatus = "Speed 2x";
            }

            bx += 44f;
            if (GUI.Button(new Rect(bx, y, 40f, 24f), "5x"))
            {
                bootstrap?.SetSpeedMultiplier(5);
                _sectionStatus = "Speed 5x";
            }

            bx += 44f;
            if (GUI.Button(new Rect(bx, y, 44f, 24f), "20x"))
            {
                bootstrap?.SetSpeedMultiplier(20);
                _sectionStatus = "Speed 20x";
            }

            bx += 52f;
            GUI.Label(new Rect(bx, y + 2f, 80f, lineH), "now " + speed + "x");

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
                _sectionStatus = "Session not ready.";
                return;
            }

            if (!int.TryParse(_advanceTicksText, out var ticks) || ticks <= 0)
            {
                _sectionStatus = "Ticks must be a positive integer.";
                return;
            }

            ticks = Mathf.Clamp(ticks, 1, MaxAdvanceTicks);
            _advanceTicksText = ticks.ToString();
            for (var i = 0; i < ticks; i++)
                bootstrap.StepTick();
            bootstrap.FlushLoadedDestinationArrivals();
            _sectionStatus = "Advanced " + ticks + " tick(s).";
        }

        void AdvanceOneDay(PlayableHostBootstrap bootstrap)
        {
            var session = bootstrap?.Session;
            if (session == null || !session.IsInitialized || session.Loop == null)
            {
                _sectionStatus = "Session not ready.";
                return;
            }

            var r = _contentDebug.AdvanceDays(session.Loop, 1);
            _sectionStatus = r.IsSuccess ? "Advanced +1 day." : r.Error.ToString();
        }
    }
}
