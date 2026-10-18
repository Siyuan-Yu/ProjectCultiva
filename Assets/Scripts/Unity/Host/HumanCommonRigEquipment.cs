using System;
using UnityEngine;

namespace XianXia.Unity.Host
{
    /// <summary>Visual attachments only. No item ownership, combat stats, or save authority.</summary>
    public sealed class HumanCommonRigEquipment : MonoBehaviour
    {
        [Serializable]
        public struct Attachment
        {
            public Sprite sprite;
            public Vector2 grip01;
            public float height;
            public float angle;
            public bool twoHanded;
            public Vector2 supportGrip01;
        }
        [Serializable]
        public struct WorkTool
        {
            public HumanWorkMotion motion;
            public Attachment attachment; // Null sprite explicitly means empty hand.
        }

        [SerializeField] bool overrideWeapon;
        [SerializeField] Attachment weapon;
        [SerializeField] HumanAttackStyle weaponStyle = HumanAttackStyle.Swing;
        [SerializeField] WorkTool[] workTools = Array.Empty<WorkTool>();
        bool _guardDefault;

        public HumanAttackStyle AttackStyle => overrideWeapon
            ? weapon.sprite != null ? weaponStyle : HumanAttackStyle.Unarmed
            : _guardDefault ? HumanAttackStyle.Thrust : HumanAttackStyle.Unarmed;
        public HumanCommonRigView.Equipment WeaponKind => !overrideWeapon && _guardDefault
            ? HumanCommonRigView.Equipment.Spear : HumanCommonRigView.Equipment.None;

        public void ConfigureDefault(HostStepAArt.Profile profile) => _guardDefault = profile == HostStepAArt.Profile.Guard;
        public void SetWeaponAppearance(Sprite sprite, HumanAttackStyle style, Vector2 grip01, float height = 1f)
        {
            overrideWeapon = true;
            weapon = new Attachment { sprite = sprite, grip01 = grip01, height = height };
            weaponStyle = style;
        }
        public void SetWeaponAppearance(Attachment attachment, HumanAttackStyle style)
        {
            overrideWeapon = true;
            weapon = attachment;
            weaponStyle = style;
        }
        public void ClearWeaponOverride() => overrideWeapon = false;
        public void SetWorkToolAppearance(HumanWorkMotion motion, Attachment attachment)
        {
            if (workTools == null) workTools = Array.Empty<WorkTool>();
            for (var i = 0; i < workTools.Length; i++)
                if (workTools[i].motion == motion) { workTools[i].attachment = attachment; return; }
            Array.Resize(ref workTools, workTools.Length + 1);
            workTools[workTools.Length - 1] = new WorkTool { motion = motion, attachment = attachment };
        }
        public Attachment ResolveWeapon(Sprite fallback) => overrideWeapon ? weapon : Default(fallback,
            _guardDefault ? HumanCommonRigView.Equipment.Spear : HumanCommonRigView.Equipment.None);
        public Attachment ResolveWorkTool(HumanWorkMotion motion, Sprite fallback,
            HumanCommonRigView.Equipment kind = HumanCommonRigView.Equipment.None)
        {
            if (workTools == null) return Default(fallback, kind);
            for (var i = 0; i < workTools.Length; i++)
                if (workTools[i].motion == motion) return workTools[i].attachment;
            return Default(fallback, kind);
        }
        public static Attachment Default(Sprite sprite, HumanCommonRigView.Equipment kind)
        {
            var attachment = new Attachment
            { sprite = sprite, grip01 = sprite == null ? Vector2.zero : sprite.pivot / sprite.rect.size };
            // These points describe the existing atlas handle, not an equipment/gameplay definition.
            switch (kind)
            {
                case HumanCommonRigView.Equipment.Spear:
                    attachment.grip01 = new Vector2(.5f, .4f);
                    attachment.twoHanded = true;
                    attachment.supportGrip01 = new Vector2(.5f, .5f);
                    break;
                case HumanCommonRigView.Equipment.Axe:
                    attachment.grip01 = new Vector2(.34f, .24f);
                    break;
                case HumanCommonRigView.Equipment.Hoe:
                    attachment.grip01 = new Vector2(.85f, .22f);
                    attachment.twoHanded = true;
                    attachment.supportGrip01 = new Vector2(.81f, .35f);
                    break;
                case HumanCommonRigView.Equipment.Pick:
                    attachment.grip01 = new Vector2(.52f, .22f);
                    attachment.twoHanded = true;
                    attachment.supportGrip01 = new Vector2(.51f, .37f);
                    break;
                case HumanCommonRigView.Equipment.Sickle:
                    attachment.grip01 = new Vector2(.71f, .18f);
                    break;
            }
            return attachment;
        }
    }
}
