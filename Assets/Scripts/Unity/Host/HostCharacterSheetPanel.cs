using System.Collections.Generic;
using System.Text;
using UnityEngine;
using XianXia.Core.Attributes;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Npc;
using XianXia.Core.Schedule;
using XianXia.Core.Social;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// 人物本貌：属性／灵根／性格履历／活动倾向。打开时暂停。
    /// </summary>
    public sealed class HostCharacterSheetPanel : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] bool open;

        EntityId _subject = EntityId.None;
        bool _holdingPause;
        Vector2 _scroll;
        GUIStyle _title;
        GUIStyle _body;
        Texture2D _px;
        readonly List<(ScheduleActivity Activity, int Priority)> _tendencyScratch =
            new List<(ScheduleActivity, int)>(16);

        static readonly Color Parchment = new Color(0.92f, 0.86f, 0.74f, 0.98f);
        static readonly Color ParchmentDark = new Color(0.70f, 0.58f, 0.42f, 1f);

        static readonly AttributeId[] AttrOrder =
        {
            AttributeId.Physique, AttributeId.MaxHp, AttributeId.Attack, AttributeId.Defense, AttributeId.Speed,
            AttributeId.Stamina, AttributeId.SpiritSense, AttributeId.Comprehension,
            AttributeId.SpiritPower, AttributeId.Cultivation, AttributeId.MindState
        };

        static readonly SpiritRootKind[] RootOrder =
        {
            SpiritRootKind.Fire, SpiritRootKind.Metal, SpiritRootKind.Earth, SpiritRootKind.Wood,
            SpiritRootKind.Thunder, SpiritRootKind.Wind, SpiritRootKind.Ice, SpiritRootKind.Poison
        };

        public bool IsOpen => open;

        public void Bind(PlayableHostBootstrap host, HostSelectionController selection)
        {
            bootstrap = host;
            selectionController = selection;
        }

        public void ClearSessionState()
        {
            open = false;
            _subject = EntityId.None;
            _holdingPause = false;
            HostInputGate.Clear();
        }

        public void OpenFor(EntityId id)
        {
            if (id.IsNone) return;
            _subject = id;
            open = true;
            _scroll = Vector2.zero;
        }

        public void Close() => open = false;

        void Update()
        {
            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return;
            if (bootstrap.QuestJournal != null && bootstrap.QuestJournal.IsOpen)
            {
                open = false;
                ReleasePause();
                return;
            }

            if (open)
            {
                HostInputGate.BlockWorldCamera = true;
                HostInputGate.BlockWorldInteraction = true;
                if (!_holdingPause)
                {
                    bootstrap.Session.IsPaused = true;
                    _holdingPause = true;
                }
            }
            else
                ReleasePause();
        }

        void ReleasePause()
        {
            if (!_holdingPause) return;
            _holdingPause = false;
            if (bootstrap?.Session != null)
                bootstrap.Session.IsPaused = false;
            HostInputGate.Clear();
        }

        void OnGUI()
        {
            if (!open || bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return;
            EnsureStyles();
            if (_subject.IsNone ||
                !bootstrap.Session.World.Entities.TryGet(_subject, out var entity))
            {
                open = false;
                return;
            }

            var name = string.IsNullOrEmpty(entity.DisplayName) ? _subject.ToString() : entity.DisplayName;
            var w = Mathf.Min(640f, Screen.width - 40f);
            var h = Mathf.Min(520f, Screen.height - 40f);
            var rect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            HostUiHitTest.Block(rect);
            Fill(rect, Parchment);
            DrawFrame(rect, ParchmentDark);

            GUI.Label(new Rect(rect.x + 16f, rect.y + 12f, rect.width - 90f, 28f), "人物 · " + name, _title);
            if (HostImguiStyles.ParchmentBtn(new Rect(rect.xMax - 72f, rect.y + 10f, 56f, 28f), "关闭"))
                open = false;

            var body = new Rect(rect.x + 16f, rect.y + 48f, rect.width - 32f, rect.height - 64f);
            var text = BuildBody(entity);
            var viewH = Mathf.Max(body.height, _body.CalcHeight(new GUIContent(text), body.width - 18f) + 8f);
            _scroll = GUI.BeginScrollView(body, _scroll, new Rect(0f, 0f, body.width - 18f, viewH));
            GUI.Label(new Rect(0f, 0f, body.width - 18f, viewH), text, _body);
            GUI.EndScrollView();
        }

        string BuildBody(Entity entity)
        {
            var sb = new StringBuilder(1024);
            sb.AppendLine("【属性】");
            if (entity.TryGet<AttributesComponent>(out var attrs))
            {
                for (var i = 0; i < AttrOrder.Length; i++)
                {
                    var id = AttrOrder[i];
                    sb.Append(AttrName(id)).Append("　").Append(attrs.GetFinal(id)).Append('\n');
                }
            }
            else sb.AppendLine("无");

            sb.AppendLine();
            sb.AppendLine("【灵根】");
            if (entity.TryGet<SpiritRootComponent>(out var roots))
            {
                for (var i = 0; i < RootOrder.Length; i++)
                {
                    var k = RootOrder[i];
                    sb.Append(RootName(k)).Append("　")
                        .Append(roots.Get(k)).Append('/').Append(SpiritRootComponent.DefaultMax).Append('\n');
                }
            }
            else sb.AppendLine("无");

            sb.AppendLine();
            sb.AppendLine("【性格／履历】");
            if (entity.TryGet<CharacterBioComponent>(out var bio))
            {
                if (!string.IsNullOrEmpty(bio.Hometown))
                    sb.Append("籍贯 ").Append(bio.Hometown).Append('\n');
                sb.Append("声望 ").Append(bio.Reputation).Append('\n');
                for (var i = 0; i < bio.Goals.Count; i++)
                    sb.Append("目标 · ").Append(bio.Goals[i]).Append('\n');
                for (var i = 0; i < bio.Desires.Count; i++)
                    sb.Append("欲求 · ").Append(bio.Desires[i]).Append('\n');
            }

            if (entity.TryGet<PersonalityProfileComponent>(out var profile) && profile.Count > 0)
            {
                foreach (var tag in profile.Tags)
                    sb.Append("标签 · ").Append(tag).Append('\n');
            }

            sb.AppendLine();
            sb.AppendLine("【活动倾向】");
            if (entity.TryGet<ActivityTendencyComponent>(out var tendency))
            {
                if (!string.IsNullOrEmpty(tendency.HomeWorkAreaId))
                    sb.Append("住房 ").Append(ShortId(tendency.HomeWorkAreaId)).Append('\n');
                tendency.CopyPrioritiesTo(_tendencyScratch);
                for (var i = 0; i < _tendencyScratch.Count; i++)
                {
                    var item = _tendencyScratch[i];
                    sb.Append(ActivityName(item.Activity)).Append("　优先 ")
                        .Append(item.Priority).Append('\n');
                }
            }
            else sb.AppendLine("无");

            return sb.ToString();
        }

        void EnsureStyles()
        {
            if (_title != null) return;
            _px = Texture2D.whiteTexture;
            _title = HostImguiStyles.InkLabel(18, bold: true);
            _body = HostImguiStyles.InkLabel(13, wordWrap: true);
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
            Fill(new Rect(r.x, r.y, r.width, 1f), c);
            Fill(new Rect(r.x, r.yMax - 1f, r.width, 1f), c);
            Fill(new Rect(r.x, r.y, 1f, r.height), c);
            Fill(new Rect(r.xMax - 1f, r.y, 1f, r.height), c);
        }

        static string AttrName(AttributeId id) => HostAttributeLabels.Name(id);

        static string RootName(SpiritRootKind k)
        {
            switch (k)
            {
                case SpiritRootKind.Fire: return "火";
                case SpiritRootKind.Metal: return "金";
                case SpiritRootKind.Earth: return "土";
                case SpiritRootKind.Wood: return "木";
                case SpiritRootKind.Thunder: return "雷";
                case SpiritRootKind.Wind: return "风";
                case SpiritRootKind.Ice: return "冰";
                case SpiritRootKind.Poison: return "毒";
                default: return k.ToString();
            }
        }

        static string ActivityName(ScheduleActivity a)
        {
            switch (a)
            {
                case ScheduleActivity.Labor: return "工作";
                case ScheduleActivity.Rest: return "休息";
                case ScheduleActivity.Eat: return "吃饭";
                case ScheduleActivity.Cultivate: return "修炼";
                case ScheduleActivity.Explore: return "探索";
                case ScheduleActivity.Patrol: return "巡视";
                case ScheduleActivity.Inspect: return "检查";
                case ScheduleActivity.Idle: return "发呆";
                default: return a.ToString();
            }
        }

        static string ShortId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "-";
            var i = id.IndexOf(':');
            return i >= 0 && i + 1 < id.Length ? id.Substring(i + 1) : id;
        }
    }
}
