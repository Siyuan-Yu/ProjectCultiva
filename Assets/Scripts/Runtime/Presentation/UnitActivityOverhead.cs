using UnityEngine;
using XianXia.Unity.Cultivation;

namespace XianXia.Unity.Presentation
{
    /// <summary>
    /// 头顶简易活动图标：移动／工作／修炼／交战。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UnitActivityOverhead : MonoBehaviour
    {
        [SerializeField] private DemoUnitController unit;
        [SerializeField] private float height = 0.72f;

        private SpriteRenderer _icon;
        private UnitCultivation _cultivation;
        private Sprite _move;
        private Sprite _work;
        private Sprite _cultivate;
        private Sprite _attack;
        private UnitActivityState _lastState;
        private bool _lastCultivating;
        private bool _lastAttacking;

        private void Awake()
        {
            if (unit == null)
            {
                unit = GetComponent<DemoUnitController>();
            }

            _cultivation = GetComponent<UnitCultivation>();
            EnsureIcon();
        }

        private void LateUpdate()
        {
            if (unit == null || _icon == null)
            {
                return;
            }

            bool cultivating = _cultivation != null && _cultivation.IsCultivating;
            bool attacking = unit.IsAttacking;
            UnitActivityState state = unit.ActivityState;
            if (state != _lastState || cultivating != _lastCultivating || attacking != _lastAttacking)
            {
                ApplyVisual(state, cultivating, attacking);
                _lastState = state;
                _lastCultivating = cultivating;
                _lastAttacking = attacking;
            }

            _icon.transform.position = transform.position + Vector3.up * height;
            _icon.sortingOrder = 5600;
        }

        private void ApplyVisual(UnitActivityState state, bool cultivating, bool attacking)
        {
            if (attacking || state == UnitActivityState.Attacking)
            {
                _icon.enabled = true;
                _icon.sprite = _attack ??= CreateDiamondSprite();
                _icon.color = new Color(0.95f, 0.25f, 0.25f, 0.95f);
                return;
            }

            if (cultivating)
            {
                _icon.enabled = true;
                _icon.sprite = _cultivate ??= CreateRingSprite();
                _icon.color = new Color(0.45f, 0.85f, 1f, 0.95f);
                return;
            }

            switch (state)
            {
                case UnitActivityState.Working:
                    _icon.enabled = true;
                    _icon.sprite = _work ??= CreateSquareSprite();
                    _icon.color = new Color(1f, 0.82f, 0.28f, 0.95f);
                    break;
                case UnitActivityState.Moving:
                    _icon.enabled = true;
                    _icon.sprite = _move ??= CreateTriangleSprite();
                    _icon.color = new Color(0.55f, 0.95f, 0.55f, 0.95f);
                    break;
                default:
                    _icon.enabled = false;
                    break;
            }
        }

        private void EnsureIcon()
        {
            Transform existing = transform.Find("ActivityIcon");
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
                _icon = go.GetComponent<SpriteRenderer>();
            }
            else
            {
                go = new GameObject("ActivityIcon");
                go.transform.SetParent(transform, false);
                _icon = go.AddComponent<SpriteRenderer>();
            }

            go.transform.localScale = Vector3.one * 0.28f;
            _icon.enabled = false;
        }

        private static Sprite CreateTriangleSprite()
        {
            const int size = 16;
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
                    float nx = (x + 0.5f) / size;
                    float ny = (y + 0.5f) / size;
                    bool inside = ny > 0.15f && ny < 0.85f && Mathf.Abs(nx - 0.5f) < (ny - 0.15f) * 0.7f;
                    texture.SetPixel(x, y, inside ? fill : clear);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite CreateSquareSprite()
        {
            const int size = 14;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point
            };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool edge = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                    bool fill = x > 2 && x < size - 3 && y > 2 && y < size - 3;
                    texture.SetPixel(x, y, edge || fill ? Color.white : new Color(0f, 0f, 0f, 0f));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite CreateRingSprite()
        {
            const int size = 16;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point
            };
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    bool on = d <= 6.5f && d >= 4.2f;
                    texture.SetPixel(x, y, on ? Color.white : new Color(0f, 0f, 0f, 0f));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite CreateDiamondSprite()
        {
            const int size = 16;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point
            };
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Abs(x - c) + Mathf.Abs(y - c);
                    texture.SetPixel(x, y, d <= 6.5f ? Color.white : new Color(0f, 0f, 0f, 0f));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
