using UnityEngine;

namespace XianXia.Unity.World
{
    /// <summary>
    /// 隐藏灵地交互边界。进入后可开始修炼；在区内且未修炼时可缓慢采集敛息草。
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class SpiritSiteZone : MonoBehaviour
    {
        [SerializeField] private BoxCollider2D area;
        [SerializeField] private string zoneId = "hidden_spirit_site";
        [SerializeField] private string displayName = "隐藏灵地";
        [SerializeField] private float concealGrassPerGameHour = 1.5f;

        public string ZoneId => zoneId;
        public string DisplayName => displayName;
        public float ConcealGrassPerGameHour => Mathf.Max(0f, concealGrassPerGameHour);
        public Bounds Bounds => area != null ? area.bounds : new Bounds(transform.position, Vector3.one);

        private void Awake()
        {
            if (area == null)
            {
                area = GetComponent<BoxCollider2D>();
            }

            area.isTrigger = true;
        }

        public void Configure(
            string id,
            string zoneDisplayName,
            float grassPerGameHour,
            Vector2 center,
            Vector2 size)
        {
            zoneId = id;
            displayName = zoneDisplayName;
            concealGrassPerGameHour = grassPerGameHour;
            transform.position = center;
            if (area == null)
            {
                area = GetComponent<BoxCollider2D>();
            }

            area.isTrigger = true;
            area.size = size;
            area.offset = Vector2.zero;
        }

        public bool Contains(Vector2 worldPosition)
        {
            return Bounds.Contains(new Vector3(worldPosition.x, worldPosition.y, Bounds.center.z));
        }
    }
}
