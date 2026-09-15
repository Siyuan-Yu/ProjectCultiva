using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Npc;
using XianXia.Core.World.Strategic;
using XianXia.Data.Content;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// 议政厅突击：靠近后按正式近战节奏／属性伤害拆耐久，破门后站立占领。
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
        bool _contested;
        XianXia.Core.Simulation.SimulationWorld _world;

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
            _world = bootstrap?.Session?.World;
        }

        public void Clear()
        {
            _targetWorkAreaId = string.Empty;
            _meleeCooldown = 0f;
        }

        void Update()
        {
            var objective = bootstrap?.Session?.World?.Strategic?.CharacterEncounter?.Objective;
            if (string.IsNullOrEmpty(_targetWorkAreaId) && objective != null && !objective.Resolved &&
                objective.Kind == SiteCoreObjectiveKind.FixedSiteCoreCapture &&
                WorldSiteCoreWarfareService.TryGetFixedCore(bootstrap.Session.World, objective.SiteId, out var occupying) && occupying.CaptureAvailable)
            { _targetWorkAreaId = occupying.WorkAreaId; _world = bootstrap.Session.World; }
            if (string.IsNullOrEmpty(_targetWorkAreaId) || bootstrap?.Session?.World == null)
                return;
            if (bootstrap.Session.IsPaused)
                return;

            var world = bootstrap.Session.World;
            if (!ReferenceEquals(_world, world)) { Clear(); return; }
            var encounter = world.Strategic.CharacterEncounter;
            if (encounter != null && encounter.Phase == CharacterEncounterPhase.Committed) { Clear(); return; }
            if (!world.ControlCores.TryGet(_targetWorkAreaId, out var core))
            {
                Clear();
                return;
            }

            MapLayoutDefinition layout = null;
            if (bootstrap.ContinuousOutdoorSurfaceRuntime == null ||
                !bootstrap.ContinuousOutdoorSurfaceRuntime.IsActive)
                MapLayoutPick.TryGet(bootstrap.Session, out layout);

            CollectAssaultPresentationPoints();
            if (!core.CaptureAvailable && encounter != null && encounter.Find(bootstrap.Session.PlayerParty.ActiveCharacterId.Value)?.TargetId != ulong.MaxValue)
            { Clear(); return; }
            var near = HostControlCoreQuery.IsAnyPointNear(
                world, layout, bootstrap.ContinuousOutdoorSurfaceRuntime, core, _partyPoints);

            if (!near && !core.CaptureAvailable)
            {
                world.ControlCores.ResetOccupyProgress(_targetWorkAreaId);
                return;
            }

            var dt = bootstrap.PresentationDeltaTime;
            if (core.CurrentDurability > 0)
            {
                var combatActor = encounter?.Find(bootstrap.Session.PlayerParty.ActiveCharacterId.Value);
                if (combatActor != null) _meleeCooldown = combatActor.Cooldown;
                else _meleeCooldown -= dt;
                if (_meleeCooldown <= 0f)
                {
                    _meleeCooldown = MeleeCombatService.DefaultMeleeIntervalSeconds;
                    if (combatActor != null) combatActor.Cooldown = _meleeCooldown;
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
                            Toast(attacker, "核心已攻破 · 进入范围持续占领", new Color(0.55f, 1f, 0.45f));
                    }
                    else if (hit.IsFailure)
                    {
                        // 领域层战争门槛是最终权威。政治状态在靠近途中发生变化时，
                        // 立即停止本次突击，避免 Host 继续无效尝试。
                        Toast(attacker, "无法突击：" + hit.Error.Message, new Color(1f, 0.45f, 0.35f));
                        Clear();
                    }
                }

                return;
            }

            if (encounter != null) bootstrap.ContinuousOutdoorSurfaceRuntime.CaptureIndependentField();
            else bootstrap.ContinuousOutdoorSurfaceRuntime.CaptureCurrentPersonalPlacements();
            HostControlCoreQuery.TryGetFootprint(world, layout, bootstrap.ContinuousOutdoorSurfaceRuntime, core,
                out var minX, out var maxX, out var minY, out var maxY, out _, out _);
            bool InOccupyArea(float x, float y)
            {
                bootstrap.ContinuousOutdoorSurfaceRuntime.Mapper.WorldToPresentation(x, y, out var px, out var py);
                return px >= minX - HostControlCoreQuery.MeleeMargin && px <= maxX + HostControlCoreQuery.MeleeMargin &&
                    py >= minY - HostControlCoreQuery.MeleeMargin && py <= maxY + HostControlCoreQuery.MeleeMargin;
            }
            WorldSiteCoreWarfareService.TickOccupation(world, _targetWorkAreaId, dt, out var contested, InOccupyArea);
            if (contested && !_contested) Toast(bootstrap.Session.PlayerParty.ActiveCharacterId, "占领被守军阻断", new Color(1f, .6f, .3f));
            _contested = contested;
            if (world.ControlCores.TryGet(_targetWorkAreaId, out core) && !core.CaptureAvailable && core.CurrentDurability > 0)
            {
                if (_actorScratch.Count > 0)
                    Toast(_actorScratch[0], "已占领议政厅", new Color(0.45f, 1f, 0.55f));
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
            if (HostControlCoreQuery.TryGetCenter(bootstrap.Session.World, layout,
                    bootstrap.ContinuousOutdoorSurfaceRuntime, core, out var center))
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
            if (!CharacterEncounterService.IsLiving(bootstrap.Session.World, id.Value)) return;
            if (id.IsNone || !spawner.Registry.TryGet(id, out var view) || view == null)
                return;
            var p = HostPresentationSpace.ToPresentation(view.transform.position);
            _partyPoints.Add((p.x, p.y));
            _actorScratch.Add(id);
        }
    }
}
