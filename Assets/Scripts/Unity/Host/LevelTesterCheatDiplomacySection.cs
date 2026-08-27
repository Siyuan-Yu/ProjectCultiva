using System.Collections.Generic;
using System.Text;
using UnityEngine;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>LevelTester Cheat · Diplomacy acceptance。</summary>
    public sealed class LevelTesterCheatDiplomacySection
    {
        readonly List<string> _factions = new List<string>(16);
        int _factionAIndex;
        int _factionBIndex;
        int _overlordIndex;
        int _vassalIndex;
        string _sectionStatus = string.Empty;

        public string SectionStatus => _sectionStatus;

        public float Draw(
            PlayableHostBootstrap bootstrap,
            float x,
            float y,
            float width,
            GUIStyle body)
        {
            var lineH = 18f;
            var session = bootstrap?.Session;
            var world = session?.World;

            GUI.Label(new Rect(x, y, width, lineH), "Diplomacy (Development Acceptance)", body);
            y += lineH + 4f;

            if (world == null || !session.IsInitialized)
            {
                GUI.Label(new Rect(x, y, width, lineH), "Session not ready.");
                return y + lineH;
            }

            StrategicAcceptanceInspector.CollectKnownFactionIds(world, _factions);
            ClampIndices();

            y = DrawReadOnlyStatus(world, x, y, width, lineH, body);
            y += 6f;

            if (_factions.Count < 2)
            {
                GUI.Label(new Rect(x, y, width, lineH), "Need at least 2 factions.", body);
                return y + lineH;
            }

            y = DrawFactionPicker(x, y, width, lineH, body, "Faction A", ref _factionAIndex, _factions);
            y = DrawFactionPicker(x, y, width, lineH, body, "Faction B", ref _factionBIndex, _factions);
            if (GUI.Button(new Rect(x, y, width, 24f), "Declare War"))
            {
                var result = StrategicAcceptanceCommands.TryDeclareWar(
                    world, _factions[_factionAIndex], _factions[_factionBIndex]);
                _sectionStatus = result.IsSuccess
                    ? "OK: Declare War"
                    : "FAIL: " + result.Error.Message;
            }

            y += 28f;
            if (GUI.Button(new Rect(x, y, width, 24f), "Create Alliance"))
            {
                var result = StrategicAcceptanceCommands.TryFormAlliance(
                    world, _factions[_factionAIndex], _factions[_factionBIndex]);
                _sectionStatus = result.IsSuccess
                    ? "OK: Create Alliance"
                    : "FAIL: " + result.Error.Message;
            }

            y += 28f;
            y = DrawFactionPicker(x, y, width, lineH, body, "Overlord", ref _overlordIndex, _factions);
            y = DrawFactionPicker(x, y, width, lineH, body, "Vassal", ref _vassalIndex, _factions);
            if (GUI.Button(new Rect(x, y, width, 24f), "Create Vassalage (TEST)"))
            {
                var result = StrategicAcceptanceCommands.TryBindVassalage(
                    world, _factions[_overlordIndex], _factions[_vassalIndex]);
                _sectionStatus = result.IsSuccess
                    ? "OK: Create Vassalage"
                    : "FAIL: " + result.Error.Message;
            }

            y += 28f;
            if (!string.IsNullOrEmpty(_sectionStatus))
            {
                GUI.Label(new Rect(x, y, width, lineH * 2f), _sectionStatus, body);
                y += lineH * 2f;
            }

            return y;
        }

        float DrawReadOnlyStatus(
            SimulationWorld world,
            float x,
            float y,
            float width,
            float lineH,
            GUIStyle body)
        {
            var sb = new StringBuilder(256);
            sb.AppendLine("Factions: " + _factions.Count);
            if (world.Strategic?.Wars?.All != null)
            {
                foreach (var kv in world.Strategic.Wars.All)
                {
                    var war = kv.Value;
                    if (war == null || !war.Active)
                        continue;
                    sb.AppendLine("War " + war.WarId);
                }
            }

            GUI.Label(new Rect(x, y, width, lineH * 4f), sb.ToString(), body);
            return y + lineH * 4f;
        }

        static float DrawFactionPicker(
            float x,
            float y,
            float width,
            float lineH,
            GUIStyle body,
            string label,
            ref int index,
            List<string> factions)
        {
            if (factions == null || factions.Count == 0)
                return y + 24f;
            GUI.Label(new Rect(x, y, 80f, lineH), label, body);
            if (GUI.Button(new Rect(x + 82f, y, 24f, 22f), "<"))
                index = (index + factions.Count - 1) % factions.Count;
            GUI.Label(new Rect(x + 110f, y, width - 150f, lineH),
                StrategicFactionCatalog.DisplayName(factions[index]), body);
            if (GUI.Button(new Rect(x + width - 28f, y, 24f, 22f), ">"))
                index = (index + 1) % factions.Count;
            return y + 24f;
        }

        void ClampIndices()
        {
            if (_factions.Count == 0)
            {
                _factionAIndex = _factionBIndex = 0;
                _overlordIndex = _vassalIndex = 0;
                return;
            }

            _factionAIndex = Mathf.Clamp(_factionAIndex, 0, _factions.Count - 1);
            _factionBIndex = Mathf.Clamp(_factionBIndex, 0, _factions.Count - 1);
            _overlordIndex = Mathf.Clamp(_overlordIndex, 0, _factions.Count - 1);
            _vassalIndex = Mathf.Clamp(_vassalIndex, 0, _factions.Count - 1);
        }
    }
}
