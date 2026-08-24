using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Persistence;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>Development / Acceptance UI 非正式游UX。F8 切换/summary>
    public sealed class HostStrategicAcceptancePanel : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] KeyCode toggleKey = KeyCode.F8;

        bool _visible;
        Vector2 _scroll;
        string _status = string.Empty;
        int _factionAIndex;
        int _factionBIndex;
        int _overlordIndex;
        int _vassalIndex;
        int _tributePayerIndex;
        int _tributeReceiverIndex;
        readonly List<string> _factions = new List<string>(16);
        GUIStyle _title;
        GUIStyle _body;
        GUIStyle _warn;
        Texture2D _px;
        float _contentHeight;

        public bool IsVisible => _visible;

        public void Bind(PlayableHostBootstrap host) => bootstrap = host;

        public void Show()
        {
            _visible = true;
            if (bootstrap?.SnapshotPanel != null)
                bootstrap.SnapshotPanel.ShowAcceptanceButtons = true;
        }

        public void Hide()
        {
            _visible = false;
            if (bootstrap?.SnapshotPanel != null)
                bootstrap.SnapshotPanel.ShowAcceptanceButtons = false;
        }

        public void Toggle()
        {
            _visible = !_visible;
            if (bootstrap?.SnapshotPanel != null)
                bootstrap.SnapshotPanel.ShowAcceptanceButtons = _visible;
        }

        void Update()
        {
            if (Input.GetKeyDown(toggleKey))
                Toggle();
        }

        void OnGUI()
        {
            if (!_visible || bootstrap == null || !bootstrap.Session.IsInitialized)
                return;

            EnsureStyles();
            GUI.depth = -85;

            var world = bootstrap.Session.World;
            StrategicAcceptanceInspector.CollectKnownFactionIds(world, _factions);
            ClampPickerIndices();

            var boxW = Mathf.Min(580f, Screen.width - 24f);
            var boxH = Screen.height - 60f;
            var boxX = 12f;
            var boxY = 48f;
            if (bootstrap.WorldMapPanel != null && bootstrap.WorldMapPanel.IsOpen)
            {
                boxX = Mathf.Max(12f, Screen.width - boxW - 12f);
            }
            else
            {
                var formalHud = bootstrap.GetComponent<HostFormalHud>();
                if (formalHud != null && formalHud.IsHudVisible)
                    boxY = HostFormalHud.HeaderReservedHeight + 8f;
                boxX = Mathf.Max(12f, Screen.width - boxW - 12f);
            }

            var box = new Rect(boxX, boxY, boxW, boxH);
            var prev = GUI.color;
            GUI.color = new Color(0.10f, 0.11f, 0.13f, 0.97f);
            GUI.DrawTexture(box, _px);
            GUI.color = prev;

            GUI.Label(new Rect(box.x + 12f, box.y + 8f, box.width - 24f, 24f),
                "STRATEGIC ACCEPTANCE  [DEVELOPMENT / ACCEPTANCE UI]",
                _warn);
            GUI.Label(new Rect(box.x + 12f, box.y + 30f, box.width - 24f, 18f),
                "非正式外交／验收界面 — 仅用于 Domain 手操验证",
                _body);

            var viewRect = new Rect(box.x + 8f, box.y + 52f, box.width - 16f, box.height - 96f);
            _scroll = GUI.BeginScrollView(viewRect, _scroll, new Rect(0f, 0f, viewRect.width - 18f, _contentHeight));

            var y = 0f;
            var w = viewRect.width - 18f;
            y = DrawReadOnlySections(world, w, y);
            y = DrawWarControls(world, w, y);
            y = DrawAllianceControls(world, w, y);
            y = DrawVassalageControls(world, w, y);
            y = DrawTributeControls(world, w, y);
            y = DrawSnapshotControls(w, y);

            _contentHeight = y + 12f;
            GUI.EndScrollView();

            if (GUI.Button(new Rect(box.x + 12f, box.yMax - 36f, 80f, 28f), "关闭 F8"))
                Hide();
            if (!string.IsNullOrEmpty(_status))
                GUI.Label(new Rect(box.x + 100f, box.yMax - 34f, box.width - 112f, 24f), _status, _body);
        }

        float DrawReadOnlySections(SimulationWorld world, float w, float y)
        {
            y = DrawSectionHeader(w, y, "[FACTIONS / Strategic Status]");
            for (var i = 0; i < _factions.Count; i++)
            {
                var id = _factions[i];
                var block = StrategicFactionCatalog.DisplayName(id) + "\n" +
                            "  FactionId: " + id + "\n" +
                            "  Nodes: " + StrategicAcceptanceInspector.CountOwnedNodes(world, id) + "\n" +
                            "  Armies: " + StrategicAcceptanceInspector.CountFormalArmies(world, id) + "\n" +
                            "  Landless: " + (StrategicAcceptanceInspector.IsLandless(world, id) ? "YES" : "NO");
                y = DrawWrapped(w, y, block);
                y += 6f;
            }

            y = DrawSectionHeader(w, y, "[SNAPSHOT]");
            var anyWar = false;
            if (world.Strategic?.Wars?.All != null)
            {
                foreach (var kv in world.Strategic.Wars.All)
                {
                    var war = kv.Value;
                    if (war == null || !war.Active)
                        continue;
                    anyWar = true;
                    y = DrawWrapped(w, y,
                        war.WarId + "  Active=YES\n" +
                        "  Attackers: " + string.Join(", ", war.Attackers) + "\n" +
                        "  Defenders: " + string.Join(", ", war.Defenders));
                }
            }

            if (!anyWar)
                y = DrawWrapped(w, y, "(none)");

            y = DrawSectionHeader(w, y, "[SITES]");
            if (world.Strategic?.Sites?.Sites != null)
            {
                foreach (var kv in world.Strategic.Sites.Sites)
                {
                    var site = kv.Value;
                    if (site == null)
                        continue;
                    var label = string.IsNullOrEmpty(site.DisplayName) ? site.SiteId : site.DisplayName;
                    y = DrawWrapped(w, y,
                        label + "\n  Owner: " + StrategicAcceptanceInspector.ResolveOwnerDisplay(site.OwnerFactionId));
                }
            }

            y = DrawSectionHeader(w, y, "[RETREATING ARMIES]");
            if (world.Strategic?.RetreatingArmies?.All == null ||
                world.Strategic.RetreatingArmies.All.Count == 0)
            {
                y = DrawWrapped(w, y, "(none)");
            }
            else
            {
                foreach (var kv in world.Strategic.RetreatingArmies.All)
                {
                    var army = kv.Value;
                    if (army == null)
                        continue;
                    y = DrawWrapped(w, y,
                        army.RetreatingArmyId + "  Faction=" + army.FactionId +
                        "  Hex=" + (army.UsesHexPosition ? army.HexQ + "," + army.HexR : "none") +
                        "  Members=" + army.MemberCharacterIds.Count);
                }
            }

            return y;
        }

        float DrawWarControls(SimulationWorld world, float w, float y)
        {
            y = DrawSectionHeader(w, y, "[SNAPSHOT]");
            if (_factions.Count < 2)
            {
                y = DrawWrapped(w, y, "Need at least 2 factions.");
                return y;
            }

            y = DrawFactionPicker(w, y, "Faction A", ref _factionAIndex);
            y = DrawFactionPicker(w, y, "Faction B", ref _factionBIndex);
            var fa = _factions[_factionAIndex];
            var fb = _factions[_factionBIndex];
            y = DrawWrapped(w, y,
                StrategicFactionCatalog.DisplayName(fa) + " vs " + StrategicFactionCatalog.DisplayName(fb) +
                "  At War: " + (StrategicAcceptanceCommands.IsAtWar(world, fa, fb) ? "YES" : "NO"));

            if (GUI.Button(new Rect(0f, y, w, 26f), "[ACCEPTANCE] Declare War"))
            {
                var result = StrategicAcceptanceCommands.TryDeclareWar(world, fa, fb);
                _status = result.IsSuccess
                    ? "Declare War OK"
                    : "Declare War FAIL: " + result.Error.Message;
            }

            y += 30f;
            return y;
        }

        float DrawAllianceControls(SimulationWorld world, float w, float y)
        {
            y = DrawSectionHeader(w, y, "[SNAPSHOT]");
            if (world.Strategic?.Alliances?.All != null)
            {
                foreach (var kv in world.Strategic.Alliances.All)
                {
                    y = DrawWrapped(w, y, kv.Key + "  Members: " + string.Join(", ", kv.Value));
                }
            }

            if (_factions.Count < 2)
                return y + 4f;

            y = DrawFactionPicker(w, y, "Member A", ref _factionAIndex);
            y = DrawFactionPicker(w, y, "Member B", ref _factionBIndex);
            if (GUI.Button(new Rect(0f, y, w, 26f), "[ACCEPTANCE] Create Alliance"))
            {
                var result = StrategicAcceptanceCommands.TryFormAlliance(
                    world, _factions[_factionAIndex], _factions[_factionBIndex]);
                _status = result.IsSuccess
                    ? "Create Alliance OK"
                    : "Create Alliance FAIL: " + result.Error.Message;
            }

            y += 30f;
            return y;
        }

        float DrawVassalageControls(SimulationWorld world, float w, float y)
        {
            y = DrawSectionHeader(w, y, "[SNAPSHOT]");
            if (world.Strategic?.Vassalages?.All != null)
            {
                foreach (var kv in world.Strategic.Vassalages.All)
                {
                    y = DrawWrapped(w, y, "Overlord: " + kv.Value + "  Vassal: " + kv.Key);
                }
            }

            if (_factions.Count < 2)
                return y + 4f;

            y = DrawFactionPicker(w, y, "Overlord", ref _overlordIndex);
            y = DrawFactionPicker(w, y, "Vassal", ref _vassalIndex);
            if (GUI.Button(new Rect(0f, y, w, 26f), "[ACCEPTANCE] Create Vassalage (TEST)"))
            {
                var result = StrategicAcceptanceCommands.TryBindVassalage(
                    world, _factions[_overlordIndex], _factions[_vassalIndex]);
                _status = result.IsSuccess
                    ? "Create Vassalage OK"
                    : "Create Vassalage FAIL: " + result.Error.Message;
            }

            y += 30f;
            return y;
        }

        float DrawTributeControls(SimulationWorld world, float w, float y)
        {
            y = DrawSectionHeader(w, y, "[SNAPSHOT]");
            y = DrawWrapped(w, y, "Placeholder amount: " + TributeService.PlaceholderTributeAmount);
            if (_factions.Count < 2)
                return y + 4f;

            y = DrawFactionPicker(w, y, "Payer", ref _tributePayerIndex);
            y = DrawFactionPicker(w, y, "Receiver", ref _tributeReceiverIndex);
            if (GUI.Button(new Rect(0f, y, w, 26f), "[TEST] Invoke Tribute Hook"))
            {
                var result = StrategicAcceptanceCommands.TryCollectTribute(
                    world,
                    _factions[_tributePayerIndex],
                    _factions[_tributeReceiverIndex],
                    out var amount);
                _status = result.IsSuccess
                    ? "Tribute hook OK (placeholder " + amount + ")"
                    : "Tribute FAIL: " + result.Error.Message;
            }

            y += 30f;
            return y;
        }

        float DrawSnapshotControls(float w, float y)
        {
            y = DrawSectionHeader(w, y, "[SNAPSHOT]");
            var snap = bootstrap.SnapshotPanel;
            y = DrawWrapped(w, y,
                "Schema v" + WorldSnapshot.CurrentSchemaVersion + "\n" +
                "Save Snapshot v2: F5\nLoad Snapshot v2: F9\n" +
                "Last Save: " + (snap != null ? snap.LastSaveLabel : "N/A") + "\n" +
                "Last Load: " + (snap != null ? snap.LastLoadLabel : "N/A"));

            if (snap != null)
            {
                if (GUI.Button(new Rect(0f, y, w * 0.48f, 26f), "[ACCEPTANCE] Save v2"))
                {
                    var ok = snap.TrySave();
                    _status = ok ? "Save SUCCESS" : "Save FAIL";
                }

                if (GUI.Button(new Rect(w * 0.52f, y, w * 0.48f, 26f), "[ACCEPTANCE] Load v2"))
                {
                    var ok = snap.TryLoad();
                    _status = ok ? "Load SUCCESS" : "Load FAIL";
                }

                y += 30f;
            }

            return y;
        }

        float DrawSectionHeader(float w, float y, string title)
        {
            GUI.Label(new Rect(0f, y, w, 22f), title, _title);
            return y + 24f;
        }

        float DrawWrapped(float w, float y, string text)
        {
            var h = _body.CalcHeight(new GUIContent(text), w);
            GUI.Label(new Rect(0f, y, w, h), text, _body);
            return y + h + 4f;
        }

        float DrawFactionPicker(float w, float y, string label, ref int index)
        {
            if (_factions.Count == 0)
                return y;
            GUI.Label(new Rect(0f, y, w * 0.35f, 20f), label, _body);
            if (GUI.Button(new Rect(w * 0.36f, y, 28f, 20f), "<"))
                index = (index + _factions.Count - 1) % _factions.Count;
            GUI.Label(new Rect(w * 0.44f, y, w * 0.48f, 20f),
                StrategicFactionCatalog.DisplayName(_factions[index]), _body);
            if (GUI.Button(new Rect(w * 0.94f, y, 28f, 20f), ">"))
                index = (index + 1) % _factions.Count;
            return y + 22f;
        }

        void ClampPickerIndices()
        {
            if (_factions.Count == 0)
            {
                _factionAIndex = _factionBIndex = 0;
                _overlordIndex = _vassalIndex = 0;
                _tributePayerIndex = _tributeReceiverIndex = 0;
                return;
            }

            _factionAIndex = Mathf.Clamp(_factionAIndex, 0, _factions.Count - 1);
            _factionBIndex = Mathf.Clamp(_factionBIndex, 0, _factions.Count - 1);
            _overlordIndex = Mathf.Clamp(_overlordIndex, 0, _factions.Count - 1);
            _vassalIndex = Mathf.Clamp(_vassalIndex, 0, _factions.Count - 1);
            _tributePayerIndex = Mathf.Clamp(_tributePayerIndex, 0, _factions.Count - 1);
            _tributeReceiverIndex = Mathf.Clamp(_tributeReceiverIndex, 0, _factions.Count - 1);
        }

        void EnsureStyles()
        {
            if (_title != null)
                return;
            _px = Texture2D.whiteTexture;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            _warn = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.95f, 0.55f, 0.25f) }
            };
        }
    }
}
