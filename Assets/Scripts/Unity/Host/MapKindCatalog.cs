namespace XianXia.Unity.Host
{
    /// <summary>
    /// MapEditor kind → Host prefab／默认占地。
    /// 分区（zone*）仅标记，无寻路／交互；物件与地表才参与玩法。
    /// </summary>
    public static class MapKindCatalog
    {
        public const int HouseFootprint = 20;
        public const int RoadFootprint = 1;
        public const int DefaultWallLen = 6;
        public const int DefaultWallThickness = 1;

        public const string Grass = "Assets/Prefabs/Environment/Tiles/GrassTile.prefab";
        public const string Herb = "Assets/Prefabs/Environment/Tiles/HerbPatchTile.prefab";
        public const string Farm = "Assets/Prefabs/Environment/Tiles/FarmlandTile.prefab";
        public const string Forest = "Assets/Prefabs/Environment/Tiles/ForestFloorTile.prefab";
        public const string Spirit = "Assets/Prefabs/Environment/Tiles/SpiritSiteTile.prefab";
        public const string Road = "Assets/Prefabs/Environment/Tiles/DirtRoadTile.prefab";
        public const string Wall = "Assets/Prefabs/Environment/Tiles/WallTile.prefab";
        public const string Rock = "Assets/Prefabs/Environment/Tiles/RockTile.prefab";
        public const string House = "Assets/Prefabs/Environment/Buildings/SmallHouse.prefab";
        public const string HouseFallback = "Assets/Prefabs/Environment/Buildings/CommonHouse.prefab";
        public const string Hub = "Assets/Prefabs/Environment/Buildings/SupervisorHouse.prefab";
        public const string Mine = "Assets/Prefabs/Environment/Buildings/Warehouse.prefab";
        public const string Cave = "Assets/Prefabs/Environment/Buildings/Warehouse.prefab";
        public const string TreeS = "Assets/Prefabs/Environment/Props/TreeSmall.prefab";
        public const string TreeM = "Assets/Prefabs/Environment/Props/TreeMid.prefab";
        public const string TreeL = "Assets/Prefabs/Environment/Props/TreeLarge.prefab";
        public const string Ore = "Assets/Prefabs/Environment/Props/OrePile.prefab";
        public const string Cushion = "Assets/Prefabs/Environment/Props/Cushion.prefab";
        public const string MissingPrefab = "Assets/Prefabs/Environment/Tiles/MissingPrefabPlaceholder.prefab";

        public enum StampMode
        {
            /// <summary>矩形内每一格一个 1×1 prefab（药田／农田／路／墙等）。</summary>
            PerCell = 0,
            /// <summary>矩形中心一个大体量 prefab（房子／树／矿石等）。</summary>
            SingleCentered = 1,
            /// <summary>半透明分区色块，无交互、不挡路（除非数据里手动勾 blocksMovement）。</summary>
            ZoneOverlay = 2
        }

        public readonly struct KindInfo
        {
            public KindInfo(
                string kind,
                string prefabPath,
                StampMode mode,
                int defaultW,
                int defaultH,
                bool plantable,
                HostInteractSpotKind? interactKind,
                UnityEngine.Color fallbackColor)
            {
                Kind = kind;
                PrefabPath = prefabPath;
                Mode = mode;
                DefaultW = defaultW;
                DefaultH = defaultH;
                Plantable = plantable;
                InteractKind = interactKind;
                FallbackColor = fallbackColor;
            }

            public string Kind { get; }
            public string PrefabPath { get; }
            public StampMode Mode { get; }
            public int DefaultW { get; }
            public int DefaultH { get; }
            public bool Plantable { get; }
            public HostInteractSpotKind? InteractKind { get; }
            public UnityEngine.Color FallbackColor { get; }
            public bool IsZone => Mode == StampMode.ZoneOverlay;
        }

        public static readonly KindInfo[] All =
        {
            // —— 分区（仅标记）——
            new("zoneHerb", Herb, StampMode.ZoneOverlay, 12, 12, false, null,
                new UnityEngine.Color(0.30f, 0.70f, 0.40f, 0.35f)),
            new("zoneGrain", Farm, StampMode.ZoneOverlay, 16, 12, false, null,
                new UnityEngine.Color(0.78f, 0.72f, 0.28f, 0.35f)),
            new("zoneHousing", House, StampMode.ZoneOverlay, 20, 20, false, null,
                new UnityEngine.Color(0.70f, 0.55f, 0.40f, 0.30f)),
            new("zoneForest", Forest, StampMode.ZoneOverlay, 14, 12, false, null,
                new UnityEngine.Color(0.20f, 0.50f, 0.28f, 0.35f)),
            new("zoneMine", Rock, StampMode.ZoneOverlay, 10, 8, false, null,
                new UnityEngine.Color(0.50f, 0.45f, 0.38f, 0.35f)),
            new("zoneSpring", Spirit, StampMode.ZoneOverlay, 8, 8, false, null,
                new UnityEngine.Color(0.35f, 0.70f, 0.85f, 0.35f)),

            // 旧 forest／mine／spring：兼容已有 JSON
            new("forest", Forest, StampMode.ZoneOverlay, 14, 12, false, null,
                new UnityEngine.Color(0.20f, 0.50f, 0.28f, 0.35f)),
            new("mine", Ore, StampMode.SingleCentered, 2, 2, false, HostInteractSpotKind.Work,
                new UnityEngine.Color(0.55f, 0.48f, 0.35f)),
            new("spring", Spirit, StampMode.ZoneOverlay, 8, 8, false, null,
                new UnityEngine.Color(0.35f, 0.70f, 0.85f, 0.35f)),

            // —— 地表 ——
            new("herbField", Herb, StampMode.PerCell, 12, 12, true, HostInteractSpotKind.Work,
                new UnityEngine.Color(0.30f, 0.62f, 0.38f)),
            new("grainField", Farm, StampMode.PerCell, 16, 12, true, HostInteractSpotKind.Work,
                new UnityEngine.Color(0.72f, 0.66f, 0.28f)),
            new("road", Road, StampMode.PerCell, RoadFootprint, RoadFootprint, false, null,
                new UnityEngine.Color(0.55f, 0.45f, 0.32f)),
            new("wall", Wall, StampMode.PerCell, DefaultWallLen, DefaultWallThickness, false, null,
                new UnityEngine.Color(0.35f, 0.35f, 0.38f)),

            // —— 物件 ——
            new("treeS", TreeS, StampMode.SingleCentered, 1, 1, false, HostInteractSpotKind.Work,
                new UnityEngine.Color(0.18f, 0.48f, 0.22f)),
            new("treeM", TreeM, StampMode.SingleCentered, 2, 2, false, HostInteractSpotKind.Work,
                new UnityEngine.Color(0.15f, 0.42f, 0.20f)),
            new("treeL", TreeL, StampMode.SingleCentered, 3, 3, false, HostInteractSpotKind.Work,
                new UnityEngine.Color(0.12f, 0.36f, 0.18f)),
            new("ore", Ore, StampMode.SingleCentered, 2, 2, false, HostInteractSpotKind.Work,
                new UnityEngine.Color(0.55f, 0.48f, 0.35f)),
            new("cushion", Cushion, StampMode.SingleCentered, 1, 1, false, HostInteractSpotKind.Cultivate,
                new UnityEngine.Color(0.55f, 0.45f, 0.70f)),
            new("rock", Rock, StampMode.PerCell, 4, 4, false, null,
                new UnityEngine.Color(0.45f, 0.45f, 0.48f)),

            // —— 其它建筑（保留）——
            new("house", House, StampMode.SingleCentered, HouseFootprint, HouseFootprint, false, null,
                new UnityEngine.Color(0.55f, 0.40f, 0.28f)),
            new("cave", Cave, StampMode.SingleCentered, 10, 8, true, HostInteractSpotKind.Cultivate,
                new UnityEngine.Color(0.42f, 0.36f, 0.40f)),
            new("roadHub", Hub, StampMode.SingleCentered, 8, 8, false, null,
                new UnityEngine.Color(0.58f, 0.50f, 0.40f)),
        };

        public static bool TryGet(string kind, out KindInfo info)
        {
            info = default;
            if (string.IsNullOrEmpty(kind))
                return false;
            for (var i = 0; i < All.Length; i++)
            {
                if (All[i].Kind != kind)
                    continue;
                info = All[i];
                return true;
            }

            return false;
        }
    }
}
