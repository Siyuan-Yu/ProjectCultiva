using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using XianXia.Unity.CameraControl;
using XianXia.Unity.Cultivation;
using XianXia.Unity.Input;
using XianXia.Unity.Obligation;
using XianXia.Unity.Presentation;
using XianXia.Unity.Resources;
using XianXia.Unity.Tasks;
using XianXia.Unity.Time;
using XianXia.Unity.UI;
using XianXia.Unity.World;

namespace XianXia.Unity.Editor
{
    [InitializeOnLoad]
    public static class DemoPrototypeBuilder
    {
        private const string ScenePath = "Assets/Scenes/Demo_v0_1.unity";
        private const string ScheduleConfigPath = "Assets/Configs/Schedules/DaySchedule_Laborer.asset";
        private const string DailyTaskConfigPath = "Assets/Configs/Tasks/DailyTasks_Supervisor.asset";
        private const string AngerConfigPath = "Assets/Configs/Obligation/SupervisorAnger_Default.asset";
        private const string CultivationConfigPath = "Assets/Configs/Cultivation/Cultivation_Default.asset";
        private const int MapWidth = 80;
        private const int MapHeight = 50;
        private const int MapMinX = -40;
        private const int MapMinY = -25;
        private const float CharacterVisualScale = 0.6f;
        private const float CharacterMoveSpeed = 1.5f;

        static DemoPrototypeBuilder()
        {
            EditorApplication.delayCall += BuildOnFirstEditorOpen;
        }

        private sealed class PlaceholderSpec
        {
            public string Path;
            public int Width;
            public int Height;
            public Color32 Color;
            public PlaceholderKind Kind;
            public bool BottomPivot;
        }

        private enum PlaceholderKind
        {
            Tile,
            Character,
            Building,
            Ring
        }

        [MenuItem("XianXia/Build Demo v0.1 Prototype")]
        public static void BuildFromMenu()
        {
            BuildPrototype();
        }

        private static void BuildOnFirstEditorOpen()
        {
            if (Application.isBatchMode
                || EditorApplication.isCompiling
                || EditorApplication.isUpdating
                || File.Exists(ScenePath))
            {
                return;
            }

            BuildPrototype();
            SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Selection.activeObject = scene;
            Debug.Log("Demo scene was generated automatically on first editor open.");
        }

        public static void BuildPrototype()
        {
            EnsureFolders();
            CreatePlaceholderSprites();
            CreateScheduleConfig();
            CreateDailyLifeConfigs();
            CreatePrefabs();
            CreateDemoScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Demo v0.1 prototype created: {ScenePath}");
        }

        private static void EnsureFolders()
        {
            string[] folders =
            {
                "Assets/Scenes",
                "Assets/Configs/Schedules",
                "Assets/Configs/Tasks",
                "Assets/Configs/Obligation",
                "Assets/Configs/Cultivation",
                "Assets/Prefabs/Characters/Players",
                "Assets/Prefabs/Characters/NPCs",
                "Assets/Prefabs/Environment/Tiles",
                "Assets/Prefabs/Environment/Buildings",
                "Assets/Prefabs/UI"
            };

            foreach (string folder in folders)
            {
                Directory.CreateDirectory(folder);
            }
        }

