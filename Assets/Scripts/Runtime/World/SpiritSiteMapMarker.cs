using UnityEngine;

namespace XianXia.Unity.World
{
    /// <summary>
    /// 在隐藏灵地显示地图标记与屏幕标签，便于玩家找到东南角修炼点。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpiritSiteMapMarker : MonoBehaviour
    {
        [SerializeField] private SpiritSiteZone site;
        [SerializeField] private string mapLabel = "隐藏灵地";
        [SerializeField] private string mapHint = "东南角 · 修炼 / 采敛息草";

        private static GUIStyle _labelStyle;
        private static GUIStyle _hintStyle;
        private static Sprite _diamondSprite;

        private void Awake()
        {
            if (site == null)
            {
                site = GetComponent<SpiritSiteZone>();
            }

            EnsureWorldMarker();
        }

        public void Configure(SpiritSiteZone spiritSite, string label, string hint)
        {
            site = spiritSite;
            mapLabel = label;
            mapHint = hint;
            EnsureWorldMarker();
        }

        private void EnsureWorldMarker()
        {
            Transform existing = transform.Find("MapMarker");
            if (existing != null)
            {
                return;
            }

            GameObject markerObject = new("MapMarker");
            markerObject.transform.SetParent(transform, false);
            markerObject.transform.localPosition = Vector3.zero;

            SpriteRenderer renderer = markerObject.AddComponent<SpriteRenderer>();
            renderer.sprite = GetDiamondSprite();
            renderer.color = new Color(0.25f, 0.9f, 0.78f, 0.9f);
            renderer.sortingOrder = 5000;
        }

        private void OnGUI()
        {
            if (site == null)
            {
                return;
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            Vector3 screen = camera.WorldToScreenPoint(site.transform.position);
            if (screen.z < 0f)
            {
                return;
            }

            EnsureGuiStyles();
            float guiX = screen.x - 70f;
            float guiY = Screen.height - screen.y - 36f;
            GUI.Label(new Rect(guiX, guiY, 140f, 22f), mapLabel, _labelStyle);
            string hint = site.HasPartyInside ? site.InteractiveStatus : mapHint;
            GUI.Label(new Rect(guiX - 20f, guiY + 20f, 180f, 18f), hint, _hintStyle);
        }

        private static void EnsureGuiStyles()
        {
            if (_labelStyle != null)
            {
                return;
            }

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.55f, 1f, 0.88f) }
            };

            _hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.75f, 0.95f, 0.9f) }
            };
        }

        private static Sprite GetDiamondSprite()
        {
            if (_diamondSprite != null)
            {
                return _diamondSprite;
            }

            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color clear = new(0f, 0f, 0f, 0f);
            Color fill = new(1f, 1f, 1f, 1f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int dx = Mathf.Abs(x - size / 2);
                    int dy = Mathf.Abs(y - size / 2);
                    texture.SetPixel(x, y, dx + dy <= size / 2 - 2 ? fill : clear);
                }
            }

            texture.Apply();
            _diamondSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                size);
            return _diamondSprite;
        }
    }
}
