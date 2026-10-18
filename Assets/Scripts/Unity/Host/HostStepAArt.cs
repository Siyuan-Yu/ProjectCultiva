using System;
using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Entities;

namespace XianXia.Unity.Host
{
    /// <summary>LevelTester Step A art selection. All frames of an action share one imported texture.</summary>
    public static class HostStepAArt
    {
        public enum Profile { None, Villager, Guard, Protagonist, CompanionA, CompanionB, Supervisor, FarmerHoe, FarmerApron, ResidentBasket, Merchant, Other, JiangLao }
        public enum Action { Idle, Walk, Work, Attack }
        // Preserve the first four values: they are the authored legacy sheet row order.
        public enum Direction { North, South, West, East, NorthEast, SouthEast, SouthWest, NorthWest }

        const int FrameWidth = 560;
        const int FrameHeight = 440;
        // The authored foot anchor is (280,392) in a top-origin 560x440 frame.
        // At LevelTester scale the ~310px body becomes ~1.2 presentation units (root scale 1.15).
        public const float CharacterPixelsPerUnit = 300f;
        static readonly Vector2 FootPivot = new Vector2(0.5f, 48f / FrameHeight);
        static readonly Dictionary<string, Sprite[]> Frames = new Dictionary<string, Sprite[]>();
        static readonly Dictionary<string, Sprite> Props = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, Texture2D> Icons = new Dictionary<string, Texture2D>();
        static Sprite _plainFlag;

        public static Sprite FlagSprite(string factionId)
        {
            if (factionId == XianXia.Core.World.Strategic.StrategicFactionCatalog.HuangcunLaborId)
            {
                var red = MapSprite("factionControlPost");
                if (red != null) return red;
            }
            if (_plainFlag != null) return _plainFlag;
            var texture = new Texture2D(32, 48, TextureFormat.RGBA32, false);
            texture.name = "FactionFlagFallback";
            for (var row = 0; row < 48; row++)
            for (var column = 0; column < 32; column++)
                texture.SetPixel(column, row, column >= 7 && column <= 9 && row >= 2 && row <= 45 ||
                    column >= 10 && column <= 29 && row >= 25 && row <= 44 ? Color.white : Color.clear);
            texture.Apply();
            _plainFlag = Sprite.Create(texture, new Rect(0, 0, 32, 48), new Vector2(.5f, .5f), 16f);
            return _plainFlag;
        }

        public static Color FlagColor(string factionId)
        {
            if (factionId == XianXia.Core.World.Strategic.StrategicFactionCatalog.HuangcunLaborId &&
                MapSprite("factionControlPost") != null) return Color.white;
            XianXia.Core.World.Strategic.StrategicFactionCatalog.MapTint(factionId, out var red, out var green, out var blue);
            return new Color(red, green, blue);
        }

        public static Texture2D ItemIcon(string itemId)
        {
            string name;
            switch (itemId)
            {
                case "base:item_rough_wood": name = "ART-UI-003_rough_wood_v01"; break;
                case "base:item_manual_jiang_lao_legacy": name = "ART-UI-003_jiang_manual_v01"; break;
                case "base:item_manual_dongfu_secret": name = "ART-UI-003_dongfu_manual_v01"; break;
                case "base:item_art_liezhao_claw": name = "ART-UI-003_liezhao_art_v01"; break;
                case "base:item_art_kaishan_fist": name = "ART-UI-003_kaishan_art_v01"; break;
                default: return null;
            }
            return LoadIcon(name);
        }

        public static Texture2D StatusIcon(string label)
        {
            var name = label == "饱食" ? "ART-UI-002_hunger_v01" :
                label == "精力" ? "ART-UI-002_stamina_v01" : null;
            return name == null ? null : LoadIcon(name);
        }

        public static Sprite SiteMarkerSprite()
        {
            const string key = "ART-UI-007_site_marker_v01";
            if (Props.TryGetValue(key, out var sprite)) return sprite;
            var texture = LoadIcon(key);
            if (texture == null) return null;
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 500f, 0, SpriteMeshType.FullRect);
            Props.Add(key, sprite);
            return sprite;
        }

