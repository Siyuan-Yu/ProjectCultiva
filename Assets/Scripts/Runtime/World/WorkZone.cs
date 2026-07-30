using UnityEngine;

namespace XianXia.Unity.World
{
    /// <summary>
    /// 工作区触发边界。用于时间表遵守检测，不驱动自动移动。
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class WorkZone : MonoBehaviour
    {
        [SerializeField] private BoxCollider2D area;
        [SerializeField] private string zoneId = "farm_work";

        public string ZoneId => zoneId;
        public Bounds Bounds => area != null ? area.bounds : new Bounds(transform.position, Vector3.one);

        private void Awake()
        {
            if (area == null)
            {
                area = GetComponent<BoxCollider2D>();
            }

            area.isTrigger = true;
        }

        public void Configure(string id, Vector2 center, Vector2 size)
        {
            zoneId = id;
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
