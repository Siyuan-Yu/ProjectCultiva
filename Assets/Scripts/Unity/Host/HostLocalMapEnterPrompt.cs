using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// 入洞确认弹窗：打勾选择随行己方（含当前框选）。
    /// </summary>
    public sealed class HostLocalMapEnterPrompt : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] HostCommandBridge commandBridge;
        [SerializeField] HostMoveController moveController;

        bool _open;
        bool _holdingPause;
        string _entranceLocationId = string.Empty;
        string _entranceName = string.Empty;
        EntityId _leader = EntityId.None;
        readonly List<EntityId> _candidates = new List<EntityId>(8);
        readonly List<bool> _selected = new List<bool>(8);
        Vector2 _scroll;

        GUIStyle _title;
        GUIStyle _body;
        GUIStyle _toggle;
        Texture2D _px;

        static readonly Color Parchment = new Color(0.92f, 0.86f, 0.74f, 0.98f);
        static readonly Color ParchmentDark = new Color(0.70f, 0.58f, 0.42f, 1f);
        static readonly Color Ink = new Color(0.22f, 0.16f, 0.10f, 1f);

        public bool IsOpen => _open;

        public void Bind(
            PlayableHostBootstrap host,
            HostSelectionController selection,
            HostCommandBridge bridge,
            HostMoveController move)
        {
            bootstrap = host;
            selectionController = selection;
            commandBridge = bridge;
            moveController = move;
        }

        public void ClearSessionState() => Close();

        public void Open(EntityId leader, string entranceLocationId)
        {
            var session = bootstrap?.Session;
            if (session == null || !session.IsInitialized || leader.IsNone ||
                string.IsNullOrWhiteSpace(entranceLocationId))
                return;
            if (!session.World.WorldRegion.TryGet(entranceLocationId, out var entrance))
                return;
            if (!OpportunityEntranceRules.IsRevealed(session.World, entrance))
                return;

            _leader = leader;
            _entranceLocationId = entrance.Id;
            _entranceName = string.IsNullOrEmpty(entrance.Name) ? entrance.Id : entrance.Name;
            RebuildCandidates(session, entrance);
            _scroll = Vector2.zero;
            _open = true;
        }

        public void Close()
        {
            _open = false;
            _entranceLocationId = string.Empty;
            _entranceName = string.Empty;
            _leader = EntityId.None;
            _candidates.Clear();
            _selected.Clear();
            ReleasePause();
        }

        void Update()
        {
            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return;
            if (!_open)
            {
                ReleasePause();
                return;
            }

            HostInputGate.BlockWorldCamera = true;
            HostInputGate.BlockWorldInteraction = true;
            if (!_holdingPause)
            {
                bootstrap.Session.IsPaused = true;
                _holdingPause = true;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
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

        void RebuildCandidates(PlayableHostSession session, WorldLocationState entrance)
        {
            _candidates.Clear();
            _selected.Clear();

            // 主导
            AddCandidate(_leader, selected: true);

            // 当前框选的己方（优先，默认勾选）
            if (selectionController != null)
            {
                for (var i = 0; i < selectionController.State.Count; i++)
                {
                    var id = selectionController.State.SelectedIds[i];
                    if (id.IsNone || id == _leader)
                        continue;
                    if (!selectionController.IsPartyUnit(id))
                        continue;
                    AddCandidate(id, selected: true);
                }
            }

            // 洞口附近未选中的己方（默认勾选，可取消）
            var spawner = bootstrap.ViewSpawner;
            for (var i = 0; i < session.CharacterIds.Count; i++)
            {
                var id = session.CharacterIds[i];
                if (id.IsNone || ContainsCandidate(id))
                    continue;
                if (!IsNearbyParty(session, spawner, id, entrance))
                    continue;
                AddCandidate(id, selected: true);
            }
        }

        void AddCandidate(EntityId id, bool selected)
        {
            if (id.IsNone || ContainsCandidate(id))
                return;
            _candidates.Add(id);
            _selected.Add(selected);
        }

        bool ContainsCandidate(EntityId id)
        {
            for (var i = 0; i < _candidates.Count; i++)
            {
                if (_candidates[i] == id)
                    return true;
            }

            return false;
        }

        static bool IsNearbyParty(
            PlayableHostSession session,
            EntityViewSpawner spawner,
            EntityId id,
            WorldLocationState entrance)
        {
            float px, pz;
            if (spawner != null && spawner.Registry.TryGet(id, out var view) && view != null)
            {
                var p = HostPresentationSpace.ToPresentation(view.transform.position);
                px = p.x;
                pz = p.y;
            }
            else if (session.World.Entities.TryGet(id, out var entity) &&
                     entity.TryGet<EntityLocationComponent>(out var loc) &&
                     loc.HasLocation &&
                     session.World.WorldRegion.TryGet(loc.LocationId, out var place))
            {
                px = place.PresentationX;
                pz = place.PresentationZ;
            }
            else
                return false;

            var dx = entrance.PresentationX - px;
            var dz = entrance.PresentationZ - pz;
            var r = HostCaveEntranceQuery.NearbyPartyRadius;
            return dx * dx + dz * dz <= r * r;
        }

        void OnGUI()
        {
            if (!_open || bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return;
            EnsureStyles();

            var dim = new Rect(0f, 0f, Screen.width, Screen.height);
            HostUiHitTest.Block(dim);
            Fill(dim, new Color(0f, 0f, 0f, 0.45f));

            var rowH = 28f;
            var listH = Mathf.Min(220f, Mathf.Max(1, _candidates.Count) * rowH + 8f);
            var h = 128f + listH + 56f;
            var w = 440f;
            var box = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            HostUiHitTest.Block(box);
            Fill(box, Parchment);
            DrawFrame(box, ParchmentDark);

            GUI.Label(new Rect(box.x + 16f, box.y + 12f, box.width - 32f, 26f),
                "进入「" + _entranceName + "」", _title);
            GUI.Label(new Rect(box.x + 16f, box.y + 42f, box.width - 32f, 40f),
                "在下方打勾选择一起进入的己方；未勾选者留在地表。", _body);

            var listRect = new Rect(box.x + 14f, box.y + 88f, box.width - 28f, listH);
            Fill(listRect, new Color(1f, 1f, 1f, 0.22f));
            DrawFrame(listRect, ParchmentDark);

            var contentH = _candidates.Count * rowH + 6f;
            var view = new Rect(0f, 0f, listRect.width - 18f, contentH);
            _scroll = GUI.BeginScrollView(listRect, _scroll, view);
            for (var i = 0; i < _candidates.Count; i++)
            {
                var name = ResolveName(_candidates[i]);
                var isLeader = _candidates[i] == _leader;
                var label = isLeader ? name + "（主导·必进）" : name;
                var row = new Rect(8f, 4f + i * rowH, view.width - 12f, rowH - 4f);
                if (isLeader)
                {
                    GUI.enabled = false;
                    GUI.Toggle(row, true, label, _toggle);
                    GUI.enabled = true;
                    _selected[i] = true;
                }
                else
                {
                    _selected[i] = GUI.Toggle(row, _selected[i], label, _toggle);
                }
            }

            GUI.EndScrollView();

            var btnW = (box.width - 40f) * 0.5f;
            var btnY = box.yMax - 44f;
            if (HostImguiStyles.ParchmentBtn(new Rect(box.x + 14f, btnY, btnW, 32f), "确认进入"))
            {
                Event.current.Use();
                ConfirmEnter();
            }

            if (HostImguiStyles.ParchmentBtn(new Rect(box.x + 22f + btnW, btnY, btnW, 32f), "取消"))
            {
                Event.current.Use();
                Close();
            }
        }

        void ConfirmEnter()
        {
            var session = bootstrap?.Session;
            if (session == null || string.IsNullOrEmpty(_entranceLocationId) || _leader.IsNone)
            {
                Close();
                return;
            }

            if (!session.World.WorldRegion.TryGet(_entranceLocationId, out var entrance))
            {
                Close();
                return;
            }

            var party = new List<EntityId>(4);
            for (var i = 0; i < _candidates.Count; i++)
            {
                if (_selected[i])
                    party.Add(_candidates[i]);
            }

            if (party.Count == 0 || !ContainsId(party, _leader))
            {
                if (!ContainsId(party, _leader))
                    party.Insert(0, _leader);
            }

            if (!IsLeaderNear(session, entrance))
            {
                if (moveController != null &&
                    HostCaveEntranceQuery.TryGetCenter(session.World, _entranceLocationId, out var center))
                {
                    var pending = party.ToArray();
                    var entranceId = _entranceLocationId;
                    var leader = _leader;
                    Close();
                    moveController.OrderEntityToWorldPointPublic(
                        leader,
                        center,
                        onArrive: () => FinishEnter(leader, entranceId, pending));
                    return;
                }
            }

            FinishEnter(_leader, _entranceLocationId, party.ToArray());
            Close();
        }

        static bool ContainsId(List<EntityId> list, EntityId id)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] == id)
                    return true;
            }

            return false;
        }

        bool IsLeaderNear(PlayableHostSession session, WorldLocationState entrance)
        {
            var spawner = bootstrap.ViewSpawner;
            float px, pz;
            if (spawner != null && spawner.Registry.TryGet(_leader, out var view) && view != null)
            {
                var p = HostPresentationSpace.ToPresentation(view.transform.position);
                px = p.x;
                pz = p.y;
            }
            else if (session.World.Entities.TryGet(_leader, out var entity) &&
                     entity.TryGet<EntityLocationComponent>(out var loc) &&
                     loc.HasLocation &&
                     session.World.WorldRegion.TryGet(loc.LocationId, out var place))
            {
                px = place.PresentationX;
                pz = place.PresentationZ;
            }
            else
                return false;

            return HostCaveEntranceQuery.IsNearEntrance(px, pz, entrance);
        }

        void FinishEnter(EntityId leader, string entranceId, EntityId[] party)
        {
            if (commandBridge == null || party == null || party.Length == 0)
                return;
            commandBridge.IssueEnterLocalMapWithParty(leader, entranceId, party);
        }

        string ResolveName(EntityId id)
        {
            var session = bootstrap?.Session;
            if (session != null && session.World.Entities.TryGet(id, out var e) &&
                !string.IsNullOrEmpty(e.DisplayName))
                return e.DisplayName;
            return id.ToString();
        }

        void EnsureStyles()
        {
            if (_title != null)
                return;
            _px = Texture2D.whiteTexture;
            _title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Ink }
            };
            _body = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true,
                normal = { textColor = Ink }
            };
            _toggle = new GUIStyle(GUI.skin.toggle)
            {
                fontSize = 14,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                wordWrap = false
            };
            _toggle.normal.textColor = Ink;
            _toggle.onNormal.textColor = Ink;
            _toggle.hover.textColor = Ink;
            _toggle.onHover.textColor = Ink;
            _toggle.active.textColor = Ink;
            _toggle.onActive.textColor = Ink;
            _toggle.focused.textColor = Ink;
            _toggle.onFocused.textColor = Ink;
            _toggle.padding = new RectOffset(4, 4, 2, 2);
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
            const float t = 1f;
            Fill(new Rect(r.x, r.y, r.width, t), c);
            Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            Fill(new Rect(r.x, r.y, t, r.height), c);
            Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
        }
    }
}
