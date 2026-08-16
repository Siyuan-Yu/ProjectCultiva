using UnityEngine;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// 战斗 Alpha：下令攻击后持续追击互砍，直到目标／己方倒下，或玩家下令移动／Stop 打断。
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

        EntityId _attacker = EntityId.None;
        EntityId _defender = EntityId.None;
        float _cooldown;
        float _nextRepathTime;
        float _nextStrikeFailToast;

        public bool IsFighting => !_attacker.IsNone && !_defender.IsNone;
        public EntityId AttackerId => _attacker;
        public EntityId DefenderId => _defender;
        public float MeleeRange => meleeRange;

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

            // 换目标：松开旧敌人
            if (IsFighting && _defender != defender)
                ReleaseFightHold(_defender);
            if (IsFighting && _attacker != attacker)
                ClearFightActivity(_attacker);

            _attacker = attacker;
            _defender = defender;
            _cooldown = 0f;
            _nextRepathTime = 0f;
            moveController?.HoldNpcForInteraction(defender);
            SetFightActivity(attacker, true);
            SetFightActivity(defender, true);
        }

        public void Clear()
        {
            ClearInternal(toast: null);
        }

        /// <summary>玩家下令移动／Stop：若该单位是攻方则脱离。</summary>
        public void DisengageIfAttacker(EntityId id)
        {
            if (!IsFighting || id.IsNone || id != _attacker)
                return;
            ClearInternal("脱离战斗");
        }

        /// <summary>选中单位若在交战中（攻或守）则整场停战。</summary>
        public void DisengageIfInvolved(EntityId id)
        {
            if (!IsFighting || id.IsNone)
                return;
            if (id != _attacker && id != _defender)
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
                if (id == _attacker || id == _defender)
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
            return Vector2.Distance(new Vector2(ax, ay), new Vector2(bx, by)) <= meleeRange;
        }

        void Update()
        {
            if (!IsFighting || bootstrap?.Session?.World == null)
                return;
            if (bootstrap.Session.IsPaused)
                return;

            var world = bootstrap.Session.World;
            if (!world.Entities.TryGet(_attacker, out var atkEnt) ||
                !world.Entities.TryGet(_defender, out var defEnt))
            {
                ClearInternal(null);
                return;
            }

            if (!atkEnt.TryGet<LifecycleComponent>(out var atkLife) || atkLife.IsDead || atkLife.IsRemoved ||
                !defEnt.TryGet<LifecycleComponent>(out var defLife) || defLife.IsDead || defLife.IsRemoved)
            {
                FinishIfNeeded();
                return;
            }

            CombatDamageRules.EnsureVitals(atkEnt);
            CombatDamageRules.EnsureVitals(defEnt);
            if (atkEnt.TryGet<CombatVitalsComponent>(out var atkHp) && atkHp.CurrentHp <= 0)
            {
                Toast(_attacker, "重伤，战斗中止", new Color(1f, 0.4f, 0.35f));
                ClearInternal(null);
                return;
            }

            if (!TryGetPresentation(_attacker, out var ax, out var ay) ||
                !TryGetPresentation(_defender, out var dx, out var dy))
            {
                ClearInternal(null);
                return;
            }

            var dist = Vector2.Distance(new Vector2(ax, ay), new Vector2(dx, dy));
            if (dist > meleeRange)
            {
                ChaseDefender();
                return;
            }

            var dt = bootstrap.PresentationDeltaTime;
            _cooldown -= dt;
            if (_cooldown > 0f)
                return;
            _cooldown = MeleeCombatService.DefaultMeleeIntervalSeconds;

            var hit = _melee.ApplyStrike(world, _attacker, _defender, out var dmg, out var defeated);
            if (hit.IsSuccess)
            {
                PlayStrikeVfx(_attacker, _defender);
                Toast(_defender, "-" + dmg, new Color(1f, 0.55f, 0.35f));
                if (defeated)
                {
                    var name = string.IsNullOrEmpty(defEnt.DisplayName)
                        ? _defender.ToString()
                        : defEnt.DisplayName;
                    Toast(_attacker, "击败 " + name, new Color(0.45f, 1f, 0.55f));
                    bootstrap.DispatchDrainedEvents();
                    // 勿 Rebuild 整图：会把交战中的表现坐标重置成地点中心 → 瞬移
                    if (viewSpawner != null)
                        viewSpawner.Despawn(_defender);
                    ClearInternal(null);
                    return;
                }
            }
            else if (Time.unscaledTime >= _nextStrikeFailToast)
            {
                _nextStrikeFailToast = Time.unscaledTime + 2.5f;
                Toast(_attacker, "攻击未生效", new Color(1f, 0.5f, 0.4f));
            }

            if (!world.Entities.TryGet(_defender, out defEnt) ||
                !defEnt.TryGet<LifecycleComponent>(out defLife) ||
                defLife.IsDead || defLife.IsRemoved)
            {
                ClearInternal(null);
                return;
            }

            var back = _melee.ApplyStrike(world, _defender, _attacker, out var backDmg, out var atkDown);
            if (back.IsSuccess)
            {
                PlayStrikeVfx(_defender, _attacker);
                Toast(_attacker, "-" + backDmg, new Color(1f, 0.35f, 0.4f));
                if (atkDown)
                {
                    Toast(_attacker, "重伤，战斗中止", new Color(1f, 0.4f, 0.35f));
                    bootstrap.DispatchDrainedEvents();
                    ClearInternal(null);
                }
            }
        }

        void OnGUI()
        {
            if (!IsFighting || bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return;

            var atkName = ResolveName(_attacker);
            var defName = ResolveName(_defender);
            var line = "交战中　" + atkName + " ↔ " + defName + "　·　追击至死　·　右键地面／S 打断";
            const float w = 520f;
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

        void ClearInternal(string toast)
        {
            if (!string.IsNullOrEmpty(toast) && !_attacker.IsNone)
                Toast(_attacker, toast, new Color(0.85f, 0.85f, 0.75f));
            ClearFightActivity(_attacker);
            ClearFightActivity(_defender);
            ReleaseFightHold(_defender);
            _attacker = EntityId.None;
            _defender = EntityId.None;
            _cooldown = 0f;
            _nextRepathTime = 0f;
        }

        void PlayStrikeVfx(EntityId from, EntityId to)
        {
            if (strikeVfx == null && bootstrap != null)
                strikeVfx = bootstrap.GetComponent<HostMeleeStrikeVfx>();
            strikeVfx?.PlayBetween(viewSpawner, from, to);
        }

        void ChaseDefender()
        {
            if (moveController == null || viewSpawner == null)
                return;
            // 交战中防守方应定住；若被日程／其它逻辑拉开则继续追
            moveController.HoldNpcForInteraction(_defender);
            if (Time.unscaledTime < _nextRepathTime)
                return;
            if (!viewSpawner.Registry.TryGet(_defender, out var defView) || defView == null)
                return;

            _nextRepathTime = Time.unscaledTime + Mathf.Max(0.1f, repathInterval);
            var dest = defView.transform.position;
            if (viewSpawner.Registry.TryGet(_attacker, out var atkView) && atkView != null)
            {
                var delta = atkView.transform.position - dest;
                delta.z = 0f;
                if (delta.sqrMagnitude > 0.01f)
                    dest += delta.normalized * (meleeRange * 0.35f);
            }

            dest.z = HostPresentationSpace.EntityZ;
            // issueStop:false — 避免 Stop→脱离战斗；到位后由 MoveController 识别交战攻方跳过待命
            moveController.OrderEntityToWorldPoint(
                _attacker, dest, arriveCommand: null, issueStop: false);
            SetFightActivity(_attacker, true);
        }

        void FinishIfNeeded()
        {
            var world = bootstrap?.Session?.World;
            if (world != null &&
                world.Entities.TryGet(_defender, out var def) &&
                def.TryGet<LifecycleComponent>(out var life) &&
                life.IsDead)
            {
                bootstrap.DispatchDrainedEvents();
                if (viewSpawner != null)
                    viewSpawner.Despawn(_defender);
            }

            ClearInternal(null);
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
