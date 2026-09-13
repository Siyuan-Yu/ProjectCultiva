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
        public static bool EncounterModalLock { get; set; }

        public static bool BlockWorldCamera
        {
            get => _blockWorldCamera || EncounterModalLock;
            set => _blockWorldCamera = value;
        }

        public static bool BlockWorldInteraction
        {
            get => _blockWorldInteraction || EncounterModalLock;
            set => _blockWorldInteraction = value;
        }

        public static void Clear()
        {
            _blockWorldCamera = false;
            _blockWorldInteraction = false;
            EncounterModalLock = false;
        }
    }
}