        static Texture2D LoadIcon(string name)
        {
            if (Icons.TryGetValue(name, out var texture)) return texture;
            texture = Resources.Load<Texture2D>(name);
            Icons.Add(name, texture);
            return texture;
        }

        public static Profile ResolveProfile(Entity entity)
        {
            if (entity == null || !entity.TryGet<IdentityComponent>(out var identity))
                return Profile.None;
            switch (identity.DefinitionId.ToString())
            {
                case "base:character_ch01_ref_guard_a":
                case "base:character_ch01_ref_guard_b":
                case "base:character_ch01_ref_guard_c":
                    return Profile.Guard;
                // Explicit identities keep their reviewed skin; other people share the common rig skin.
                case "base:character_ch01_ref_mortal_a":
                case "base:character_village_recruit":
                    return Profile.Villager;
                case "base:character_protagonist": return Profile.Protagonist;
                case "base:character_companion_a": return Profile.CompanionA;
                case "base:character_companion_b": return Profile.CompanionB;
                case "base:character_ch01_ref_supervisor": return Profile.Supervisor;
                case "base:character_ch01_ref_farmer_b": return Profile.FarmerHoe;
                case "base:character_ch01_ref_farmer_c": return Profile.FarmerApron;
                case "base:character_ch01_ref_mortal_b": return Profile.ResidentBasket;
                case "base:character_qingshi_shopkeeper":
                case "base:character_qingshi_auction_attendant": return Profile.Merchant;
                case "base:character_jiang_lao": return Profile.JiangLao;
                case "base:character_ch01_ref_herb_b":
                case "base:character_ch01_ref_herb_c":
                case "base:character_ch01_ref_woodcutter":
                case "base:character_ch01_ref_wood_b":
                case "base:character_ch01_ref_wood_c":
                case "base:character_qingshi_acceptance_unaffiliated_a":
                case "base:character_qingshi_acceptance_unaffiliated_b":
                case "base:character_qingshi_acceptance_unaffiliated_hostile":
                case "base:character_qingshi_acceptance_shuofeng_civilian_a":
                case "base:character_qingshi_acceptance_shuofeng_civilian_b": return Profile.Other;
                default:
                    return (entity.Tags & (EntityTag.Character | EntityTag.Npc)) != 0
                        ? Profile.Other : Profile.None;
            }
        }

        public static Sprite CharacterFrame(Profile profile, Action action, Direction direction, int frame)
        {
            if (profile == Profile.None)
                return null;
            var identity = profile == Profile.Protagonist ? "protagonist" :
                profile == Profile.CompanionA ? "companion_a" :
                profile == Profile.CompanionB ? "companion_b" : null;
            if (identity != null && action != Action.Idle && action != Action.Walk)
                return StaticCharacter(profile); // Never swap a unique actor into the common-villager sheet.
            if (identity == null && profile != Profile.Villager && profile != Profile.Guard)
                return StaticCharacter(profile);
            if (profile == Profile.Guard && action == Action.Work)
                return CharacterFrame(profile, Action.Idle, direction, 0);
            var id = identity != null ? "ART-CHAR-002_" + identity :
                profile == Profile.Guard ? "ART-CHAR-004" : "ART-CHAR-001";
            var actionName = action.ToString().ToLowerInvariant();
            var path = id + "_" + actionName + (identity == null ? "_v02" : "_v03");
            if (!Frames.TryGetValue(path, out var sprites))
            {
                var texture = Resources.Load<Texture2D>(path);
                if (texture == null || texture.width != FrameWidth * (action == Action.Idle ? 1 : 4) ||
                    texture.height != FrameHeight * 4)
                {
                    Debug.LogWarning("[StepAArt] Missing or invalid character sheet: " + path);
                    Frames[path] = Array.Empty<Sprite>();
                    return identity != null ? StaticCharacter(profile) :
                        action == Action.Idle ? null : CharacterFrame(profile, Action.Idle, direction, 0);
                }
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                var columns = action == Action.Idle ? 1 : 4;
                sprites = new Sprite[4 * columns];
                for (var row = 0; row < 4; row++)
                for (var col = 0; col < columns; col++)
                {
                    var rect = new Rect(col * FrameWidth, texture.height - (row + 1) * FrameHeight,
                        FrameWidth, FrameHeight);
                    sprites[row * columns + col] = Sprite.Create(texture, rect, FootPivot,
                        CharacterPixelsPerUnit, 0, SpriteMeshType.FullRect);
                }
                Frames[path] = sprites;
            }
            if (sprites.Length == 0)
                return identity != null ? StaticCharacter(profile) :
                    action == Action.Idle ? null : CharacterFrame(profile, Action.Idle, direction, 0);
            var framesPerDirection = action == Action.Idle ? 1 : 4;
            // Legacy sheets contain four rows. Diagonals are rendered by HumanCommonRig;
            // keep the fallback path in bounds if the rig resource is unavailable.
            var legacyDirection = direction == Direction.NorthEast || direction == Direction.NorthWest
                ? Direction.North : direction == Direction.SouthEast || direction == Direction.SouthWest
                    ? Direction.South : direction;
            return sprites[(int)legacyDirection * framesPerDirection + Mathf.Clamp(frame, 0, framesPerDirection - 1)];
        }

