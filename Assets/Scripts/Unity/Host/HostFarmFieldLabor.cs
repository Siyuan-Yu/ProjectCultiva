using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Actions;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Schedule;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// 田区自动农作：整片 location 为一区，按格状态选活、走格、播种／照料／收获／清理。
    /// 玩家交互下令；NPC 在日程 Labor＋农田工区 WorkAction 期间自动接入同一套走格。
    /// </summary>
    public sealed class HostFarmFieldLabor : MonoBehaviour
    {
        enum Phase
        {
            Idle = 0,
            Move = 1,
            Work = 2
        }

        sealed class Worker
        {
            public EntityId Id;
            public string LocationId;
            public HostMapPlotCell Cell;
            public Phase Phase;
            public float WorkLeft;
            public int ReservedCellId;
            public bool FromNpcSchedule;
        }

        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostMoveController moveController;
        [SerializeField] EntityViewSpawner viewSpawner;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] HostWorkLoop workLoop;

        readonly List<Worker> _workers = new List<Worker>(8);
        readonly HashSet<int> _reserved = new HashSet<int>();
        readonly Dictionary<ulong, float> _npcRetryAt = new Dictionary<ulong, float>();

        public void Bind(PlayableHostBootstrap host)
        {
            bootstrap = host;
            if (host == null)
                return;
            moveController = host.GetComponent<HostMoveController>();
            viewSpawner = host.ViewSpawner;
            selectionController = host.GetComponent<HostSelectionController>();
            workLoop = host.GetComponent<HostWorkLoop>();
        }

        public bool IsFarming(EntityId id)
        {
            for (var i = 0; i < _workers.Count; i++)
            {
                if (_workers[i].Id == id)
                    return true;
            }

            return false;
        }

        public void Stop(EntityId id)
        {
            for (var i = _workers.Count - 1; i >= 0; i--)
            {
                if (_workers[i].Id != id)
                    continue;
                ReleaseReserve(_workers[i]);
                ClearActivity(_workers[i].Id);
                _workers.RemoveAt(i);
            }
        }

        public void StopAll()
        {
            for (var i = 0; i < _workers.Count; i++)
            {
                ReleaseReserve(_workers[i]);
                ClearActivity(_workers[i].Id);
            }

            _workers.Clear();
            _reserved.Clear();
        }

        /// <summary>对当前选中己方：在该地点田区开始自动农作。</summary>
        public int BeginForSelection(string locationId)
        {
            if (string.IsNullOrEmpty(locationId) ||
                !HostFarmFieldRegistry.HasField(locationId) ||
                selectionController == null)
                return 0;

            var n = 0;
            for (var i = 0; i < selectionController.State.Count; i++)
            {
                var id = selectionController.State.SelectedIds[i];
                if (!selectionController.IsPartyUnit(id))
                    continue;
                if (Begin(id, locationId, fromNpcSchedule: false))
                    n++;
            }

            return n;
        }

        public bool Begin(EntityId id, string locationId, bool fromNpcSchedule = false)
        {
            if (id.IsNone || string.IsNullOrEmpty(locationId) ||
                !HostFarmFieldRegistry.TryGetPlots(locationId, out _))
                return false;

            for (var i = 0; i < _workers.Count; i++)
            {
                if (_workers[i].Id == id &&
                    string.Equals(_workers[i].LocationId, locationId, System.StringComparison.Ordinal))
                {
                    _workers[i].FromNpcSchedule =
                        fromNpcSchedule || _workers[i].FromNpcSchedule;
                    return true;
                }
            }

            Stop(id);
            if (!fromNpcSchedule)
                workLoop?.StopLoop(id);

            var w = new Worker
            {
                Id = id,
                LocationId = locationId,
                Phase = Phase.Idle,
                FromNpcSchedule = fromNpcSchedule
            };
            _workers.Add(w);
            if (!AssignNextCell(w))
            {
                if (!fromNpcSchedule)
                    Toast(id, "本区暂无农活", new Color(0.9f, 0.85f, 0.55f));
                Stop(id);
                return false;
            }

            return true;
        }

        void Update()
        {
            if (bootstrap?.Session?.World == null)
                return;
            if (bootstrap.Session.IsPaused)
                return;

            TickPassiveGrowth(bootstrap.PresentationDeltaTime);
            SyncNpcScheduleFarmers();

            if (_workers.Count == 0)
                return;

            for (var i = _workers.Count - 1; i >= 0; i--)
            {
                var w = _workers[i];
                if (!bootstrap.Session.World.Entities.TryGet(w.Id, out var ent) ||
                    !ent.TryGet<LifecycleComponent>(out var life) ||
                    life.IsDead || life.IsRemoved)
                {
                    ReleaseReserve(w);
                    _workers.RemoveAt(i);
                    continue;
                }

                TickWorker(w, bootstrap.PresentationDeltaTime);
                if (w.Phase == Phase.Idle)
                {
                    ReleaseReserve(w);
                    ClearActivity(w.Id);
                    _workers.RemoveAt(i);
                }
            }
        }

        void SyncNpcScheduleFarmers()
        {
            var world = bootstrap.Session.World;
            var now = Time.unscaledTime;
            foreach (var entity in world.Entities.All)
            {
                if (entity == null || (entity.Tags & EntityTag.Npc) == 0)
                    continue;
                if ((entity.Tags & EntityTag.Character) != 0)
                    continue;

                var id = entity.Id;
                if (TryResolveNpcFarmLaborLocation(world, entity, out var locId))
                {
                    if (IsFarming(id))
                        continue;
                    if (_npcRetryAt.TryGetValue(id.Value, out var retryAt) && now < retryAt)
                        continue;
                    if (!Begin(id, locId, fromNpcSchedule: true))
                        _npcRetryAt[id.Value] = now + 2.5f;
                    else
                        _npcRetryAt.Remove(id.Value);
                    continue;
                }

                for (var i = 0; i < _workers.Count; i++)
                {
                    if (_workers[i].Id == id && _workers[i].FromNpcSchedule)
                    {
                        Stop(id);
                        break;
                    }
                }
            }
        }

        static bool TryResolveNpcFarmLaborLocation(
            XianXia.Core.Simulation.SimulationWorld world,
            Entity entity,
            out string locationId)
        {
            locationId = null;
            if (world == null || entity == null)
                return false;
            if (!entity.TryGet<ActionStateComponent>(out var actionState) ||
                !actionState.HasActiveAction)
                return false;
            if (!world.ActiveActions.TryGetValue(actionState.ActiveActionId, out var action))
                return false;
            if (!(action is WorkAction work) ||
                work.Activity != ScheduleActivity.Labor ||
                work.Status != ActionStatus.Running)
                return false;
            if (string.IsNullOrEmpty(work.TargetWorkAreaId) ||
                !world.TryGetWorkArea(work.TargetWorkAreaId, out var area) ||
                !HostFarmFieldRules.IsFarmTaggedWorkArea(area) ||
                string.IsNullOrEmpty(area.LocationId) ||
                !HostFarmFieldRegistry.HasField(area.LocationId))
                return false;

            locationId = area.LocationId;
            return true;
        }

        void TickWorker(Worker w, float dt)
        {
            if (w.Cell == null)
            {
                if (!AssignNextCell(w))
                {
                    if (!w.FromNpcSchedule)
                        Toast(w.Id, "本区农作完成", new Color(0.55f, 1f, 0.55f));
                    w.Phase = Phase.Idle;
                }

                return;
            }

            if (w.Phase == Phase.Move)
            {
                if (!TryGetWorldPos(w.Id, out var pos))
                    return;
                var dist = HostFarmFieldRules.XyDistance(pos, w.Cell.transform.position);
                if (dist > HostFarmFieldRules.ArriveEpsilon)
                    return;
                w.Phase = Phase.Work;
                w.WorkLeft = HostFarmFieldRules.WorkSeconds;
                SetActivity(w.Id, HostFarmFieldRules.JobVerb(w.Cell.CropStage) + "中");
                return;
            }

            if (w.Phase != Phase.Work)
                return;

            w.WorkLeft -= dt;
            if (w.WorkLeft > 0f)
                return;

            ApplyJob(w);
            ReleaseReserve(w);
            w.Cell = null;
            if (!AssignNextCell(w))
            {
                if (!w.FromNpcSchedule)
                    Toast(w.Id, "本区农作完成", new Color(0.55f, 1f, 0.55f));
                w.Phase = Phase.Idle;
            }
        }

        bool AssignNextCell(Worker w)
        {
            if (!HostFarmFieldRegistry.TryGetPlots(w.LocationId, out var plots))
                return false;
            if (!TryGetWorldPos(w.Id, out var from))
                from = Vector3.zero;

            if (!HostFarmFieldRules.TryPickJobCell(plots, from, _reserved, out var cell))
                return false;

            w.Cell = cell;
            w.ReservedCellId = cell.GetInstanceID();
            _reserved.Add(w.ReservedCellId);
            w.Phase = Phase.Move;
            w.WorkLeft = 0f;
            SetActivity(w.Id, "前往" + HostFarmFieldRules.JobVerb(cell.CropStage));

            if (moveController != null)
            {
                var dest = cell.transform.position;
                dest.z = HostPresentationSpace.EntityZ;
                moveController.OrderEntityToWorldPoint(
                    w.Id, dest, arriveCommand: null, issueStop: false);
            }

            return true;
        }

        void ApplyJob(Worker w)
        {
            var cell = w.Cell;
            var world = bootstrap.Session.World;
            if (cell == null || !world.Entities.TryGet(w.Id, out var entity))
                return;

            var isNpc = (entity.Tags & EntityTag.Npc) != 0;
            var verb = HostFarmFieldRules.JobVerb(cell.CropStage);
            switch (cell.CropStage)
            {
                case PlotCropStage.Empty:
                    cell.SetPlanted(HostFarmFieldRules.CropIdForPlot(cell));
                    cell.RefreshCropVisual();
                    if (!isNpc)
                        Toast(w.Id, verb + " · " + cell.CropName(), new Color(0.65f, 0.95f, 0.55f));
                    break;
                case PlotCropStage.Growing:
                    cell.SetCropStage(PlotCropStage.Growing,
                        cell.Growth01 + HostFarmFieldRules.TendGrowthGain);
                    if (cell.Growth01 >= 0.999f)
                        cell.SetCropStage(PlotCropStage.Mature, 1f);
                    cell.RefreshCropVisual();
                    if (!isNpc)
                        Toast(w.Id,
                            verb + " · " + Mathf.RoundToInt(cell.Growth01 * 100f) + "%",
                            new Color(0.7f, 0.9f, 0.5f));
                    break;
                case PlotCropStage.Mature:
                {
                    var itemId = HostFarmFieldRules.HarvestItemId(cell);
                    var added = GrantHarvest(world, entity, itemId);
                    cell.SetCropStage(PlotCropStage.Empty);
                    cell.RefreshCropVisual();
                    Toast(w.Id,
                        added > 0 ? ("收获 · " + ShortItem(itemId)) : "收获失败",
                        new Color(0.95f, 0.85f, 0.4f));
                    bootstrap.DispatchDrainedEvents();
                    break;
                }
                case PlotCropStage.Ruined:
                    cell.SetCropStage(PlotCropStage.Empty);
                    cell.RefreshCropVisual();
                    if (!isNpc)
                        Toast(w.Id, "清理完毕", new Color(0.8f, 0.8f, 0.75f));
                    break;
            }
        }

        static int GrantHarvest(
            XianXia.Core.Simulation.SimulationWorld world,
            Entity entity,
            string itemId)
        {
            if (world == null || string.IsNullOrEmpty(itemId))
                return 0;

            if (entity != null && (entity.Tags & EntityTag.Npc) != 0)
            {
                foreach (var kv in world.Settlements.All)
                {
                    var settlement = kv.Value;
                    if (settlement == null)
                        continue;
                    settlement.AddStock(itemId, 1);
                    world.Events.Publish(
                        XianXia.Core.Events.EventType.SettlementStockChanged,
                        world.Tick,
                        actor: entity.Id,
                        payload: settlement.Id + ":" + itemId + ":" + settlement.GetStock(itemId));
                    return 1;
                }
            }

            return world.Inventory != null ? world.Inventory.TryAdd(itemId, 1) : 0;
        }

        void TickPassiveGrowth(float dt)
        {
            var plots = HostMapObjectRegistry.AllPlots;
            for (var i = 0; i < plots.Count; i++)
            {
                var p = plots[i];
                if (p == null || !p.IsPlantableField || p.CropStage != PlotCropStage.Growing)
                    continue;
                var g = p.Growth01 + HostFarmFieldRules.PassiveGrowthPerSecond * dt;
                if (g >= 1f)
                    p.SetCropStage(PlotCropStage.Mature, 1f);
                else
                    p.SetCropStage(PlotCropStage.Growing, g);
                if ((Time.frameCount + i) % 30 == 0)
                    p.RefreshCropVisual();
            }
        }

        void ReleaseReserve(Worker w)
        {
            if (w.ReservedCellId != 0)
                _reserved.Remove(w.ReservedCellId);
            w.ReservedCellId = 0;
        }

        bool TryGetWorldPos(EntityId id, out Vector3 pos)
        {
            pos = default;
            if (viewSpawner != null &&
                viewSpawner.Registry.TryGet(id, out var view) &&
                view != null)
            {
                pos = view.transform.position;
                return true;
            }

            return false;
        }

        void SetActivity(EntityId id, string text)
        {
            if (viewSpawner == null || id.IsNone)
                return;
            if (viewSpawner.Registry.TryGet(id, out var view) && view != null)
                view.SetActivityText(text ?? string.Empty);
        }

        void ClearActivity(EntityId id)
        {
            if (viewSpawner == null || id.IsNone)
                return;
            if (viewSpawner.Registry.TryGet(id, out var view) && view != null &&
                !string.IsNullOrEmpty(view.ActivityText) &&
                (view.ActivityText.IndexOf("农") >= 0 ||
                 view.ActivityText.IndexOf("播种") >= 0 ||
                 view.ActivityText.IndexOf("收获") >= 0 ||
                 view.ActivityText.IndexOf("照料") >= 0 ||
                 view.ActivityText.IndexOf("清理") >= 0 ||
                 view.ActivityText.IndexOf("前往") >= 0))
                view.SetActivityText(string.Empty);
        }

        static string ShortItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return "?";
            if (itemId.IndexOf("spirit_herb", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "灵药";
            if (itemId.IndexOf("grain", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "粮食";
            var i = itemId.LastIndexOf(':');
            return i >= 0 && i + 1 < itemId.Length ? itemId.Substring(i + 1) : itemId;
        }

        void Toast(EntityId id, string text, Color color)
        {
            var overlay = bootstrap != null ? bootstrap.GetComponent<HostFeedbackOverlay>() : null;
            if (overlay == null || viewSpawner == null || id.IsNone)
                return;
            overlay.SpawnAtEntity(viewSpawner, id, text, color);
        }
    }
}
