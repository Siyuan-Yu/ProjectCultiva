namespace XianXia.Unity.Host
{
    /// <summary>
    /// 模态 UI（任务日志等）打开时阻断相机缩放／平移与 RTS 世界操作。
    /// </summary>
    public static class HostInputGate
    {
        static bool _blockWorldCamera;
        static bool _blockWorldInteraction;

        /// <summary>Encounter owns this independently of other panels' per-frame bool writes.</summary>
        static readonly System.Collections.Generic.HashSet<string> _owners = new System.Collections.Generic.HashSet<string>();
        public static void Acquire(string owner) { if (!string.IsNullOrEmpty(owner)) _owners.Add(owner); }
        public static void Release(string owner) { if (!string.IsNullOrEmpty(owner)) _owners.Remove(owner); }
        public static bool EncounterModalLock
        {
            get => _owners.Contains("CharacterEncounterUI");
            set { if (value) Acquire("CharacterEncounterUI"); else Release("CharacterEncounterUI"); }
        }

        public static bool BlockWorldCamera
        {
            get => _blockWorldCamera || _owners.Count > 0;
            set => _blockWorldCamera = value;
        }

        public static bool BlockWorldInteraction
        {
            get => _blockWorldInteraction || _owners.Count > 0;
            set => _blockWorldInteraction = value;
        }

        public static void Clear()
        {
            _blockWorldCamera = false;
            _blockWorldInteraction = false;
        }

        // Only the explicit session boundary may clear other owners. Ordinary panel Close
        // still calls Clear(), which clears its legacy aggregate without touching named locks.
        public static void ResetSession()
        {
            Clear();
            _owners.Clear();
        }
    }
}
