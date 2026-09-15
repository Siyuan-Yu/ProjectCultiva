using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Core.Settlement;

namespace XianXia.Core.Npc
{
    /// <summary>
    /// Runtime state for a settlement control core (e.g. 主管府).
    /// Session-only; not Snapshot v1.
    /// </summary>
    public sealed class ControlCoreState
    {
        public string WorkAreaId { get; set; } = string.Empty;
        public string LocationId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int MaxDurability { get; set; }
        public int CurrentDurability { get; set; }
        public int Defense { get; set; }
        public float OccupyHoldSeconds { get; set; } = 10f;
        public float OccupyProgressSeconds { get; set; }
        /// <summary>由 authored controlCore placement 派生的固定 Site identity binding。</summary>
        public string BoundWorldSiteId { get; internal set; } = string.Empty;
        public bool CaptureAvailable { get; set; }
        public List<string> GrantsPrivileges { get; } = new List<string>();
    }

    /// <summary>Tracks attackable control cores registered from work areas.</summary>
    public sealed class ControlCoreBoard
    {
        readonly Dictionary<string, ControlCoreState> _byWorkArea =
            new Dictionary<string, ControlCoreState>(System.StringComparer.Ordinal);
        readonly Dictionary<string, string> _workAreaByLocation =
            new Dictionary<string, string>(System.StringComparer.Ordinal);
        readonly Dictionary<string, string> _workAreaByWorldSite =
            new Dictionary<string, string>(System.StringComparer.Ordinal);
        readonly Dictionary<string, PendingRuntimeState> _pendingRuntimeState =
            new Dictionary<string, PendingRuntimeState>(System.StringComparer.Ordinal);

        public IReadOnlyDictionary<string, ControlCoreState> All => _byWorkArea;

        public void Clear()
        {
            _byWorkArea.Clear();
            _workAreaByLocation.Clear();
            _workAreaByWorldSite.Clear();
            _pendingRuntimeState.Clear();
        }

        public void RegisterOrRefresh(WorkAreaDefinition area)
        {
            if (area == null || !area.IsControlCore || string.IsNullOrEmpty(area.Id))
                return;
            var max = area.MaxDurability > 0 ? area.MaxDurability : 100;
            var hold = area.OccupyHoldSeconds > 0.01f ? area.OccupyHoldSeconds : 10f;
            if (_byWorkArea.TryGetValue(area.Id, out var existing))
            {
                existing.Name = area.Name ?? existing.Name;
                existing.LocationId = area.LocationId ?? existing.LocationId;
                existing.MaxDurability = max;
                existing.Defense = area.Defense > 0 ? area.Defense : 0;
                existing.OccupyHoldSeconds = hold;
                existing.GrantsPrivileges.Clear();
                if (area.GrantsPrivileges != null)
                    existing.GrantsPrivileges.AddRange(area.GrantsPrivileges);
                EnsureDefaultPrivileges(existing);
                if (existing.CurrentDurability > max)
                    existing.CurrentDurability = max;
                if (!string.IsNullOrEmpty(existing.LocationId))
                    _workAreaByLocation[existing.LocationId] = area.Id;
                ApplyPendingRuntimeState(existing);
                return;
            }

            var state = new ControlCoreState
            {
                WorkAreaId = area.Id,
                LocationId = area.LocationId ?? string.Empty,
                Name = string.IsNullOrEmpty(area.Name) ? area.Id : area.Name,
                MaxDurability = max,
                CurrentDurability = max,
                Defense = area.Defense > 0 ? area.Defense : 0,
                OccupyHoldSeconds = hold,
                CaptureAvailable = false
            };
            if (area.GrantsPrivileges != null)
                state.GrantsPrivileges.AddRange(area.GrantsPrivileges);
            EnsureDefaultPrivileges(state);
            _byWorkArea[area.Id] = state;
            if (!string.IsNullOrEmpty(state.LocationId))
                _workAreaByLocation[state.LocationId] = area.Id;
            ApplyPendingRuntimeState(state);
        }

        static void EnsureDefaultPrivileges(ControlCoreState state)
        {
            if (state.GrantsPrivileges.Count > 0)
                return;
            state.GrantsPrivileges.Add(SettlementPrivilegeIds.ManageHousing);
            state.GrantsPrivileges.Add(SettlementPrivilegeIds.ManageSchedules);
        }

        public bool TryGet(string workAreaId, out ControlCoreState state)
        {
            state = null;
            return !string.IsNullOrEmpty(workAreaId) && _byWorkArea.TryGetValue(workAreaId, out state);
        }

        public bool TryGetByLocation(string locationId, out ControlCoreState state)
        {
            state = null;
            if (string.IsNullOrEmpty(locationId) ||
                !_workAreaByLocation.TryGetValue(locationId, out var wa) ||
                !_byWorkArea.TryGetValue(wa, out state))
                return false;
            return true;
        }

        public bool ApplyDamage(
            string workAreaId,
            int amount,
            out ControlCoreState state,
            bool defenseAlreadyApplied = false)
        {
            state = null;
            if (!TryGet(workAreaId, out state))
                return false;
            if (amount < 1)
                amount = 1;
            if (!defenseAlreadyApplied)
            {
                amount -= state.Defense;
                if (amount < 1)
                    amount = 1;
            }

            state.CurrentDurability -= amount;
            if (state.CurrentDurability > 0)
                return false;
            state.CurrentDurability = 0;
            state.CaptureAvailable = true;
            return true;
        }

