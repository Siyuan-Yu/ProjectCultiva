using System.Collections.Generic;
using UnityEngine;
using XianXia.Unity.Time;

namespace XianXia.Unity.Presentation
{
    /// <summary>
    /// 交互反馈：移动落点标记、资源／状态飘字。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldFeedbackOverlay : MonoBehaviour
    {
        private sealed class FloatingText
        {
            public Vector3 World;
            public string Text;
            public Color Color;
            public float Age;
            public float Lifetime;
        }

        private sealed class OrderMarker
        {
            public GameObject Object;
            public SpriteRenderer Renderer;
            public float Age;
            public float Lifetime;
        }

        private static WorldFeedbackOverlay _instance;
        private readonly List<FloatingText> _texts = new();
        private readonly List<OrderMarker> _markers = new();
        private Sprite _markerSprite;
        private GUIStyle _floatStyle;

        public static WorldFeedbackOverlay Ensure()
        {
            if (_instance != null)
            {
                return _instance;
            }

            _instance = FindObjectOfType<WorldFeedbackOverlay>();
            if (_instance != null)
            {
                return _instance;
            }

            GameObject go = new("WorldFeedbackOverlay");
            _instance = go.AddComponent<WorldFeedbackOverlay>();
            return _instance;
        }

        private void Awake()
        {
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Update()
        {
            float delta = GameClock.Instance != null
                ? GameClock.Instance.ScaledDeltaTime
                : UnityEngine.Time.deltaTime;
            if (delta <= 0f)
            {
                delta = UnityEngine.Time.unscaledDeltaTime;
            }

            for (int i = _texts.Count - 1; i >= 0; i--)
            {
                FloatingText text = _texts[i];
                text.Age += delta;
                text.World += Vector3.up * (0.55f * delta);
                if (text.Age >= text.Lifetime)
                {
                    _texts.RemoveAt(i);
                }
            }

            for (int i = _markers.Count - 1; i >= 0; i--)
            {
                OrderMarker marker = _markers[i];
                marker.Age += delta;
                float t = Mathf.Clamp01(marker.Age / marker.Lifetime);
                if (marker.Renderer != null)
                {
                    Color c = marker.Renderer.color;
                    c.a = 0.9f * (1f - t);
                    marker.Renderer.color = c;
                    marker.Object.transform.localScale = Vector3.one * Mathf.Lerp(0.45f, 0.25f, t);
                }

                if (marker.Age >= marker.Lifetime)
                {
                    if (marker.Object != null)
                    {
                        Destroy(marker.Object);
                    }

                    _markers.RemoveAt(i);
                }
            }
        }

        private void OnGUI()
        {
            if (_texts.Count == 0)
            {
                return;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            EnsureStyle();
            for (int i = 0; i < _texts.Count; i++)
            {
                FloatingText text = _texts[i];
                Vector3 screen = cam.WorldToScreenPoint(text.World);
                if (screen.z < 0f)
                {
                    continue;
                }

                float alpha = 1f - Mathf.Clamp01(text.Age / text.Lifetime);
                Color c = text.Color;
                c.a *= alpha;
                _floatStyle.normal.textColor = c;
                float guiY = Screen.height - screen.y;
                GUI.Label(new Rect(screen.x - 40f, guiY - 18f, 120f, 24f), text.Text, _floatStyle);
            }
        }

        public void SpawnFloatingText(Vector3 world, string text, Color color, float lifetime = 1.35f)
        {
            _texts.Add(new FloatingText
            {
                World = world + Vector3.up * 0.35f,
                Text = text,
                Color = color,
                Age = 0f,
                Lifetime = lifetime
            });
        }

        public void SpawnOrderMarker(Vector3 world)
        {
            if (_markerSprite == null)
            {
                _markerSprite = CreateXSprite();
            }

            GameObject go = new("OrderMarker");
            go.transform.position = new Vector3(world.x, world.y, 0f);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = _markerSprite;
            renderer.color = new Color(0.45f, 0.95f, 0.55f, 0.9f);
            renderer.sortingOrder = 5200;
            go.transform.localScale = Vector3.one * 0.45f;

            _markers.Add(new OrderMarker
            {
                Object = go,
                Renderer = renderer,
                Age = 0f,
                Lifetime = 1.4f
            });
        }

        private void EnsureStyle()
        {
            if (_floatStyle != null)
            {
                return;
            }

            _floatStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
        }

        private static Sprite CreateXSprite()
        {
            const int size = 20;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point
            };
            Color clear = new(0f, 0f, 0f, 0f);
            Color fill = Color.white;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool on =
                        Mathf.Abs(x - y) <= 1
                        || Mathf.Abs(x - (size - 1 - y)) <= 1;
                    bool inRing = x > 1 && x < size - 2 && y > 1 && y < size - 2;
                    texture.SetPixel(x, y, on && inRing ? fill : clear);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
