using System;
using UnityEngine;
using XianXia.Core.Inventory;

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
        bool _hasEquipmentSync, _syncedExplicitLoadout;
        WeaponKind? _syncedWeapon;
        static Sprite _sword, _saber, _hammer;

        public HumanAttackStyle AttackStyle => overrideWeapon
            ? weapon.sprite != null ? weaponStyle : HumanAttackStyle.Unarmed
            : _guardDefault ? HumanAttackStyle.Swing : HumanAttackStyle.Unarmed;
        public HumanCommonRigView.Equipment DefaultWeaponKind => !overrideWeapon && _guardDefault
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

        /// <summary>Read-only projection of Core equipment; it never equips or creates items.</summary>
        public void SynchronizeEquippedWeapon(WeaponSpec equipped, bool playerControllable)
        {
            var explicitLoadout = equipped != null || playerControllable;
            WeaponKind? kind = equipped?.Kind;
            if (_hasEquipmentSync && _syncedExplicitLoadout == explicitLoadout && _syncedWeapon == kind) return;
            var hadSync = _hasEquipmentSync;
            _hasEquipmentSync = true;
            _syncedExplicitLoadout = explicitLoadout;
            _syncedWeapon = kind;
            if (!explicitLoadout)
            {
                // Unowned NPC guards retain their authored default spear. Only undo our own
                // prior projection, never discard a freshly authored inspector override.
                if (hadSync) ClearWeaponOverride();
                return;
            }
            if (!kind.HasValue)
            {
                SetWeaponAppearance(default(Attachment), HumanAttackStyle.Unarmed);
                return;
            }
            var sprite = GetWeaponSprite(kind.Value);
            var tool = ToolKind(kind.Value);
            var attachment = Default(sprite, tool);
            if (tool == HumanCommonRigView.Equipment.None)
            {
                // Generated art uses a vertical handle; these are the actual palm points.
                attachment.grip01 = kind == WeaponKind.Saber ? new Vector2(18f / 56f, 20f / 144f) :
                    kind == WeaponKind.Hammer ? new Vector2(.5f, 19f / 104f) : new Vector2(.5f, 19f / 144f);
            }
            SetWeaponAppearance(attachment, HumanAttackStyle.Swing);
        }

        static HumanCommonRigView.Equipment ToolKind(WeaponKind kind)
        {
            switch (kind)
            {
                case WeaponKind.Hoe: return HumanCommonRigView.Equipment.Hoe;
                case WeaponKind.Axe: return HumanCommonRigView.Equipment.Axe;
                case WeaponKind.Pick: return HumanCommonRigView.Equipment.Pick;
                case WeaponKind.Spear: return HumanCommonRigView.Equipment.Spear;
                default: return HumanCommonRigView.Equipment.None;
            }
        }

        /// <summary>One shared sprite per weapon, also reusable by backpack item slots.</summary>
        public static Sprite GetWeaponSprite(WeaponKind kind)
        {
            var tool = ToolKind(kind);
            if (tool != HumanCommonRigView.Equipment.None) return HumanCommonRigView.GetToolSprite(tool);
            switch (kind)
            {
                case WeaponKind.Sword: return _sword != null ? _sword : (_sword = CreateBlade(kind));
                case WeaponKind.Saber: return _saber != null ? _saber : (_saber = CreateBlade(kind));
                case WeaponKind.Hammer: return _hammer != null ? _hammer : (_hammer = CreateBlade(kind));
                default: return null;
            }
        }

        // Lightweight first-pass silhouettes, generated only on first use. Existing atlas art
        // remains the source for tools/spears. No per-view textures, meshes, or frame allocations.
        static Sprite CreateBlade(WeaponKind kind)
        {
            var width = kind == WeaponKind.Sword ? 40 : kind == WeaponKind.Saber ? 56 : 64;
            var height = kind == WeaponKind.Hammer ? 104 : 144;
            var pixels = new Color32[width * height];
            var outline = new Color32(41, 36, 33, 255);
            var steel = new Color32(136, 155, 163, 255);
            var light = new Color32(221, 227, 216, 255);
            var shadow = new Color32(75, 91, 99, 255);
            var wood = new Color32(117, 68, 36, 255);
            var wrap = new Color32(175, 117, 55, 255);
            var brass = new Color32(173, 139, 68, 255);
            if (kind == WeaponKind.Sword)
            {
                PaintPolygon(pixels,width,height,outline, new Vector2(13,38),new Vector2(13,124),new Vector2(20,140),new Vector2(27,124),new Vector2(27,38));
                PaintPolygon(pixels,width,height,steel, new Vector2(15,40),new Vector2(15,124),new Vector2(20,135),new Vector2(25,124),new Vector2(25,40));
                PaintPolygon(pixels,width,height,light, new Vector2(16,41),new Vector2(16,123),new Vector2(20,134),new Vector2(20,41));
                PaintRect(pixels,width,height,14,5,26,37,outline);
                PaintRect(pixels,width,height,16,8,24,35,wood);
                for (var y=11;y<34;y+=6) PaintRect(pixels,width,height,16,y,24,y+2,wrap);
                PaintRect(pixels,width,height,4,34,36,41,outline);
                PaintRect(pixels,width,height,6,36,34,39,brass);
                PaintRect(pixels,width,height,14,4,26,10,brass);
            }
            else if (kind == WeaponKind.Saber)
            {
                // Broad single edge that widens toward its curved tip, distinct from the sword.
                PaintPolygon(pixels,width,height,outline, new Vector2(12,37),new Vector2(12,116),new Vector2(16,139),new Vector2(40,124),new Vector2(46,103),new Vector2(38,61),new Vector2(26,37));
                PaintPolygon(pixels,width,height,steel, new Vector2(15,40),new Vector2(15,116),new Vector2(18,134),new Vector2(37,122),new Vector2(42,103),new Vector2(35,62),new Vector2(24,40));
                PaintPolygon(pixels,width,height,light, new Vector2(18,134),new Vector2(37,122),new Vector2(42,103),new Vector2(35,62),new Vector2(24,40),new Vector2(27,61),new Vector2(35,104),new Vector2(31,121));
                PaintRect(pixels,width,height,12,5,24,37,outline);
                PaintRect(pixels,width,height,14,8,22,35,wood);
                for (var y=10;y<33;y+=6) PaintRect(pixels,width,height,14,y,22,y+2,wrap);
                PaintRect(pixels,width,height,5,34,34,41,outline);
                PaintRect(pixels,width,height,7,36,32,39,brass);
                PaintRect(pixels,width,height,12,4,24,10,brass);
            }
            else
            {
                PaintRect(pixels,width,height,26,5,38,87,outline);
                PaintRect(pixels,width,height,29,8,35,85,wood);
                for (var y=10;y<43;y+=7) PaintRect(pixels,width,height,28,y,36,y+2,wrap);
                PaintRect(pixels,width,height,5,72,59,100,outline);
                PaintRect(pixels,width,height,8,75,56,97,steel);
                PaintRect(pixels,width,height,8,90,56,97,light);
                PaintRect(pixels,width,height,8,75,56,81,shadow);
                PaintRect(pixels,width,height,28,75,36,97,shadow);
                PaintRect(pixels,width,height,6,74,11,98,brass);
                PaintRect(pixels,width,height,53,74,58,98,brass);
            }
            var texture = new Texture2D(width,height,TextureFormat.RGBA32,false)
            { name = "SharedWeapon_" + kind, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixels32(pixels);
            texture.Apply(false,true);
            var worldHeight = kind == WeaponKind.Hammer ? .9f : kind == WeaponKind.Saber ? 1.2f : 1.3f;
            var sprite = Sprite.Create(texture,new Rect(0,0,width,height),new Vector2(.5f,.5f),height/worldHeight,
                0,SpriteMeshType.FullRect);
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        static void PaintRect(Color32[] pixels,int width,int height,int x0,int y0,int x1,int y1,Color32 color)
        {
            for(var y=Mathf.Max(0,y0);y<Mathf.Min(height,y1);y++)
                for(var x=Mathf.Max(0,x0);x<Mathf.Min(width,x1);x++) pixels[y*width+x]=color;
        }

        static void PaintPolygon(Color32[] pixels,int width,int height,Color32 color,params Vector2[] points)
        {
            for(var y=0;y<height;y++) for(var x=0;x<width;x++)
            {
                var inside=false;
                for(int i=0,j=points.Length-1;i<points.Length;j=i++)
                {
                    var a=points[i]; var b=points[j];
                    if((a.y>y+.5f)!=(b.y>y+.5f) &&
                        x+.5f<(b.x-a.x)*(y+.5f-a.y)/(b.y-a.y)+a.x) inside=!inside;
                }
                if(inside) pixels[y*width+x]=color;
            }
        }
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