        public void AddOccupyProgress(string workAreaId, float seconds, out ControlCoreState state)
        {
            state = null;
            if (!TryGet(workAreaId, out state) || !state.CaptureAvailable)
                return;
            if (seconds <= 0f)
                return;
            state.OccupyProgressSeconds += seconds;
            if (state.OccupyProgressSeconds > state.OccupyHoldSeconds)
                state.OccupyProgressSeconds = state.OccupyHoldSeconds;
        }

        public void ResetOccupyProgress(string workAreaId)
        {
            if (TryGet(workAreaId, out var state))
                state.OccupyProgressSeconds = 0f;
        }

        public bool TryCapture(string workAreaId, out ControlCoreState state)
        {
            state = null;
            if (!TryGet(workAreaId, out state))
                return false;
            if (!state.CaptureAvailable || state.CurrentDurability > 0)
                return false;
            if (state.OccupyProgressSeconds + 0.001f < state.OccupyHoldSeconds)
                return false;
            return true;
        }

        public Result BindWorldSite(string workAreaId, string siteId)
        {
            if (string.IsNullOrWhiteSpace(workAreaId) || string.IsNullOrWhiteSpace(siteId) ||
                !TryGet(workAreaId, out var core) || core == null)
                return Result.Failure(ErrorCode.InvalidArgument, "ControlCore binding requires an existing core and SiteId.");
            if (!string.IsNullOrEmpty(core.BoundWorldSiteId) &&
                !string.Equals(core.BoundWorldSiteId, siteId, System.StringComparison.Ordinal))
                return Result.Failure(ErrorCode.InvalidOperation, "ControlCore is already bound to another WorldSite.", workAreaId);
            if (_workAreaByWorldSite.TryGetValue(siteId, out var existingWorkAreaId) &&
                !string.Equals(existingWorkAreaId, workAreaId, System.StringComparison.Ordinal))
                return Result.Failure(ErrorCode.InvalidOperation, "Fixed WorldSite already has another ControlCore.", siteId);
            core.BoundWorldSiteId = siteId;
            _workAreaByWorldSite[siteId] = workAreaId;
            return Result.Success();
        }

        public bool TryGetBoundSiteId(string workAreaId, out string siteId)
        {
            siteId = string.Empty;
            if (!TryGet(workAreaId, out var core) || core == null || string.IsNullOrEmpty(core.BoundWorldSiteId))
                return false;
            siteId = core.BoundWorldSiteId;
            return true;
        }

        public bool TryGetByWorldSite(string siteId, out ControlCoreState state)
        {
            state = null;
            return !string.IsNullOrEmpty(siteId) &&
                   _workAreaByWorldSite.TryGetValue(siteId, out var workAreaId) &&
                   TryGet(workAreaId, out state);
        }

        public Result RestoreRuntimeState(string workAreaId, int currentDurability,
            float occupyProgressSeconds, bool legacyCompleted = false)
        {
            if (string.IsNullOrWhiteSpace(workAreaId) || currentDurability < 0 ||
                float.IsNaN(occupyProgressSeconds) || float.IsInfinity(occupyProgressSeconds) || occupyProgressSeconds < 0f)
                return Result.Failure(ErrorCode.SnapshotInvalid, "Invalid ControlCore runtime snapshot.", workAreaId ?? string.Empty);
            var pending = new PendingRuntimeState(currentDurability, occupyProgressSeconds, legacyCompleted);
            if (TryGet(workAreaId, out var core) && core != null)
                ApplyRuntimeState(core, pending);
            else
                _pendingRuntimeState[workAreaId] = pending;
            return Result.Success();
        }

        public void PrepareRuntimeRestore()
        {
            _pendingRuntimeState.Clear();
            foreach (var pair in _byWorkArea)
            {
                var core = pair.Value;
                if (core == null) continue;
                core.CurrentDurability = System.Math.Max(1, core.MaxDurability);
                core.OccupyProgressSeconds = 0f;
                core.CaptureAvailable = false;
            }
        }

        void ApplyPendingRuntimeState(ControlCoreState core)
        {
            if (core == null || !_pendingRuntimeState.TryGetValue(core.WorkAreaId, out var pending))
                return;
            _pendingRuntimeState.Remove(core.WorkAreaId);
            ApplyRuntimeState(core, pending);
        }

        static void ApplyRuntimeState(ControlCoreState core, PendingRuntimeState pending)
        {
            if (pending.LegacyCompleted)
            {
                core.CurrentDurability = System.Math.Max(1, core.MaxDurability);
                core.OccupyProgressSeconds = 0f;
                core.CaptureAvailable = false;
                return;
            }
            core.CurrentDurability = System.Math.Min(System.Math.Max(1, core.MaxDurability), pending.CurrentDurability);
            core.OccupyProgressSeconds = System.Math.Min(
                pending.OccupyProgressSeconds, System.Math.Max(.1f, core.OccupyHoldSeconds));
            core.CaptureAvailable = core.CurrentDurability <= 0;
        }

        readonly struct PendingRuntimeState
        {
            public PendingRuntimeState(int currentDurability, float occupyProgressSeconds, bool legacyCompleted)
            { CurrentDurability = currentDurability; OccupyProgressSeconds = occupyProgressSeconds; LegacyCompleted = legacyCompleted; }
            public int CurrentDurability { get; }
            public float OccupyProgressSeconds { get; }
            public bool LegacyCompleted { get; }
        }

        /// <summary>政治 Transfer 成功后恢复新 Owner 的建筑物理状态。</summary>
        public void ResetAfterCapture(string workAreaId, out ControlCoreState state)
        {
            state = null;
            if (!TryGet(workAreaId, out state))
                return;
            state.CurrentDurability = System.Math.Max(1, state.MaxDurability);
            state.CaptureAvailable = false;
            state.OccupyProgressSeconds = 0f;
        }
    }
}
