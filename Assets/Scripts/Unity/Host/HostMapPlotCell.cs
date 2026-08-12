using UnityEngine;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// 一格可交互地块（药田／农田等）。后续可挂作物状态。
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

        public string LocationId => locationId;
        public HostInteractSpotKind InteractKind => interactKind;
        public string Label => label;
        public int GridX => gridX;
        public int GridY => gridY;
        public string Kind => kind;
        public string PlantedCropId => plantedCropId;
        public bool IsPlanted => !string.IsNullOrEmpty(plantedCropId);

        public void Configure(
            string locationIdValue,
            HostInteractSpotKind interact,
            string labelValue,
            int x,
            int y,
            string kindValue)
        {
            locationId = locationIdValue ?? string.Empty;
            interactKind = interact;
            label = labelValue ?? string.Empty;
            gridX = x;
            gridY = y;
            kind = kindValue ?? string.Empty;
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
                string.IsNullOrEmpty(label) ? kind : label);
    }
}
