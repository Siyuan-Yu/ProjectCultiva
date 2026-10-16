using UnityEngine;
using XianXia.Core.Construction;
using XianXia.Core.Exploration;
using XianXia.Core.Simulation;

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
        [SerializeField] OutdoorFarmCropStage cropStage = OutdoorFarmCropStage.Empty;
        [SerializeField] float growth01;
        [SerializeField] string lootSpotId;
        [SerializeField] string lootItemId;
        [SerializeField] string stableCellId;
        SimulationWorld _world;
        Rect _interactionBounds;
        bool _hasInteractionBounds;

        public string LocationId => locationId;
        public HostInteractSpotKind InteractKind => interactKind;
        public string Label => label;
        public int GridX => gridX;
        public int GridY => gridY;
        public string Kind => kind;
        public string PlantedCropId => plantedCropId;
        public OutdoorFarmCropStage CropStage => cropStage;
        public float Growth01 => growth01;
        public string LootSpotId => lootSpotId;
        public string LootItemId => lootItemId;
        public string StableCellId => stableCellId;
        public bool HasInteractionBounds => _hasInteractionBounds;
        public Rect InteractionBounds => _interactionBounds;
        public float InteractionBoundsArea => _hasInteractionBounds ? _interactionBounds.width * _interactionBounds.height : float.MaxValue;
        public bool IsPlanted =>
            !string.IsNullOrEmpty(plantedCropId) && cropStage != OutdoorFarmCropStage.Empty;

        public bool IsPlantableField =>
            string.Equals(kind, "herbField", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "grainField", System.StringComparison.OrdinalIgnoreCase);

        public bool IsRecoverySpot =>
            interactKind == HostInteractSpotKind.Recovery ||
            string.Equals(kind, "recoverySpot", System.StringComparison.OrdinalIgnoreCase);

        public bool IsPrisonDropOff =>
            interactKind == HostInteractSpotKind.PrisonDropOff ||
            string.Equals(kind, "prisonDropOff", System.StringComparison.OrdinalIgnoreCase);

        public bool IsStorageRoom =>
            interactKind == HostInteractSpotKind.Storage ||
            string.Equals(kind, "storageRoom", System.StringComparison.OrdinalIgnoreCase);

        public bool IsBed => interactKind == HostInteractSpotKind.Bed ||
            string.Equals(kind, "bed", System.StringComparison.OrdinalIgnoreCase);
        public bool IsRoom => interactKind == HostInteractSpotKind.Room ||
            string.Equals(kind, "room", System.StringComparison.OrdinalIgnoreCase);

        public void Configure(
            SimulationWorld world,
            string stableId,
            string locationIdValue,
            HostInteractSpotKind interact,
            string labelValue,
            int x,
            int y,
            string kindValue,
            string lootSpotIdValue = null,
            string lootItemIdValue = null)
        {
            _world = world;
            stableCellId = stableId ?? string.Empty;
            locationId = locationIdValue ?? string.Empty;
            interactKind = interact;
            label = labelValue ?? string.Empty;
            gridX = x;
            gridY = y;
            kind = kindValue ?? string.Empty;
            lootSpotId = lootSpotIdValue ?? string.Empty;
            lootItemId = lootItemIdValue ?? string.Empty;
            RefreshFromWorldState();
            HostMapObjectRegistry.Register(this);
            HostFarmFieldRegistry.Register(this);
            RefreshCropVisual();
        }

        /// <summary>Runtime-only final presentation geometry; never part of content or snapshot state.</summary>
        public void ConfigureInteractionBounds(Rect bounds)
        {
            _interactionBounds = bounds;
            _hasInteractionBounds = bounds.width > 0f && bounds.height > 0f;
        }

        public bool ContainsInteractionPoint(Vector3 point) =>
            _hasInteractionBounds && _interactionBounds.Contains(new Vector2(point.x, point.y));

        public void SetPlanted(string cropId)
        {
            plantedCropId = cropId ?? string.Empty;
            if (string.IsNullOrEmpty(plantedCropId))
            {
                cropStage = OutdoorFarmCropStage.Empty;
                growth01 = 0f;
                RefreshCropVisual();
                PersistFarmState();
                return;
            }

            cropStage = OutdoorFarmCropStage.Growing;
            growth01 = 0f;
            RefreshCropVisual();
            PersistFarmState();
        }

        public void SetCropStage(OutdoorFarmCropStage stage, float growth = -1f)
        {
            cropStage = stage;
            if (stage == OutdoorFarmCropStage.Empty)
            {
                plantedCropId = string.Empty;
                growth01 = 0f;
                RefreshCropVisual();
                PersistFarmState();
                return;
            }

            if (growth >= 0f)
                growth01 = Mathf.Clamp01(growth);
            else if (stage == OutdoorFarmCropStage.Mature)
                growth01 = 1f;

            if (stage == OutdoorFarmCropStage.Growing && growth01 >= 0.999f)
            {
                cropStage = OutdoorFarmCropStage.Mature;
                growth01 = 1f;
            }

            RefreshCropVisual();
            PersistFarmState();
        }

        void PersistFarmState()
        {
            if (IsPlantableField && !string.IsNullOrEmpty(stableCellId))
                _world?.OutdoorStatefulObjects.SetFarmPlot(
                    stableCellId, plantedCropId, cropStage, growth01);
        }

        /// <summary>Presentation-only pull from the Core board after WorldTick advances.</summary>
        public void RefreshFromWorldState()
        {
            if (!IsPlantableField || string.IsNullOrEmpty(stableCellId) ||
                _world?.OutdoorStatefulObjects == null)
                return;
            if (_world.OutdoorStatefulObjects.TryGetFarmPlot(stableCellId, out var saved))
            {
                plantedCropId = saved.CropId;
                cropStage = saved.Stage;
                growth01 = Mathf.Clamp01(saved.Growth);
            }
            else
            {
                plantedCropId = string.Empty;
                cropStage = OutdoorFarmCropStage.Empty;
                growth01 = 0f;
            }
            RefreshCropVisual();
        }

        public void RefreshCropVisual()
        {
            if (!IsPlantableField)
                return;
            var sr = GetComponentInChildren<SpriteRenderer>();
            if (sr == null)
                return;
            Color c;
            switch (cropStage)
            {
                case OutdoorFarmCropStage.Empty:
                    // 空闲：农田偏土黄；药田偏灰绿土（与成长绿明显分开）
                    c = string.Equals(kind, "herbField", System.StringComparison.OrdinalIgnoreCase)
                        ? new Color(0.88f, 0.97f, 0.88f, 1f) // 空闲：非常浅绿
                        : new Color(0.62f, 0.56f, 0.28f, 1f);
                    break;
                case OutdoorFarmCropStage.Growing:
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
                case OutdoorFarmCropStage.Mature:
                    c = string.Equals(kind, "herbField", System.StringComparison.OrdinalIgnoreCase)
                        ? new Color(0.55f, 0.95f, 0.70f, 1f) // 药田成熟偏青白
                        : new Color(0.85f, 0.78f, 0.28f, 1f);
                    break;
                case OutdoorFarmCropStage.Ruined:
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
                case OutdoorFarmCropStage.Empty:
                    return "空闲（未种植）";
                case OutdoorFarmCropStage.Growing:
                    return "成长中 · " + CropName() + " · " +
                           Mathf.RoundToInt(growth01 * 100f) + "%";
                case OutdoorFarmCropStage.Mature:
                    return "已成熟 · " + CropName() + " · 可收获";
                case OutdoorFarmCropStage.Ruined:
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
                case "recoveryspot": return "恢复处";
                case "prisondropoff": return "囚犯交接点";
                case "storageroom": return "储藏室";
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

    /// <summary>Presentation-only bed appearance derived from parent Room.RoomUsage.</summary>
    public static class HostBedVisualStyle
    {
        public static readonly Color Residential = new Color(0.55f, 0.42f, 0.72f, 1f);
        public static readonly Color Prison = new Color(0x8b / 255f, 0x5a / 255f, 0x3c / 255f, 1f);
        public static readonly Color Medical = new Color(0xa0 / 255f, 0xa0 / 255f, 0xa0 / 255f, 1f);

        public static void Apply(GameObject target, SimulationWorld world, string bedId)
        {
            if (target == null || world == null || string.IsNullOrEmpty(bedId) ||
                !world.OutdoorConstructedAssets.Assets.TryGetValue(bedId, out var bed) ||
                !RoomBedGeometry.IsBed(bed))
                return;
            var usage = RoomBedFacilityService.BedHasUsage(world, bed, RoomUsage.Prison) ? RoomUsage.Prison :
                RoomBedFacilityService.BedHasUsage(world, bed, RoomUsage.Medical) ? RoomUsage.Medical :
                    RoomUsage.Residential;
            if (HostStepAArt.ApplyObjectVisual(target, HostStepAArt.BedSprite(usage)))
                return;
            var color = usage == RoomUsage.Prison ? Prison :
                usage == RoomUsage.Medical ? Medical : Residential;
            var renderers = target.GetComponentsInChildren<SpriteRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].sprite = HostSpriteFactory.BedSprite();
                renderers[i].color = color;
            }
        }

        public static void RefreshRoom(SimulationWorld world, string roomId)
        {
            if (world == null || string.IsNullOrEmpty(roomId)) return;
            var plots = HostMapObjectRegistry.AllPlots;
            for (var i = 0; i < plots.Count; i++)
            {
                var plot = plots[i];
                if (plot == null || !plot.IsBed ||
                    !world.OutdoorConstructedAssets.Assets.TryGetValue(plot.StableCellId, out var bed) ||
                    !string.Equals(bed.ParentRoomId, roomId, System.StringComparison.Ordinal)) continue;
                Apply(plot.gameObject, world, plot.StableCellId);
            }
        }
    }
}
