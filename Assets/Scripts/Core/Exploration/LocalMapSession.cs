using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Exploration
{
    /// <summary>
    /// 当前加载的 LocalMap（session-only；对齐 [113] 进出图竖切，不进 Snapshot v1）。
    /// </summary>
    public sealed class LocalMapSession
    {
        readonly List<EntityId> _occupantIds = new List<EntityId>(8);

        /// <summary>当前 Host 应显示的 mapLayout id。</summary>
        public string ActiveMapLayoutId { get; set; } = string.Empty;

        /// <summary>进入洞府／秘境前记住的地表图；离开时还原。</summary>
        public string OverworldMapLayoutId { get; set; } = string.Empty;

        /// <summary>离开时把队伍送回的地点（通常是洞口）。</summary>
        public string ReturnLocationId { get; set; } = string.Empty;
        public bool HasContinuousOutdoorReturn { get; set; }
        public float ContinuousOutdoorReturnX { get; set; }
        public float ContinuousOutdoorReturnY { get; set; }
        public string ContinuousOutdoorReturnSurfaceId { get; set; } = string.Empty;

        /// <summary>
        /// Surface Exit Trigger Depth（Gameplay）。由当前 MapLayout 写入；≤0 表示使用默认值。
        /// 只影响 Detection/Presentation 共用的 Exit Zone，不进 Snapshot。
        /// </summary>
        public float ExitTriggerDepth { get; set; }

        public float PlayableOriginX { get; set; }
        public float PlayableOriginY { get; set; }
        public float PlayableCellSize { get; set; } = 1f;
        public int PlayableWidth { get; set; }
        public int PlayableHeight { get; set; }

        public bool HasPlayableBounds => PlayableWidth > 0 && PlayableHeight > 0;

        public void SetPlayableBounds(
            float originX,
            float originY,
            float cellSize,
            int width,
            int height)
        {
            PlayableOriginX = originX;
            PlayableOriginY = originY;
            PlayableCellSize = cellSize > 0.0001f ? cellSize : 1f;
            PlayableWidth = width;
            PlayableHeight = height;
        }

        public bool TryGetPlayableBounds(
            out WildernessLocalWorldProjection.WildernessLocalMapBounds bounds)
        {
            bounds = default;
            if (!HasPlayableBounds)
                return false;
            bounds = WildernessLocalWorldProjection.WildernessLocalMapBounds.FromOriginSize(
                PlayableOriginX,
                PlayableOriginY,
                PlayableCellSize,
                PlayableWidth,
                PlayableHeight);
            return true;
        }

        public void ClearPlayableBounds()
        {
            PlayableOriginX = 0f;
            PlayableOriginY = 0f;
            PlayableCellSize = 1f;
            PlayableWidth = 0;
            PlayableHeight = 0;
        }

        /// <summary>当前仍在洞内的己方（进洞登记；离开关闭时清空）。</summary>
        public IReadOnlyList<EntityId> OccupantIds => _occupantIds;

        public bool IsInInterior =>
            !string.IsNullOrEmpty(ActiveMapLayoutId) &&
            (HasContinuousOutdoorReturn ||
             (!string.IsNullOrEmpty(OverworldMapLayoutId) &&
              !string.Equals(ActiveMapLayoutId, OverworldMapLayoutId, System.StringComparison.Ordinal)));

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

        public void Clear()
        {
            ActiveMapLayoutId = string.Empty;
            OverworldMapLayoutId = string.Empty;
            ReturnLocationId = string.Empty;
            HasContinuousOutdoorReturn = false;
            ContinuousOutdoorReturnSurfaceId = string.Empty;
            ContinuousOutdoorReturnX = ContinuousOutdoorReturnY = 0f;
            ExitTriggerDepth = 0f;
            ClearPlayableBounds();
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
