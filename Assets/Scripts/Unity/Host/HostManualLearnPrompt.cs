using UnityEngine;
using XianXia.Core.Content;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;

namespace XianXia.Unity.Host
{
    /// <summary>背包秘籍：选择炼气期队员学习功法（秘籍不消耗；换功法需确认覆盖）。</summary>
    public sealed class HostManualLearnPrompt : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;

        bool _open;
        bool _holdingPause;
        string _itemId = string.Empty;
        string _status = string.Empty;
        EntityId _pendingLearner = EntityId.None;
        string _pendingOldManualName = string.Empty;
        Texture2D _px;
        GUIStyle _title;
        GUIStyle _body;

        static readonly Color Parchment = new Color(0.92f, 0.86f, 0.74f, 0.98f);
        static readonly Color ParchmentDark = new Color(0.70f, 0.58f, 0.42f, 1f);
        static readonly Color Ink = new Color(0.16f, 0.12f, 0.08f, 1f);

        public bool IsOpen => _open;

        public void Bind(PlayableHostBootstrap host) => bootstrap = host;

        public void ClearSessionState()
        {
            _open = false;
            _itemId = string.Empty;
            _status = string.Empty;
            _pendingLearner = EntityId.None;
            _pendingOldManualName = string.Empty;
            ReleasePause();
        }

        public void Open(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return;
            _itemId = itemId;
            _status = string.Empty;
            _pendingLearner = EntityId.None;
            _pendingOldManualName = string.Empty;
            _open = true;
        }

        public void Close()
        {
            _open = false;
            _itemId = string.Empty;
            _pendingLearner = EntityId.None;
            _pendingOldManualName = string.Empty;
            ReleasePause();
        }

        void Update()
        {
            if (!_open)
            {
                ReleasePause();
                return;
            }

            HostInputGate.BlockWorldCamera = true;
            HostInputGate.BlockWorldInteraction = true;
            if (!_holdingPause && bootstrap?.Session != null)
            {
                bootstrap.Session.IsPaused = true;
                _holdingPause = true;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (!_pendingLearner.IsNone)
                {
                    _pendingLearner = EntityId.None;
                    _pendingOldManualName = string.Empty;
                }
                else
                    Close();
            }
        }