        private static IReadOnlyList<PlaceholderSpec> Specs => new[]
        {
            new PlaceholderSpec { Path = "Assets/Art/Environment/Tiles/Ground/TILE_Ground_Grass_01.png", Width = 32, Height = 32, Color = new Color32(91, 117, 72, 255), Kind = PlaceholderKind.Tile },
            new PlaceholderSpec { Path = "Assets/Art/Environment/Tiles/Ground/TILE_Ground_DirtRoad_01.png", Width = 32, Height = 32, Color = new Color32(137, 105, 70, 255), Kind = PlaceholderKind.Tile },
            new PlaceholderSpec { Path = "Assets/Art/Environment/Tiles/Farmland/TILE_Ground_Farmland_01.png", Width = 32, Height = 32, Color = new Color32(111, 78, 55, 255), Kind = PlaceholderKind.Tile },
            new PlaceholderSpec { Path = "Assets/Art/Environment/Tiles/Forest/TILE_Ground_ForestFloor_01.png", Width = 32, Height = 32, Color = new Color32(51, 78, 55, 255), Kind = PlaceholderKind.Tile },
            new PlaceholderSpec { Path = "Assets/Art/Environment/Tiles/Plants/TILE_Ground_HerbPatch_01.png", Width = 32, Height = 32, Color = new Color32(65, 112, 82, 255), Kind = PlaceholderKind.Tile },
            new PlaceholderSpec { Path = "Assets/Art/Environment/Tiles/SpiritSite/TILE_Ground_SpiritSite_01.png", Width = 32, Height = 32, Color = new Color32(54, 130, 128, 255), Kind = PlaceholderKind.Tile },

            new PlaceholderSpec { Path = "Assets/Art/Characters/Players/Player_A/CHR_PlayerA_Idle_Down.png", Width = 64, Height = 64, Color = new Color32(113, 78, 53, 255), Kind = PlaceholderKind.Character, BottomPivot = true },
            new PlaceholderSpec { Path = "Assets/Art/Characters/Players/Player_B/CHR_PlayerB_Idle_Down.png", Width = 64, Height = 64, Color = new Color32(104, 121, 95, 255), Kind = PlaceholderKind.Character, BottomPivot = true },
            new PlaceholderSpec { Path = "Assets/Art/Characters/Players/Player_C/CHR_PlayerC_Idle_Down.png", Width = 64, Height = 64, Color = new Color32(92, 84, 111, 255), Kind = PlaceholderKind.Character, BottomPivot = true },
            new PlaceholderSpec { Path = "Assets/Art/Characters/NPCs/Supervisor/CHR_NPC_Supervisor_Idle_Down.png", Width = 64, Height = 64, Color = new Color32(35, 78, 82, 255), Kind = PlaceholderKind.Character, BottomPivot = true },
            new PlaceholderSpec { Path = "Assets/Art/Characters/NPCs/Merchant/CHR_NPC_Merchant_Idle_Down.png", Width = 64, Height = 64, Color = new Color32(151, 111, 55, 255), Kind = PlaceholderKind.Character, BottomPivot = true },
            new PlaceholderSpec { Path = "Assets/Art/Characters/NPCs/Guard/CHR_NPC_Guard_Idle_Down.png", Width = 64, Height = 64, Color = new Color32(104, 51, 48, 255), Kind = PlaceholderKind.Character, BottomPivot = true },

            new PlaceholderSpec { Path = "Assets/Art/Environment/Buildings/Houses/BLD_House_Common_01.png", Width = 128, Height = 160, Color = new Color32(125, 88, 57, 255), Kind = PlaceholderKind.Building, BottomPivot = true },
            new PlaceholderSpec { Path = "Assets/Art/Environment/Buildings/SupervisorHouse/BLD_SupervisorHouse_01.png", Width = 192, Height = 160, Color = new Color32(55, 73, 73, 255), Kind = PlaceholderKind.Building, BottomPivot = true },
            new PlaceholderSpec { Path = "Assets/Art/Environment/Buildings/Warehouse/BLD_Warehouse_01.png", Width = 128, Height = 128, Color = new Color32(107, 92, 68, 255), Kind = PlaceholderKind.Building, BottomPivot = true },

            new PlaceholderSpec { Path = "Assets/Art/UI/HUD/UI_SelectRing_Player.png", Width = 64, Height = 64, Color = new Color32(100, 220, 130, 255), Kind = PlaceholderKind.Ring }
        };

        private static void CreateScheduleConfig()
        {
            DayScheduleConfig existing = AssetDatabase.LoadAssetAtPath<DayScheduleConfig>(ScheduleConfigPath);
            if (existing != null)
            {
                return;
            }

            DayScheduleConfig config = DayScheduleConfig.CreateDefaultLaborer();
            AssetDatabase.CreateAsset(config, ScheduleConfigPath);
        }

