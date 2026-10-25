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
        static readonly Dictionary<string, Sprite> SurfaceTiles = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, Sprite[]> FarmStages = new Dictionary<string, Sprite[]>();
        static Sprite[] _roomDoorParts;
        static Sprite[] _roomUsageMarkers;
        static readonly Dictionary<string, Texture2D> Icons = new Dictionary<string, Texture2D>();
        [Serializable] sealed class AppearanceTable { public CharacterAppearance[] characters; public ItemAppearance[] items; }
        [Serializable] sealed class CharacterAppearance { public string id; public string profile; public RigSkin rig; }
        [Serializable] public sealed class RigSkin
        {
            public string partsBase;
            public string sideHeadAtlas;
            public int sideHeadColumns;
            public int sideHeadIndex;
            public bool basket;
            public string staticSprite;
            public string sleepSprite;
        }
        [Serializable] sealed class ItemAppearance { public string id; public string icon; public string groundKind; }
        static Dictionary<string, Profile> _characterProfiles;
        static Dictionary<string, RigSkin> _rigSkins;
        static Dictionary<string, ItemAppearance> _itemAppearances;
        static Sprite _plainFlag;

        static void EnsureAppearanceTable()
        {
            if (_characterProfiles != null) return;
            _characterProfiles = new Dictionary<string, Profile>(StringComparer.Ordinal);
            _rigSkins = new Dictionary<string, RigSkin>(StringComparer.Ordinal);
            _itemAppearances = new Dictionary<string, ItemAppearance>(StringComparer.Ordinal);
            var source = Resources.Load<TextAsset>("HostAppearanceMappings");
            if (source == null) return;
            var table = JsonUtility.FromJson<AppearanceTable>(source.text);
            if (table?.characters != null)
                foreach (var entry in table.characters)
                    if (entry != null && !string.IsNullOrWhiteSpace(entry.id) &&
                        Enum.TryParse(entry.profile, false, out Profile profile) && profile != Profile.None)
                    {
                        _characterProfiles[entry.id] = profile;
                        if (entry.rig != null && !string.IsNullOrWhiteSpace(entry.rig.partsBase))
                            _rigSkins[entry.id] = entry.rig;
                    }
            if (table?.items != null)
                foreach (var entry in table.items)
                    if (entry != null && !string.IsNullOrWhiteSpace(entry.id)) _itemAppearances[entry.id] = entry;
        }

        public static List<string> ValidateAppearanceMappings()
        {
            var errors = new List<string>();
            var source = Resources.Load<TextAsset>("HostAppearanceMappings");
            if (source == null)
            {
                errors.Add("缺少 HostAppearanceMappings TextAsset。");
                return errors;
            }
            AppearanceTable table;
            try { table = JsonUtility.FromJson<AppearanceTable>(source.text); }
            catch (Exception error)
            {
                errors.Add("HostAppearanceMappings JSON 无效：" + error.Message);
                return errors;
            }
            if (table == null || table.characters == null || table.items == null)
            {
                errors.Add("HostAppearanceMappings 缺少 characters 或 items 列表。");
                return errors;
            }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in table.characters)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.id))
                {
                    errors.Add("角色外观映射含空 ID。");
                    continue;
                }
                if (!ids.Add("character:" + entry.id)) errors.Add("角色外观重复 ID：" + entry.id);
                if (!Enum.TryParse(entry.profile, false, out Profile profile) ||
                    !Enum.IsDefined(typeof(Profile), profile) || profile == Profile.None)
                    errors.Add("角色外观无效 profile：" + entry.id + " / " + entry.profile);
                var rig = entry.rig;
                if (rig == null) continue;
                if (string.IsNullOrWhiteSpace(rig.partsBase) || string.IsNullOrWhiteSpace(rig.sideHeadAtlas) ||
                    string.IsNullOrWhiteSpace(rig.staticSprite) || rig.sideHeadColumns < 1 ||
                    rig.sideHeadIndex < 0 || rig.sideHeadIndex >= rig.sideHeadColumns)
                {
                    errors.Add("角色自定义 rig 字段不完整：" + entry.id);
                    continue;
                }
                RequireTexture(rig.partsBase + "_HumanCommonRig_parts_v01", entry.id, errors);
                RequireLayout(rig.partsBase + "_HumanCommonRig_parts_v01_layout", entry.id, errors);
                RequireTexture(rig.sideHeadAtlas, entry.id, errors);
                RequireLayout(rig.sideHeadAtlas + "_layout", entry.id, errors);
                RequireTexture(rig.staticSprite, entry.id, errors);
                if (!string.IsNullOrWhiteSpace(rig.sleepSprite)) RequireTexture(rig.sleepSprite, entry.id, errors);
            }
            foreach (var entry in table.items)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.id))
                {
                    errors.Add("物品外观映射含空 ID。");
                    continue;
                }
                if (!ids.Add("item:" + entry.id)) errors.Add("物品外观重复 ID：" + entry.id);
                if (!string.IsNullOrWhiteSpace(entry.groundKind) && entry.groundKind != "bundle" &&
                    entry.groundKind != "book" && entry.groundKind != "scroll" && entry.groundKind != "herb")
                    errors.Add("物品地面表现类型无效：" + entry.id + " / " + entry.groundKind);
                if (!string.IsNullOrWhiteSpace(entry.icon)) RequireTexture(entry.icon, entry.id, errors);
            }
            return errors;
        }

        static void RequireTexture(string key, string owner, List<string> errors)
        {
            if (Resources.Load<Texture2D>(key) == null) errors.Add(owner + " 缺少 Texture2D：" + key);
        }

        static void RequireLayout(string key, string owner, List<string> errors)
        {
            if (Resources.Load<TextAsset>(key) == null) errors.Add(owner + " 缺少布局 TextAsset：" + key);
        }

        public static Sprite FlagSprite(string factionId)
        {
            if (factionId == XianXia.Core.World.Strategic.StrategicFactionCatalog.HuangcunLaborId)
            {
                var red = MapSprite("factionControlPost");
                if (red != null) return red;
            }
            if (factionId == XianXia.Core.World.Strategic.StrategicFactionCatalog.PlayerFactionId)
            {
                var green = LoadProp("ART-ROOM-008_flag_green_v02");
                if (green != null) return green;
            }
            if (factionId == XianXia.Core.World.Strategic.StrategicFactionCatalog.FisherVillageId)
            {
                var blue = LoadProp("ART-ROOM-008_flag_blue_v02");
                if (blue != null) return blue;
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
            if ((factionId == XianXia.Core.World.Strategic.StrategicFactionCatalog.HuangcunLaborId &&
                 MapSprite("factionControlPost") != null) ||
                (factionId == XianXia.Core.World.Strategic.StrategicFactionCatalog.PlayerFactionId &&
                 LoadProp("ART-ROOM-008_flag_green_v02") != null) ||
                (factionId == XianXia.Core.World.Strategic.StrategicFactionCatalog.FisherVillageId &&
                 LoadProp("ART-ROOM-008_flag_blue_v02") != null)) return Color.white;
            XianXia.Core.World.Strategic.StrategicFactionCatalog.MapTint(factionId, out var red, out var green, out var blue);
            return new Color(red, green, blue);
        }

        public static Texture2D ItemIcon(string itemId)
        {
            EnsureAppearanceTable();
            return itemId != null && _itemAppearances.TryGetValue(itemId, out var appearance) &&
                !string.IsNullOrWhiteSpace(appearance.icon) ? LoadIcon(appearance.icon) : null;
        }

        public static string ItemGroundKind(string itemId)
        {
            EnsureAppearanceTable();
            return itemId != null && _itemAppearances.TryGetValue(itemId, out var appearance) &&
                !string.IsNullOrWhiteSpace(appearance.groundKind) ? appearance.groundKind : "bundle";
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
            EnsureAppearanceTable();
            return _characterProfiles.TryGetValue(identity.DefinitionId.ToString(), out var profile)
                ? profile : (entity.Tags & (EntityTag.Character | EntityTag.Npc)) != 0 ? Profile.Other : Profile.None;
        }

        public static RigSkin ResolveRigSkin(Entity entity)
        {
            if (entity == null || !entity.TryGet<IdentityComponent>(out var identity)) return null;
            EnsureAppearanceTable();
            return _rigSkins.TryGetValue(identity.DefinitionId.ToString(), out var skin) ? skin : null;
        }

        public static Sprite CustomStaticSprite(RigSkin skin)
        {
            if (skin == null || string.IsNullOrWhiteSpace(skin.staticSprite)) return null;
            if (Props.TryGetValue(skin.staticSprite, out var cached)) return cached;
            var texture = Resources.Load<Texture2D>(skin.staticSprite);
            if (texture == null) return null;
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(.5f, .035f), texture.height / 1.18f, 0, SpriteMeshType.FullRect);
            Props.Add(skin.staticSprite, sprite);
            return sprite;
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

        public static Sprite SleepSprite(Profile profile, RigSkin skin = null)
        {
            if (skin != null && !string.IsNullOrWhiteSpace(skin.sleepSprite))
            {
                if (Props.TryGetValue(skin.sleepSprite, out var custom)) return custom;
                var image = Resources.Load<Texture2D>(skin.sleepSprite);
                if (image == null) return null;
                custom = Sprite.Create(image, new Rect(0, 0, image.width, image.height),
                    new Vector2(.5f, .5f), CharacterPixelsPerUnit, 0, SpriteMeshType.FullRect);
                Props.Add(skin.sleepSprite, custom);
                return custom;
            }
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
            if (kind == "zoneMine") return StoneFloorTile();
            if (kind == "cushion") return CultivateCushionSprite();
            if (kind == "chessBoard") return LoadProp("ART-ROOM-011_weiqi_board_v01");
            if (kind == "road") return SurfaceTile("ART-GROUND-002_road_horizontal_v02", 319f);
            if (kind == "roadVertical") return SurfaceTile("ART-GROUND-002_road_vertical_v02", 425f);
            if (kind == "roadCross") return SurfaceTile("ART-GROUND-002_road_cross_v02", 406f);
            if (kind == "wall") return SolidWallSprite();
            if (kind == "stoneWall") return StoneWallTile();
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
                case "recoverySpot": name = "ART-ROOM-009_recovery_cushion_v01"; break;
                case "storageRoom": name = "ART-ROOM-007_storage_crates_v03"; break;
                case "controlCore": name = "ART-ROOM-008_council_hall_ward_v05"; break;
                case "factionControlPost": name = "ART-ROOM-008_flag_red_v02"; break;
                default: return null;
            }
            return LoadProp(name);
        }

        public static Sprite RoomFloorTile() => SurfaceTile("ART-ROOM-001_floor_interior_v02", 520f);

        public static Sprite StoneFloorTile() => SurfaceTile("ART-GROUND-004_stone_mine_floor_v01", 512f);

        public static Sprite StoneWallTile() => SolidWallSprite("ART-ROOM-012_stone_wall_solid_v01");

        // Source artwork is split at runtime so the visual opening follows the real two-cell doorway.
        // 0/1 are the posts and 2 is the shallow lintel; none carries collision authority.
        public static Sprite RoomDoorPart(int part)
        {
            if (part < 0 || part > 2) return null;
            if (_roomDoorParts == null)
            {
                var texture = Resources.Load<Texture2D>("ART-ROOM-003_door_frame_source_v03");
                _roomDoorParts = new Sprite[3];
                if (texture == null)
                {
                    Debug.LogWarning("[StepAArt] Missing room door frame source.");
                    return null;
                }
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                var sx = texture.width / 1991f;
                var sy = texture.height / 790f;
                _roomDoorParts[0] = Sprite.Create(texture, new Rect(565 * sx, 0, 145 * sx, 655 * sy),
                    new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
                _roomDoorParts[1] = Sprite.Create(texture, new Rect(1275 * sx, 0, 155 * sx, 655 * sy),
                    new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
                _roomDoorParts[2] = Sprite.Create(texture, new Rect(610 * sx, 625 * sy, 780 * sx, 160 * sy),
                    new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
            }
            return _roomDoorParts[part];
        }

        public static Sprite RoomUsageMarker(XianXia.Core.Construction.RoomUsage usage)
        {
            var index = usage == XianXia.Core.Construction.RoomUsage.Medical ? 1 :
                usage == XianXia.Core.Construction.RoomUsage.Prison ? 2 : 0;
            if (_roomUsageMarkers == null)
            {
                var texture = Resources.Load<Texture2D>("ART-ROOM-005_usage_markers_v02");
                _roomUsageMarkers = new Sprite[3];
                if (texture == null)
                {
                    Debug.LogWarning("[StepAArt] Missing room usage markers.");
                    return null;
                }
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                var sx = texture.width / 2048f;
                var sy = texture.height / 768f;
                var starts = new[] { 260f, 790f, 1350f };
                for (var i = 0; i < 3; i++)
                    _roomUsageMarkers[i] = Sprite.Create(texture, new Rect(starts[i] * sx, 0, 450f * sx, 768f * sy),
                        new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
            }
            return _roomUsageMarkers[index];
        }

        public static Sprite FarmStageSprite(string kind, int stage)
        {
            var name = kind == "grainField" ? "ART-GROUND-010_grain_4stage_sheet_v01" :
                kind == "herbField" ? "ART-GROUND-011_herb_4stage_sheet_v01" : null;
            if (name == null || stage < 0 || stage > 3) return null;
            if (!FarmStages.TryGetValue(name, out var sprites))
            {
                var texture = Resources.Load<Texture2D>(name);
                if (texture == null || texture.width != 2172 || texture.height != 724)
                {
                    Debug.LogWarning("[StepAArt] Missing or invalid four-stage farm sheet: " + name);
                    return null;
                }
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                sprites = new Sprite[4];
                for (var i = 0; i < 4; i++)
                    // Use the inner furrow/crop area: the full authored square has a dirt rim.
                    // Repeating that rim once per logical cell looks like miniature separate plots.
                    sprites[i] = Sprite.Create(texture, new Rect(i * 543 + 80, 174, 384, 384),
                        new Vector2(.5f, .5f), 384f, 0, SpriteMeshType.FullRect);
                FarmStages.Add(name, sprites);
            }
            return sprites[stage];
        }

        static Sprite SolidWallSprite() => SolidWallSprite("ART-ROOM-002_wood_wall_solid_v03");

        static Sprite SolidWallSprite(string name)
        {
            if (Props.TryGetValue(name, out var sprite)) return sprite;
            var texture = Resources.Load<Texture2D>(name);
            if (texture == null)
            {
                Debug.LogWarning("[StepAArt] Missing wall texture: " + name);
                return null;
            }
            texture.filterMode = texture.mipmapCount > 1 ? FilterMode.Trilinear : FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            // The generated cutout has a small transparent margin; use only its opaque square.
            var marginX = texture.width * (32f / 1254f);
            var marginY = texture.height * (32f / 1254f);
            sprite = Sprite.Create(texture, new Rect(marginX, marginY,
                texture.width - 2f * marginX, texture.height - 2f * marginY),
                new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
            Props.Add(name, sprite);
            return sprite;
        }

        static Sprite SurfaceTile(string name, float pixelsPerUnit)
        {
            if (SurfaceTiles.TryGetValue(name, out var sprite)) return sprite;
            var texture = Resources.Load<Texture2D>(name);
            if (texture == null)
            {
                Debug.LogWarning("[StepAArt] Missing surface tile: " + name);
                return null;
            }
            texture.filterMode = texture.mipmapCount > 1 ? FilterMode.Trilinear : FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(.5f, .5f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
            SurfaceTiles.Add(name, sprite);
            return sprite;
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

        /// <summary>
        /// 修炼蒲团（kind=cushion，1×1）：复用现有蒲团原画，但裁到蒲团本体。
        /// 原图四周留白很大，若沿用恢复点的整幅方形裁切，缩进 1×1 格后蒲团只剩半格大小。
        /// 只换 Sprite 选择，不改占地、交互范围与碰撞。
        /// </summary>
        static Sprite CultivateCushionSprite()
        {
            const string key = "ART-ROOM-009_recovery_cushion_v01#cultivate";
            if (Props.TryGetValue(key, out var cached)) return cached;
            var texture = Resources.Load<Texture2D>("ART-ROOM-009_recovery_cushion_v01");
            if (texture == null)
            {
                Debug.LogWarning("[StepAArt] Missing cushion texture for cultivate cushion.");
                return null;
            }
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            var side = Mathf.Min(texture.width * .48f, texture.height * .74f);
            var rect = new Rect(texture.width * .5f - side * .5f, texture.height * .515f - side * .5f, side, side);
            var sprite = Sprite.Create(texture, rect, new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
            Props.Add(key, sprite);
            return sprite;
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
            texture.filterMode = texture.mipmapCount > 1 ? FilterMode.Trilinear : FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            var squareRecovery = name == "ART-ROOM-009_recovery_cushion_v01";
            var rect = squareRecovery
                ? new Rect((texture.width - texture.height) * .5f, 0f, texture.height, texture.height)
                : new Rect(0, 0, texture.width, texture.height);
            sprite = Sprite.Create(texture, rect,
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            Props.Add(name, sprite);
            return sprite;
        }

        /// <summary>Visual child only: original fit, colliders, interaction bounds and root position stay intact.</summary>
        public static bool ApplyObjectVisual(GameObject root, Sprite sprite, int visualQuarterTurns = 0,
            bool preserveAspect = false, bool alignBottom = false)
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
            var visualWidth = sideways ? sprite.bounds.size.y : sprite.bounds.size.x;
            var visualHeight = sideways ? sprite.bounds.size.x : sprite.bounds.size.y;
            var fit = preserveAspect
                ? Mathf.Min(bounds.size.x / Mathf.Max(.0001f, visualWidth),
                    bounds.size.y / Mathf.Max(.0001f, visualHeight)) : 0f;
            var targetWidth = preserveAspect ? visualWidth * fit : bounds.size.x;
            var targetHeight = preserveAspect ? visualHeight * fit : bounds.size.y;
            if (alignBottom)
                overlay.transform.position = new Vector3(bounds.center.x,
                    bounds.min.y + targetHeight * .5f, root.transform.position.z);
            overlay.transform.localScale = new Vector3(
                (sideways ? targetHeight : targetWidth) / Mathf.Max(0.0001f,
                    sprite.bounds.size.x * Mathf.Abs(sideways ? root.transform.lossyScale.y : root.transform.lossyScale.x)),
                (sideways ? targetWidth : targetHeight) / Mathf.Max(0.0001f,
                    sprite.bounds.size.y * Mathf.Abs(sideways ? root.transform.lossyScale.x : root.transform.lossyScale.y)), 1f);
            for (var i = 0; i < renderers.Length; i++)
                if (renderers[i] != null && renderers[i] != overlay)
                    renderers[i].enabled = false;
            return true;
        }
    }
}
