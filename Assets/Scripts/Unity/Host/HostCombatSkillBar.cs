using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// 选中己方：快捷键 1–6 释放装备栏主动斗技；冷却由 FormalHud 侧栏展示与点击共用。
    /// </summary>
    public sealed class HostCombatSkillBar : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] EntityViewSpawner viewSpawner;
        [SerializeField] HostNpcMeleeAssault meleeAssault;
        [SerializeField] HostMeleeStrikeVfx strikeVfx;
        [SerializeField] float castRange = 3.5f;

        readonly MeleeCombatService _melee = new MeleeCombatService();
        readonly float[] _slotCooldown = new float[CombatArtsComponent.MaxEquippedSlots];
        readonly Dictionary<ulong, float[]> _cooldownsByCaster = new Dictionary<ulong, float[]>();

        public void Bind(PlayableHostBootstrap host)
        {
            bootstrap = host;
            if (host == null)
                return;
            selectionController = host.GetComponent<HostSelectionController>();
            viewSpawner = host.ViewSpawner;
            meleeAssault = host.GetComponent<HostNpcMeleeAssault>();
            strikeVfx = host.GetComponent<HostMeleeStrikeVfx>() ??
                        host.gameObject.AddComponent<HostMeleeStrikeVfx>();
        }

        public void ClearSessionState()
        {
            _cooldownsByCaster.Clear();
            for (var i = 0; i < _slotCooldown.Length; i++)
                _slotCooldown[i] = 0f;
        }

        /// <summary>快捷键／面板点击共用：释放装备栏主动斗技。</summary>
        public void TryCastEquippedSlot(int slotIndex) => TryCastSlot(slotIndex);

        public float GetSlotCooldown(EntityId caster, int slotIndex)
        {
            if (caster.IsNone || slotIndex < 0 || slotIndex >= CombatArtsComponent.MaxEquippedSlots)
                return 0f;
            return GetCooldownArray(caster)[slotIndex];
        }

        void Update()
        {
            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return;
            if (bootstrap.Session.IsPaused)
                return;

            var dt = bootstrap.PresentationDeltaTime;
            TickCooldowns(dt);

            if (HostInputGate.BlockWorldInteraction)
                return;

            for (var i = 0; i < CombatArtsComponent.MaxEquippedSlots; i++)
            {
                if (!Input.GetKeyDown(KeyCode.Alpha1 + i) && !Input.GetKeyDown(KeyCode.Keypad1 + i))
                    continue;
                TryCastSlot(i);
            }
        }

        void TryCastSlot(int slotIndex)
        {
            if (selectionController == null || selectionController.State.Count == 0)
                return;
            var caster = selectionController.State.SelectedIds[0];
            if (caster.IsNone)
                return;
            var world = bootstrap.Session.World;
            if (!world.Entities.TryGet(caster, out var entity) ||
                (entity.Tags & EntityTag.Npc) != 0 ||
                !entity.TryGet<CombatArtsComponent>(out var arts))
                return;
            var artId = arts.GetEquipped(slotIndex);
            if (!artId.HasValue)
                return;
            if (!world.TryGetCombatArt(artId.Value, out var art) || art == null || !art.IsActiveSkill)
            {
                Toast(caster, "该格不是主动技", new Color(1f, 0.85f, 0.4f));
                return;
            }

            var cds = GetCooldownArray(caster);
            if (cds[slotIndex] > 0.05f)
            {
                Toast(caster, "冷却中", new Color(0.8f, 0.8f, 0.8f));
                return;
            }

            if (!TryResolveTarget(caster, out var target))
            {
                Toast(caster, "附近无目标", new Color(1f, 0.55f, 0.4f));
                return;
            }

            var result = _melee.CastEquippedArt(
                world, caster, target, slotIndex,
                out var total, out var hits, out var defeated);
            if (result.IsFailure)
            {
                Toast(caster, "释放失败", new Color(1f, 0.4f, 0.35f));
                return;
            }

            cds[slotIndex] = art.CooldownSeconds > 0f ? art.CooldownSeconds : 2f;
            if (strikeVfx == null && bootstrap != null)
                strikeVfx = bootstrap.GetComponent<HostMeleeStrikeVfx>();
            strikeVfx?.PlayBetween(viewSpawner, caster, target);
            Toast(target, art.Name + " -" + total + (hits > 1 ? ("×" + hits) : ""), new Color(1f, 0.75f, 0.35f));
            if (defeated)
            {
                Toast(caster, "击败", new Color(0.45f, 1f, 0.55f));
                bootstrap.DispatchDrainedEvents();
                bootstrap.ReloadLocalMapPresentation(frameCamera: false);
                meleeAssault?.Clear();
            }
            else
            {
                bootstrap.DispatchDrainedEvents();
                meleeAssault?.Begin(caster, target);
            }
        }

        bool TryResolveTarget(EntityId caster, out EntityId target)
        {
            target = EntityId.None;
            if (meleeAssault != null && meleeAssault.IsFighting &&
                meleeAssault.AttackerId == caster && !meleeAssault.DefenderId.IsNone)
            {
                target = meleeAssault.DefenderId;
                return true;
            }

            if (!TryGetPresentation(caster, out var cx, out var cy))
                return false;

            var world = bootstrap.Session.World;
            var best = EntityId.None;
            var bestD = castRange * castRange;
            var bestHostile = false;
            foreach (var e in world.Entities.All)
            {
                if (e == null || (e.Tags & EntityTag.Npc) == 0)
                    continue;
                if (!e.TryGet<LifecycleComponent>(out var life) || life.IsDead || life.IsRemoved)
                    continue;
                if (!TryGetPresentation(e.Id, out var x, out var y))
                    continue;
                var dx = x - cx;
                var dy = y - cy;
                var d2 = dx * dx + dy * dy;
                if (d2 > castRange * castRange)
                    continue;
                var hostile = HostNpcInteraction.IsHostileNpc(bootstrap.Session, e.Id);
                if (best.IsNone ||
                    (hostile && !bestHostile) ||
                    (hostile == bestHostile && d2 < bestD))
                {
                    bestD = d2;
                    best = e.Id;
                    bestHostile = hostile;
                }
            }

            if (best.IsNone)
                return false;
            target = best;
            return true;
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

        float[] GetCooldownArray(EntityId caster)
        {
            if (!_cooldownsByCaster.TryGetValue(caster.Value, out var arr) || arr == null ||
                arr.Length != CombatArtsComponent.MaxEquippedSlots)
            {
                arr = new float[CombatArtsComponent.MaxEquippedSlots];
                _cooldownsByCaster[caster.Value] = arr;
            }

            return arr;
        }

        void TickCooldowns(float dt)
        {
            foreach (var kv in _cooldownsByCaster)
            {
                var arr = kv.Value;
                if (arr == null)
                    continue;
                for (var i = 0; i < arr.Length; i++)
                {
                    if (arr[i] > 0f)
                        arr[i] = Mathf.Max(0f, arr[i] - dt);
                }
            }
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