        private static void CreateDailyLifeConfigs()
        {
            DailyTaskConfig taskConfig = AssetDatabase.LoadAssetAtPath<DailyTaskConfig>(DailyTaskConfigPath);
            if (taskConfig == null)
            {
                taskConfig = DailyTaskConfig.CreateDefaultSupervisorTasks();
                AssetDatabase.CreateAsset(taskConfig, DailyTaskConfigPath);
            }

            SupervisorAngerConfig angerConfig = AssetDatabase.LoadAssetAtPath<SupervisorAngerConfig>(AngerConfigPath);
            if (angerConfig == null)
            {
                angerConfig = SupervisorAngerConfig.CreateDefault();
                AssetDatabase.CreateAsset(angerConfig, AngerConfigPath);
            }

            CultivationConfig cultivationConfig =
                AssetDatabase.LoadAssetAtPath<CultivationConfig>(CultivationConfigPath);
            if (cultivationConfig == null)
            {
                cultivationConfig = CultivationConfig.CreateDefault();
                AssetDatabase.CreateAsset(cultivationConfig, CultivationConfigPath);
            }
        }

        private static void CreatePlaceholderSprites()
        {
            foreach (PlaceholderSpec spec in Specs)
            {
                if (!File.Exists(spec.Path))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(spec.Path) ?? "Assets");
                    WritePlaceholderPng(spec);
                }

