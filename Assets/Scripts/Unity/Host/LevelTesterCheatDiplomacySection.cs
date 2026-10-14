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
        readonly Dictionary<string, string> _toolStatuses = new Dictionary<string, string>();
        string _statusToolId = string.Empty;

        public string SectionStatus => _sectionStatus;

        public void ResetSessionState()
        {
            _factions.Clear();
            _factionAIndex = _factionBIndex = _overlordIndex = _vassalIndex = 0;
            _sectionStatus = string.Empty;
            _toolStatuses.Clear();
            _statusToolId = string.Empty;
        }

        public float DrawTool(string toolId, PlayableHostBootstrap bootstrap,
            float x, float y, float width, GUIStyle body)
        {
            ActivateToolStatus(toolId);
            var world = bootstrap?.Session?.World;
            if (world == null || bootstrap.Session.IsInitialized == false)
            {
                GUI.Label(new Rect(x, y, width, 22f), "会话未就绪。", body);
                return y + 24f;
            }
            FactionDiplomacyOverviewQuery.CollectRuntimeFactionIds(world, _factions);
            ClampIndices();
            if (_factions.Count < 2)
            {
                GUI.Label(new Rect(x, y, width, 22f), "至少需要两个势力。", body);
                return y + 24f;
            }

            if (toolId == "diplomacy.vassal")
            {
                y = DrawFactionPicker(x, y, width, 18f, body, "宗主", ref _overlordIndex, _factions);
                y = DrawFactionPicker(x, y, width, 18f, body, "附庸", ref _vassalIndex, _factions);
                if (GUI.Button(new Rect(x, y, width, 26f), "建立附庸"))
                {
                    var ok = world.Strategic.Vassalages.TryBindVassalage(_factions[_vassalIndex], _factions[_overlordIndex]);
                    _sectionStatus = ok ? "成功：建立附庸。" : "失败：无法建立附庸。";
                }
            }
            else
            {
                y = DrawFactionPicker(x, y, width, 18f, body, "势力 A", ref _factionAIndex, _factions);
                y = DrawFactionPicker(x, y, width, 18f, body, "势力 B", ref _factionBIndex, _factions);
                var label = toolId == "diplomacy.war" ? "宣战" : "结盟";
                if (GUI.Button(new Rect(x, y, width, 26f), label))
                {
                    if (toolId == "diplomacy.war")
                    {
                        var result = WarGateService.DeclareWar(world, _factions[_factionAIndex], _factions[_factionBIndex]);
                        _sectionStatus = result.IsSuccess ? "成功：宣战。" : "失败：" + result.Error.Message;
                    }
                    else
                    {
                        var ok = world.Strategic.Alliances.FormAlliance(_factions[_factionAIndex], _factions[_factionBIndex], out _);
                        _sectionStatus = ok ? "成功：结盟。" : "失败：无法建立联盟。";
                    }
                }
            }
            y += 34f;
            GUI.Label(new Rect(x, y, width, 54f), string.IsNullOrEmpty(_sectionStatus) ? "尚未执行。" : _sectionStatus, body);
            _toolStatuses[toolId] = _sectionStatus ?? string.Empty;
            return y + 58f;
        }

        void ActivateToolStatus(string toolId)
        {
            if (string.Equals(_statusToolId, toolId, System.StringComparison.Ordinal)) return;
            if (!string.IsNullOrEmpty(_statusToolId)) _toolStatuses[_statusToolId] = _sectionStatus ?? string.Empty;
            _statusToolId = toolId ?? string.Empty;
            _sectionStatus = _toolStatuses.TryGetValue(_statusToolId, out var status) ? status : string.Empty;
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
