using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Npc;
using XianXia.Data.Content;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Test melee assault on a control core: 20 damage / second while standing near the building,
    /// then accumulate occupy hold seconds after breach until capture.
    /// </summary>
    public sealed class HostControlCoreAssault : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] HostMoveController moveController;

        readonly List<(float X, float Z)> _partyPoints = new List<(float, float)>(8);
        readonly List<EntityId> _actorScratch = new List<EntityId>(8);

        string _targetWorkAreaId = string.Empty;
        float _meleeCooldown;

        public string TargetWorkAreaId => _targetWorkAreaId;

        public bool IsAssaulting => !string.IsNullOrEmpty(_targetWorkAreaId);

        public void Bind(PlayableHostBootstrap host)
        {
            bootstrap = host;
            if (host != null)
            {
                moveController = host.GetComponent<HostMoveController>();
                selectionController = host.GetComponent<HostSelectionController>();
            }
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

            var dt = bootstrap != null
                ? bootstrap.PresentationDeltaTime
                : Time.unscaledDeltaTime;
            if (core.CurrentDurability > 0)
            {
                _meleeCooldown -= dt;
                if (_meleeCooldown <= 0f)
                {
                    var hit = ControlCoreService.ApplyStrike(
                        world, _targetWorkAreaId, ControlCoreService.TestMeleeDamagePerHit);
                    _meleeCooldown = ControlCoreService.TestMeleeIntervalSeconds;
                    if (hit.IsSuccess &&
                        world.ControlCores.TryGet(_targetWorkAreaId, out var after))
                    {
                        Debug.Log(
                            "[Host] 突击主管府 耐久 " +
                            after.CurrentDurability + "/" + after.MaxDurability +
                            (after.CaptureAvailable ? " → 可站立占领" : ""));
                        var overlay = bootstrap.GetComponent<HostFeedbackOverlay>();
                        if (overlay != null && _actorScratch.Count > 0)
                        {
                            overlay.SpawnAtEntity(
                                bootstrap.ViewSpawner,
                                _actorScratch[0],
                                "-" + ControlCoreService.TestMeleeDamagePerHit,
                                new Color(1f, 0.4f, 0.3f, 1f));
                        }
                    }
                }

                return;
            }

            ControlCoreService.TickOccupy(world, _targetWorkAreaId, dt, true);
            if (world.ControlCores.TryGet(_targetWorkAreaId, out core) && core.PlayerControlled)
            {
                Debug.Log("[Host] 已占领主管府，获得住房／课表管理权限。");
                Clear();
            }
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
