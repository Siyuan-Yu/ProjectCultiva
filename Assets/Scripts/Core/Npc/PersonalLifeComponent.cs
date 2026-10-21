using System;
using System.Collections.Generic;
using XianXia.Core.Entities;
using XianXia.Core.World;

namespace XianXia.Core.Npc
{
    public enum PersonalLifePreference { Cultivation = 0, Balanced = 1, Recreation = 2 }
    public enum PersonalLifeActivity { None = 0, Cultivating = 1, Resting = 2, Recreational = 3, FetchingFood = 4, Eating = 5 }

    /// <summary>Persistent needs and preference of an individual non-civilian Character.</summary>
    public sealed class PersonalLifeComponent : IComponent
    {
        public PersonalLifePreference Preference { get; set; } = PersonalLifePreference.Balanced;
        public PersonalLifeActivity Activity { get; set; }
        public int Satiety { get; set; } = 75;
        public int Energy { get; set; } = 75;
        public int CurrentMood { get; set; } = 75;
        public ulong LastProcessedTick { get; set; }
        public ulong LastCultivationTick { get; set; }
        public ulong LastRecreationTick { get; set; }
        public string FoodRoomId { get; set; } = string.Empty;
        public int FoodAccessAttempt { get; set; }
        public ulong FoodRetryAfterTick { get; set; }
        public ulong BedRetryAfterTick { get; set; }
        public string MeditationSpotId { get; set; } = string.Empty;
        public bool MeditationTraveling { get; set; }
        public string Status { get; set; } = string.Empty;

        public void Clamp()
        {
            Satiety = Clamp100(Satiety);
            Energy = Clamp100(Energy);
            CurrentMood = Clamp100(CurrentMood);
        }

        static int Clamp100(int value) => value < 0 ? 0 : value > 100 ? 100 : value;
    }

    /// <summary>Immutable authored meditation position; rehydrated from Surface content, not saved.</summary>
    public sealed class MeditationSpot
    {
        public string Id { get; set; } = string.Empty;
        public string SiteId { get; set; } = string.Empty;
        public string SurfaceId { get; set; } = string.Empty;
        public WorldVec2 Position { get; set; }
    }

    public sealed class MeditationSpotBoard
    {
        readonly Dictionary<string, MeditationSpot> _spots =
            new Dictionary<string, MeditationSpot>(StringComparer.Ordinal);
        public IReadOnlyDictionary<string, MeditationSpot> Spots => _spots;
        public void Clear() => _spots.Clear();
        public bool Register(MeditationSpot spot)
        {
            if (spot == null || string.IsNullOrWhiteSpace(spot.Id) ||
                string.IsNullOrWhiteSpace(spot.SiteId) || string.IsNullOrWhiteSpace(spot.SurfaceId) ||
                _spots.ContainsKey(spot.Id)) return false;
            _spots.Add(spot.Id, spot);
            return true;
        }
    }
}
