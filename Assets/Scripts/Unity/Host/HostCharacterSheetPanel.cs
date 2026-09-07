using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Attributes;
using XianXia.Core.Combat;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Npc;
using XianXia.Core.Schedule;
using XianXia.Core.Social;

namespace XianXia.Unity.Host
{
    public enum CharacterProfilePage { Attributes = 0, Story = 1, Family = 2, Social = 3 }

    /// <summary>统一人物档案：固定身份栏 + 属性／故事／亲族／人际四页。打开时暂停。</summary>
    public sealed class HostCharacterSheetPanel : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] bool open;

        EntityId _subject = EntityId.None;
        EntityId _relatedSelection = EntityId.None;
        CharacterProfilePage _page;
        bool _holdingPause;
        bool _storyOnlyActive;
        Vector2 _attributesScroll, _storyScroll, _familyScroll, _socialScroll;
        GUIStyle _title, _heading, _body, _small, _center, _tab, _tabSelected;
        Texture2D _px;

        readonly List<(ScheduleActivity Activity, int Priority)> _tendencyScratch = new List<(ScheduleActivity, int)>(16);
        readonly List<SocialBond> _bondScratch = new List<SocialBond>(16);
        readonly List<RelationshipEvent> _storyScratch = new List<RelationshipEvent>(60);
        readonly List<EntityId> _parents = new List<EntityId>(8);
        readonly List<EntityId> _spouses = new List<EntityId>(8);
        readonly List<EntityId> _siblings = new List<EntityId>(16);
        readonly List<EntityId> _children = new List<EntityId>(16);
        readonly List<EntityId> _friends = new List<EntityId>(32);
        readonly List<EntityId> _enemies = new List<EntityId>(32);
        readonly List<EntityId> _mentors = new List<EntityId>(32);
        readonly List<EntityId> _sworn = new List<EntityId>(32);
        readonly List<EntityId> _others = new List<EntityId>(40);

        static readonly Color Backdrop = new Color(0.035f, 0.045f, 0.055f, 0.82f);
        static readonly Color Panel = new Color(0.075f, 0.09f, 0.105f, 0.99f);
        static readonly Color Rail = new Color(0.105f, 0.12f, 0.135f, 1f);
        static readonly Color Card = new Color(0.115f, 0.135f, 0.15f, 0.98f);
        static readonly Color Border = new Color(0.43f, 0.36f, 0.24f, 1f);
        static readonly Color Gold = new Color(0.83f, 0.66f, 0.30f, 1f);

        static readonly AttributeId[] PersonalAttributes =
        {
            AttributeId.Physique, AttributeId.Stamina, AttributeId.SpiritSense,
            AttributeId.MindState, AttributeId.Comprehension
        };
        static readonly AttributeId[] CombatAttributes =
        {
            AttributeId.MaxHp, AttributeId.Attack, AttributeId.Defense,
            AttributeId.Speed, AttributeId.SpiritPower
        };
        static readonly SpiritRootKind[] RootOrder =
        {
            SpiritRootKind.Fire, SpiritRootKind.Metal, SpiritRootKind.Earth, SpiritRootKind.Wood,
            SpiritRootKind.Thunder, SpiritRootKind.Wind, SpiritRootKind.Ice, SpiritRootKind.Poison
        };

        public bool IsOpen => open;
        public EntityId Subject => _subject;
        public CharacterProfilePage Page => _page;

        public void Bind(PlayableHostBootstrap host, HostSelectionController selection)
        {
            bootstrap = host;
            selectionController = selection;
        }

        public void ClearSessionState()
        {
            open = false;
            _subject = EntityId.None;
            _relatedSelection = EntityId.None;
            _holdingPause = false;
            HostInputGate.Clear();
        }

        public void OpenFor(EntityId id) => OpenFor(id, CharacterProfilePage.Attributes);

        public void OpenFor(EntityId id, CharacterProfilePage page)
        {
            if (id.IsNone) return;
            CloseCompetingPanels();
            if (_subject != id)
            {
                _subject = id;
                _relatedSelection = EntityId.None;
                ResetPageState();
            }
            _page = page;
            open = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var name = bootstrap?.Session?.World?.Entities.TryGet(id, out var entity) == true ? entity.DisplayName : "?";
            Debug.Log("[CharacterUI] Profile subject=" + id + " name=" + name + " page=" + page);
#endif
        }

        public void Close() => open = false;

        void CloseCompetingPanels()
        {
            bootstrap?.RelationPanel?.Close();
            bootstrap?.CultivationPanel?.Close();
            bootstrap?.CombatArtsPanel?.Close();
            bootstrap?.InventoryPanel?.Close();
            bootstrap?.ConstructionPanel?.Close();
            bootstrap?.WorldMapPanel?.Close();
            bootstrap?.QuestJournal?.Close();
        }

        void ResetPageState()
        {
            _attributesScroll = _storyScroll = _familyScroll = _socialScroll = Vector2.zero;
            _storyOnlyActive = false;
        }

        void Update()
        {
            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized) return;
            if (open && Input.GetKeyDown(KeyCode.Escape)) open = false;
            if (open)
            {
                HostInputGate.BlockWorldCamera = true;
                HostInputGate.BlockWorldInteraction = true;
                // 竞争 Modal 可能在同帧稍后释放自己的暂停；档案打开期间每帧重申 ownership。
                bootstrap.Session.IsPaused = true;
                _holdingPause = true;
            }
            else ReleasePause();
        }

        void ReleasePause()
        {
            if (!_holdingPause) return;
            _holdingPause = false;
            if (bootstrap?.Session != null) bootstrap.Session.IsPaused = false;
            HostInputGate.Clear();
        }

        void OnGUI()
        {
            if (!open || bootstrap?.Session == null || !bootstrap.Session.IsInitialized) return;
            EnsureStyles();
            if (_subject.IsNone || !bootstrap.Session.World.Entities.TryGet(_subject, out var entity))
            {
                open = false;
                return;
            }

            var mx = Mathf.Max(24f, Screen.width * 0.04f);
            var my = Mathf.Max(24f, Screen.height * 0.055f);
            var rect = new Rect(mx, my, Screen.width - mx * 2f, Screen.height * 0.86f);
            if (rect.yMax > Screen.height - 24f) rect.height = Screen.height - 24f - rect.y;
            HostUiHitTest.Block(rect);
            HostCharacterRelationUi.Fill(new Rect(0f, 0f, Screen.width, Screen.height), Backdrop, _px);
            HostCharacterRelationUi.Fill(rect, Panel, _px);
            HostCharacterRelationUi.Stroke(rect, Border, _px);

            var railW = Mathf.Clamp(rect.width * 0.235f, 220f, 330f);
            var rail = new Rect(rect.x, rect.y, railW, rect.height);
            var main = new Rect(rail.xMax, rect.y, rect.width - railW, rect.height);
            DrawSubjectRail(rail, entity);
            DrawTopTabs(main);
            var bodyRect = new Rect(main.x + 18f, main.y + 66f, main.width - 36f, main.height - 84f);
            switch (_page)
            {
                case CharacterProfilePage.Story: DrawStoryPage(bodyRect, entity); break;
                case CharacterProfilePage.Family: DrawFamilyPage(bodyRect, entity); break;
                case CharacterProfilePage.Social: DrawSocialPage(bodyRect, entity); break;
                default: DrawAttributesPage(bodyRect, entity); break;
            }
        }

        void DrawSubjectRail(Rect rail, Entity entity)
        {
            HostCharacterRelationUi.Fill(rail, Rail, _px);
            HostCharacterRelationUi.Fill(new Rect(rail.xMax - 1f, rail.y, 1f, rail.height), Border, _px);
            var name = HostCharacterRelationUi.ResolveDisplayName(bootstrap.Session.World, entity.Id);
            GUI.Label(new Rect(rail.x + 16f, rail.y + 18f, rail.width - 32f, 32f), name, _title);
            var portrait = new Rect(rail.x + 24f, rail.y + 62f, rail.width - 48f, Mathf.Min(250f, rail.width - 48f));
            HostCharacterRelationUi.Fill(portrait, new Color(0.17f, 0.20f, 0.22f, 1f), _px);
            HostCharacterRelationUi.Stroke(portrait, Gold, _px);
            if (HostCharacterRelationUi.TryResolveCharacterPortrait(bootstrap.Session, entity.Id, out var image) && image != null)
                GUI.DrawTexture(new Rect(portrait.x + 6f, portrait.y + 6f, portrait.width - 12f, portrait.height - 12f), image, ScaleMode.ScaleToFit);
            else
                GUI.Label(portrait, string.IsNullOrEmpty(name) ? "人" : name.Substring(0, 1),
                    new GUIStyle(_title) { alignment = TextAnchor.MiddleCenter, fontSize = 58 });

            HostCharacterPresentationResolver.TryBuild(bootstrap.Session, entity.Id, out var info);
            var realm = entity.TryGet<CultivationComponent>(out var cultivation)
                ? RealmDisplay.Format(cultivation.Realm, cultivation.MinorStage) : "凡俗";
            var life = CombatLifeStateService.FormatLifeStateWithCountdown(bootstrap.Session.World, entity);
            if (string.IsNullOrEmpty(life)) life = "正常";
            var y = portrait.yMax + 18f;
            DrawRailRow(rail, ref y, "生命状态", life);
            DrawRailRow(rail, ref y, "势力", info?.FactionName ?? "无");
            DrawRailRow(rail, ref y, "身份", info?.FactionRole ?? "无");
            DrawRailRow(rail, ref y, "境界", realm);
            DrawRailRow(rail, ref y, "当前位置", info?.Location ?? "未知");
        }

        void DrawRailRow(Rect rail, ref float y, string label, string value)
        {
            GUI.Label(new Rect(rail.x + 20f, y, rail.width - 40f, 20f), label, _small);
            y += 19f;
            GUI.Label(new Rect(rail.x + 20f, y, rail.width - 40f, 34f), value, _body);
            y += 39f;
        }

        void DrawTopTabs(Rect main)
        {
            var tabs = new[] { "人物属性", "人物故事", "亲族关系", "人际关系" };
            var tabW = Mathf.Min(138f, (main.width - 86f) / tabs.Length);
            for (var i = 0; i < tabs.Length; i++)
            {
                var r = new Rect(main.x + 18f + i * (tabW + 5f), main.y + 14f, tabW, 38f);
                if (GUI.Button(r, tabs[i], (int)_page == i ? _tabSelected : _tab)) _page = (CharacterProfilePage)i;
            }
            if (GUI.Button(new Rect(main.xMax - 52f, main.y + 14f, 34f, 34f), "×", _tab)) open = false;
        }

        void DrawAttributesPage(Rect rect, Entity entity)
        {
            var canvas = new Rect(0f, 0f, rect.width - 18f, 730f);
            _attributesScroll = GUI.BeginScrollView(rect, _attributesScroll, canvas);
            HostCharacterPresentationResolver.TryBuild(bootstrap.Session, entity.Id, out var info);
            var active = bootstrap.Session.PlayerParty?.ActiveCharacterId ?? EntityId.None;
            var leftW = active.IsNone || active == entity.Id ? canvas.width : canvas.width * 0.61f;
            DrawOverviewCard(new Rect(0f, 0f, leftW - 8f, 170f), entity, info);
            if (!active.IsNone && active != entity.Id)
                HostCharacterRelationUi.DrawAttitudeDetail(
                    new Rect(leftW + 8f, 0f, canvas.width - leftW - 8f, 170f), "对当前主控的态度",
                    bootstrap.Session.World.Relationships.GetAttitude(entity.Id, active), _heading, _body, _px);

            const float gap = 12f;
            var colW = (canvas.width - gap * 2f) / 3f;
            DrawAttributeColumn(new Rect(0f, 184f, colW, 286f), "个人属性", entity, PersonalAttributes, false);
            DrawAttributeColumn(new Rect(colW + gap, 184f, colW, 286f), "战斗属性", entity, CombatAttributes, true);
            DrawCultivationColumn(new Rect((colW + gap) * 2f, 184f, colW, 286f), entity);
            var lowerW = (canvas.width - gap * 3f) / 4f;
            DrawTextCard(new Rect(0f, 484f, lowerW, 220f), "性格", JoinTags(info?.PersonalityTags));
            DrawTextCard(new Rect(lowerW + gap, 484f, lowerW, 220f), "背景", JoinTags(info?.BackgroundTags));
            DrawTextCard(new Rect((lowerW + gap) * 2f, 484f, lowerW, 220f), "天赋", JoinTags(info?.TalentTags));
            DrawTextCard(new Rect((lowerW + gap) * 3f, 484f, lowerW, 220f), "活动倾向", BuildTendency(entity));
            GUI.EndScrollView();
        }

        void DrawOverviewCard(Rect rect, Entity entity, HostCharacterPresentation info)
        {
            DrawCard(rect, "人物概况");
            var realm = entity.TryGet<CultivationComponent>(out var c) ? RealmDisplay.Format(c.Realm, c.MinorStage) : "凡俗";
            var life = CombatLifeStateService.FormatLifeStateWithCountdown(bootstrap.Session.World, entity);
            if (string.IsNullOrEmpty(life)) life = "正常";
            var text = "势力：" + (info?.FactionName ?? "无") + "\n身份：" + (info?.FactionRole ?? "无") +
                       "\n境界：" + realm + "\n当前位置：" + (info?.Location ?? "未知") +
                       "\n当前状态：" + (info?.Activity ?? "待命") + "\n日程：" + (info?.Schedule ?? "无");
            GUI.Label(new Rect(rect.x + 14f, rect.y + 38f, rect.width - 28f, rect.height - 46f), text, _body);
        }

        void DrawAttributeColumn(Rect rect, string title, Entity entity, AttributeId[] order, bool currentHp)
        {
            DrawCard(rect, title);
            var y = rect.y + 42f;
            if (currentHp && entity.TryGet<CombatVitalsComponent>(out var vitals)) DrawValueRow(rect, ref y, "当前生命", vitals.CurrentHp.ToString());
            if (entity.TryGet<AttributesComponent>(out var attrs))
                for (var i = 0; i < order.Length; i++) DrawValueRow(rect, ref y, HostAttributeLabels.Name(order[i]), attrs.GetFinal(order[i]).ToString());
            else GUI.Label(new Rect(rect.x + 14f, y, rect.width - 28f, 24f), "无数据", _body);
        }

        void DrawCultivationColumn(Rect rect, Entity entity)
        {
            DrawCard(rect, "修炼资质");
            var y = rect.y + 42f;
            if (entity.TryGet<CultivationComponent>(out var c))
            {
                DrawValueRow(rect, ref y, "境界", RealmDisplay.Format(c.Realm, c.MinorStage));
                DrawValueRow(rect, ref y, "修为进度", c.Progress + "/" + c.BreakthroughProgressRequired);
                DrawValueRow(rect, ref y, "修炼速度", c.CultivationSpeed.ToString());
            }
            if (entity.TryGet<SpiritRootComponent>(out var roots))
            {
                for (var row = 0; row < 2; row++)
                {
                    var line = string.Empty;
                    for (var i = row * 4; i < row * 4 + 4; i++) line += (i == row * 4 ? "" : "　") + RootName(RootOrder[i]) + roots.Get(RootOrder[i]);
                    GUI.Label(new Rect(rect.x + 14f, y, rect.width - 28f, 22f), line, _small);
                    y += 23f;
                }
            }
        }

        void DrawValueRow(Rect rect, ref float y, string label, string value)
        {
            GUI.Label(new Rect(rect.x + 14f, y, rect.width * 0.58f, 24f), label, _body);
            GUI.Label(new Rect(rect.x + rect.width * 0.60f, y, rect.width * 0.34f, 24f), value, _body);
            y += 29f;
        }

        void DrawStoryPage(Rect rect, Entity entity)
        {
            var active = bootstrap.Session.PlayerParty?.ActiveCharacterId ?? EntityId.None;
            GUI.Label(new Rect(rect.x, rect.y, 180f, 26f), "近期经历", _heading);
            if (!active.IsNone && active != entity.Id)
                _storyOnlyActive = GUI.Toggle(new Rect(rect.x + 190f, rect.y, 190f, 24f), _storyOnlyActive, "与当前主控相关", _body);
            else _storyOnlyActive = false;

            HostCharacterStoryQuery.Collect(bootstrap.Session.World, entity.Id, active, _storyOnlyActive, _storyScratch);
            var timeline = new Rect(rect.x, rect.y + 32f, rect.width, Mathf.Min(300f, rect.height * 0.52f));
            var canvasW = Mathf.Max(timeline.width - 18f, 90f + _storyScratch.Count * 190f);
            var canvasH = timeline.height - 18f;
            _storyScroll = GUI.BeginScrollView(timeline, _storyScroll, new Rect(0f, 0f, canvasW, canvasH));
            var lineY = canvasH * 0.5f;
            HostCharacterRelationUi.Fill(new Rect(26f, lineY, canvasW - 52f, 2f), Border, _px);
            if (_storyScratch.Count == 0) GUI.Label(new Rect(20f, lineY - 16f, canvasW - 40f, 32f), "暂无可显示的人际经历", _center);
            for (var i = 0; i < _storyScratch.Count; i++) DrawTimelineEntry(_storyScratch[i], entity.Id, 50f + i * 190f, lineY, (i & 1) == 0);
            GUI.EndScrollView();
            var bioY = timeline.yMax + 12f;
            DrawBiography(new Rect(rect.x, bioY, rect.width, rect.yMax - bioY), entity);
        }

        void DrawTimelineEntry(RelationshipEvent evt, EntityId subject, float x, float lineY, bool above)
        {
            HostCharacterRelationUi.Fill(new Rect(x - 5f, lineY - 5f, 12f, 12f), Gold, _px);
            var cardRect = new Rect(x - 28f, above ? lineY - 106f : lineY + 18f, 170f, 82f);
            var connectorY = above ? cardRect.yMax : lineY + 5f;
            HostCharacterRelationUi.Fill(new Rect(x, connectorY, 1f, Mathf.Abs(cardRect.y - lineY) - (above ? 24f : 5f)), Border, _px);
            HostCharacterRelationUi.Fill(cardRect, Card, _px);
            HostCharacterRelationUi.Stroke(cardRect, Border, _px);
            var clock = DayClock.FromWorldTick(evt.Tick);
            var time = "第" + (clock.DayIndex + 1UL) + "天 " + clock.HourOfDay.ToString("00") + ":" + clock.MinuteOfHour.ToString("00");
            GUI.Label(new Rect(cardRect.x + 9f, cardRect.y + 7f, cardRect.width - 18f, 20f), time, _small);
            GUI.Label(new Rect(cardRect.x + 9f, cardRect.y + 29f, cardRect.width - 18f, cardRect.height - 34f),
                HostSocialHistoryPresentation.Format(bootstrap.Session.World, subject, evt), _body);
        }

        void DrawBiography(Rect rect, Entity entity)
        {
            DrawCard(rect, "人物小传");
            HostCharacterPresentationResolver.TryBuild(bootstrap.Session, entity.Id, out var info);
            var hometown = string.Empty;
            IReadOnlyList<string> goals = null, desires = null;
            if (entity.TryGet<CharacterBioComponent>(out var bio)) { hometown = bio.Hometown; goals = bio.Goals; desires = bio.Desires; }
            else if (info?.Definition != null) { hometown = info.Definition.Hometown; goals = info.Definition.Goals; desires = info.Definition.Desires; }
            var colW = (rect.width - 54f) / 3f;
            DrawBioColumn(new Rect(rect.x + 14f, rect.y + 42f, colW, rect.height - 54f), "籍贯与背景",
                (string.IsNullOrEmpty(hometown) ? "未知" : hometown) + "\n\n" + JoinTags(info?.BackgroundTags));
            DrawBioColumn(new Rect(rect.x + 27f + colW, rect.y + 42f, colW, rect.height - 54f), "当前目标", JoinList(goals));
            DrawBioColumn(new Rect(rect.x + 40f + colW * 2f, rect.y + 42f, colW, rect.height - 54f), "欲求与禀赋",
                JoinList(desires) + "\n\n" + JoinTags(info?.PersonalityTags) + "\n" + JoinTags(info?.TalentTags));
        }

        void DrawBioColumn(Rect rect, string title, string text)
        {
            HostCharacterRelationUi.Fill(rect, new Color(0.09f, 0.11f, 0.125f, 1f), _px);
            GUI.Label(new Rect(rect.x + 10f, rect.y + 8f, rect.width - 20f, 22f), title, _heading);
            GUI.Label(new Rect(rect.x + 10f, rect.y + 36f, rect.width - 20f, rect.height - 44f), string.IsNullOrEmpty(text) ? "无" : text, _body);
        }

        void DrawFamilyPage(Rect rect, Entity entity)
        {
            CategorizeFamily(entity.Id);
            var sideCount = Mathf.Max(_spouses.Count, _siblings.Count);
            var canvasW = Mathf.Max(rect.width - 18f, 760f + sideCount * 132f);
            var canvasH = Mathf.Max(rect.height - 18f, 620f);
            _familyScroll = GUI.BeginScrollView(rect, _familyScroll, new Rect(0f, 0f, canvasW, canvasH));
            var center = new Rect(canvasW * 0.5f - 62f, canvasH * 0.5f - 70f, 124f, 140f);
            DrawFamilyGroup(_parents, "父母", 26f, canvasW * 0.5f, center, true);
            DrawFamilyGroup(_children, "子女", canvasH - 152f, canvasW * 0.5f, center, false);
            DrawSideFamilyGroup(_spouses, "配偶", 42f, center, true);
            DrawSideFamilyGroup(_siblings, "兄弟姐妹", center.xMax + 42f, center, false);
            HostCharacterRelationUi.DrawPersonCard(center, bootstrap.Session, entity.Id, "当前人物", true, _center, _small, _px);
            if (_parents.Count + _spouses.Count + _siblings.Count + _children.Count == 0)
                GUI.Label(new Rect(center.x - 170f, center.yMax + 18f, 340f, 28f), "暂无已知亲族关系", _center);
            DrawRelatedDetailButton(new Rect(canvasW - 150f, 16f, 132f, 32f));
            GUI.EndScrollView();
        }

        void CategorizeFamily(EntityId subject)
        {
            _parents.Clear(); _spouses.Clear(); _siblings.Clear(); _children.Clear();
            SocialBondQuery.GetForSubject(bootstrap.Session.World, subject, _bondScratch);
            for (var i = 0; i < _bondScratch.Count; i++)
            {
                var bond = _bondScratch[i];
                var other = bond.Other(subject);
                if (bond.Kind == SocialBondKind.ParentChild) AddUnique(bond.From == subject ? _children : _parents, other);
                else if (bond.Kind == SocialBondKind.Spouse) AddUnique(_spouses, other);
                else if (bond.Kind == SocialBondKind.Sibling) AddUnique(_siblings, other);
            }
            if (!_parents.Contains(_relatedSelection) && !_spouses.Contains(_relatedSelection) &&
                !_siblings.Contains(_relatedSelection) && !_children.Contains(_relatedSelection))
                _relatedSelection = EntityId.None;
        }

        void DrawFamilyGroup(List<EntityId> ids, string badge, float y, float centerX, Rect subject, bool above)
        {
            if (ids.Count == 0) return;
            const float w = 112f, gap = 12f;
            var start = centerX - (ids.Count * w + (ids.Count - 1) * gap) * 0.5f;
            var junctionY = above ? subject.y - 22f : subject.yMax + 22f;
            HostCharacterRelationUi.Fill(new Rect(centerX, above ? junctionY : subject.yMax, 1f, 22f), Border, _px);
            for (var i = 0; i < ids.Count; i++)
            {
                var personRect = new Rect(start + i * (w + gap), y, w, 126f);
                var cx = personRect.center.x;
                HostCharacterRelationUi.Fill(new Rect(Mathf.Min(centerX, cx), junctionY, Mathf.Abs(cx - centerX) + 1f, 1f), Border, _px);
                HostCharacterRelationUi.Fill(new Rect(cx, above ? personRect.yMax : junctionY, 1f,
                    above ? junctionY - personRect.yMax : personRect.y - junctionY), Border, _px);
                if (HostCharacterRelationUi.DrawPersonCard(personRect, bootstrap.Session, ids[i], badge,
                    _relatedSelection == ids[i], _center, _small, _px)) _relatedSelection = ids[i];
            }
        }

        void DrawSideFamilyGroup(List<EntityId> ids, string badge, float x, Rect subject, bool left)
        {
            if (ids.Count == 0) return;
            const float w = 112f, gap = 10f;
            var y = subject.center.y - 63f;
            for (var i = 0; i < ids.Count; i++)
            {
                var personRect = new Rect(x + i * (w + gap), y, w, 126f);
                var from = left ? personRect.xMax : subject.xMax;
                var to = left ? subject.x : personRect.x;
                HostCharacterRelationUi.Fill(new Rect(Mathf.Min(from, to), subject.center.y, Mathf.Abs(to - from), 1f), Border, _px);
                if (HostCharacterRelationUi.DrawPersonCard(personRect, bootstrap.Session, ids[i], badge,
                    _relatedSelection == ids[i], _center, _small, _px)) _relatedSelection = ids[i];
            }
        }

        void DrawSocialPage(Rect rect, Entity entity)
        {
            CategorizeSocial(entity.Id);
            var active = bootstrap.Session.PlayerParty?.ActiveCharacterId ?? EntityId.None;
            var detailW = Mathf.Clamp(rect.width * 0.34f, 250f, 390f);
            var listRect = new Rect(rect.x, rect.y, rect.width - detailW - 16f, rect.height);
            var detailRect = new Rect(listRect.xMax + 16f, rect.y, detailW, rect.height);
            _socialScroll = GUI.BeginScrollView(listRect, _socialScroll,
                new Rect(0f, 0f, listRect.width - 18f, Mathf.Max(listRect.height, SocialContentHeight())));
            var y = 0f;
            DrawSocialGroup(_friends, "好友", ref y, listRect.width - 18f);
            DrawSocialGroup(_enemies, "仇视", ref y, listRect.width - 18f);
            DrawSocialGroup(_mentors, "师徒", ref y, listRect.width - 18f);
            DrawSocialGroup(_sworn, "结义", ref y, listRect.width - 18f);
            DrawSocialGroup(_others, "其他", ref y, listRect.width - 18f);
            if (y <= 1f) GUI.Label(new Rect(0f, 12f, listRect.width - 18f, 28f), "暂无显著人际关系", _center);
            GUI.EndScrollView();

            var dy = detailRect.y;
            if (!active.IsNone && active != entity.Id)
            {
                HostCharacterRelationUi.DrawAttitudeDetail(new Rect(detailRect.x, dy, detailRect.width, 174f),
                    "对当前主控的态度", bootstrap.Session.World.Relationships.GetAttitude(entity.Id, active), _heading, _body, _px);
                dy += 188f;
            }
            if (!_relatedSelection.IsNone && bootstrap.Session.World.Entities.TryGet(_relatedSelection, out _))
            {
                var label = ResolveSocialBadge(entity.Id, _relatedSelection);
                HostCharacterRelationUi.DrawAttitudeDetail(new Rect(detailRect.x, dy, detailRect.width, 202f),
                    "对「" + HostCharacterRelationUi.ResolveDisplayName(bootstrap.Session.World, _relatedSelection) + "」的态度",
                    bootstrap.Session.World.Relationships.GetAttitude(entity.Id, _relatedSelection), _heading, _body, _px);
                GUI.Label(new Rect(detailRect.x + 14f, dy + 172f, detailRect.width - 28f, 22f), "关系标签：" + label, _small);
                dy += 214f;
                DrawRelatedDetailButton(new Rect(detailRect.x, dy, detailRect.width, 34f));
            }
            else
            {
                DrawCard(new Rect(detailRect.x, dy, detailRect.width, 96f), "关系详情");
                GUI.Label(new Rect(detailRect.x + 14f, dy + 42f, detailRect.width - 28f, 34f), "选择一张人物卡查看精确五维态度", _body);
            }
        }

        void CategorizeSocial(EntityId subject)
        {
            _friends.Clear(); _enemies.Clear(); _mentors.Clear(); _sworn.Clear(); _others.Clear();
            SocialBondQuery.GetForSubject(bootstrap.Session.World, subject, _bondScratch);
            foreach (var other in bootstrap.Session.World.Entities.All)
            {
                if (other == null || other.Id == subject ||
                    (other.Tags & (EntityTag.Character | EntityTag.Npc)) == 0) continue;
                var attitude = bootstrap.Session.World.Relationships.GetAttitude(subject, other.Id);
                if (attitude.Grudge >= 60) AddUnique(_enemies, other.Id);
                else if (attitude.Affection >= 60) AddUnique(_friends, other.Id);
                else if (HasBond(other.Id, SocialBondKind.MasterDisciple)) AddUnique(_mentors, other.Id);
                else if (HasBond(other.Id, SocialBondKind.SwornSibling)) AddUnique(_sworn, other.Id);
                else if (!attitude.IsZero) AddUnique(_others, other.Id);
                if (TotalSocialCount() >= 40) break;
            }
            if (!ContainsSocialPeer(_relatedSelection))
                _relatedSelection = TotalSocialCount() > 0 ? FirstSocialPeer() : EntityId.None;
        }

        bool HasBond(EntityId peer, SocialBondKind kind)
        {
            for (var i = 0; i < _bondScratch.Count; i++)
                if (_bondScratch[i].Kind == kind && _bondScratch[i].Other(_subject) == peer) return true;
            return false;
        }

        string ResolveSocialBadge(EntityId subject, EntityId peer)
        {
            var attitude = bootstrap.Session.World.Relationships.GetAttitude(subject, peer);
            var label = attitude.Grudge >= 60 ? "仇视" : (attitude.Affection >= 60 ? "好友" : string.Empty);
            for (var i = 0; i < _bondScratch.Count; i++)
            {
                var bond = _bondScratch[i];
                if (bond.Other(subject) == peer && (bond.Kind == SocialBondKind.MasterDisciple || bond.Kind == SocialBondKind.SwornSibling))
                {
                    var role = HostCharacterRelationUi.ResolveBondRole(bond, subject);
                    if (!string.IsNullOrEmpty(role)) label += (string.IsNullOrEmpty(label) ? "" : " · ") + role;
                }
            }
            return string.IsNullOrEmpty(label) ? "其他" : label;
        }

        void DrawSocialGroup(List<EntityId> ids, string title, ref float y, float width)
        {
            if (ids.Count == 0) return;
            GUI.Label(new Rect(0f, y, width, 26f), "【" + title + "】", _heading);
            y += 32f;
            const float cardW = 116f, cardH = 128f, gap = 10f;
            var columns = Mathf.Max(1, Mathf.FloorToInt((width + gap) / (cardW + gap)));
            for (var i = 0; i < ids.Count; i++)
            {
                var personRect = new Rect((i % columns) * (cardW + gap), y + (i / columns) * (cardH + gap), cardW, cardH);
                if (HostCharacterRelationUi.DrawPersonCard(personRect, bootstrap.Session, ids[i], ResolveSocialBadge(_subject, ids[i]),
                    _relatedSelection == ids[i], _center, _small, _px)) _relatedSelection = ids[i];
            }
            y += Mathf.CeilToInt((float)ids.Count / columns) * (cardH + gap) + 12f;
        }

        float SocialContentHeight() => TotalSocialCount() == 0 ? 80f : 80f + TotalSocialCount() * 150f;
        int TotalSocialCount() => _friends.Count + _enemies.Count + _mentors.Count + _sworn.Count + _others.Count;
        bool ContainsSocialPeer(EntityId id) => !id.IsNone &&
            (_friends.Contains(id) || _enemies.Contains(id) || _mentors.Contains(id) ||
             _sworn.Contains(id) || _others.Contains(id));
        EntityId FirstSocialPeer()
        {
            if (_friends.Count > 0) return _friends[0];
            if (_enemies.Count > 0) return _enemies[0];
            if (_mentors.Count > 0) return _mentors[0];
            if (_sworn.Count > 0) return _sworn[0];
            return _others.Count > 0 ? _others[0] : EntityId.None;
        }

        void DrawRelatedDetailButton(Rect rect)
        {
            var old = GUI.enabled;
            GUI.enabled = !_relatedSelection.IsNone;
            if (GUI.Button(rect, "查看详情", _tab)) OpenFor(_relatedSelection, CharacterProfilePage.Attributes);
            GUI.enabled = old;
        }

        void DrawTextCard(Rect rect, string title, string text)
        {
            DrawCard(rect, title);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 39f, rect.width - 24f, rect.height - 49f), string.IsNullOrEmpty(text) ? "无" : text, _body);
        }

        void DrawCard(Rect rect, string title)
        {
            HostCharacterRelationUi.Fill(rect, Card, _px);
            HostCharacterRelationUi.Stroke(rect, Border, _px);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 8f, rect.width - 24f, 25f), title, _heading);
        }

        string BuildTendency(Entity entity)
        {
            if (!entity.TryGet<ActivityTendencyComponent>(out var tendency)) return "无";
            tendency.CopyPrioritiesTo(_tendencyScratch);
            var text = string.Empty;
            for (var i = 0; i < _tendencyScratch.Count; i++) text += (i == 0 ? "" : "\n") + ActivityName(_tendencyScratch[i].Activity) + "　" + _tendencyScratch[i].Priority;
            return string.IsNullOrEmpty(text) ? "无" : text;
        }

        static string JoinTags(IReadOnlyList<string> tags)
        {
            if (tags == null || tags.Count == 0) return "无";
            var text = string.Empty;
            for (var i = 0; i < tags.Count; i++) text += (i == 0 ? "" : "　") + HostCharacterTagPresentation.Display(tags[i]);
            return text;
        }

        static string JoinList(IReadOnlyList<string> items)
        {
            if (items == null || items.Count == 0) return "无";
            var text = string.Empty;
            for (var i = 0; i < items.Count; i++) text += (i == 0 ? "" : "\n") + "· " + items[i];
            return text;
        }

        static void AddUnique(List<EntityId> list, EntityId id) { if (!id.IsNone && !list.Contains(id)) list.Add(id); }

        void EnsureStyles()
        {
            if (_title != null) return;
            _px = Texture2D.whiteTexture;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
            _title.normal.textColor = new Color(0.95f, 0.86f, 0.65f);
            _heading = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
            _heading.normal.textColor = new Color(0.91f, 0.79f, 0.54f);
            _body = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            _body.normal.textColor = new Color(0.88f, 0.89f, 0.87f);
            _small = new GUIStyle(_body) { fontSize = 11 };
            _small.normal.textColor = new Color(0.68f, 0.71f, 0.70f);
            _center = new GUIStyle(_body) { alignment = TextAnchor.MiddleCenter };
            _tab = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold };
            _tab.normal.textColor = new Color(0.78f, 0.80f, 0.78f);
            _tabSelected = new GUIStyle(_tab);
            _tabSelected.normal.textColor = new Color(1f, 0.82f, 0.38f);
            _tabSelected.normal.background = _tabSelected.active.background;
        }

        static string RootName(SpiritRootKind kind)
        {
            switch (kind)
            {
                case SpiritRootKind.Fire: return "火"; case SpiritRootKind.Metal: return "金";
                case SpiritRootKind.Earth: return "土"; case SpiritRootKind.Wood: return "木";
                case SpiritRootKind.Thunder: return "雷"; case SpiritRootKind.Wind: return "风";
                case SpiritRootKind.Ice: return "冰"; case SpiritRootKind.Poison: return "毒";
                default: return kind.ToString();
            }
        }

        static string ActivityName(ScheduleActivity activity)
        {
            switch (activity)
            {
                case ScheduleActivity.Labor: return "工作"; case ScheduleActivity.Rest: return "休息";
                case ScheduleActivity.Eat: return "吃饭"; case ScheduleActivity.Cultivate: return "修炼";
                case ScheduleActivity.Explore: return "探索"; case ScheduleActivity.Patrol: return "巡视";
                case ScheduleActivity.Inspect: return "检查"; case ScheduleActivity.Idle: return "发呆";
                default: return activity.ToString();
            }
        }
    }
}
