using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// 战斗 Alpha：下令攻击后持续追击互砍，直到目标倒下，或玩家下令移动／Stop 打断。
    /// 支持多名己方同时攻击同一目标；开斗气纱衣时交战距离用远程半径。
    /// </summary>
    public sealed class HostNpcMeleeAssault : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] EntityViewSpawner viewSpawner;
        [SerializeField] HostMoveController moveController;
        [SerializeField] HostMeleeStrikeVfx strikeVfx;
        [SerializeField] float meleeRange = HostNpcInteraction.DefaultMeleeEngageRange;
        [SerializeField] float repathInterval = 0.2f;

        readonly MeleeCombatService _melee = new MeleeCombatService();
        readonly SpiritVeilService _veil = new SpiritVeilService();
        readonly List<EntityId> _attackers = new List<EntityId>(4);
        readonly Dictionary<ulong, float> _cooldownByAttacker = new Dictionary<ulong, float>(4);
        readonly Dictionary<ulong, float> _nextRepathByAttacker = new Dictionary<ulong, float>(4);
        readonly List<EntityId> _scratch = new List<EntityId>(4);

        EntityId _defender = EntityId.None;
        float _nextStrikeFailToast;
        float _defenderCooldown;
        int _counterFocus;

        public bool IsFighting => _attackers.Count > 0 && !_defender.IsNone;
        /// <summary>主攻方（名单首位）；兼容旧调用。</summary>
        public EntityId AttackerId => _attackers.Count > 0 ? _attackers[0] : EntityId.None;
        public EntityId DefenderId => _defender;
        public float MeleeRange => meleeRange;

        public bool IsAttacker(EntityId id)
        {
            if (id.IsNone)
                return false;
            for (var i = 0; i < _attackers.Count; i++)
            {
                if (_attackers[i] == id)
                    return true;
            }

            return false;
        }

        public bool IsInFight(EntityId id) =>
            IsAttacker(id) || (!id.IsNone && id == _defender);

        public void Bind(PlayableHostBootstrap host)
        {
            bootstrap = host;
            if (host != null)
            {
                viewSpawner = host.ViewSpawner;
                moveController = host.GetComponent<HostMoveController>();
                strikeVfx = host.GetComponent<HostMeleeStrikeVfx>() ??
                            host.gameObject.AddComponent<HostMeleeStrikeVfx>();
            }
        }

        public void Begin(EntityId attacker, EntityId defender)
        {
            if (attacker.IsNone || defender.IsNone || attacker == defender)
                return;

            // 换目标：结束旧战，改打新敌人
            if (IsFighting && _defender != defender)
                ClearInternal(toast: null);

            _defender = defender;
            moveController?.HoldNpcForInteraction(defender);
            SetFightActivity(defender, true);

            if (!IsAttacker(attacker))
            {
                _attackers.Add(attacker);
                _cooldownByAttacker[attacker.Value] = 0f;
                _nextRepathByAttacker[attacker.Value] = 0f;
            }

            SetFightActivity(attacker, true);
            TryAutoVeilOne(bootstrap?.Session?.World, attacker);
            TryAutoVeilOne(bootstrap?.Session?.World, defender);
        }

        void TryAutoVeilOne(XianXia.Core.Simulation.SimulationWorld world, EntityId id)
        {
            if (world == null || id.IsNone)
                return;
            var result = _veil.TryAutoActivateForNonPlayer(world, id);
            if (!result.IsSuccess)
                return;
            if (!world.Entities.TryGet(id, out var e) || !SpiritVeilService.IsActive(e))
                return;
            Toast(id, "展开斗气纱衣", new Color(0.55f, 0.9f, 1f));
        }

        public void Clear() => ClearInternal(toast: null);

        /// <summary>玩家下令移动／Stop：若该单位是攻方则仅他脱离；无人攻则整场结束。</summary>
        public void DisengageIfAttacker(EntityId id)
        {
            if (!IsFighting || id.IsNone || !IsAttacker(id))
                return;
            RemoveAttacker(id, "脱离战斗");
        }

        /// <summary>选中单位若在交战中（攻或守）则整场停战。</summary>
        public void DisengageIfInvolved(EntityId id)
        {
            if (!IsFighting || id.IsNone)
                return;
            if (!IsInFight(id))
                return;
            ClearInternal("脱离战斗");
        }

        public void DisengageSelected(HostSelectionController selection)
        {
            if (!IsFighting || selection == null)
                return;
            for (var i = 0; i < selection.State.Count; i++)
            {
                var id = selection.State.SelectedIds[i];
                if (IsInFight(id))
                {
                    ClearInternal("脱离战斗");
                    return;
                }
            }
        }

        public bool IsWithinMeleeRange(EntityId a, EntityId b)
        {
            if (!TryGetPresentation(a, out var ax, out var ay) ||
                !TryGetPresentation(b, out var bx, out var by))
                return false;
            var range = meleeRange;
            if (bootstrap?.Session?.World != null &&
                bootstrap.Session.World.Entities.TryGet(a, out var ent))
                range = _veil.ResolveEngageRange(ent);
            return Vector2.Distance(new Vector2(ax, ay), new Vector2(bx, by)) <= range;
        }

        void Update()
        {
            if (!IsFighting || bootstrap?.Session?.World == null)
                return;
            if (bootstrap.Session.IsPaused)
                return;

            var world = bootstrap.Session.World;
            if (!world.Entities.TryGet(_defender, out var defEnt) ||
                !defEnt.TryGet<LifecycleComponent>(out var defLife) ||
                defLife.IsRemoved ||
                (defLife.IsDead && !CombatLifeStateService.HasVisibleCorpse(defEnt)))
            {
                FinishIfNeeded();
                return;
            }

            if (defLife.IsIncapacitated || defLife.IsDead)
            {
                ClearInternal(null);
                return;
            }

            CombatDamageRules.EnsureVitals(defEnt);
            NotifyVeilSpiritEmpty(defEnt);

            if (!TryGetPresentation(_defender, out var dx, out var dy))
            {
                ClearInternal(null);
                return;
            }

            var defRange = _veil.ResolveEngageRange(defEnt);
            var dt = bootstrap.PresentationDeltaTime;
            _defenderCooldown -= dt;

            // 清掉已死／失踪的攻方
            _scratch.Clear();
            for (var i = 0; i < _attackers.Count; i++)
                _scratch.Add(_attackers[i]);
            for (var i = 0; i < _scratch.Count; i++)
            {
                var atkId = _scratch[i];
                if (!world.Entities.TryGet(atkId, out var atkEnt) ||
                    !atkEnt.TryGet<LifecycleComponent>(out var atkLife) ||
                    atkLife.IsRemoved ||
                    (atkLife.IsDead && !CombatLifeStateService.HasVisibleCorpse(atkEnt)))
                {
                    RemoveAttacker(atkId, null);
                    continue;
                }

                if (atkLife.IsIncapacitated)
                {
                    Toast(atkId, "弥留，脱离战斗", new Color(1f, 0.4f, 0.35f));
                    RemoveAttacker(atkId, null);
                    continue;
                }

                CombatDamageRules.EnsureVitals(atkEnt);
                if (atkEnt.TryGet<CombatVitalsComponent>(out var atkHp) && atkHp.CurrentHp <= 0)
                {
                    CombatLifeStateService.TryEnterIncapacitated(world, atkEnt);
                    ApplyDownPresentation(atkId, atkEnt);
                    Toast(atkId, "弥留，脱离战斗", new Color(1f, 0.4f, 0.35f));
                    RemoveAttacker(atkId, null);
                    continue;
                }

                NotifyVeilSpiritEmpty(atkEnt);
                if (!TryGetPresentation(atkId, out var ax, out var ay))
                {
                    RemoveAttacker(atkId, null);
                    continue;
                }

                var atkRange = _veil.ResolveEngageRange(atkEnt);
                var dist = Vector2.Distance(new Vector2(ax, ay), new Vector2(dx, dy));
                if (dist > atkRange)
                {
                    ChaseDefender(atkId, atkRange);
                    continue;
                }

                if (!_cooldownByAttacker.TryGetValue(atkId.Value, out var cd))
                    cd = 0f;
                cd -= dt;
                _cooldownByAttacker[atkId.Value] = cd;
                if (cd > 0f)
                    continue;

                _cooldownByAttacker[atkId.Value] = MeleeCombatService.DefaultMeleeIntervalSeconds;
                var atkRanged = SpiritVeilService.IsActive(atkEnt);
                var hit = _melee.ApplyStrike(world, atkId, _defender, out var dmg, out var defeated);
                if (hit.IsSuccess)
                {
                    PlayStrikeVfx(atkId, _defender, atkRanged);
                    Toast(_defender, "-" + dmg, new Color(1f, 0.55f, 0.35f));
                    NotifyVeilSpiritEmpty(defEnt);
                    if (defeated)
                    {
                        if (world.Entities.TryGet(_defender, out defEnt) &&
                            defEnt.TryGet<LifecycleComponent>(out var postLife))
                        {
                            if (postLife.IsIncapacitated)
                            {
                                ApplyDownPresentation(_defender, defEnt);
                                var name = string.IsNullOrEmpty(defEnt.DisplayName)
                                    ? _defender.ToString()
                                    : defEnt.DisplayName;
                                Toast(atkId, name + " 弥留", new Color(1f, 0.72f, 0.45f));
                            }
                            else if (postLife.IsDead)
                            {
                                ApplyDownPresentation(_defender, defEnt);
                                var name = string.IsNullOrEmpty(defEnt.DisplayName)
                                    ? _defender.ToString()
                                    : defEnt.DisplayName;
                                Toast(atkId, "击杀 " + name, new Color(0.45f, 1f, 0.55f));
                            }
                        }

                        bootstrap.DispatchDrainedEvents();
                        ClearInternal(null);
                        return;
                    }
                }
                else if (Time.unscaledTime >= _nextStrikeFailToast)
                {
                    _nextStrikeFailToast = Time.unscaledTime + 2.5f;
                    Toast(atkId, "攻击未生效", new Color(1f, 0.5f, 0.4f));
                }
            }

            if (!IsFighting)
                return;

            // 守方反击：对一名在反击距离内的攻方出手
            if (_defenderCooldown > 0f)
                return;
            if (!world.Entities.TryGet(_defender, out defEnt) ||
                !defEnt.TryGet<LifecycleComponent>(out defLife) ||
                defLife.IsIncapacitated ||
                defLife.IsDead)
                return;

            for (var n = 0; n < _attackers.Count; n++)
            {
                var idx = (_counterFocus + n) % _attackers.Count;
                var atkId = _attackers[idx];
                if (!TryGetPresentation(atkId, out var ax, out var ay))
                    continue;
                var dist = Vector2.Distance(new Vector2(ax, ay), new Vector2(dx, dy));
                if (dist > defRange)
                    continue;

                _counterFocus = (idx + 1) % Mathf.Max(1, _attackers.Count);
                _defenderCooldown = MeleeCombatService.DefaultMeleeIntervalSeconds;
                var defRanged = SpiritVeilService.IsActive(defEnt);
                var back = _melee.ApplyStrike(world, _defender, atkId, out var backDmg, out var atkDown);
                if (back.IsSuccess)
                {
                    PlayStrikeVfx(_defender, atkId, defRanged);
                    Toast(atkId, "-" + backDmg, new Color(1f, 0.35f, 0.4f));
                    if (world.Entities.TryGet(atkId, out var atkEnt))
                        NotifyVeilSpiritEmpty(atkEnt);
                    if (atkDown)
                    {
                        if (world.Entities.TryGet(atkId, out var downEnt))
                            ApplyDownPresentation(atkId, downEnt);
                        Toast(atkId, "弥留，脱离战斗", new Color(1f, 0.4f, 0.35f));
                        bootstrap.DispatchDrainedEvents();
                        RemoveAttacker(atkId, null);
                    }
                }

                return;
            }
        }

        void OnGUI()
        {
            if (!IsFighting || bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return;

            var atkLabel = _attackers.Count <= 1
                ? ResolveName(AttackerId)
                : ResolveName(AttackerId) + " 等" + _attackers.Count + "人";
            var defName = ResolveName(_defender);
            var veilHint = "";
            if (bootstrap.Session.World.Entities.TryGet(AttackerId, out var atkBanner) &&
                SpiritVeilService.IsActive(atkBanner))
                veilHint = "　·　纱衣远程";
            var line = "交战中　" + atkLabel + " ↔ " + defName + veilHint + "　·　0 血弥留，补刀致死　·　右键地面／S 打断";
            const float w = 560f;
            const float h = 28f;
            var r = new Rect((Screen.width - w) * 0.5f, 56f, w, h);
            HostUiHitTest.Block(r);
            var prev = GUI.color;
            GUI.color = new Color(0.12f, 0.1f, 0.08f, 0.78f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.85f, 0.55f, 1f);
            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };
            GUI.Label(r, line, style);
            GUI.color = prev;
        }

        void RemoveAttacker(EntityId id, string toast)
        {
            if (id.IsNone)
                return;
            if (!string.IsNullOrEmpty(toast))
                Toast(id, toast, new Color(0.85f, 0.85f, 0.75f));

            var world = bootstrap?.Session?.World;
            if (world != null && world.Entities.TryGet(id, out var atk))
            {
                var had = SpiritVeilService.IsActive(atk);
                _veil.DeactivateOnCombatEnd(atk, null);
                if (had)
                    Toast(id, "脱离战斗，纱衣散去", new Color(0.75f, 0.85f, 1f));
            }

            ClearFightActivity(id);
            for (var i = _attackers.Count - 1; i >= 0; i--)
            {
                if (_attackers[i] == id)
                    _attackers.RemoveAt(i);
            }

            _cooldownByAttacker.Remove(id.Value);
            _nextRepathByAttacker.Remove(id.Value);

            if (_attackers.Count == 0)
                ClearInternal(null);
        }

        void ClearInternal(string toast)
        {
            if (!string.IsNullOrEmpty(toast) && _attackers.Count > 0)
                Toast(AttackerId, toast, new Color(0.85f, 0.85f, 0.75f));

            var world = bootstrap?.Session?.World;
            if (world != null)
            {
                world.Entities.TryGet(_defender, out var def);
                var defHad = SpiritVeilService.IsActive(def);
                for (var i = 0; i < _attackers.Count; i++)
                {
                    world.Entities.TryGet(_attackers[i], out var atk);
                    var atkHad = SpiritVeilService.IsActive(atk);
                    _veil.DeactivateOnCombatEnd(atk, null);
                    if (atkHad)
                        Toast(_attackers[i], "战斗结束，纱衣散去", new Color(0.75f, 0.85f, 1f));
                    ClearFightActivity(_attackers[i]);
                }

                _veil.DeactivateOnCombatEnd(null, def);
                if (defHad)
                    Toast(_defender, "战斗结束，纱衣散去", new Color(0.75f, 0.85f, 1f));
            }
            else
            {
                for (var i = 0; i < _attackers.Count; i++)
                    ClearFightActivity(_attackers[i]);
            }

            ClearFightActivity(_defender);
            ReleaseFightHold(_defender);
            _attackers.Clear();
            _cooldownByAttacker.Clear();
            _nextRepathByAttacker.Clear();
            _defender = EntityId.None;
            _defenderCooldown = 0f;
            _counterFocus = 0;
        }

        void PlayStrikeVfx(EntityId from, EntityId to, bool ranged)
        {
            if (strikeVfx == null && bootstrap != null)
                strikeVfx = bootstrap.GetComponent<HostMeleeStrikeVfx>();
            if (ranged)
                strikeVfx?.PlayRangedBetween(viewSpawner, from, to);
            else
                strikeVfx?.PlayBetween(viewSpawner, from, to);
        }

        void ChaseDefender(EntityId attacker, float engageRange)
        {
            if (moveController == null || viewSpawner == null || attacker.IsNone)
                return;
            moveController.HoldNpcForInteraction(_defender);
            if (_nextRepathByAttacker.TryGetValue(attacker.Value, out var next) &&
                Time.unscaledTime < next)
                return;
            if (!viewSpawner.Registry.TryGet(_defender, out var defView) || defView == null)
                return;

            _nextRepathByAttacker[attacker.Value] = Time.unscaledTime + Mathf.Max(0.1f, repathInterval);
            var dest = defView.transform.position;
            if (viewSpawner.Registry.TryGet(attacker, out var atkView) && atkView != null)
            {
                var delta = atkView.transform.position - dest;
                delta.z = 0f;
                if (delta.sqrMagnitude > 0.01f)
                    dest += delta.normalized * (engageRange * 0.35f);
            }

            dest.z = HostPresentationSpace.EntityZ;
            moveController.OrderEntityToWorldPoint(
                attacker, dest, arriveCommand: null, issueStop: false);
            SetFightActivity(attacker, true);
        }

        void NotifyVeilSpiritEmpty(Entity entity)
        {
            if (entity == null || !_veil.DeactivateIfSpiritEmpty(entity))
                return;
            Toast(entity.Id, "灵力耗尽，纱衣散去", new Color(0.75f, 0.85f, 1f));
        }

        void FinishIfNeeded()
        {
            bootstrap?.DispatchDrainedEvents();
            ClearInternal(null);
        }

        void ApplyDownPresentation(EntityId id, Entity entity)
        {
            if (viewSpawner == null || id.IsNone || entity == null)
                return;
            moveController?.CancelPresentationMovementPublic(id);
            if (!viewSpawner.Registry.TryGet(id, out var view) || view == null)
                return;
            if (entity.TryGet<LifecycleComponent>(out var life) && life.IsDead)
            {
                view.SetActivityText("尸体");
                view.SetBaseColor(new Color(0.35f, 0.32f, 0.30f, 0.85f));
            }
            else if (entity.TryGet<LifecycleComponent>(out var life2) && life2.IsIncapacitated)
            {
                view.SetActivityText("弥留");
                view.SetBaseColor(new Color(0.72f, 0.45f, 0.42f, 0.92f));
            }
        }

        void SetFightActivity(EntityId id, bool fighting)
        {
            if (id.IsNone || viewSpawner == null)
                return;
            if (!viewSpawner.Registry.TryGet(id, out var view) || view == null)
                return;
            if (fighting)
                view.SetActivityText("交战中");
            else if (view.ActivityText == "交战中" || view.ActivityText == "稍候")
                view.SetActivityText(string.Empty);
        }

        void ClearFightActivity(EntityId id) => SetFightActivity(id, false);

        void ReleaseFightHold(EntityId npc)
        {
            if (npc.IsNone || moveController == null)
                return;
            moveController.ReleaseNpcForInteraction(npc);
        }

        string ResolveName(EntityId id)
        {
            var world = bootstrap?.Session?.World;
            if (world != null && world.Entities.TryGet(id, out var e) &&
                !string.IsNullOrEmpty(e.DisplayName))
                return e.DisplayName;
            return id.IsNone ? "?" : id.ToString();
        }

        bool TryGetPresentation(EntityId id, out float x, out float y)
        {
            x = y = 0f;
            if (viewSpawner != null && viewSpawner.Registry.TryGet(id, out var view) && view != null)
            {
                var p = HostPresentationSpace.ToPresentation(view.transform.position);
                x = p.x;
                y = p.y;
                return true;
            }

            return false;
        }

        void Toast(EntityId id, string text, Color color)
        {
            var overlay = bootstrap != null ? bootstrap.GetComponent<HostFeedbackOverlay>() : null;
            if (overlay == null || viewSpawner == null)
                return;
            overlay.SpawnAtEntity(viewSpawner, id, text, color);
        }
    }
}
