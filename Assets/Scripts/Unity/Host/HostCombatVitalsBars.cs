using UnityEngine;
using XianXia.Core.Attributes;
using XianXia.Core.Combat;
using XianXia.Core.Cultivation;
using XianXia.Core.Entities;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// 头顶生命／灵力护盾条：己方、敌对、交战单位常显；其余单位淡显。
    /// </summary>
    public sealed class HostCombatVitalsBars : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] EntityViewSpawner viewSpawner;
        [SerializeField] HostNpcMeleeAssault meleeAssault;
        [SerializeField] HostSelectionController selection;
        [SerializeField] Camera worldCamera;
        [SerializeField] float barWidth = 42f;
        [SerializeField] float barHeight = 5f;
        [SerializeField] float yOffset = 0.85f;

        static readonly Color HpFill = new Color(0.82f, 0.28f, 0.22f, 0.95f);
        static readonly Color HpBack = new Color(0.08f, 0.06f, 0.05f, 0.7f);
        static readonly Color SpFill = new Color(0.35f, 0.72f, 0.95f, 0.9f);
        static readonly Color SpBack = new Color(0.06f, 0.1f, 0.14f, 0.65f);
        static readonly Color Border = new Color(0.95f, 0.88f, 0.7f, 0.55f);

        public void Bind(PlayableHostBootstrap host)
        {
            bootstrap = host;
            if (host == null)
                return;
            viewSpawner = host.ViewSpawner;
            meleeAssault = host.GetComponent<HostNpcMeleeAssault>();
            selection = host.GetComponent<HostSelectionController>();
            if (worldCamera == null)
                worldCamera = Camera.main;
        }

        void OnGUI()
        {
            if (bootstrap?.Session?.World == null || viewSpawner == null)
                return;
            if (worldCamera == null)
                worldCamera = Camera.main;
            if (worldCamera == null)
                return;

            var world = bootstrap.Session.World;
            foreach (var view in viewSpawner.Registry.All)
            {
                if (view == null || !view.IsBound)
                    continue;
                var id = view.EntityId;
                if (id.IsNone || !world.Entities.TryGet(id, out var entity))
                    continue;
                if (!entity.TryGet<LifecycleComponent>(out var life) || life.IsDead || life.IsRemoved)
                    continue;

                CombatDamageRules.EnsureVitals(entity);
                if (!entity.TryGet<CombatVitalsComponent>(out var vitals) ||
                    !entity.TryGet<AttributesComponent>(out var attrs))
                    continue;

                var maxHp = Mathf.Max(1, attrs.GetFinal(AttributeId.MaxHp));
                var curHp = Mathf.Clamp(vitals.CurrentHp, 0, maxHp);
                var emphasize = ShouldEmphasize(entity, id);
                // 满血且非强调：不画，减杂讯
                if (!emphasize && curHp >= maxHp)
                {
                    var qi = entity.TryGet<CultivationComponent>(out var c) &&
                             c.Realm >= RealmStage.QiRefining;
                    var maxSpQuiet = attrs.GetFinal(AttributeId.SpiritPower);
                    if (!qi || maxSpQuiet <= 0 || vitals.CurrentSpiritPower >= maxSpQuiet)
                        continue;
                }

                var worldPos = view.transform.position + Vector3.up * yOffset;
                var screen = worldCamera.WorldToScreenPoint(worldPos);
                if (screen.z < 0.05f)
                    continue;

                var guiX = screen.x - barWidth * 0.5f;
                var guiY = Screen.height - screen.y;
                var alpha = emphasize ? 1f : 0.55f;
                DrawBar(guiX, guiY, barWidth, barHeight, curHp / (float)maxHp, HpFill, HpBack, alpha);

                var hasShield = entity.TryGet<CultivationComponent>(out var cult) &&
                                cult.Realm >= RealmStage.QiRefining;
                var maxSp = attrs.GetFinal(AttributeId.SpiritPower);
                if (hasShield && maxSp > 0)
                {
                    var curSp = Mathf.Clamp(vitals.CurrentSpiritPower, 0, maxSp);
                    DrawBar(
                        guiX, guiY + barHeight + 2f, barWidth, barHeight - 1f,
                        curSp / (float)maxSp, SpFill, SpBack, alpha * 0.95f);
                }
            }
        }

        bool ShouldEmphasize(Entity entity, XianXia.Core.Domain.Ids.EntityId id)
        {
            if (selection != null && selection.State.Contains(id))
                return true;
            if (meleeAssault != null && meleeAssault.IsFighting &&
                meleeAssault.IsInFight(id))
                return true;
            if ((entity.Tags & EntityTag.Npc) != 0 &&
                HostNpcInteraction.IsHostileNpc(bootstrap.Session, id))
                return true;
            if ((entity.Tags & EntityTag.Character) != 0)
                return true;
            return false;
        }

        static void DrawBar(
            float x, float y, float w, float h, float fill01,
            Color fill, Color back, float alpha)
        {
            fill01 = Mathf.Clamp01(fill01);
            var prev = GUI.color;
            GUI.color = new Color(back.r, back.g, back.b, back.a * alpha);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = new Color(fill.r, fill.g, fill.b, fill.a * alpha);
            GUI.DrawTexture(new Rect(x, y, w * fill01, h), Texture2D.whiteTexture);
            GUI.color = new Color(Border.r, Border.g, Border.b, Border.a * alpha);
            // 细边
            GUI.DrawTexture(new Rect(x, y, w, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x, y + h - 1f, w, 1f), Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
