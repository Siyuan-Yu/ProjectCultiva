using UnityEngine;
using XianXia.Core.Combat;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;

namespace XianXia.Unity.Host
{
    /// <summary>背包斗技秘本：选人学习（不消耗；可学多门）。</summary>
    public sealed class HostCombatArtLearnPrompt : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;

        bool _open;
        bool _holdingPause;
        string _itemId = string.Empty;
        string _status = string.Empty;
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
            ReleasePause();
        }

        public void Open(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return;
            _itemId = itemId;
            _status = string.Empty;
            _open = true;
        }

        public void Close()
        {
            _open = false;
            _itemId = string.Empty;
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
                Close();
        }

        void OnGUI()
        {
            if (!_open || bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return;
            EnsureStyles();
            var world = bootstrap.Session.World;
            var artIdText = world.InventoryCatalog.GetTeachesArtId(_itemId);
            CombatArtSpec art = null;
            if (!string.IsNullOrEmpty(artIdText) && DefinitionId.TryParse(artIdText, out var artId))
                world.TryGetCombatArt(artId, out art);

            var dim = new Rect(0f, 0f, Screen.width, Screen.height);
            Fill(dim, new Color(0f, 0f, 0f, 0.45f));
            HostUiHitTest.Block(dim);

            var w = 420f;
            var h = 340f;
            var rect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            HostUiHitTest.Block(rect);
            Fill(rect, Parchment);
            DrawFrame(rect, ParchmentDark);

            var x = rect.x + 16f;
            var y = rect.y + 12f;
            GUI.Label(new Rect(x, y, w - 32f, 26f), "研习斗技", _title);
            y += 30f;
            var name = art == null ? artIdText : (string.IsNullOrEmpty(art.Name) ? art.Id.ToString() : art.Name);
            var grade = art == null || string.IsNullOrEmpty(art.Grade) ? "" : " · " + art.Grade;
            var effect = art == null || string.IsNullOrEmpty(art.EffectSummary)
                ? ""
                : "\n" + art.EffectSummary;
            GUI.Label(
                new Rect(x, y, w - 32f, 64f),
                world.InventoryCatalog.GetName(_itemId) + "（秘本保留）\n" + name + grade + effect,
                _body);
            y += 70f;
            GUI.Label(new Rect(x, y, w - 32f, 22f), "选择学习者：", _body);
            y += 26f;

            var ids = bootstrap.Session.CharacterIds;
            for (var i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                if (!world.Entities.TryGet(id, out var entity))
                    continue;
                var label = string.IsNullOrEmpty(entity.DisplayName) ? id.ToString() : entity.DisplayName;
                if (entity.TryGet<CombatArtsComponent>(out var arts) &&
                    art != null && arts.Knows(art.Id))
                    label += "（已学会）";
                if (HostImguiStyles.ParchmentBtn(new Rect(x, y, w - 32f, 32f), label))
                {
                    var result = new CombatArtItemLearnService().TryLearnFromItem(world, id, _itemId);
                    if (result.IsSuccess)
                    {
                        if (entity.TryGet<CombatArtsComponent>(out var learned) && art != null)
                            learned.TryEquip(art.Id);
                        bootstrap.DispatchDrainedEvents();
                        Close();
                        return;
                    }

                    _status = result.Error.ToString();
                }

                y += 36f;
            }

            if (!string.IsNullOrEmpty(_status))
                GUI.Label(new Rect(x, rect.yMax - 72f, w - 32f, 24f), _status, _body);
            if (HostImguiStyles.ParchmentBtn(new Rect(x, rect.yMax - 40f, 100f, 28f), "取消"))
                Close();
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