        public static Sprite SleepSprite(Profile profile)
        {
            string name;
            switch (profile)
            {
                case Profile.Villager: name = "ART-CHAR-001_villager_sleep_v03"; break;
                case Profile.Guard: name = "ART-CHAR-004_guard_sleep_v03"; break;
                case Profile.Protagonist: name = "ART-CHAR-002_protagonist_sleep_v03"; break;
                case Profile.CompanionA: name = "ART-CHAR-002_companion_a_sleep_v03"; break;
                case Profile.CompanionB: name = "ART-CHAR-002_companion_b_sleep_v03"; break;
                case Profile.Supervisor: name = "ART-CHAR-003_supervisor_sleep_v03"; break;
                case Profile.FarmerHoe: name = "ART-CHAR-005_resident_hoe_sleep_v03"; break;
                case Profile.FarmerApron: name = "ART-CHAR-005_resident_apron_sleep_v03"; break;
                case Profile.ResidentBasket: name = "ART-CHAR-005_resident_basket_sleep_v03"; break;
                default: return null;
            }
            if (Props.TryGetValue(name, out var cached)) return cached;
            var texture = Resources.Load<Texture2D>(name);
            if (texture == null) return null;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), CharacterPixelsPerUnit, 0, SpriteMeshType.FullRect);
            Props.Add(name, sprite);
            return sprite;
        }

        static Sprite StaticCharacter(Profile profile)
        {
            string name;
            switch (profile)
            {
                case Profile.Protagonist: name = "ART-CHAR-002_protagonist_idle_south_v02"; break;
                case Profile.CompanionA: name = "ART-CHAR-002_companion_a_idle_south_v02"; break;
                case Profile.CompanionB: name = "ART-CHAR-002_companion_b_idle_south_v02"; break;
                case Profile.Supervisor: name = "ART-CHAR-003_supervisor_idle_south_v02"; break;
                case Profile.FarmerHoe: name = "ART-CHAR-005_resident_hoe_idle_south_v02"; break;
                case Profile.FarmerApron: name = "ART-CHAR-005_resident_apron_idle_south_v02"; break;
                case Profile.ResidentBasket: name = "ART-CHAR-005_resident_basket_idle_south_v02"; break;
                default: return null;
            }
            if (Props.TryGetValue(name, out var sprite)) return sprite;
            var texture = Resources.Load<Texture2D>(name);
            if (texture == null) return null;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            // Static cutouts have different source dimensions; normalize visible height, not outfit width.
            var ppu = texture.height / 1.18f;
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.035f), ppu, 0, SpriteMeshType.FullRect);
            Props.Add(name, sprite);
            return sprite;
        }

        public static Sprite MapSprite(string kind)
        {
            string name;
            switch (kind)
            {
                case "grass": name = "ART-GROUND-001_grass_tile_seam_candidate_v02"; break;
                case "treeS": name = "ART-GROUND-007_tree_small_v02"; break;
                case "treeM": name = "ART-GROUND-007_tree_medium_v02"; break;
                case "treeL": name = "ART-GROUND-007_tree_large_v02"; break;
                case "mine":
                case "ore": name = "ART-GROUND-009_ore_base_v02"; break;
                case "cave": name = "ART-ROOM-010_cave_entrance_v02"; break;
                case "storageRoom": name = "ART-ROOM-007_storage_crates_v03"; break;
                case "controlCore": name = "ART-ROOM-008_core_isolated_v03"; break;
                case "factionControlPost": name = "ART-ROOM-008_flag_red_v02"; break;
                default: return null;
            }
            return LoadProp(name);
        }

        public static Sprite BedSprite(XianXia.Core.Construction.RoomUsage usage)
        {
            var name = usage == XianXia.Core.Construction.RoomUsage.Prison
                ? "ART-ROOM-006_bed_prison_v02"
                : usage == XianXia.Core.Construction.RoomUsage.Medical
                    ? "ART-ROOM-006_bed_medical_v02"
                    : "ART-ROOM-006_bed_residential_v02";
            return LoadProp(name);
        }

        static Sprite LoadProp(string name)
        {
            if (Props.TryGetValue(name, out var sprite))
                return sprite;
            var texture = Resources.Load<Texture2D>(name);
            if (texture == null)
            {
                Debug.LogWarning("[StepAArt] Missing prop texture: " + name);
                return null;
            }
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            Props.Add(name, sprite);
            return sprite;
        }

        /// <summary>Visual child only: original fit, colliders, interaction bounds and root position stay intact.</summary>
        public static bool ApplyObjectVisual(GameObject root, Sprite sprite, int visualQuarterTurns = 0)
        {
            if (root == null || sprite == null)
                return false;
            const string overlayName = "StepAArtVisual";
            var existing = root.transform.Find(overlayName);
            SpriteRenderer overlay = existing != null ? existing.GetComponent<SpriteRenderer>() : null;
            var renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            var bedLayers = root.GetComponent<HostBedVisualLayers>();
            Bounds bounds = default;
            var found = false;
            var order = -12;
            var sortingLayerId = 0;
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || !renderer.enabled || renderer.sprite == null)
                    continue;
                if (bedLayers != null && bedLayers.IsForegroundRenderer(renderer)) continue;
                if (!found)
                {
                    bounds = renderer.bounds;
                    order = renderer.sortingOrder;
                    sortingLayerId = renderer.sortingLayerID;
                    found = true;
                }
                else bounds.Encapsulate(renderer.bounds);
            }
            if (!found)
                return false;
            if (overlay == null)
            {
                var child = new GameObject(overlayName);
                child.transform.SetParent(root.transform, false);
                overlay = child.AddComponent<SpriteRenderer>();
            }
            overlay.sprite = sprite;
            overlay.color = Color.white;
            overlay.sortingLayerID = sortingLayerId;
            overlay.sortingOrder = order;
            overlay.transform.position = new Vector3(bounds.center.x, bounds.center.y, root.transform.position.z);
            var quarter = ((visualQuarterTurns % 4) + 4) % 4;
            overlay.transform.localRotation = Quaternion.Euler(0f, 0f, -90f * quarter);
            var sideways = (quarter & 1) != 0;
            overlay.transform.localScale = new Vector3(
                (sideways ? bounds.size.y : bounds.size.x) / Mathf.Max(0.0001f,
                    sprite.bounds.size.x * Mathf.Abs(sideways ? root.transform.lossyScale.y : root.transform.lossyScale.x)),
                (sideways ? bounds.size.x : bounds.size.y) / Mathf.Max(0.0001f,
                    sprite.bounds.size.y * Mathf.Abs(sideways ? root.transform.lossyScale.x : root.transform.lossyScale.y)), 1f);
            for (var i = 0; i < renderers.Length; i++)
                if (renderers[i] != null && renderers[i] != overlay)
                    renderers[i].enabled = false;
            return true;
        }
    }
}
