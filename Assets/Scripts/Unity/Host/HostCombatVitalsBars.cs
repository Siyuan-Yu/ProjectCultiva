using UnityEngine;
using XianXia.Core.Attributes;
using XianXia.Core.Combat;
using XianXia.Core.Cultivation;
using XianXia.Core.Entities;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// 人体脚下生命／灵力护盾条，世界尺寸随人物和镜头一起缩放。
    /// </summary>
    public sealed class HostCombatVitalsBars : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] EntityViewSpawner viewSpawner;
        [SerializeField] HostNpcMeleeAssault meleeAssault;
        [SerializeField] HostSelectionController selection;
        [SerializeField] Camera worldCamera;
        // Actor-local world units, including spacing and borders; never clamp to screen pixels.
        [SerializeField] float barLocalWidth = .8f;
        [SerializeField] float barLocalHeight = .07f;
        [SerializeField] float groundGap = .10f;
        [SerializeField] float shieldGap = .025f;
        [SerializeField] float borderThickness = .012f;

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
            // This overlay has no controls: layout/key events need no entity scan or projection.
            if (Event.current.type != EventType.Repaint) return;
            if (bootstrap?.Session?.World == null || viewSpawner == null)
                return;
            // 人物详情是硬模态；世界人物条不得穿透到其上层。
            if (bootstrap.CharacterSheetPanel != null && bootstrap.CharacterSheetPanel.IsOpen)
                return;
            if (worldCamera == null)
                worldCamera = Camera.main;
            if (worldCamera == null)
                return;

            var world = bootstrap.Session.World;
            foreach (var view in viewSpawner.Registry.All)
            {
                if (view == null || !view.IsBound || !view.gameObject.activeInHierarchy)
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

                if (!view.TryGetVisibleBodyBounds(out _))
                    continue;
                var scale = view.transform.lossyScale;
                var scaleX = Mathf.Abs(scale.x);
                var scaleY = Mathf.Abs(scale.y);
                var width = Mathf.Max(0f, barLocalWidth) * scaleX;
                var height = Mathf.Max(0f, barLocalHeight) * scaleY;
                var border = Mathf.Max(0f, borderThickness) * scaleY;
                var topCenter = view.VitalsGroundAnchor - Vector3.up * (Mathf.Max(0f, groundGap) * scaleY);
                if (!TryProjectBar(topCenter, width, height, border, out var hpRect, out var hpBorder))
                    continue;
                var alpha = emphasize ? 1f : 0.55f;
                DrawBar(hpRect, hpBorder, curHp / (float)maxHp, HpFill, HpBack, alpha);

                var hasShield = entity.TryGet<CultivationComponent>(out var cult) &&
                                cult.Realm >= RealmStage.QiRefining;
                var maxSp = attrs.GetFinal(AttributeId.SpiritPower);
                if (hasShield && maxSp > 0)
                {
                    var curSp = Mathf.Clamp(vitals.CurrentSpiritPower, 0, maxSp);
                    var shieldTop = topCenter - Vector3.up * (height + Mathf.Max(0f, shieldGap) * scaleY);
                    if (TryProjectBar(shieldTop, width, height * .8f, border,
                        out var spRect, out var spBorder))
                        DrawBar(spRect, spBorder, curSp / (float)maxSp, SpFill, SpBack, alpha * 0.95f);
                }
            }
        }

        bool TryProjectBar(Vector3 topCenter, float width, float height, float border,
            out Rect rect, out float projectedBorder)
        {
            rect = default;
            projectedBorder = 0f;
            if (width <= 0f || height <= 0f)
                return false;
            var topLeft = worldCamera.WorldToScreenPoint(topCenter + Vector3.left * (width * .5f));
            var bottomRight = worldCamera.WorldToScreenPoint(
                topCenter + Vector3.right * (width * .5f) + Vector3.down * height);
            if (topLeft.z < .05f || bottomRight.z < .05f)
                return false;
            rect = new Rect(Mathf.Min(topLeft.x, bottomRight.x),
                Screen.height - Mathf.Max(topLeft.y, bottomRight.y),
                Mathf.Abs(bottomRight.x - topLeft.x), Mathf.Abs(topLeft.y - bottomRight.y));
            projectedBorder = rect.height * Mathf.Clamp(border / height, 0f, .5f);
            return rect.width > 0f && rect.height > 0f &&
                rect.xMax >= 0f && rect.yMax >= 0f && rect.xMin <= Screen.width && rect.yMin <= Screen.height;
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
            Rect rect, float border, float fill01,
            Color fill, Color back, float alpha)
        {
            fill01 = Mathf.Clamp01(fill01);
            var prev = GUI.color;
            GUI.color = new Color(back.r, back.g, back.b, back.a * alpha);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = new Color(fill.r, fill.g, fill.b, fill.a * alpha);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * fill01, rect.height), Texture2D.whiteTexture);
            GUI.color = new Color(Border.r, Border.g, Border.b, Border.a * alpha);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, border), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - border, rect.width, border), Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
