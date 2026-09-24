using UnityEngine;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Opportunity;

namespace XianXia.Unity.Host
{
    /// <summary>LevelTester Cheat · Content flags / events。</summary>
    public sealed class LevelTesterCheatContentSection
    {
        readonly ContentDebugService _debug = new ContentDebugService();
        string _flagInput = "story:debug_flag";
        string _eventInput = "base:event_herb_whisper";
        string _dump = string.Empty;
        string _sectionStatus = string.Empty;

        public string SectionStatus => _sectionStatus;

        public float Draw(
            PlayableHostBootstrap bootstrap,
            HostSelectionController selection,
            float x,
            float y,
            float width,
            GUIStyle body)
        {
            var lineH = 18f;
            GUI.Label(new Rect(x, y, width, lineH), "内容", body);
            y += lineH + 4f;

            var session = bootstrap?.Session;
            if (session == null || !session.IsInitialized)
            {
                GUI.Label(new Rect(x, y, width, lineH), "会话未就绪。");
                return y + lineH;
            }

            RefreshDump(session, selection);

            GUI.Label(new Rect(x, y, 40f, lineH), "标记");
            _flagInput = GUI.TextField(new Rect(x + 44f, y, width - 200f, 22f), _flagInput);
            if (GUI.Button(new Rect(x + width - 148f, y, 68f, 22f), "设置"))
                RunFlag(session, selection, true);
            if (GUI.Button(new Rect(x + width - 74f, y, 68f, 22f), "清除"))
                RunFlag(session, selection, false);
            y += 26f;

            GUI.Label(new Rect(x, y, 40f, lineH), "事件");
            _eventInput = GUI.TextField(new Rect(x + 44f, y, width - 100f, 22f), _eventInput);
            if (GUI.Button(new Rect(x + width - 90f, y, 88f, 22f), "强制呈现"))
                ForceEvent(session, selection);
            y += 26f;

            if (GUI.Button(new Rect(x, y, width, 24f), "QUEST-INSTANCE-01：生成两名同模板临时行商"))
            {
                var result = WorldOpportunityDriver.SpawnAcceptanceInstances(
                    session.World, "base:world_opportunity_event02_temporary_merchant", 2, out var summary);
                _sectionStatus = result.IsSuccess ? "已生成：" + summary : "失败：" + result.Error;
            }
            y += 28f;

            GUI.Label(new Rect(x, y, width, lineH), "DYNAMIC-DISCOVERY-01", body); y += lineH + 2f;
            if (GUI.Button(new Rect(x, y, width, 24f), "生成 Hidden 石碑"))
                SpawnDynamic(session, "base:world_opportunity_dynamic_hidden_stele");
            y += 27f;
            if (GUI.Button(new Rect(x, y, width, 24f), "生成 PublicNotice 包裹"))
                SpawnDynamic(session, "base:world_opportunity_dynamic_public_package");
            y += 27f;
            if (GUI.Button(new Rect(x, y, width, 24f), "生成 WorldVisible 灵草"))
                SpawnDynamic(session, "base:world_opportunity_dynamic_visible_herb");
            y += 27f;
            if (GUI.Button(new Rect(x, y, width, 24f), "清理 DYNAMIC-DISCOVERY-01 验收物体"))
            {
                var count = WorldOpportunityDriver.ClearAcceptanceInstances(session.World,
                    "base:world_opportunity_dynamic_hidden_stele",
                    "base:world_opportunity_dynamic_public_package",
                    "base:world_opportunity_dynamic_visible_herb");
                _sectionStatus = "已清理 " + count + " 个动态机会物体。";
            }
            y += 28f;

            if (GUI.Button(new Rect(x, y, 100f, 24f), "刷新 Dump"))
                RefreshDump(session, selection);
            y += 28f;

            if (!string.IsNullOrEmpty(_sectionStatus))
            {
                GUI.Label(new Rect(x, y, width, lineH * 2f), _sectionStatus, body);
                y += lineH * 2f;
            }

            var dumpH = Mathf.Min(120f, body.CalcHeight(new GUIContent(_dump), width));
            GUI.TextArea(new Rect(x, y, width, dumpH), _dump);
            y += dumpH + 4f;
            return y;
        }

        void RunFlag(PlayableHostSession session, HostSelectionController selection, bool set)
        {
            var subject = FocusId(session, selection);
            var r = set
                ? _debug.SetFlag(session.World, _flagInput, subject)
                : _debug.ClearFlag(session.World, _flagInput, subject);
            _sectionStatus = r.IsSuccess
                ? (set ? "成功：标记已设置。" : "成功：标记已清除。")
                : "失败：" + r.Error;
            RefreshDump(session, selection);
        }

        void SpawnDynamic(PlayableHostSession session, string definitionId)
        {
            var result = WorldOpportunityDriver.SpawnAcceptanceInstances(session.World, definitionId, 1, out var summary);
            _sectionStatus = result.IsSuccess ? "已生成：" + summary : "失败：" + result.Error;
        }

        void ForceEvent(PlayableHostSession session, HostSelectionController selection)
        {
            var r = _debug.ForcePresentEvent(session.World, FocusId(session, selection), _eventInput);
            _sectionStatus = r.IsSuccess ? "成功：事件已呈现。" : "失败：" + r.Error;
            RefreshDump(session, selection);
        }

        void RefreshDump(PlayableHostSession session, HostSelectionController selection)
        {
            _dump = _debug.Dump(session.World, FocusId(session, selection));
        }

        static EntityId FocusId(PlayableHostSession session, HostSelectionController selection)
        {
            if (selection != null && selection.State.Count > 0)
                return selection.State.SelectedIds[0];
            if (session?.CharacterIds != null && session.CharacterIds.Count > 0)
                return session.CharacterIds[0];
            return EntityId.None;
        }
    }
}
