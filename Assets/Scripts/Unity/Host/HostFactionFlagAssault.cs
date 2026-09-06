using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.World.Strategic;
using XianXia.Data.Content;

namespace XianXia.Unity.Host
{
    /// <summary>无守军时，靠近阵营旗建筑并按正式近战节奏持续拆除。</summary>
    public sealed class HostFactionFlagAssault : MonoBehaviour
    {
        PlayableHostBootstrap _bootstrap;
        HostSelectionController _selection;
        HostMoveController _move;
        HostMeleeStrikeVfx _vfx;
        readonly List<(float X, float Z)> _points = new List<(float, float)>(8);
        readonly List<EntityId> _actors = new List<EntityId>(8);
        string _flagId = string.Empty;
        float _cooldown;

        public void Bind(PlayableHostBootstrap host)
        {
            _bootstrap = host;
            _selection = host != null ? host.SelectionController : null;
            _move = host != null ? host.MoveController : null;
            _vfx = host != null ? host.GetComponent<HostMeleeStrikeVfx>() : null;
        }

        public void Begin(string flagId)
        {
            _flagId = flagId ?? string.Empty;
            _cooldown = 0f;
        }

        public void Clear() { _flagId = string.Empty; _cooldown = 0f; }

        void Update()
        {
            var session = _bootstrap?.Session;
            var world = session?.World;
            if (string.IsNullOrEmpty(_flagId) || world == null || session.IsPaused)
                return;
            if (!world.Strategic.FactionFlags.Flags.TryGetValue(_flagId, out var flag) || flag == null)
            {
                Clear();
                _bootstrap?.RefreshFactionFlagWalkGrid();
                return;
            }
            MapLayoutPick.TryGet(session, out var layout);
            CollectPoints();
            if (!HostFactionFlagQuery.IsAnyPointNear(flag, layout, _points))
                return;
            _cooldown -= _bootstrap.PresentationDeltaTime;
            if (_cooldown > 0f || _actors.Count == 0)
                return;
            _cooldown = MeleeCombatService.DefaultMeleeIntervalSeconds;
            var attacker = _actors[0];
            var hit = FactionFlagService.ApplyStrikeFromAttacker(
                world, session.PlayerParty, world.Strategic.PlayerFactionId, _flagId, attacker, out var damage);
            if (hit.IsFailure)
            {
                Toast(attacker, "无法突击：" + hit.Error.Message, new Color(1f, .4f, .3f));
                Clear();
                return;
            }
            if (_bootstrap.ViewSpawner.Registry.TryGet(attacker, out var view) && view != null &&
                HostFactionFlagQuery.TryGetCenter(flag, layout, out var center))
                _vfx?.Play(view.transform.position, center);
            Toast(attacker, "-" + damage, new Color(1f, .45f, .3f));
            if (!world.Strategic.FactionFlags.Flags.ContainsKey(_flagId))
            {
                Toast(attacker, "阵营旗已拆除", new Color(1f, .75f, .3f));
                Clear();
                _bootstrap.RefreshFactionFlagWalkGrid();
            }
        }

        void CollectPoints()
        {
            _points.Clear(); _actors.Clear();
            var spawner = _bootstrap.ViewSpawner;
            if (_selection != null)
                for (var i = 0; i < _selection.State.Count; i++)
                    if (_selection.IsPartyUnit(_selection.State.SelectedIds[i]))
                        Add(_selection.State.SelectedIds[i], spawner);
            if (_points.Count == 0)
                for (var i = 0; i < _bootstrap.Session.PlayerParty.Members.Count; i++)
                    Add(_bootstrap.Session.PlayerParty.Members[i], spawner);
        }

        void Add(EntityId id, EntityViewSpawner spawner)
        {
            if (id.IsNone || spawner == null || !spawner.Registry.TryGet(id, out var view) || view == null)
                return;
            var p = HostPresentationSpace.ToPresentation(view.transform.position);
            _points.Add((p.x, p.y)); _actors.Add(id);
        }

        void Toast(EntityId id, string text, Color color) =>
            _bootstrap?.GetComponent<HostFeedbackOverlay>()?.SpawnAtEntity(_bootstrap.ViewSpawner, id, text, color);
    }
}
