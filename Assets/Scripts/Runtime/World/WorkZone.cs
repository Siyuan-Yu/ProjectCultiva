using System.Collections.Generic;
using UnityEngine;
using XianXia.Unity.Resources;

namespace XianXia.Unity.World
{
    /// <summary>
    /// 工作区边界 + 多个可操作工位。
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class WorkZone : MonoBehaviour
    {
        [SerializeField] private BoxCollider2D area;
        [SerializeField] private string zoneId = "farm_work";
        [SerializeField] private string displayName = "工作区";
        [SerializeField] private ResourceType resourceType = ResourceType.Food;
        [SerializeField] private float unitsPerGameHour = 4f;
        [SerializeField] private List<WorkSpot> spots = new();

        public string ZoneId => zoneId;
        public string DisplayName => displayName;
        public ResourceType ResourceType => resourceType;
        public float UnitsPerGameHour => Mathf.Max(0f, unitsPerGameHour);
        public Bounds Bounds => area != null ? area.bounds : new Bounds(transform.position, Vector3.one);
        public IReadOnlyList<WorkSpot> Spots => spots;

        private void Awake()
        {
            if (area == null)
            {
                area = GetComponent<BoxCollider2D>();
            }

            area.isTrigger = true;
            RefreshSpotCache();
        }

        public void Configure(
            string id,
            string zoneDisplayName,
            ResourceType producedResource,
            float productionPerGameHour,
            Vector2 center,
            Vector2 size)
        {
            zoneId = id;
            displayName = zoneDisplayName;
            resourceType = producedResource;
            unitsPerGameHour = productionPerGameHour;
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

        public void EnsureDefaultSpots(int count)
        {
            RefreshSpotCache();
            if (spots.Count >= count)
            {
                return;
            }

            Bounds bounds = Bounds;
            int columns = Mathf.CeilToInt(Mathf.Sqrt(count));
            int rows = Mathf.CeilToInt(count / (float)columns);
            int created = spots.Count;
            for (int i = created; i < count; i++)
            {
                int row = i / columns;
                int col = i % columns;
                float nx = columns <= 1 ? 0.5f : (col + 0.5f) / columns;
                float ny = rows <= 1 ? 0.5f : (row + 0.5f) / rows;
                Vector2 pos = new(
                    Mathf.Lerp(bounds.min.x + 0.6f, bounds.max.x - 0.6f, nx),
                    Mathf.Lerp(bounds.min.y + 0.6f, bounds.max.y - 0.6f, ny));

                GameObject spotObject = new($"WorkSpot_{i + 1}");
                spotObject.transform.SetParent(transform, false);
                WorkSpot spot = spotObject.AddComponent<WorkSpot>();
                spot.Configure(this, $"{displayName}工位{i + 1}", pos);
                spots.Add(spot);
            }
        }

        public WorkSpot FindNearestSpot(Vector2 worldPosition)
        {
            RefreshSpotCache();
            WorkSpot best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < spots.Count; i++)
            {
                WorkSpot spot = spots[i];
                if (spot == null)
                {
                    continue;
                }

                float d = ((Vector2)spot.transform.position - worldPosition).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = spot;
                }
            }

            return best;
        }

        public WorkSpot FindSpotContaining(Vector2 worldPosition)
        {
            RefreshSpotCache();
            for (int i = 0; i < spots.Count; i++)
            {
                WorkSpot spot = spots[i];
                if (spot != null && spot.IsInRange(worldPosition))
                {
                    return spot;
                }
            }

            return null;
        }

        private void RefreshSpotCache()
        {
            spots.RemoveAll(s => s == null);
            WorkSpot[] children = GetComponentsInChildren<WorkSpot>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] != null && !spots.Contains(children[i]))
                {
                    spots.Add(children[i]);
                }
            }
        }
    }
}
