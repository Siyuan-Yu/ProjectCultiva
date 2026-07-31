using UnityEngine;

namespace XianXia.Unity.World
{
    /// <summary>
    /// 将鼠标悬停的世界格解析为环境数据，供 HUD 浮层显示。
    /// </summary>
    public sealed class TileHoverProbe : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private WorldTileAmbientGrid ambientGrid;

        public bool HasHover { get; private set; }
        public TileAmbientData HoveredTile { get; private set; }
        public Vector2 HoverWorldPosition { get; private set; }

        public void Configure(Camera camera, WorldTileAmbientGrid grid)
        {
            worldCamera = camera;
            ambientGrid = grid;
        }

        private void Update()
        {
            HasHover = false;
            if (worldCamera == null || ambientGrid == null)
            {
                return;
            }

            Vector3 screen = UnityEngine.Input.mousePosition;
            if (screen.x < 0f || screen.y < 0f || screen.x > Screen.width || screen.y > Screen.height)
            {
                return;
            }

            Vector2 world = worldCamera.ScreenToWorldPoint(screen);
            HoverWorldPosition = world;
            if (ambientGrid.TryGetAtWorld(world, out TileAmbientData data))
            {
                HoveredTile = data;
                HasHover = true;
            }
        }
    }
}
