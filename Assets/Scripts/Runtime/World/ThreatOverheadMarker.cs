using UnityEngine;

namespace XianXia.Unity.World
{
    /// <summary>
    /// 威胁人物头顶色标三角：高威胁红、中威胁黄。无威胁不显示。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WorldCharacterInspectable))]
    public sealed class ThreatOverheadMarker : MonoBehaviour
    {
        private const float HighThreatThreshold = 0.75f;
        private const float MediumThreatThreshold = 0.2f;

        [SerializeField] private WorldCharacterInspectable character;
        [SerializeField] private float heightOffset = 1.35f;

        private static Sprite _triangleSprite;
        private SpriteRenderer _renderer;

        private void Awake()
        {
            if (character == null)
            {
                character = GetComponent<WorldCharacterInspectable>();
            }

            EnsureMarker();
            RefreshColor();
        }

        private void LateUpdate()
        {
            RefreshColor();
        }

        public void RefreshColor()
        {
            EnsureMarker();
            if (_renderer == null || character == null)
            {
                return;
            }

            float threat = character.ThreatLevel;
            if (threat < MediumThreatThreshold)
            {
                _renderer.enabled = false;
                return;
            }

            _renderer.enabled = true;
            _renderer.color = threat >= HighThreatThreshold
                ? new Color(0.92f, 0.18f, 0.16f, 0.95f)   // 主管等：红
                : new Color(0.95f, 0.78f, 0.15f, 0.95f);  // 守卫等：黄
        }

        private void EnsureMarker()
        {
            if (_renderer != null)
            {
                return;
            }

            Transform existing = transform.Find("ThreatMarker");
            GameObject markerObject;
            if (existing != null)
            {
                markerObject = existing.gameObject;
                _renderer = markerObject.GetComponent<SpriteRenderer>();
                if (_renderer == null)
                {
                    _renderer = markerObject.AddComponent<SpriteRenderer>();
                }
            }
            else
            {
                markerObject = new GameObject("ThreatMarker");
                markerObject.transform.SetParent(transform, false);
                _renderer = markerObject.AddComponent<SpriteRenderer>();
            }

            markerObject.transform.localPosition = new Vector3(0f, heightOffset, 0f);
            markerObject.transform.localScale = Vector3.one * 0.45f;
            _renderer.sprite = GetTriangleSprite();
            _renderer.sortingOrder = 6000;
        }

        private static Sprite GetTriangleSprite()
        {
            if (_triangleSprite != null)
            {
                return _triangleSprite;
            }

            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color clear = new(0f, 0f, 0f, 0f);
            Color fill = Color.white;
            Color outline = new(0f, 0f, 0f, 0.85f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    texture.SetPixel(x, y, clear);
                }
            }

            // 顶点朝上的实心三角（尖朝上）。
            int tipY = size - 3;
            int baseY = 4;
            for (int y = baseY; y <= tipY; y++)
            {
                float t = (y - baseY) / (float)(tipY - baseY);
                int halfWidth = Mathf.RoundToInt(Mathf.Lerp(size * 0.42f, 0.5f, t));
                int center = size / 2;
                for (int x = center - halfWidth; x <= center + halfWidth; x++)
                {
                    bool edge = x == center - halfWidth
                        || x == center + halfWidth
                        || y == baseY
                        || y == tipY;
                    texture.SetPixel(x, y, edge ? outline : fill);
                }
            }

            texture.Apply();
            _triangleSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0f),
                size);
            return _triangleSprite;
        }
    }
}
