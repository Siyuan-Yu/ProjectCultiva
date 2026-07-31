using UnityEngine;
using XianXia.Unity.Resources;

namespace XianXia.Unity.World
{
    /// <summary>
    /// 工作区内的可操作工位。角色需被指派到工位并显式开始工作后才产出。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorkSpot : MonoBehaviour
    {
        [SerializeField] private WorkZone ownerZone;
        [SerializeField] private string spotName = "工位";
        [SerializeField] private float interactRadius = 0.85f;

        private static readonly Color NormalColor = new(0.95f, 0.78f, 0.28f, 0.85f);
        private static readonly Color TargetingColor = new(1f, 0.92f, 0.35f, 1f);
        private static readonly Color HoverColor = new(1f, 1f, 0.55f, 1f);

        private SpriteRenderer _marker;
        private bool _targetingActive;
        private bool _hovered;

        public WorkZone OwnerZone => ownerZone;
        public string SpotName => spotName;
        public float InteractRadius => Mathf.Max(0.3f, interactRadius);
        public Vector2 Position => transform.position;

        public ResourceType ResourceType =>
            ownerZone != null ? ownerZone.ResourceType : ResourceType.Food;

        public void Configure(WorkZone zone, string name, Vector2 worldPosition)
        {
            ownerZone = zone;
            spotName = name;
            transform.position = new Vector3(worldPosition.x, worldPosition.y, 0f);
            EnsureVisual();
            EnsureCollider();
        }

        private void Awake()
        {
            if (ownerZone == null)
            {
                ownerZone = GetComponentInParent<WorkZone>();
            }

            EnsureVisual();
            EnsureCollider();
        }

        public bool IsInRange(Vector2 worldPosition)
        {
            return ((Vector2)transform.position - worldPosition).sqrMagnitude
                <= InteractRadius * InteractRadius;
        }

        public void SetTargetingVisual(bool targetingActive, bool hovered)
        {
            _targetingActive = targetingActive;
            _hovered = hovered;
            if (_marker == null)
            {
                return;
            }

            if (hovered)
            {
                _marker.color = HoverColor;
                _marker.transform.localScale = Vector3.one * 0.75f;
            }
            else if (targetingActive)
            {
                _marker.color = TargetingColor;
                _marker.transform.localScale = Vector3.one * 0.65f;
            }
            else
            {
                _marker.color = NormalColor;
                _marker.transform.localScale = Vector3.one * 0.55f;
            }
        }

        private void EnsureCollider()
        {
            CircleCollider2D circle = GetComponent<CircleCollider2D>();
            if (circle == null)
            {
                circle = gameObject.AddComponent<CircleCollider2D>();
            }

            circle.isTrigger = true;
            circle.radius = InteractRadius;
        }

        private void EnsureVisual()
        {
            Transform existing = transform.Find("SpotMarker");
            GameObject markerObject;
            if (existing != null)
            {
                markerObject = existing.gameObject;
                _marker = markerObject.GetComponent<SpriteRenderer>();
            }
            else
            {
                markerObject = new GameObject("SpotMarker");
                markerObject.transform.SetParent(transform, false);
                markerObject.transform.localPosition = Vector3.zero;
                _marker = markerObject.AddComponent<SpriteRenderer>();
            }

            if (_marker.sprite == null)
            {
                _marker.sprite = CreateSpotSprite();
            }

            _marker.color = NormalColor;
            _marker.sortingOrder = 4500;
            markerObject.transform.localScale = Vector3.one * 0.55f;
            SetTargetingVisual(_targetingActive, _hovered);
        }

        private static Sprite CreateSpotSprite()
        {
            const int size = 24;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point
            };
            Color clear = new(0f, 0f, 0f, 0f);
            Color fill = Color.white;
            float center = (size - 1) * 0.5f;
            float outer = size * 0.42f;
            float inner = size * 0.22f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    texture.SetPixel(x, y, d <= outer && d >= inner ? fill : clear);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
