using System;
using UnityEngine;

namespace XianXia.Unity.Time
{
    /// <summary>
    /// Demo 统一时间推进。逻辑层只认游戏分钟与 Tick；表现层可读取日／时／分。
    /// </summary>
    public sealed class GameClock : MonoBehaviour
    {
        public const int MinutesPerDay = 24 * 60;
        public const int MinutesPerTick = 15;
        public const int TicksPerDay = MinutesPerDay / MinutesPerTick;

        [SerializeField]
        [Tooltip("一个游戏日对应的现实分钟数。Demo 默认 8，可调 5～10。")]
        private float realMinutesPerGameDay = 8f;

        [SerializeField] private float timeScale = 1f;
        [SerializeField] private int dayNumber = 1;
        [SerializeField] private int startHour = 6;
        [SerializeField] private int startMinute;

        private float _gameMinutesOfDay;
        private float _previousTimeScale = 1f;

        public static GameClock Instance { get; private set; }

        public event Action<int> DayStarted;
        public event Action TimeScaleChanged;

        public float RealMinutesPerGameDay
        {
            get => realMinutesPerGameDay;
            set => realMinutesPerGameDay = Mathf.Clamp(value, 5f, 10f);
        }

        public float TimeScale => timeScale;
        public bool IsPaused => Mathf.Approximately(timeScale, 0f);
        public int DayNumber => dayNumber;
        public float GameMinutesOfDay => _gameMinutesOfDay;
        public int Hour => Mathf.FloorToInt(_gameMinutesOfDay / 60f) % 24;
        public int Minute => Mathf.FloorToInt(_gameMinutesOfDay) % 60;
        public int TickOfDay => Mathf.FloorToInt(_gameMinutesOfDay / MinutesPerTick) % TicksPerDay;

        /// <summary>受暂停与倍速影响的现实帧间隔，供移动等表现使用。</summary>
        public float ScaledDeltaTime => UnityEngine.Time.unscaledDeltaTime * timeScale;

        /// <summary>本帧推进的游戏分钟数。</summary>
        public float DeltaGameMinutes
        {
            get
            {
                float realSecondsPerGameDay = RealMinutesPerGameDay * 60f;
                float gameMinutesPerRealSecond = MinutesPerDay / realSecondsPerGameDay;
                return UnityEngine.Time.unscaledDeltaTime * timeScale * gameMinutesPerRealSecond;
            }
        }

        public string FormattedClock => $"{Hour:00}:{Minute:00}";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            realMinutesPerGameDay = Mathf.Clamp(realMinutesPerGameDay, 5f, 10f);
            _gameMinutesOfDay = startHour * 60f + startMinute;
            if (_previousTimeScale <= 0f)
            {
                _previousTimeScale = 1f;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            float delta = DeltaGameMinutes;
            if (delta <= 0f)
            {
                return;
            }

            _gameMinutesOfDay += delta;
            while (_gameMinutesOfDay >= MinutesPerDay)
            {
                _gameMinutesOfDay -= MinutesPerDay;
                dayNumber++;
                DayStarted?.Invoke(dayNumber);
            }
        }

        public void SetPaused(bool paused)
        {
            if (paused)
            {
                if (!IsPaused)
                {
                    _previousTimeScale = timeScale <= 0f ? 1f : timeScale;
                }

                SetTimeScale(0f);
            }
            else
            {
                SetTimeScale(_previousTimeScale <= 0f ? 1f : _previousTimeScale);
            }
        }

        public void TogglePause()
        {
            SetPaused(!IsPaused);
        }

        public void SetTimeScale(float scale)
        {
            float normalized = scale switch
            {
                <= 0f => 0f,
                <= 1.5f => 1f,
                <= 3.5f => 2f,
                _ => 5f
            };

            if (Mathf.Approximately(timeScale, normalized))
            {
                return;
            }

            if (normalized > 0f)
            {
                _previousTimeScale = normalized;
            }

            timeScale = normalized;
            TimeScaleChanged?.Invoke();
        }

        public void Configure(float realMinutes, float initialScale, int day, int hour, int minute)
        {
            RealMinutesPerGameDay = realMinutes;
            dayNumber = Mathf.Max(1, day);
            _gameMinutesOfDay = Mathf.Clamp(hour, 0, 23) * 60f + Mathf.Clamp(minute, 0, 59);
            SetTimeScale(initialScale);
        }
    }
}
