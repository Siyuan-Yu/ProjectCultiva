using UnityEngine;
using XianXia.Unity.Presentation;

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

        private bool _hadPartyInside;

        public string ZoneId => zoneId;
        public string DisplayName => displayName;
        public float ConcealGrassPerGameHour => Mathf.Max(0f, concealGrassPerGameHour);
        public Bounds Bounds => area != null ? area.bounds : new Bounds(transform.position, Vector3.one);
        /// <summary>队伍是否有人在区内（可交互修炼）。</summary>
        public bool HasPartyInside { get; private set; }
        /// <summary>区内是否有人正在修炼。</summary>
        public int CultivatingCount { get; private set; }
        public bool CanStartCultivation => HasPartyInside;
        public string InteractiveStatus => HasPartyInside
            ? (CultivatingCount > 0 ? $"修炼中×{CultivatingCount}" : "可修炼（右键／C）")
            : "进入后可修炼";

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

        /// <summary>由 CultivationSystem 每帧同步占用状态；刚进入时飘字提示可修炼。</summary>
        public void SetPartyPresence(bool partyInside, int cultivatingCount)
        {
            HasPartyInside = partyInside;
            CultivatingCount = Mathf.Max(0, cultivatingCount);
            if (partyInside && !_hadPartyInside)
            {
                WorldFeedbackOverlay.Ensure().SpawnFloatingText(
                    Bounds.center,
                    "进入灵地·可修炼",
                    new Color(0.45f, 0.95f, 0.85f),
                    1.3f);
            }

            _hadPartyInside = partyInside;
        }
    }
}
