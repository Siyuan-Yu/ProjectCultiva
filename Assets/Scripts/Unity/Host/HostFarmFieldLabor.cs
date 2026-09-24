using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Actions;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Schedule;
using XianXia.Core.World.Strategic;

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
            public bool FromPartyFollow;
            public bool RequiresAdministrativeAuthorization;
            public string ActingFactionId;
        }

        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostMoveController moveController;
        [SerializeField] EntityViewSpawner viewSpawner;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] HostWorkLoop workLoop;

        readonly List<Worker> _workers = new List<Worker>(8);
        readonly List<EntityId> _commandWorkers = new List<EntityId>(6);
        readonly HashSet<int> _reserved = new HashSet<int>();
        readonly Dictionary<ulong, float> _npcRetryAt = new Dictionary<ulong, float>();
        ulong _lastPresentedFarmTick = ulong.MaxValue;

        public void Bind(PlayableHostBootstrap host)
        {
            bootstrap = host;
            if (host == null)
                return;
            moveController = host.GetComponent<HostMoveController>();
            viewSpawner = host.ViewSpawner;
            selectionController = host.GetComponent<HostSelectionController>();
            workLoop = host.GetComponent<HostWorkLoop>();
            _lastPresentedFarmTick = ulong.MaxValue;
        }

        public bool IsPartyDerivedFarming(EntityId id)
        {
            for (var i = 0; i < _workers.Count; i++)
            {
                if (_workers[i].Id == id && _workers[i].FromPartyFollow)
                    return true;
            }

            return false;
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

        public bool TryGetFarmLocation(EntityId id, out string locationId)
        {
            locationId = null;
            for (var i = 0; i < _workers.Count; i++)
            {
                if (_workers[i].Id != id)
                    continue;
                locationId = _workers[i].LocationId;
                return !string.IsNullOrEmpty(locationId);
            }

            return false;
        }

        public void StopPartyDerived(EntityId id)
        {
            for (var i = _workers.Count - 1; i >= 0; i--)
            {
                if (_workers[i].Id != id || !_workers[i].FromPartyFollow)
                    continue;
                ReleaseReserve(_workers[i]);
                ClearActivity(_workers[i].Id);
                _workers.RemoveAt(i);
            }
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
        public int BeginForSelection(HostMapPlotCell clickedPlot)
        {
            var world = bootstrap?.Session?.World;
            if (world == null || clickedPlot == null || !clickedPlot.IsPlantableField ||
                string.IsNullOrEmpty(clickedPlot.LocationId) ||
                string.IsNullOrEmpty(clickedPlot.StableCellId) || selectionController == null)
                return 0;

            var actingFactionId = world.Strategic?.PlayerFactionId ?? string.Empty;
            var authorization = WorldAdministrativeAssetWorkAuthorizationService.ResolveForFaction(
                world, clickedPlot.StableCellId, actingFactionId);
            if (!authorization.IsAllowed)
            {
                ToastSelection(DescribeAuthorizationDenial(authorization));
                return 0;
            }

            var party = bootstrap.Session.PlayerParty;
            var resolution = HostPlayerMoveCommandGate.CollectPartyWorkersOrActive(
                world, selectionController, party, _commandWorkers);
            if (_commandWorkers.Count == 0)
            {
                ToastSelectionOrActive(
                    resolution == PartyWorkerCommandResolutionStatus.InvalidNonEmptySelection
                        ? "当前选择不允许下达农作命令"
                        : "当前没有可执行农作的队员");
                return 0;
            }

            var n = 0;
            for (var i = 0; i < _commandWorkers.Count; i++)
            {
                var id = _commandWorkers[i];
                if (Begin(id, clickedPlot.LocationId, fromNpcSchedule: false))
                    n++;
            }

            return n;
        }

        public bool Begin(EntityId id, string locationId, bool fromNpcSchedule = false, bool fromPartyFollow = false)
        {
            if (id.IsNone || string.IsNullOrEmpty(locationId) ||
                !HostFarmFieldRegistry.TryGetPlots(locationId, out _))
                return false;

            var world = bootstrap?.Session?.World;
            if (!AutonomousActionContinuationService.CanContinue(world, id))
                return false;
            var actingFactionId = world?.Strategic?.PlayerFactionId ?? string.Empty;
            if (fromNpcSchedule)
            {
                if (world == null || !world.Entities.TryGet(id, out var npc) ||
                    !npc.TryGet<XianXia.Core.Social.FactionMembershipComponent>(out var membership) ||
                    !membership.IsAffiliated)
                    return false;
                actingFactionId = membership.FactionId;
            }

            for (var i = 0; i < _workers.Count; i++)
            {
                if (_workers[i].Id == id &&
                    string.Equals(_workers[i].LocationId, locationId, System.StringComparison.Ordinal))
                {
                    _workers[i].FromNpcSchedule =
                        fromNpcSchedule || _workers[i].FromNpcSchedule;
                    _workers[i].FromPartyFollow =
                        fromPartyFollow || _workers[i].FromPartyFollow;
                    _workers[i].RequiresAdministrativeAuthorization = true;
                    _workers[i].ActingFactionId = actingFactionId;
                    return true;
                }
            }

            Stop(id);
            if (!fromNpcSchedule && !fromPartyFollow)
                workLoop?.StopLoop(id);

            var w = new Worker
            {
                Id = id,
                LocationId = locationId,
                Phase = Phase.Idle,
                FromNpcSchedule = fromNpcSchedule,
                FromPartyFollow = fromPartyFollow,
                RequiresAdministrativeAuthorization = true,
                ActingFactionId = actingFactionId
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
            SyncFarmPresentationFromCore();
            if (bootstrap.Session.IsPaused)
                return;

            SyncNpcScheduleFarmers();

            if (_workers.Count == 0)
                return;

            for (var i = _workers.Count - 1; i >= 0; i--)
            {
                var w = _workers[i];
                if (!AutonomousActionContinuationService.CanContinue(bootstrap.Session.World, w.Id))
                {
                    ReleaseReserve(w);
                    moveController?.CancelPresentationMovementPublic(w.Id);
                    ClearActivity(w.Id);
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

            if (!IsCellAuthorized(w, w.Cell))
            {
                HandleAuthorizationLoss(w);
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

            if (!ApplyJob(w))
                return;
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

            if (!HostFarmFieldRules.TryPickJobCell(
                    plots, from, _reserved, out var cell,
                    candidate => IsCellAuthorized(w, candidate)))
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

        bool IsCellAuthorized(Worker worker, HostMapPlotCell cell)
        {
            if (worker == null || !worker.RequiresAdministrativeAuthorization)
                return true;
            if (cell == null || string.IsNullOrEmpty(cell.StableCellId))
                return false;
            var world = bootstrap?.Session?.World;
            if (worker.FromNpcSchedule)
            {
                if (world == null || !world.Entities.TryGet(worker.Id, out var npc) ||
                    !npc.TryGet<XianXia.Core.Social.FactionMembershipComponent>(out var membership) ||
                    !membership.IsAffiliated)
                    return false;
                worker.ActingFactionId = membership.FactionId;
            }
            var authorization = WorldAdministrativeAssetWorkAuthorizationService.ResolveForFaction(
                world, cell.StableCellId, worker.ActingFactionId);
            return authorization.IsAllowed;
        }

        void HandleAuthorizationLoss(Worker worker)
        {
            ReleaseReserve(worker);
            worker.Cell = null;
            worker.WorkLeft = 0f;
            if (AssignNextCell(worker))
                return;
            moveController?.CancelPresentationMovementPublic(worker.Id);
            if (!worker.FromNpcSchedule)
                Toast(worker.Id, "已失去该农田的劳作权限，自动农作停止", new Color(1f, .45f, .35f));
            worker.Phase = Phase.Idle;
        }

        bool ApplyJob(Worker w)
        {
            var cell = w.Cell;
            var world = bootstrap.Session.World;
            if (cell == null || !world.Entities.TryGet(w.Id, out var entity))
                return false;
            if (!IsCellAuthorized(w, cell))
            {
                HandleAuthorizationLoss(w);
                return false;
            }

            var isNpc = (entity.Tags & EntityTag.Npc) != 0;
            var verb = HostFarmFieldRules.JobVerb(cell.CropStage);
            switch (cell.CropStage)
            {
                case OutdoorFarmCropStage.Empty:
                    cell.SetPlanted(HostFarmFieldRules.CropIdForPlot(cell));
                    cell.RefreshCropVisual();
                    if (!isNpc)
                        Toast(w.Id, verb + " · " + cell.CropName(), new Color(0.65f, 0.95f, 0.55f));
                    break;
                case OutdoorFarmCropStage.Growing:
                    cell.SetCropStage(OutdoorFarmCropStage.Growing,
                        cell.Growth01 + HostFarmFieldRules.TendGrowthGain);
                    if (cell.Growth01 >= 0.999f)
                        cell.SetCropStage(OutdoorFarmCropStage.Mature, 1f);
                    cell.RefreshCropVisual();
                    if (!isNpc)
                        Toast(w.Id,
                            verb + " · " + Mathf.RoundToInt(cell.Growth01 * 100f) + "%",
                            new Color(0.7f, 0.9f, 0.5f));
                    break;
                case OutdoorFarmCropStage.Mature:
                {
                    var itemId = HostFarmFieldRules.HarvestItemId(cell);
                    var added = GrantHarvest(world, entity, cell, itemId, w.FromNpcSchedule);
                    if (added > 0)
                    {
                        cell.SetCropStage(OutdoorFarmCropStage.Empty);
                        cell.RefreshCropVisual();
                    }
                    Toast(w.Id,
                        added > 0 ? ("收获 · " + ShortItem(itemId)) : "收获失败",
                        new Color(0.95f, 0.85f, 0.4f));
                    bootstrap.DispatchDrainedEvents();
                    break;
                }
                case OutdoorFarmCropStage.Ruined:
                    cell.SetCropStage(OutdoorFarmCropStage.Empty);
                    cell.RefreshCropVisual();
                    if (!isNpc)
                        Toast(w.Id, "清理完毕", new Color(0.8f, 0.8f, 0.75f));
                    break;
            }
            return true;
        }

        static int GrantHarvest(
            XianXia.Core.Simulation.SimulationWorld world,
            Entity entity,
            HostMapPlotCell cell,
            string itemId,
            bool fromNpcSchedule)
        {
            if (world == null || string.IsNullOrEmpty(itemId))
                return 0;

            if (fromNpcSchedule)
            {
                if (entity == null || cell == null || string.IsNullOrEmpty(cell.StableCellId)) return 0;
                var added = WorldSiteFarmHarvestService.TryDepositNpcHarvest(
                    world, entity.Id, cell.StableCellId, itemId);
                return added.IsSuccess ? 1 : 0;
            }

            return world.Inventory != null ? world.Inventory.TryAdd(itemId, 1) : 0;
        }

        void SyncFarmPresentationFromCore()
        {
            var tick = bootstrap.Session.World.Tick.Value;
            if (tick == _lastPresentedFarmTick)
                return;
            _lastPresentedFarmTick = tick;
            var plots = HostMapObjectRegistry.AllPlots;
            for (var i = 0; i < plots.Count; i++)
            {
                var p = plots[i];
                if (p != null && p.IsPlantableField)
                    p.RefreshFromWorldState();
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

        public static string DescribeAuthorizationDenial(AdministrativeAssetWorkAuthorization authorization)
        {
            switch (authorization?.Status ?? AdministrativeAssetWorkAuthorizationStatus.Invalid)
            {
                case AdministrativeAssetWorkAuthorizationStatus.Unmanaged:
                    return "该农田暂无行政管理，无法组织农作";
                case AdministrativeAssetWorkAuthorizationStatus.ManagedByOtherFaction:
                    return "该农田由其他势力管理，无法组织农作";
                case AdministrativeAssetWorkAuthorizationStatus.NotAdministrativeAsset:
                    return "该地块不是可组织管理的农田";
                default:
                    return "无法确认该农田的行政管理";
            }
        }

        void ToastSelection(string text)
        {
            ToastSelectionOrActive(text);
        }

        void ToastSelectionOrActive(string text)
        {
            var party = bootstrap?.Session?.PlayerParty;
            var shown = false;
            if (selectionController != null)
            {
                for (var i = 0; i < selectionController.State.Count; i++)
                {
                    var id = selectionController.State.SelectedIds[i];
                    if (party?.IsMember(id) != true)
                        continue;
                    Toast(id, text, new Color(1f, .45f, .35f));
                    shown = true;
                }
            }
            if (!shown && party?.HasActive == true)
                Toast(party.ActiveCharacterId, text, new Color(1f, .45f, .35f));
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
