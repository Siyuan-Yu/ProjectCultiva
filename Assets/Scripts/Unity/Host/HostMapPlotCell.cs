using UnityEngine;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// 一格可交互地块（药田／农田／拾取物等）。
    /// </summary>
    public sealed class HostMapPlotCell : MonoBehaviour
    {
        [SerializeField] string locationId;
        [SerializeField] HostInteractSpotKind interactKind = HostInteractSpotKind.Work;
        [SerializeField] string label;
        [SerializeField] int gridX;
        [SerializeField] int gridY;
        [SerializeField] string kind;
        [SerializeField] string plantedCropId;
        [SerializeField] string lootSpotId;
        [SerializeField] string lootItemId;

        public string LocationId => locationId;
        public HostInteractSpotKind InteractKind => interactKind;
        public string Label => label;
        public int GridX => gridX;
        public int GridY => gridY;
        public string Kind => kind;
        public string PlantedCropId => plantedCropId;
        public string LootSpotId => lootSpotId;
        public string LootItemId => lootItemId;
        public bool IsPlanted => !string.IsNullOrEmpty(plantedCropId);

        public void Configure(
            string locationIdValue,
            HostInteractSpotKind interact,
            string labelValue,
            int x,
            int y,
            string kindValue,
            string lootSpotIdValue = null,
            string lootItemIdValue = null)
        {
            locationId = locationIdValue ?? string.Empty;
            interactKind = interact;
            label = labelValue ?? string.Empty;
            gridX = x;
            gridY = y;
            kind = kindValue ?? string.Empty;
            lootSpotId = lootSpotIdValue ?? string.Empty;
            lootItemId = lootItemIdValue ?? string.Empty;
        }

        public void SetPlanted(string cropId)
        {
            plantedCropId = cropId ?? string.Empty;
        }

        public HostInteractSpot ToInteractSpot() =>
            new HostInteractSpot(
                locationId,
                interactKind,
                transform.position.x,
                transform.position.y,
                string.IsNullOrEmpty(label) ? kind : label,
                lootSpotId,
                lootItemId);
    }
}
