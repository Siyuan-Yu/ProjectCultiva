using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Exploration
{
    /// <summary>
    /// Separate Space Session（SPACE-01）。
    /// 类型名保留 LocalMapSession 以兼容既有调用点；语义已收窄为独立可玩空间，不再表示 Outdoor LocalMap。
    /// </summary>
    public sealed class LocalMapSession
    {
        readonly List<EntityId> _occupantIds = new List<EntityId>(8);

        /// <summary>当前 Separate Space 的 MapLayout id；Outdoor 时为空。</summary>
        public string ActiveMapLayoutId { get; set; } = string.Empty;

        /// <summary>当前激活的 LocalPlaceSet id（可由 MapLayout resolve）。</summary>
        public string ActiveLocalPlaceSetId { get; set; } = string.Empty;

        /// <summary>进入前记住的旧 Overworld MapLayout（legacy 非 Continuous 路径）。</summary>
        public string OverworldMapLayoutId { get; set; } = string.Empty;

        /// <summary>逻辑入口 identity（通常是洞口 location）。</summary>
        public string EntryLocationId { get; set; } = string.Empty;

        /// <summary>离开时把队伍送回的地点（通常是洞口）。</summary>
        public string ReturnLocationId { get; set; } = string.Empty;

        public SeparateSpaceKind SpaceKind { get; set; } = SeparateSpaceKind.None;

        /// <summary>进入原因（最小字符串，如 enter / migrate）。</summary>
        public string EntryReason { get; set; } = string.Empty;

        public bool HasContinuousOutdoorReturn { get; set; }
        public float ContinuousOutdoorReturnX { get; set; }
        public float ContinuousOutdoorReturnY { get; set; }
        public string ContinuousOutdoorReturnSurfaceId { get; set; } = string.Empty;

        /// <summary>Outdoor return authority 别名。</summary>
        public bool HasOutdoorReturn
        {
            get => HasContinuousOutdoorReturn;
            set => HasContinuousOutdoorReturn = value;
        }

        public string ReturnSurfaceId
        {
            get => ContinuousOutdoorReturnSurfaceId;
            set => ContinuousOutdoorReturnSurfaceId = value ?? string.Empty;
        }

        public float ReturnWorldX
        {
            get => ContinuousOutdoorReturnX;
            set => ContinuousOutdoorReturnX = value;
        }

        public float ReturnWorldY
        {
            get => ContinuousOutdoorReturnY;
            set => ContinuousOutdoorReturnY = value;
        }

        /// <summary>
        /// Surface Exit Trigger Depth（Gameplay）。由当前 MapLayout 写入；≤0 表示使用默认值。
        /// 只影响 Detection/Presentation 共用的 Exit Zone，不进 Snapshot。
        /// </summary>
        public float ExitTriggerDepth { get; set; }

        /// <summary>当前仍在 Separate Space 的己方（进洞登记；离开关闭时清空）。</summary>
        public IReadOnlyList<EntityId> OccupantIds => _occupantIds;

        /// <summary>Separate Space 是否激活（正式名）。</summary>
        public bool IsActive => IsInInterior;

        /// <summary>兼容旧名：是否处于独立内室／洞府等 Separate Space。</summary>
        public bool IsInInterior
        {
            get
            {
                if (string.IsNullOrEmpty(ActiveMapLayoutId))
                    return false;
                if (HasContinuousOutdoorReturn)
                    return true;
                // Active 已回到 Overworld → 明确不在 Separate Space（含半离开兼容）。
                if (!string.IsNullOrEmpty(OverworldMapLayoutId) &&
                    string.Equals(ActiveMapLayoutId, OverworldMapLayoutId, System.StringComparison.Ordinal))
                    return false;
                if (SpaceKind != SeparateSpaceKind.None)
                    return true;
                return !string.IsNullOrEmpty(OverworldMapLayoutId) &&
                       !string.Equals(ActiveMapLayoutId, OverworldMapLayoutId, System.StringComparison.Ordinal);
            }
        }

        public void EnsureOverworld(string mapLayoutId)
        {
            if (string.IsNullOrWhiteSpace(mapLayoutId))
                return;
            if (string.IsNullOrEmpty(OverworldMapLayoutId))
                OverworldMapLayoutId = mapLayoutId;
            if (string.IsNullOrEmpty(ActiveMapLayoutId))
                ActiveMapLayoutId = mapLayoutId;
        }

        public void ClearOccupants() => _occupantIds.Clear();

        public void SetOccupants(IReadOnlyList<EntityId> occupants)
        {
            _occupantIds.Clear();
            if (occupants == null)
                return;
            for (var i = 0; i < occupants.Count; i++)
            {
                var id = occupants[i];
                if (id.IsNone || ContainsOccupant(id))
                    continue;
                _occupantIds.Add(id);
            }
        }

        public void AddOccupant(EntityId id)
        {
            if (id.IsNone || ContainsOccupant(id))
                return;
            _occupantIds.Add(id);
        }

        public bool RemoveOccupant(EntityId id)
        {
            if (id.IsNone)
                return false;
            for (var i = 0; i < _occupantIds.Count; i++)
            {
                if (_occupantIds[i] != id)
                    continue;
                _occupantIds.RemoveAt(i);
                return true;
            }

            return false;
        }

        public bool ContainsOccupant(EntityId id)
        {
            if (id.IsNone)
                return false;
            for (var i = 0; i < _occupantIds.Count; i++)
            {
                if (_occupantIds[i] == id)
                    return true;
            }

            return false;
        }

        public void EstablishSeparateSpace(
            string mapLayoutId,
            string localPlaceSetId,
            SeparateSpaceKind kind,
            string entryLocationId,
            string returnLocationId,
            string entryReason)
        {
            ActiveMapLayoutId = mapLayoutId ?? string.Empty;
            ActiveLocalPlaceSetId = localPlaceSetId ?? string.Empty;
            SpaceKind = kind;
            EntryLocationId = entryLocationId ?? string.Empty;
            ReturnLocationId = returnLocationId ?? string.Empty;
            EntryReason = entryReason ?? string.Empty;
        }

        public void ClearSeparateSpaceIdentity()
        {
            SpaceKind = SeparateSpaceKind.None;
            EntryLocationId = string.Empty;
            EntryReason = string.Empty;
            ActiveLocalPlaceSetId = string.Empty;
        }

        public void Clear()
        {
            ActiveMapLayoutId = string.Empty;
            ActiveLocalPlaceSetId = string.Empty;
            OverworldMapLayoutId = string.Empty;
            EntryLocationId = string.Empty;
            ReturnLocationId = string.Empty;
            SpaceKind = SeparateSpaceKind.None;
            EntryReason = string.Empty;
            HasContinuousOutdoorReturn = false;
            ContinuousOutdoorReturnSurfaceId = string.Empty;
            ContinuousOutdoorReturnX = ContinuousOutdoorReturnY = 0f;
            ExitTriggerDepth = 0f;
            _occupantIds.Clear();
        }
    }

    public sealed class OutdoorStatefulObjectBoard
    {
        readonly Dictionary<string, OutdoorDestructibleState> _destructibles =
            new Dictionary<string, OutdoorDestructibleState>(System.StringComparer.Ordinal);
        readonly Dictionary<string, OutdoorFarmPlotState> _farmPlots =
            new Dictionary<string, OutdoorFarmPlotState>(System.StringComparer.Ordinal);

        public IReadOnlyDictionary<string, OutdoorDestructibleState> Destructibles => _destructibles;
        public IReadOnlyDictionary<string, OutdoorFarmPlotState> FarmPlots => _farmPlots;
        /// <summary>
        /// Changes only when a destructible starts or stops contributing authored collision.
        /// HP-only changes do not force a navigation rebuild.
        /// </summary>
        public ulong DestructibleTopologyRevision { get; private set; }
        public bool TryGetDestructible(string id, out OutdoorDestructibleState state)
        {
            state = default;
            return !string.IsNullOrEmpty(id) && _destructibles.TryGetValue(id, out state);
        }
        public bool TryGetFarmPlot(string id, out OutdoorFarmPlotState state)
        {
            state = default;
            return !string.IsNullOrEmpty(id) && _farmPlots.TryGetValue(id, out state);
        }
        public bool IsDestructibleDestroyed(string id) =>
            TryGetDestructible(id, out var state) && (state.Destroyed || state.Hp <= 0);
        public void SetDestructible(string id, int hp, bool destroyed)
        {
            if (string.IsNullOrEmpty(id))
                return;
            var effectiveDestroyed = destroyed || hp <= 0;
            var topologyChanged = !_destructibles.TryGetValue(id, out var previous)
                ? effectiveDestroyed
                : (previous.Destroyed || previous.Hp <= 0) != effectiveDestroyed;
            _destructibles[id] = new OutdoorDestructibleState(hp, effectiveDestroyed);
            if (topologyChanged)
                DestructibleTopologyRevision++;
        }
        public void SetFarmPlot(string id, string cropId, int cropStage, float growth) =>
            SetFarmPlot(id, cropId, (OutdoorFarmCropStage)cropStage, growth);
        public void SetFarmPlot(string id, string cropId, OutdoorFarmCropStage cropStage, float growth)
        {
            if (!string.IsNullOrEmpty(id)) _farmPlots[id] = new OutdoorFarmPlotState(cropId, cropStage, growth);
        }
        public void Clear()
        {
            if (_destructibles.Count > 0)
                DestructibleTopologyRevision++;
            _destructibles.Clear();
            _farmPlots.Clear();
        }
    }

    /// <summary>Stable authored identity shared by Outdoor presentation, state and navigation.</summary>
    public static class OutdoorStatefulObjectId
    {
        public static string ForCell(string placementId, int localX, int localY) =>
            (placementId ?? string.Empty) + ":" + localX + ":" + localY;
    }

    public readonly struct OutdoorDestructibleState
    {
        public OutdoorDestructibleState(int hp, bool destroyed) { Hp = hp; Destroyed = destroyed; }
        public int Hp { get; }
        public bool Destroyed { get; }
    }

    public readonly struct OutdoorFarmPlotState
    {
        public OutdoorFarmPlotState(string cropId, OutdoorFarmCropStage cropStage, float growth)
        { CropId = cropId ?? string.Empty; Stage = cropStage; Growth = growth; }
        public string CropId { get; }
        public OutdoorFarmCropStage Stage { get; }
        public int CropStage => (int)Stage;
        public float Growth { get; }
    }
}
