namespace XianXia.Unity.Host
{
    /// <summary>
    /// MapEditor kind → Host prefab／默认占地。尺寸可再调。
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

        public enum StampMode
        {
            /// <summary>矩形内每一格一个 1×1 prefab（药田／农田／路／墙等）。</summary>
            PerCell = 0,
            /// <summary>矩形中心一个大体量 prefab（房子等）。</summary>
            SingleCentered = 1
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
        }

        public static readonly KindInfo[] All =
        {
            new("wall", Wall, StampMode.PerCell, DefaultWallLen, DefaultWallThickness, false, null,
                new UnityEngine.Color(0.35f, 0.35f, 0.38f)),
            new("rock", Rock, StampMode.PerCell, 4, 4, false, null,
                new UnityEngine.Color(0.45f, 0.45f, 0.48f)),
            new("house", House, StampMode.SingleCentered, HouseFootprint, HouseFootprint, false, null,
                new UnityEngine.Color(0.55f, 0.40f, 0.28f)),
            new("herbField", Herb, StampMode.PerCell, 12, 12, true, HostInteractSpotKind.Work,
                new UnityEngine.Color(0.30f, 0.62f, 0.38f)),
            new("grainField", Farm, StampMode.PerCell, 16, 12, true, HostInteractSpotKind.Work,
                new UnityEngine.Color(0.72f, 0.66f, 0.28f)),
            new("forest", Forest, StampMode.PerCell, 14, 12, true, HostInteractSpotKind.Work,
                new UnityEngine.Color(0.18f, 0.42f, 0.24f)),
            new("mine", Mine, StampMode.SingleCentered, 10, 8, true, HostInteractSpotKind.Work,
                new UnityEngine.Color(0.42f, 0.36f, 0.30f)),
            new("spring", Spirit, StampMode.PerCell, 8, 8, true, HostInteractSpotKind.Cultivate,
                new UnityEngine.Color(0.30f, 0.62f, 0.78f)),
            new("cave", Cave, StampMode.SingleCentered, 10, 8, true, HostInteractSpotKind.Cultivate,
                new UnityEngine.Color(0.42f, 0.36f, 0.40f)),
            new("road", Road, StampMode.PerCell, RoadFootprint, RoadFootprint, false, null,
                new UnityEngine.Color(0.55f, 0.45f, 0.32f)),
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
