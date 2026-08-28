using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Attributes;
using XianXia.Core.Combat;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// 砍树／拆墙：靠近后按攻击力拆血量；树归零后粗木进队伍背包。
    /// 支持多名攻击者（PlayerParty Follower 可各自找附近树）。
    /// </summary>
    public sealed class HostDestructibleAssault : MonoBehaviour
    {
        sealed class Session
        {
            public EntityId Attacker;
            public HostMapDestructible Target;
            public float Cooldown;
            public float NextRepath;
            public bool FromPartyFollow;
        }

        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] EntityViewSpawner viewSpawner;
        [SerializeField] HostMoveController moveController;
        [SerializeField] HostMeleeStrikeVfx strikeVfx;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] float engageRange = 1.85f;
        [SerializeField] float repathInterval = 0.25f;

        readonly List<Session> _sessions = new List<Session>(4);

        public bool IsAssaulting => _sessions.Count > 0;
        public HostMapDestructible Target => _sessions.Count > 0 ? _sessions[0].Target : null;

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

        public bool IsAttacker(EntityId id)
        {
            if (id.IsNone)
                return false;
            for (var i = 0; i < _sessions.Count; i++)
            {
                if (_sessions[i].Attacker == id)
                    return true;
            }

            return false;
        }

        public bool IsPartyDerivedAttacker(EntityId id)
        {
            if (id.IsNone)
                return false;
            for (var i = 0; i < _sessions.Count; i++)
            {
                if (_sessions[i].Attacker == id && _sessions[i].FromPartyFollow)
                    return true;
            }

            return false;
        }

        public HostMapDestructible GetTargetForAttacker(EntityId id)
        {
            for (var i = 0; i < _sessions.Count; i++)
            {
                if (_sessions[i].Attacker == id)
                    return _sessions[i].Target;
            }

            return null;
        }

        public void Begin(EntityId attacker, HostMapDestructible target, bool fromPartyFollow = false)
        {
            if (attacker.IsNone || target == null || target.IsDestroyed)
                return;

            for (var i = 0; i < _sessions.Count; i++)
            {
                if (_sessions[i].Attacker != attacker)
                    continue;
                _sessions[i].Target = target;
                _sessions[i].Cooldown = 0f;
                _sessions[i].NextRepath = 0f;
                _sessions[i].FromPartyFollow = fromPartyFollow || _sessions[i].FromPartyFollow;
                if (!fromPartyFollow)
                    bootstrap?.GetComponent<HostHousingAreaSelection>()?.SelectDestructible(target);
                return;
            }

            _sessions.Add(new Session
            {
                Attacker = attacker,
                Target = target,
                Cooldown = 0f,
                NextRepath = 0f,
                FromPartyFollow = fromPartyFollow
            });

            if (!fromPartyFollow)
                bootstrap?.GetComponent<HostHousingAreaSelection>()?.SelectDestructible(target);
        }

        public void Clear() => ClearAllSessions(clearInspect: true);

        public void DisengageIfAttacker(EntityId id)
        {
            if (id.IsNone)
                return;
            RemoveSession(id, clearInspect: true);
        }

        public void StopPartyDerived(EntityId id)
        {
            if (id.IsNone)
                return;
            for (var i = _sessions.Count - 1; i >= 0; i--)
            {
                if (_sessions[i].Attacker != id || !_sessions[i].FromPartyFollow)
                    continue;
                _sessions.RemoveAt(i);
            }
        }

        void Update()
        {
            if (_sessions.Count == 0 || bootstrap?.Session?.World == null)
                return;
            if (bootstrap.Session.IsPaused)
                return;

            for (var i = _sessions.Count - 1; i >= 0; i--)
            {
                if (!TickSession(_sessions[i]))
                    _sessions.RemoveAt(i);
            }
        }

        bool TickSession(Session session)
        {
            if (session == null)
                return false;

            var target = session.Target;
            if (target == null || target.IsDestroyed)
                return false;

            var world = bootstrap.Session.World;
            var attacker = session.Attacker;
            if (!world.Entities.TryGet(attacker, out var atk) ||
                !atk.TryGet<LifecycleComponent>(out var life) ||
                life.IsDead || life.IsRemoved)
                return false;

            if (!TryGetPresentation(attacker, out var ax, out var ay))
                return false;

            var tp = HostPresentationSpace.ToPresentation(target.transform.position);
            var dist = Vector2.Distance(new Vector2(ax, ay), new Vector2(tp.x, tp.y));
            if (dist > engageRange)
            {
                Chase(session);
                return true;
            }

            var dt = bootstrap.PresentationDeltaTime;
            session.Cooldown -= dt;
            if (session.Cooldown > 0f)
                return true;
            session.Cooldown = MeleeCombatService.DefaultMeleeIntervalSeconds;

            var damage = 1;
            if (atk.TryGet<AttributesComponent>(out var attrs))
                damage = Mathf.Max(1, attrs.GetFinal(AttributeId.Attack));

            var isTree = target.IsTree;
            var kind = target.Kind;
            var expectedYield = target.ResolveWoodYield();
            var hitPos = target.transform.position;
            var dealt = target.ApplyDamage(damage, out var destroyed);
            if (dealt > 0)
            {
                strikeVfx?.Play(hitPos + Vector3.up * 0.4f, hitPos);
                Toast(attacker, "-" + dealt, new Color(0.75f, 0.9f, 0.45f));
            }

            if (!destroyed)
                return true;

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

            Toast(attacker, msg, new Color(0.55f, 1f, 0.55f));
            if (!session.FromPartyFollow)
                bootstrap.GetComponent<HostHousingAreaSelection>()?.Clear();
            bootstrap.DispatchDrainedEvents();
            // 已从 _sessions 批量移除；勿 return false，否则 Update 会再次 RemoveAt(i) 越界。
            RemoveSessionsOnTarget(target, clearInspect: !session.FromPartyFollow);
            return true;
        }

        void RemoveSessionsOnTarget(HostMapDestructible target, bool clearInspect)
        {
            for (var i = _sessions.Count - 1; i >= 0; i--)
            {
                if (_sessions[i].Target != target)
                    continue;
                _sessions.RemoveAt(i);
            }

            if (clearInspect)
                bootstrap?.GetComponent<HostHousingAreaSelection>()?.Clear();
        }

        void RemoveSession(EntityId attacker, bool clearInspect)
        {
            for (var i = _sessions.Count - 1; i >= 0; i--)
            {
                if (_sessions[i].Attacker != attacker)
                    continue;
                _sessions.RemoveAt(i);
            }

            if (clearInspect && _sessions.Count == 0)
                bootstrap?.GetComponent<HostHousingAreaSelection>()?.Clear();
        }

        void ClearAllSessions(bool clearInspect)
        {
            _sessions.Clear();
            if (clearInspect)
                bootstrap?.GetComponent<HostHousingAreaSelection>()?.Clear();
        }

        static int GrantRoughWood(XianXia.Core.Simulation.SimulationWorld world, int yield)
        {
            if (world?.Inventory == null || yield <= 0)
                return 0;

            var id = HostMapDestructible.RoughWoodItemId;
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

        void Chase(Session session)
        {
            if (moveController == null || session?.Target == null)
                return;
            if (Time.unscaledTime < session.NextRepath)
                return;
            session.NextRepath = Time.unscaledTime + repathInterval;
            var dest = session.Target.transform.position;
            if (viewSpawner != null &&
                viewSpawner.Registry.TryGet(session.Attacker, out var atkView) &&
                atkView != null)
            {
                var delta = atkView.transform.position - dest;
                delta.z = 0f;
                if (delta.sqrMagnitude > 0.01f)
                    dest += delta.normalized * (engageRange * 0.4f);
            }

            dest.z = HostPresentationSpace.EntityZ;
            moveController.OrderEntityToWorldPoint(
                session.Attacker, dest, arriveCommand: null, issueStop: false);
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
