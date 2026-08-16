using UnityEngine;
using XianXia.Core.Attributes;
using XianXia.Core.Combat;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// 砍树／拆墙：选中己方靠近后按攻击力拆血量；树归零后粗木进队伍背包。
    /// </summary>
    public sealed class HostDestructibleAssault : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] EntityViewSpawner viewSpawner;
        [SerializeField] HostMoveController moveController;
        [SerializeField] HostMeleeStrikeVfx strikeVfx;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] float engageRange = 1.85f;
        [SerializeField] float repathInterval = 0.25f;

        HostMapDestructible _target;
        EntityId _attacker = EntityId.None;
        float _cooldown;
        float _nextRepath;

        public bool IsAssaulting => _target != null && !_attacker.IsNone;
        public HostMapDestructible Target => _target;

        public void Bind(PlayableHostBootstrap host)
        {
            bootstrap = host;
            if (host == null)
                return;
            viewSpawner = host.ViewSpawner;
            moveController = host.GetComponent<HostMoveController>();
            strikeVfx = host.GetComponent<HostMeleeStrikeVfx>() ??
                        host.gameObject.AddComponent<HostMeleeStrikeVfx>();
            selectionController = host.GetComponent<HostSelectionController>();
        }

        public void Begin(EntityId attacker, HostMapDestructible target)
        {
            if (attacker.IsNone || target == null || target.IsDestroyed)
                return;
            _attacker = attacker;
            _target = target;
            _cooldown = 0f;
            _nextRepath = 0f;
            bootstrap?.GetComponent<HostHousingAreaSelection>()?.SelectDestructible(target);
        }

        public void Clear()
        {
            _attacker = EntityId.None;
            _target = null;
            _cooldown = 0f;
        }

        public void DisengageIfAttacker(EntityId id)
        {
            if (IsAssaulting && id == _attacker)
                Clear();
        }

        void Update()
        {
            if (!IsAssaulting || bootstrap?.Session?.World == null)
                return;
            if (bootstrap.Session.IsPaused)
                return;
            if (_target == null || _target.IsDestroyed)
            {
                Clear();
                return;
            }

            var world = bootstrap.Session.World;
            if (!world.Entities.TryGet(_attacker, out var atk) ||
                !atk.TryGet<LifecycleComponent>(out var life) ||
                life.IsDead || life.IsRemoved)
            {
                Clear();
                return;
            }

            if (!TryGetPresentation(_attacker, out var ax, out var ay))
            {
                Clear();
                return;
            }

            var tp = HostPresentationSpace.ToPresentation(_target.transform.position);
            var dist = Vector2.Distance(new Vector2(ax, ay), new Vector2(tp.x, tp.y));
            if (dist > engageRange)
            {
                Chase();
                return;
            }

            var dt = bootstrap.PresentationDeltaTime;
            _cooldown -= dt;
            if (_cooldown > 0f)
                return;
            _cooldown = MeleeCombatService.DefaultMeleeIntervalSeconds;

            var damage = 1;
            if (atk.TryGet<AttributesComponent>(out var attrs))
                damage = Mathf.Max(1, attrs.GetFinal(AttributeId.Attack));

            // 先记下掉落信息：ApplyDamage 会 Destroy 目标
            var isTree = _target.IsTree;
            var kind = _target.Kind;
            var expectedYield = _target.ResolveWoodYield();
            var hitPos = _target.transform.position;
            var dealt = _target.ApplyDamage(damage, out var destroyed);
            if (dealt > 0)
            {
                strikeVfx?.Play(hitPos + Vector3.up * 0.4f, hitPos);
                Toast(_attacker, "-" + dealt, new Color(0.75f, 0.9f, 0.45f));
            }

            if (!destroyed)
                return;

            var wood = 0;
            if (isTree && expectedYield > 0)
                wood = GrantRoughWood(world, expectedYield);

            string msg;
            if (isTree)
            {
                if (wood <= 0 && expectedYield > 0)
                    msg = "伐倒 · 背包已满，粗木未入包";
                else if (wood < expectedYield && expectedYield > 0)
                    msg = "伐倒 · 获粗木 ×" + wood + "（背包将满）";
                else if (wood > 0)
                    msg = "伐倒 · 获粗木 ×" + wood;
                else
                    msg = "伐倒 · 无木材产量（kind=" + kind + "）";
                Debug.Log(
                    "[Host] 砍树结算 kind=" + kind +
                    " yield=" + expectedYield +
                    " added=" + wood +
                    " bagWood=" + world.Inventory.GetCount(HostMapDestructible.RoughWoodItemId));
            }
            else
                msg = "摧毁";

            Toast(_attacker, msg, new Color(0.55f, 1f, 0.55f));
            bootstrap.GetComponent<HostHousingAreaSelection>()?.Clear();
            bootstrap.DispatchDrainedEvents();
            Clear();
        }

        static int GrantRoughWood(XianXia.Core.Simulation.SimulationWorld world, int yield)
        {
            if (world?.Inventory == null || yield <= 0)
                return 0;

            var id = HostMapDestructible.RoughWoodItemId;
            // 确保目录有粗木条目（堆叠／显示名）；缺失时仍可 TryAdd（默认堆 99）
            if (!world.InventoryCatalog.TryGet(id, out _))
                world.InventoryCatalog.Register(id, "粗木", 99, new[] { "resource", "wood" });

            var added = world.Inventory.TryAdd(id, yield);
            if (added > 0)
            {
                world.Events.Publish(
                    XianXia.Core.Events.EventType.SettlementStockChanged,
                    world.Tick,
                    payload: "bag:" + id + ":" + added + ":chopTree");
                QuestProgressRefresh.AfterWorldChange(world, EntityId.None);
            }
            else
            {
                Debug.LogWarning(
                    "[Host] 砍树粗木未入包：TryAdd 返回 0（背包可能已满）。yield=" + yield +
                    " usedSlots=" + world.Inventory.UsedSlotCount +
                    "/" + world.Inventory.SlotCapacity);
            }

            return added;
        }

        void Chase()
        {
            if (moveController == null || _target == null)
                return;
            if (Time.unscaledTime < _nextRepath)
                return;
            _nextRepath = Time.unscaledTime + repathInterval;
            var dest = _target.transform.position;
            if (viewSpawner != null &&
                viewSpawner.Registry.TryGet(_attacker, out var atkView) &&
                atkView != null)
            {
                var delta = atkView.transform.position - dest;
                delta.z = 0f;
                if (delta.sqrMagnitude > 0.01f)
                    dest += delta.normalized * (engageRange * 0.4f);
            }

            dest.z = HostPresentationSpace.EntityZ;
            moveController.OrderEntityToWorldPoint(
                _attacker, dest, arriveCommand: null, issueStop: false);
        }

        bool TryGetPresentation(EntityId id, out float x, out float y)
        {
            x = y = 0f;
            if (viewSpawner != null &&
                viewSpawner.Registry.TryGet(id, out var view) &&
                view != null)
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
