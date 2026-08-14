using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
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
        public bool PlayerControlled { get; set; }
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

        public IReadOnlyDictionary<string, ControlCoreState> All => _byWorkArea;

        public void Clear()
        {
            _byWorkArea.Clear();
            _workAreaByLocation.Clear();
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
                PlayerControlled = false,
                CaptureAvailable = false
            };
            if (area.GrantsPrivileges != null)
                state.GrantsPrivileges.AddRange(area.GrantsPrivileges);
            EnsureDefaultPrivileges(state);
            _byWorkArea[area.Id] = state;
            if (!string.IsNullOrEmpty(state.LocationId))
                _workAreaByLocation[state.LocationId] = area.Id;
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

        public bool ApplyDamage(string workAreaId, int amount, out ControlCoreState state)
        {
            state = null;
            if (!TryGet(workAreaId, out state) || state.PlayerControlled)
                return false;
            if (amount < 1)
                amount = 1;
            amount -= state.Defense;
            if (amount < 1)
                amount = 1;
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
            if (!TryGet(workAreaId, out state) || state.PlayerControlled || !state.CaptureAvailable)
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
            if (state.PlayerControlled)
                return true;
            if (!state.CaptureAvailable || state.CurrentDurability > 0)
                return false;
            if (state.OccupyProgressSeconds + 0.001f < state.OccupyHoldSeconds)
                return false;
            state.PlayerControlled = true;
            state.CaptureAvailable = false;
            state.OccupyProgressSeconds = state.OccupyHoldSeconds;
            state.CurrentDurability = state.MaxDurability;
            return true;
        }

        public bool AnyPlayerControlled()
        {
            foreach (var kv in _byWorkArea)
            {
                if (kv.Value.PlayerControlled)
                    return true;
            }

            return false;
        }
    }
}
