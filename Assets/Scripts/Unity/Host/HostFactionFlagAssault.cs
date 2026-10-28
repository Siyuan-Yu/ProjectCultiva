using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>在正式独立遭遇中靠近势力旗，并按既有近战节奏拆除。</summary>
    public sealed class HostFactionFlagAssault : MonoBehaviour
    {
        PlayableHostBootstrap _bootstrap;
        HostSelectionController _selection;
        HostMoveController _move;
        HostMeleeStrikeVfx _vfx;
        readonly List<(float X, float Z)> _points = new List<(float, float)>(8);
        readonly List<EntityId> _actors = new List<EntityId>(8);
        string _flagId = string.Empty;
        public string TargetFlagId => _flagId;
        float _cooldown;
        XianXia.Core.Simulation.SimulationWorld _world;

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
            _world = _bootstrap?.Session?.World;
        }

        public void Clear() { _flagId = string.Empty; _cooldown = 0f; }

        void Update()
        {
            var session = _bootstrap?.Session;
            var world = session?.World;
            if (string.IsNullOrEmpty(_flagId) || world == null || session.IsPaused)
                return;
            if (!ReferenceEquals(_world, world)) { Clear(); return; }
            var encounter = world.Strategic.CharacterEncounter;
            if (encounter == null || encounter.Phase != CharacterEncounterPhase.Active ||
                encounter.Find(session.PlayerParty.ActiveCharacterId.Value)?.TargetId != ulong.MaxValue)
            { Clear(); return; }
            if (!world.Strategic.FactionFlags.Flags.TryGetValue(_flagId, out var flag) || flag == null)
            {
                Clear();
                _bootstrap?.RefreshFactionFlagWalkGrid();
                return;
            }
            CollectPoints();
            var continuous = _bootstrap.ContinuousOutdoorSurfaceRuntime;
            if (continuous == null || !continuous.IsActive ||
                !HostFactionFlagQuery.IsAnyPointNear(flag, continuous, _points))
                return;
            var combatActor = encounter?.Find(session.PlayerParty.ActiveCharacterId.Value);
            var manual = !HostRpgControls.SceneRts;
            if (manual && _bootstrap.GetComponent<HostDirectionalRangedAttack>()?.ConsumeStructureSwing(
                    session.PlayerParty.ActiveCharacterId, _flagId) != true) return;
            if (combatActor != null) _cooldown = combatActor.Cooldown;
            else _cooldown -= _bootstrap.PresentationDeltaTime;
            if ((!manual && _cooldown > 0f) || _actors.Count == 0)
                return;
            _cooldown = MeleeCombatService.DefaultMeleeIntervalSeconds;
            if (combatActor != null) combatActor.Cooldown = _cooldown;
            var attacker = manual ? session.PlayerParty.ActiveCharacterId : _actors[0];
            var hit = FactionFlagService.ApplyStrikeFromAttacker(
                world, session.PlayerParty, world.Strategic.PlayerFactionId, _flagId, attacker, out var damage);
            if (hit.IsFailure)
            {
                Toast(attacker, "无法突击：" + hit.Error.Message, new Color(1f, .4f, .3f));
                Clear();
                return;
            }
            if (_bootstrap.ViewSpawner.Registry.TryGet(attacker, out var view) && view != null)
            {
                if (HostFactionFlagQuery.TryGetCenter(flag, continuous, out var center))
                {
                    view.PlayArtAttack(center);
                    _vfx?.Play(view.transform.position, center);
                }
            }
            Toast(attacker, "-" + damage, new Color(1f, .45f, .3f));
            if (!world.Strategic.FactionFlags.Flags.ContainsKey(_flagId))
            {
                Toast(attacker, "敌方势力旗已摧毁。", new Color(1f, .75f, .3f));
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
            var world = _bootstrap.Session.World;
            var encounter = world.Strategic.CharacterEncounter;
            var participant = encounter?.Find(id.Value);
            if (encounter != null && (participant == null || participant.Enemy)) return;
            if (!CharacterEncounterService.IsLiving(world, id.Value)) return;
            if (id.IsNone || spawner == null || !spawner.Registry.TryGet(id, out var view) || view == null)
                return;
            var p = HostPresentationSpace.ToPresentation(view.transform.position);
            _points.Add((p.x, p.y)); _actors.Add(id);
        }

        void Toast(EntityId id, string text, Color color) =>
            _bootstrap?.GetComponent<HostFeedbackOverlay>()?.SpawnAtEntity(_bootstrap.ViewSpawner, id, text, color);
    }
}
