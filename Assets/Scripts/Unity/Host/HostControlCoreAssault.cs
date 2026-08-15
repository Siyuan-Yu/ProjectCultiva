using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Npc;
using XianXia.Data.Content;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// 主管府突击：靠近后按正式近战节奏／属性伤害拆耐久，破门后站立占领。
    /// </summary>
    public sealed class HostControlCoreAssault : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] HostMoveController moveController;
        [SerializeField] HostMeleeStrikeVfx strikeVfx;
        [SerializeField] EntityViewSpawner viewSpawner;

        readonly List<(float X, float Z)> _partyPoints = new List<(float, float)>(8);
        readonly List<EntityId> _actorScratch = new List<EntityId>(8);

        string _targetWorkAreaId = string.Empty;
        float _meleeCooldown;

        public string TargetWorkAreaId => _targetWorkAreaId;

        public bool IsAssaulting => !string.IsNullOrEmpty(_targetWorkAreaId);

        public void Bind(PlayableHostBootstrap host)
        {
            bootstrap = host;
            if (host == null)
                return;
            moveController = host.GetComponent<HostMoveController>();
            selectionController = host.GetComponent<HostSelectionController>();
            viewSpawner = host.ViewSpawner;
            strikeVfx = host.GetComponent<HostMeleeStrikeVfx>() ??
                        host.gameObject.AddComponent<HostMeleeStrikeVfx>();
        }

        public void Begin(string workAreaId)
        {
            _targetWorkAreaId = workAreaId ?? string.Empty;
            _meleeCooldown = 0f;
        }

        public void Clear()
        {
            _targetWorkAreaId = string.Empty;
            _meleeCooldown = 0f;
        }

        void Update()
        {
            if (string.IsNullOrEmpty(_targetWorkAreaId) || bootstrap?.Session?.World == null)
                return;
            if (bootstrap.Session.IsPaused)
                return;

            var world = bootstrap.Session.World;
            if (!world.ControlCores.TryGet(_targetWorkAreaId, out var core) || core.PlayerControlled)
            {
                Clear();
                return;
            }

            MapLayoutDefinition layout = null;
            MapLayoutPick.TryGet(bootstrap.Session, out layout);

            CollectAssaultPresentationPoints();
            var near = HostControlCoreQuery.IsAnyPointNear(world, layout, core, _partyPoints);

            if (!near)
            {
                world.ControlCores.ResetOccupyProgress(_targetWorkAreaId);
                return;
            }

            var dt = bootstrap.PresentationDeltaTime;
            if (core.CurrentDurability > 0)
            {
                _meleeCooldown -= dt;
                if (_meleeCooldown <= 0f)
                {
                    _meleeCooldown = MeleeCombatService.DefaultMeleeIntervalSeconds;
                    var attacker = ResolveAttacker();
                    if (attacker.IsNone)
                        return;

                    var hit = ControlCoreService.ApplyStrikeFromAttacker(
                        world, _targetWorkAreaId, attacker, out var dmg);
                    if (hit.IsSuccess &&
                        world.ControlCores.TryGet(_targetWorkAreaId, out var after))
                    {
                        PlayStrikeAtCore(attacker, layout, after);
                        Toast(attacker, "-" + dmg, new Color(1f, 0.45f, 0.3f));
                        if (after.CaptureAvailable)
                            Toast(attacker, "破门·站立占领", new Color(0.55f, 1f, 0.45f));
                    }
                }

                return;
            }

            ControlCoreService.TickOccupy(world, _targetWorkAreaId, dt, true);
            if (world.ControlCores.TryGet(_targetWorkAreaId, out core) && core.PlayerControlled)
            {
                if (_actorScratch.Count > 0)
                    Toast(_actorScratch[0], "已占领主管府", new Color(0.45f, 1f, 0.55f));
                Clear();
            }
        }

        EntityId ResolveAttacker()
        {
            if (_actorScratch.Count > 0)
                return _actorScratch[0];
            return EntityId.None;
        }

        void PlayStrikeAtCore(EntityId attacker, MapLayoutDefinition layout, ControlCoreState core)
        {
            if (strikeVfx == null && bootstrap != null)
                strikeVfx = bootstrap.GetComponent<HostMeleeStrikeVfx>();
            if (strikeVfx == null || viewSpawner == null)
                return;
            if (!viewSpawner.Registry.TryGet(attacker, out var aView) || aView == null)
                return;

            var from = aView.transform.position;
            var to = from;
            if (HostControlCoreQuery.TryGetCenter(bootstrap.Session.World, layout, core, out var center))
                to = center;

            strikeVfx.Play(from, to);
        }

        void Toast(EntityId id, string text, Color color)
        {
            var overlay = bootstrap != null ? bootstrap.GetComponent<HostFeedbackOverlay>() : null;
            if (overlay == null || viewSpawner == null || id.IsNone)
                return;
            overlay.SpawnAtEntity(viewSpawner, id, text, color);
        }

        void CollectAssaultPresentationPoints()
        {
            _partyPoints.Clear();
            _actorScratch.Clear();
            var session = bootstrap.Session;
            var spawner = bootstrap.ViewSpawner;
            if (session == null || spawner == null)
                return;

            if (selectionController != null)
            {
                for (var i = 0; i < selectionController.State.Count; i++)
                {
                    var id = selectionController.State.SelectedIds[i];
                    if (!selectionController.IsPartyUnit(id))
                        continue;
                    AddActorPoint(spawner, id);
                }
            }

            if (_partyPoints.Count > 0)
                return;

            for (var i = 0; i < session.CharacterIds.Count; i++)
                AddActorPoint(spawner, session.CharacterIds[i]);
        }

        void AddActorPoint(EntityViewSpawner spawner, EntityId id)
        {
            if (id.IsNone || !spawner.Registry.TryGet(id, out var view) || view == null)
                return;
            var p = HostPresentationSpace.ToPresentation(view.transform.position);
            _partyPoints.Add((p.x, p.y));
            _actorScratch.Add(id);
        }
    }
}
