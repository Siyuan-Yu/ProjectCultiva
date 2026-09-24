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
            y = DrawQuestSocial(session,selection,x,y,width,body);
            var schedules = session.World.ScheduledContentEvents;
            GUI.Label(new Rect(x, y, width, lineH), "Scheduled Content Events · pending " + schedules.Pending.Count +
                " · 已履约 " + session.World.ContentCounters.Get("acceptance:delayed_event_presented"), body);
            y += lineH;
            foreach (var item in schedules.OrderedPending())
            {
                var remaining = item.ExecuteTick > session.World.Tick.Value ? item.ExecuteTick - session.World.Tick.Value : 0;
                var info = item.InstanceId + " / " + item.EventId + "\nExecuteTick=" + item.ExecuteTick +
                    " remaining=" + remaining + " ticks / " + ((double)remaining / XianXia.Core.Domain.Time.WorldTick.TicksPerDay).ToString("0.###") +
                    " days\nActor=" + item.ActorEntityId + " Target=" + item.TargetEntityId + " Issuer=" + item.IssuerEntityId +
                    "\nOpportunity=" + item.OpportunityInstanceId + " Key=" + item.TargetKey;
                var height = body.CalcHeight(new GUIContent(info), width);
                GUI.Label(new Rect(x, y, width, height), info, body); y += height + 4f;
            }
            if (!string.IsNullOrEmpty(schedules.LastDiagnostic))
            {
                var height = body.CalcHeight(new GUIContent(schedules.LastDiagnostic), width);
                GUI.Label(new Rect(x, y, width, height), schedules.LastDiagnostic, body); y += height + 4f;
            }

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

        float DrawQuestSocial(PlayableHostSession session, HostSelectionController selection, float x,float y,float width,GUIStyle body)
        {
            GUI.Label(new Rect(x,y,width,20), "SOCIAL-QUEST-01（关系方向：NPC → 当前 Active，门槛 20）",body); y += 24;
            foreach (var suffix in new[] { "cave", "cave_b", "general" })
            {
                var id = "base:quest_social01_" + suffix;
                if (GUI.Button(new Rect(x,y,width,24), "接取：" + (suffix == "general" ? "普通对照任务" : suffix == "cave" ? "秘境任务 A" : "秘境任务 B")))
                {
                    var result = new QuestService().TryStart(session.World,id,session.PlayerParty.ActiveCharacterId);
                    _sectionStatus = result.IsSuccess ? "已接取 " + id : result.Error.ToString();
                }
                y += 27;
            }
            if (GUI.Button(new Rect(x,y,width,24),"选中 NPC → Active 好感设为 19（不足）")) SetSocialScore(session,selection,19,false); y += 27;
            if (GUI.Button(new Rect(x,y,width,24),"选中 NPC → Active 好感设为 20（达标）")) SetSocialScore(session,selection,20,false); y += 27;
            if (GUI.Button(new Rect(x,y,width,24),"仅 Active → 选中 NPC 好感设为 30（反向对照）")) SetSocialScore(session,selection,30,true); y += 27;
            var actor = session.PlayerParty.ActiveCharacterId;
            var target = selection != null && selection.State.Count > 0 ? selection.State.SelectedIds[0] : EntityId.None;
            var text = "Actor=" + actor + " Target=" + target + " NPC→Actor=" + session.World.Relationships.Score(target,actor) + "\n";
            foreach (var q in session.World.Quests.Runtime.Values)
                if (session.World.Quests.TryGetSpec(q.QuestId,out var spec) && (spec.IsSecretRealm || q.QuestId == "base:quest_social01_general"))
                    text += q.QuestInstanceId + " | " + spec.QuestKind + " | " + q.Status + "\n";
            foreach (var b in session.World.QuestCompanions.Bindings.Values)
                text += "NPC=" + b.CompanionEntityId + " Quest=" + b.QuestInstanceId + "\nOriginalSquad=" + b.OriginalSquadId + " | " + b.State + "\n";
            var height = body.CalcHeight(new GUIContent(text),width);
            GUI.Label(new Rect(x,y,width,height),text,body);
            return y + height + 5;
        }

        void SetSocialScore(PlayableHostSession session,HostSelectionController selection,int score,bool reverse)
        {
            var actor = session.PlayerParty.ActiveCharacterId;
            var target = selection != null && selection.State.Count > 0 ? selection.State.SelectedIds[0] : EntityId.None;
            if (actor.IsNone || target == actor || !session.World.Entities.TryGet(target,out var npc) ||
                (npc.Tags & XianXia.Core.Entities.EntityTag.Npc) == 0)
            { _sectionStatus = "请先选中一名真实 NPC，并保持合法 Active。"; return; }
            var from = reverse ? actor : target; var to = reverse ? target : actor;
            var result = new XianXia.Core.Social.RelationshipService().Record(session.World,from,to,score-session.World.Relationships.Score(from,to),"social_quest01_acceptance");
            _sectionStatus = result.IsSuccess ? "好感已通过 RelationshipLedger 更新。" : result.Error.ToString();
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
