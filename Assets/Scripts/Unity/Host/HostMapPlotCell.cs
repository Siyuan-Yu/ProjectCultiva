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
        [SerializeField] PlotCropStage cropStage = PlotCropStage.Empty;
        [SerializeField] float growth01;
        [SerializeField] string lootSpotId;
        [SerializeField] string lootItemId;

        public string LocationId => locationId;
        public HostInteractSpotKind InteractKind => interactKind;
        public string Label => label;
        public int GridX => gridX;
        public int GridY => gridY;
        public string Kind => kind;
        public string PlantedCropId => plantedCropId;
        public PlotCropStage CropStage => cropStage;
        public float Growth01 => growth01;
        public string LootSpotId => lootSpotId;
        public string LootItemId => lootItemId;
        public bool IsPlanted =>
            !string.IsNullOrEmpty(plantedCropId) && cropStage != PlotCropStage.Empty;

        public bool IsPlantableField =>
            string.Equals(kind, "herbField", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "grainField", System.StringComparison.OrdinalIgnoreCase);

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
            HostMapObjectRegistry.Register(this);
            HostFarmFieldRegistry.Register(this);
            RefreshCropVisual();
        }

        public void SetPlanted(string cropId)
        {
            plantedCropId = cropId ?? string.Empty;
            if (string.IsNullOrEmpty(plantedCropId))
            {
                cropStage = PlotCropStage.Empty;
                growth01 = 0f;
                RefreshCropVisual();
                return;
            }

            cropStage = PlotCropStage.Growing;
            growth01 = 0f;
            RefreshCropVisual();
        }

        public void SetCropStage(PlotCropStage stage, float growth = -1f)
        {
            cropStage = stage;
            if (stage == PlotCropStage.Empty)
            {
                plantedCropId = string.Empty;
                growth01 = 0f;
                RefreshCropVisual();
                return;
            }

            if (growth >= 0f)
                growth01 = Mathf.Clamp01(growth);
            else if (stage == PlotCropStage.Mature)
                growth01 = 1f;

            if (stage == PlotCropStage.Growing && growth01 >= 0.999f)
            {
                cropStage = PlotCropStage.Mature;
                growth01 = 1f;
            }

            RefreshCropVisual();
        }

        public void RefreshCropVisual()
        {
            var sr = GetComponentInChildren<SpriteRenderer>();
            if (sr == null)
                return;
            Color c;
            switch (cropStage)
            {
                case PlotCropStage.Empty:
                    // 空闲：农田偏土黄；药田偏灰绿土（与成长绿明显分开）
                    c = string.Equals(kind, "herbField", System.StringComparison.OrdinalIgnoreCase)
                        ? new Color(0.88f, 0.97f, 0.88f, 1f) // 空闲：非常浅绿
                        : new Color(0.62f, 0.56f, 0.28f, 1f);
                    break;
                case PlotCropStage.Growing:
                    if (string.Equals(kind, "herbField", System.StringComparison.OrdinalIgnoreCase))
                    {
                        // 药田成长：从暗土 → 鲜明翠绿
                        c = Color.Lerp(
                            new Color(0.28f, 0.48f, 0.30f, 1f),
                            new Color(0.35f, 0.88f, 0.45f, 1f),
                            growth01);
                    }
                    else
                    {
                        c = Color.Lerp(
                            new Color(0.35f, 0.55f, 0.28f, 1f),
                            new Color(0.45f, 0.75f, 0.35f, 1f),
                            growth01);
                    }

                    break;
                case PlotCropStage.Mature:
                    c = string.Equals(kind, "herbField", System.StringComparison.OrdinalIgnoreCase)
                        ? new Color(0.55f, 0.95f, 0.70f, 1f) // 药田成熟偏青白
                        : new Color(0.85f, 0.78f, 0.28f, 1f);
                    break;
                case PlotCropStage.Ruined:
                    c = new Color(0.42f, 0.35f, 0.28f, 1f);
                    break;
                default:
                    return;
            }

            sr.color = c;
        }

        public string DescribeCropStatus()
        {
            if (!IsPlantableField)
                return KindDisplayName();

            switch (cropStage)
            {
                case PlotCropStage.Empty:
                    return "空闲（未种植）";
                case PlotCropStage.Growing:
                    return "成长中 · " + CropName() + " · " +
                           Mathf.RoundToInt(growth01 * 100f) + "%";
                case PlotCropStage.Mature:
                    return "已成熟 · " + CropName() + " · 可收获";
                case PlotCropStage.Ruined:
                    return "已损坏 · " + CropName();
                default:
                    return "—";
            }
        }

        public string CropName()
        {
            if (string.IsNullOrEmpty(plantedCropId))
                return "作物";
            if (plantedCropId.IndexOf("herb", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                plantedCropId.IndexOf("灵药", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "灵药";
            if (plantedCropId.IndexOf("grain", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                plantedCropId.IndexOf("麦", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "麦";
            return plantedCropId;
        }

        public string KindDisplayName()
        {
            switch ((kind ?? string.Empty).ToLowerInvariant())
            {
                case "herbField": return "药田格";
                case "grainField": return "农田格";
                case "loot": return "地上物";
                case "cushion": return "蒲团";
                case "ore":
                case "mine": return "矿点";
                default: return string.IsNullOrEmpty(label) ? kind : label;
            }
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