                ConfigureSpriteImporter(spec);
            }
        }

        private static void WritePlaceholderPng(PlaceholderSpec spec)
        {
            Texture2D texture = new(spec.Width, spec.Height, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[spec.Width * spec.Height];
            Color32 clear = new(0, 0, 0, 0);
            Array.Fill(pixels, spec.Kind == PlaceholderKind.Tile ? spec.Color : clear);
            texture.SetPixels32(pixels);

            switch (spec.Kind)
            {
                case PlaceholderKind.Tile:
                    DrawTile(texture, spec.Color);
                    break;
                case PlaceholderKind.Character:
                    DrawCharacter(texture, spec.Color);
                    break;
                case PlaceholderKind.Building:
                    DrawBuilding(texture, spec.Color);
                    break;
                case PlaceholderKind.Ring:
                    DrawRing(texture, spec.Color);
                    break;
            }

            texture.Apply();
            File.WriteAllBytes(spec.Path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        private static void DrawTile(Texture2D texture, Color32 baseColor)
        {
            Color32 accent = Shade(baseColor, 15);
            for (int y = 0; y < texture.height; y += 8)
            {
                for (int x = (y / 8) % 2 * 4; x < texture.width; x += 8)
                {
                    SetPixelSafe(texture, x, y, accent);
                    SetPixelSafe(texture, x + 1, y, accent);
                }
            }
        }

        private static void DrawCharacter(Texture2D texture, Color32 baseColor)
        {
            Color32 outline = new(39, 35, 32, 255);
            Color32 skin = new(204, 165, 124, 255);
            FillRect(texture, 22, 5, 8, 17, outline);
            FillRect(texture, 34, 5, 8, 17, outline);
            FillRect(texture, 18, 19, 28, 27, outline);
            FillRect(texture, 21, 22, 22, 22, baseColor);
            FillRect(texture, 23, 44, 18, 15, outline);
            FillRect(texture, 25, 46, 14, 11, skin);
            FillRect(texture, 24, 55, 16, 4, Shade(baseColor, -35));
        }

        private static void DrawBuilding(Texture2D texture, Color32 baseColor)
        {
            Color32 outline = new(48, 40, 34, 255);
            Color32 roof = Shade(baseColor, -30);
            FillRect(texture, 6, 4, texture.width - 12, texture.height - 54, outline);
            FillRect(texture, 10, 8, texture.width - 20, texture.height - 62, baseColor);
            FillRect(texture, 2, texture.height - 58, texture.width - 4, 50, outline);
            FillRect(texture, 6, texture.height - 54, texture.width - 12, 42, roof);
            FillRect(texture, texture.width / 2 - 10, 5, 20, 34, Shade(baseColor, -45));
        }

        private static void DrawRing(Texture2D texture, Color32 color)
        {
            float centerX = (texture.width - 1) * 0.5f;
            float centerY = (texture.height - 1) * 0.5f;
            float radiusX = texture.width * 0.40f;
            float radiusY = texture.height * 0.22f;

            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    float dx = (x - centerX) / radiusX;
                    float dy = (y - centerY) / radiusY;
                    float distance = dx * dx + dy * dy;
                    if (distance > 0.78f && distance < 1.18f)
                    {
                        texture.SetPixel(x, y, color);
                    }
                }
            }
        }

        private static void FillRect(Texture2D texture, int x, int y, int width, int height, Color32 color)
        {
            for (int py = y; py < y + height; py++)
            {
                for (int px = x; px < x + width; px++)
                {
                    SetPixelSafe(texture, px, py, color);
                }
            }
        }

        private static void SetPixelSafe(Texture2D texture, int x, int y, Color32 color)
        {
            if (x >= 0 && x < texture.width && y >= 0 && y < texture.height)
            {
                texture.SetPixel(x, y, color);
            }
        }

        private static Color32 Shade(Color32 color, int amount)
        {
            return new Color32(
                (byte)Mathf.Clamp(color.r + amount, 0, 255),
                (byte)Mathf.Clamp(color.g + amount, 0, 255),
                (byte)Mathf.Clamp(color.b + amount, 0, 255),
                color.a);
        }

        private static void ConfigureSpriteImporter(PlaceholderSpec spec)
        {
            AssetDatabase.ImportAsset(spec.Path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(spec.Path) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"Cannot configure sprite importer: {spec.Path}");
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32f;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = spec.Kind == PlaceholderKind.Tile ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;

            TextureImporterSettings settings = new();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = spec.BottomPivot ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 0.5f);
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        private static void CreatePrefabs()
        {
            Sprite ring = LoadSprite("Assets/Art/UI/HUD/UI_SelectRing_Player.png");

            CreateUnitPrefab("Player_A", "Assets/Art/Characters/Players/Player_A/CHR_PlayerA_Idle_Down.png", "Assets/Prefabs/Characters/Players/Player_A.prefab", ring);
            CreateUnitPrefab("Player_B", "Assets/Art/Characters/Players/Player_B/CHR_PlayerB_Idle_Down.png", "Assets/Prefabs/Characters/Players/Player_B.prefab", ring);
            CreateUnitPrefab("Player_C", "Assets/Art/Characters/Players/Player_C/CHR_PlayerC_Idle_Down.png", "Assets/Prefabs/Characters/Players/Player_C.prefab", ring);

            CreateCharacterPrefab("Supervisor", "Assets/Art/Characters/NPCs/Supervisor/CHR_NPC_Supervisor_Idle_Down.png", "Assets/Prefabs/Characters/NPCs/Supervisor.prefab");
            CreateCharacterPrefab("Merchant", "Assets/Art/Characters/NPCs/Merchant/CHR_NPC_Merchant_Idle_Down.png", "Assets/Prefabs/Characters/NPCs/Merchant.prefab");
            CreateCharacterPrefab("Guard", "Assets/Art/Characters/NPCs/Guard/CHR_NPC_Guard_Idle_Down.png", "Assets/Prefabs/Characters/NPCs/Guard.prefab");

            CreateStaticPrefab("GrassTile", "Assets/Art/Environment/Tiles/Ground/TILE_Ground_Grass_01.png", "Assets/Prefabs/Environment/Tiles/GrassTile.prefab", false, -1000);
            CreateStaticPrefab("DirtRoadTile", "Assets/Art/Environment/Tiles/Ground/TILE_Ground_DirtRoad_01.png", "Assets/Prefabs/Environment/Tiles/DirtRoadTile.prefab", false, -1000);
            CreateStaticPrefab("FarmlandTile", "Assets/Art/Environment/Tiles/Farmland/TILE_Ground_Farmland_01.png", "Assets/Prefabs/Environment/Tiles/FarmlandTile.prefab", false, -1000);
            CreateStaticPrefab("ForestFloorTile", "Assets/Art/Environment/Tiles/Forest/TILE_Ground_ForestFloor_01.png", "Assets/Prefabs/Environment/Tiles/ForestFloorTile.prefab", false, -1000);
            CreateStaticPrefab("HerbPatchTile", "Assets/Art/Environment/Tiles/Plants/TILE_Ground_HerbPatch_01.png", "Assets/Prefabs/Environment/Tiles/HerbPatchTile.prefab", false, -1000);
            CreateStaticPrefab("SpiritSiteTile", "Assets/Art/Environment/Tiles/SpiritSite/TILE_Ground_SpiritSite_01.png", "Assets/Prefabs/Environment/Tiles/SpiritSiteTile.prefab", false, -1000);

            CreateStaticPrefab("CommonHouse", "Assets/Art/Environment/Buildings/Houses/BLD_House_Common_01.png", "Assets/Prefabs/Environment/Buildings/CommonHouse.prefab", true, 0);
            CreateStaticPrefab("SupervisorHouse", "Assets/Art/Environment/Buildings/SupervisorHouse/BLD_SupervisorHouse_01.png", "Assets/Prefabs/Environment/Buildings/SupervisorHouse.prefab", true, 0);
            CreateStaticPrefab("Warehouse", "Assets/Art/Environment/Buildings/Warehouse/BLD_Warehouse_01.png", "Assets/Prefabs/Environment/Buildings/Warehouse.prefab", true, 0);

            CreateStaticPrefab("PlayerSelectionRing", "Assets/Art/UI/HUD/UI_SelectRing_Player.png", "Assets/Prefabs/UI/PlayerSelectionRing.prefab", false, -1);
        }

        private static void CreateUnitPrefab(string name, string spritePath, string prefabPath, Sprite ringSprite)
        {
            GameObject root = new(name);

            GameObject visualObject = new("Visual");
            visualObject.transform.SetParent(root.transform, false);
            SpriteRenderer visual = visualObject.AddComponent<SpriteRenderer>();
            Sprite sprite = LoadSprite(spritePath);
            visualObject.AddComponent<ReplaceableSprite>().Configure(Path.GetFileNameWithoutExtension(spritePath), visual, sprite);
            visualObject.transform.localScale = Vector3.one * CharacterVisualScale;

            GameObject ringObject = new("SelectionRing");
            ringObject.transform.SetParent(root.transform, false);
            SpriteRenderer ring = ringObject.AddComponent<SpriteRenderer>();
            ring.sprite = ringSprite;
            ring.sortingOrder = -1;

            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.75f, 1.0f);
            collider.offset = new Vector2(0f, 0.5f);

            DemoUnitController unit = root.AddComponent<DemoUnitController>();
            unit.Configure(visualObject.transform, visual, ring, collider, CharacterVisualScale, CharacterMoveSpeed);
            root.AddComponent<UnitCultivation>().Configure(0f, 0f);

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void CreateCharacterPrefab(string name, string spritePath, string prefabPath)
        {
            GameObject root = new(name);
            GameObject visualObject = new("Visual");
            visualObject.transform.SetParent(root.transform, false);
            SpriteRenderer renderer = visualObject.AddComponent<SpriteRenderer>();
            Sprite sprite = LoadSprite(spritePath);
            visualObject.AddComponent<ReplaceableSprite>().Configure(Path.GetFileNameWithoutExtension(spritePath), renderer, sprite);
            visualObject.transform.localScale = Vector3.one * CharacterVisualScale;

            BoxCollider2D box = root.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.75f, 1.0f);
            box.offset = new Vector2(0f, 0.5f);

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void CreateStaticPrefab(string name, string spritePath, string prefabPath, bool collider, int sortingOrder)
        {
            GameObject root = new(name);
            GameObject visualObject = new("Visual");
            visualObject.transform.SetParent(root.transform, false);
            SpriteRenderer renderer = visualObject.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = sortingOrder;
            Sprite sprite = LoadSprite(spritePath);
            visualObject.AddComponent<ReplaceableSprite>().Configure(Path.GetFileNameWithoutExtension(spritePath), renderer, sprite);

            if (collider)
            {
                BoxCollider2D box = root.AddComponent<BoxCollider2D>();
                Bounds bounds = sprite.bounds;
                box.size = new Vector2(Mathf.Max(0.5f, bounds.size.x * 0.8f), Mathf.Max(0.5f, bounds.size.y * 0.55f));
                box.offset = new Vector2(0f, box.size.y * 0.5f);
            }

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static Sprite LoadSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                throw new InvalidOperationException($"Sprite not found: {path}");
            }

            return sprite;
        }

        private static void CreateDemoScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject environment = new("Environment");
            GameObject ground = new("Ground");
            ground.transform.SetParent(environment.transform);
            PlaceGround(ground.transform);

            GameObject buildings = new("Buildings");
            buildings.transform.SetParent(environment.transform);
            PlacePrefab("Assets/Prefabs/Environment/Buildings/CommonHouse.prefab", new Vector3(-18f, 10f), buildings.transform, "House_01");
            PlacePrefab("Assets/Prefabs/Environment/Buildings/CommonHouse.prefab", new Vector3(-12f, 12f), buildings.transform, "House_02");
            PlacePrefab("Assets/Prefabs/Environment/Buildings/CommonHouse.prefab", new Vector3(-8f, 8f), buildings.transform, "House_03");
            PlacePrefab("Assets/Prefabs/Environment/Buildings/CommonHouse.prefab", new Vector3(-16f, 6f), buildings.transform, "House_04");
            PlacePrefab("Assets/Prefabs/Environment/Buildings/SupervisorHouse.prefab", new Vector3(18f, 10f), buildings.transform, "SupervisorHouse");
            PlacePrefab("Assets/Prefabs/Environment/Buildings/Warehouse.prefab", new Vector3(12f, 5f), buildings.transform, "Warehouse");

            GameObject zones = new("Zones");
            zones.transform.SetParent(environment.transform);
            WorkZone forestZone = CreateWorkZone(
                zones.transform,
                "WorkZone_Forest",
                "森林采集区",
                "forest_wood",
                ResourceType.Wood,
                6f,
                new Vector2(-34f, 0f),
                new Vector2(12f, 40f));
            WorkZone herbZone = CreateWorkZone(
                zones.transform,
                "WorkZone_Herb",
                "草药区",
                "herb_patch",
                ResourceType.Herb,
                2f,
                new Vector2(-3f, -15f),
                new Vector2(14f, 10f));
            WorkZone farmZone = CreateWorkZone(
                zones.transform,
                "WorkZone_Farm",
                "农田",
                "farmland_food",
                ResourceType.Food,
                4f,
                new Vector2(20f, -12f),
                new Vector2(24f, 16f));
            WorkZone[] workZones = { forestZone, herbZone, farmZone };
            SpiritSiteZone spiritSite = CreateSpiritSiteZone(
                zones.transform,
                "SpiritSite_Hidden",
                "隐藏灵地",
                "hidden_spirit_site",
                1.5f,
                new Vector2(32f, -17.5f),
                new Vector2(16f, 15f));

            GameObject characters = new("Characters");
            GameObject party = new("PlayerParty");
            party.transform.SetParent(characters.transform);
            GameObject playerA = PlacePrefab("Assets/Prefabs/Characters/Players/Player_A.prefab", new Vector3(-6f, 0f), party.transform, "Player_A");
            GameObject playerB = PlacePrefab("Assets/Prefabs/Characters/Players/Player_B.prefab", new Vector3(-4f, 0f), party.transform, "Player_B");
            GameObject playerC = PlacePrefab("Assets/Prefabs/Characters/Players/Player_C.prefab", new Vector3(-2f, 0f), party.transform, "Player_C");

            GameObject npcs = new("NPCs");
            npcs.transform.SetParent(characters.transform);
            GameObject supervisor = PlacePrefab(
                "Assets/Prefabs/Characters/NPCs/Supervisor.prefab",
                new Vector3(18f, 7f),
                npcs.transform,
                "Supervisor");
            PlacePrefab("Assets/Prefabs/Characters/NPCs/Merchant.prefab", new Vector3(0f, 4f), npcs.transform, "Merchant");
            PlacePrefab("Assets/Prefabs/Characters/NPCs/Guard.prefab", new Vector3(14f, 8f), npcs.transform, "Guard_01");
            PlacePrefab("Assets/Prefabs/Characters/NPCs/Guard.prefab", new Vector3(22f, 8f), npcs.transform, "Guard_02");

            GameObject systems = new("Systems");
            WorldBounds worldBounds = systems.AddComponent<WorldBounds>();
            worldBounds.Configure(new Rect(MapMinX, MapMinY, MapWidth, MapHeight));

            GameClock clock = systems.AddComponent<GameClock>();
            clock.Configure(8f, 1f, 1, 6, 0);

            DayScheduleConfig scheduleConfig = AssetDatabase.LoadAssetAtPath<DayScheduleConfig>(ScheduleConfigPath);

            DemoUnitController[] partyUnits =
            {
                playerA.GetComponent<DemoUnitController>(),
                playerB.GetComponent<DemoUnitController>(),
                playerC.GetComponent<DemoUnitController>()
            };

            ScheduleService scheduleService = systems.AddComponent<ScheduleService>();
            scheduleService.Configure(clock, scheduleConfig, partyUnits, enableTestingEdit: true);

            WorldTileAmbientGrid ambientGrid = systems.AddComponent<WorldTileAmbientGrid>();
            ambientGrid.Configure(MapMinX, MapMinY, MapWidth, MapHeight, 20260731);

            ResourceInventory inventory = systems.AddComponent<ResourceInventory>();
            inventory.ConfigureStartingAmounts(0, 0, 0, 3);
            WorkSystem workSystem = systems.AddComponent<WorkSystem>();
            workSystem.Configure(clock, inventory, partyUnits, workZones);

            ScheduleComplianceTracker tracker = systems.AddComponent<ScheduleComplianceTracker>();
            tracker.Configure(scheduleService, workSystem, partyUnits);

            SupervisorAngerConfig angerConfig =
                AssetDatabase.LoadAssetAtPath<SupervisorAngerConfig>(AngerConfigPath);
            SupervisorAngerSystem angerSystem = systems.AddComponent<SupervisorAngerSystem>();
            angerSystem.Configure(clock, scheduleService, tracker, angerConfig);

            DailyTaskConfig dailyTaskConfig =
                AssetDatabase.LoadAssetAtPath<DailyTaskConfig>(DailyTaskConfigPath);
            DailyTaskSystem dailyTaskSystem = systems.AddComponent<DailyTaskSystem>();
            dailyTaskSystem.Configure(clock, inventory, angerSystem, dailyTaskConfig, angerConfig);

            CultivationConfig cultivationConfig =
                AssetDatabase.LoadAssetAtPath<CultivationConfig>(CultivationConfigPath);
            CultivationSystem cultivationSystem = systems.AddComponent<CultivationSystem>();
            cultivationSystem.Configure(
                clock,
                inventory,
                spiritSite,
                cultivationConfig,
                supervisor.transform,
                partyUnits);

            Camera camera = CreateCamera(new Vector3(-4f, 2f, -10f));
            CameraController cameraController = camera.gameObject.AddComponent<CameraController>();
            cameraController.Configure(camera, worldBounds, 12f);

            TileHoverProbe hoverProbe = systems.AddComponent<TileHoverProbe>();
            hoverProbe.Configure(camera, ambientGrid);

            PartyCommandController input = systems.AddComponent<PartyCommandController>();
            SerializedObject inputObject = new(input);
            inputObject.FindProperty("worldCamera").objectReferenceValue = camera;
            inputObject.ApplyModifiedPropertiesWithoutUndo();

            DemoPrototypeHud hud = systems.AddComponent<DemoPrototypeHud>();
            hud.Configure(
                clock,
                scheduleService,
                tracker,
                partyUnits,
                inventory,
                dailyTaskSystem,
                angerSystem,
                cultivationSystem,
                input,
                hoverProbe);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static WorkZone CreateWorkZone(
            Transform parent,
            string objectName,
            string displayName,
            string zoneId,
            ResourceType resourceType,
            float unitsPerGameHour,
            Vector2 center,
            Vector2 size)
        {
            GameObject zoneObject = new(objectName);
            zoneObject.transform.SetParent(parent);
            zoneObject.AddComponent<BoxCollider2D>();
            WorkZone zone = zoneObject.AddComponent<WorkZone>();
            zone.Configure(
                zoneId,
                displayName,
                resourceType,
                unitsPerGameHour,
                center,
                size);
            return zone;
        }

        private static SpiritSiteZone CreateSpiritSiteZone(
            Transform parent,
            string objectName,
            string displayName,
            string zoneId,
            float concealGrassPerGameHour,
            Vector2 center,
            Vector2 size)
        {
            GameObject zoneObject = new(objectName);
            zoneObject.transform.SetParent(parent);
            zoneObject.AddComponent<BoxCollider2D>();
            SpiritSiteZone zone = zoneObject.AddComponent<SpiritSiteZone>();
            zone.Configure(zoneId, displayName, concealGrassPerGameHour, center, size);
            return zone;
        }

        private static void PlaceGround(Transform parent)
        {
            int maxX = MapMinX + MapWidth - 1;
            int maxY = MapMinY + MapHeight - 1;
            for (int y = MapMinY; y <= maxY; y++)
            {
                for (int x = MapMinX; x <= maxX; x++)
                {
                    string prefabPath = ChooseGroundPrefab(x, y);
                    PlacePrefab(prefabPath, new Vector3(x + 0.5f, y + 0.5f, 0f), parent, $"Tile_{x}_{y}");
                }
            }
        }

        private static string ChooseGroundPrefab(int x, int y)
        {
            // 隐藏灵地：东南角
            if (x >= 24 && y <= -10)
            {
                return "Assets/Prefabs/Environment/Tiles/SpiritSiteTile.prefab";
            }

            // 森林采集区：西侧宽带
            if (x <= -28)
            {
                return "Assets/Prefabs/Environment/Tiles/ForestFloorTile.prefab";
            }

            // 草药采集区：西南小块
            if (x >= -10 && x <= 3 && y >= -20 && y <= -11)
            {
                return "Assets/Prefabs/Environment/Tiles/HerbPatchTile.prefab";
            }

            // 工作区／农田：东南偏南
            if (x >= 8 && x <= 32 && y >= -20 && y <= -4)
            {
                return "Assets/Prefabs/Environment/Tiles/FarmlandTile.prefab";
            }

            // 连接各区的主干道
            bool horizontalRoad = Mathf.Abs(y) <= 1 || (y >= 8 && y <= 9 && x >= -20 && x <= 22);
            bool verticalRoad = Mathf.Abs(x) <= 1
                || (x >= 16 && x <= 17 && y >= -16 && y <= 12)
                || (x >= -14 && x <= -13 && y >= -2 && y <= 14);
            if (horizontalRoad || verticalRoad)
            {
                return "Assets/Prefabs/Environment/Tiles/DirtRoadTile.prefab";
            }

            return "Assets/Prefabs/Environment/Tiles/GrassTile.prefab";
        }

        private static GameObject PlacePrefab(string prefabPath, Vector3 position, Transform parent, string objectName)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException($"Prefab not found: {prefabPath}");
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = objectName;
            instance.transform.SetParent(parent);
            instance.transform.position = position;
            return instance;
        }

        private static Camera CreateCamera(Vector3 position)
        {
            GameObject cameraObject = new("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = position;
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 12f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.10f, 0.12f, 0.11f);
            return camera;
        }
    }
}
