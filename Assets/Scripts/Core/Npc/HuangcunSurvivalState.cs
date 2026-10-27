using System.Collections.Generic;
using XianXia.Core.World;

namespace XianXia.Core.Npc
{
    // ADR-0061: scenario state, not a second health, faction, or spatial authority.
    public sealed class HuangcunSurvivalState
    {
        public bool Enabled { get; set; }
        public ulong StartedTick { get; set; }
        public ulong LastProcessedTick { get; set; }
        public ulong PrisonerId { get; set; }
        public bool Released { get; set; }
        public string PrisonRoomId { get; set; } = string.Empty;
        public string CampRoomId { get; set; } = string.Empty;
        public string OwnerFactionId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int SupervisorAnger { get; set; }
        public int ObservationSeconds { get; set; }
        public ulong LastObservationTick { get; set; }
        public ulong PunisherId { get; set; }
        public ulong PunishmentTargetId { get; set; }
        public bool PunishmentBattleActive { get; set; }
        public Dictionary<string, WorldVec2> Places { get; } = new Dictionary<string, WorldVec2>();
        public Dictionary<string, int> StoredItems { get; } = new Dictionary<string, int>();
        public HuangcunSurvivalState Copy()
        {
            var s = new HuangcunSurvivalState { Enabled = Enabled, StartedTick = StartedTick,
                LastProcessedTick = LastProcessedTick, PrisonerId = PrisonerId, Released = Released,
                PrisonRoomId = PrisonRoomId, CampRoomId = CampRoomId, OwnerFactionId = OwnerFactionId, Message = Message,
                SupervisorAnger = SupervisorAnger, ObservationSeconds = ObservationSeconds, LastObservationTick = LastObservationTick,
                PunisherId = PunisherId, PunishmentTargetId = PunishmentTargetId, PunishmentBattleActive = PunishmentBattleActive };
            foreach (var p in Places) s.Places.Add(p.Key, p.Value);
            foreach (var p in StoredItems) s.StoredItems.Add(p.Key, p.Value);
            return s;
        }
    }
}
