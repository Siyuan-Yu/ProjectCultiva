using System;
using UnityEngine;

namespace XianXia.Unity.World
{
    [Serializable]
    public struct TileAmbientData
    {
        public int TileX;
        public int TileY;
        public float AttributeEnergy;
        public float SpiritQi;
        public bool IsDense;

        public string DensityLabel => IsDense ? "浓郁" : SpiritQi >= 40f ? "普通" : "稀薄";
    }

    /// <summary>
    /// 地图格环境数据。Demo 用确定性随机生成，悬停查看。
    /// </summary>
    public sealed class WorldTileAmbientGrid : MonoBehaviour
    {
        [SerializeField] private int mapMinX = -40;
        [SerializeField] private int mapMinY = -25;
        [SerializeField] private int mapWidth = 80;
        [SerializeField] private int mapHeight = 50;
        [SerializeField] private int seed = 20260731;
        [SerializeField] private float denseQiThreshold = 70f;

        private TileAmbientData[] _tiles;
        private bool _ready;

        public int MapMinX => mapMinX;
        public int MapMinY => mapMinY;
        public int MapWidth => mapWidth;
        public int MapHeight => mapHeight;

        public void Configure(int minX, int minY, int width, int height, int generationSeed)
        {
            mapMinX = minX;
            mapMinY = minY;
            mapWidth = width;
            mapHeight = height;
            seed = generationSeed;
            Generate();
        }

        private void Awake()
        {
            if (!_ready)
            {
                Generate();
            }
        }

        public void Generate()
        {
            int count = mapWidth * mapHeight;
            _tiles = new TileAmbientData[count];
            var rng = new System.Random(seed);

            for (int y = 0; y < mapHeight; y++)
            {
                for (int x = 0; x < mapWidth; x++)
                {
                    int tileX = mapMinX + x;
                    int tileY = mapMinY + y;
                    float bias = RegionQiBias(tileX, tileY);
                    float qi = Mathf.Clamp((float)(rng.NextDouble() * 55.0 + bias), 0f, 100f);
                    float energy = Mathf.Clamp((float)(rng.NextDouble() * 40.0 + bias * 0.6f), 0f, 100f);
                    _tiles[Index(x, y)] = new TileAmbientData
                    {
                        TileX = tileX,
                        TileY = tileY,
                        SpiritQi = qi,
                        AttributeEnergy = energy,
                        IsDense = qi >= denseQiThreshold
                    };
                }
            }

            _ready = true;
        }

        public bool TryGetAtWorld(Vector2 worldPosition, out TileAmbientData data)
        {
            int tileX = Mathf.FloorToInt(worldPosition.x);
            int tileY = Mathf.FloorToInt(worldPosition.y);
            return TryGetAtTile(tileX, tileY, out data);
        }

        public bool TryGetAtTile(int tileX, int tileY, out TileAmbientData data)
        {
            if (!_ready || _tiles == null)
            {
                data = default;
                return false;
            }

            int localX = tileX - mapMinX;
            int localY = tileY - mapMinY;
            if (localX < 0 || localY < 0 || localX >= mapWidth || localY >= mapHeight)
            {
                data = default;
                return false;
            }

            data = _tiles[Index(localX, localY)];
            return true;
        }

        private static float RegionQiBias(int tileX, int tileY)
        {
            // 隐藏灵地偏高；森林中等；农田偏低。
            if (tileX >= 24 && tileY <= -10)
            {
                return 55f;
            }

            if (tileX <= -28)
            {
                return 28f;
            }

            if (tileX >= 8 && tileX <= 32 && tileY >= -20 && tileY <= -4)
            {
                return 8f;
            }

            if (tileX >= -10 && tileX <= 3 && tileY >= -20 && tileY <= -11)
            {
                return 22f;
            }

            return 12f;
        }

        private int Index(int localX, int localY) => localY * mapWidth + localX;
    }
}