        void OnGUI()
        {
            if (!_open || bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return;
            EnsureStyles();

            var world = bootstrap.Session.World;
            var manualId = world.InventoryCatalog.GetTeachesManualId(_itemId);
            CultivationManualSpec manual = null;
            if (!string.IsNullOrEmpty(manualId) &&
                DefinitionId.TryParse(manualId, out var mid))
                world.TryGetManual(mid, out manual);

            var dim = new Rect(0f, 0f, Screen.width, Screen.height);
            Fill(dim, new Color(0f, 0f, 0f, 0.45f));
            HostUiHitTest.Block(dim);

            var w = 440f;
            var h = 380f;
            var rect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            HostUiHitTest.Block(rect);
            Fill(rect, Parchment);
            DrawFrame(rect, ParchmentDark);

            var x = rect.x + 16f;
            var y = rect.y + 12f;

            if (!_pendingLearner.IsNone)
            {
                DrawOverwriteConfirm(rect, x, y, w, manual);
                return;
            }

            GUI.Label(new Rect(x, y, w - 32f, 26f), "研读功法", _title);
            y += 30f;

            var tomeName = world.InventoryCatalog.GetName(_itemId);
            var summary = BuildManualSummary(manual);
            GUI.Label(new Rect(x, y, w - 32f, 72f),
                tomeName + "（秘籍保留，可多次传授）\n" + summary, _body);
            y += 78f;
            GUI.Label(new Rect(x, y, w - 32f, 22f), "选择学习者（需已入炼气）：", _body);
            y += 26f;

            var ids = bootstrap.Session.CharacterIds;
            for (var i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                if (!world.Entities.TryGet(id, out var entity))
                    continue;
                var name = string.IsNullOrEmpty(entity.DisplayName) ? id.Value.ToString() : entity.DisplayName;
                var realmOk = CanLearn(entity, manual, out var reason);
                var label = realmOk ? name : name + "（" + reason + "）";
                if (realmOk && entity.TryGet<CultivationComponent>(out var cult) &&
                    cult.HasLearnedManual && cult.LearnedManualId.HasValue &&
                    manual != null && !cult.LearnedManualId.Value.Equals(manual.Id))
                    label += " · 将覆盖现有功法";

                GUI.enabled = realmOk;
                if (HostImguiStyles.ParchmentBtn(new Rect(x, y, w - 32f, 32f), label))
                    TryPickLearner(id, entity, manual);
                GUI.enabled = true;
                y += 36f;
            }

            if (!string.IsNullOrEmpty(_status))
                GUI.Label(new Rect(x, rect.yMax - 72f, w - 32f, 24f), _status, _body);

            if (HostImguiStyles.ParchmentBtn(new Rect(x, rect.yMax - 40f, 100f, 28f), "取消"))
                Close();
        }

        void DrawOverwriteConfirm(Rect rect, float x, float y, float w, CultivationManualSpec manual)
        {
            GUI.Label(new Rect(x, y, w - 32f, 26f), "覆盖功法？", _title);
            y += 34f;
            var newName = manual == null
                ? "新功法"
                : (string.IsNullOrEmpty(manual.Name) ? manual.Id.ToString() : manual.Name);
            GUI.Label(
                new Rect(x, y, w - 32f, 80f),
                "将遗忘「" + _pendingOldManualName + "」，改学「" + newName + "」。\n一人同时只能运转一本功法。",
                _body);
            y += 96f;
            if (HostImguiStyles.ParchmentBtn(new Rect(x, y, 140f, 34f), "确认覆盖"))
            {
                var learner = _pendingLearner;
                _pendingLearner = EntityId.None;
                CommitLearn(learner);
            }

            if (HostImguiStyles.ParchmentBtn(new Rect(x + 156f, y, 100f, 34f), "取消"))
            {
                _pendingLearner = EntityId.None;
                _pendingOldManualName = string.Empty;
            }
        }

        void TryPickLearner(EntityId id, Entity entity, CultivationManualSpec manual)
        {
            if (manual != null &&
                entity.TryGet<CultivationComponent>(out var cult) &&
                cult.HasLearnedManual &&
                cult.LearnedManualId.HasValue &&
                !cult.LearnedManualId.Value.Equals(manual.Id))
            {
                _pendingLearner = id;
                var oldId = cult.LearnedManualId.Value;
                if (bootstrap.Session.World.TryGetManual(oldId, out var oldManual) && oldManual != null &&
                    !string.IsNullOrEmpty(oldManual.Name))
                    _pendingOldManualName = oldManual.Name;
                else
                    _pendingOldManualName = oldId.ToString();
                return;
            }

            CommitLearn(id);
        }

        void CommitLearn(EntityId learner)
        {
            var world = bootstrap.Session.World;
            var result = new ManualItemLearnService().TryLearnFromItem(world, learner, _itemId);
            if (result.IsSuccess)
            {
                bootstrap.DispatchDrainedEvents();
                Close();
                return;
            }

            _status = result.Error.ToString();
        }

        static string BuildManualSummary(CultivationManualSpec manual)
        {
            if (manual == null)
                return "（功法数据缺失）";
            var name = string.IsNullOrEmpty(manual.Name) ? manual.Id.ToString() : manual.Name;
            var grade = string.IsNullOrEmpty(manual.Grade) ? "品阶未标" : manual.Grade;
            var effect = string.IsNullOrEmpty(manual.EffectSummary)
                ? "打坐时每 5 游戏分钟 +" + manual.CultivationSpeed + " 修为"
                : manual.EffectSummary;
            return name + " · " + grade + "\n" + effect;
        }

        static bool CanLearn(Entity entity, CultivationManualSpec manual, out string reason)
        {
            reason = string.Empty;
            if (manual == null)
            {
                reason = "无功法";
                return false;
            }

            if (!entity.TryGet<CultivationComponent>(out var cult))
            {
                reason = "无修炼";
                return false;
            }

            if (cult.Realm < RealmStage.QiRefining)
            {
                reason = "未入炼气";
                return false;
            }

            if (!TryParseRealm(manual.RequiredRealm, out var required))
                return true;
            if (cult.Realm < required)
            {
                reason = "境界不足";
                return false;
            }

            return true;
        }

        static bool TryParseRealm(string text, out RealmStage realm)
        {
            realm = RealmStage.Mortal;
            if (string.IsNullOrEmpty(text))
                return false;
            if (string.Equals(text, "凡人", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "Mortal", System.StringComparison.OrdinalIgnoreCase))
            {
                realm = RealmStage.Mortal;
                return true;
            }

            if (string.Equals(text, "炼气", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "QiRefining", System.StringComparison.OrdinalIgnoreCase))
            {
                realm = RealmStage.QiRefining;
                return true;
            }

            if (string.Equals(text, "筑基", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "Foundation", System.StringComparison.OrdinalIgnoreCase))
            {
                realm = RealmStage.Foundation;
                return true;
            }

            return System.Enum.TryParse(text, true, out realm);
        }

        void ReleasePause()
        {
            if (!_holdingPause)
                return;
            _holdingPause = false;
            if (bootstrap?.Session != null)
                bootstrap.Session.IsPaused = false;
            HostInputGate.Clear();
        }

        void EnsureStyles()
        {
            if (_title != null)
                return;
            _px = Texture2D.whiteTexture;
            _title = HostImguiStyles.InkLabel(18, bold: true, ink: Ink);
            _body = HostImguiStyles.InkLabel(13, wordWrap: true, ink: Ink);
        }

        void Fill(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _px != null ? _px : Texture2D.whiteTexture);
            GUI.color = prev;
        }

        void DrawFrame(Rect r, Color c)
        {
            Fill(new Rect(r.x, r.y, r.width, 1f), c);
            Fill(new Rect(r.x, r.yMax - 1f, r.width, 1f), c);
            Fill(new Rect(r.x, r.y, 1f, r.height), c);
            Fill(new Rect(r.xMax - 1f, r.y, 1f, r.height), c);
        }
    }
}
